!define APPNAME "GenealogyDiffUtility"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"

; CI passes the release version with /DVERSION=x.y.z; local builds fall back to this
!ifndef VERSION
  !define VERSION "0.0.0-dev"
!endif

Unicode true
Name "${APPNAME}"
OutFile "GenealogyDiffUtility-Setup.exe"

; Icon for the installer and uninstaller .exe files and their windows
Icon "${__FILEDIR__}\TwoSideBySideTrees.ico"
UninstallIcon "${__FILEDIR__}\TwoSideBySideTrees.ico"

; Enforce standard native 64-bit installation path alignment
InstallDir "$PROGRAMFILES64\${APPNAME}"
; Reuse the previous install location when upgrading
InstallDirRegKey HKLM "Software\${APPNAME}" "InstallDir"
RequestExecutionLevel admin

Page directory
Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

; Build the list of files the uninstaller removes from the publish folder, so it
; deletes exactly what was installed and never anything else in the folder.
; (Pass /DNO_GENERATE_UNINSTALL_LIST if uninstall-files.nsh was made another way.)
!ifndef NO_GENERATE_UNINSTALL_LIST
  !system 'powershell -NoProfile -ExecutionPolicy Bypass -File "${__FILEDIR__}\generate-uninstall-list.ps1" -PublishDir "${__FILEDIR__}\publish" -OutFile "${__FILEDIR__}\uninstall-files.nsh"' = 0
!endif

Section "Install"
  SetShellVarContext all
  SetOutPath "$INSTDIR"

  ; Pulls everything compiled by the dotnet publish deployment output step
  File /r "publish\*.*"

  WriteUninstaller "$INSTDIR\uninstall.exe"
  CreateShortcut "$SMPROGRAMS\${APPNAME}.lnk" "$INSTDIR\GenealogyDiffUtility.exe"

  ; Add/Remove Programs entry
  WriteRegStr HKLM "Software\${APPNAME}" "InstallDir" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKLM "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\GenealogyDiffUtility.exe"
  WriteRegStr HKLM "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${UNINSTKEY}" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegStr HKLM "${UNINSTKEY}" "QuietUninstallString" '"$INSTDIR\uninstall.exe" /S'
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINSTKEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  SetShellVarContext all
  Delete "$SMPROGRAMS\${APPNAME}.lnk"

  ; Remove only the files this installer put here (generated list), then the
  ; folders if they are now empty. Anything else in the folder is left alone.
  !include "uninstall-files.nsh"
  Delete "$INSTDIR\uninstall.exe"
  RMDir "$INSTDIR"

  DeleteRegKey HKLM "${UNINSTKEY}"
  DeleteRegKey HKLM "Software\${APPNAME}"
SectionEnd
