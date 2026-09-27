# Importing courses

GolfSimZA can play courses that were built as standard Unity course files. This is for use on your own home simulator.

## What can be imported

A course folder is importable when it contains:

| File | Used for |
|---|---|
| `*.unity3d` | The 3D course (terrain, trees, buildings). Must be a standard Unity file (starts with `UnityFS`). |
| `*.GKD` | Hole data as plain JSON: par, handicap index, tee positions, pins (Thursday-Sunday), aim points, out-of-bounds line, penalty areas and drop zones. |
| `*.csv` (optional) | Scorecard; used for the course altitude in metres. |
| `coursedetails.txt` (optional) | Name, designer, description, location when the GKD file lacks them. |
| `splash.jpg` (optional) | Preview picture in the course list. |

**Encrypted `.gspcrse` packages are not supported.** GolfSimZA lists them as "Encrypted - not supported" and does not try to open or decode them.

## How to import

1. Copy the course folders to the PC, e.g. `C:\Shared\GolfSimZA-Courses\<course folder>`.
2. Start GolfSimZA → **IMPORT COURSES**.
3. Type or paste the folder (a folder of course folders, a single course folder, or an install folder containing `Core\GSP\Courses`) and press **SCAN**.
4. Press **IMPORT** on a course or **IMPORT ALL**.
5. Close the panel. Imported courses appear in the course list with their tees and hole count.

GolfSimZA stores only the hole data and preview in `%USERPROFILE%\AppData\LocalLow\DefaultCompany\GolfSimZA\Courses`.
The large course file stays where it is, so **do not move the course folder after importing** (import it again if you do).
**REMOVE** only removes the course from GolfSimZA; it never deletes your course files.

## Simulator PC

Copy the course folders to the simulator PC too (or to a network share it can read) and import them there.
Each PC keeps its own course list.

## What to expect

Courses were built with Unity 2018. GolfSimZA (Unity 6) loads them and then repairs their materials:

* Materials that used Unity built-in shaders (Standard, Legacy, terrain, trees) are switched to this Unity's versions.
* Custom shaders that do not run in Unity 6 are replaced with the closest built-in shader, keeping the textures.
  Some grass, water and tree effects look simpler than in the original software.
* Scripts that belonged to the original software or third-party tools (vegetation spawners, post-processing,
  animated grass) are not present, so anything they created at run time is missing. Terrain, meshes and Unity trees load.

If a course cannot be opened (for example a newer or damaged file) the round screen explains why and offers
**PLAY DEMO HOLES** or **BACK TO COURSES**. The Unity Player.log has the details:
`%USERPROFILE%\AppData\LocalLow\DefaultCompany\GolfSimZA\Player.log`.

## Rules applied on imported courses

* **Tees**: every enabled tee with a position. Longest first: Black, Blue, White, Yellow, Green, Red, Junior, Par3.
* **Pins**: Easy = Thursday, Standard = Friday, Tournament = Sunday.
* **Aim**: the tee shot aims at the first aim point (doglegs), later shots at the pin. Adjust with ← / →.
* **Out of bounds**: ball finishing outside the course boundary → +1 stroke, replay from the previous spot.
* **Penalty areas**: +1 stroke, drop at the course's drop zone when it has one, otherwise replay from the previous spot. Free-drop areas cost no stroke.
* **Holing out**: the ball drops when it reaches the cup slowly enough. Gimme distance (Round Settings) holes out automatically with one more stroke.
