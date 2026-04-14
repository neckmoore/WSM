; WSM Monitor — Inno Setup 6+
; Run build-installer.bat from this folder first. Sync AppVersion with WSMMonitor.App.csproj <Version>.

#define MyAppName "WSM Monitor"
#define MyAppExeName "WSMMonitor.exe"
#define MyAppVersion "1.0.3"
#define MyAppPublisher "WSM Monitor"
#define StagingDir "staging"

[Setup]
AppId={{B5E9F421-7C3A-4D8E-9F12-0123456789AB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=WSMMonitor-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
CloseApplications=no
SetupIconFile=..\Assets\wsm.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}
MinVersion=10.0

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "installservice"; Description: "Install and start the WSMMonitor Windows service"

[Files]
Source: "{#StagingDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "logs\*;*.log;appsettings.local.json"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--install-service"; StatusMsg: "Installing Windows service..."; Flags: runhidden waituntilterminated; Tasks: installservice
Filename: "{sys}\sc.exe"; Parameters: "start WSMMonitor"; StatusMsg: "Starting Windows service..."; Flags: runhidden waituntilterminated; Tasks: installservice
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent unchecked

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop WSMMonitor"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "delete WSMMonitor"; Flags: runhidden waituntilterminated

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"
Type: files; Name: "{app}\appsettings.local.json"
