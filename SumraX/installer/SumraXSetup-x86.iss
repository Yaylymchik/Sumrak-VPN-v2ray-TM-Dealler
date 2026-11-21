; Sumrak VPN — 32-bit (x86) Inno Setup

#define MyAppName "Sumrak VPN"
#define MyAppVersion "2.2.21"
#define MyAppPublisher "Sumrak VPN"
#define MyAppPublisherURL "https://github.com/Yaylymchik/Sumrak-VPN-v2ray-PC"
#define MyAppSupportURL "https://github.com/Yaylymchik/Sumrak-VPN-v2ray-PC/issues"
#define MyAppExeName "Sumrak VPN.exe"
#define MyAppIcon "..\src\SumraX.App\Assets\sumrax.ico"

[Setup]
AppId={{B2C3D4E5-F6A7-8901-BCDE-F12345678901}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion} (32-bit)
DefaultDirName={autopf32}\{#MyAppName}
DefaultGroupName={#MyAppName}
SetupIconFile={#MyAppIcon}
OutputDir=..\dist
OutputBaseFilename=SumraX-VPN-Setup-{#MyAppVersion}-x86
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "..\publish-x86\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish-x86\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent