namespace LiveBuildLogger.Music;

/// <summary>
/// Defines a musical scale as a set of semitone intervals from the root note.
/// Provides conversion from scale degrees to MIDI note numbers.
/// </summary>
internal sealed class Scale
{
    /// <summary>
    /// Human-readable name of the scale.
    /// </summary>
    internal required string Name { get; init; }

    /// <summary>
    /// Semitone intervals from root (e.g., [0, 2, 4, 5, 7, 9, 11] for major).
    /// </summary>
    internal required IReadOnlyList<int> Intervals { get; init; }

    /// <summary>
    /// Converts a scale degree to a MIDI note number (0–127).
    /// Supports negative degrees and degrees beyond the scale range (wraps octaves).
    /// </summary>
    /// <param name="degree">Scale degree (0-based). Can be negative or exceed scale size.</param>
    /// <param name="key">Root key of the scale.</param>
    /// <param name="octave">Base MIDI octave (4 = middle C area).</param>
    /// <returns>MIDI note number clamped to 0–127.</returns>
    internal int DegreeToMidiNote(int degree, MusicalKey key, int octave)
    {
        var count = Intervals.Count;

        // Handle negative degrees and octave wrapping
        var normalizedDegree = ((degree % count) + count) % count;
        var octaveOffset = (int)Math.Floor((double)degree / count);
        var interval = Intervals[normalizedDegree];

        var midiNote = ((octave + octaveOffset) * 12) + (int)key + interval;
        return Math.Clamp(midiNote, 0, 127);
    }

    /// <summary>Major scale: W-W-H-W-W-W-H.</summary>
    internal static Scale Major { get; } = new()
    {
        Name = "Major",
        Intervals = [0, 2, 4, 5, 7, 9, 11],
    };

    /// <summary>Natural minor scale: W-H-W-W-H-W-W.</summary>
    internal static Scale Minor { get; } = new()
    {
        Name = "Minor",
        Intervals = [0, 2, 3, 5, 7, 8, 10],
    };

    /// <summary>Pentatonic scale — always consonant.</summary>
    internal static Scale Pentatonic { get; } = new()
    {
        Name = "Pentatonic",
        Intervals = [0, 2, 4, 7, 9],
    };

    /// <summary>Blues scale — pentatonic with the blue note.</summary>
    internal static Scale Blues { get; } = new()
    {
        Name = "Blues",
        Intervals = [0, 3, 5, 6, 7, 10],
    };

    /// <summary>Chromatic scale — all 12 semitones.</summary>
    internal static Scale Chromatic { get; } = new()
    {
        Name = "Chromatic",
        Intervals = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
    };

    /// <summary>
    /// Gets the predefined <see cref="Scale"/> for the given <see cref="ScaleType"/>.
    /// </summary>
    internal static Scale FromType(ScaleType type) => type switch
    {
        ScaleType.Major => Major,
        ScaleType.Minor => Minor,
        ScaleType.Pentatonic => Pentatonic,
        ScaleType.Blues => Blues,
        ScaleType.Chromatic => Chromatic,
        _ => Pentatonic,
    };
}
