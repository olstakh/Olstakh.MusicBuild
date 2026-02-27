using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MusicBuild.Music;

namespace MusicBuild.Midi;

/// <summary>
/// Real-time MIDI output using the Windows Multimedia API (winmm.dll).
/// Plays notes through the default system MIDI synthesizer (typically Microsoft GS Wavetable Synth).
/// <para>
/// Use <see cref="TryCreate"/> to safely attempt to open a MIDI device.
/// Returns <c>null</c> if the platform is unsupported or no MIDI device is available.
/// </para>
/// </summary>
internal sealed class LiveMidiOutput : IMidiOutput
{
    private nint _handle;
    private readonly object _lock = new();
    private bool _disposed;

    private LiveMidiOutput(nint handle) => _handle = handle;

    /// <summary>
    /// Attempts to open the default MIDI output device.
    /// Returns <c>null</c> on non-Windows platforms or if no MIDI device is available.
    /// </summary>
    internal static LiveMidiOutput? TryCreate()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return null;
        }

        const int MIDI_MAPPER = -1; // auto-select default MIDI device
        var result = NativeMethods.MidiOutOpen(out var handle, MIDI_MAPPER, 0, 0, 0);
        return result == 0 ? new LiveMidiOutput(handle) : null;
    }

    /// <inheritdoc />
    public void SetInstrument(int channel, GeneralMidiInstrument instrument)
    {
        if (channel == MidiConstants.PercussionChannel)
        {
            return; // percussion channel ignores program changes
        }

        // Program Change: 0xC0 | channel, program
        var msg = (uint)(0xC0 | (channel & 0x0F))
                | ((uint)((int)instrument & 0x7F) << 8);
        SendMessage(msg);
    }

    /// <inheritdoc />
    public void PlayNote(int channel, int noteNumber, int velocity, int durationMs)
    {
        var ch = channel & 0x0F;
        var note = (uint)Math.Max(0, Math.Min(127, noteNumber));
        var vel = (uint)Math.Max(0, Math.Min(127, velocity));

        // Note On: 0x90 | channel, note, velocity
        var noteOnMsg = (uint)(0x90 | ch) | (note << 8) | (vel << 16);
        SendMessage(noteOnMsg);

        // Schedule note-off after the duration expires
        _ = ScheduleNoteOffAsync(ch, note, durationMs);
    }

    /// <summary>
    /// Waits for the given duration then sends a MIDI Note Off message.
    /// Intentionally fire-and-forget — interrupted note-offs are harmless
    /// since <see cref="Dispose"/> calls <c>midiOutReset</c> to silence everything.
    /// </summary>
    private async Task ScheduleNoteOffAsync(int channel, uint note, int durationMs)
    {
        await Task.Delay(Math.Max(1, durationMs)).ConfigureAwait(false);

        // Note Off: 0x80 | channel, note, velocity=0
        var noteOffMsg = (uint)(0x80 | channel) | (note << 8);
        SendMessage(noteOffMsg);
    }

    private void SendMessage(uint msg)
    {
        lock (_lock)
        {
            if (!_disposed && _handle != 0)
            {
                _ = NativeMethods.MidiOutShortMsg(_handle, msg);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_handle != 0)
            {
                _ = NativeMethods.MidiOutReset(_handle);  // silence all playing notes
                _ = NativeMethods.MidiOutClose(_handle);
                _handle = 0;
            }
        }
    }

    /// <summary>
    /// P/Invoke declarations for the Windows Multimedia MIDI API.
    /// </summary>
    private static class NativeMethods
    {
        /// <summary>Opens a MIDI output device.</summary>
        [DllImport("winmm.dll", EntryPoint = "midiOutOpen")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int MidiOutOpen(out nint lphMidiOut, int uDeviceID, nint dwCallback, nint dwInstance, int fdwOpen);

        /// <summary>Sends a short MIDI message (note on/off, program change, etc.).</summary>
        [DllImport("winmm.dll", EntryPoint = "midiOutShortMsg")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int MidiOutShortMsg(nint hMidiOut, uint dwMsg);

        /// <summary>Resets the MIDI output device, silencing all notes.</summary>
        [DllImport("winmm.dll", EntryPoint = "midiOutReset")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int MidiOutReset(nint hMidiOut);

        /// <summary>Closes a MIDI output device.</summary>
        [DllImport("winmm.dll", EntryPoint = "midiOutClose")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static extern int MidiOutClose(nint hMidiOut);
    }
}
