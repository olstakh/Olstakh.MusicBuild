# LiveBuildLogger

An MSBuild logger that turns your .NET builds into music. Each project picks a musical key, targets become melodic phrases, tasks add texture, warnings hit percussion, and errors crash. A continuous drum loop and bass line provide rhythmic backbone throughout.

## Quick Start

### As a dotnet tool (easiest)

```bash
# Install globally
dotnet tool install --global Olstakh.MusicBuild

# Build any project with music
dotnet music-build

# Customize the sound
dotnet music-build --bpm 140 --scale Blues --instrument ElectricPiano1

# Save a MIDI file too
dotnet music-build --output build.mid

# Pass arguments to dotnet build
dotnet music-build -- -c Release
```

## Tool Options

```
dotnet music-build [music-options] [-- dotnet-build-options]
```

| Option | Description |
|--------|-------------|
| `--bpm <number>` | Tempo in beats per minute (default: 120) |
| `--scale <name>` | Scale type (default: Pentatonic) |
| `--octave <0-9>` | Base octave for melody (default: 4) |
| `--instrument <name>` | Melody instrument (default: AcousticGrandPiano) |
| `--bass <name>` | Bass instrument (default: SynthBass1) |
| `--pad <name>` | Pad instrument (default: PadNewAge) |
| `--output <path.mid>` | Also save a MIDI file |
| `--pace` | Pace events to original timing (for binlog replay) |
| `--speed <number>` | Playback speed multiplier (default: 1.0) |
| `--no-live` | Disable live playback (only useful with --output) |
| `--help` | Show help |

Everything after `--` is passed directly to `dotnet build`.

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


## Binlog Replay

Replay a previously recorded binary log with music:

```bash
# Replay at original build speed (pacing is on by default)
dotnet music-build replay build.binlog

# 3x faster
dotnet music-build replay build.binlog --speed 3

# Blues scale at 140 BPM, save a MIDI file
dotnet music-build replay build.binlog --scale Blues --bpm 140 --output replay.mid
```

Use `--no-pace` if you want all events to fire instantly (no timing gaps).

<details>
<summary>Using the raw logger instead of the tool</summary>

```bash
dotnet msbuild build.binlog -logger:"LiveBuildLogger.dll;Pace=true"
dotnet msbuild build.binlog -logger:"LiveBuildLogger.dll;Pace=true;Speed=3"
```

</details>

## Requirements

- .NET 10.0+
- Windows (for live MIDI playback via `winmm.dll`). MIDI file output works on any platform.

## Building

```bash
dotnet build
```

