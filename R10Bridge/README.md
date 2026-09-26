# GolfSimZA R10 Bridge

GolfSimZA's packaged Windows R10 transport uses the direct-Bluetooth R10 implementation from `mholow/gsp-r10-adapter` and sends OpenConnect v1 shot data directly to the GolfSimZA receiver on `127.0.0.1:921`.

The bridge is built as a self-contained Windows x64 executable by the GolfSimZA GitHub Actions workflow. GolfSimZA does not use GSPro and does not require GSPro to be installed or running.

## Windows setup

1. Pair the Garmin Approach R10 with Windows Bluetooth.
2. The Windows Bluetooth device name should be `Approach R10`.
3. Place the built bridge folder beside the GolfSimZA simulator or in its configured bridge directory.
4. Start GolfSimZA first so its OpenConnect receiver owns TCP port 921.
5. Start the packaged bridge.
6. The bridge connects directly to the R10 and forwards shot data to GolfSimZA.

## Important R10 behaviour

A practice swing is intentionally reported by the R10 without BallData. The R10 supplies club/swing metrics for a practice swing, but ball speed, launch and spin require the radar to detect a ball departure. GolfSimZA therefore must not manufacture ball data for a practice swing.

For a real shot, the OpenConnect payload should contain both `BallData` and `ClubData` with `ContainsBallData=true` and `ContainsClubData=true`.

## Third-party source

The direct Bluetooth transport is based on the MIT-licensed `mholow/gsp-r10-adapter` project. The packaged artifact includes the required third-party license notice.
