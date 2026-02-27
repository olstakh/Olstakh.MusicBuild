using System.Diagnostics;
using MusicBuild.Mapping;
using MusicBuild.Midi;
using MusicBuild.Music;

namespace MusicBuild.Composition;

/// <summary>
/// Converts MSBuild build events into <see cref="NoteEvent"/>s by mapping build
/// structure (projects, targets, tasks) onto musical concepts (keys, scale degrees, rhythm).
/// <para>
/// This is the creative core: it decides what the build "sounds like."
/// Swap this out for a different genre/style implementation in the future.
/// </para>
/// <para>
/// Musical design principles:
/// <list type="bullet">
///   <item>Drum loop: four-on-the-floor kick pattern provides rhythmic backbone</item>
///   <item>Bass line: root–fifth alternation follows harmonic progression</item>
///   <item>Pad chords: sustained triads give harmonic context to melody notes</item>
///   <item>Stepwise motion: melody moves by small intervals, not random jumps</item>
///   <item>Note spacing: minimum delay between melody notes prevents clusters</item>
///   <item>Thinning: not every MSBuild event produces a note (tasks are filtered)</item>
///   <item>Arpeggiation: chords spread in time instead of slamming simultaneously</item>
/// </list>
/// </para>
/// </summary>
internal sealed class BuildMusicComposer : IDisposable
{
    private readonly MusicConfiguration _config;
    private readonly Scale _scale;
    private readonly List<NoteEvent> _events = new();
    private readonly IMidiOutput? _liveOutput;
    private readonly Dictionary<string, MusicalKey> _projectKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _targetStartTimes = new(StringComparer.OrdinalIgnoreCase);

    private DateTime _buildStartTime;
    private DateTime _lastEventTimestamp;
    private MusicalKey _currentKey;
    private bool _instrumentsInitialized;

    /// <summary>
    /// Current melody degree — moves by step for smooth melodic motion.
    /// </summary>
    private int _currentMelodyDegree;

    /// <summary>
    /// Tick of the last melody note — used to enforce minimum spacing.
    /// </summary>
    private long _lastMelodyTick = -1;

    /// <summary>
    /// Counter for incoming task events — used to thin out the note density.
    /// Only every Nth task produces a note.
    /// </summary>
    private int _taskCounter;

    /// <summary>
    /// Key changes over time, used by the pattern generator to follow harmonic progression.
    /// </summary>
    private readonly List<(long Tick, MusicalKey Key)> _keyChanges = [];

    /// <summary>
    /// Cancellation source for the live drum/bass background loop.
    /// </summary>
    private CancellationTokenSource? _liveDrumCts;

    /// <summary>
    /// The background task running the live drum/bass loop.
    /// </summary>
    private Task? _liveDrumTask;

    /// <summary>
    /// Whether the live drum loop has been started (idempotent guard).
    /// </summary>
    private bool _liveDrumStarted;

    /// <summary>
    /// Minimum ticks between melody notes (half note = 2 beats).
    /// Keeps the melody sparse so notes don't pile up.
    /// </summary>
    private int MinMelodySpacingTicks => _config.TicksPerQuarterNote * 2;

    /// <summary>
    /// Only every Nth task gets a note — keeps things very sparse.
    /// </summary>
    private const int TaskThinningFactor = 8;

    /// <summary>
    /// Tick spacing between arpeggiated chord notes (a 16th note apart).
    /// </summary>
    private int ArpeggioSpacingTicks => _config.TicksPerQuarterNote / 4;

    /// <summary>
    /// Half-note grid for snapping melody notes to strong beats.
    /// Notes that land on beat 1 or 3 of a bar sound intentional, not random.
    /// </summary>
    private int HalfNoteGrid => _config.TicksPerQuarterNote * 2;

    /// <summary>
    /// Chord tones of the triad (root, 3rd, 5th). Melody gravitates toward these
    /// because they are consonant with the pad and bass.
    /// </summary>
    private static readonly int[] ChordToneDegrees = { 0, 2, 4 };

    internal BuildMusicComposer(MusicConfiguration config, IMidiOutput? liveOutput = null)
    {
        _config = config;
        _scale = Scale.FromType(config.ScaleType);
        _liveOutput = liveOutput;
    }

    /// <summary>
    /// All note events accumulated so far.
    /// </summary>
    internal IReadOnlyList<NoteEvent> Events => _events;

    // ── Build event handlers ─────────────────────────────────────────────

    internal void OnProjectStarted(string projectPath, DateTime timestamp)
    {
        if (_buildStartTime == default)
        {
            _buildStartTime = timestamp;
            _lastEventTimestamp = timestamp;
        }

        PaceToTimestamp(timestamp);
        EnsureInstrumentsInitialized();
        StartLiveDrumLoop();

        _currentKey = MusicalMapping.ProjectToKey(projectPath);
        _projectKeys[projectPath] = _currentKey;

        var tick = ToTick(timestamp);
        _keyChanges.Add((tick, _currentKey));

        // Pad chord establishes harmonic context (root + 3rd + 5th)
        // Placed one octave below melody so they don't compete in the same register
        var padDuration = _config.TicksPerQuarterNote * 16; // 4 bars
        foreach (var degree in (int[])new[] { 0, 2, 4 })
        {
            EmitNote(new NoteEvent
            {
                MidiNoteNumber = _scale.DegreeToMidiNote(degree, _currentKey, _config.BaseOctave - 1),
                Velocity = 40,
                Channel = MidiConstants.PadChannel,
                StartTick = tick,
                DurationTicks = padDuration,
            });
        }

        // Reset melody to root on new project for grounded feel
        _currentMelodyDegree = 0;
    }

    internal void OnProjectFinished(string projectPath, bool succeeded, DateTime timestamp)
    {
        PaceToTimestamp(timestamp);

        if (!succeeded)
        {
            // Dissonant low pad on project failure — unsettling harmonic shift
            var key = _projectKeys.TryGetValue(projectPath, out var k) ? k : _currentKey;
            var tick = ToTick(timestamp);

            EmitNote(new NoteEvent
            {
                MidiNoteNumber = _scale.DegreeToMidiNote(-1, key, _config.BaseOctave - 1),
                Velocity = MidiConstants.HighVelocity,
                Channel = MidiConstants.PadChannel,
                StartTick = tick,
                DurationTicks = _config.TicksPerQuarterNote * 4,
            });
        }
    }

    internal void OnTargetStarted(string targetName, string projectPath, DateTime timestamp)
    {
        PaceToTimestamp(timestamp);
        _targetStartTimes[targetName] = timestamp;

        var rawTick = ToTick(timestamp);

        // Snap to half-note grid so melody lands on strong beats (1 or 3)
        var tick = SnapToGrid(rawTick);

        // Enforce minimum spacing — skip if too close to last melody note
        if (!HasMinimumSpacing(tick))
        {
            return;
        }

        var targetDegree = MusicalMapping.TargetToScaleDegree(targetName);

        // Stepwise motion toward the target, then snap to nearest chord tone
        _currentMelodyDegree = StepToward(_currentMelodyDegree, targetDegree, maxStep: 2);
        _currentMelodyDegree = SnapToChordTone(_currentMelodyDegree);

        var key = _projectKeys.TryGetValue(projectPath, out var k) ? k : _currentKey;

        EmitNote(new NoteEvent
        {
            MidiNoteNumber = _scale.DegreeToMidiNote(_currentMelodyDegree, key, _config.BaseOctave),
            Velocity = MelodyVelocity(),
            Channel = MidiConstants.MelodyChannel,
            StartTick = tick,
            DurationTicks = _config.TicksPerQuarterNote * 2, // half note — let it sing
        });

        _lastMelodyTick = tick;
    }

    internal void OnTargetFinished(string targetName, bool succeeded, DateTime timestamp)
    {
        PaceToTimestamp(timestamp);
        if (!succeeded)
        {
            var tick = ToTick(timestamp);

            // Dissonant drop on failure — step down and play a short, loud note
            _currentMelodyDegree -= 1;

            EmitNote(new NoteEvent
            {
                MidiNoteNumber = _scale.DegreeToMidiNote(_currentMelodyDegree, _currentKey, _config.BaseOctave),
                Velocity = MidiConstants.HighVelocity,
                Channel = MidiConstants.MelodyChannel,
                StartTick = tick,
                DurationTicks = _config.TicksPerQuarterNote / 2, // eighth note
            });

            _lastMelodyTick = tick;
        }

        _targetStartTimes.Remove(targetName);
    }

    internal void OnTaskStarted(string taskName, DateTime timestamp)
    {
        // Thin out tasks aggressively — only every Nth task produces a note
        _taskCounter++;
        if (_taskCounter % TaskThinningFactor != 0)
        {
            return;
        }

        PaceToTimestamp(timestamp);
        var rawTick = ToTick(timestamp);
        var tick = SnapToGrid(rawTick);

        if (!HasMinimumSpacing(tick))
        {
            return;
        }

        // Gentle step from current position, then snap to chord tone
        var direction = MusicalMapping.TaskToScaleDegreeOffset(taskName) >= 3 ? 1 : -1;
        _currentMelodyDegree += direction;
        _currentMelodyDegree = WrapDegree(_currentMelodyDegree, 0, 7);
        _currentMelodyDegree = SnapToChordTone(_currentMelodyDegree);

        EmitNote(new NoteEvent
        {
            MidiNoteNumber = _scale.DegreeToMidiNote(_currentMelodyDegree, _currentKey, _config.BaseOctave),
            Velocity = MelodyVelocity(),
            Channel = MidiConstants.MelodyChannel,
            StartTick = tick,
            DurationTicks = _config.TicksPerQuarterNote, // quarter note — lighter than targets
        });

        _lastMelodyTick = tick;
    }

    internal void OnWarningRaised(DateTime timestamp)
    {
        PaceToTimestamp(timestamp);
        var tick = ToTick(timestamp);

        // Open hi-hat for warnings — attention-getting but not alarming
        EmitNote(new NoteEvent
        {
            MidiNoteNumber = MidiConstants.OpenHiHat,
            Velocity = MidiConstants.HighVelocity,
            Channel = MidiConstants.PercussionChannel,
            StartTick = tick,
            DurationTicks = _config.TicksPerQuarterNote / 4,
        });
    }

    internal void OnErrorRaised(DateTime timestamp)
    {
        PaceToTimestamp(timestamp);
        var tick = ToTick(timestamp);

        // Crash cymbal + bass drum for errors — dramatic impact
        EmitNote(new NoteEvent
        {
            MidiNoteNumber = MidiConstants.CrashCymbal,
            Velocity = MidiConstants.MaxVelocity,
            Channel = MidiConstants.PercussionChannel,
            StartTick = tick,
            DurationTicks = _config.TicksPerQuarterNote,
        });
        EmitNote(new NoteEvent
        {
            MidiNoteNumber = MidiConstants.BassDrum,
            Velocity = MidiConstants.MaxVelocity,
            Channel = MidiConstants.PercussionChannel,
            StartTick = tick,
            DurationTicks = _config.TicksPerQuarterNote / 2,
        });
    }

    internal void OnBuildFinished(bool succeeded, DateTime timestamp)
    {
        PaceToTimestamp(timestamp);
        var tick = ToTick(timestamp);

        // Generate the rhythmic backbone covering the entire build for MIDI file output
        if (_config.EnableDrums)
        {
            _events.AddRange(
                BackgroundPatternGenerator.GenerateDrumPattern(0, tick, _config.TicksPerQuarterNote));
        }

        if (_config.EnableBassLine)
        {
            _events.AddRange(
                BackgroundPatternGenerator.GenerateBassLine(
                    0, tick, _keyChanges, _scale, _config.BaseOctave - 1, _config.TicksPerQuarterNote));
        }

        if (succeeded)
        {
            // Arpeggiated major-ish resolution: root → 3rd → 5th → octave
            // Spread across time so it sounds like a flourish, not a cluster
            EmitArpeggiatedChord(tick, _currentKey, new[] { 0, 2, 4, 7 }, _config.BaseOctave,
                MidiConstants.HighVelocity, _config.TicksPerQuarterNote * 3);
        }
        else
        {
            // Low, dark cluster for failure + crash
            EmitArpeggiatedChord(tick, _currentKey, new[] { 0, 1, -1 }, _config.BaseOctave - 1,
                MidiConstants.MaxVelocity, _config.TicksPerQuarterNote * 3);

            EmitNote(new NoteEvent
            {
                MidiNoteNumber = MidiConstants.CrashCymbal,
                Velocity = MidiConstants.MaxVelocity,
                Channel = MidiConstants.PercussionChannel,
                StartTick = tick,
                DurationTicks = _config.TicksPerQuarterNote * 2,
            });
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        StopLiveDrumLoop();
        _liveOutput?.Dispose();
    }

    // ── Private helpers ──────────────────────────────────────────────────

    private long ToTick(DateTime timestamp)
        => MusicalMapping.TimestampToTick(timestamp, _buildStartTime, _config.BeatsPerMinute, _config.TicksPerQuarterNote);

    /// <summary>
    /// When pacing is enabled, sleeps the wall-clock delta between events
    /// (scaled by <see cref="MusicConfiguration.Speed"/>).
    /// This makes binlog replay sound like the original build.
    /// </summary>
    private void PaceToTimestamp(DateTime timestamp)
    {
        if (!_config.Pace || _liveOutput is null || _lastEventTimestamp == default)
        {
            _lastEventTimestamp = timestamp;
            return;
        }

        var delta = timestamp - _lastEventTimestamp;
        _lastEventTimestamp = timestamp;

        if (delta > TimeSpan.Zero)
        {
            var sleepMs = (int)(delta.TotalMilliseconds / _config.Speed);
            if (sleepMs > 0)
            {
                Thread.Sleep(sleepMs);
            }
        }
    }

    /// <summary>
    /// Starts a background thread that plays a drum beat and bass line through
    /// the live MIDI output. Idempotent — only starts once.
    /// </summary>
    private void StartLiveDrumLoop()
    {
        if (_liveOutput is null || _liveDrumStarted || (!_config.EnableDrums && !_config.EnableBassLine))
        {
            return;
        }

        _liveDrumStarted = true;
        _liveDrumCts = new CancellationTokenSource();
        _liveDrumTask = Task.Run(() => LiveDrumBassLoop(_liveDrumCts.Token));
    }

    /// <summary>
    /// Stops the background drum loop and waits briefly for it to exit.
    /// </summary>
    private void StopLiveDrumLoop()
    {
        if (_liveDrumCts is null)
        {
            return;
        }

        _liveDrumCts.Cancel();
#pragma warning disable CA1031 // Must not let exceptions escape cleanup
        try
        {
            _ = _liveDrumTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Task may have faulted — safe to ignore during cleanup
        }
#pragma warning restore CA1031
        _liveDrumCts.Dispose();
        _liveDrumCts = null;
        _liveDrumTask = null;
    }

    /// <summary>
    /// Background loop that plays a four-on-the-floor drum beat and bass notes
    /// through the live MIDI output. Uses <see cref="Stopwatch"/> for timing accuracy.
    /// </summary>
    private void LiveDrumBassLoop(CancellationToken ct)
    {
        var output = _liveOutput!;
        var sw = Stopwatch.StartNew();
        var eighthNoteMs = 60_000.0 / _config.BeatsPerMinute / 2;
        var eighthCount = 0;

        while (!ct.IsCancellationRequested)
        {
            var targetMs = (long)(eighthCount * eighthNoteMs);
            var sleepMs = (int)(targetMs - sw.ElapsedMilliseconds);
            if (sleepMs > 1)
            {
                Thread.Sleep(sleepMs);
            }

            if (ct.IsCancellationRequested)
            {
                break;
            }

            var posInBar = eighthCount % 8;
            var isOnBeat = posInBar % 2 == 0;
            var beatNumber = posInBar / 2;
            var noteLen = (int)eighthNoteMs;

            if (_config.EnableDrums)
            {
                // Four-on-the-floor kick
                if (isOnBeat)
                {
                    output.PlayNote(MidiConstants.PercussionChannel, MidiConstants.BassDrum,
                        MidiConstants.MediumVelocity, noteLen);
                }

                // Snare on beats 2 and 4
                if (isOnBeat && beatNumber is 1 or 3)
                {
                    output.PlayNote(MidiConstants.PercussionChannel, MidiConstants.SnareDrum,
                        70, noteLen);
                }

                // Closed hi-hat on every 8th note
                output.PlayNote(MidiConstants.PercussionChannel, MidiConstants.ClosedHiHat,
                    isOnBeat ? 45 : 30, noteLen / 2);
            }

            // Bass on beats 1 and 3 — follows current key
            if (_config.EnableBassLine && isOnBeat && beatNumber is 0 or 2)
            {
                var key = _currentKey;
                var degree = beatNumber == 0 ? 0 : 4;
                var bassNote = _scale.DegreeToMidiNote(degree, key, _config.BaseOctave - 1);
                output.PlayNote(MidiConstants.BassChannel, bassNote,
                    MidiConstants.MediumVelocity, (int)(eighthNoteMs * 3));
            }

            eighthCount++;
        }
    }

    /// <summary>
    /// Returns <c>true</c> if enough time has passed since the last melody note.
    /// </summary>
    private bool HasMinimumSpacing(long tick)
        => _lastMelodyTick < 0 || (tick - _lastMelodyTick) >= MinMelodySpacingTicks;

    /// <summary>
    /// Snaps a tick to the nearest half-note grid position.
    /// This makes melody notes land on strong beats (beats 1 and 3 of each bar)
    /// instead of at arbitrary positions, which sounds rhythmically intentional.
    /// </summary>
    private long SnapToGrid(long tick)
    {
        var grid = HalfNoteGrid;
        return grid > 0 ? ((tick + (grid / 2)) / grid) * grid : tick;
    }

    /// <summary>
    /// Snaps a scale degree to the nearest chord tone (0, 2, or 4).
    /// Melody notes that land on chord tones are consonant with the pad and bass,
    /// while non-chord tones create brief dissonance that hurts the ear.
    /// </summary>
    private static int SnapToChordTone(int degree)
    {
        // Chord tones: 0, 2, 4. Map each degree to the nearest:
        // 0→0, 1→0, 2→2, 3→2 or 4, 4→4, 5→4, 6→4 (or wrap to 0)
        return degree switch
        {
            <= 0 => 0,
            1 => 0,
            2 => 2,
            3 => 4, // resolve up to the 5th rather than down
            4 => 4,
            5 => 4,
            _ => 4, // 6+ → 5th, next note will naturally resolve to root
        };
    }

    /// <summary>
    /// Produces gentle velocity variation so the melody doesn't sound mechanical.
    /// Alternates between two levels based on the task counter.
    /// </summary>
    private int MelodyVelocity()
        => (_taskCounter % 2 == 0) ? MidiConstants.MediumVelocity : MidiConstants.MediumVelocity - 15;

    /// <summary>
    /// Moves <paramref name="current"/> toward <paramref name="target"/> by at most
    /// <paramref name="maxStep"/> degrees. Produces smooth, stepwise melodic motion
    /// instead of random jumps.
    /// </summary>
    private static int StepToward(int current, int target, int maxStep)
    {
        var diff = target - current;
        if (Math.Abs(diff) <= maxStep)
        {
            return target;
        }

        return current + (diff > 0 ? maxStep : -maxStep);
    }

    /// <summary>
    /// Wraps a degree into the range [<paramref name="min"/>, <paramref name="max"/>).
    /// </summary>
    private static int WrapDegree(int degree, int min, int max)
    {
        var range = max - min;
        return ((degree - min) % range + range) % range + min;
    }

    /// <summary>
    /// Plays a chord with notes spread in time (arpeggiated) rather than all at once.
    /// Each note starts one <see cref="ArpeggioSpacingTicks"/> after the previous,
    /// creating a musical flourish.
    /// </summary>
    private void EmitArpeggiatedChord(long startTick, MusicalKey key, int[] degrees,
        int octave, int velocity, int noteDurationTicks)
    {
        var offset = 0;
        foreach (var degree in degrees)
        {
            EmitNote(new NoteEvent
            {
                MidiNoteNumber = _scale.DegreeToMidiNote(degree, key, octave),
                Velocity = velocity,
                Channel = MidiConstants.MelodyChannel,
                StartTick = startTick + offset,
                DurationTicks = noteDurationTicks - offset, // later notes ring shorter
            });

            offset += ArpeggioSpacingTicks;
        }
    }

    /// <summary>
    /// Accumulates a note event for potential file output
    /// and simultaneously plays it through the live MIDI output (if available).
    /// </summary>
    private void EmitNote(NoteEvent note)
    {
        _events.Add(note);

        if (_liveOutput is not null)
        {
            var durationMs = _config.TicksToMilliseconds(note.DurationTicks);
            _liveOutput.PlayNote(note.Channel, note.MidiNoteNumber, note.Velocity, durationMs);
        }
    }

    /// <summary>
    /// Sends program-change messages to the live output for all configured instruments.
    /// Only runs once, on the first project start.
    /// </summary>
    private void EnsureInstrumentsInitialized()
    {
        if (_instrumentsInitialized || _liveOutput is null)
        {
            return;
        }

        _instrumentsInitialized = true;
        _liveOutput.SetInstrument(MidiConstants.MelodyChannel, _config.MelodyInstrument);
        _liveOutput.SetInstrument(MidiConstants.BassChannel, _config.BassInstrument);
        _liveOutput.SetInstrument(MidiConstants.PadChannel, _config.PadInstrument);
    }
}
