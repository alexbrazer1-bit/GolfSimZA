SimLink 1.1.1 - control the GolfSimZA SIMULATOR PC from the MAIN PC (and let Claude do it too)
=============================================================================================

WHERE IT LIVES
  GolfSimZA\SimLink\                    <- this folder, part of the GolfSimZA project
    dist\SimLink-SimPC-Setup.exe        setup for the SIMULATOR PC (agent)
    dist\SimLink-MainPC-Setup.exe       setup for the MAIN PC (Control Panel + command line + Claude bridge)
    Build-SimLink.ps1                   rebuilds SimLink.exe and both setups after source changes
    Common\ SimPC-Agent\ MainPC-Controller\ Launcher\ installer\   source
  Every GolfSimZA-Setup-x.y.z.exe also carries SimLink-SimPC-Setup.exe: when GolfSimZA is installed on a PC
  without the SimLink agent, the last setup page offers "Install the SimLink agent" (one admin prompt).
  It is also in the Start menu: GolfSimZA > SimLink agent setup. Silent in-game updates are not affected.

INSTALL / UPGRADE
  1. SIMULATOR PC: run SimLink-SimPC-Setup.exe
       first install: enter the MAIN PC's IP (ipconfig on the main PC) -> it shows a PAIRING CODE
       upgrade/repair: tick "keep current settings and pairing" -> nothing else to do
  2. MAIN PC: run SimLink-MainPC-Setup.exe
       first install: paste the pairing code (or put SimLink-Pairing.txt next to the setup)
       upgrade: tick "keep the current pairing"
  3. Claude: in the Claude desktop app click "Add folder" and add C:\SimLinkBridge (once).
  Unsigned setups: if Windows shows "Windows protected your PC" click "More info" > "Run anyway".
  Silent: SimLink-SimPC-Setup.exe /S            (upgrade on a PC that has SimLink)
          SimLink-SimPC-Setup.exe /S /CONTROLLERIP=192.168.10.100   (first install)
          SimLink-MainPC-Setup.exe /S           (upgrade, keeps pairing)
          SimLink-MainPC-Setup.exe /S /PAIRING=SIMLINK1:...

SIMLINK CONTROL PANEL (Start menu / desktop: "SimLink Control Panel")
  Top bar     live connection status (auto-checked every 15 s), Refresh, Run self-test
  GolfSimZA   installed version, running?, R10 bridge, Player.log time; Start / Stop / Restart game;
              list of published versions (C:\Shared\GolfSimZA-Updates) -> "Install LATEST" or a chosen version
              (copies the installer, closes the game, installs silently, verifies the version, screenshot);
              deploy a raw Unity build folder for quick tests; view Player.log
  Screen      screenshot of the sim PC (all monitors), auto-refresh, save
  Files       browse allowed folders, upload files/folders, download, new folder, delete, SHA-256
  Processes   list/filter, stop or gently close, start any program (optionally wait for exit)
  Command     run any PowerShell on the sim PC (+ presets: disk space, IP, uptime, restart PC ...)
  Logs        Player.log, SimLink audit log, bridge log, any file, last self-test report
  Settings    pair / re-pair, rotate token, bridge status + restart, GolfSimZA folders, sim PC details

COMMAND LINE (any new terminal on the main PC) - same commands as the panel
  simlink ping | info | help
  simlink gsz-status | gsz-releases | gsz-start | gsz-stop | gsz-log -Lines 300
  simlink gsz-update [-Version 1.8.1] [-ScreenshotAfter 20]
  simlink gsz-screens | gsz-swap-screens    show the sim monitors / swap game and swing-data screens
  simlink exec -Run "Get-Process GolfSimZA"
  simlink push -Local .\file.exe -Remote C:\Shared\file.exe [-Overwrite]
  simlink push -Local .\BuildFolder -Remote C:\Games\GolfSimZA-Dev [-Clean]
  simlink pull -Remote C:\Shared\report.txt -Local .\report.txt
  simlink ls | mkdir | delete [-Recurse] | stat -Remote <path>
  simlink tail -Remote <file> -Lines 200
  simlink ps [-Name GolfSimZA] | start -Path <exe> [-Arguments ".."] [-Wait] | stop -Name <name> | -Id <pid>
  simlink screenshot [-Local .\sim.png]
  simlink deploy -Local <build> -Remote <folder> -StopName GolfSimZA -Launch GolfSimZA.exe [-Clean] [-ScreenshotAfter 15]
  simlink selftest            full automated test against the sim PC (report in %APPDATA%\SimLink)
  simlink rotate-token        new access token on both PCs
  simlink panel               open the Control Panel

CLAUDE BRIDGE (C:\SimLinkBridge)
  inbox\<job>.json  e.g. {"command":"gsz-update","params":{"ScreenshotAfter":20}}
  results\<job>.result.json, files\ (screenshots/pulled files), status.json (heartbeat), bridge.log
  Bridge commands: ping info exec ls stat mkdir delete tail ps start stop push pull screenshot deploy
                   gsz-status gsz-releases gsz-update gsz-start gsz-stop gsz-log
                   (pairing and token rotation are manual only)

SECURITY
  * Only the main PC's IP can connect (Windows Firewall rule + check inside the agent).
  * Every request is AES-256 encrypted and HMAC-SHA256 signed; the token never crosses the network.
    Replayed/tampered requests are rejected; 5 failed logins in 5 min -> 15 min lockout.
  * Token stored with Windows DPAPI on both PCs; files locked to your account/admins.
  * File actions only inside the allowed folders (default C:\Shared, C:\Games, AppData\LocalLow).
  * Every action is logged on the sim PC: C:\SimLink\logs\agent-YYYYMMDD.log (30 days).
  * Remote commands = full PowerShell on the sim PC. Turn off: re-run the sim setup and untick it.
  * Home LAN only - never port-forward 8765 on the router.

TROUBLESHOOTING
  Panel shows "not reachable"   sim PC off/asleep/not logged in, or IP changed (use DHCP reservations)
  "rejected authentication"     wrong token or clocks >2 min apart -> Settings > Pair with a fresh code
  "refused this IP"             main PC IP changed -> sim setup again (untick keep settings, new IP)
  "locked out"                  wait 15 min or restart the sim PC
  Screenshot black              sim PC must be logged in and unlocked
  Self-test                     Control Panel > Run self-test (or: simlink selftest)

UNINSTALL  Settings > Apps > "SimLink Agent (Simulator PC)" / "SimLink Controller (Main PC)"
