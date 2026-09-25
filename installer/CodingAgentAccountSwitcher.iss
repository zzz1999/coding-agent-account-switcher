; Use ISPP's actual version: ISCC.exe file metadata can report 0.0.0.0.
#pragma message "Inno Setup compiler version: " + Str(Ver >> 24) + "." + Str((Ver >> 16) & 0xFF) + "." + Str((Ver >> 8) & 0xFF)
#define AppName "Coding Agent Account Switcher"
#define AppPublisher "Coding Agent Account Switcher contributors"
#define AppExeName "CodingAgentAccountSwitcher.exe"
#define PublishDirectory AddBackslash(SourcePath) + "..\artifacts\publish"
#ifndef AppVersion
#define FullAppVersion GetVersionNumbersString(PublishDirectory + "\" + AppExeName)
; Keep direct local builds aligned with the three-part GitHub Release version.
#define AppVersion Copy(FullAppVersion, 1, RPos(".", FullAppVersion) - 1)
#endif

[Setup]
AppId={{E5D34605-0A13-418C-A504-85181E8EEF8A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/zzz1999/coding-agent-account-switcher
AppSupportURL=https://github.com/zzz1999/coding-agent-account-switcher/issues
AppUpdatesURL=https://github.com/zzz1999/coding-agent-account-switcher/releases/latest
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\artifacts
OutputBaseFilename=CAAS-v{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Always confirm the language before entering an interactive wizard, including upgrades.
; Inno Setup skips this dialog for /SILENT, /VERYSILENT, or an explicit /LANG.
ShowLanguageDialog=yes
UsePreviousLanguage=no
LanguageDetectionMethod=uilanguage
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

[Languages]
; Match LocalizationService.SupportedLanguages; Inno names require '_' instead of '-'.
; Vendored message files keep builds independent of runner translation versions/network access.
Name: "en_US"; MessagesFile: "Languages\English.isl"
Name: "es_ES"; MessagesFile: "Languages\Spanish.isl"
Name: "fr_FR"; MessagesFile: "Languages\French.isl"
Name: "de_DE"; MessagesFile: "Languages\German.isl"
Name: "ja_JP"; MessagesFile: "Languages\Japanese.isl"
Name: "ko_KR"; MessagesFile: "Languages\Korean.isl"
Name: "pt_BR"; MessagesFile: "Languages\BrazilianPortuguese.isl"
Name: "ru_RU"; MessagesFile: "Languages\Russian.isl"
Name: "ar_SA"; MessagesFile: "Languages\Arabic.isl"
Name: "hi_IN"; MessagesFile: "Languages\Hindi.isl"

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
