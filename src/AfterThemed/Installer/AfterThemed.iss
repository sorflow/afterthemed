#define MyAppName "AfterThemed"
#define MyAppDisplayName "AfterThemed by Drerachi"
#ifndef MyAppVersion
#define MyAppVersion "1.3.12"
#endif
#define MyAppPublisher "Drerachi"
#define MyAppExeName "AfterThemed.exe"
#ifndef MyAppId
#define MyAppId "{{B359DA8A-527A-4C90-B5A4-9C7FDF25058E}"
#endif
#ifndef MyAppUninstallKey
#define MyAppUninstallKey "{B359DA8A-527A-4C90-B5A4-9C7FDF25058E}_is1"
#endif
#ifndef MyAppDefaultDir
#define MyAppDefaultDir "{localappdata}\Programs\AfterThemed"
#endif
#ifndef MyAppMutex
#define MyAppMutex "AfterThemed.App"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVerName={#MyAppDisplayName} {#MyAppVersion}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=Copyright (C) 2026 Drerachi. All rights reserved.
DefaultDirName={#MyAppDefaultDir}
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
AppMutex={#MyAppMutex}

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
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppUninstallKey}';

function QueryAfterThemedAtRoot(const RootKey: Integer;
  var InstalledVersion, UninstallCommand: String): Boolean;
var
  DisplayName: String;
  QuietUninstallCommand: String;
begin
  Result :=
    RegQueryStringValue(RootKey, AfterThemedUninstallKey, 'DisplayName', DisplayName) and
    (Pos('AfterThemed', DisplayName) = 1) and
    RegQueryStringValue(RootKey, AfterThemedUninstallKey, 'DisplayVersion', InstalledVersion);
  if not Result then
    Exit;

  if RegQueryStringValue(RootKey, AfterThemedUninstallKey,
    'QuietUninstallString', QuietUninstallCommand) then
    UninstallCommand := QuietUninstallCommand
  else
    Result := RegQueryStringValue(RootKey, AfterThemedUninstallKey,
      'UninstallString', UninstallCommand);
end;

function QueryInstalledAfterThemed(var InstalledRoot: Integer;
  var InstalledVersion, UninstallCommand: String): Boolean;
begin
  InstalledRoot := HKCU64;
  Result := QueryAfterThemedAtRoot(InstalledRoot, InstalledVersion, UninstallCommand);
  if not Result then
  begin
    InstalledRoot := HKCU32;
    Result := QueryAfterThemedAtRoot(InstalledRoot, InstalledVersion, UninstallCommand);
  end;
  if not Result then
  begin
    InstalledRoot := HKLM64;
    Result := QueryAfterThemedAtRoot(InstalledRoot, InstalledVersion, UninstallCommand);
  end;
  if not Result then
  begin
    InstalledRoot := HKLM32;
    Result := QueryAfterThemedAtRoot(InstalledRoot, InstalledVersion, UninstallCommand);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  InstalledRoot: Integer;
  InstalledVersion: String;
  UninstallCommand: String;
  InstalledPackedVersion: Int64;
  SetupPackedVersion: Int64;
  VersionOrder: Integer;
  ResultCode: Integer;
begin
  Result := '';
  NeedsRestart := False;

  if not QueryInstalledAfterThemed(InstalledRoot, InstalledVersion,
    UninstallCommand) then
    Exit;

  if not StrToVersion(InstalledVersion, InstalledPackedVersion) then
  begin
    Result := Format('Setup found AfterThemed %s but could not compare its version safely. Uninstall it from Windows Settings, then run Setup again.', [InstalledVersion]);
    Exit;
  end;
  if not StrToVersion('{#MyAppVersion}', SetupPackedVersion) then
  begin
    Result := 'Setup contains an invalid application version and cannot continue.';
    Exit;
  end;

  VersionOrder := ComparePackedVersion(InstalledPackedVersion, SetupPackedVersion);
  if VersionOrder = 0 then
    Exit;
  if VersionOrder > 0 then
  begin
    Result := Format('AfterThemed %s is newer than this %s installer. Uninstall the newer version explicitly before downgrading.', [InstalledVersion, '{#MyAppVersion}']);
    Exit;
  end;

  Log(Format('Removing AfterThemed %s before installing {#MyAppVersion}.', [InstalledVersion]));
  if not Exec('>', UninstallCommand +
    ' /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="' +
    ExpandConstant('{tmp}\AfterThemed-upgrade-uninstall.log') + '"', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode) then
  begin
    Result := Format('Setup could not start the uninstaller for AfterThemed %s: %s', [InstalledVersion, SysErrorMessage(ResultCode)]);
    Exit;
  end;

  if ResultCode <> 0 then
  begin
    Result := Format('AfterThemed %s could not be removed (exit code %d). Close AfterThemed and run Setup again.', [InstalledVersion, ResultCode]);
    Exit;
  end;

  if RegKeyExists(InstalledRoot, AfterThemedUninstallKey) then
    Result := Format('AfterThemed %s reported a successful uninstall, but its Windows registration remains. Uninstall it from Windows Settings, then run Setup again.', [InstalledVersion]);
end;
