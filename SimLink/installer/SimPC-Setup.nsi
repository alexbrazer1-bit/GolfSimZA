; SimLink - Simulator PC setup (builds SimLink-SimPC-Setup.exe)
; Interactive:  SimLink-SimPC-Setup.exe
; Silent:       SimLink-SimPC-Setup.exe /S [/CONTROLLERIP=192.168.1.10] [/PORT=8765] [/ROOTS=C:\Shared;C:\Games]
;                                         [/NOEXEC] [/NOELEVATION] [/NOAWAKE] [/NEWTOKEN] [/PAIRINGFILE=C:\path\pairing.txt]
;               On a PC that already has SimLink, /S with no /CONTROLLERIP repairs/upgrades and keeps all settings + pairing.
Unicode true
ManifestDPIAware true
RequestExecutionLevel admin
SetCompressor /SOLID lzma

!define APPNAME   "SimLink Agent (Simulator PC)"
!define REGKEY    "Software\Microsoft\Windows\CurrentVersion\Uninstall\SimLinkSimPC"
!define VERSION   "1.1.1"
!define SRC       "..\SimPC-Agent"

Name "${APPNAME}"
OutFile "..\dist\SimLink-SimPC-Setup.exe"
InstallDir "C:\SimLink"
BrandingText "SimLink ${VERSION}"
VIProductVersion "1.1.1.0"
VIAddVersionKey "ProductName" "SimLink Simulator PC Agent"
VIAddVersionKey "FileDescription" "SimLink Simulator PC Setup"
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
!define MUI_WELCOMEPAGE_TITLE "SimLink - Simulator PC"
!define MUI_WELCOMEPAGE_TEXT "This installs (or upgrades) the SimLink agent on this SIMULATOR PC.$\r$\n$\r$\nIt lets your MAIN PC (and Claude) securely send game builds, install GolfSimZA updates, run tests, read logs and take screenshots on this PC over your home network.$\r$\n$\r$\nOnly the main PC IP address you enter will be allowed to connect. All traffic is encrypted.$\r$\n$\r$\nUpgrading keeps your settings and the main PC stays paired.$\r$\n$\r$\nClick Next to continue."
!insertmacro MUI_PAGE_WELCOME
Page custom SettingsPage SettingsLeave
!insertmacro MUI_PAGE_INSTFILES
Page custom PairingPage
!define MUI_FINISHPAGE_TITLE "SimLink agent installed"
!define MUI_FINISHPAGE_TEXT "The agent starts automatically every time you log on to this PC.$\r$\n$\r$\nFirst install: run SimLink-MainPC-Setup.exe on your main PC and enter the pairing code.$\r$\nUpgrade/repair: nothing else to do - the main PC stays paired.$\r$\n$\r$\nAudit logs: C:\SimLink\logs$\r$\nUninstall: Settings > Apps > SimLink Agent (Simulator PC)"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Var Dlg
Var hKeep
Var hIP
Var hPort
Var hRoots
Var hExec
Var hAwake
Var hElev
Var hSave
Var Existing
Var KeepSettings
Var ControllerIP
Var Port
Var Roots
Var AllowExec
Var KeepAwake
Var Elevated
Var SaveDesktop
Var NewToken
Var PairingOut
Var InstallOK
Var Pairing
Var PSExe

Function .onInit
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_ICONSTOP "SimLink requires Windows 10 or 11." /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
  StrCpy $Port "8765"
  StrCpy $Roots "C:\Shared;C:\Games"
  StrCpy $AllowExec ${BST_CHECKED}
  StrCpy $KeepAwake ${BST_CHECKED}
  StrCpy $Elevated ${BST_CHECKED}
  StrCpy $SaveDesktop ${BST_CHECKED}
  StrCpy $NewToken "0"
  StrCpy $PairingOut ""
  StrCpy $InstallOK "0"
  StrCpy $Existing "0"
  StrCpy $KeepSettings "0"
  ${If} ${FileExists} "$INSTDIR\agent.config.json"
  ${AndIf} ${FileExists} "$INSTDIR\token.dat"
    StrCpy $Existing "1"
    StrCpy $KeepSettings "1"
    StrCpy $SaveDesktop ${BST_UNCHECKED}
  ${EndIf}

  ; command line options (mainly for silent installs)
  ${GetParameters} $R0
  ClearErrors
  ${GetOptions} $R0 "/CONTROLLERIP=" $R1
  ${IfNot} ${Errors}
    StrCpy $ControllerIP $R1
    StrCpy $KeepSettings "0"
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/PORT=" $R1
  ${IfNot} ${Errors}
    StrCpy $Port $R1
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/ROOTS=" $R1
  ${IfNot} ${Errors}
    StrCpy $Roots $R1
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/NOEXEC" $R1
  ${IfNot} ${Errors}
    StrCpy $AllowExec ${BST_UNCHECKED}
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/NOELEVATION" $R1
  ${IfNot} ${Errors}
    StrCpy $Elevated ${BST_UNCHECKED}
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/NOAWAKE" $R1
  ${IfNot} ${Errors}
    StrCpy $KeepAwake ${BST_UNCHECKED}
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/NEWTOKEN" $R1
  ${IfNot} ${Errors}
    StrCpy $NewToken "1"
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/PAIRINGFILE=" $R1
  ${IfNot} ${Errors}
    StrCpy $PairingOut $R1
  ${EndIf}

  ${If} ${Silent}
    ${If} $KeepSettings != "1"
      StrCpy $R0 $ControllerIP
      Call ValidateIPv4
      ${If} $R9 != 1
        ; silent first install needs the main PC address
        SetErrorLevel 2
        Abort
      ${EndIf}
    ${EndIf}
    StrCpy $SaveDesktop ${BST_UNCHECKED}
  ${EndIf}
FunctionEnd

Function KeepClicked
  Pop $0
  ${NSD_GetState} $hKeep $1
  ${If} $1 == ${BST_CHECKED}
    StrCpy $2 0
  ${Else}
    StrCpy $2 1
  ${EndIf}
  EnableWindow $hIP $2
  EnableWindow $hPort $2
  EnableWindow $hRoots $2
  EnableWindow $hExec $2
  EnableWindow $hElev $2
FunctionEnd

Function SettingsPage
  !insertmacro MUI_HEADER_TEXT "Connection settings" "Which PC may control this simulator PC?"
  nsDialogs::Create 1018
  Pop $Dlg
  ${If} $Dlg == error
    Abort
  ${EndIf}
  StrCpy $3 0
  ${If} $Existing == "1"
    ${NSD_CreateCheckbox} 0 0 100% 12u "SimLink is already installed: keep current settings and pairing (recommended)"
    Pop $hKeep
    ${If} $KeepSettings == "1"
      ${NSD_Check} $hKeep
    ${EndIf}
    ${NSD_OnClick} $hKeep KeepClicked
    StrCpy $3 16
  ${EndIf}

  IntOp $4 $3 + 0
  ${NSD_CreateLabel} 0 $4u 55% 12u "IP address of your MAIN PC (ipconfig on the main PC):"
  Pop $0
  IntOp $4 $3 + 13
  ${NSD_CreateText} 0 $4u 50% 12u "$ControllerIP"
  Pop $hIP
  IntOp $4 $3 + 0
  ${NSD_CreateLabel} 57% $4u 40% 12u "Port:"
  Pop $0
  IntOp $4 $3 + 13
  ${NSD_CreateNumber} 57% $4u 20% 12u "$Port"
  Pop $hPort
  IntOp $4 $3 + 30
  ${NSD_CreateLabel} 0 $4u 100% 12u "Folders the main PC may read/write (separate with ;). Unity log folder is added automatically:"
  Pop $0
  IntOp $4 $3 + 43
  ${NSD_CreateText} 0 $4u 100% 12u "$Roots"
  Pop $hRoots
  IntOp $4 $3 + 60
  ${NSD_CreateCheckbox} 0 $4u 100% 12u "Allow remote commands and starting programs (needed for testing and updates)"
  Pop $hExec
  ${NSD_SetState} $hExec $AllowExec
  IntOp $4 $3 + 73
  ${NSD_CreateCheckbox} 0 $4u 100% 12u "Run with admin rights (game installers run without a UAC prompt on this PC)"
  Pop $hElev
  ${NSD_SetState} $hElev $Elevated
  IntOp $4 $3 + 86
  ${NSD_CreateCheckbox} 0 $4u 100% 12u "Keep this PC awake while on AC power (so it is always reachable)"
  Pop $hAwake
  ${NSD_SetState} $hAwake $KeepAwake
  IntOp $4 $3 + 99
  ${NSD_CreateCheckbox} 0 $4u 100% 12u "Save the pairing code to the Desktop (SimLink-Pairing.txt) - delete it after pairing"
  Pop $hSave
  ${NSD_SetState} $hSave $SaveDesktop

  ${If} $Existing == "1"
    Push $hKeep
    Call KeepClicked
  ${EndIf}
  nsDialogs::Show
FunctionEnd

; basic IPv4 check: 4 numeric parts 0-255
Function ValidateIPv4   ; in: $R0  out: $R9 = 1 ok / 0 bad
  StrCpy $R9 0
  StrCpy $R1 0      ; index
  StrCpy $R2 ""     ; current part
  StrCpy $R3 0      ; dot count
  StrLen $R4 $R0
  ${If} $R4 < 7
  ${OrIf} $R4 > 15
    Return
  ${EndIf}
  ${Do}
    StrCpy $R5 $R0 1 $R1
    ${If} $R5 == ""
    ${OrIf} $R5 == "."
      ${If} $R2 == ""
        Return
      ${EndIf}
      IntCmp $R2 255 +2 +2 0
      Goto ipbad
      StrCpy $R2 ""
      ${If} $R5 == ""
        ${ExitDo}
      ${EndIf}
      IntOp $R3 $R3 + 1
    ${Else}
      StrCpy $R6 "0123456789"
      StrCpy $R7 0
      ${Do}
        StrCpy $R8 $R6 1 $R7
        ${If} $R8 == ""
          Goto ipbad
        ${EndIf}
        ${If} $R8 == $R5
          ${ExitDo}
        ${EndIf}
        IntOp $R7 $R7 + 1
      ${Loop}
      StrCpy $R2 "$R2$R5"
      StrLen $R8 $R2
      ${If} $R8 > 3
        Goto ipbad
      ${EndIf}
    ${EndIf}
    IntOp $R1 $R1 + 1
  ${Loop}
  ${If} $R3 == 3
    StrCpy $R9 1
  ${EndIf}
  Return
  ipbad:
  StrCpy $R9 0
FunctionEnd

Function SettingsLeave
  ${If} $Existing == "1"
    ${NSD_GetState} $hKeep $1
    ${If} $1 == ${BST_CHECKED}
      StrCpy $KeepSettings "1"
    ${Else}
      StrCpy $KeepSettings "0"
    ${EndIf}
  ${EndIf}
  ${NSD_GetText} $hIP $ControllerIP
  ${NSD_GetText} $hPort $Port
  ${NSD_GetText} $hRoots $Roots
  ${NSD_GetState} $hExec $AllowExec
  ${NSD_GetState} $hAwake $KeepAwake
  ${NSD_GetState} $hElev $Elevated
  ${NSD_GetState} $hSave $SaveDesktop
  ${If} $KeepSettings == "1"
    Return
  ${EndIf}

  StrCpy $R0 $ControllerIP
  Call ValidateIPv4
  ${If} $R9 != 1
    MessageBox MB_ICONEXCLAMATION "Please enter a valid IPv4 address for the main PC (for example 192.168.1.10)."
    Abort
  ${EndIf}
  ${If} $Port == ""
  ${OrIf} $Port < 1024
  ${OrIf} $Port > 65535
    MessageBox MB_ICONEXCLAMATION "Port must be a number between 1024 and 65535."
    Abort
  ${EndIf}
  ${If} $Roots == ""
    MessageBox MB_ICONEXCLAMATION "Enter at least one folder (for example C:\Shared)."
    Abort
  ${EndIf}
  StrCpy $R0 $Roots
  Call HasBadChars
  ${If} $R9 == 1
    MessageBox MB_ICONEXCLAMATION "The folder list may not contain quotes, backticks or $$ characters."
    Abort
  ${EndIf}
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

Section "Install"
  SetOutPath "$PLUGINSDIR\src"
  File "${SRC}\SimLink-Agent.ps1"
  File "${SRC}\SimLink.Common.ps1"
  File "${SRC}\Install-Agent.ps1"
  File "${SRC}\Uninstall-Agent.ps1"

  ${If} ${RunningX64}
    ${DisableX64FSRedirection}
  ${EndIf}
  StrCpy $PSExe "$WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"

  StrCpy $0 '"$PSExe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\src\Install-Agent.ps1" -InstallDir "$INSTDIR" -PairingFile "$PLUGINSDIR\pairing.txt"'
  ${If} $KeepSettings == "1"
    StrCpy $0 "$0 -KeepSettings"
  ${Else}
    StrCpy $0 '$0 -ControllerIP $ControllerIP -Port $Port -AllowedRoots "$Roots"'
    ${If} $AllowExec != ${BST_CHECKED}
      StrCpy $0 "$0 -DisableExec"
    ${EndIf}
    ${If} $Elevated != ${BST_CHECKED}
      StrCpy $0 "$0 -NoElevation"
    ${EndIf}
  ${EndIf}
  ${If} $KeepAwake == ${BST_CHECKED}
    StrCpy $0 "$0 -KeepAwake"
  ${EndIf}
  ${If} $NewToken == "1"
    StrCpy $0 "$0 -NewToken"
  ${EndIf}

  DetailPrint "Configuring SimLink agent (firewall, startup task, token)..."
  nsExec::ExecToLog $0
  Pop $1
  ${If} ${RunningX64}
    ${EnableX64FSRedirection}
  ${EndIf}

  ${If} $1 == "error"
  ${OrIf} $1 == "timeout"
    MessageBox MB_ICONSTOP "Could not start PowerShell ($1). SimLink was not installed." /SD IDOK
    SetErrorLevel 3
    Abort
  ${EndIf}
  ${If} $1 != 0
  ${AndIf} $1 != 3
    MessageBox MB_ICONSTOP "SimLink setup failed (code $1). See 'Show details' for the reason, fix it and run setup again." /SD IDOK
    SetErrorLevel 4
    Abort
  ${EndIf}
  ${If} $1 == 3
    MessageBox MB_ICONEXCLAMATION "SimLink is installed, but the agent did not start listening yet.$\r$\nIt starts automatically at your next logon; check C:\SimLink\logs if it still does not connect." /SD IDOK
  ${EndIf}

  ClearErrors
  FileOpen $2 "$PLUGINSDIR\pairing.txt" r
  ${IfNot} ${Errors}
    FileRead $2 $Pairing
    FileClose $2
  ${EndIf}
  ${If} $SaveDesktop == ${BST_CHECKED}
  ${AndIf} $Pairing != ""
    SetShellVarContext current
    CopyFiles /SILENT "$PLUGINSDIR\pairing.txt" "$DESKTOP\SimLink-Pairing.txt"
    DetailPrint "Pairing code saved to $DESKTOP\SimLink-Pairing.txt"
  ${EndIf}
  ${If} $PairingOut != ""
  ${AndIf} $Pairing != ""
    CopyFiles /SILENT "$PLUGINSDIR\pairing.txt" "$PairingOut"
  ${EndIf}
  Delete "$PLUGINSDIR\pairing.txt"

  WriteUninstaller "$INSTDIR\Uninstall-SimLink.exe"
  File "/oname=$INSTDIR\simlink.ico" "..\Launcher\simlink.ico"
  WriteRegStr HKLM "${REGKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${REGKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${REGKEY}" "DisplayIcon" "$INSTDIR\simlink.ico"
  WriteRegStr HKLM "${REGKEY}" "Publisher" "SimLink"
  WriteRegStr HKLM "${REGKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${REGKEY}" "UninstallString" '"$INSTDIR\Uninstall-SimLink.exe"'
  WriteRegStr HKLM "${REGKEY}" "QuietUninstallString" '"$INSTDIR\Uninstall-SimLink.exe" /S'
  WriteRegDWORD HKLM "${REGKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${REGKEY}" "NoRepair" 1
  WriteRegDWORD HKLM "${REGKEY}" "EstimatedSize" 250
  StrCpy $InstallOK "1"
SectionEnd

Function PairingPage
  ${If} $InstallOK != "1"
    Abort
  ${EndIf}
  !insertmacro MUI_HEADER_TEXT "Pairing code" "Only needed the first time (or after a new token)"
  nsDialogs::Create 1018
  Pop $Dlg
  ${If} $KeepSettings == "1"
  ${AndIf} $NewToken != "1"
    StrCpy $2 "Upgrade/repair done with the SAME access token, so your main PC is still paired - nothing else to do.$\r$\n$\r$\nOnly if you ever need to pair a main PC again, use this code:"
  ${Else}
    StrCpy $2 "Enter this code in SimLink-MainPC-Setup.exe (or SimLink Control Panel > Settings) on your main PC. Select it and press Ctrl+C to copy. It contains the secret access key - keep it private."
  ${EndIf}
  ${NSD_CreateLabel} 0 0 100% 36u "$2"
  Pop $0
  ${NSD_CreateText} 0 40u 100% 14u "$Pairing"
  Pop $1
  SendMessage $1 ${EM_SETREADONLY} 1 0
  ${If} $SaveDesktop == ${BST_CHECKED}
    ${NSD_CreateLabel} 0 62u 100% 40u "It was also saved to your Desktop as SimLink-Pairing.txt. Copy that file next to SimLink-MainPC-Setup.exe on the main PC and it is filled in automatically. Delete the file after pairing."
    Pop $0
  ${EndIf}
  nsDialogs::Show
FunctionEnd

; ---------------------------------------------------------------- uninstall
Function un.onInit
  ${If} ${RunningX64}
    SetRegView 64
  ${EndIf}
FunctionEnd

Section "Uninstall"
  InitPluginsDir
  CopyFiles /SILENT "$INSTDIR\Uninstall-Agent.ps1" "$PLUGINSDIR\Uninstall-Agent.ps1"
  ${If} ${RunningX64}
    ${DisableX64FSRedirection}
  ${EndIf}
  nsExec::ExecToLog '"$WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\Uninstall-Agent.ps1" -InstallDir "$INSTDIR"'
  Pop $1
  ${If} ${RunningX64}
    ${EnableX64FSRedirection}
  ${EndIf}
  ${If} $1 != 0
    MessageBox MB_ICONEXCLAMATION "The uninstall script reported a problem (code $1). See details." /SD IDOK
  ${EndIf}
  Delete "$INSTDIR\Uninstall-SimLink.exe"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "${REGKEY}"
SectionEnd
