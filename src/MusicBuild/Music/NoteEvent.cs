using System.Runtime.InteropServices;

namespace MusicBuild.Music;

/// <summary>
/// Represents a single MIDI note event with timing, channel, and dynamics.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct NoteEvent
{
    /// <summary>
    /// MIDI note number (0–127). Middle C = 60.
    /// </summary>
    internal required int MidiNoteNumber { get; init; }

    /// <summary>
    /// Note velocity / volume (0–127).
    /// </summary>
    internal required int Velocity { get; init; }

    /// <summary>
    /// MIDI channel (0–15). Channel 9 is percussion in General MIDI.
    /// </summary>
    internal required int Channel { get; init; }

    /// <summary>
    /// Tick offset from the start of the song.
    /// </summary>
    internal required long StartTick { get; init; }

    /// <summary>
    /// Duration of the note in ticks.
    /// </summary>
    internal required int DurationTicks { get; init; }
}
