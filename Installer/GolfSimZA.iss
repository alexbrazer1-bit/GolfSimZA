; GolfSimZA installer (Inno Setup 6)
; Built by Installer\Publish-Update.ps1 - do not edit the version here.
; Installs per user (no admin prompt), so the in-game updater can upgrade silently.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\Builds\GolfSimZA"
#endif
#ifndef OutputDir
  #define OutputDir "Output"
#endif

[Setup]
AppId={{8F3C2B7A-5D1E-4C9A-9E2B-6A7D0C1F4B52}
AppName=GolfSimZA
AppVersion={#AppVersion}
AppVerName=GolfSimZA {#AppVersion}
AppPublisher=GolfSimZA
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\GolfSimZA
DefaultGroupName=GolfSimZA
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=GolfSimZA-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
CloseApplicationsFilter=GolfSimZA.exe,gspro-r10.exe
RestartApplications=no
UninstallDisplayIcon={app}\GolfSimZA.exe
UninstallDisplayName=GolfSimZA

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; Remove the previous build's data folder so no stale files survive an upgrade or rollback.
; Player bags, settings and imported courses are NOT here (they live in %USERPROFILE%\AppData\LocalLow).
Type: filesandordirs; Name: "{app}\GolfSimZA_Data"
Type: filesandordirs; Name: "{app}\MonoBleedingEdge"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*_BurstDebugInformation_DoNotShip\*,*_BackUpThisFolder_ButDontShipItWithYourGame\*"

[Icons]
Name: "{group}\GolfSimZA"; Filename: "{app}\GolfSimZA.exe"
Name: "{group}\Uninstall GolfSimZA"; Filename: "{uninstallexe}"
Name: "{autodesktop}\GolfSimZA"; Filename: "{app}\GolfSimZA.exe"; Tasks: desktopicon

[Run]
; Normal install: offer to start GolfSimZA. Silent update from the in-game button: restart it automatically.
Filename: "{app}\GolfSimZA.exe"; Description: "{cm:LaunchProgram,GolfSimZA}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\GolfSimZA.exe"; Flags: nowait; Check: WizardSilent
