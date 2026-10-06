; SimLink - Main PC setup (builds SimLink-MainPC-Setup.exe)
; Interactive:  SimLink-MainPC-Setup.exe
; Silent:       SimLink-MainPC-Setup.exe /S [/PAIRING=SIMLINK1:...] [/PAIRINGFILE=path] [/BRIDGEDIR=C:\SimLinkBridge] [/NOBRIDGE]
;               An already-paired PC keeps its pairing when no pairing code is given (upgrade).
Unicode true
ManifestDPIAware true
RequestExecutionLevel admin
SetCompressor /SOLID lzma

!define APPNAME   "SimLink Controller (Main PC)"
!define REGKEY    "Software\Microsoft\Windows\CurrentVersion\Uninstall\SimLinkMainPC"
!define VERSION   "1.1.1"
!define SRC       "..\MainPC-Controller"

Name "${APPNAME}"
OutFile "..\dist\SimLink-MainPC-Setup.exe"
InstallDir "$PROGRAMFILES64\SimLink Controller"
BrandingText "SimLink ${VERSION}"
VIProductVersion "1.1.1.0"
VIAddVersionKey "ProductName" "SimLink Main PC Controller"
VIAddVersionKey "FileDescription" "SimLink Main PC Setup"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "Private use"

!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "FileFunc.nsh"

!define MUI_ICON "..\Launcher\simlink.ico"
!define MUI_UNICON "..\Launcher\simlink.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "SimLink - Main PC"
!define MUI_WELCOMEPAGE_TEXT "This installs (or upgrades) SimLink on this MAIN PC:$\r$\n$\r$\n  - SimLink Control Panel (Start menu + desktop): every command as a button - GolfSimZA status/updates, screenshots, files, processes, remote commands, logs$\r$\n  - the 'simlink' command for terminals$\r$\n  - the Claude bridge (C:\SimLinkBridge)$\r$\n$\r$\nFirst install: install SimLink-SimPC-Setup.exe on the simulator PC FIRST to get a pairing code. Upgrades keep the current pairing.$\r$\n$\r$\nClick Next to continue."
!insertmacro MUI_PAGE_WELCOME
Page custom PairPage PairLeave
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE "SimLink is ready"
!define MUI_FINISHPAGE_TEXT "Open 'SimLink Control Panel' from the Start menu or your desktop.$\r$\n$\r$\nFor Claude: in the Claude desktop app click 'Add folder' and add C:\SimLinkBridge (or the bridge folder you chose).$\r$\n$\r$\nCommand line: open a NEW terminal and type  simlink help$\r$\n$\r$\nUninstall: Settings > Apps > SimLink Controller (Main PC)"
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "Open SimLink Control Panel now"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchPanel
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\README.txt"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Open the SimLink guide"
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Var Dlg
Var hKeep
Var hCode
Var hBridge
Var hBridgeDir
Var Code
Var CodeFile
Var Bridge
Var BridgeDir
Var Paired
Var KeepPairing

Function .onInit
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_ICONSTOP "SimLink requires Windows 10 or 11." /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
  ${If} ${RunningX64}
    SetRegView 64
  ${Else}
    StrCpy $INSTDIR "$PROGRAMFILES\SimLink Controller"
  ${EndIf}
  SetShellVarContext current
  StrCpy $Bridge ${BST_CHECKED}
  StrCpy $BridgeDir "C:\SimLinkBridge"
  ReadRegStr $0 HKLM "${REGKEY}" "BridgeDir"
  ${If} $0 != ""
    StrCpy $BridgeDir $0
  ${EndIf}
  StrCpy $CodeFile ""
  StrCpy $Code ""
  StrCpy $Paired "0"
  StrCpy $KeepPairing "0"
  ${If} ${FileExists} "$APPDATA\SimLink\token.dat"
  ${AndIf} ${FileExists} "$APPDATA\SimLink\controller.json"
    StrCpy $Paired "1"
    StrCpy $KeepPairing "1"
  ${EndIf}

  ; pre-fill the pairing code from SimLink-Pairing.txt next to this setup or on the Desktop
  ${If} ${FileExists} "$EXEDIR\SimLink-Pairing.txt"
    StrCpy $CodeFile "$EXEDIR\SimLink-Pairing.txt"
  ${ElseIf} ${FileExists} "$DESKTOP\SimLink-Pairing.txt"
    StrCpy $CodeFile "$DESKTOP\SimLink-Pairing.txt"
  ${EndIf}

  ${GetParameters} $R0
  ClearErrors
  ${GetOptions} $R0 "/PAIRINGFILE=" $R1
  ${IfNot} ${Errors}
    StrCpy $CodeFile $R1
  ${EndIf}
  ${If} $CodeFile != ""
    ClearErrors
    FileOpen $0 $CodeFile r
    ${IfNot} ${Errors}
      FileRead $0 $Code
      FileClose $0
    ${EndIf}
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/PAIRING=" $R1
  ${IfNot} ${Errors}
    StrCpy $Code $R1
  ${EndIf}
  ${If} $Code != ""
    StrCpy $KeepPairing "0"
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/BRIDGEDIR=" $R1
  ${IfNot} ${Errors}
    StrCpy $BridgeDir $R1
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/NOBRIDGE" $R1
  ${IfNot} ${Errors}
    StrCpy $Bridge ${BST_UNCHECKED}
  ${EndIf}

  ${If} ${Silent}
  ${AndIf} $KeepPairing != "1"
    StrCpy $R0 $Code 9
    ${If} $R0 != "SIMLINK1:"
      SetErrorLevel 2
      Abort
    ${EndIf}
  ${EndIf}
FunctionEnd

Function KeepClicked
  Pop $0
  ${NSD_GetState} $hKeep $1
  ${If} $1 == ${BST_CHECKED}
    EnableWindow $hCode 0
  ${Else}
    EnableWindow $hCode 1
  ${EndIf}
FunctionEnd

Function PairPage
  !insertmacro MUI_HEADER_TEXT "Pair with the simulator PC" "Enter the pairing code shown by SimLink-SimPC-Setup.exe"
  nsDialogs::Create 1018
  Pop $Dlg
  ${If} $Dlg == error
    Abort
  ${EndIf}
  StrCpy $3 0
  ${If} $Paired == "1"
    ${NSD_CreateCheckbox} 0 0 100% 12u "This PC is already paired - keep the current pairing (recommended for upgrades)"
    Pop $hKeep
    ${If} $KeepPairing == "1"
      ${NSD_Check} $hKeep
    ${EndIf}
    ${NSD_OnClick} $hKeep KeepClicked
    StrCpy $3 18
  ${EndIf}
  IntOp $4 $3 + 0
  ${NSD_CreateLabel} 0 $4u 100% 12u "Pairing code (SIMLINK1:<ip>:<port>:...). Paste with Ctrl+V:"
  Pop $0
  IntOp $4 $3 + 13
  ${NSD_CreatePassword} 0 $4u 100% 12u "$Code"
  Pop $hCode
  ${If} $CodeFile != ""
    IntOp $4 $3 + 28
    ${NSD_CreateLabel} 0 $4u 100% 12u "Filled in from $CodeFile (deleted after successful pairing)."
    Pop $0
  ${EndIf}
  IntOp $4 $3 + 48
  ${NSD_CreateCheckbox} 0 $4u 100% 12u "Install the Claude bridge (lets Claude control the simulator PC)"
  Pop $hBridge
  ${NSD_SetState} $hBridge $Bridge
  IntOp $4 $3 + 64
  ${NSD_CreateLabel} 0 $4u 100% 12u "Bridge folder (add this folder in the Claude desktop app):"
  Pop $0
  IntOp $4 $3 + 77
  ${NSD_CreateText} 0 $4u 100% 12u "$BridgeDir"
  Pop $hBridgeDir
  ${If} $Paired == "1"
    Push $hKeep
    Call KeepClicked
  ${EndIf}
  nsDialogs::Show
FunctionEnd

Function HasBadChars   ; in: $R0  out: $R9 = 1 if it contains " ' ` or $
  StrCpy $R9 0
  StrCpy $R1 0
  StrCpy $R3 `"`
  StrCpy $R4 "'"
  StrCpy $R5 '`'
  StrCpy $R6 "$$"
  ${Do}
    StrCpy $R2 $R0 1 $R1
    ${If} $R2 == ""
      ${ExitDo}
    ${EndIf}
    ${If} $R2 == $R3
    ${OrIf} $R2 == $R4
    ${OrIf} $R2 == $R5
    ${OrIf} $R2 == $R6
      StrCpy $R9 1
      ${ExitDo}
    ${EndIf}
    IntOp $R1 $R1 + 1
  ${Loop}
FunctionEnd

Function PairLeave
  ${If} $Paired == "1"
    ${NSD_GetState} $hKeep $1
    ${If} $1 == ${BST_CHECKED}
      StrCpy $KeepPairing "1"
    ${Else}
      StrCpy $KeepPairing "0"
    ${EndIf}
  ${EndIf}
  ${NSD_GetText} $hCode $Code
  ${NSD_GetState} $hBridge $Bridge
  ${NSD_GetText} $hBridgeDir $BridgeDir

  ${If} $KeepPairing != "1"
    StrCpy $R0 $Code 9
    ${If} $R0 != "SIMLINK1:"
      MessageBox MB_ICONEXCLAMATION "That is not a SimLink pairing code. It must start with SIMLINK1: - copy it exactly from the simulator PC setup (or SimLink-Pairing.txt)."
      Abort
    ${EndIf}
    StrCpy $R0 $Code
    Call HasBadChars
    ${If} $R9 == 1
      MessageBox MB_ICONEXCLAMATION "The pairing code contains invalid characters. Copy it again exactly."
      Abort
    ${EndIf}
  ${EndIf}
  ${If} $Bridge == ${BST_CHECKED}
    StrCpy $R1 $BridgeDir 2 1
    StrLen $R2 $BridgeDir
    ${If} $R1 != ":\"
    ${OrIf} $R2 < 4
      MessageBox MB_ICONEXCLAMATION "Enter a full bridge folder path such as C:\SimLinkBridge (not a whole drive)."
      Abort
    ${EndIf}
    StrCpy $R0 $BridgeDir
    Call HasBadChars
    ${If} $R9 == 1
      MessageBox MB_ICONEXCLAMATION "The bridge folder may not contain quotes, backticks or $$ characters."
      Abort
    ${EndIf}
  ${EndIf}
FunctionEnd

Section "Install"
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File "${SRC}\SimLink.ps1"
  File "${SRC}\SimLink.Core.ps1"
  File "${SRC}\SimLink.Common.ps1"
  File "${SRC}\SimLink-Panel.ps1"
  File "${SRC}\Test-SimLink.ps1"
  File "${SRC}\Install-Controller.ps1"
  File "${SRC}\Uninstall-Controller.ps1"
  File "${SRC}\SimLink.exe"
  File "${SRC}\simlink.ico"
  File "${SRC}\WindowsDisplays.cs"
  File "..\README.txt"

  InitPluginsDir
  StrCpy $0 '"$WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$INSTDIR\Install-Controller.ps1" -JobsDir "$BridgeDir"'
  ${If} $KeepPairing == "1"
    StrCpy $0 "$0 -KeepPairing"
  ${Else}
    ; pairing code goes through a temp file (never on a command line)
    FileOpen $2 "$PLUGINSDIR\pair.txt" w
    FileWrite $2 $Code
    FileClose $2
    StrCpy $0 '$0 -PairingFile "$PLUGINSDIR\pair.txt"'
  ${EndIf}
  ${If} $Bridge != ${BST_CHECKED}
    StrCpy $0 "$0 -NoBridge"
  ${EndIf}

  ${If} ${RunningX64}
    ${DisableX64FSRedirection}
  ${EndIf}
  DetailPrint "Installing SimLink app, pairing and bridge..."
  nsExec::ExecToLog $0
  Pop $1
  ${If} ${RunningX64}
    ${EnableX64FSRedirection}
  ${EndIf}
  Delete "$PLUGINSDIR\pair.txt"

  ${If} $1 == "error"
  ${OrIf} $1 == "timeout"
    MessageBox MB_ICONSTOP "Could not start PowerShell ($1). SimLink was not installed." /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
  ${If} $1 != 0
    MessageBox MB_ICONSTOP "Setup failed (code $1). Click 'Show details' to see why.$\r$\n$\r$\nFor pairing problems check that the simulator PC is on and logged in, the pairing code is correct, and this PC's IP is the one entered on the simulator PC." /SD IDOK
    SetErrorLevel 4
    Abort
  ${EndIf}

  ${If} $KeepPairing != "1"
  ${AndIf} $CodeFile != ""
    Delete $CodeFile
    ${If} ${FileExists} $CodeFile
      DetailPrint "Could not delete $CodeFile - please delete it yourself."
    ${Else}
      DetailPrint "Deleted $CodeFile"
    ${EndIf}
  ${EndIf}

  ; shortcuts to the installed app (per user)
  CreateShortCut "$SMPROGRAMS\SimLink Control Panel.lnk" "$LOCALAPPDATA\SimLink\app\SimLink.exe" "" "$LOCALAPPDATA\SimLink\app\simlink.ico" 0 SW_SHOWNORMAL "" "Control the GolfSimZA simulator PC"
  CreateShortCut "$DESKTOP\SimLink Control Panel.lnk" "$LOCALAPPDATA\SimLink\app\SimLink.exe" "" "$LOCALAPPDATA\SimLink\app\simlink.ico" 0 SW_SHOWNORMAL "" "Control the GolfSimZA simulator PC"

  WriteUninstaller "$INSTDIR\Uninstall-SimLink.exe"
  WriteRegStr HKLM "${REGKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${REGKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${REGKEY}" "DisplayIcon" "$INSTDIR\simlink.ico"
  WriteRegStr HKLM "${REGKEY}" "Publisher" "SimLink"
  WriteRegStr HKLM "${REGKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${REGKEY}" "UninstallString" '"$INSTDIR\Uninstall-SimLink.exe"'
  WriteRegStr HKLM "${REGKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall-SimLink.exe" /S'
  WriteRegStr HKLM "${REGKEY}" "BridgeDir" "$BridgeDir"
  WriteRegDWORD HKLM "${REGKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${REGKEY}" "NoRepair" 1
  WriteRegDWORD HKLM "${REGKEY}" "EstimatedSize" 300
SectionEnd

Function LaunchPanel
  ; start through Explorer so the panel runs as the normal (non-admin) user
  SetShellVarContext current
  Exec '"$WINDIR\explorer.exe" "$LOCALAPPDATA\SimLink\app\SimLink.exe"'
FunctionEnd

; ---------------------------------------------------------------- uninstall
Function un.onInit
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
FunctionEnd

Section "Uninstall"
  SetShellVarContext current
  ReadRegStr $3 HKLM "${REGKEY}" "BridgeDir"
  ${If} $3 == ""
    StrCpy $3 "C:\SimLinkBridge"
  ${EndIf}
  InitPluginsDir
  CopyFiles /SILENT "$INSTDIR\Uninstall-Controller.ps1" "$PLUGINSDIR\Uninstall-Controller.ps1"
  ${If} ${RunningX64}
    ${DisableX64FSRedirection}
  ${EndIf}
  nsExec::ExecToLog '"$WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\Uninstall-Controller.ps1" -JobsDir "$3"'
  Pop $1
  ${If} ${RunningX64}
    ${EnableX64FSRedirection}
  ${EndIf}
  ${If} $1 != 0
    MessageBox MB_ICONEXCLAMATION "The uninstall script reported a problem (code $1). See details." /SD IDOK
  ${EndIf}
  Delete "$SMPROGRAMS\SimLink Control Panel.lnk"
  Delete "$DESKTOP\SimLink Control Panel.lnk"
  Delete "$INSTDIR\SimLink.ps1"
  Delete "$INSTDIR\SimLink.Core.ps1"
  Delete "$INSTDIR\SimLink.Common.ps1"
  Delete "$INSTDIR\SimLink-Panel.ps1"
  Delete "$INSTDIR\Test-SimLink.ps1"
  Delete "$INSTDIR\Install-Controller.ps1"
  Delete "$INSTDIR\Uninstall-Controller.ps1"
  Delete "$INSTDIR\SimLink.exe"
  Delete "$INSTDIR\simlink.ico"
  Delete "$INSTDIR\WindowsDisplays.cs"
  Delete "$INSTDIR\README.txt"
  Delete "$INSTDIR\Uninstall-SimLink.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKLM "${REGKEY}"
  MessageBox MB_ICONINFORMATION|MB_OK "SimLink controller removed. The bridge folder $3 was kept - delete it yourself if you no longer need it." /SD IDOK
SectionEnd
