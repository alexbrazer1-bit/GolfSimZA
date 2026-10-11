# SimLink - main PC <-> simulator PC

SimLink lets the main (development) PC - and Claude through the bridge folder - control the GolfSimZA
simulator PC over the home network. Source, build script and setups live in `SimLink/`.

## Parts

| Part | Runs on | What it is |
|---|---|---|
| SimLink agent | simulator PC | `C:\SimLink`, starts at logon, listens on TCP 8765 for the main PC only |
| SimLink Control Panel | main PC | Start menu / desktop app with every command as a button |
| `simlink` command | main PC | same commands for terminals and scripts |
| Claude bridge | main PC | `C:\SimLinkBridge` job folder Claude uses (add it in the Claude desktop app) |

## Setups

* `SimLink/dist/SimLink-SimPC-Setup.exe` - simulator PC (first install asks for the main PC IP and shows a pairing code;
  upgrades keep settings and pairing; silent: `/S`).
* `SimLink/dist/SimLink-MainPC-Setup.exe` - main PC (pairing code on first install; upgrades keep the pairing).
* Every `GolfSimZA-Setup-x.y.z.exe` installs `{app}\SimLink\SimLink-SimPC-Setup.exe` and, on an interactive install
  on a PC without the agent, offers "Install the SimLink agent" on the last page (admin prompt). Silent in-game
  updates never run it. Start menu: GolfSimZA > SimLink agent setup (simulator PC).

## GolfSimZA commands

| Panel button | Command | What happens |
|---|---|---|
| Refresh status | `simlink gsz-status` | installed version, running, R10 bridge, Player.log time |
| Install LATEST / selected | `simlink gsz-update [-Version x.y.z]` | copies the published installer from `C:\Shared\GolfSimZA-Updates` (SHA-256 checked), closes the game, installs silently, verifies the new version |
| Start / Stop / Restart game | `simlink gsz-start` / `gsz-stop` | |
| Load Player.log | `simlink gsz-log -Lines 300` | |
| Swap screens | `simlink gsz-swap-screens` | the other monitor becomes the Windows main display; GolfSimZA (1.8.2+) restarts itself with the game and the swing data swapped |
| Deploy build to sim PC | `simlink deploy -Local Builds\GolfSimZA -Remote C:\Games\GolfSimZA-Dev -StopName GolfSimZA -Launch GolfSimZA.exe -Clean` | raw Unity build for quick tests |

## Changing SimLink

Edit the sources in `SimLink/`, then run `SimLink/Build-SimLink.ps1` (installs NSIS with winget once) and publish
GolfSimZA as usual so the new agent setup is bundled. Run `simlink selftest` (or Control Panel > Run self-test)
to verify everything against the real simulator PC.

## Security

Only the main PC IP may connect (firewall + agent check); AES-256 + HMAC-SHA256 with a DPAPI-stored token
that never crosses the network; replay protection; lockout after 5 failed logins; file access limited to
allowed folders; every action logged in `C:\SimLink\logs` on the simulator PC.

## Screens (game + swing data)

Unity can only open the second (swing-data) screen on a monitor that is not the Windows main display, so the game always
plays on the Windows main display. SWAP SCREENS (in-game Settings > Game > Screens, the start-up picker, SimLink's
Swap screens button) makes the other monitor the main display and restarts the game. Monitors are matched by their
real names via Windows (`Assets/GolfSim/UI/WindowsDisplays.cs`, shared with SimLink), so two monitors with the same
resolution are never mixed up. Changing the main display in Windows Settings also makes the game restart into the
right layout.
