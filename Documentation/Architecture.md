# GolfSimZA architecture

## Runtime flow

```text
Launch Monitor
    -> ILaunchMonitorAdapter
    -> ShotData
    -> BallFlightModel
    -> Ball / Course physics
    -> Shot result / statistics
    -> Firebase services
```

## Garmin R10

`GarminR10Adapter` is the integration boundary. The R10 transport must remain isolated from the simulator core. Garmin currently documents direct PC Bluetooth connectivity for GSPro and third-party R10 integrations, so direct PC connectivity is the target workflow for GolfSimZA. Exact implementation will be validated against supported interfaces and licensing requirements before shipping.

## Course packages

Courses should be content-driven rather than hard-coded. A course package will eventually contain metadata, terrain, hole definitions, playable surfaces, collision data, vegetation/objects, lighting data and thumbnails.

Example metadata:

```json
{
  "courseId": "example-course",
  "name": "Example Golf Club",
  "country": "South Africa",
  "region": "Free State",
  "holes": 18,
  "par": 72,
  "version": "1.0.0"
}
```

## Firebase responsibilities

- Authentication
- Player profiles
- Statistics
- Course catalog metadata
- Course entitlements/download metadata
- Cloud saves
- Leaderboards
- Remote configuration

Large binary course assets should use object storage/CDN delivery rather than Firestore documents.

## Security

Never commit Firebase credentials, private keys, Garmin SDK binaries, proprietary protocols, or licensed course assets.

## Courses (1.1)

```text
Course folder (.unity3d + .GKD)
    -> CourseImporter / GkdCourseReader   (import once, hole data -> course.json)
    -> CourseLibrary                      (persistentDataPath/Courses)
    -> CourseLoader                       (loads the course scene additively, ShaderRepair)
    -> RoundGameplayUI                    (tees, pins, aim, penalties, scoring)
    -> BallFlightSimulator + GroundProbe  (flight, bounce and roll on the terrain)
```

## Updates (1.1)

```text
Release Manager (Unity) -> Builds/GolfSimZA -> Installer/Publish-Update.ps1 (Inno Setup)
    -> <update folder>/installers/GolfSimZA-Setup-x.y.z.exe + latest.json
    -> UpdateService (in game): check, verify SHA-256, run installer, restart
```
