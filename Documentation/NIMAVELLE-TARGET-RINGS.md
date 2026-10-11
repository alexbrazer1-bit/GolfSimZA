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
2. To test the scene directly in the Editor, choose **Nimavelle Golf → Build Target Rings Demo Scene** and press Play.
3. In a Windows player build, open **Mini Games → Play Nimavelle Target Rings**. The selected players and their names carry into the game. Use **BACK TO GOLF SIM MENU** to return.
4. Choose a target and difficulty in the left HUD.
5. With a paired Garmin Approach R10, wait for **GARMIN R10 CONNECTED — LIVE SHOTS**, then hit a real ball; the game uses measured carry and horizontal launch direction to score the landing against the selected island. Align the R10 with the course's forward axis. Press Space or **HIT SHOT** for a demo shot when no live R10 is connected.
6. Press **TAB** or the camera button to switch between aerial and playable views.
7. Finish 10 shots and choose **NEW ROUND** to restart.

## Implementation status and next integration steps

- The course geometry, target islands, ring meshes, environment dressing, lighting, camera views and HUD are generated procedurally.
- Live Garmin Approach R10 shots use the project's direct-Bluetooth bridge and receiver. Target Rings scores the measured carry and horizontal launch direction against the selected island; the practice shot still uses the clearly labelled demo-shot model.
- Multi-player turn order, editable player names and per-player best scores are supported.
- The mini-game uses measured landing data to score targets; it does not simulate a full physical ball flight inside the mini-game. Procedural placeholder mountains, trees and islands make the scene self-contained; replace these with original, optimized art assets before calling the visuals production-ready.
- Procedural placeholder mountains, trees and islands make the first scene self-contained. Replace these with original, optimized art assets before calling the visuals production-ready.

No TrackMan assets, course data or proprietary code are used.
