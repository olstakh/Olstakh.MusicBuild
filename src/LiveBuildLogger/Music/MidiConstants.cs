namespace LiveBuildLogger.Music;

/// <summary>
/// Constants for MIDI channel assignments, velocities, and General MIDI percussion note numbers.
/// </summary>
internal static class MidiConstants
{
    // ── Channel assignments ──────────────────────────────────────────────
    // These can be extended when adding per-project channel support.

    /// <summary>Melody / lead voice channel.</summary>
    internal const int MelodyChannel = 0;

    /// <summary>Bass voice channel.</summary>
    internal const int BassChannel = 1;

    /// <summary>Pad / texture channel (reserved for future use).</summary>
    internal const int PadChannel = 2;

    /// <summary>General MIDI percussion channel (always 9).</summary>
    internal const int PercussionChannel = 9;

    // ── Velocity levels ──────────────────────────────────────────────────

    /// <summary>Maximum MIDI velocity (127).</summary>
    internal const int MaxVelocity = 127;

    /// <summary>High-intensity velocity for accents.</summary>
    internal const int HighVelocity = 110;

    /// <summary>Medium velocity for normal notes.</summary>
    internal const int MediumVelocity = 80;

    /// <summary>Soft velocity for subtle texture.</summary>
    internal const int SoftVelocity = 50;

    // ── General MIDI percussion note numbers ─────────────────────────────

    /// <summary>Bass Drum 1 (GM note 36).</summary>
    internal const int BassDrum = 36;

    /// <summary>Acoustic Snare (GM note 38).</summary>
    internal const int SnareDrum = 38;

    /// <summary>Hand Clap (GM note 39).</summary>
    internal const int Clap = 39;

    /// <summary>Closed Hi-Hat (GM note 42).</summary>
    internal const int ClosedHiHat = 42;

    /// <summary>Open Hi-Hat (GM note 46).</summary>
    internal const int OpenHiHat = 46;

    /// <summary>Crash Cymbal 1 (GM note 49).</summary>
    internal const int CrashCymbal = 49;

    /// <summary>Ride Cymbal 1 (GM note 51).</summary>
    internal const int RideCymbal = 51;
}
