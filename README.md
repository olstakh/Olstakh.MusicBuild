# LiveBuildLogger

An MSBuild logger that turns your .NET builds into music. Each project picks a musical key, targets become melodic phrases, tasks add texture, warnings hit percussion, and errors crash. A continuous drum loop and bass line provide rhythmic backbone throughout.

## Quick Start

```
dotnet build -logger:path/to/LiveBuildLogger.dll
```

By default, music plays live through your system's MIDI synthesizer (Windows only). No MIDI file is written unless you ask for one.

## Usage Examples

```bash
# Live playback with defaults (piano, pentatonic scale, 120 BPM)
dotnet build -logger:LiveBuildLogger.dll

# Save a MIDI file without live playback
dotnet build -logger:"LiveBuildLogger.dll;Output=build.mid;Live=false"

# Blues scale at 140 BPM with Rhodes piano
dotnet build -logger:"LiveBuildLogger.dll;BPM=140;Scale=Blues;Instrument=ElectricPiano1"

# Replay a binary log with original timing
dotnet msbuild build.binlog -logger:"LiveBuildLogger.dll;Pace=true"

# Replay at double speed, saving a MIDI file too
dotnet msbuild build.binlog -logger:"LiveBuildLogger.dll;Pace=true;Speed=2;Output=replay.mid"
```

## Parameters

Parameters are passed as semicolon-separated `key=value` pairs after the DLL path.

| Parameter | Default | Description |
|-----------|---------|-------------|
| `BPM` | `120` | Tempo in beats per minute |
| `Scale` | `Pentatonic` | Musical scale (see [Scales](#scales)) |
| `Octave` | `4` | Base MIDI octave for melody (0–9, where 4 ≈ middle C) |
| `Instrument` | `AcousticGrandPiano` | General MIDI instrument for melody (see [Instruments](#instruments)) |
| `Bass` | `SynthBass1` | General MIDI instrument for bass line |
| `Pad` | `PadNewAge` | General MIDI instrument for sustained harmony chords |
| `Output` | *(none)* | File path to write a `.mid` file. No file is written if omitted |
| `Live` | `true` | Enable/disable real-time MIDI playback (`true`/`false`) |
| `Pace` | `false` | Space events at original timing — essential for binlog replay |
| `Speed` | `1.0` | Playback speed multiplier (use with `Pace`). `2` = double speed, `0.5` = half |

### Scales

| Value | Character |
|-------|-----------|
| `Pentatonic` | Always consonant — safe default |
| `Major` | Bright, happy |
| `Minor` | Darker, more serious |
| `Blues` | Adds the "blue" note to pentatonic |
| `Chromatic` | All 12 semitones — maximum variety |

### Instruments

Any of the following names can be used for `Instrument`, `Bass`, or `Pad`:

| Value | GM Program | Description |
|-------|------------|-------------|
| `AcousticGrandPiano` | 0 | Classic piano |
| `ElectricPiano1` | 4 | Rhodes electric piano |
| `Vibraphone` | 11 | Vibraphone |
| `Marimba` | 12 | Marimba |
| `ChurchOrgan` | 19 | Church organ |
| `AcousticGuitarNylon` | 24 | Nylon acoustic guitar |
| `ElectricGuitarClean` | 27 | Clean electric guitar |
| `OverdrivenGuitar` | 29 | Overdriven electric guitar |
| `SynthBass1` | 38 | Synth bass |
| `Violin` | 40 | Violin |
| `StringEnsemble1` | 48 | String ensemble |
| `SynthStrings1` | 50 | Synth strings |
| `LeadSquare` | 80 | Square wave lead synth |
| `PadNewAge` | 88 | New Age / Fantasia pad |
| `FxCrystal` | 98 | Crystal effect |
| `SteelDrums` | 114 | Steel drums |

## How It Maps

| Build Concept | Musical Element |
|---------------|-----------------|
| Project | Musical key (deterministic from project name) |
| Target | Melody note — stepwise motion toward a scale degree |
| Task | Lighter melody note (thinned — every 4th task) |
| Warning | Open hi-hat accent |
| Error | Crash cymbal + bass drum |
| Build success | Arpeggiated resolving chord |
| Build failure | Low dissonant cluster + crash |

A continuous drum loop (kick on every beat, snare on 2 & 4, hi-hat on 8th notes) and bass line (root–fifth alternation) play throughout, providing rhythmic and harmonic structure.

## Playback Modes

| Mode | Parameters | Description |
|------|------------|-------------|
| Live only | *(default)* | Real-time MIDI through system synthesizer |
| Live + file | `Output=build.mid` | Live playback and saves a MIDI file |
| File only | `Output=build.mid;Live=false` | Writes MIDI file, no sound |

## Binlog Replay

When replaying a binary log, events fire instantly by default (no wall-clock gaps). Use `Pace=true` to space them at the original build timing:

```bash
dotnet msbuild build.binlog -logger:"LiveBuildLogger.dll;Pace=true"
```

Use `Speed` to adjust replay rate:

```bash
# 3x faster than original build
dotnet msbuild build.binlog -logger:"LiveBuildLogger.dll;Pace=true;Speed=3"
```

## Requirements

- .NET 10.0+
- Windows (for live MIDI playback via `winmm.dll`). MIDI file output works on any platform.

## Building

```bash
dotnet build
```
