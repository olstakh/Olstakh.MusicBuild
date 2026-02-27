using System.Diagnostics;

namespace MusicBuild.Tool;

/// <summary>
/// Entry point for the <c>dotnet music-build</c> global tool.
/// Wraps <c>dotnet build</c> (or replays a binlog) and injects the MusicBuild
/// as an MSBuild logger, so users get music without manually specifying <c>-logger:</c>.
/// <para>
/// Usage:
/// <code>
/// dotnet music-build                              # builds current project with music
/// dotnet music-build --bpm 140 --scale Blues       # custom music parameters
/// dotnet music-build -- -c Release                 # passes -c Release to dotnet build
/// dotnet music-build replay build.binlog --pace    # replay a binlog with pacing
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
        var loggerDll = Path.Combine(toolDir, "MusicBuild.dll");

        if (!File.Exists(loggerDll))
        {
            Console.Error.WriteLine("Error: Could not find MusicBuild.dll alongside the tool.");
            Console.Error.WriteLine($"Expected at: {loggerDll}");
            return 1;
        }

        // Check for "replay" subcommand
        if (args.Length > 0 && string.Equals(args[0], "replay", StringComparison.OrdinalIgnoreCase))
        {
            return RunReplay(args.AsSpan(1), loggerDll);
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

        return RunDotnet(dotnetCommand);
    }

    /// <summary>
    /// Handles <c>dotnet music-build replay &lt;file.binlog&gt; [music-options]</c>.
    /// Replays a binary log through <c>dotnet msbuild</c> with the logger attached.
    /// Automatically enables <c>Pace=true</c> unless <c>--no-pace</c> is specified.
    /// </summary>
    private static int RunReplay(ReadOnlySpan<string> args, string loggerDll)
    {
        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            Console.Error.WriteLine("Error: Expected a .binlog file path after 'replay'.");
            Console.Error.WriteLine("Usage: dotnet music-build replay <file.binlog> [--speed 3] [--bpm 140] ...");
            return 1;
        }

        var binlogPath = args[0];

        if (!File.Exists(binlogPath))
        {
            Console.Error.WriteLine($"Error: File not found: {binlogPath}");
            return 1;
        }

        var (loggerParams, showHelp) = ParseReplayArgs(args[1..]);
        if (showHelp)
        {
            PrintReplayHelp();
            return 0;
        }

        var loggerArg = string.IsNullOrEmpty(loggerParams)
            ? $"-logger:\"{loggerDll}\""
            : $"-logger:\"{loggerDll};{loggerParams}\"";

        var dotnetCommand = $"msbuild \"{binlogPath}\" {loggerArg}";

        Console.WriteLine($"  \u266b  Replaying {Path.GetFileName(binlogPath)} with music...");
        Console.WriteLine();

        return RunDotnet(dotnetCommand);
    }

    /// <summary>
    /// Parses replay-specific arguments into logger parameters.
    /// Returns the logger parameter string and whether help was requested.
    /// </summary>
    private static (string LoggerParams, bool ShowHelp) ParseReplayArgs(ReadOnlySpan<string> args)
    {
        var loggerParts = new List<string>();
        var paceExplicitlyDisabled = false;

        for (var i = 0; i < args.Length; i++)
        {
            if (KnownMusicArgs.TryGetValue(args[i], out var paramName) && i + 1 < args.Length)
            {
                i++;
                loggerParts.Add($"{paramName}={args[i]}");
            }
            else if (args[i] is "--no-live")
            {
                loggerParts.Add("Live=false");
            }
            else if (args[i] is "--no-drums")
            {
                loggerParts.Add("Drums=false");
            }
            else if (args[i] is "--no-bass-line")
            {
                loggerParts.Add("BassLine=false");
            }
            else if (args[i] is "--pace")
            {
                loggerParts.Add("Pace=true");
            }
            else if (args[i] is "--no-pace")
            {
                paceExplicitlyDisabled = true;
            }
            else if (args[i] is "--help" or "-h" or "-?")
            {
                return (string.Empty, true);
            }
            else
            {
                Console.Error.WriteLine($"Warning: Unknown option '{args[i]}' ignored.");
            }
        }

        // Pace is on by default for replay (the whole point is hearing the build timing)
        if (!paceExplicitlyDisabled)
        {
            loggerParts.Add("Pace=true");
        }

        return (string.Join(';', loggerParts), false);
    }

    private static int RunDotnet(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            Console.Error.WriteLine("Error: Failed to start 'dotnet'.");
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
            else if (args[i] is "--no-drums")
            {
                loggerParts.Add("Drums=false");
            }
            else if (args[i] is "--no-bass-line")
            {
                loggerParts.Add("BassLine=false");
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
              dotnet music-build replay <file.binlog> [music-options]

            Music options:
              --bpm <number>        Tempo in beats per minute (default: 120)
              --scale <name>        Scale: Major, Minor, Pentatonic, Blues, Chromatic (default: Pentatonic)
              --octave <0-9>        Base octave for melody (default: 4)
              --instrument <name>   Melody instrument (default: AcousticGrandPiano)
              --bass <name>         Bass instrument (default: SynthBass1)
              --pad <name>          Pad instrument (default: PadNewAge)
              --output <path.mid>   Also save a MIDI file
              --pace                Enable pacing (for binlog replay — on by default with 'replay')
              --speed <number>      Playback speed multiplier (default: 1.0)
              --no-drums            Disable the drum loop
              --no-bass-line        Disable the bass line
              --no-live             Disable live playback (only useful with --output)

            Commands:
              replay <file.binlog>  Replay a binary log with music (auto-enables --pace)

            Examples:
              dotnet music-build                              # build with music
              dotnet music-build --bpm 140 --scale Blues      # blues at 140 BPM
              dotnet music-build --output build.mid           # also save MIDI file
              dotnet music-build -- -c Release /p:Foo=Bar     # pass args to dotnet build
              dotnet music-build replay build.binlog          # replay binlog with music
              dotnet music-build replay build.binlog --speed 3  # replay at 3x speed

            Instruments:
              AcousticGrandPiano, ElectricPiano1, Vibraphone, Marimba,
              ChurchOrgan, AcousticGuitarNylon, ElectricGuitarClean,
              OverdrivenGuitar, SynthBass1, Violin, StringEnsemble1,
              SynthStrings1, LeadSquare, PadNewAge, FxCrystal, SteelDrums
            """);
    }

    private static void PrintReplayHelp()
    {
        Console.WriteLine("""
            dotnet music-build replay - Replay a binary log with music

            Usage:
              dotnet music-build replay <file.binlog> [options]

            Options:
              --bpm <number>        Tempo in beats per minute (default: 120)
              --scale <name>        Scale: Major, Minor, Pentatonic, Blues, Chromatic (default: Pentatonic)
              --octave <0-9>        Base octave for melody (default: 4)
              --instrument <name>   Melody instrument (default: AcousticGrandPiano)
              --bass <name>         Bass instrument (default: SynthBass1)
              --pad <name>          Pad instrument (default: PadNewAge)
              --output <path.mid>   Also save a MIDI file
              --speed <number>      Playback speed multiplier (default: 1.0)
              --no-drums            Disable the drum loop
              --no-bass-line        Disable the bass line
              --no-pace             Disable pacing (events fire instantly)
              --no-live             Disable live playback (only useful with --output)

            Examples:
              dotnet music-build replay build.binlog            # replay at original speed
              dotnet music-build replay build.binlog --speed 3  # 3x speed
              dotnet music-build replay build.binlog --scale Blues --bpm 140
            """);
    }
#pragma warning restore CA1303
}
