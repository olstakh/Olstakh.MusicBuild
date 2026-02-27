namespace LiveBuildLogger.Music;

/// <summary>
/// Available musical scale types for mapping build events to notes.
/// </summary>
internal enum ScaleType
{
    /// <summary>Major scale — bright, happy (W-W-H-W-W-W-H).</summary>
    Major,

    /// <summary>Natural minor scale — darker, serious (W-H-W-W-H-W-W).</summary>
    Minor,

    /// <summary>Pentatonic scale — always consonant, safe default.</summary>
    Pentatonic,

    /// <summary>Blues scale — adds the "blue" note to pentatonic.</summary>
    Blues,

    /// <summary>Chromatic scale — all 12 semitones, maximum variety.</summary>
    Chromatic,
}
