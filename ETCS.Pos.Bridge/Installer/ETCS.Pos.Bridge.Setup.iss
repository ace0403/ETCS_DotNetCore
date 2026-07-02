; ETCS POS Bridge — Inno Setup installer
; Build with: .\Build-PosBridgeSetup.ps1

#define MyAppName "ETCS POS Bridge"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ETCS"
#define MyAppExeName "ETCS.Pos.Bridge.exe"
#define ServiceName "ETCSPosBridge"
#define InstallUtil "{sys}\Microsoft.NET\Framework64\v4.0.30319\installutil.exe"

[Setup]
AppId={{B8E4F2A1-9C3D-4E5F-A1B2-3C4D5E6F7081}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ETCS\POSBridge
DefaultGroupName={#MyAppName}
OutputBaseFilename=ETCS.Pos.Bridge.Setup
OutputDir=Output
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Staged by Build-PosBridgeSetup.ps1 into Installer\POSBridge\
Source: "POSBridge\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "ETCS.Pos.Bridge.Setup.exe"

[Run]
Filename: "sc.exe"; Parameters: "create {#ServiceName} binPath= ""{app}\{#MyAppExeName}"" start= auto DisplayName= ""ETCS POS Bridge"""; StatusMsg: "Installing Windows Service..."; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "description {#ServiceName} ""Local HTTP bridge for iBonus SOAP and receipt printing."""; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "config {#ServiceName} start= auto"; StatusMsg: "Setting automatic start..."; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "failure {#ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000"; StatusMsg: "Configuring service recovery..."; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "start {#ServiceName}"; StatusMsg: "Starting service..."; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "sc.exe"; Parameters: "stop {#ServiceName}"; Flags: runhidden waituntilterminated
Filename: "sc.exe"; Parameters: "delete {#ServiceName}"; Flags: runhidden waituntilterminated

[Code]
function NetFx48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
    and (Release >= 528040);
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not NetFx48Installed then
  begin
    MsgBox('.NET Framework 4.8 or later is required.' + #13#10 +
      'Install it from https://dotnet.microsoft.com/download/dotnet-framework/net48',
      mbError, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if RegKeyExists(HKLM, 'SYSTEM\CurrentControlSet\Services\{#ServiceName}') then
  begin
    Exec('sc.exe', 'stop {#ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('sc.exe', 'delete {#ServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
