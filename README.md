# AsusFanControlKimera

AsusFanControlKimera is a fan control application created by combining two existing open-source projects:

- **AsusFanControl**, used as the hardware communication and fan control engine;
- **AsusFanControlEnhanced**, used as the basis for the graphical interface, controls, profiles, and related features.

This project does not introduce a new hardware control engine. It combines the working hardware layer from AsusFanControl with the interface and profile-management functionality derived from AsusFanControlEnhanced.

## Features

- ASUS System, Manual, and Fan Curve modes;
- editable graphical fan curve;
- textual fan curve format using `temperature,percentage`;
- configurable hysteresis and update interval;
- CPU temperature and RPM readings for all detected fans;
- optional safety limits from 40% to 99%;
- system tray support;
- minimized startup and startup with Windows;
- optional release of fan control when the application exits;
- optional debugging through a rotating diagnostic log.

## Building

Open `AsusFanControlKimera.sln` in Visual Studio and build the `Release|x64` configuration.

The .NET Framework 4.7.2 targeting pack is required.

`AsusWinIO64.dll` must be placed next to the executable.

On the system used for development and testing, the ASUS DLL returns valid hardware data only when the application runs under the `SYSTEM` account.

Kimera first requests administrator privileges and then relaunches itself through the existing `PsExec.exe` installation located at:

`C:\Program Files (x86)\AsusFanControl`

This reproduces the behavior of the `run.bat` file used by the working AsusFanControl application.

PsExec is not included in this project.

Before relaunching as `SYSTEM`, Kimera preserves the SID of the interactive user. The **Start with Windows** option therefore writes its configuration to the actual user's profile.

A UAC confirmation is still required at login because PsExec requires elevation.

A global mutex prevents multiple Windows sessions from controlling the same hardware at the same time.

## Safety

When **Safe Limits** is enabled, Manual and Fan Curve modes restrict fan values to the range between 40% and 99%.

The **ASUS System** mode uses the special value `0` to disable test mode and return fan control to the firmware.

Kimera automatically switches back to ASUS System mode when it detects any of the following conditions:

- two consecutive temperature readings outside the plausible range of 10–115 °C;
- three consecutive hardware reading errors;
- a fan reporting 0 RPM for three consecutive readings while the requested PWM value is at least 40%.

If an error occurs while sending a command to multiple fans, the controller also attempts to disable test mode on every previously detected fan.

Disabling Safe Limits requires explicit confirmation.

Disabling fan-control release on exit also requires confirmation because the last applied PWM value may remain active after the application is closed.

These protections cover errors that can be handled by the application. A forced process termination, a lock-up inside the native DLL, or a sudden loss of power cannot be handled reliably by an application running in the same process.

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

## Compatibility and Disclaimer

AsusFanControlKimera has been developed and tested primarily on an **ASUS Vivobook V16 V3607VU**.

Compatibility with other ASUS laptop models is not guaranteed. Hardware interfaces, fan controllers, firmware behavior, sensor mappings, and supported commands may differ between models.

This project is still under development. Bugs, incomplete features, incorrect readings, unexpected behavior, and other issues may be present and may require further investigation and fixes.

The software is provided **as is**, without warranties or guarantees of any kind. Use it at your own risk. The authors and contributors are not responsible for overheating, instability, data loss, hardware damage, or any other direct or indirect consequences resulting from its use.

Users should monitor system temperatures carefully and verify that fan control behaves correctly on their specific hardware before relying on the application.