using MusicBuild.Music;

namespace MusicBuild.Midi;

/// <summary>
/// Abstraction for MIDI output — either real-time audio or file recording.
/// Enables swapping backends (Windows MIDI, ALSA, network, etc.) in the future.
/// </summary>
internal interface IMidiOutput : IDisposable
{
    /// <summary>
    /// Sends a MIDI Program Change to select the instrument on a channel.
    /// Ignored on the percussion channel (9).
    /// </summary>
    void SetInstrument(int channel, GeneralMidiInstrument instrument);

    /// <summary>
    /// Plays a single MIDI note with the given velocity for the specified duration.
    /// The implementation handles scheduling the note-off.
    /// </summary>
    /// <param name="channel">MIDI channel (0–15).</param>
    /// <param name="noteNumber">MIDI note number (0–127).</param>
    /// <param name="velocity">Note velocity (0–127).</param>
    /// <param name="durationMs">How long the note should ring, in milliseconds.</param>
    void PlayNote(int channel, int noteNumber, int velocity, int durationMs);
}
