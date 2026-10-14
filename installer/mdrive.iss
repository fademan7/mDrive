#ifndef BundleRoot
  #define BundleRoot "..\release\distribution-0516"
#endif
#ifndef DriverRoot
  #define DriverRoot "..\release\prerequisites"
#endif
#define Version "0.5.16"
[Setup]
SetupIconFile=..\assets\mdrive.ico
AppId={{AB9A8312-51C4-4D11-893B-02DD62665F1A}
AppName=mDrive
AppVersion={#Version}
AppPublisher=fademan7 / neojshin
AppPublisherURL=https://fademan7.github.io/
AppSupportURL=https://github.com/fademan7/mDrive
DefaultDirName={autopf}\mDrive
DefaultGroupName=mDrive
UninstallDisplayIcon={app}\PhoneWheel.Receiver.exe
OutputDir=..\release\installer
OutputBaseFilename=mDrive-{#Version}-Setup-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
InfoBeforeFile=README.txt
CloseApplications=no
RestartApplications=no
AppMutex=Local\mDrive.Receiver
DisableProgramGroupPage=yes
SetupLogging=yes

[Files]
Source: "{#BundleRoot}\PhoneWheel.Receiver.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BundleRoot}\speech\vosk-model-small-en-us-0.15\*"; DestDir: "{app}\speech\vosk-model-small-en-us-0.15"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#BundleRoot}\notices\*"; DestDir: "{app}\notices"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "README.txt"; DestDir: "{app}"; DestName: "SETUP-README.txt"; Flags: ignoreversion
Source: "{#DriverRoot}\ViGEmBus_1.22.0_x64_x86_arm64.exe"; Flags: dontcopy

[Icons]
Name: "{group}\mDrive"; Filename: "{app}\PhoneWheel.Receiver.exe"; WorkingDir: "{app}"
Name: "{group}\Uninstall mDrive"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\PhoneWheel.Receiver.exe"; Parameters: "--wifi --qr --audio-check"; Description: "Open mDrive QR and Wendy settings"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function NeedDriver: Boolean;
var ImagePath: String;
begin
  Result := not (RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Services\ViGEmBus', 'ImagePath', ImagePath) and
    FileExists(ExpandConstant('{sys}\drivers\ViGEmBus.sys')));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var Driver: String; Code: Integer;
begin
  Result := '';
  if CheckForMutexes('Local\mDrive.Receiver') then begin
    Result := 'Close mDrive and pause your game before installing.';
    Exit;
  end;
  if not NeedDriver then begin
    Log('Existing ViGEmBus service and file found. Keeping the shared driver.');
    Exit;
  end;
  ExtractTemporaryFile('ViGEmBus_1.22.0_x64_x86_arm64.exe');
  Driver := ExpandConstant('{tmp}\ViGEmBus_1.22.0_x64_x86_arm64.exe');
  if CompareText(GetSHA256OfFile(Driver), '89220A7865076B342892F98865F3499FB7C4CFD673159E89D352C360FD014C6A') <> 0 then begin
    Result := 'Driver integrity check failed. Setup stopped.';
    Exit;
  end;
  if not Exec(Driver, '/install /passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, Code) then begin
    Result := 'Unable to start the official ViGEmBus installer.';
    Exit;
  end;
  if (Code = 3010) or (Code = 1641) then NeedsRestart := True
  else if Code <> 0 then begin
    Result := 'ViGEmBus installation failed (code ' + IntToStr(Code) + '). Setup did not complete.';
    Exit;
  end;
  if NeedDriver and not NeedsRestart then
    Result := 'Driver installation could not be confirmed. No success is claimed; check the official driver installer.';
end;
