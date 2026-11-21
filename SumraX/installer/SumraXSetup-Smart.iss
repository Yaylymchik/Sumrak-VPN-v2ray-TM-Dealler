; Sumrak VPN — smart bootstrap installer (OS + CPU detection, user choice)
; Bundles Win10/11 x64/x86 and Win7/8.1 x64/x86 child installers from ..\dist

#define MyAppName "Sumrak VPN"
#define MyAppVersion "2.2.21"
#define MyAppPublisher "Sumrak VPN"
#define MyAppPublisherURL "https://github.com/Yaylymchik/Sumrak-VPN-v2ray-PC"
#define MyAppIcon "..\src\SumraX.App\Assets\sumrax.ico"

#define PayloadWin10X64 "..\dist\SumraX-VPN-Setup-2.2.21.exe"
#define PayloadWin10X86 "..\dist\SumraX-VPN-Setup-2.2.21-x86.exe"
#define PayloadWin7X64  "..\dist\SumraX-VPN-Setup-2.2.21-Win7.exe"
#define PayloadWin7X86  "..\dist\SumraX-VPN-Setup-2.2.21-Win7-x86.exe"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion} (Smart)
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppPublisherURL}
SetupIconFile={#MyAppIcon}
OutputDir=..\dist
OutputBaseFilename=SumraX-VPN-Setup-{#MyAppVersion}-Smart
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=no
CreateAppDir=no
Uninstallable=no
ShowLanguageDialog=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "{#PayloadWin10X64}"; DestDir: "{tmp}"; DestName: "p_win10_x64.exe"; Flags: dontcopy
Source: "{#PayloadWin10X86}"; DestDir: "{tmp}"; DestName: "p_win10_x86.exe"; Flags: dontcopy
Source: "{#PayloadWin7X64}"; DestDir: "{tmp}"; DestName: "p_win7_x64.exe"; Flags: dontcopy
Source: "{#PayloadWin7X86}"; DestDir: "{tmp}"; DestName: "p_win7_x86.exe"; Flags: dontcopy

[Code]
var
  ChoicePage: TWizardPage;
  RbWin10X64, RbWin10X86, RbWin7X64, RbWin7X86: TRadioButton;
  LblDetect, LblHint: TNewStaticText;

const
  IDX_WIN10_X64 = 0;
  IDX_WIN10_X86 = 1;
  IDX_WIN7_X64  = 2;
  IDX_WIN7_X86  = 3;

function L(const En, Ru: AnsiString): AnsiString;
begin
  if ActiveLanguage = 'russian' then
    Result := Ru
  else
    Result := En;
end;

function OsCaption: AnsiString;
var
  Ver: TWindowsVersion;
begin
  GetWindowsVersionEx(Ver);
  Result := Format('%d.%d.%d', [Ver.Major, Ver.Minor, Ver.Build]);
  Result := L('Windows ' + Result, 'Windows ' + Result);
  if IsWin64 then
    Result := Result + L(' (64-bit)', ' (64-bit)')
  else
    Result := Result + L(' (32-bit)', ' (32-bit)');
end;

function IsLegacyWindows: Boolean;
begin
  Result := GetWindowsVersion < PackVersionComponents(10, 0, 0, 0);
end;

function RecommendIndex: Integer;
begin
  if IsLegacyWindows then
  begin
    if IsWin64 then Result := IDX_WIN7_X64 else Result := IDX_WIN7_X86;
  end
  else
  begin
    if IsWin64 then Result := IDX_WIN10_X64 else Result := IDX_WIN10_X86;
  end;
end;

procedure SelectIndex(Index: Integer);
begin
  RbWin10X64.Checked := (Index = IDX_WIN10_X64);
  RbWin10X86.Checked := (Index = IDX_WIN10_X86);
  RbWin7X64.Checked := (Index = IDX_WIN7_X64);
  RbWin7X86.Checked := (Index = IDX_WIN7_X86);
end;

function SelectedIndex: Integer;
begin
  if RbWin10X64.Checked then Result := IDX_WIN10_X64
  else if RbWin10X86.Checked then Result := IDX_WIN10_X86
  else if RbWin7X64.Checked then Result := IDX_WIN7_X64
  else Result := IDX_WIN7_X86;
end;

function PayloadFileName(Index: Integer): String;
begin
  case Index of
    IDX_WIN10_X64: Result := 'p_win10_x64.exe';
    IDX_WIN10_X86: Result := 'p_win10_x86.exe';
    IDX_WIN7_X64:  Result := 'p_win7_x64.exe';
    IDX_WIN7_X86:  Result := 'p_win7_x86.exe';
  else
    Result := 'p_win10_x64.exe';
  end;
end;

procedure InitializeWizard;
var
  TopY: Integer;
begin
  ChoicePage := CreateCustomPage(
    wpWelcome,
    L('Choose installer package', 'Выбор сборки'),
    L('Detected your system. Pick the recommended option or override below.', 'Определена ваша система. Оставьте рекомендацию или выберите другую сборку.'));

  TopY := ScaleY(0);
  LblDetect := TNewStaticText.Create(ChoicePage);
  LblDetect.Parent := ChoicePage.Surface;
  LblDetect.Left := ScaleX(0);
  LblDetect.Top := TopY;
  LblDetect.Width := ChoicePage.SurfaceWidth;
  LblDetect.Caption := L('Detected: ', 'Определено: ') + OsCaption;

  TopY := TopY + ScaleY(24);
  LblHint := TNewStaticText.Create(ChoicePage);
  LblHint.Parent := ChoicePage.Surface;
  LblHint.Left := ScaleX(0);
  LblHint.Top := TopY;
  LblHint.Width := ChoicePage.SurfaceWidth;
  LblHint.Caption := L(
    'Win7/8.1 builds need .NET 6 Desktop Runtime. Win10/11 builds are self-contained.',
    'Сборки Win7/8.1 требуют .NET 6 Desktop Runtime. Win10/11 — self-contained.');

  TopY := TopY + ScaleY(28);
  RbWin10X64 := TRadioButton.Create(ChoicePage);
  RbWin10X64.Parent := ChoicePage.Surface;
  RbWin10X64.Left := ScaleX(0);
  RbWin10X64.Top := TopY;
  RbWin10X64.Width := ChoicePage.SurfaceWidth;
  RbWin10X64.Caption := L('Windows 10 / 11 — 64-bit (recommended on modern PCs)', 'Windows 10 / 11 — 64-bit (рекомендуется)');

  TopY := TopY + ScaleY(24);
  RbWin10X86 := TRadioButton.Create(ChoicePage);
  RbWin10X86.Parent := ChoicePage.Surface;
  RbWin10X86.Left := ScaleX(0);
  RbWin10X86.Top := TopY;
  RbWin10X86.Width := ChoicePage.SurfaceWidth;
  RbWin10X86.Caption := L('Windows 10 / 11 — 32-bit (x86)', 'Windows 10 / 11 — 32-bit (x86)');

  TopY := TopY + ScaleY(24);
  RbWin7X64 := TRadioButton.Create(ChoicePage);
  RbWin7X64.Parent := ChoicePage.Surface;
  RbWin7X64.Left := ScaleX(0);
  RbWin7X64.Top := TopY;
  RbWin7X64.Width := ChoicePage.SurfaceWidth;
  RbWin7X64.Caption := L('Windows 7 SP1 / 8.1 — 64-bit (.NET 6)', 'Windows 7 SP1 / 8.1 — 64-bit (.NET 6)');

  TopY := TopY + ScaleY(24);
  RbWin7X86 := TRadioButton.Create(ChoicePage);
  RbWin7X86.Parent := ChoicePage.Surface;
  RbWin7X86.Left := ScaleX(0);
  RbWin7X86.Top := TopY;
  RbWin7X86.Width := ChoicePage.SurfaceWidth;
  RbWin7X86.Caption := L('Windows 7 SP1 / 8.1 — 32-bit (.NET 6)', 'Windows 7 SP1 / 8.1 — 32-bit (.NET 6)');

  SelectIndex(RecommendIndex);
  if not IsWin64 then
  begin
    RbWin10X64.Enabled := False;
    RbWin7X64.Enabled := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Idx: Integer;
  Payload, PayloadPath: String;
  ResultCode: Integer;
begin
  if CurStep <> ssInstall then Exit;

  Idx := SelectedIndex;
  Payload := PayloadFileName(Idx);
  ExtractTemporaryFile(Payload);
  PayloadPath := ExpandConstant('{tmp}\') + Payload;

  if not FileExists(PayloadPath) then
  begin
    MsgBox(L('Payload missing in Smart installer — rebuild dist packages.', 'Внутренняя сборка не найдена — пересоберите dist.'), mbError, MB_OK);
    Exit;
  end;

  if not Exec(PayloadPath, '', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    MsgBox(L('Could not run the selected installer.', 'Не удалось запустить выбранный установщик.'), mbError, MB_OK)
  else if ResultCode <> 0 then
    MsgBox(L('Installer finished with error code: ', 'Установщик завершился с кодом: ') + IntToStr(ResultCode), mbError, MB_OK);
end;