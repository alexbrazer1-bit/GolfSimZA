# Nimavelle Golf — Target Rings (first playable)

This is an original mini-game prototype for the existing GolfSimZA Unity 6 project. It uses a tropical mountain lake, six floating target islands, concentric scoring zones, an aerial overview and a behind-the-tee camera.

## Course layout

The playable target centres are laid out from the tee as follows:

| Target | Nominal distance | Position (X, Z) | Role |
|---|---:|---:|---|
| 1 | 80 m | (-28, 75) | Short, left-side target |
| 2 | 100 m | (31, 95) | Short, right-side target |
| 3 | 120 m | (-52, 108) | Offset target |
| 4 | 150 m | (68, 134) | Mid-range island |
| 5 | 200 m | (-52, 193) | Long island |
| 6 | 250 m | (74, 239) | Pro-distance cliff-side target |

Distances are nominal target labels. The layout is intentionally offset so players must select and aim at a target rather than simply hit straight down a range.

## Scoring zones

Score is based on how close the landing point is to the selected target centre:

- Outer zone: 5 points
- Middle zone: 10 points
- Inner zone: 20 points
- Bullseye: 50 points
- Outside the scoring zones: 0 points

A round contains 10 shots. Difficulty changes the scoring radii:

- Beginner: 1.25x target radius
- Intermediate: 1.0x
- Advanced: 0.78x
- Pro: 0.60x

## Create and play the scene

1. Open the GolfSimZA project in Unity 6.
2. Let Unity import `Assets/GolfSim/Minigames/TargetRingsCourse.cs`.
3. Choose **Nimavelle Golf → Build Target Rings Demo Scene** from the Unity menu.
4. Open `Assets/Scenes/Nimavelle_TargetRings.unity` if it did not open automatically.
5. Press Play.
6. Choose a target, difficulty, shot power and aim setting in the left HUD.
7. Press **HIT SHOT** or the Space key. Press **TAB** or the camera button to switch between aerial and playable views.
8. Finish 10 shots and choose **NEW ROUND** to restart.

## Implementation status and next integration steps

- The course geometry, target islands, ring meshes, environment dressing, lighting, camera views and HUD are generated procedurally.
- Shot input currently uses a clearly labelled simplified demo-shot model. It is not a replacement for the project's real ball-flight physics.
- The player-count control is currently a UI setting; per-player turn order and scoreboards are a next step.
- The next integration task is to route launch-monitor `ShotData` through the existing GolfSimZA shot/physics pipeline, use actual carry and lateral dispersion to resolve the landing point, and then add turn order, per-player totals, wind, target movement and polished environment assets.
- Procedural placeholder mountains, trees and islands make the first scene self-contained. Replace these with original, optimized art assets before calling the visuals production-ready.

No TrackMan assets, course data or proprietary code are used.
