# RF Safety Scanner v3 - .NET 8 WPF / C#

Separate RF monitoring app for HackRF/Airspy SDR workflows.

## Purpose

Detect and record receiver-relative strong RF events that may cause unsafe audio spikes.  
This does **not** measure biological RF exposure and does **not** produce V/m or W/m² safety compliance readings.

## v3 additions

- Radar-like / pulsed wideband detection
- Pulse score history
- Smart mute request for critical events
- Event classifier:
  - CARRIER
  - VOICE_OR_AM
  - WIDEBAND
  - PULSED_WIDEBAND
  - EDGE_OVERLOAD
  - CENTER_DC_SPIKE
- FFT/peak-profile visual panel
- Pulse-history visual panel
- CSV and JSONL logs

## Default thresholds

- Alert: `>= -65 dBFS`
- Critical/Mute: `>= -58 dBFS`
- Wideband: `> 20 kHz`
- Edge overload: `> 100 kHz`
- Ignore band edges: first/last `500 kHz`
- Pulse score: `>= 0.35`

## Recommended HackRF starting settings

- Correct IQ: ON
- AMP: OFF
- LNA: 8 dB
- VGA: 10 dB
- Noise floor target: `-90 to -85 dBFS`
- AM bandwidth: 10 kHz
- USB bandwidth: 3–5 kHz
- Sample rate: 10 MSPS for wide view or 2–5 MSPS for cleaner scanning

## Build

```powershell
dotnet build
dotnet run
```

## HackRF setup

Install HackRF tools for Windows and make `hackrf.dll` available in PATH or beside the EXE.  
Mock mode is enabled by default for UI testing if no HackRF DLL is found.

## Audio safety

Use an external limiter after SDR audio:

- Threshold: -10 dBFS
- Ceiling: -3 dBFS
- Attack: 1–5 ms
- Release: 50–100 ms
