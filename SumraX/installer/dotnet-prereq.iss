function DotNetDesktopMajorInstalled(ArchPrefix, MajorVersion: String): Boolean;
var
  SubKey: String;
  Names: TArrayOfString;
  I: Integer;
  Prefix: String;
begin
  Result := False;
  Prefix := MajorVersion + '.';
  SubKey := 'SOFTWARE\dotnet\Setup\InstalledVersions\' + ArchPrefix + '\sharedfx\Microsoft.WindowsDesktop.App';
  if not RegGetValueNames(HKLM, SubKey, Names) then
    RegGetValueNames(HKLM, 'SOFTWARE\WOW6432Node\' + SubKey, Names);
  for I := 0 to GetArrayLength(Names) - 1 do
  begin
    if Pos(Prefix, Names[I]) = 1 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function InstallBundledDotNet(DestFileName: String; DisplayName: String): Boolean;
var
  Path: String;
  Status: Integer;
begin
  Result := True;
  Path := ExpandConstant('{tmp}\') + DestFileName;
  if not FileExists(Path) then
  begin
    MsgBox('Missing bundled runtime: ' + DisplayName, mbError, MB_OK);
    Result := False;
    Exit;
  end;
  WizardForm.StatusLabel.Caption := DisplayName;
  if Exec(Path, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, Status) then
    Result := (Status = 0) or (Status = 1638) or (Status = 1641) or (Status = 3010)
  else
    Result := False;
end;

procedure EnsureDotNet6DesktopX64;
begin
  if DotNetDesktopMajorInstalled('x64', '6') then Exit;
  if not InstallBundledDotNet('dotnet6-x64-setup.exe', '.NET 6 Desktop Runtime (x64)') then
    Abort;
end;

procedure EnsureDotNet6DesktopX86;
begin
  if DotNetDesktopMajorInstalled('x86', '6') then Exit;
  if not InstallBundledDotNet('dotnet6-x86-setup.exe', '.NET 6 Desktop Runtime (x86)') then
    Abort;
end;

procedure EnsureDotNet8DesktopX64;
begin
  if DotNetDesktopMajorInstalled('x64', '8') then Exit;
  if not InstallBundledDotNet('dotnet8-x64-setup.exe', '.NET 8 Desktop Runtime (x64)') then
    Abort;
end;

procedure EnsureDotNet8DesktopX86;
begin
  if DotNetDesktopMajorInstalled('x86', '8') then Exit;
  if not InstallBundledDotNet('dotnet8-x86-setup.exe', '.NET 8 Desktop Runtime (x86)') then
    Abort;
end;