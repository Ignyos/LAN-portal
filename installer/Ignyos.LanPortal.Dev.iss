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

#ifndef InstallerFlavor
  #define InstallerFlavor "dev"
#endif

#define IsDevInstaller (LowerCase(InstallerFlavor) == "dev")

#define MyAppPublisher "Ignyos"
#define MyAppLauncherScript "Launch-LanPortal.ps1"

#if IsDevInstaller
  #define MyAppName "Ignyos LAN Portal (Dev)"
  #define MyAppInstallDir "LanPortalDev"
  #define MyAppOutputBase "Ignyos-LanPortal-Dev-"
  #define MyAppProgramGroup "Ignyos LAN Portal Dev"
#else
  #define MyAppName "Ignyos LAN Portal"
  #define MyAppInstallDir "LanPortal"
  #define MyAppOutputBase "Ignyos-LanPortal-"
  #define MyAppProgramGroup "Ignyos LAN Portal"
#endif

[Setup]
AppId={{A47A2A1D-2D72-4121-B95B-1CE67AF0D5A3}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Ignyos\{#MyAppInstallDir}
PrivilegesRequired=admin
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
OutputDir={#InstallerOutRoot}
OutputBaseFilename={#MyAppOutputBase}{#MyAppVersion}
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
Name: "{autoprograms}\{#MyAppProgramGroup}\Open Ignyos LAN Portal"; Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\{#MyAppLauncherScript}"""; WorkingDir: "{app}"

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\{#MyAppLauncherScript}"""; WorkingDir: "{app}"; Flags: nowait skipifsilent runhidden
