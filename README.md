# GolfSimZA

A Windows-first Unity golf simulator focused on realistic golf physics, Garmin Approach R10 integration, and a scalable course library for South African and international courses.

## Project goals

- Garmin Approach R10 as a first-class launch-monitor target
- Realistic ball-flight, bounce and roll simulation
- South African and international course support
- Downloadable course packages
- Firebase-backed profiles, statistics, course metadata and online services
- Future multiplayer and competitions
- PC/Windows-first architecture

## Important R10 integration note

Garmin currently documents third-party R10 integrations, including direct Bluetooth PC connections for GSPro and direct connections for other simulator products. This project therefore isolates launch-monitor communication behind `ILaunchMonitorAdapter` so the R10 transport can be implemented/tested independently from the simulator physics.

We will not bundle Garmin proprietary SDKs or reverse-engineered proprietary material in this repository. Any commercial Garmin compatibility will follow Garmin's applicable developer/brand requirements.

## Unity target

Unity 6 / 6000.x. The initial target is Windows Standalone 64-bit.

## Current milestone: 0.1.1

- Unity package/project foundation
- Golf shot data model
- Launch-monitor adapter abstraction
- Garmin R10 adapter boundary
- Development test-shot provider
- Prototype ball-flight, bounce and roll simulation
- One-click driving-range scene creator
- Windows build target foundation

### Prototype controls

1. Open the project in Unity 6.
2. Run `GolfSimZA > Create 0.1.1 Driving Range`.
3. Open the generated driving-range scene.
4. Press Play.
5. Press **Space** to fire a development driver shot.

The development shot provider is intentionally separate from the Garmin R10 adapter. This lets us validate the simulator physics and UI before enabling an approved R10 transport.

## R10 development target

The intended production path is:

`Garmin Approach R10 -> Windows Bluetooth -> GolfSimZA R10 adapter -> ShotData -> Golf physics -> Unity course`

Garmin currently documents direct Bluetooth PC connectivity for GSPro, so this architecture keeps direct Windows connectivity as the target while avoiding proprietary protocol reproduction in the repository.

## Course licensing

Real-world course representations will only be distributed when the project has the appropriate rights, licenses, or permission. The course system is designed to support licensed, original, and permitted community-created content.

## Repository structure

```text
Packages/
ProjectSettings/
Assets/
  GolfSim/Core/
  GolfSim/Physics/
  GolfSim/LaunchMonitors/
    GarminR10/
  GolfSim/Editor/
  GolfSim/UI/
  GolfSim/Courses/
  GolfSim/Firebase/
Documentation/
CoursePackages/
```
