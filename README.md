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

Garmin currently documents third-party R10 integrations, including direct Bluetooth PC connections for GSPro and direct connections for Awesome Golf. This project therefore isolates launch-monitor communication behind `ILaunchMonitorAdapter` so the R10 transport can be implemented/tested independently from the simulator physics.

We will not bundle Garmin proprietary SDKs or reverse-engineered proprietary material in this repository. Any commercial Garmin compatibility will follow Garmin's applicable developer/brand requirements.

## Unity target

Unity 6 / 6000.x. The initial target is Windows Standalone.

## Current milestone: 0.1

1. Unity project foundation
2. Golf shot data model
3. Launch-monitor adapter abstraction
4. Garmin R10 adapter boundary
5. Test-shot provider for development
6. Ball-flight physics foundation
7. Basic simulator HUD
8. Firebase service boundary

## Course licensing

Real-world course representations will only be distributed when the project has the appropriate rights, licenses, or permission. The course system is designed to support licensed, original, and permitted community-created content.

## Repository structure

```text
Unity/
  Assets/GolfSim/Core/
  Assets/GolfSim/Physics/
  Assets/GolfSim/LaunchMonitors/
  Assets/GolfSim/UI/
  Assets/GolfSim/Courses/
  Assets/GolfSim/Firebase/
  ProjectSettings/
Documentation/
CoursePackages/
```
