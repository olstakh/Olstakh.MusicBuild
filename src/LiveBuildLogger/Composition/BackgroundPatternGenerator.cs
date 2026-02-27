using LiveBuildLogger.Music;

namespace LiveBuildLogger.Composition;

/// <summary>
/// Generates continuous musical patterns (drums, bass) that provide rhythmic and
/// harmonic structure throughout the build. These patterns form the musical
/// backbone that makes build-event-driven melody notes sound intentional
/// rather than random.
/// </summary>
internal static class BackgroundPatternGenerator
{
    /// <summary>
    /// Generates a four-on-the-floor EDM drum pattern: kick on every beat,
    /// snare on beats 2 and 4, closed hi-hat on every 8th note.
    /// </summary>
    internal static IReadOnlyList<NoteEvent> GenerateDrumPattern(
        long startTick, long endTick, int ticksPerQuarterNote)
    {
        var events = new List<NoteEvent>();
        var eighthNote = ticksPerQuarterNote / 2;
        var sixteenthNote = ticksPerQuarterNote / 4;

        for (long tick = startTick, i = 0; tick < endTick; tick += eighthNote, i++)
        {
            var posInBar = (int)(i % 8);
            var isOnBeat = posInBar % 2 == 0;
            var beatNumber = posInBar / 2; // 0–3

            // Kick on every beat (four-on-the-floor)
            if (isOnBeat)
            {
                events.Add(new NoteEvent
                {
                    MidiNoteNumber = MidiConstants.BassDrum,
                    Velocity = MidiConstants.MediumVelocity,
                    Channel = MidiConstants.PercussionChannel,
                    StartTick = tick,
                    DurationTicks = eighthNote,
                });
            }

            // Snare on beats 2 and 4
            if (isOnBeat && beatNumber is 1 or 3)
            {
                events.Add(new NoteEvent
                {
                    MidiNoteNumber = MidiConstants.SnareDrum,
                    Velocity = 70,
                    Channel = MidiConstants.PercussionChannel,
                    StartTick = tick,
                    DurationTicks = eighthNote,
                });
            }

            // Closed hi-hat on every 8th note (accented on downbeats)
            events.Add(new NoteEvent
            {
                MidiNoteNumber = MidiConstants.ClosedHiHat,
                Velocity = isOnBeat ? MidiConstants.SoftVelocity : 35,
                Channel = MidiConstants.PercussionChannel,
                StartTick = tick,
                DurationTicks = sixteenthNote,
            });
        }

        return events;
    }

    /// <summary>
    /// Generates a bass line that follows key changes with a root–fifth alternation
    /// (half-note rhythm). Provides harmonic movement under the melody.
    /// </summary>
    internal static IReadOnlyList<NoteEvent> GenerateBassLine(
        long startTick,
        long endTick,
        IReadOnlyList<(long Tick, MusicalKey Key)> keyChanges,
        Scale scale,
        int octave,
        int ticksPerQuarterNote)
    {
        var events = new List<NoteEvent>();
        var halfNote = ticksPerQuarterNote * 2;

        for (long tick = startTick, i = 0; tick < endTick; tick += halfNote, i++)
        {
            var key = GetKeyAtTick(keyChanges, tick);
            var degree = i % 2 == 0 ? 0 : 4; // alternating root and fifth

            events.Add(new NoteEvent
            {
                MidiNoteNumber = scale.DegreeToMidiNote(degree, key, octave),
                Velocity = MidiConstants.MediumVelocity,
                Channel = MidiConstants.BassChannel,
                StartTick = tick,
                DurationTicks = halfNote - (ticksPerQuarterNote / 4), // slight gap for articulation
            });
        }

        return events;
    }

    /// <summary>
    /// Finds the active musical key at a given tick by scanning key change events.
    /// </summary>
    private static MusicalKey GetKeyAtTick(
        IReadOnlyList<(long Tick, MusicalKey Key)> keyChanges, long tick)
    {
        var result = MusicalKey.C;
        foreach (var (changeTick, changeKey) in keyChanges)
        {
            if (changeTick > tick)
            {
                break;
            }

            result = changeKey;
        }

        return result;
    }
}
