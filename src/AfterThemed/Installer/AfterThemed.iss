#define MyAppName "AfterThemed"
#define MyAppDisplayName "AfterThemed by Drerachi"
#define MyAppVersion "1.3.5"
#define MyAppPublisher "Drerachi"
#define MyAppExeName "AfterThemed.exe"

[Setup]
AppId={{B359DA8A-527A-4C90-B5A4-9C7FDF25058E}
AppName={#MyAppName}
AppVerName={#MyAppDisplayName} {#MyAppVersion}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=Copyright (C) 2026 Drerachi. All rights reserved.
DefaultDirName={localappdata}\Programs\AfterThemed
DefaultGroupName={#MyAppDisplayName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\artifacts\installer
OutputBaseFilename=AfterThemed-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\AfterThemed-AppIcon.ico
LicenseFile=..\..\..\EULA.txt
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\DVAUI Theme Editor.exe"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion
Source: "..\..\..\EULA.txt"; DestDir: "{app}"; DestName: "EULA.txt"; Flags: ignoreversion
Source: "..\..\..\LICENSE.txt"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppDisplayName}"; Flags: nowait postinstall skipifsilent

[Code]
const
  AfterThemedUninstallKey =
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B359DA8A-527A-4C90-B5A4-9C7FDF25058E}_is1';

function QueryAfterThemedAtRoot(const RootKey: Integer;
  var InstalledVersion, UninstallCommand: String): Boolean;
var
  DisplayName: String;
begin
  Result :=
    RegQueryStringValue(RootKey, AfterThemedUninstallKey, 'DisplayName', DisplayName) and
    (Pos('AfterThemed', DisplayName) = 1) and
    RegQueryStringValue(RootKey, AfterThemedUninstallKey, 'DisplayVersion', InstalledVersion) and
    RegQueryStringValue(RootKey, AfterThemedUninstallKey, 'UninstallString', UninstallCommand);
end;

function QueryInstalledAfterThemed(var InstalledVersion, UninstallCommand: String): Boolean;
begin
  Result := QueryAfterThemedAtRoot(HKCU64, InstalledVersion, UninstallCommand);
  if not Result then
    Result := QueryAfterThemedAtRoot(HKCU32, InstalledVersion, UninstallCommand);
  if not Result then
    Result := QueryAfterThemedAtRoot(HKLM64, InstalledVersion, UninstallCommand);
  if not Result then
    Result := QueryAfterThemedAtRoot(HKLM32, InstalledVersion, UninstallCommand);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  InstalledVersion: String;
  UninstallCommand: String;
  ResultCode: Integer;
begin
  Result := '';
  NeedsRestart := False;

  if not QueryInstalledAfterThemed(InstalledVersion, UninstallCommand) then
    Exit;
  if CompareText(InstalledVersion, '{#MyAppVersion}') = 0 then
    Exit;

  Log(Format('Removing AfterThemed %s before installing {#MyAppVersion}.', [InstalledVersion]));
  if not Exec('>', UninstallCommand +
    ' /VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode) then
  begin
    Result := Format('Setup could not start the uninstaller for AfterThemed %s: %s', [InstalledVersion, SysErrorMessage(ResultCode)]);
    Exit;
  end;

  if ResultCode <> 0 then
    Result := Format('AfterThemed %s could not be removed (exit code %d). Close AfterThemed and run Setup again.', [InstalledVersion, ResultCode]);
end;
