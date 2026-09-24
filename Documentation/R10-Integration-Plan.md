# Garmin Approach R10 integration plan

## Target workflow

Windows PC + Bluetooth + Garmin Approach R10 + GolfSimZA.

## Milestones

### A. Device discovery
- Detect the R10 as a nearby Bluetooth device.
- Display connection state in the simulator.
- Do not require the Garmin Golf mobile app for the target PC workflow unless Garmin's supported integration path requires it.

### B. Session
- Start/stop a launch-monitor session.
- Detect shot events.
- Record timestamps and raw fields separately from derived fields.

### C. Shot mapping

Map available R10 measurements into `ShotData` without silently inventing unavailable measurements. Any derived value must be explicitly marked as derived.

### D. Validation

For a controlled set of shots, compare:
- Ball speed
- Club speed
- Launch angle
- Launch direction
- Spin values
- Carry distance
- Total distance

against the R10's reference output.

### E. Simulator

Pass validated `ShotData` into the common physics pipeline. The simulator must not contain R10-specific physics.

## Garmin compliance

Use Garmin's current developer/brand guidance for any commercial presentation of Garmin-sourced data. The repository must not contain Garmin proprietary SDK binaries or copied proprietary materials.
