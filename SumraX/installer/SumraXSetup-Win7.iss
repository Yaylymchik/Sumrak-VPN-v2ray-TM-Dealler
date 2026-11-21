; Sumrak VPN — Windows 7 SP1 / 8.1 (net6.0-windows7.0) + bundled .NET 6 Desktop x64

#define MyAppName "Sumrak VPN"
#define MyAppVersion "2.2.21"
#define MyAppPublisher "Sumrak VPN"
#define MyAppPublisherURL "https://github.com/Yaylymchik/Sumrak-VPN-v2ray-PC"
#define MyAppSupportURL "https://github.com/Yaylymchik/Sumrak-VPN-v2ray-PC/issues"
#define MyAppExeName "Sumrak VPN.exe"
#define MyAppIcon "..\src\SumraX.App\Assets\sumrax.ico"
#define RedistDir "..\runtime\dotnet-redist"
#define DotNet6X64 "windowsdesktop-runtime-6.0.36-win-x64.exe"

[Setup]
AppId={{C3D4E5F6-A7B8-9012-CDEF-123456789012}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion} (Win7/8.1)
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppPublisherURL}
AppSupportURL={#MyAppSupportURL}
SetupIconFile={#MyAppIcon}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\dist
OutputBaseFilename=SumraX-VPN-Setup-{#MyAppVersion}-Win7
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
MinVersion=6.1sp1

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#RedistDir}\{#DotNet6X64}"; DestDir: "{tmp}"; DestName: "dotnet6-x64-setup.exe"; Flags: deleteafterinstall
Source: "..\publish-win7\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish-win7\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
#include "dotnet-prereq.iss"

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  EnsureDotNet6DesktopX64;
end;