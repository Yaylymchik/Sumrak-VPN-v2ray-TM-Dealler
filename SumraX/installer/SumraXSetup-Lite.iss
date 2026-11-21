; Sumrak VPN Lite — net8 framework-dependent + bundled .NET 8 Desktop Runtime

#define MyAppName "Sumrak VPN"
#define MyAppVersion "2.2.21"
#define MyAppPublisher "Sumrak VPN"
#define MyAppPublisherURL "https://github.com/Yaylymchik/Sumrak-VPN-v2ray-PC"
#define MyAppExeName "Sumrak VPN.exe"
#define MyAppIcon "..\src\SumraX.App\Assets\sumrax.ico"
#define RedistDir "..\runtime\dotnet-redist"
#define DotNet8X64 "windowsdesktop-runtime-8.0.28-win-x64.exe"

[Setup]
AppId={{E5F6A7B8-C9D0-1234-EF01-567890123456}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion} (Lite)
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppPublisherURL}
SetupIconFile={#MyAppIcon}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\dist
OutputBaseFilename=SumraX-VPN-Setup-{#MyAppVersion}-Lite
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "{#RedistDir}\{#DotNet8X64}"; DestDir: "{tmp}"; DestName: "dotnet8-x64-setup.exe"; Flags: deleteafterinstall
Source: "..\publish-lite\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish-lite\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
#include "dotnet-prereq.iss"

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  EnsureDotNet8DesktopX64;
end;