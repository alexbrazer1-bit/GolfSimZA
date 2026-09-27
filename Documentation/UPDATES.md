# Installing and updating GolfSimZA

GolfSimZA is installed with a normal Windows installer and updated from a **local update folder** - no internet,
store or GitHub account needed. It is designed for two PCs: the **build PC** (Unity project) and the **simulator PC**.

## One-time setup

1. **Build PC** - create the update folder and share it on the network:
   * Folder: `C:\Shared\GolfSimZA-Updates` (the default).
   * Share `C:\Shared` (right-click → Properties → Sharing) so the simulator PC can read it,
     e.g. `\\BUILD-PC\Shared\GolfSimZA-Updates`.
2. **Build PC** - get the R10 bridge once: run the GitHub Actions workflow **Build GolfSimZA R10 Bridge**, download the
   artifact `GolfSimZA-R10-Bridge-win-x64` and put `GolfSimZA-R10-Bridge-win-x64.zip` into the project's `R10Bridge` folder.
3. **Build PC** - publish the first version (below). Inno Setup is installed automatically the first time (winget).
4. **Simulator PC** - run `installers\GolfSimZA-Setup-x.y.z.exe` from the update folder. It installs to
   `%LOCALAPPDATA%\Programs\GolfSimZA` (no administrator prompt) with a desktop shortcut.
5. **Simulator PC** - start GolfSimZA → **UPDATES & SETTINGS** → set the update folder to the network path
   (`\\BUILD-PC\Shared\GolfSimZA-Updates`) → **SAVE**.

## Publishing an update (build PC)

In Unity: **GolfSimZA → Release → Release Manager**

1. **Bump patch/minor/major** - the version must be newer than the last published one.
2. Write **What changed** (shown in the game).
3. **Build + Publish Update**.

This builds `Builds\GolfSimZA\GolfSimZA.exe`, includes the R10 bridge, creates
`Installer\Output\GolfSimZA-Setup-x.y.z.exe`, copies it to `<update folder>\installers\` and updates `latest.json`
(version, SHA-256 checksum, notes).

Without opening Unity (Unity must be closed):

```powershell
cd "C:\Shared\Unity Programs\GolfSimZA\Installer"
.\Build-And-Publish.ps1 -Notes "What changed"
```

## Updating (simulator PC)

GolfSimZA checks the update folder when it starts. When a newer version exists the menu button changes to
**UPDATE x.y.z AVAILABLE**. Open it → **INSTALL x.y.z NOW**. GolfSimZA copies the installer, checks its SHA-256
checksum, closes, installs and starts again.

**Roll back**: UPDATES & SETTINGS lists every published version; press **INSTALL** next to an older one.

## What an update keeps

Updates and reinstalls replace only the program folder. These are kept:

| Data | Where |
|---|---|
| Players, golf bags, mapped club distances, round settings | Windows registry `HKCU\Software\DefaultCompany\GolfSimZA` |
| Settings (update folder, home altitude) | `%USERPROFILE%\AppData\LocalLow\DefaultCompany\GolfSimZA\settings.json` |
| Imported course list | `%USERPROFILE%\AppData\LocalLow\DefaultCompany\GolfSimZA\Courses\` |

## Safety checks

* The installer must be inside the update folder and must be an `.exe`.
* The SHA-256 checksum in `latest.json` must match the copied installer, otherwise nothing is installed.
* The installer runs per user and closes GolfSimZA and the R10 bridge before replacing files.
