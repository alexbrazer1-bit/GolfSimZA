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

## Current milestone: 0.4

### 0.1.1 foundation

- Unity package/project foundation
- Golf shot data model
- Launch-monitor adapter abstraction
- Garmin R10 adapter boundary
- Development test-shot provider
- Prototype ball-flight, bounce and roll simulation
- One-click driving-range scene creator
- Windows build target foundation

### 0.1.2 simulator workflow

- Expanded shot data with club metadata
- Eight development club presets: Driver, 3 Wood, 5 Iron, 7 Iron, 9 Iron, Pitching Wedge, Sand Wedge and Putter
- New Input System controls for development shots
- Club selection using keys 1-8
- Spacebar shot launch
- Bounded shot history for the current simulator session
- HUD showing club, ball speed, club speed, launch, direction, spin and recent shots
- Garmin R10 adapter boundary remains isolated from the development provider

### 0.2 visual driving range

- Visual fairway and target greens at 50m, 100m, 150m and 200m
- Distance markers and target flags
- Camera and lighting setup for the visual range
- Shot tracer for the development ball flight
- Refined simulator HUD layout

### 0.3 golf ball and shot physics

- Continuous 3D ball-flight simulation from launch data
- Aerodynamic drag and spin-based lift approximation
- Spin decay during flight
- Ground impact and bounce response
- Post-landing roll and deceleration
- Calculated carry and total distance
- Calculated apex height and flight time
- Completed shot results written back into shot history
- HUD displays calculated carry and total distance

### 0.4 simulator presentation

- Dedicated presentation-range scene generator
- Camera follows the ball during flight and returns to the address view after the shot
- Persistent landing marker for the latest completed shot
- Existing tracer retained for the full ball-flight path
- Presentation layer kept separate from launch-monitor transport and physics
- Foundation ready for course selection and R10 integration work

## Prototype controls

1. Open the project in Unity 6.
2. Run `GolfSimZA > Create 0.4 Simulator Presentation Range` for the current presentation test scene.
3. Open the generated `GolfSimZA_0_4_SimulatorPresentationRange` scene.
4. Press Play.
5. Press **1-8** to select a development club.
6. Press **Space** to fire a test shot.
7. Watch the ball flight, tracer, camera follow, landing marker and calculated carry/total distance.

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
