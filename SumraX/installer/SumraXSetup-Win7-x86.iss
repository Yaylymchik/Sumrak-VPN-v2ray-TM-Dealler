; Sumrak VPN — Windows 7 SP1 / 8.1 x86 + bundled .NET 6 Desktop x86

#define MyAppName "Sumrak VPN"
#define MyAppVersion "2.2.21"
#define MyAppPublisher "Sumrak VPN"
#define MyAppPublisherURL "https://github.com/Yaylymchik/Sumrak-VPN-v2ray-PC"
#define MyAppExeName "Sumrak VPN.exe"
#define MyAppIcon "..\src\SumraX.App\Assets\sumrax.ico"
#define RedistDir "..\runtime\dotnet-redist"
#define DotNet6X86 "windowsdesktop-runtime-6.0.36-win-x86.exe"

[Setup]
AppId={{D4E5F6A7-B8C9-0123-DEF0-234567890123}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion} (Win7/8.1 32-bit)
DefaultDirName={autopf32}\{#MyAppName}
DefaultGroupName={#MyAppName}
SetupIconFile={#MyAppIcon}
OutputDir=..\dist
OutputBaseFilename=SumraX-VPN-Setup-{#MyAppVersion}-Win7-x86
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
MinVersion=6.1sp1

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "{#RedistDir}\{#DotNet6X86}"; DestDir: "{tmp}"; DestName: "dotnet6-x86-setup.exe"; Flags: deleteafterinstall
Source: "..\publish-win7-x86\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish-win7-x86\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
#include "dotnet-prereq.iss"

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  EnsureDotNet6DesktopX86;
end;