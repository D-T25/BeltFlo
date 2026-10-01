# BeltFlo ESP32 firmware — first conveyor conversion

This is the first BeltFlo conversion of `Modules/ESP32/YieldFlo_ESP32`.
It deliberately keeps the proven YieldFlo ESP32 networking, captive portal,
EEPROM, W5500 Ethernet, TWAI CAN and ESP2SOTA OTA structure, and replaces the
grain-specific sensing with a conveyor scale and belt-distance input.

## What changed

- NAU7802 / Load Cell 2 Click replaces the ADS1115 moisture board.
- The old RPM input on GPIO 35 is reused as the belt proximity input.
- The existing EEPROM layout/version is retained, so current WiFi/Ethernet/CAN settings survive the firmware change.
- Optical grain-flow code is removed from the measurement path.
- BeltFlo PC settings PGN **40011** are received over UDP or CAN.
- Conveyor data PGN **40010** is sent at 5 Hz over UDP.
- BeltFlo CAN frames `0x18FF02F8` and `0x18FF03F8` are sent at 5 Hz.
- The firmware integrates conveyor mass as:

  `delivered_lb += section_lb / section_length_in * belt_travel_in`

- The module sets the BeltFlo status bits for Scale OK, Belt Running,
  zero/tare status, PC-settings heartbeat and converter overload.
- Pound integration is disabled until a real zero and span have both been received;
  the current PC default span of 1 lb/count is treated as an uncalibrated placeholder.
- The web main page now shows live scale, belt, settings and communication status.

## Hardware / pins

| Signal | GPIO | Notes |
|---|---:|---|
| NAU7802 SDA | 21 | ESP32/YF1 I2C SDA |
| NAU7802 SCL | 22 | ESP32/YF1 I2C SCL |
| Belt proximity pulse | 35 | Reuses YieldFlo RPM input; input-only pin, YF1 conditioning expected |
| CAN TX | 14 | To MCP2562 TXD |
| CAN RX | 27 | From MCP2562 RXD |
| W5500 SS | 5 | Ethernet chip select |
| W5500 SCK/MISO/MOSI | 18/19/23 | VSPI defaults |

The two conveyor load cells should be electrically combined into the single
weighing channel presented to the NAU7802, so the module sees one raw scale
value for the whole weighed section.

## Required Arduino libraries

Keep the existing YieldFlo dependencies and add:

- **SparkFun Qwiic Scale NAU7802 Arduino Library**
  - Header: `SparkFun_Qwiic_Scale_NAU7802_Arduino_Library.h`
  - `begin()` configures the NAU7802 for gain 128 and 80 samples/second.

Existing requirements still include ESP32 Arduino core, `Ethernet_Generic`, and
the bundled `src/ESP2SOTA_RC` code.

## BeltFlo UDP protocol

### Module -> PC

Port **30300**, PGN **40010**, 19 bytes:

| Bytes | Field |
|---|---|
| 0-1 | PGN 40010 little-endian |
| 2 | flags: bit0 ScaleOK, bit1 BeltRunning, bit2 Tared/zero set, bit3 ReceivingFromPC, bit4 Overload |
| 3-6 | cumulative pounds x10, uint32 LE |
| 7-10 | cumulative belt pulses, uint32 LE |
| 11-12 | live section pounds x10, int16 LE |
| 13-16 | filtered raw NAU7802 counts, int32 LE |
| 17 | reserved |
| 18 | byte-sum CRC8 |

### PC -> module

Module listens on port **30400**, PGN **40011**, 18 bytes. The 13-byte settings
block is exactly the layout in `BeltFlo/Communication/ModuleSettings.cs`:

| Block bytes | Field |
|---|---|
| 0-3 | zero counts, int32 |
| 4-7 | span lb/count, float32 |
| 8-9 | weighed section length x10 inches, uint16 |
| 10-11 | belt travel x1000 inches/pulse, uint16 |
| 12 | belt-stop timeout x10 seconds, uint8 |

The block is protected by CRC-16/CCITT-FALSE and the whole UDP packet by the
same byte-sum CRC8 used elsewhere in BeltFlo.

## BeltFlo CAN protocol

At 250 kbps, extended IDs:

- `0x18FF02F8`: cumulative pounds x10 + cumulative pulses
- `0x18FF03F8`: status flags + scale pounds x10 + raw counts
- `0x18FF04F9`: PC settings block bytes 0-7
- `0x18FF05F9`: PC settings block bytes 8-12 + CRC-16

## Applying this patch

Replace these files inside the current `Modules/ESP32/YieldFlo_ESP32` folder:

- `YieldFlo_ESP32.ino`
- `Begin.ino`
- `Analog.ino`
- `Flow.ino`
- `Comm.ino`
- `PgMain.ino`
- `GUI.ino`
- `README.md`

Keep the current `Wifi.ino`, `PgWifi.ino`, `PgUpdate.ino` and
`src/ESP2SOTA_RC/` files. They are intentionally reused unchanged for now.

The folder/main-sketch name is left as `YieldFlo_ESP32` in this first patch so
Arduino and the existing Visual Micro project continue to open without a rename.
The running firmware identifies itself as **BeltFlo_ESP32**. Existing communication settings are retained; an untouched default `YieldFlo_ESP32` hotspot name is migrated to `BeltFlo_ESP32`, while custom hotspot names are preserved.

## First bench test

1. Install the SparkFun NAU7802 library.
2. Apply the replacement files above and compile/upload.
3. Connect to the BeltFlo hotspot and open the module page.
4. Confirm `Scale = OK` and that raw counts change when weight is applied.
5. Run BeltFlo PC app and confirm `PC settings = Receiving`.
6. Rotate the belt sensor target by hand and confirm `Belt pulses` increments.
7. Enter/activate calibration and conveyor geometry in the PC app once those
   setup screens are available, or inject PGN 40011 from the simulator/test tool.
8. Put a known weight on the section, move the belt a known distance, and verify
   cumulative pounds follows `weight / section length * belt travel`.

## Known first-pass limits

- Conveyor calibration/setup screens in the PC app are still listed as not built
  in the repository README, so end-to-end field calibration is not yet complete.
- Belt pulse GPIO remains fixed at the old RPM input (GPIO 35) in the portal.
- The WiFi and firmware-update sub-pages still come from the current YieldFlo
  files; their page titles may still say YieldFlo until the cosmetic rename pass.
- This code has been protocol-checked against the current BeltFlo source, but it
  has not yet been compiled on the target Arduino toolchain or tested on hardware.
