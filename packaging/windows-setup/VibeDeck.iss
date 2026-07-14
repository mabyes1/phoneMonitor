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
#define MyAppExeName "PhoneMonitor.Host.exe"
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
Name: "autostart"; Description: "Install VibeDeck Host as a Windows Service and start it automatically"; GroupDescription: "Background service:"; Flags: checkedonce

[Files]
Source: "{#MyPayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "Open-VibeDeck.cmd"; DestDir: "{app}"; Flags: ignoreversion
Source: "Open-VibeDeck.vbs"; DestDir: "{app}"; Flags: ignoreversion
Source: "product-install.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "vibedeck.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Primary product entry: opens the PC web UI (starts service if needed)
Name: "{group}\{#MyAppName}"; Filename: "{app}\Open-VibeDeck.vbs"; IconFilename: "{app}\vibedeck.ico"; Comment: "Open VibeDeck web UI on this PC"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Open-VibeDeck.vbs"; IconFilename: "{app}\vibedeck.ico"; Comment: "Open VibeDeck web UI on this PC"; Tasks: desktopicon

[Run]
; Register recovery + auto-start service
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden; StatusMsg: "Stopping previous VibeDeck Host service..."; Tasks: autostart; Check: ServiceExists
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden; StatusMsg: "Removing previous VibeDeck Host service..."; Tasks: autostart; Check: ServiceExists
Filename: "{sys}\sc.exe"; Parameters: "create {#MyServiceName} binPath= ""\""{app}\{#MyAppExeName}\"""" DisplayName= ""{#MyServiceDisplayName}"" start= auto obj= LocalSystem"; Flags: runhidden; StatusMsg: "Installing VibeDeck Host Windows Service..."; Tasks: autostart
Filename: "{sys}\sc.exe"; Parameters: "description {#MyServiceName} ""VibeDeck phone sideboard, AI quotas, and virtual display host. Opens on {#MyAppURL}."""; Flags: runhidden; Tasks: autostart
Filename: "{sys}\sc.exe"; Parameters: "failure {#MyServiceName} reset= 86400 actions= restart/5000/restart/10000/restart/30000"; Flags: runhidden; Tasks: autostart
Filename: "{sys}\sc.exe"; Parameters: "start {#MyServiceName}"; Flags: runhidden; StatusMsg: "Starting VibeDeck Host..."; Tasks: autostart
; Allow LAN phones to reach Host HTTP/HTTPS
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""VibeDeck Host"""; Flags: runhidden; Tasks: autostart
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""VibeDeck Host"" dir=in action=allow program=""{app}\{#MyAppExeName}"" enable=yes profile=any"; Flags: runhidden; StatusMsg: "Adding firewall rule..."; Tasks: autostart
; Open web UI after install
Filename: "{app}\Open-VibeDeck.vbs"; Description: "Open VibeDeck web UI now"; Flags: postinstall nowait skipifsilent shellexec

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden; RunOnceId: "StopVibeDeckHost"
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden; RunOnceId: "DeleteVibeDeckHost"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""VibeDeck Host"""; Flags: runhidden; RunOnceId: "RemoveVibeDeckFirewall"

[Code]
function ServiceExists(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), 'query {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
    and (ResultCode = 0);
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
