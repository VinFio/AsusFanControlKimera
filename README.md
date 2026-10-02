# AsusFanControlKimera

AsusFanControlKimera is a fan control application created by combining two existing open-source projects:

- **AsusFanControl**, used as the hardware communication and fan control engine;
- **AsusFanControlEnhanced**, used as the basis for the graphical interface, controls, profiles, and related features.

This project does not introduce a new hardware control engine. It combines the working hardware layer from AsusFanControl with the interface and profile-management functionality derived from AsusFanControlEnhanced.

## Features

- ASUS System, Manual, and Fan Curve modes;
- editable graphical fan curve;
- named fan-curve profiles with save, overwrite, rename, delete, and quick switching;
- textual fan curve format using `temperature,percentage`;
- configurable hysteresis and update interval;
- CPU temperature and RPM readings for all detected fans;
- optional safety limits from 40% to 99%;
- system tray support;
- minimized startup and startup with Windows;
- optional release of fan control when the application exits;
- optional debugging through a rotating diagnostic log;
- complete Italian, English, and Russian interface, switchable at runtime from
  **Options > Language** (`Opzioni > Lingua` in Italian).

## Fan Curve Profiles

The profile bar above the graph stores complete Fan Curve configurations. Each
profile contains the curve points, hysteresis, and update interval. Safety
limits and the currently selected control mode remain global settings and are
not changed when a profile is loaded.

Changes continue to affect the current working curve immediately. A named
profile is overwritten only with **Save**; **Save as** creates a separate copy.
An asterisk next to the active profile indicates changes that have not yet been
saved to that profile. Kimera asks whether to save, discard, or cancel before
switching away from such changes.

Existing installations keep their current curve after upgrading. It initially
appears as **Current curve** and can be stored under a name with **Save as**.

## System Requirements

### Operating system and runtime

- 64-bit Windows 10 or Windows 11;
- .NET Framework 4.7.2 or a later compatible .NET Framework 4.x release;

Kimera is compiled exclusively for `x64`. It is not compatible with 32-bit
Windows and does not use the modern .NET/.NET Core desktop runtime.

### ASUS software and hardware

- ASUS System Control Interface v3 installed;
- a supported ASUS laptop whose firmware exposes Fan Diagnosis through MyASUS;

Compatibility cannot be inferred from the ASUS brand alone. A model may satisfy
the software requirements while using a different embedded controller, sensor
mapping, or fan-control protocol.

### Privileges and PsExec

The current hardware library returns valid data only while Kimera runs under
the `SYSTEM` account. Kimera therefore requires:

- permission to display and approve a UAC elevation prompt;
- Microsoft Sysinternals `PsExec.exe`, preferably at
  `C:\Program Files (x86)\AsusFanControl\PsExec.exe` (searched first) or next
  to Kimera.

PsExec is not distributed with Kimera. The application first elevates as
administrator and then uses PsExec with `-i -s` to enter the interactive SYSTEM
session, reproducing the `run.bat` file of the working AsusFanControl
application.

Before relaunching as `SYSTEM`, Kimera preserves the SID of the interactive
user, so **Start with Windows** writes its configuration to the actual user's
profile. A UAC confirmation is still required at login because PsExec requires
elevation.

A global mutex prevents multiple Windows sessions from controlling the same
hardware at the same time.

Official PsExec download:
[Microsoft Sysinternals PsExec](https://learn.microsoft.com/sysinternals/downloads/psexec)

### Required release files

Keep these files together in the same directory:

- `AsusFanControlKimera.exe`
- `AsusFanControlKimera.exe.config`
- `AsusWinIO64.dll`

Release archives also include `README.md`, `LICENSE`, and
`THIRD_PARTY_NOTICES.md`; they are documentation files and are not required at
runtime.

The icon is embedded in the executable; `propeller.ico` is not required at
runtime. An Internet connection, NuGet packages, LibreHardwareMonitor, Sentry,
Fody, and Microsoft.Extensions.DependencyInjection are not required.

> **Third-party binary notice:** `AsusWinIO64.dll` is digitally signed and
> copyrighted by ASUSTeK COMPUTER INC. It is not covered by this repository's
> MIT license. Confirm that you have the right to redistribute the DLL before
> including it in a public release; users with the ASUS System Control Interface
> installed can obtain it from their local ASUS installation.

### Recommended environment

- close AsusFanControl, other Kimera versions, FanControl, G-Helper, and any
  other utility that directly controls the same fans;
- keep **Safe Limits** and **Release fan control on exit** enabled;
- verify RPM and temperature readings before relying on Manual or Fan Curve
  mode;
- enable Debug when diagnosing intermittent hardware or firmware behavior.

When Debug is enabled, Kimera writes to
`C:\ProgramData\AsusFanControlKimera\kimera-debug.log`. The SYSTEM account used
by Kimera normally has permission to create and update this location.

## Download

A precompiled version is available from the repository's
[Releases](https://github.com/VinFio/AsusFanControlKimera/releases) section.

Download the latest `AsusFanControlKimera-vX.X.X.zip` archive, extract it to a
protected folder such as `C:\Program Files\AsusFanControlKimera` (see
[Security Notes](#security-notes)), and keep the following files in the same
folder:

- `AsusFanControlKimera.exe`
- `AsusFanControlKimera.exe.config`
- `AsusWinIO64.dll`

Keep the included `README.md`, `LICENSE`, and `THIRD_PARTY_NOTICES.md` files
with redistributed copies of the archive.

Run `AsusFanControlKimera.exe`. The runtime, PsExec, and privilege requirements
are described in [System Requirements](#system-requirements).

## Building from Source

Building is only required if you want to modify the code or create your own executable.

Open `AsusFanControlKimera.sln` in Visual Studio and build the `Release|x64` configuration.

The .NET Framework 4.7.2 targeting pack is required.

After compilation, the output files are located in:

`bin\x64\Release`

`AsusWinIO64.dll` must be placed next to the executable.

`bin\x64\Release` is inside your user profile and can be modified without
administrator rights, so Kimera shows the unprotected-folder warning when it is
started from there. For daily use, copy the output files to a protected folder
(see [Security Notes](#security-notes)).

## Safety

When **Safe Limits** is enabled, Manual and Fan Curve modes restrict fan values to the range between 40% and 99%.

The **ASUS System** mode uses the special value `0` to disable test mode and return fan control to the firmware.

While Manual or Fan Curve mode is active, Kimera automatically switches back to ASUS System mode when it detects any of the following conditions:

- two consecutive temperature readings outside the plausible range of 10–115 °C;
- three consecutive hardware reading errors;
- a fan reporting 0 RPM for three consecutive readings while the requested PWM value is at least 40%. Readings taken during the first 5 seconds after a speed change are ignored, so stopped fans have time to spin up.

In ASUS System mode the firmware is already in control, so these conditions are reported in the status bar without opening the fail-safe window.

If an error occurs while sending a command to multiple fans, the controller also attempts to disable test mode on every previously detected fan.

Disabling Safe Limits requires explicit confirmation.

Disabling fan-control release on exit also requires confirmation because the last applied PWM value may remain active after the application is closed.

These protections cover errors that can be handled by the application. A forced process termination, a lock-up inside the native DLL, or a sudden loss of power cannot be handled reliably by an application running in the same process.

## Security Notes

Kimera runs under the `SYSTEM` account, the most privileged account in Windows.
Every file it executes or loads therefore runs with the same privileges.

- **Install Kimera in a protected folder**, such as
  `C:\Program Files\AsusFanControlKimera`, where only administrators can write.
  If `AsusFanControlKimera.exe`, `AsusFanControlKimera.exe.config`,
  `AsusWinIO64.dll`, or PsExec are in a folder that standard accounts can
  modify (for example Downloads, Desktop, or another folder inside the user
  profile), any program running without privileges could replace them and
  obtain `SYSTEM` access.
- At startup Kimera checks these paths. If one of them can be modified by a
  non-administrator account, a warning is displayed. Kimera starts only after
  confirmation, and the choice is remembered for that folder, so **Start with
  Windows** keeps working without further prompts.
- PsExec is searched first in `C:\Program Files (x86)\AsusFanControl` and only
  then next to the executable.
- The diagnostic log folder is created, or corrected if it already exists, so
  that only `SYSTEM` and administrators can write to it, while standard users
  can still read the log. Symbolic links, junctions, and hard links found in
  place of the folder or log file are removed before writing.
- The **Open log** button in the fail-safe window shows the log inside Kimera
  instead of launching Notepad, because an external editor would also run as
  `SYSTEM` and its Open/Save dialogs could be used to start other programs with
  the same privileges.

## Diagnostic Log

The diagnostic log can be enabled from:

`Options > Debug`

The log file is stored at:

`C:\ProgramData\AsusFanControlKimera\kimera-debug.log`

It contains:

- temperature, RPM, and PWM snapshots;
- mode changes;
- hardware and control errors;
- fail-safe activations.

The maximum file size is 1 MB. When this limit is reached, Kimera automatically retains approximately 768 KB of the most recent log entries.

Any log-writing error is ignored to prevent diagnostic logging from interfering with fan control.

When a fail-safe condition is triggered, Kimera displays a persistent warning window containing:

- the activation time;
- the detected cause;
- the affected fans;
- the latest observed values;
- the result of the attempt to return control to the firmware.

## Credits and Attribution

AsusFanControlKimera combines code and functionality from two existing projects:

- [AsusFanControl](https://github.com/Karmel0x/AsusFanControl), which provides the hardware communication and fan control engine;
- [AsusFanControlEnhanced](https://github.com/Darren80/AsusFanControlEnhanced), which provides the basis for the graphical interface, controls, profiles, and related functionality.

The original projects remain the work of their respective authors.

Users and contributors should refer to the original repositories and their license files for the applicable attribution, redistribution, and licensing requirements.
The packaged notices are collected in `THIRD_PARTY_NOTICES.md`.

## Compatibility and Disclaimer

AsusFanControlKimera has been developed and tested on a limited selection of
**ASUS Vivobook V16-series** hardware.

Compatibility with other ASUS laptop models is not guaranteed. Hardware interfaces, fan controllers, firmware behavior, sensor mappings, and supported commands may differ between models.

This project is still under development. Bugs, incomplete features, incorrect readings, unexpected behavior, and other issues may be present and may require further investigation and fixes.

The software is provided **as is**, without warranties or guarantees of any kind. Use it at your own risk. The authors and contributors are not responsible for overheating, instability, data loss, hardware damage, or any other direct or indirect consequences resulting from its use.

Users should monitor system temperatures carefully and verify that fan control behaves correctly on their specific hardware before relying on the application.
