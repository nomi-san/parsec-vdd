
#define MyAppName "ParsecVDisplay"
#define MyAppPublisher "Nguyen Duy"
#define MyAppURL "https://github.com/nomi-san/parsec-vdd"
#define MyAppExeName "ParsecVDisplay.exe"
#define MyAppCopyright "(c) 2024 Nguyen Duy."

#define _Major
#define _Minor
#define _Rev
#define _Build
#define MyAppVersion GetVersionComponents("..\bin\ParsecVDisplay.exe", _Major, _Minor, _Rev, _Build), Str(_Major) + "." + Str(_Minor) + "." + Str(_Rev)

[Setup]
AppId={{D2005B5A-A8C4-4B77-807F-155132973D5D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoCompany={#MyAppPublisher}
VersionInfoCopyright={#MyAppCopyright}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={commonpf64}\{#MyAppName}
UsePreviousAppDir=yes
DisableProgramGroupPage=yes
LicenseFile=.\LICENSE
PrivilegesRequired=admin
OutputDir=.\out
OutputBaseFilename={#MyAppName}-v{#MyAppVersion}-setup
SetupIconFile=.\icon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}   
UninstallDisplayIcon={app}\{#MyAppExeName}
ChangesEnvironment=true

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
Name: "{app}"; Permissions: everyone-full
Name: "{app}\cli"; Permissions: everyone-full
Name: "{app}\driver"; Permissions: everyone-full

[Files]
Source: "..\bin\version"; DestDir: "{app}"; Flags: skipifsourcedoesntexist
Source: "..\bin\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\bin\{#MyAppExeName}.config"; DestDir: "{app}"; Flags: skipifsourcedoesntexist
Source: "..\bin\vdd.cmd"; DestDir: "{app}\cli"; Flags: ignoreversion
Source: ".\drivers\parsec-vdd-0.41.0.0.exe"; DestDir: "{app}\drivers"; Flags: ignoreversion
Source: ".\drivers\parsec-vdd-0.45.0.0.exe"; DestDir: "{app}\drivers"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Tasks]
Name: add_startup; Description: "Add program to startup"
Name: add_cli_path; Description: "Add VDD (CLI) to PATH variable"
Name: install_vdd_none; Description: "Do not install"; GroupDescription: "Driver setup"; Flags: exclusive unchecked
Name: install_vdd_0_41; Description: "Parsec VDD v0.41"; GroupDescription: "Driver setup"; Flags: exclusive unchecked
Name: install_vdd_0_45; Description: "Parsec VDD v0.45"; GroupDescription: "Driver setup"; Flags: exclusive

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runascurrentuser;
Filename: "{app}\drivers\parsec-vdd-0.41.0.0.exe"; Parameters: "/S"; Flags: runascurrentuser; Tasks: install_vdd_0_41
Filename: "{app}\drivers\parsec-vdd-0.45.0.0.exe"; Parameters: "/S"; Flags: runascurrentuser; Tasks: install_vdd_0_45;

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" -silent"; Flags: uninsdeletevalue; Tasks: add_startup

[Code]
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectTasks then
  begin
    WizardForm.TasksList.Checked[0] := True;
    WizardForm.TasksList.Checked[1] := True;
  end;
end;

const EnvironmentKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';

procedure EnvAddPath(Path: string);
var
  Paths: string;
begin
  { Retrieve current path (use empty string if entry not exists) }
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Paths)
  then Paths := '';

  { Skip if string already found in path }
  if Pos(';' + Uppercase(Path) + ';', ';' + Uppercase(Paths) + ';') > 0 then exit;

  { App string to the end of the path variable }
  Paths := Paths + ';'+ Path +';'

  { Overwrite (or create if missing) path environment variable }
  if RegWriteStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Paths)
  then Log(Format('The [%s] added to PATH: [%s]', [Path, Paths]))
  else Log(Format('Error while adding the [%s] to PATH: [%s]', [Path, Paths]));
end;

procedure EnvRemovePath(Path: string);
var
  Paths: string;
  P: Integer;
begin
  { Skip if registry entry not exists }
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Paths) then
      exit;

  { Skip if string not found in path }
  P := Pos(';' + Uppercase(Path) + ';', ';' + Uppercase(Paths) + ';');
  if P = 0 then exit;

  { Update path variable }
  Delete(Paths, P - 1, Length(Path) + 1);

  { Overwrite path environment variable }
  if RegWriteStringValue(HKEY_LOCAL_MACHINE, EnvironmentKey, 'Path', Paths)
  then Log(Format('The [%s] removed from PATH: [%s]', [Path, Paths]))
  else Log(Format('Error while removing the [%s] from PATH: [%s]', [Path, Paths]));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and IsTaskSelected('add_cli_path')
  then EnvAddPath(ExpandConstant('{app}') + '\cli');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall
  then EnvRemovePath(ExpandConstant('{app}') + '\cli');
end;
