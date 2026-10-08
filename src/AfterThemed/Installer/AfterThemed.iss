#define MyAppName "AfterThemed"
#define MyAppDisplayName "AfterThemed by Drerachi"
#ifndef MyAppVersion
#error MyAppVersion is required. Use Build-Installer.ps1 to read the project version.
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
; {autopf} is %LOCALAPPDATA%\Programs for a per-user install and Program Files for all users.
#define MyAppDefaultDir "{autopf}\AfterThemed"
#endif
; File associations and the sign-in entry write shared registry keys; the upgrade integration test turns
; them off so it never touches a real installation's associations.
#ifndef MyAppAssociations
#define MyAppAssociations 1
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
; Folder and Start menu pages appear on a fresh install and are skipped on upgrades.
DisableDirPage=auto
DisableProgramGroupPage=auto
AllowNoIcons=yes
UsePreviousAppDir=yes
; "Install for me only" (no admin) is the default; the dialog offers "Install for all users".
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
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
; Branding: the editor's electric blue and ice. Artwork is rendered by Render-WizardImages.cjs.
WizardStyle=modern
WizardBackColor=#EDF5FF
WizardImageFile=Images\WizardImage-100.bmp,Images\WizardImage-150.bmp,Images\WizardImage-200.bmp
WizardSmallImageFile=Images\WizardSmallImage-100.bmp,Images\WizardSmallImage-150.bmp,Images\WizardSmallImage-200.bmp
WizardImageBackColor=#100BEA
WizardSmallImageBackColor=#EDF5FF
#if MyAppAssociations
ChangesAssociations=yes
#endif
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
AppMutex={#MyAppMutex}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Make After Effects feel like yours
WelcomeLabel2=This installs [name/ver] on your computer.%n%nAfterThemed restyles After Effects with your own colors, keeps verified Adobe originals so every change can be undone, and downgrades projects for older After Effects releases.%n%nClose After Effects before you continue.
FinishedHeadingLabel=AfterThemed is ready
FinishedLabel=AfterThemed is installed. Open it to pick a palette and install your first theme.
SelectTasksDesc=Shortcuts and how AfterThemed works with your files.

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
#if MyAppAssociations
Name: "startup"; Description: "Open AfterThemed when I sign in (notices After Effects updates that removed my theme)"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "associatetheme"; Description: "Open .afterthemed theme files with AfterThemed"; GroupDescription: "Files:"
Name: "aepdowngrade"; Description: "Add ""Downgrade with AfterThemed"" when right-clicking an .aep project"; GroupDescription: "Files:"
#endif

[Files]
Source: "..\artifacts\publish\win-x64\DVAUI Theme Editor.exe"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion
Source: "..\artifacts\publish\win-x64\WebUi\*"; DestDir: "{app}\WebUi"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\..\EULA.txt"; DestDir: "{app}"; DestName: "EULA.txt"; Flags: ignoreversion
Source: "..\..\..\LICENSE.txt"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

#if MyAppAssociations
[Registry]
; .afterthemed theme files open in AfterThemed.
Root: HKA; Subkey: "Software\Classes\.afterthemed"; ValueType: string; ValueName: ""; ValueData: "AfterThemed.Theme"; Flags: uninsdeletevalue; Tasks: associatetheme
Root: HKA; Subkey: "Software\Classes\.afterthemed\OpenWithProgids"; ValueType: string; ValueName: "AfterThemed.Theme"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associatetheme
Root: HKA; Subkey: "Software\Classes\AfterThemed.Theme"; ValueType: string; ValueName: ""; ValueData: "AfterThemed theme"; Flags: uninsdeletekey; Tasks: associatetheme
Root: HKA; Subkey: "Software\Classes\AfterThemed.Theme\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: associatetheme
Root: HKA; Subkey: "Software\Classes\AfterThemed.Theme\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associatetheme
; Right-click an .aep: "Downgrade with AfterThemed". SystemFileAssociations adds the verb without
; replacing After Effects as the default program for .aep files.
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.aep\shell\AfterThemedDowngrade"; ValueType: string; ValueName: ""; ValueData: "Downgrade with AfterThemed"; Flags: uninsdeletekey; Tasks: aepdowngrade
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.aep\shell\AfterThemedDowngrade"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: aepdowngrade
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.aep\shell\AfterThemedDowngrade\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" --open-downgrader ""%1"""; Tasks: aepdowngrade
; Optional: open at sign-in.
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AfterThemed"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup
#endif

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppDisplayName}"; Flags: nowait postinstall skipifsilent

[Code]
// Headings in the brand's electric blue (TColor is $00BBGGRR).
procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel1.Font.Color := $00EA0B10;
  WizardForm.FinishedHeadingLabel.Font.Color := $00EA0B10;
  WizardForm.PageNameLabel.Font.Color := $00EA0B10;
end;

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
