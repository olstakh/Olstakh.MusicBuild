namespace MusicBuild.Music;

/// <summary>
/// Subset of General MIDI program numbers (0-indexed).
/// These map to MIDI Program Change values.
/// </summary>
internal enum GeneralMidiInstrument
{
    /// <summary>Program 0: Acoustic Grand Piano.</summary>
    AcousticGrandPiano = 0,

    /// <summary>Program 4: Electric Piano 1 (Rhodes).</summary>
    ElectricPiano1 = 4,

    /// <summary>Program 11: Vibraphone.</summary>
    Vibraphone = 11,

    /// <summary>Program 12: Marimba.</summary>
    Marimba = 12,

    /// <summary>Program 19: Church Organ.</summary>
    ChurchOrgan = 19,

    /// <summary>Program 24: Acoustic Guitar (Nylon).</summary>
    AcousticGuitarNylon = 24,

    /// <summary>Program 27: Electric Guitar (Clean).</summary>
    ElectricGuitarClean = 27,

    /// <summary>Program 29: Overdriven Guitar.</summary>
    OverdrivenGuitar = 29,

    /// <summary>Program 38: Synth Bass 1.</summary>
    SynthBass1 = 38,

    /// <summary>Program 40: Violin.</summary>
    Violin = 40,

    /// <summary>Program 48: String Ensemble 1.</summary>
    StringEnsemble1 = 48,

    /// <summary>Program 50: Synth Strings 1.</summary>
    SynthStrings1 = 50,

    /// <summary>Program 80: Lead 1 (Square).</summary>
    LeadSquare = 80,

    /// <summary>Program 88: Pad 1 (New Age / Fantasia).</summary>
    PadNewAge = 88,

    /// <summary>Program 98: FX 3 (Crystal).</summary>
    FxCrystal = 98,

    /// <summary>Program 114: Steel Drums.</summary>
    SteelDrums = 114,
}
