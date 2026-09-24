# GolfSimZA 1.0 — Garmin Approach R10 setup

GolfSimZA now has a native **OpenConnect receiver** for R10 shot data. The R10 Bluetooth transport is intentionally kept in a separate Windows bridge so the Unity simulator remains focused on gameplay, graphics, physics and scoring.

## What the final test flow is

1. Garmin Approach R10 pairs to the Windows PC over Bluetooth.
2. The R10 bridge connects to the R10 and forwards each shot to GolfSimZA.
3. GolfSimZA listens on `127.0.0.1:921`.
4. A received shot enters the normal GolfSimZA physics engine.
5. The Play Round HUD updates the club, ball speed, launch, spin, carry and remaining distance.
6. The player can continue through the hole and score the round.

## Recommended R10 bridge

The open-source `mholow/gsp-r10-adapter` project supports a direct Windows Bluetooth connection to the R10 and an OpenConnect output. It is MIT licensed.

Repository: https://github.com/mholow/gsp-r10-adapter

Use its current release rather than copying its Bluetooth implementation into GolfSimZA. This keeps the R10 transport isolated and makes future Garmin firmware changes easier to handle.

## Bridge configuration

Set the bridge `settings.json` OpenConnect section to:

```json
"openConnect": {
  "ip": "127.0.0.1",
  "port": 921
}
```

Enable its Bluetooth section and pair the R10 with Windows. The bridge documentation recommends Windows Bluetooth discovery set to Advanced when the R10 is not visible.

## GolfSimZA setup

After pulling the repository:

1. Open the project in Unity 6.
2. Wait for compilation to finish.
3. Select **GolfSimZA → Create 1.0 Playable Garmin R10 Simulator**.
4. Open the generated `GolfSimZA_0_6_PlayRound` scene.
5. Press Play.
6. Start the R10 bridge.
7. Hit a real shot.

Keyboard test mode remains available with `1-8` to choose a club and `SPACE` to simulate a shot.

## R10 pairing notes

Garmin's current documentation confirms that the R10 can connect directly to a compatible PC simulator over Bluetooth. Garmin also lists GSPro, E6 Connect, Awesome Golf and Creative Golf among compatible third-party simulator options. GolfSimZA uses the same direct-PC concept but receives the shot through its OpenConnect-compatible local receiver.

If the R10 is already connected to another device, disconnect that device first so it does not steal the Bluetooth connection.

## Important limitation

The Garmin R10's low-level Bluetooth transport is not implemented in GolfSimZA itself. The bridge is the transport layer. This is deliberate: Garmin's public documentation describes the supported simulator connection path, while the low-level R10 packet protocol is not documented as a public Unity API.

Once a shot reaches GolfSimZA, the simulator does not depend on the bridge for physics, scoring, player profiles, club mapping or course gameplay.
