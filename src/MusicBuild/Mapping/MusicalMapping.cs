using System.Collections.Generic;
using MusicBuild.Music;

namespace MusicBuild.Mapping;

/// <summary>
/// Maps MSBuild build concepts (projects, targets, tasks) to musical elements
/// (keys, scale degrees, ticks). All methods are pure and deterministic.
/// </summary>
internal static class MusicalMapping
{
    /// <summary>
    /// Well-known MSBuild targets mapped to intentional scale degrees
    /// that create a musically coherent progression through a typical build.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, int> WellKnownTargetDegrees =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Restore"] = 0,                              // Root — grounding start
            ["_CheckForNETCoreSdkIsPreview"] = 1,          // Second — gentle step
            ["CollectPackageReferences"] = 1,
            ["ResolveProjectReferences"] = 2,              // Third — building
            ["ResolveReferences"] = 2,
            ["ResolveAssemblyReferences"] = 2,
            ["PrepareForBuild"] = 2,
            ["GenerateAssemblyInfo"] = 3,                  // Fourth — structural
            ["GenerateTargetFrameworkMonikerAttribute"] = 3,
            ["CoreCompile"] = 4,                           // Fifth — the big one
            ["Compile"] = 4,
            ["Csc"] = 4,
            ["CopyFilesToOutputDirectory"] = 5,            // Sixth — wrapping up
            ["_CopyFilesMarkedCopyLocal"] = 5,
            ["GenerateBuildDependencyFile"] = 6,           // Seventh — almost done
            ["Build"] = 7,                                 // Octave — resolution
            ["Clean"] = -1,                                // Below root — deconstructing
            ["Rebuild"] = 0,
            ["Pack"] = 6,
            ["Publish"] = 7,
            ["Test"] = 5,
        };

    /// <summary>
    /// Maps a project file path to a musical key deterministically.
    /// The same project always produces the same key.
    /// </summary>
    internal static MusicalKey ProjectToKey(string projectFile)
    {
        var hash = GetStableHash(Path.GetFileNameWithoutExtension(projectFile));
        return (MusicalKey)(hash % 12);
    }

    /// <summary>
    /// Maps a target name to a scale degree.
    /// Well-known targets get musically intentional mappings;
    /// unknown targets get a deterministic hash-based degree within one octave.
    /// </summary>
    internal static int TargetToScaleDegree(string targetName)
    {
        if (WellKnownTargetDegrees.TryGetValue(targetName, out var degree))
        {
            return degree;
        }

        // Keep within a single octave so stepwise motion stays musical
        var hash = GetStableHash(targetName);
        return (int)(hash % 7); // 0–6: single octave of a 7-note scale
    }

    /// <summary>
    /// Maps a task name to a small scale-degree offset, adding melodic variety within a target.
    /// </summary>
    internal static int TaskToScaleDegreeOffset(string taskName)
    {
        var hash = GetStableHash(taskName);
        return (int)(hash % 5); // 0–4: small melodic range
    }

    /// <summary>
    /// Converts a timestamp to a tick position relative to the build start.
    /// Quantizes to the nearest 16th-note grid.
    /// </summary>
    internal static long TimestampToTick(DateTime timestamp, DateTime buildStart, int bpm, int ticksPerQuarterNote)
    {
        var elapsed = timestamp - buildStart;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var quarterNoteDurationSeconds = 60.0 / bpm;
        var beats = elapsed.TotalSeconds / quarterNoteDurationSeconds;
        var rawTicks = (long)(beats * ticksPerQuarterNote);

        // Quantize to nearest 16th note
        var sixteenthNote = ticksPerQuarterNote / 4;
        return sixteenthNote > 0 ? (rawTicks / sixteenthNote) * sixteenthNote : rawTicks;
    }

    /// <summary>
    /// Converts a duration to MIDI ticks, quantized to the nearest rhythmic grid value.
    /// Capped at 2 whole notes to avoid absurdly long notes.
    /// </summary>
    internal static int DurationToTicks(TimeSpan duration, int bpm, int ticksPerQuarterNote)
    {
        var quarterNoteDurationSeconds = 60.0 / bpm;
        var beats = duration.TotalSeconds / quarterNoteDurationSeconds;
        var rawTicks = (int)(beats * ticksPerQuarterNote);

        // Quantize to nearest 16th note, minimum 1 sixteenth
        var sixteenthNote = ticksPerQuarterNote / 4;
        var quantized = Math.Max(sixteenthNote, (rawTicks / sixteenthNote) * sixteenthNote);

        // Cap at 2 whole notes
        var maxTicks = ticksPerQuarterNote * 8;
        return Math.Min(quantized, maxTicks);
    }

    /// <summary>
    /// Produces a deterministic, platform-independent hash for a string.
    /// Uses a FNV-1a–inspired algorithm so the same input always yields the same output,
    /// regardless of runtime or platform (unlike <see cref="string.GetHashCode()"/>).
    /// </summary>
    private static uint GetStableHash(string input)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in input)
            {
                hash = (hash ^ c) * 16777619;
            }

            return hash;
        }
    }
}
