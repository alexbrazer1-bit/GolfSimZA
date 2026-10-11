# GolfSimZA - handoff for a new chat (2026-10-07)

Paste or attach this file in the new chat. It says where the project stands, what is half done, and how the work is done on this PC.

## The project

- **What it is:** GolfSimZA, a Unity 6 (6000.0.84f1) golf simulator for a Garmin R10 launch monitor.
- **Project folder:** `C:\Shared\Unity Programs\GolfSimZA` (git repository).
- **Update folder:** `C:\Shared\GolfSimZA-Updates`. Releases are published here and the sim PC installs from it.
- **Installed game:** `C:\Users\britz\AppData\Local\Programs\GolfSimZA`.
- **Game data:** `%USERPROFILE%\AppData\LocalLow\DefaultCompany\GolfSimZA`. This holds settings.json, the course library (`Courses\`), the OpenStreetMap cache (`OsmCache\`) and the test reports.
- **Sim PC:** 192.168.10.108. It was offline (still on 1.7.0) at last check. It updates from the shared update folder.
- **Monitors:** Unity runs on "LED 2023WL"; the main monitor is "S27E390".

## Released versions

| Version | What changed |
|---|---|
| 1.8.1 | Dispersion circle, roll-out, mini game scoring, out-of-bounds lines, bigger data tiles, club grid, persistent R10 link, second screen. |
| 1.8.4 | Another chat's work: SimLink, Windows display detection. |
| 1.8.5 (git 1fe9636) | **Putting removed:** balls on or near the green are holed automatically (1/2/3 putts); the putter is out of the bag.<br>**Obstructions:** hills, objects and course trees block the ball.<br>**Sloped lies:** ball above or below your feet affects the shot.<br>**Course look:** sky light, reflections, new water and leaf shaders. |
| 1.8.6 (git bac7e82) | **Club tile:** shows a club picture, the name and your Map My Bag carry. NEXT CLUB is picked from your carries for every shot (e.g. 180 m gives the 5 iron). The club grid shows carries. The on-screen slope readout is removed (the physics stays). |
| 1.8.7 (git 8417f97) | The CLUB RECOMMENDATION panel that covered the hole map is removed (`ClubDistanceAdvisorUI.cs` deleted). |

All three (1.8.5 to 1.8.7) were published and installed on this PC.

## In progress: OpenStreetMap course builder (not released yet)

**Goal:** build playable South African courses from OpenStreetMap data.

**New files:**
- `Assets/GolfSim/Courses/Osm/`:
  - `MiniJson`, `OsmModel`: reading OpenStreetMap data, building outlines from relations.
  - `OsmFeatures`: sorts the data into greens, tees, fairways, bunkers, water, woods, trees, buildings, paths and holes.
  - `Poly2D`: polygon maths.
  - `OsmNet`: Overpass servers (4 mirrors) plus elevation tiles, with a disk cache.
  - `ElevationGrid`: free ground height tiles (AWS Terrarium, zoom 15).
  - `OsmCatalog`: the South Africa course list with hole counts, cached for 7 days.
  - `OsmCourseBuilder`: works out holes, tees, pins, aim points, hazards and out of bounds, shapes the ground, and saves the files (heights.bin, surface.bin, features.json, osm.json, splash.png).
  - `OsmTerrainFiles`, `GroundTextures`.
  - `OsmCourseScene`: builds the 3D course at load. A terrain collider is used for the ball; the visible ground is GolfSimZA's own meshes with a custom shader. It also adds water ("Map_Water_*", so the ball splashes), buildings and planned trees.
- `Assets/GolfSim/Physics/CourseSurfaceMap.cs`: surface lookup used by the green check, grass and tree planting.
- `Assets/GolfSim/Resources/GolfSimZA_CourseGround.shader`: 8 surfaces from 2 splat maps, a texture array and mowing stripes.
- `Assets/GolfSim/UI/OsmCoursePanel.cs` (+ `OsmRunner`): the IMPORT COURSES → "OPENSTREETMAP • SOUTH AFRICA" tab with search, BUILD / REBUILD and a progress bar.
- `Assets/GolfSim/Testing/OsmBuildTester.cs`: editor test.

**Changed files:**
- `CourseDefinition`: `SourceOpenStreetMap`, osm fields.
- `CourseLibrary`: `IsAvailable` for OpenStreetMap courses, `DataFolderFor`.
- `CourseLoader`: `LoadBuiltCourse`.
- `CourseScenery`: skips the built terrain; no trees on paths or fairways.
- `CourseSurvey`: skips OpenStreetMap courses.
- `GreenSurface` and `GrassField`: use the surface map.
- `CourseImportPanel`: tabs.
- `GolfSimAutoPlayMenu`: new Test menu items.

**Test results so far:**
- **Course list:** 607 South African courses, 47 with 9+ holes mapped.
- **Royal Oak Country Club:** built OK. 18 holes, par 72, 6,393 m, Back/Middle/Forward tees, 5 water hazards, out-of-bounds line, ground 1,920 m square, heights 1,609 to 1,646 m.
- **Royal Oak auto round:** a round started and played through at least hole 13 with green detection working. The final round report was not read before this backup.
- **Glenvista:** failed because every Overpass server was busy (HTTP 504 / timeouts). Try again later.

**Fixes made after the first look:**
1. The ground showed as flat pale green. Unity's terrain drawing needs terrain shaders, so the ground is now drawn with GolfSimZA's own meshes and the custom shader (the terrain stays for collisions). Surfaces now show.
2. Trees: mapped woods could run for kilometres and hit the 18,000 limit. Woods are now limited to 120 m around the course, with an overall cap of 9,000.
3. Darker, richer surface colours. Ambient light is 0.85 on built courses.

Fix 3 was packed as `Logs/osm-1007d.tgz` in the cloud workspace, but at backup time it may not have reached the PC. Check that `GroundTextures.cs` has rough = `Color32(62, 92, 30)` and `CourseLoader.cs` contains `ambientIntensity = 0.85f`.

## Next steps (in order)

1. Make sure osm-1007d is applied, recompile, and look at Royal Oak again (Test → "Auto-play 18 holes - newest OpenStreetMap course, 2 players"). Tune the colours and the path brightness, and check the water look and the frame rate.
2. Build Glenvista when the Overpass servers answer.
3. Test the in-game UI: Home → IMPORT COURSES → OPENSTREETMAP tab → search → BUILD.
4. Publish 1.8.8 (Release Manager: Bump patch, notes, Build + Publish Update, then Install on this PC), and commit to git.
5. Later stages from the plan:
   - Use OpenStreetMap to fix existing GSPro courses (missing out-of-bounds lines and water hazards, hole maps, trees).
   - Smarter drop zones for water hazards (at the moment it is stroke and distance).
6. Still open from before:
   - Sim PC install (offline).
   - Apostle runs at about 10 fps in the editor.
   - `Installer/GolfSimZA.iss.before-simlink` is left uncommitted on purpose (it is a backup from the other chat).

## How the work is done (important for the next chat)

**Editing and copying files**
- Code is edited in the cloud workspace (`/home/claude/work`).
- `/home/claude/check/build.sh` compile-checks against the Unity 6 references.
- Changed files are packed as a tar with a **new unique name every time** (a reused name once delivered an old cached file) and committed to `~/mnt/Unity Programs--GolfSimZA/Logs/<name>.tgz`. On the PC run `tar --overwrite -xzf` inside `Assets/GolfSim`.
- Before overwriting a file, compare it with the PC copy. Another chat may also be editing this project.

**Running Unity**
- Unity is controlled by screenshots on the "LED 2023WL" monitor:
  - Click (130,560) to focus, then Ctrl+R to recompile.
  - GolfSimZA menu at (330,29). The Test submenu is at the top (345,78) or at the bottom (345,398); **always screenshot first**. A wrong click once ran "Build Current 0.9.5", which changed 4 scenes; they were restored with `git show HEAD:path > path`.
  - Release Manager: GolfSimZA → Release → Release Manager. It can open on the main monitor.
- Do **not** use "open application Unity" when Unity is closed. It starts an empty editor that overwrites Editor.log. Open the project from Unity Hub, or click the Unity icon on the taskbar if the editor is only minimized.

**Logs and reports**
- Editor.log is at `~/mnt/Editor/Editor.log` (read it with `tr -d '\0'`).
- Reports are in `~/mnt/DefaultCompany--GolfSimZA`: autoplay-report.txt, autoplay-trace.txt, osm-build-report.txt.

**Git**
- Command: `git -c safe.directory=* -c core.autocrlf=true -c user.name=GolfSimZA -c user.email=billing@jdelite.co.za`.
- Commit trailers: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

**Permissions**
- Folder access, delete permission and app control are granted per session and lapse after 30 minutes idle, so they are asked for again.
- Adding the folders to the task in the Claude desktop app avoids repeat folder prompts.
