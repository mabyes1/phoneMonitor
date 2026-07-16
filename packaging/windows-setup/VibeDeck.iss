; VibeDeck Windows Setup — Inno Setup 6+
; Built by scripts\package-windows-setup.ps1

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#ifndef MyPayloadDir
  #define MyPayloadDir "..\..\artifacts\windows-setup\payload"
#endif

#ifndef MyOutputDir
  #define MyOutputDir "..\..\artifacts\windows-setup"
#endif

#define MyAppName "VibeDeck"
#define MyAppPublisher "VibeDeck"
#define MyAppURL "http://127.0.0.1:5000"
#define MyAppExeName "VibeDeck.Host.exe"
#define MyServiceName "VibeDeckHost"
#define MyServiceDisplayName "VibeDeck Host"

[Setup]
AppId={{A8F3C2E1-7B4D-4F9A-9C21-6E8B0D4F1A2C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\..\LICENSE
OutputDir={#MyOutputDir}
OutputBaseFilename=VibeDeck-Setup-{#MyAppVersion}
SetupIconFile=vibedeck.ico
UninstallDisplayIcon={app}\vibedeck.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=force
RestartIfNeededByRun=no
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductName={#MyAppName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon to open the VibeDeck web UI"; GroupDescription: "Additional icons:"; Flags: checkedonce
Name: "autostart"; Description: "Start VibeDeck automatically when a user signs in"; GroupDescription: "Background app:"; Flags: checkedonce

[Files]
Source: "{#MyPayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Open-VibeDeck.cmd"; DestDir: "{app}"; Flags: ignoreversion
Source: "Open-VibeDeck.vbs"; DestDir: "{app}"; Flags: ignoreversion
Source: "Start-VibeDeck-Host.vbs"; DestDir: "{app}"; Flags: ignoreversion
Source: "product-install.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "vibedeck.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "Stop-VibeDeck-Host.ps1"; Flags: dontcopy

[Dirs]
; Host runs in the signed-in desktop session and owns mutable product state.
Name: "{commonappdata}\VibeDeck"; Permissions: users-modify

[InstallDelete]
; Remove replaceable web/runtime trees so deleted modules cannot survive an upgrade.
Type: filesandordirs; Name: "{app}\wwwroot"
Type: filesandordirs; Name: "{app}\Installers"
Type: filesandordirs; Name: "{app}\runtimes"
; Remove the pre-0.1.1 binary name after upgrading.
Type: files; Name: "{app}\PhoneMonitor.Host.*"

[Icons]
; Primary product entry: starts the desktop Host if needed and opens the PC web UI.
Name: "{group}\{#MyAppName}"; Filename: "{app}\Open-VibeDeck.vbs"; IconFilename: "{app}\vibedeck.ico"; Comment: "Open VibeDeck web UI on this PC"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Open-VibeDeck.vbs"; IconFilename: "{app}\vibedeck.ico"; Comment: "Open VibeDeck web UI on this PC"; Tasks: desktopicon

[Run]
; Allow LAN phones to reach Host HTTP/HTTPS
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""VibeDeck Host"""; Flags: runhidden
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""VibeDeck Host"" dir=in action=allow program=""{app}\{#MyAppExeName}"" enable=yes profile=any"; Flags: runhidden; StatusMsg: "Adding firewall rule..."; Tasks: autostart
Filename: "{sys}\wscript.exe"; Parameters: """{app}\Start-VibeDeck-Host.vbs"""; Flags: runhidden nowait runasoriginaluser; StatusMsg: "Starting VibeDeck Host in your desktop session..."; Tasks: autostart
; Open web UI after install
Filename: "{app}\Open-VibeDeck.vbs"; Description: "Open VibeDeck web UI now"; Flags: postinstall nowait skipifsilent shellexec

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden; RunOnceId: "StopVibeDeckHost"
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden; RunOnceId: "DeleteVibeDeckHost"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""VibeDeck Host"""; Flags: runhidden; RunOnceId: "RemoveVibeDeckFirewall"

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "VibeDeckHost"; ValueData: """{sys}\wscript.exe"" ""{app}\Start-VibeDeck-Host.vbs"""; Flags: uninsdeletevalue; Tasks: autostart
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "VibeDeckHost"; Flags: deletevalue; Tasks: not autostart

[Code]
function ServiceExists(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), 'query {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
    and (ResultCode = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  StopScript: String;
begin
  Result := '';
  ExtractTemporaryFile('Stop-VibeDeck-Host.ps1');
  StopScript := ExpandConstant('{tmp}\Stop-VibeDeck-Host.ps1');
  if not Exec(
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -ExecutionPolicy Bypass -File "' + StopScript + '" -InstallDir "' + ExpandConstant('{app}') + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
  begin
    Result := 'VibeDeck Host could not be stopped for the update. Close VibeDeck and try again.';
    exit;
  end;
  { A legacy service locks the executable before [Files] runs and also places
    Host in Session 0. Remove it before the upgrade copies any product files. }
  if ServiceExists() then
  begin
    Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(1000);
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Exec(ExpandConstant('{sys}\sc.exe'), 'stop {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
