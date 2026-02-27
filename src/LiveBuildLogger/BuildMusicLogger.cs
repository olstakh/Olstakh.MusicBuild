using System.Diagnostics;
using LiveBuildLogger.Composition;
using LiveBuildLogger.Midi;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace LiveBuildLogger;

/// <summary>
/// MSBuild logger that generates music from build events.
/// By default, plays live audio through the system MIDI synthesizer.
/// Optionally saves a MIDI file when <c>Output=path.mid</c> is specified.
/// <para>
/// Each project selects a musical key, each target becomes a melodic phrase,
/// individual tasks add rhythmic texture, warnings hit percussion, and
/// errors crash. The build ends with a triumphant chord or an ominous rumble.
/// </para>
/// <para>
/// Usage:
/// <code>dotnet build -logger:path/to/LiveBuildLogger.dll[;parameters]</code>
/// </para>
/// <para>
/// Parameters (semicolon-separated <c>key=value</c>):
/// <list type="bullet">
///   <item><c>BPM=120</c> — tempo in beats per minute</item>
///   <item><c>Scale=Pentatonic</c> — scale type (Major, Minor, Pentatonic, Blues, Chromatic)</item>
///   <item><c>Octave=4</c> — base octave for melody</item>
///   <item><c>Instrument=AcousticGrandPiano</c> — General MIDI instrument for melody</item>
///   <item><c>Bass=SynthBass1</c> — General MIDI instrument for bass</item>
///   <item><c>Output=build-music.mid</c> — save a MIDI file (off by default)</item>
///   <item><c>Live=false</c> — disable real-time playback</item>
///   <item><c>Pace=true</c> — pace events to original timing (for binlog replay)</item>
///   <item><c>Speed=2</c> — playback speed multiplier (use with Pace)</item>
/// </list>
/// </para>
/// </summary>
#pragma warning disable CA1001 // Disposable is cleaned up in Shutdown(), not IDisposable — MSBuild logger lifecycle
public sealed class BuildMusicLogger : Logger
#pragma warning restore CA1001
{
    private BuildMusicComposer? _composer;
    private MusicConfiguration _config = new();

    /// <inheritdoc />
    public override void Initialize(IEventSource eventSource)
    {
        ArgumentNullException.ThrowIfNull(eventSource);

        _config = MusicConfiguration.Parse(Parameters);

        // Create live MIDI output if enabled (and platform supports it)
        IMidiOutput? liveOutput = null;
        if (_config.LivePlayback)
        {
            liveOutput = LiveMidiOutput.TryCreate();
        }

        _composer = new BuildMusicComposer(_config, liveOutput);

        eventSource.ProjectStarted += OnProjectStarted;
        eventSource.ProjectFinished += OnProjectFinished;
        eventSource.TargetStarted += OnTargetStarted;
        eventSource.TargetFinished += OnTargetFinished;
        eventSource.TaskStarted += OnTaskStarted;
        eventSource.WarningRaised += OnWarningRaised;
        eventSource.ErrorRaised += OnErrorRaised;
        eventSource.BuildFinished += OnBuildFinished;
    }

    /// <inheritdoc />
    public override void Shutdown()
    {
        _composer?.Dispose();
        _composer = null;
    }

    // ── Event handlers ───────────────────────────────────────────────────

    private void OnProjectStarted(object sender, ProjectStartedEventArgs e)
    {
        if (e.ProjectFile is not null)
        {
            _composer?.OnProjectStarted(e.ProjectFile, e.Timestamp);
        }
    }

    private void OnProjectFinished(object sender, ProjectFinishedEventArgs e)
    {
        if (e.ProjectFile is not null)
        {
            _composer?.OnProjectFinished(e.ProjectFile, e.Succeeded, e.Timestamp);
        }
    }

    private void OnTargetStarted(object sender, TargetStartedEventArgs e)
    {
        _composer?.OnTargetStarted(e.TargetName, e.ProjectFile, e.Timestamp);
    }

    private void OnTargetFinished(object sender, TargetFinishedEventArgs e)
    {
        _composer?.OnTargetFinished(e.TargetName, e.Succeeded, e.Timestamp);
    }

    private void OnTaskStarted(object sender, TaskStartedEventArgs e)
    {
        _composer?.OnTaskStarted(e.TaskName, e.Timestamp);
    }

    private void OnWarningRaised(object sender, BuildWarningEventArgs e)
    {
        _composer?.OnWarningRaised(e.Timestamp);
    }

    private void OnErrorRaised(object sender, BuildErrorEventArgs e)
    {
        _composer?.OnErrorRaised(e.Timestamp);
    }

    private void OnBuildFinished(object sender, BuildFinishedEventArgs e)
    {
        if (_composer is null)
        {
            return;
        }

        _composer.OnBuildFinished(e.Succeeded, e.Timestamp);

        // Let the final chord ring before we tear everything down
        if (_config.LivePlayback)
        {
            var finalChordMs = _config.TicksToMilliseconds(_config.TicksPerQuarterNote * 4);
            Thread.Sleep(finalChordMs);
        }

        // Write MIDI file only if explicitly requested
        if (_config.OutputFilePath is not null)
        {
            try
            {
                MidiFileWriter.Write(_config.OutputFilePath, _composer.Events, _config);
            }
#pragma warning disable CA1031 // Do not catch general exception types — logger must never crash the build
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Debug.WriteLine($"BuildMusicLogger: Failed to write MIDI file: {ex.Message}");
            }
        }
    }
}
