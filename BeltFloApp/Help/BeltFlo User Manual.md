# BeltFlo User Manual

> **Development build - test before field use.** The BeltFlo PC application and ESP32 firmware build successfully and the application protocol/logic tests pass, but the YF1/NAU7802 hardware still needs full bench and field validation before relying on it for harvest records.

BeltFlo is a conveyor-based yield monitor for root-crop harvesters such as potatoes, sugar beets, carrots, and onions. It works alongside AgOpenGPS (AOG), weighs crop on a conveyor section with load cells, and maps yield across the field. A direct-to-truck scale can also weigh each truck load; a scale mounted before a holding tank keeps truck tickets separately and uses their total to check or correct the job.

BeltFlo stores weight internally in pounds and yield in pounds per acre. Display units can be changed to **lb/ac**, **cwt/ac**, **tons/ac**, or **t/ha** without changing the stored data.

---

## Table of Contents

1. Getting Started
2. How BeltFlo Works
3. First-Time Setup
4. Main Screen
5. Harvester Profiles
6. Conveyor Setup
7. Scale Calibration
8. Jobs and Rows Harvested
9. Loads and Truck Tickets
10. Crops and Fields
11. Yield Map
12. Job Report and CSV Export
13. Settings
14. Module Connection and Web Portal
15. Testing with the Module Simulator
16. Troubleshooting
17. Files, Logs, and Data

---

## 1. Getting Started

### Requirements

| Item | Requirement |
|---|---|
| Operating system | Windows 10 or Windows 11 |
| Runtime | .NET Framework 4.8 |
| AgOpenGPS | Running on the same PC or local network for GPS, speed, and section state |
| BeltFlo module | ESP32/YF1 module with NAU7802 scale input and belt proximity sensor, or the Module Simulator for testing |

No installer is required for the development build. Keep the files in the **BeltFloApp** folder together and run `BeltFlo.exe` from that folder.

### Hardware overview

The BeltFlo module uses:

- an ESP32/YF1 controller,
- a NAU7802 load-cell interface,
- two conveyor load cells combined into one weighing channel,
- a proximity sensor on the belt drive for belt travel,
- WiFi/Ethernet UDP or CAN communication to the PC.

The module measures the weight sitting on the weighed conveyor section and combines it with belt travel. It sends cumulative delivered pounds, cumulative belt pulses, live section weight, and raw scale counts to the PC.

### Network ports in WiFi/Ethernet mode

| Direction | Port | Purpose |
|---|---:|---|
| Module -> PC | 30300 | Conveyor status and cumulative counters, PGN 40010 |
| PC -> Module | 30400 | Calibration and conveyor geometry, PGN 40011 |

The PC also receives AOG GPS and section data over the existing AgOpenGPS UDP connection.

---

## 2. How BeltFlo Works

BeltFlo has three main data sources:

1. **The conveyor module** supplies cumulative pounds, belt pulses, live scale weight, raw counts, belt-running status, scale health, overload status, and confirmation that the module is receiving settings from the PC.
2. **AgOpenGPS** supplies GPS position, speed, heading, and section on/off state.
3. **The BeltFlo PC app** combines those data streams, applies the dig-to-scale delay, removes overlap, stores points in SQLite, and assigns weight to the open truck load.

### Conveyor weight calculation

The module uses the weighed-section load and belt travel:

`delivered pounds = section pounds / section length x belt travel`

For example, if 20 lb is sitting on a 36 in weighed section and the belt moves 36 in, that represents 20 lb delivered.

### Dig-to-scale delay

Crop is dug at the harvester first and reaches the scale later. BeltFlo delays the scale weight back to the GPS position where that crop was dug. Set this in **Conveyor Setup -> Dig to Scale Delay**.

### Overlap compensation

BeltFlo tracks already-harvested ground. If a pass overlaps a previous pass, only the new uncovered part is credited with area and yield. This means a short last pass generally does not require manually changing the row count just because only part of the machine is in crop.

### Calibration revisions

Conveyor calibration records are revisioned. Yield points and loads remember which calibration revision produced them. This lets later ticket corrections rescale previously recorded data without losing the original calibration history.

---

## 3. First-Time Setup

For a new machine, do the setup in this order:

1. Open **Menu -> Settings** and choose Imperial or Metric units.
2. Open **Menu -> Profiles** and create the harvester profile.
3. Enter the machine's row count and row spacing. BeltFlo calculates harvester width from those values.
4. Enter the harvester's AOG pivot-to-digger distance.
5. Choose whether the scale weighs directly into the **Truck** or into a **Tank**.
6. Open **Menu -> Conveyor Setup** and enter or measure belt travel per pulse, weighed-section length, belt-stop timeout, empty-belt threshold, and dig-to-scale delay.
7. Open **Menu -> Scale Calibration** and perform **Zero Scale** with the belt running empty.
8. Perform **Known Weight** with the belt stopped and a known test weight resting on the weighed section.
9. Create the crop and field if needed.
10. Create/start a job.
11. Press **Start** on the main screen to open **Load 1**.

Do not expect BeltFlo to total weight until a valid zero and span calibration have been saved.

---

## 4. Main Screen

The main screen is the normal operating screen during harvest.

### Main readings

| Display | Meaning |
|---|---|
| **YIELD** | Current smoothed yield in lb/ac, cwt/ac, tons/ac, or t/ha |
| **LOAD n / TRUCK n** | **Direct to Truck:** live load weight. **Before Tank:** shows which truck ticket record is open; scale pounds continue to the job, not that truck. |
| **Flow bar** | Crop flow over the scale in lb/min or kg/min |
| **Belt bar** | Conveyor belt speed in ft/min or m/min |
| **Area** | Job area |
| **Total** | Job mass |
| **Average yield** | Total job mass divided by total job area |
| **Work rate** | Current mass flow rate |

Tap the **LOAD** title to open the Loads screen.

### Start, Pause, and End/Stop buttons

These buttons control the **load** and manual counting state. They do not all mean "stop the job."

| Button | Action |
|---|---|
| **Start** | Opens a new truck record when none is open. Direct-to-truck records collect scale pounds; Before Tank records are ticket/log entries only. If BeltFlo is manually paused, Start resumes instead of opening a new record. |
| **Pause** | Stops job counting and map point recording. Use for cleaning the belt, clearing a jam, or intentionally running material that should not count. |
| **End/Stop** | Closes the current truck record. Direct-to-truck freezes its monitor weight; Before Tank simply closes the ticket record. The job stays active. |

### Normal truck cycle

For direct-to-truck operation, the normal sequence is:

**Start Load 1 -> harvest -> End -> Start Load 2 -> harvest -> End -> Start Load 3**

When **End/Stop** is pressed, BeltFlo closes the current load and keeps the job running. Pressing **Start** after that creates the next load at 0 lb.

If the operator presses **Pause**, then presses **Start**, BeltFlo resumes the paused load instead of creating a new one.

### Weight between loads

With **Direct to Truck**, crop crossing the scale between truck records still counts to the job but cannot be assigned to a truck, so BeltFlo warns after sustained flow with no load open. With **Before Tank**, scale pounds always belong to the job and truck records are optional for measurement; no no-load or truck-full alarm is generated from the pre-tank scale.

### Status bar

The status bar checks several parts of the system separately.

| Indicator | Good | Warning / Fault |
|---|---|---|
| **GPS** | AOG is sending valid data | Red when AOG/GPS data is missing |
| **Module** | Data is arriving and the module confirms it is receiving PC settings | Orange if packets arrive but the module is not hearing the PC; red if module packets stop |
| **Scale / Zero / Belt** | Converter healthy, no overload, valid calibration, belt pulses believable | Red for scale/overload; orange for calibration or suspected belt-sensor problems |
| **Job** | Job recording | Orange when not recording or manually paused; red on data-write fault |

### Alarms

BeltFlo can warn for:

- manual pause while AOG sections come back on,
- crop flow with no truck load open,
- configured truck-full weight reached,
- scale overload or other scale faults through the status indicators.

---

## 5. Harvester Profiles

**Menu -> Profiles**

A profile describes the harvester geometry and how the scale relates to truck loads. Conveyor setup and scale calibration are tied to the profile.

### Profile fields

| Field | Description |
|---|---|
| **Name** | Harvester/profile name |
| **Harvester ID** | Optional identifier for the machine |
| **Rows** | Physical row count of the harvester |
| **Row Spacing** | Row spacing in inches or centimetres |
| **Ahead of Pivot** | AOG pivot-to-digger distance; negative values are valid for a towed implement behind the pivot |
| **Scale Position** | **Direct to Truck** or **Before Tank** |

### Harvester width

BeltFlo does not require a separate width entry in the profile. Width is calculated as:

`harvester width = rows x row spacing`

Example: 6 rows x 36 in = 216 in = 18 ft.

The calculated digging width is shown on the profile screen.

### Direct to Truck vs Before Tank

Choose **Direct to Truck** when crop crossing the scale goes directly into the truck being tracked. BeltFlo assigns those scale pounds and map points to the open load, can warn when crop flows with no load open, and can compare or correct each load from its own certified ticket.

Choose **Before Tank** when the weighing conveyor is upstream of an on-machine holding tank, for example on the rear scrub elevator of a beet lifter. The scale measures field yield before crop can be stored or mixed. BeltFlo **does not assign those scale pounds or map points to an individual truck**. Truck records are only a ticket log.

After the job is finished and crop belonging to that job has been emptied/cleaned out of the tank, the sum of all truck tickets can correct the whole job and can optionally update the scale span.

In Before Tank mode it does not matter whether the machine unloads stopped, unloads while harvesting, or runs temporarily with an empty tank: the crop was already weighed before the tank.

### Editing profiles

Changes to the active profile take effect when the active job configuration is reloaded. Conveyor geometry and calibration should not be changed during a running job.

---

## 6. Conveyor Setup

**Menu -> Conveyor Setup**

Conveyor Setup stores belt geometry, the weighed-section length, thresholds, and the dig-to-scale delay for the active harvester profile.

### Settings

| Setting | Description |
|---|---|
| **Belt per Pulse** | Belt travel for one proximity-sensor pulse |
| **Pulses per Belt Turn** | Number of pulses during one complete belt revolution |
| **Weigh Section Length** | Length of the conveyor section supported by the scale/load cells |
| **Empty Belt Below** | Flow below this rate is treated as empty-belt noise and is not credited as crop |
| **Belt Stopped After** | Time without a pulse before the module reports the belt stopped |
| **Dig to Scale Delay** | Seconds from digging the crop until it reaches the scale |

### Live diagnostics

With a module connected, the screen shows:

- pulses counted since the distance reset,
- pulse rate,
- belt speed,
- calculated belt distance,
- belt running/stopped state.

Use **Reset Distance** when checking travel over a known distance.

### Measure Belt

The **Measure Belt** function can calculate Belt per Pulse:

1. Make a visible mark on the belt.
2. Press **Measure Belt**.
3. Run the belt exactly one full revolution until the mark returns.
4. Press **Stop**.
5. Enter the full belt length.
6. BeltFlo calculates pulses per turn and belt travel per pulse.
7. Press **Save**.

If no pulses are counted, check the proximity sensor, its target, and wiring.

### Changing conveyor settings

BeltFlo blocks saving conveyor settings while a job is running. Finish or unload the active job before changing geometry that would affect recorded pounds or position.

---

## 7. Scale Calibration

**Menu -> Scale Calibration**

Scale calibration has two parts:

1. **Zero Scale** - raw counts with the empty belt running.
2. **Known Weight** - span in pounds per raw count with a known test weight on the stopped section.

Nothing is permanently changed until **Save** is pressed.

### Live readings

The calibration screen shows:

- raw NAU7802 counts,
- calculated live section weight once a span exists,
- stable/unstable indication,
- current/new zero,
- current/new span.

### Zero Scale

Use a running empty belt so BeltFlo averages belt irregularities instead of zeroing on one heavy or dirty spot.

1. Make sure no crop is on the weighed section.
2. Start the conveyor and let it run empty.
3. Open **Scale Calibration**.
4. Press **Zero Scale**.
5. Keep the belt running empty.
6. If Pulses per Belt Turn is known, BeltFlo samples at least 10 seconds and at least one full belt revolution.
7. If pulses per turn has not been set, BeltFlo uses a 30-second fallback.
8. When finished, the screen shows the new zero as **not saved**.
9. Press **Save** when ready.

### Known Weight

A valid zero is required before Known Weight.

1. Stop the conveyor.
2. Place a known test weight fully on the weighed conveyor section. Nothing else should touch or support the section.
3. Press **Known Weight**.
4. Enter the known weight in lb or kg, according to the current units.
5. Leave the weight still for the 5-second sample.
6. BeltFlo calculates the new span in lb/count.
7. Press **Save**.

If the raw reading moves less than about 100 counts, BeltFlo rejects the span calculation. Check that the test weight is actually loading the weighed section and that Zero Scale was completed first.

### Calibration validity

An uncalibrated profile sends a span of 0 to the module. The module will not accumulate pounds until valid calibration and geometry have been received. This prevents a new profile from accidentally recording weight with placeholder values.

### Calibration revisions

A new span creates a new calibration revision. Earlier points and loads remember the calibration they used, so a later ticket correction can be applied correctly.

---

## 8. Jobs and Rows Harvested

**Menu -> Jobs**

A job ties together a crop, field, harvester profile, rows harvested, area, weight, loads, and map points.

### Creating a job

1. Open **Jobs**.
2. Press **New**.
3. Select the crop.
4. Select the harvester profile.
5. Select a field if desired.
6. Check **Rows Harvested**.
7. Enter notes if desired.
8. Press **Save**. The job is created and becomes the active job.
9. Return to the main screen and press **Start** to open the first load.

### Rows Harvested

By default, a job uses the row count from the harvester profile.

The job can override Rows Harvested when the effective pickup width is intentionally different, such as picking up windrowed crop from more rows than the harvester's physical row count.

BeltFlo then calculates effective width as:

`rows harvested x profile row spacing`

For ordinary partial overlap on the last pass, usually leave the normal row count alone; overlap compensation already removes previously harvested area.

### Auto recording from AOG

A job is the active harvest record, while AOG sections determine when ground is actually being harvested. BeltFlo can auto-pause/resume around headland turns according to AOG section state.

### Manual pause

Manual **Pause** is different from normal section-off behavior. It means that material and ground during that period should not count at all.

### Finishing a job

Finish the job from the **Jobs** screen using **Finish Job**. Finishing a job also finishes any open load and writes job totals.

The main-screen **End/Stop** button finishes only the current load; it does not end the job.

---

## 9. Loads and Truck Tickets

**Menu -> Loads** or tap the **LOAD** title on the main screen.

BeltFlo keeps a load list across jobs. Loads are numbered within each job starting at Load 1.

### Load list columns

| Column | Description |
|---|---|
| **Load** | Load number within that job |
| **Job** | Job name |
| **Truck** | Optional truck/load name |
| **Monitor** | Weight measured by BeltFlo |
| **Ticket** | Certified scale ticket weight, when entered |
| **Diff** | Percent difference between certified and monitor weight |
| **Status** | Active, waiting for ticket, weighed, or corrected; optional flag is also shown |

For **Direct to Truck**, the open load's monitor weight updates live. For **Before Tank**, the Monitor column is intentionally blank because a pre-tank scale cannot know which stored crop later went into a particular truck.

### Starting and ending loads

On the main screen:

- Press **Start** with an active job and no open load to create the next load.
- Press **End/Stop** to finish the current load.
- Press **Start** again to start the next load.

Example:

**Load 1 -> End -> Start -> Load 2 -> End -> Start -> Load 3**

### Reopen a load

A finished load that is still waiting for a ticket can be reopened if:

- it belongs to the currently running job,
- no other load is open.

This is useful if a truck pulled away and then returned to be topped up. The load resumes from its existing monitor weight.

### Load flags

A load can be tagged as:

- None
- Wet
- Spoiled
- Trash
- Stones

These flags are notes for identifying unusual loads.

### Entering a certified ticket

1. Select the finished load.
2. Tap the **Ticket Weight** box.
3. Enter the certified weight.
4. BeltFlo shows the difference and correction factor.

For direct-to-truck profiles, choose one of the following:

| Action | Result |
|---|---|
| **Save Weight Only** | Stores the certified ticket but leaves the map and monitor weight as measured |
| **Correct This Load** | Rescales the selected load's map points and updates the job total to match the ticket factor |
| **Update Calibration** | Corrects the load and also changes the active profile's scale span based on the load's ticket factor |

A load's original monitor weight is retained; correction is applied through factors rather than overwriting the original measurement.

### Before-Tank ticket correction

When **Scale Position = Before Tank**, Start/End still creates numbered truck records so certified tickets stay organized, but those records do not collect scale pounds and yield measurement never depends on whether a truck record is open.

After harvest:

1. Finish the job only after crop belonging to that job has been emptied/cleaned out of the holding tank.
2. Enter and **Save Weight Only** for every certified truck ticket.
3. BeltFlo shows the ticket total against the job's measured scale total.
4. **Correct Job** rescales the entire job and map by `ticket total / measured job total`.
5. **Update Calibration** does the same whole-job correction and also multiplies the profile's scale span by that factor for future harvesting.

Because the crop was weighed before the holding tank, stopped unloading, unloading while harvesting, and temporary direct pass-through do not require different yield logic.

---

## 10. Crops and Fields

### Crops

**Menu -> Crops**

A crop is currently a name used by the job, report, and map record. Because the conveyor scale measures mass directly, BeltFlo does not need grain-style bushel-weight or moisture constants.

Create names such as:

- Potato
- Sugar Beet
- Carrot
- Onion

At least one crop must exist.

### Fields

**Menu -> Fields**

Fields are optional names used to organize jobs and reports.

Fields can be created manually. BeltFlo can also import field names from AgOpenGPS/TWOL field folders when that import option is used. The field name is imported; the job still uses live AOG GPS and section data while harvesting.

---

## 11. Yield Map

**Menu -> Yield Map**

The yield map displays recorded swaths colored by yield.

### Map behavior

- Map points are stored with GPS position, yield and calibration revision. Direct-to-truck points also carry their truck load ID; Before Tank points remain job-level because truck assignment happens after storage.
- Swaths are broken when crop flow stops, data gaps are too long, or GPS jumps are too large.
- Overlap compensation prevents already-harvested ground from being counted again.
- The mini map follows the vehicle and rotates heading-up while moving.
- Full-screen map mode is north-up and can be panned/zoomed for review.

### Ticket corrections and the map

When **Correct This Load** is used, only points tagged with that load are rescaled.

When **Correct Job** is used for a tank-mode job, the whole job is rescaled.

The map is therefore tied to the same corrected totals shown in reports.

---

## 12. Job Report and CSV Export

**Menu -> Reports**

The report screen shows saved jobs and summarizes:

- job name,
- field,
- crop,
- harvester description,
- rows and width,
- area,
- total mass,
- average yield,
- load count,
- data-point count,
- notes.

### CSV Export

Select a job and press **Export CSV** to save the job's data to a CSV file. The last export folder is remembered.

### FieldView ZIP / Shapefile Export

Select a job and press **FieldView ZIP** to create one `.zip` file containing:

- `.shp` — polygon swath geometry,
- `.shx` — shapefile index,
- `.dbf` — yield and BeltFlo attributes,
- `.prj` — WGS 84 / EPSG:4326 projection,
- `.cpg` — DBF text encoding,
- a short README describing the fields.

The polygons use the same pass-break rules and digging-width ribbon geometry as BeltFlo's yield map. Useful DBF fields include **YLD_LBAC**, **YLD_KGHA**, **WIDTH_M**, **POUNDS**, **LOAD_ID**, **CAL_REV**, **ROWS**, job, field, and crop.

For Climate FieldView, upload the ZIP at **FieldView.com -> Data -> Upload & Import** and use it as an **Imported Map**. It is a spatial yield-map layer; it is not a native combine harvest-data file and should not be expected to populate every native FieldView Harvest/Yield Analysis feature.

### Print

Select a job and press **Print** to print the report summary.

---

## 13. Settings

**Menu -> Settings**

### Units

Choose **Imperial** or **Metric**. When Imperial is selected, choose the yield display unit: **lb/ac**, **cwt/ac**, or **tons/ac**. Metric yield is displayed as **t/ha**.

Truck/load ticket entry is shown in:

- **lb** in Imperial,
- **kg** in Metric.

Changing display units does not alter stored measurements.

### Module communication

Choose:

- **WiFi/Ethernet** - UDP module communication,
- **CAN** - CAN adapter communication.

WiFi and wired Ethernet use the same BeltFlo UDP packet format from the PC application's point of view.

### CAN adapters

The current application supports SLCAN, InnoMaker, and PCAN interface choices. Select the appropriate driver and COM port where applicable, then save. Communication-setting changes may require restarting BeltFlo.

### Resume Job on Start

When enabled, BeltFlo reloads the active job after the application restarts.

### Auto Resume Pause

When enabled, a manually paused job can resume when AOG sections come back on. When disabled, BeltFlo warns if sections are on while the system remains manually paused.

### Truck Full Warning

Enter a target load weight for an audible/visual warning when the open load reaches that weight. Enter **0** to disable the warning.

The warning does not automatically end the load; the operator still presses **End/Stop** when the truck leaves.

---

## 14. Module Connection and Web Portal

### BeltFlo hotspot

With default settings, the module broadcasts an access point named similar to:

`BeltFlo_ESP32_XXXXXXXX`

The last eight characters are unique to the ESP32.

For module ID 0, the portal address is:

`http://192.168.200.1`

For other module IDs, the third octet is `200 + module ID`.

### Module portal diagnostics

The main portal page shows:

- Scale OK / No Scale,
- live section weight,
- raw counts,
- cumulative belt pulses,
- belt running state,
- whether PC settings are being received,
- calibration valid / ready to total,
- overload state,
- module total since boot,
- zero counts,
- span,
- weighed section length,
- belt travel per pulse,
- belt stop timeout,
- belt input GPIO.

### Communication mode

The module portal can select:

- WiFi,
- CAN,
- Ethernet.

In Ethernet mode, the portal also sets the Ethernet subnet and shows W5500/link status.

### WiFi Network page

The module keeps its own hotspot available and can also join an existing WiFi network. Use the **WiFi Network** page to scan, select the network, enter the password, and choose **Use this Network**.

### Firmware update

Use **Update Firmware** in the module portal for over-the-air firmware updates. Select the compiled ESP32 `.bin` file, upload it, and allow the module to restart.

### Two-way link check

A green Module indication in BeltFlo requires both:

- module packets reaching the PC,
- the module reporting that it has recently received the PC's settings.

If the module sends data but does not hear the PC, the Module indicator turns orange.

---

## 15. Testing with the Module Simulator

The repository includes **ModuleSimulator.exe** for testing without hardware.

### Basic simulator test

1. Start `BeltFlo.exe`.
2. Start `ModuleSimulator.exe`.
3. Start AgOpenGPS in simulator mode and open a field.
4. Create/select a BeltFlo harvester profile.
5. Open Conveyor Setup and Scale Calibration as needed.
6. Create and start a job.
7. Turn AOG sections on.
8. In Module Simulator, enable harvesting and set a section load / belt speed.
9. Press **Start** in BeltFlo to open Load 1.
10. Confirm load weight, job total, flow, belt speed, and yield increase.
11. Press **End/Stop** and confirm Load 1 freezes in the Loads list.
12. Press **Start** and confirm Load 2 starts at 0.
13. Enter a ticket for Load 1 and test Save Weight Only or Correct This Load.

### Simulator fault tests

The simulator can be used to check:

- module offline,
- scale fault,
- overload flag,
- belt stopped,
- dead belt sensor,
- calibration/tare not valid,
- ignored PC settings/two-way-link fault.

Use these to confirm the main-screen status indicators and alarms respond correctly.

---

## 16. Troubleshooting

### GPS stays red

- Confirm AgOpenGPS is running.
- Confirm AOG has a valid simulated or real position.
- Confirm both programs are using the expected local network interface.

### Module stays red

- Confirm the module or Module Simulator is running.
- For WiFi/Ethernet, check the PC firewall and UDP ports 30300/30400.
- Confirm the PC and module are on the same network/subnet.
- Power-cycle the module and watch the portal diagnostics.

### Module is orange

Packets are reaching BeltFlo, but the module is not confirming receipt of the PC settings.

- Check return-path networking to UDP port 30400.
- Check that the module communication mode matches the PC setting.
- In the module portal, look for **PC settings: Receiving**.

### Scale status is red

- Check NAU7802 power and I2C wiring.
- Check load-cell wiring and summing connection.
- Look for **Overload = Yes** in the module portal.
- Remove any mechanical bind or excessive load on the weighed section.

### ZERO or CAL warning

The profile is not ready to total weight.

1. Finish the active job.
2. Open Scale Calibration.
3. Run Zero Scale with an empty moving belt.
4. Run Known Weight with the belt stopped.
5. Save the calibration.

### Belt warning / pounds stay at zero

- Confirm belt pulses increase in Conveyor Setup or the module portal.
- Check the proximity sensor target and spacing.
- Confirm Belt per Pulse is not zero.
- Confirm the weighed-section length is correct.

### Load does not increase

Check all of the following:

- a job is active,
- a load is open,
- BeltFlo is not manually paused,
- AOG sections are on,
- the module link works both ways,
- scale is healthy and not overloaded,
- calibration is valid,
- flow is above the Empty Belt Below threshold.

### Crop is counted to the job but not a truck

A load was not open. Press **Start** before crop reaches the scale. In direct-to-truck mode BeltFlo warns when meaningful crop flow is detected with no load open.

### Ticket correction looks wrong

For a direct-to-truck profile, make sure the ticket belongs to the selected BeltFlo load.

For a tank profile, do not correct individual loads. Enter all truck tickets, finish the job, then use **Correct Job**.

### App crashes or data-write error appears

Check:

`Documents\BeltFlo\Logs\Errors.log`

Also review `Activity.log` for job/load/calibration events.

---

## 17. Files, Logs, and Data

BeltFlo creates its working folders under:

`Documents\BeltFlo`

Important locations include:

| Location | Contents |
|---|---|
| `Documents\BeltFlo\Logs` | Error, activity, and diagnostic logs |
| `Documents\BeltFlo\Exports` | Default location for exported CSV files |
| BeltFlo SQLite database | Jobs, loads, profiles, conveyor revisions, fields, crops, and yield points |

The exact application folder contains the program executable, required DLLs, language resources, and the `Help` folder.

Keep the application files together when moving BeltFlo to another PC.

---

## Safety and Record-Keeping Note

BeltFlo is a development yield-monitoring system and is **not a legal-for-trade certified scale**. Certified truck or platform scale tickets should remain the authoritative record for commercial settlement. Use BeltFlo for harvest monitoring, mapping, load tracking, and calibration support.