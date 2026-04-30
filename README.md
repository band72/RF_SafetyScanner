# RF Safety Scanner v3 - .NET 8 WPF / C#

Separate RF monitoring application designed for HackRF/Airspy SDR workflows.

## The Problem Solved

Software Defined Radios (SDRs) like the HackRF are incredibly sensitive. When scanning wide frequency ranges or tuning near powerful local transmitters (like AM/FM broadcast towers, radar, or wideband bursts), the SDR can suddenly encounter massive RF energy spikes. 

If you are actively monitoring the audio output with headphones, these sudden saturation events can cause extreme, deafening bursts of static that can damage your hearing or blow out your speakers.

**RF Safety Scanner v3** solves this by acting as an automated, pre-emptive safety shield. It rapidly scans the spectrum *ahead* of your primary listening software and actively intervenes when it detects dangerous RF levels.

*(Note: This application detects receiver-relative electrical overload. It does **not** measure biological RF exposure and does **not** produce V/m or W/m² safety compliance readings.)*

---

## Key Safety Features

* **OS Master Volume Mute:** If a signal breaches the `Critical` threshold (e.g. `>= -58 dBFS`), the app instantly fires a Windows API (`user32.dll` P/Invoke) system call to mute your computer's OS Master Volume, protecting your ears before the static hits.
* **Visual Danger Meter:** A real-time, normalized (0-100) progress bar visually indicates current RF levels across the spectrum. It dynamically shifts colors (Green -> Orange -> Red) depending on the severity of the signal.
* **Hardware ADC Overload Protection:** The classifier engine distinguishes between real wideband threats and physical hardware ADC clipping (e.g., when the SDR front-end is entirely saturated across multiple frequencies). If front-end overload is detected, it mutes the audio to protect your speakers, but overrides the severity to prevent false-alarm auto-stops.
* **Auto-Stop Scan:** An optional UI configuration that forcefully halts the HackRF hardware polling loop the moment a critical RF spike is detected, completely shutting down the receiver until you choose to manually restart it.
* **Graceful Hardware Disconnect:** A dedicated `Close` sequence ensures that the unmanaged HackRF resources (`hackrf_stop_rx`, `hackrf_close`) are safely disposed of before terminating the application, preventing annoying USB port lockups.

---

## HackRF Setup & Installation

1. **Dependencies:** This application requires the unmanaged `hackrf.dll` library. 
2. **Placement:** Download the HackRF Windows tools and copy `hackrf.dll` (and its dependencies, such as `libusb-1.0.dll` and `pthreadVCE2.dll`) directly into the `\bin\Debug\net8.0-windows\` directory next to the compiled executable.
3. **Unblock Files:** If Windows prevents the DLLs from loading with an `(0x80070780)` error, right-click the downloaded `.dll` files, go to Properties, and check the **Unblock** box (or run `Unblock-File` in PowerShell).
4. *Note: A `MockScanner` is enabled by default for UI testing if the physical HackRF hardware/DLL is not found.*

## Build & Run

```powershell
dotnet build
dotnet run
```

---

## Application Defaults & Thresholds

* **Start Frequency:** `2.000 MHz` *(Bypasses the AM Broadcast band which commonly causes instant front-end overload)*
* **Alert:** `>= -65 dBFS` (Turns progress bar orange)
* **Critical/Mute:** `>= -58 dBFS` (Turns progress bar red, triggers OS Volume Mute)
* **Wideband Event:** `> 20 kHz`
* **Edge overload:** `> 100 kHz`
* **Pulse Score Trigger:** `>= 0.35`

## Recommended SDR Gain Settings
To prevent the `ADC_CLIPPING_OR_FRONT_END_OVERLOAD` safeguard from constantly triggering, ensure your hardware gain is set appropriately for your antenna size:
- **AMP:** OFF
- **LNA:** 0–8 dB
- **VGA:** 0–10 dB
- **Sample rate:** 2–5 MSPS for clean scanning
