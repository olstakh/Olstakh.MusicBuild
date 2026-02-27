using System.Diagnostics;

namespace LiveBuildLogger.Tool;

/// <summary>
/// Entry point for the <c>dotnet music-build</c> global tool.
/// Wraps <c>dotnet build</c> (or any dotnet command) and injects the LiveBuildLogger
/// as an MSBuild logger, so users get music without manually specifying <c>-logger:</c>.
/// <para>
/// Usage:
/// <code>
/// dotnet music-build                          # builds current project with music
/// dotnet music-build --bpm 140 --scale Blues   # custom music parameters
/// dotnet music-build -- -c Release             # passes -c Release to dotnet build
/// </code>
/// </para>
/// </summary>
internal static class Program
{
    private static readonly Dictionary<string, string> KnownMusicArgs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["--bpm"] = "BPM",
        ["--scale"] = "Scale",
        ["--octave"] = "Octave",
        ["--instrument"] = "Instrument",
        ["--bass"] = "Bass",
        ["--pad"] = "Pad",
        ["--output"] = "Output",
        ["--pace"] = "Pace",
        ["--speed"] = "Speed",
    };

#pragma warning disable CA1303 // CLI tool help text does not need localization
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] is "--help" or "-h" or "-?")
        {
            PrintHelp();
            return 0;
        }

        // Find the logger DLL bundled alongside this tool
        var toolDir = AppContext.BaseDirectory;
        var loggerDll = Path.Combine(toolDir, "LiveBuildLogger.dll");

        if (!File.Exists(loggerDll))
        {
            Console.Error.WriteLine("Error: Could not find LiveBuildLogger.dll alongside the tool.");
            Console.Error.WriteLine($"Expected at: {loggerDll}");
            return 1;
        }

        // Parse our music args vs dotnet build args
        var (loggerParams, dotnetArgs) = ParseArgs(args);

        // Build the -logger: argument
        var loggerArg = string.IsNullOrEmpty(loggerParams)
            ? $"-logger:\"{loggerDll}\""
            : $"-logger:\"{loggerDll};{loggerParams}\"";

        // Build the full command: dotnet build <dotnet-args> -logger:<path>
        var dotnetCommand = dotnetArgs.Count > 0
            ? $"build {string.Join(' ', dotnetArgs)} {loggerArg}"
            : $"build {loggerArg}";

        Console.WriteLine($"  \u266b  Building with music...");
        Console.WriteLine();

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = dotnetCommand,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            Console.Error.WriteLine("Error: Failed to start 'dotnet build'.");
            return 1;
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    /// <summary>
    /// Splits arguments into music-specific logger parameters and passthrough dotnet build arguments.
    /// Everything before <c>--</c> is scanned for music flags; everything after goes to dotnet build.
    /// Unrecognized flags before <c>--</c> also go to dotnet build.
    /// </summary>
    private static (string LoggerParams, List<string> DotnetArgs) ParseArgs(string[] args)
    {
        var loggerParts = new List<string>();
        var dotnetArgs = new List<string>();
        var pastSeparator = false;

        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--", StringComparison.Ordinal))
            {
                pastSeparator = true;
                continue;
            }

            if (pastSeparator)
            {
                // Everything after -- goes to dotnet build
                dotnetArgs.Add(args[i]);
                continue;
            }

            // Check if it's a known music arg (--bpm 140, --scale Blues, etc.)
            if (KnownMusicArgs.TryGetValue(args[i], out var paramName) && i + 1 < args.Length)
            {
                i++;
                loggerParts.Add($"{paramName}={args[i]}");
            }
            else if (args[i] is "--no-live")
            {
                loggerParts.Add("Live=false");
            }
            else if (args[i] is "--pace")
            {
                loggerParts.Add("Pace=true");
            }
            else
            {
                // Not a music arg — pass through to dotnet build
                dotnetArgs.Add(args[i]);
            }
        }

        return (string.Join(';', loggerParts), dotnetArgs);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            dotnet music-build - Build your .NET project with music!

            Usage:
              dotnet music-build [music-options] [-- dotnet-build-options]

            Music options:
              --bpm <number>        Tempo in beats per minute (default: 120)
              --scale <name>        Scale: Major, Minor, Pentatonic, Blues, Chromatic (default: Pentatonic)
              --octave <0-9>        Base octave for melody (default: 4)
              --instrument <name>   Melody instrument (default: AcousticGrandPiano)
              --bass <name>         Bass instrument (default: SynthBass1)
              --pad <name>          Pad instrument (default: PadNewAge)
              --output <path.mid>   Also save a MIDI file
              --pace                Enable pacing (for binlog replay)
              --speed <number>      Playback speed multiplier (default: 1.0)
              --no-live             Disable live playback (only useful with --output)

            Examples:
              dotnet music-build                              # build with music
              dotnet music-build --bpm 140 --scale Blues      # blues at 140 BPM
              dotnet music-build --output build.mid           # also save MIDI file
              dotnet music-build -- -c Release /p:Foo=Bar     # pass args to dotnet build

            Instruments:
              AcousticGrandPiano, ElectricPiano1, Vibraphone, Marimba,
              ChurchOrgan, AcousticGuitarNylon, ElectricGuitarClean,
              OverdrivenGuitar, SynthBass1, Violin, StringEnsemble1,
              SynthStrings1, LeadSquare, PadNewAge, FxCrystal, SteelDrums
            """);
    }
#pragma warning restore CA1303
}
