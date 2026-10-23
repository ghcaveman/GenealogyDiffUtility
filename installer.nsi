!define APPNAME "GenealogyDiffUtility"
!define VERSION "1.0.0"

Name "${APPNAME}"
OutFile "GenealogyDiffUtility-Setup.exe"

; Enforce standard native 64-bit installation path alignment
InstallDir "$PROGRAMFILES64\${APPNAME}"
RequestExecutionLevel admin

Page directory
Page instfiles

Section "Install"
  SetOutPath "$INSTDIR"
  
  ; Pulls everything compiled by the dotnet publish deployment output step
  File /r "publish\*.*"
  
  WriteUninstaller "$INSTDIR\uninstall.exe"
  CreateShortcut "$SMPROGRAMS\${APPNAME}.lnk" "$INSTDIR\GenealogyDiffUtility.exe"
SectionEnd

Section "Uninstall"
  ; Removes entire directory structure safely including engine dependencies
  Delete "$SMPROGRAMS\${APPNAME}.lnk"
  RMDir /r "$INSTDIR"
SectionEnd
