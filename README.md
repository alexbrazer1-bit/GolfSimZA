# GolfSimZA

A Windows-first Unity golf simulator focused on realistic golf physics, Garmin Approach R10 integration, and a scalable course library for South African and international courses.

## Project goals

- Garmin Approach R10 as a first-class launch-monitor target
- Realistic ball-flight, bounce and roll simulation
- South African and international course support
- Downloadable course packages
- Firebase-backed profiles, statistics, course metadata and online services
- Future multiplayer and competitions
- PC/Windows-first architecture

## Important R10 integration note

Garmin currently documents third-party R10 integrations, including direct Bluetooth PC connections for GSPro and direct connections for other simulator products. This project therefore isolates launch-monitor communication behind `ILaunchMonitorAdapter` so the R10 transport can be implemented/tested independently from the simulator physics.

We will not bundle Garmin proprietary SDKs or reverse-engineered proprietary material in this repository. Any commercial Garmin compatibility will follow Garmin's applicable developer/brand requirements.

## Unity target

Unity 6 / 6000.x. The initial target is Windows Standalone 64-bit.

## Current version: 1.2.0

### 1.2 new look, players, range and HUD

- **Home screen** (GolfSim ZA design): EXIT / logo / PLAYERS / SETTINGS top bar, welcome banner and tiles for LOCAL MATCH, PRACTICE, MAP MY BAG and IMPORT COURSES. No online or tournament modes.
- **Players**: add, rename, delete, left/right handed, who plays the next round (up to 4), and each player's golf bag. Saved in `players.json`.
- **Local Match**: course list with search, favourites (☆/★, favourites listed first), library filters (all / favourites / imported / demo), sort by name or holes, 9/18 holes.
- **Round Settings**: game mode (Stroke Play, or Match Play for 2+ players with hole winners and "2 UP" status), tees, pins, round length, gimme, mulligans per player (Off/1/2/3/Unlimited), and RESUME ROUND (finished holes are saved after every hole).
- **Practice**: Practice Range and On-Course Practice (any hole, no scoring, ◀ HOLE ▶).
- **Driving range**: mown range with 25/50 m lines, target green and flag with distance and proximity, aim line, and a range panel (target distance, range width, green width, random target after every shot).
- **Play screen**: club drop-up at the bottom left with every club in the bag and its loft, hand (LH/RH) toggle, shot data tiles at the top right (ball/club speed, carry/total, VLA/HLA, back spin/spin axis, peak height/descent angle, club path/face to target, offline/smash factor), recent shots, hole card with players and distances, hole map.
- **Game menu** (≡ MENU or Esc): data tiles, settings (units, altitude), lighting, mulligan, flyover, putt grid, putt mode, show flag, shortcuts, scorecard, players & bags, end round / leave range, quit.
- **Units**: metric (km/h, m) or imperial (mph, yd).

### 1.1 course import, real physics and updates

- **Course importer** (IMPORT COURSES on the main menu): scans a folder of course folders, imports every course that has a standard Unity course file (`.unity3d`) and readable hole data (`.GKD`). Encrypted `.gspcrse` packages are detected and reported, never decoded. See `Documentation/COURSE-IMPORT.md`.
- **Playing imported courses**: real tees, pins (Easy/Standard/Tournament = Thursday/Friday/Sunday), aim points for doglegs, out of bounds and penalty areas, holing out, gimmes, pick-up, multi-player order (furthest from the hole plays), scorecard.
- **New ball-flight model**: drag and lift from spin, fitted to tour launch data (carry within about 5 m); spin axis now curves the ball; course and home altitude change carry; landing, bounce and roll follow the terrain and slopes; greens roll at green speed; putter shots roll.
- **Garmin R10 club**: the R10 cannot identify the club, so every R10 shot uses the club selected on screen (club bar, 1-8 keys, distance advisor or Map My Bag). Map My Bag now works with real R10 shots.
- **Aim**: ← / → adjusts aim (Shift = 5°), ↑ resets.
- **Local updater**: UPDATES & SETTINGS on the main menu checks your update folder, verifies the installer checksum, installs and restarts. Roll back by installing an older version. See `Documentation/UPDATES.md`.
- **Desktop installer**: `GolfSimZA-Setup-x.y.z.exe` with desktop and Start-menu shortcuts and an uninstaller.
- **Fixes**: all menu scenes stay in the build list; the round HUD is no longer disabled by the range HUD; the R10 receiver only accepts connections from this PC; thread-safe R10 packet handling; the R10 bridge build is pinned to a reviewed upstream commit.

## Earlier milestones

### 0.5 course and round foundation

- Course/tee/round selection scene
- Session state persists the selected course, tee and 9/18-hole round
- Three placeholder demo courses for UI testing
- Course selection launches the validated presentation simulator
- Real course content remains gated behind appropriate licensing/original-content requirements
- Build settings include the course-selection and simulator scenes

### 0.6 play round

- Dedicated round gameplay scene
- Hole number, par and distance HUD
- Shot data and score presentation
- Player score/distance-to-pin side panel
- Realistic practice-first driving-range presentation retained

### 0.9 player golf bags and club mapping

- Player must be selected before Map My Bag is available
- Expanded golf-club library covering woods, hybrids, irons, wedges, putter, utility/driving irons and short-game clubs
- Individual player golf bags (default 18-club bag as requested; the 36-club library stays available)
- TrackMan-inspired Map My Bag workflow
- Six required shots for every club mapping session
- Carry and total distance are calculated from the average of all six shots
- Mapped distances are stored per player and per club
- Round club selection can use the player's mapped distances

### 0.9.5 mapping analytics

- Six-shot mapping completion summary
- Individual carry and total results for all six shots
- Carry average and total average
- Best carry and carry spread/standard deviation
- Mapping result is associated with the player and club
- Repeat mappings can produce a new result summary without permanently blocking future club mappings
- Existing mapping and round-play logic remains unchanged

## Controls

| Key / button | Action |
|---|---|
| CLUB tile (bottom left), `1`-`0`, `Q`/`E`, `PgUp`/`PgDn`, `C` | Choose the club (used for R10 and test shots) |
| `Esc` / ≡ MENU | Game menu |
| `R` (range) | Put a new ball on the mat |
| `SPACE` | Keyboard test shot (development shot provider) |
| `←` / `→` (`Shift` = 5°) | Aim left / right, `↑` resets aim |
| PICK UP | Pick the ball up (+1 stroke) |
| NEXT PLAYER / NEXT HOLE | Continue the round |

## Prototype controls

1. Open the project in Unity 6.
2. Open the GolfSimZA course-selection scene.
3. Choose a demo course, tee and 9/18 holes.
4. Configure the players.
5. Select a player before opening **MAP MY BAG**.
6. Add the clubs that player actually carries.
7. Map a selected club by hitting six shots.
8. Review the six-shot result summary.
9. Return to the player's bag and continue mapping additional clubs.
10. Start a round using the mapped club distances.

The development shot provider is intentionally separate from the Garmin R10 adapter. This lets us validate the simulator physics, course/session flow, UI and personalized club mapping before enabling an approved R10 transport.

## R10 development target

The intended production path is:

`Garmin Approach R10 -> Windows Bluetooth -> GolfSimZA R10 adapter -> ShotData -> Golf physics -> Unity course`

Garmin currently documents direct Bluetooth PC connectivity for GSPro, so this architecture keeps direct Windows connectivity as the target while avoiding proprietary protocol reproduction in the repository.

## Course licensing

Real-world course representations will only be distributed when the project has the appropriate rights, licenses, or permission. The course system is designed to support licensed, original, and permitted community-created content.

## Repository structure

```text
Packages/
ProjectSettings/
Installer/            installer script + publish scripts (local updates)
R10Bridge/            R10 Bluetooth bridge settings and build notes
Assets/
  GolfSim/Core/       shot data, active club, settings, version
  GolfSim/Courses/    course library, importer, loader, shader repair
  GolfSim/Updates/    local update service
  GolfSim/Physics/
  GolfSim/LaunchMonitors/
    GarminR10/
  GolfSim/Editor/
  GolfSim/UI/
  GolfSim/Players/
  GolfSim/Courses/
  GolfSim/Firebase/
Documentation/
CoursePackages/
```
