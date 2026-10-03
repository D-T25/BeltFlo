# BeltFlo live yield on the AgOpenGPS main map

This integration makes a custom AgOpenGPS build display BeltFlo's yield map directly on the normal AOG field screen.

## Data flow

`ESP32 scale -> BeltFlo -> dig-to-scale delay correction -> AOG loopback UDP -> colored swath overlay`

BeltFlo sends message `0xC7` to AOG's existing loopback receiver on port 15555. The point is already corrected for BeltFlo's configured processing delay, so AOG draws the supplied position without applying another lag.

The live overlay is deliberately separate from AOG's normal section-control coverage. It is not rendered into `oglBack`, the hidden green overlap map AOG scans for section switching. Yield colors therefore cannot turn sections on or off.

## Current color scale

The first implementation uses eight fixed bands from **20,000 to 80,000 lb/ac**:

dark red -> red -> orange -> yellow -> yellow-green -> green -> cyan -> blue

BeltFlo always transmits lb/ac internally even when its display is set to cwt/ac or tons/ac.

## Persistence

The custom AOG build saves received samples in the open AOG field folder as:

`BeltFloYield.txt`

When that field is reopened, the overlay is rebuilt from that file.

## Build

The BeltFlo GitHub workflow **BeltFlo AgOpenGPS Build** checks out the pinned upstream AOG source, applies this integration, builds/tests/publishes AgOpenGPS, and uploads an `AgOpenGPS-BeltFlo` artifact.

The AOG source is pinned so an upstream source change cannot silently break the patch.