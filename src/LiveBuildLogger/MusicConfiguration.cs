using LiveBuildLogger.Music;

namespace LiveBuildLogger;

/// <summary>
/// Configuration for the build-to-music generator.
/// Parsed from MSBuild logger parameter string.
/// <para>
/// Example parameter string:
/// <c>BPM=140;Scale=Blues;Octave=5;Instrument=ElectricPiano1;Output=my-build.mid</c>
/// </para>
/// <para>
/// By default, sound is played live through the system MIDI synthesizer.
/// Set <c>Output=path.mid</c> to also save a MIDI file.
/// Set <c>Live=false</c> to disable real-time playback (only useful with Output).
/// Set <c>Pace=true</c> when replaying binlog files to space events at original timing.
/// Set <c>Speed=2</c> to replay at double speed (combine with <c>Pace=true</c>).
/// </para>
/// </summary>
internal sealed record MusicConfiguration
{
    /// <summary>
    /// Tempo in beats per minute (default: 120).
    /// </summary>
    internal int BeatsPerMinute { get; init; } = 120;

    /// <summary>
    /// Musical scale used for note generation (default: Pentatonic).
    /// </summary>
    internal ScaleType ScaleType { get; init; } = ScaleType.Pentatonic;

    /// <summary>
    /// Base MIDI octave for melody notes. 4 = middle-C area (default: 4).
    /// </summary>
    internal int BaseOctave { get; init; } = 4;

    /// <summary>
    /// General MIDI instrument for the melody channel (default: AcousticGrandPiano).
    /// </summary>
    internal GeneralMidiInstrument MelodyInstrument { get; init; } = GeneralMidiInstrument.AcousticGrandPiano;

    /// <summary>
    /// General MIDI instrument for the bass channel (default: SynthBass1).
    /// </summary>
    internal GeneralMidiInstrument BassInstrument { get; init; } = GeneralMidiInstrument.SynthBass1;

    /// <summary>
    /// General MIDI instrument for the pad/harmony channel (default: PadNewAge).
    /// </summary>
    internal GeneralMidiInstrument PadInstrument { get; init; } = GeneralMidiInstrument.PadNewAge;

    /// <summary>
    /// Whether the drum loop is enabled (default: true).
    /// Set to <c>false</c> via <c>Drums=false</c> to silence the rhythm section.
    /// </summary>
    internal bool EnableDrums { get; init; } = true;

    /// <summary>
    /// Whether the bass line is enabled (default: true).
    /// Set to <c>false</c> via <c>BassLine=false</c> to silence the bass track.
    /// </summary>
    internal bool EnableBassLine { get; init; } = true;

    /// <summary>
    /// Whether to play notes in real-time through the system MIDI synthesizer (default: true).
    /// </summary>
    internal bool LivePlayback { get; init; } = true;

    /// <summary>
    /// When <c>true</c>, sleeps between events to match original build timing.
    /// Essential for binlog replay so notes don't all fire instantly.
    /// Has no practical effect during a live build (default: false).
    /// </summary>
    internal bool Pace { get; init; }

    /// <summary>
    /// Playback speed multiplier used when <see cref="Pace"/> is enabled.
    /// 1.0 = original speed, 2.0 = double speed, 0.5 = half speed (default: 1.0).
    /// </summary>
    internal double Speed { get; init; } = 1.0;

    /// <summary>
    /// Path to write the output MIDI file, or <c>null</c> to skip file output (default: null).
    /// Set via <c>Output=path.mid</c> parameter.
    /// </summary>
    internal string? OutputFilePath { get; init; }

    /// <summary>
    /// MIDI ticks per quarter note — controls timing resolution (default: 480).
    /// </summary>
    internal int TicksPerQuarterNote { get; init; } = 480;

    /// <summary>
    /// Converts a tick duration to milliseconds based on current tempo.
    /// </summary>
    internal int TicksToMilliseconds(int ticks)
    {
        var quarterNoteMs = 60_000.0 / BeatsPerMinute;
        return (int)(ticks * quarterNoteMs / TicksPerQuarterNote);
    }

    /// <summary>
    /// Parses a semicolon-delimited key=value parameter string into a <see cref="MusicConfiguration"/>.
    /// Unknown keys are silently ignored.
    /// </summary>
    /// <param name="parameters">Raw logger parameter string, or <c>null</c>.</param>
    /// <returns>A new configuration with parsed values.</returns>
    internal static MusicConfiguration Parse(string? parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            return new MusicConfiguration();
        }

        var config = new MusicConfiguration();

        foreach (var pair in parameters.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            config = ApplyParameter(config, parts[0], parts[1]);
        }

        return config;
    }

    private static MusicConfiguration ApplyParameter(MusicConfiguration config, string key, string value)
    {
        return key.ToUpperInvariant() switch
        {
                "BPM" when int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var bpm) && bpm > 0
                    => config with { BeatsPerMinute = bpm },

                "SCALE" when Enum.TryParse<ScaleType>(value, ignoreCase: true, out var scale)
                    => config with { ScaleType = scale },

                "OCTAVE" when int.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var octave) && octave is >= 0 and <= 9
                    => config with { BaseOctave = octave },

                "INSTRUMENT" when Enum.TryParse<GeneralMidiInstrument>(value, ignoreCase: true, out var inst)
                    => config with { MelodyInstrument = inst },

                "BASS" when Enum.TryParse<GeneralMidiInstrument>(value, ignoreCase: true, out var bass)
                    => config with { BassInstrument = bass },

                "PAD" when Enum.TryParse<GeneralMidiInstrument>(value, ignoreCase: true, out var pad)
                    => config with { PadInstrument = pad },

                "OUTPUT" when !string.IsNullOrWhiteSpace(value)
                    => config with { OutputFilePath = value },

                "LIVE" when bool.TryParse(value, out var live)
                    => config with { LivePlayback = live },

                "PACE" when bool.TryParse(value, out var pace)
                    => config with { Pace = pace },

                "SPEED" when double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var speed) && speed > 0
                    => config with { Speed = speed },

                "DRUMS" when bool.TryParse(value, out var drums)
                    => config with { EnableDrums = drums },

                "BASSLINE" when bool.TryParse(value, out var bassLine)
                    => config with { EnableBassLine = bassLine },

                _ => config,
            };
    }
}
