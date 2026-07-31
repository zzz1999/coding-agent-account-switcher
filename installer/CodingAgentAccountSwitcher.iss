#define AppName "Coding Agent Account Switcher"
#define AppPublisher "Coding Agent Account Switcher contributors"
#define AppExeName "CodingAgentAccountSwitcher.exe"
#define PublishDirectory AddBackslash(SourcePath) + "..\artifacts\publish"
#define AppVersion GetVersionNumbersString(PublishDirectory + "\" + AppExeName)

[Setup]
AppId={{E5D34605-0A13-418C-A504-85181E8EEF8A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/zzz1999/coding-agent-account-switcher
AppSupportURL=https://github.com/zzz1999/coding-agent-account-switcher/issues
AppUpdatesURL=https://github.com/zzz1999/coding-agent-account-switcher/releases/tag/latest
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\artifacts
OutputBaseFilename=coding-agent-account-switcher-setup-win-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
MinVersion=10.0.14393
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
VersionInfoVersion={#AppVersion}

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDirectory}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent unchecked

[UninstallRun]
; The app removes only the exact current-user startup value owned by this installed path.
Filename: "{app}\{#AppExeName}"; Parameters: "--remove-owned-startup-registration"; WorkingDir: "{app}"; RunOnceId: "RemoveOwnedStartupRegistration"; Flags: runhidden waituntilterminated skipifdoesntexist
