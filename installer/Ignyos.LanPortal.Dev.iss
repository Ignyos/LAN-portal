; Inno Setup script for developer/QA installer builds.
; Build with: ISCC.exe installer\Ignyos.LanPortal.Dev.iss /DMyAppVersion=0.1.0-dev /DStagingRoot="C:\path\to\staging" /DInstallerOutRoot="C:\path\to\installer"

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0-dev"
#endif

#ifndef StagingRoot
  #error StagingRoot define is required.
#endif

#ifndef InstallerOutRoot
  #error InstallerOutRoot define is required.
#endif

#define MyAppName "Ignyos LAN Portal (Dev)"
#define MyAppPublisher "Ignyos"
#define MyAppLauncherScript "Launch-LanPortal.ps1"

[Setup]
AppId={{A47A2A1D-2D72-4121-B95B-1CE67AF0D5A3}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Ignyos\LanPortalDev
PrivilegesRequired=admin
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
OutputDir={#InstallerOutRoot}
OutputBaseFilename=Ignyos-LanPortal-Dev-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#StagingRoot}\app\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Ignyos LAN Portal Dev\Open Ignyos LAN Portal"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\{#MyAppLauncherScript}"""; WorkingDir: "{app}"

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\{#MyAppLauncherScript}"""; WorkingDir: "{app}"; Flags: nowait skipifsilent runhidden
