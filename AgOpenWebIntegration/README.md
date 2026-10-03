# BeltFlo + AgOpenWeb

This integration lets the same BeltFlo live-yield stream work with AgOpenWeb as well as classic AgOpenGPS.

## How it works

- BeltFlo sends its delay-corrected live-yield packet (`0xC7`) to both local guidance ports:
  - classic AgOpenGPS: `127.0.0.1:15555`
  - AgOpenWeb host: `127.0.0.1:9999`
- Whichever guidance app is running consumes the packet. No BeltFlo mode switch is required.
- The AgOpenWeb receiver converts BeltFlo's WGS84 point through the field's own `LocalPlane` and recolors the existing coverage DISPLAY pixels.
- Detection coverage is never changed by BeltFlo yield color, so automatic section control and overlap logic are unaffected.
- The same adjustable Low/High yield range from BeltFlo Settings travels in every packet.

## AgOpenWeb -> BeltFlo GPS/sections

The custom AgOpenWeb build also sends BeltFlo the classic GPS/heading, speed, and 64-section state packets on loopback port 17777. BeltFlo therefore gets the same inputs whether the guidance host is AgOpenGPS or AgOpenWeb.

## Web/iPad display

The first supported deployment has **BeltFlo and the AgOpenWeb host running on the same Windows computer**. BeltFlo and the host communicate over loopback, while the iPad/phone/browser can connect to AgOpenWeb over the normal LAN connection.

AgOpenWeb's browser clients already receive the coverage display layer from the host. Because the BeltFlo integration changes only that display layer, the yield colors are sent through the normal AgOpenWeb coverage websocket path and appear in the browser/iPad map without a separate browser plugin.

Running the AgOpenWeb backend itself on a different physical device is not part of this first version; that would require a configurable LAN target instead of loopback.

## Updates

`BeltFlo AgOpenWeb Build` checks out a pinned upstream AgOpenWeb commit, applies the small BeltFlo patch, builds/tests the upstream projects, publishes the Windows host, and uploads an `AgOpenWeb-BeltFlo` artifact. To adopt a new AgOpenWeb release, move the pinned commit and let CI prove whether the patch still applies cleanly.