using LiveBuildLogger.Music;

namespace LiveBuildLogger.Midi;

/// <summary>
/// Writes a collection of <see cref="NoteEvent"/>s to a Standard MIDI File (format 1).
/// No external dependencies — encodes raw MIDI bytes directly.
/// </summary>
#pragma warning disable CA1859 // Prefer concrete types — IReadOnlyList enforces immutability contract per coding guidelines
internal static class MidiFileWriter
{
    /// <summary>
    /// Writes a complete MIDI file with one track per unique channel used.
    /// </summary>
    /// <param name="filePath">Destination file path for the .mid file.</param>
    /// <param name="events">Note events to write.</param>
    /// <param name="config">Music configuration (tempo, instruments, resolution).</param>
    internal static void Write(string filePath, IReadOnlyList<NoteEvent> events, MusicConfiguration config)
    {
        // Group events by MIDI channel, one track per channel
        var channelGroups = events
            .GroupBy(e => e.Channel)
            .OrderBy(g => g.Key)
            .ToList();

        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);

        // Header: format 1 (multi-track), N+1 tracks (tempo track + one per channel)
        WriteHeader(writer, (ushort)(channelGroups.Count + 1), (ushort)config.TicksPerQuarterNote);

        // Track 0: tempo/conductor track
        WriteTempoTrack(writer, config.BeatsPerMinute);

        // Tracks 1–N: one per channel with note data
        foreach (var group in channelGroups)
        {
            var sortedEvents = group.OrderBy(e => e.StartTick).ToList();
            WriteNoteTrack(writer, group.Key, sortedEvents, config);
        }
    }

    /// <summary>
    /// Writes the MIDI file header chunk: "MThd" + format 1 + track count + division.
    /// </summary>
    private static void WriteHeader(BinaryWriter writer, ushort numTracks, ushort ticksPerQuarterNote)
    {
        writer.Write("MThd"u8);
        WriteBigEndian32(writer, 6);                // chunk length (always 6 for header)
        WriteBigEndian16(writer, 1);                // format 1 = multi-track
        WriteBigEndian16(writer, numTracks);
        WriteBigEndian16(writer, ticksPerQuarterNote);
    }

    /// <summary>
    /// Writes the tempo track (track 0) containing a single tempo meta-event.
    /// </summary>
    private static void WriteTempoTrack(BinaryWriter writer, int bpm)
    {
        using var trackStream = new MemoryStream();
        using var trackWriter = new BinaryWriter(trackStream);

        // Tempo meta-event: FF 51 03 tt tt tt (microseconds per beat)
        var microsecondsPerBeat = 60_000_000 / bpm;
        WriteVariableLength(trackWriter, 0); // delta time = 0
        trackWriter.Write((byte)0xFF);       // meta-event marker
        trackWriter.Write((byte)0x51);       // type = set tempo
        trackWriter.Write((byte)0x03);       // data length
        trackWriter.Write((byte)((microsecondsPerBeat >> 16) & 0xFF));
        trackWriter.Write((byte)((microsecondsPerBeat >> 8) & 0xFF));
        trackWriter.Write((byte)(microsecondsPerBeat & 0xFF));

        // End-of-track meta-event
        WriteEndOfTrack(trackWriter);

        FlushTrack(writer, trackStream);
    }

    /// <summary>
    /// Writes a note track for a single MIDI channel.
    /// Includes a program-change event (except for percussion) and all note on/off pairs.
    /// </summary>
    private static void WriteNoteTrack(
        BinaryWriter writer,
        int channel,
        IReadOnlyList<NoteEvent> events,
        MusicConfiguration config)
    {
        using var trackStream = new MemoryStream();
        using var trackWriter = new BinaryWriter(trackStream);
        var channelByte = channel & 0x0F;

        // Program change to set instrument (percussion channel ignores this)
        if (channel != MidiConstants.PercussionChannel)
        {
            var instrument = channel switch
            {
                MidiConstants.MelodyChannel => (int)config.MelodyInstrument,
                MidiConstants.BassChannel => (int)config.BassInstrument,
                MidiConstants.PadChannel => (int)config.PadInstrument,
                _ => (int)config.MelodyInstrument,
            };

            WriteVariableLength(trackWriter, 0);
            trackWriter.Write((byte)(0xC0 | channelByte)); // Program Change
            trackWriter.Write((byte)(instrument & 0x7F));
        }

        // Build sorted list of all note-on and note-off MIDI events
        var midiEvents = new List<(long Tick, byte Status, byte Data1, byte Data2)>(events.Count * 2);

        foreach (var note in events)
        {
            var noteNum = (byte)Math.Clamp(note.MidiNoteNumber, 0, 127);
            var velocity = (byte)Math.Clamp(note.Velocity, 0, 127);

            midiEvents.Add((note.StartTick, (byte)(0x90 | channelByte), noteNum, velocity));                // Note On
            midiEvents.Add((note.StartTick + note.DurationTicks, (byte)(0x80 | channelByte), noteNum, 0));  // Note Off
        }

        // Stable sort by tick (preserves note-on before note-off when at same tick)
        midiEvents.Sort((a, b) => a.Tick.CompareTo(b.Tick));

        long lastTick = 0;
        foreach (var (tick, status, data1, data2) in midiEvents)
        {
            var delta = Math.Max(0, tick - lastTick);
            WriteVariableLength(trackWriter, (uint)delta);
            trackWriter.Write(status);
            trackWriter.Write(data1);
            trackWriter.Write(data2);
            lastTick = tick;
        }

        WriteEndOfTrack(trackWriter);
        FlushTrack(writer, trackStream);
    }

    // ── Low-level MIDI encoding helpers ──────────────────────────────────

    private static void WriteEndOfTrack(BinaryWriter trackWriter)
    {
        WriteVariableLength(trackWriter, 0);
        trackWriter.Write((byte)0xFF);
        trackWriter.Write((byte)0x2F);
        trackWriter.Write((byte)0x00);
    }

    private static void FlushTrack(BinaryWriter writer, MemoryStream trackStream)
    {
        var trackData = trackStream.ToArray();
        writer.Write("MTrk"u8);
        WriteBigEndian32(writer, (uint)trackData.Length);
        writer.Write(trackData);
    }

    private static void WriteBigEndian32(BinaryWriter writer, uint value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteBigEndian16(BinaryWriter writer, ushort value)
    {
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    /// <summary>
    /// Writes a MIDI variable-length quantity (VLQ).
    /// Each byte uses 7 data bits; the MSB is set on all bytes except the last.
    /// </summary>
    private static void WriteVariableLength(BinaryWriter writer, uint value)
    {
        if (value < 0x80)
        {
            writer.Write((byte)value);
            return;
        }

        Span<byte> buffer = stackalloc byte[4];
        var index = 3;
        buffer[index] = (byte)(value & 0x7F);
        value >>= 7;

        while (value > 0)
        {
            index--;
            buffer[index] = (byte)((value & 0x7F) | 0x80);
            value >>= 7;
        }

        for (var i = index; i <= 3; i++)
        {
            writer.Write(buffer[i]);
        }
    }
}
#pragma warning restore CA1859
