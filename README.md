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

## Current milestone: 0.9.5

### 0.5 course and round foundation

- Course/tee/round selection scene
- Session state persists the selected course, tee and 9/18-hole round
- Three placeholder demo courses for UI testing
- Course selection launches the validated presentation simulator
- Real course content remains gated behind appropriate licensing/original-content requirements
- Build settings include the course-selection and simulator scenes

### 0.6 play round

- Dedicated round gameplay scene
- Hole number, par and distance HUD
- Shot data and score presentation
- Player score/distance-to-pin side panel
- Realistic practice-first driving-range presentation retained

### 0.9 player golf bags and club mapping

- Player must be selected before Map My Bag is available
- Expanded golf-club library covering woods, hybrids, irons, wedges, putter, utility/driving irons and short-game clubs
- Individual player golf bags with a 14-club limit
- TrackMan-inspired Map My Bag workflow
- Six required shots for every club mapping session
- Carry and total distance are calculated from the average of all six shots
- Mapped distances are stored per player and per club
- Round club selection can use the player's mapped distances

### 0.9.5 mapping analytics

- Six-shot mapping completion summary
- Individual carry and total results for all six shots
- Carry average and total average
- Best carry and carry spread/standard deviation
- Mapping result is associated with the player and club
- Repeat mappings can produce a new result summary without permanently blocking future club mappings
- Existing mapping and round-play logic remains unchanged

## Prototype controls

1. Open the project in Unity 6.
2. Open the GolfSimZA course-selection scene.
3. Choose a demo course, tee and 9/18 holes.
4. Configure the players.
5. Select a player before opening **MAP MY BAG**.
6. Add the clubs that player actually carries.
7. Map a selected club by hitting six shots.
8. Review the six-shot result summary.
9. Return to the player's bag and continue mapping additional clubs.
10. Start a round using the mapped club distances.

The development shot provider is intentionally separate from the Garmin R10 adapter. This lets us validate the simulator physics, course/session flow, UI and personalized club mapping before enabling an approved R10 transport.

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
  GolfSim/Players/
  GolfSim/Courses/
  GolfSim/Firebase/
Documentation/
CoursePackages/
```
