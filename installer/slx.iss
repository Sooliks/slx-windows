#ifndef AppVersion
 #error "Build with scripts/build.ps1: AppVersion is required"
#endif
#ifndef AppFileVersion
 #error "Build with scripts/build.ps1: AppFileVersion is required"
#endif
#ifndef BundleDir
 #error "Build with scripts/build.ps1: BundleDir is required"
#endif
#ifndef ReleaseDir
 #error "Build with scripts/build.ps1: ReleaseDir is required"
#endif
[Setup]
AppId={{916CD685-F2C4-4F8B-AC9C-A5BC222A49CE}
AppName=SLX VPN
AppVersion={#AppVersion}
AppPublisher=SLX
AppPublisherURL=https://slxvpn.com
AppSupportURL=https://t.me/slxvpn?direct
AppUpdatesURL=https://github.com/Sooliks/slx-windows/releases
DefaultDirName={autopf}\SLX VPN
DisableDirPage=yes
DefaultGroupName=SLX VPN
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19041
PrivilegesRequired=admin
OutputDir={#ReleaseDir}
OutputBaseFilename=SLX-VPN-{#AppVersion}-win-x64-setup
SetupIconFile=..\assets\slx.ico
UninstallDisplayIcon={app}\SLX VPN.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppFileVersion}
VersionInfoTextVersion={#AppVersion}
LicenseFile=..\LICENSE
InfoBeforeFile=alpha-notice.txt
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
[Files]
Source: "{#BundleDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\SLX VPN"; Filename: "{app}\SLX VPN.exe"
Name: "{commondesktop}\SLX VPN"; Filename: "{app}\SLX VPN.exe"; Tasks: desktopicon
[Run]
Filename: "{app}\SLX VPN.exe"; Description: "{cm:LaunchProgram,SLX VPN}"; Flags: nowait postinstall skipifsilent runasoriginaluser
[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\scripts\manage-service.ps1"" -Action Remove"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveSLXService"
[Code]
function RunServiceAction(Action: String): Boolean;
var ExitCode: Integer;
begin
 Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
  '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\scripts\manage-service.ps1') + '" -Action ' + Action,
  '', SW_HIDE, ewWaitUntilTerminated, ExitCode) and (ExitCode = 0);
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
 Result := '';
 if FileExists(ExpandConstant('{app}\scripts\manage-service.ps1')) then
  if not RunServiceAction('Stop') then Result := 'Could not stop the existing SLX service. Close SLX and retry.';
end;
procedure CurStepChanged(CurStep: TSetupStep);
var ExitCode: Integer;
begin
 if CurStep = ssPostInstall then begin
  if not Exec(ExpandConstant('{sys}\icacls.exe'), '"' + ExpandConstant('{app}') + '" /setowner *S-1-5-32-544 /T /C', '', SW_HIDE, ewWaitUntilTerminated, ExitCode)
   or (ExitCode <> 0) then RaiseException('Could not secure SLX installation ownership.');
  if not RunServiceAction('Install') then RaiseException('SLX network service installation failed. Run the installer again to repair it.');
 end;
end;
function InitializeUninstall(): Boolean;
begin
 Result := RunServiceAction('Stop');
 if not Result then MsgBox('Could not stop SLX network service. Uninstall cancelled.', mbError, MB_OK);
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var CurrentValue, ExpectedValue: String;
begin
 if CurUninstallStep = usUninstall then begin
  ExpectedValue := '"' + ExpandConstant('{app}\SLX VPN.exe') + '" --tray';
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'SLX VPN', CurrentValue) then
   if CompareText(CurrentValue, ExpectedValue) = 0 then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'SLX VPN');
 end;
end;
