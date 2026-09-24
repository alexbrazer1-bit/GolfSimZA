# GolfSimZA Phase 0.1.1

## Goal
Create the first executable simulator foundation without depending on a live Garmin R10 connection.

## What is implemented

- `ShotData` — common shot representation used by all launch monitors.
- `ILaunchMonitorAdapter` — transport-independent launch-monitor contract.
- `GarminR10Adapter` — Garmin-specific integration boundary. It deliberately does not reproduce proprietary Garmin protocols.
- `TestShotProvider` — development shot source so physics can be tested immediately.
- `BallFlightSimulator` — first-pass trajectory, gravity, drag, bounce and roll model.
- `SimulatorController` — connects a shot provider to the physics engine.
- `CreatePrototypeScene` — Unity Editor command that generates a simple driving range scene.

## Current test flow

`Space key -> TestShotProvider -> ShotData -> SimulatorController -> BallFlightSimulator -> GolfBall`

## Production R10 flow

`Garmin Approach R10 -> approved Windows transport -> GarminR10Adapter -> ShotData -> SimulatorController -> BallFlightSimulator`

Garmin currently documents direct Bluetooth PC connectivity for GSPro. That confirms the desired class of connection is technically used by the R10 ecosystem, but it does not grant this project access to Garmin's proprietary protocol. The production adapter must therefore be implemented only through an approved/documented integration route.

## Physics note

The current flight model is intentionally a prototype. It is not yet calibrated against measured R10 shots and does not yet model full aerodynamic lift, drag coefficients, spin decay, Magnus force, temperature, altitude, humidity, wind layers, lie conditions, club/face/path relationships, or green/fairway material response.

Those will be added after the basic end-to-end pipeline is verified.

## Next milestone: 0.1.2

- Add a clean simulator HUD.
- Display live connection state.
- Display last-shot metrics.
- Add shot history.
- Add configurable test-shot presets for driver, iron, wedge and putter.
- Add initial environmental settings: wind speed/direction and altitude.
- Prepare the R10 transport test harness.
- Add Windows x64 build automation once the local Unity project imports successfully.
