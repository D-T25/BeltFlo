# BeltFlo

> **Work in progress — not ready for field use.** The PC app runs and has been tested against simulators, but key setup screens are missing, the module firmware has not been converted from YieldFlo, and the manual still describes YieldFlo. See [Status](#status).

BeltFlo is a yield monitor for root-crop harvesters — potatoes, sugar beets, carrots, onions — that works alongside [AgOpenGPS](https://github.com/AgOpenGPS-Official/AgOpenGPS). It weighs the crop on a conveyor with load cells, maps yield across the field, and keeps a weight for every truck load so certified ticket weights can correct the map.

It is a fork of [YieldFlo](https://github.com/SK21/YieldFlo) (Development branch, September 2026), the grain yield monitor. The GPS, CAN, field, map and job code comes from YieldFlo; the grain sensing and calibration were removed.

## How it works

- **Module** — the YF1 board with its ESP32, a Load Cell 2 Click (NAU7802) reading two load cells under a weighed section of the conveyor, and a proximity sensor on the belt drive. The module multiplies the weight on the section by belt travel and streams cumulative pounds and belt pulses to the PC over WiFi/Ethernet UDP or CAN.
- **AgOpenGPS** supplies position, speed and section on/off state over UDP.
- **BeltFlo (PC app)** pairs the weight with where the crop was dug (allowing for the time it takes to reach the scale), subtracts overlapping ground, stores every point in a local SQLite database, and tracks truck loads. It sends the module its calibration and geometry every 2 s; the module confirms it is hearing them, so the app can show the link is working both ways.

Everything is stored in pounds and pounds per acre, and shown as cwt/ac or tons/ac, or t/ha in metric.

## Status

**Working in the PC app** (tested with the AgOpenGPS simulator and the module simulator):

- Conveyor data from the module over UDP and CAN, with scale, zero, overload and dead-belt-sensor checks on the status bar
- Settings sent to the module, and a two-way link check on the Module status light
- Jobs, truck loads (▶ start, ⏹ finish), and map points tagged with their load
- ⏸ Pause — stops all counting, for cleaning the belt or clearing a jam — with an optional auto-resume when sections come on, or an alarm if it is off
- Weight below an empty-belt threshold is not counted
- Overlap compensation, so a short last pass needs no row adjustment
- Calibration revisions recorded on every point and load, so a later span change can rescale earlier data

**Not built yet:**

- Harvester profile with rows, row spacing and scale location (replacing YieldFlo's headers), and rows harvested per job for windrowed crop
- Conveyor Setup screen (belt travel per pulse, weighed section length, delay, thresholds)
- Scale Calibration screen (zero, known weight)
- Loads screen — certified ticket weights and load or whole-job correction
- "No load open" alarm, truck-full alarm, main screen redesign
- **Module firmware.** `Modules/ESP32` still holds the YieldFlo grain firmware, to be converted.
- **Documentation.** The user manual still describes YieldFlo, and there is no diagnostic log guide yet.
- Translations for the new BeltFlo text (the other seven languages fall back to English)

## Repository layout

| Folder | Contents |
|---|---|
| [`BeltFlo/`](BeltFlo) | The Windows Forms PC app (.NET Framework 4.8) |
| [`BeltFloApp/`](BeltFloApp) | Runnable build of the app (exe and resources) |
| [`ModuleSimulator/`](ModuleSimulator) | Conveyor module simulator — load and belt-speed sliders, fault switches, receives the app's settings |
| [`ModuleSimulatorApp/`](ModuleSimulatorApp) | Runnable build of the simulator |
| [`Modules/ESP32`](Modules/ESP32) | Module firmware for the YF1 board's ESP32 — **still YieldFlo's grain firmware**, kept as the starting point for the conveyor firmware (WiFi, CAN, web portal, OTA) |
| [`PCBs/YF1`](PCBs/YF1) | KiCad design for the YF1 module board, shared with YieldFlo |

The module packet layouts are documented in the code: `BeltFlo/Communication/UDPcomm.cs` (module → PC, PGN 40010) and `BeltFlo/Communication/ModuleSettings.cs` (PC → module, PGN 40011).

## Trying it

For development and testing only.

1. Build `BeltFlo.sln` (Visual Studio, .NET Framework 4.8), or run `BeltFloApp/BeltFlo.exe`.
2. Run `ModuleSimulatorApp/ModuleSimulator.exe`. The module link uses UDP ports 30300 (module → PC) and 30400 (PC → module).
3. Run AgOpenGPS in simulator mode with a field open.
4. In BeltFlo, create a job on the Jobs screen, tick **Harvesting** in the simulator, turn sections on in AgOpenGPS, and press ▶ to open a load.

Diagnostic CSVs and logs are written to `Documents\BeltFlo`.

### Rebuilding the manual

`BeltFlo/Help/` holds the manual as `.md`, `.html` and `.pdf`. All three ship, so all three have to be kept in step — the `.md` and `.html` are edited by hand, and the `.pdf` is printed from the `.html` by headless Edge:

```
"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe" ^
  --headless=new --disable-gpu --no-pdf-header-footer ^
  --print-to-pdf="BeltFlo User Manual.pdf" ^
  "file:///F:/path/to/BeltFlo/Help/BeltFlo User Manual.html"
```

The build copies `Help/**` into `BeltFloApp/Help/`; that copy is output, not a second source to edit.

## License

GPL-3.0 — see [`LICENSE`](LICENSE).
