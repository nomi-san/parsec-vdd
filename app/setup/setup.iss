
#define MyAppName "ParsecVDisplay"
#define MyAppPublisher "Nguyen Duy"
#define MyAppURL "https://github.com/nomi-san/parsec-vdd"
#define MyAppExeName "ParsecVDisplay.exe"
#define MyAppCopyright "(c) 2024 Nguyen Duy."

#define _Major
#define _Minor
#define _Rev
#define _Build
#define VddVersion GetVersionComponents(".\parsec-vdd-setup.exe", _Major, _Minor, _Rev, _Build), Str(_Major) + "." + Str(_Minor)

[Setup]
AppId={{D2005B5A-A8C4-4B77-807F-155132973D5D}
AppName={#MyAppName}
AppVersion={#VddVersion}
AppVerName={#MyAppName} v{#VddVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoVersion={#VddVersion}
DefaultDirName={commonpf64}\{#MyAppName}
UsePreviousAppDir=yes
DisableProgramGroupPage=yes
LicenseFile=..\..\LICENSE
PrivilegesRequired=admin
OutputDir=.\
OutputBaseFilename={#MyAppName}-v{#VddVersion}-setup
SetupIconFile=..\Resources\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=classic
UninstallDisplayName={#MyAppName}   
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
Name: "{app}"; Permissions: everyone-full
Name: "{app}\driver-setup"; Permissions: everyone-full

[Files]
Source: "..\bin\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: ".\parsec-vdd-setup.exe"; DestDir: "{app}\driver"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"    

[Tasks]
Name: add_startup; Description: "Add program to startup"
Name: install_vdd; Description: "Install Parsec VDD v{#VddVersion}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser;
Filename: "{app}\driver\parsec-vdd-setup.exe"; Parameters: "/S"; Flags: runascurrentuser; Tasks: install_vdd

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" -silent"; Flags: uninsdeletevalue; Tasks: add_startup