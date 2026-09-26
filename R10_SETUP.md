# GolfSimZA 1.0 — Garmin Approach R10 setup

GolfSimZA has a native **OpenConnect receiver** for R10 shot data. The Windows R10 transport is now packaged as the **GolfSimZA R10 Bridge**, so the simulator no longer depends on a manually configured GSPro connection or a separate GSPro installation.

## Final test flow

1. Pair the Garmin Approach R10 to the Windows PC over Bluetooth.
2. Install the GolfSimZA R10 Bridge package produced by the repository's `Build GolfSimZA R10 Bridge` workflow.
3. GolfSimZA listens on `127.0.0.1:921`.
4. The packaged bridge connects directly to the R10 and forwards OpenConnect shot data to GolfSimZA.
5. A real shot enters the normal GolfSimZA physics engine.
6. The Play Round HUD updates club, ball speed, launch, spin, carry and remaining distance.
7. The player can continue through the hole and score the round.

## Packaged bridge

The bridge is built from the MIT-licensed direct-Bluetooth R10 implementation in `mholow/gsp-r10-adapter`. The build is self-contained for Windows x64 and uses GolfSimZA's `settings.json` so its OpenConnect target is always:

```json
"openConnect": {
  "ip": "127.0.0.1",
  "port": 921
}
```

The legacy R10/E6 TCP server is disabled because GolfSimZA does not need it.

## Unity setup

1. Pull the latest GolfSimZA repository.
2. Open the project in Unity 6.
3. Wait for compilation to finish.
4. Select **GolfSimZA → Create 1.0 Playable Garmin R10 Simulator**.
5. The generated Play Round scene contains the R10 receiver and packaged bridge launcher.
6. Install the bridge under `StreamingAssets/GolfSimZA-R10-Bridge/`.
7. Press Play. GolfSimZA will start the packaged bridge automatically when the executable is present.
8. Hit a real ball.

Keyboard test mode remains available with `1-8` to choose a club and `SPACE` to simulate a shot.

## Practice swings vs real shots

The R10 legitimately reports practice swings without `BallData`. A practice swing can contain club speed, path, attack angle and swing timing, but ball speed, launch and spin require the radar to detect a ball departure. GolfSimZA must therefore **not invent ball-flight values** for a practice swing.

For a real ball strike, the OpenConnect packet should contain `BallData` and `ClubData`, with:

```text
ContainsBallData = true
ContainsClubData = true
```

If the bridge log says `shotType: PRACTICE` and `ContainsBallData: false`, the R10 is telling us that it did not detect a ball flight. That is not a TCP/Unity connection failure.

## R10 pairing notes

The R10 must be paired with Windows before the bridge starts. If it is already connected to another device, disconnect that device first so it does not steal the Bluetooth connection.

On Windows 11, the direct-Bluetooth bridge documentation recommends enabling Advanced Bluetooth Device Discovery if the R10 is not visible during pairing.

## Architecture

```text
Garmin Approach R10
        │
        │ Bluetooth LE
        ▼
GolfSimZA R10 Bridge
        │
        │ OpenConnect v1 / TCP 921
        ▼
GolfSimZA GarminR10Adapter
        │
        ▼
GolfSimZA SimulatorController
        │
        ├── ball flight
        ├── HUD
        ├── scoring
        └── shot history
```

The R10 transport is isolated from GolfSimZA's gameplay and physics code, while the bridge itself is now part of the GolfSimZA build workflow rather than an unrelated executable you have to find manually.
