# BeltFlo live yield on the AgOpenGPS main map

This integration makes a custom AgOpenGPS build display BeltFlo's yield map directly on the normal AOG field screen.

## Data flow

`ESP32 scale -> BeltFlo -> dig-to-scale delay correction -> AOG loopback UDP -> colored swath overlay`

BeltFlo sends message `0xC7` to AOG's existing loopback receiver on port 15555. The point is already corrected for BeltFlo's configured processing delay, so AOG draws the supplied position without applying another lag.

The live overlay is deliberately separate from AOG's normal section-control coverage. It is not rendered into `oglBack`, the hidden green overlap map AOG scans for section switching. Yield colors therefore cannot turn sections on or off.

## Current color scale

The overlay uses eight stable bands:

dark red -> red -> orange -> yellow -> yellow-green -> green -> cyan -> blue

Set the **Low** and **High** endpoints in **BeltFlo Settings -> AOG Yield Color Range**. The defaults are **20,000 and 80,000 lb/ac**. BeltFlo shows those values in the currently selected yield units (lb/ac, cwt/ac, tons/ac, or t/ha), stores them internally in lb/ac, and sends them with every live-yield packet. Changing the range therefore recolors the whole AOG overlay without rebuilding AgOpenGPS.

## Persistence

The custom AOG build saves received samples in the open AOG field folder as:

`BeltFloYield.txt`

When that field is reopened, the overlay is rebuilt from that file.

## Build

The BeltFlo GitHub workflow **BeltFlo AgOpenGPS Build** checks out the pinned upstream AOG source, applies this integration, builds/tests/publishes AgOpenGPS, and uploads an `AgOpenGPS-BeltFlo` artifact.

The AOG source is pinned so an upstream source change cannot silently break the patch.