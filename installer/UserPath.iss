; Compile-time override only for synthetic registry integration tests.
#ifndef PathEnvironmentKey
  #define PathEnvironmentKey "Environment"
#endif
#ifndef PathOwnerKey
  #define PathOwnerKey "Software\SecretHandoff\Installer"
#endif

[Code]
const
  PathEnvironmentKey = '{#PathEnvironmentKey}';
  OwnerKey = '{#PathOwnerKey}';

function QueryValueType(Root: LongWord; SubKey, Name: String; Flags: LongWord;
  var ValueType: LongWord; Data: LongWord; var Size: LongWord): Longint;
  external 'RegGetValueW@advapi32.dll stdcall';

function ReadPath(var Value: String; var Exists: Boolean; var ValueType: LongWord): Boolean;
var
  Size: LongWord;
begin
  Value := '';
  Exists := RegValueExists(HKCU, PathEnvironmentKey, 'Path');
  ValueType := 2; { REG_EXPAND_SZ for a newly created PATH }
  Result := True;
  if not Exists then exit;
  Size := 0;
  Result := (QueryValueType(HKCU, PathEnvironmentKey, 'Path', $10000006,
    ValueType, 0, Size) = 0) and
    RegQueryStringValue(HKCU, PathEnvironmentKey, 'Path', Value);
end;

function WritePath(Value: String; ValueType: LongWord): Boolean;
begin
  if ValueType = 1 then
    Result := RegWriteStringValue(HKCU, PathEnvironmentKey, 'Path', Value)
  else
    Result := RegWriteExpandStringValue(HKCU, PathEnvironmentKey, 'Path', Value);
end;

function ComparablePath(Value: String): String;
begin
  Value := Trim(Value);
  if (Length(Value) >= 2) and (Value[1] = '"') and
    (Value[Length(Value)] = '"') then
    Value := Copy(Value, 2, Length(Value) - 2);
  Result := Lowercase(RemoveBackslashUnlessRoot(Value));
end;

function ContainsDirectory(Value, Directory: String): Boolean;
var
  Start, Finish: Integer;
begin
  Result := False;
  Start := 1;
  while Start <= Length(Value) + 1 do begin
    Finish := Start;
    while (Finish <= Length(Value)) and (Value[Finish] <> ';') do
      Finish := Finish + 1;
    if ComparablePath(Copy(Value, Start, Finish - Start)) = ComparablePath(Directory) then begin
      Result := True;
      exit;
    end;
    Start := Finish + 1;
  end;
end;

procedure RemoveOwnedPath;
var
  Owned, Value: String;
  Exists: Boolean;
  ValueType, OriginallyMissing: LongWord;
  Start, Finish: Integer;
begin
  if not RegQueryStringValue(HKCU, OwnerKey, 'AddedUserPath', Owned) then exit;
  if not ReadPath(Value, Exists, ValueType) then
    RaiseException('USER_PATH_READ_FAILED');
  OriginallyMissing := 0;
  RegQueryDWordValue(HKCU, OwnerKey, 'UserPathOriginallyMissing', OriginallyMissing);
  Start := 1;
  while Exists and (Start <= Length(Value)) do begin
    Finish := Start;
    while (Finish <= Length(Value)) and (Value[Finish] <> ';') do
      Finish := Finish + 1;
    { Remove only one exact entry that this installer added. }
    if Copy(Value, Start, Finish - Start) = Owned then begin
      if Start > 1 then
        Delete(Value, Start - 1, Finish - Start + 1)
      else if Finish <= Length(Value) then
        Delete(Value, Start, Finish - Start + 1)
      else
        Value := '';
      if (Value = '') and (OriginallyMissing = 1) then begin
        if not RegDeleteValue(HKCU, PathEnvironmentKey, 'Path') then
          RaiseException('USER_PATH_WRITE_FAILED');
      end else if not WritePath(Value, ValueType) then
        RaiseException('USER_PATH_WRITE_FAILED');
      break;
    end;
    Start := Finish + 1;
  end;
  RegDeleteValue(HKCU, OwnerKey, 'AddedUserPath');
  RegDeleteValue(HKCU, OwnerKey, 'UserPathOriginallyMissing');
  RegDeleteKeyIfEmpty(HKCU, OwnerKey);
end;

procedure UpdateUserPath;
var
  Value, Directory, Owned: String;
  Exists: Boolean;
  ValueType, OriginallyMissing: LongWord;
begin
  Directory := ExpandConstant('{app}');
  if RegQueryStringValue(HKCU, OwnerKey, 'AddedUserPath', Owned) then begin
    if (not WizardIsTaskSelected('userpath')) or (Owned <> Directory) then
      RemoveOwnedPath;
  end;
  if not WizardIsTaskSelected('userpath') then exit;
  if Pos(';', Directory) > 0 then
    RaiseException('USER_PATH_DIRECTORY_INVALID');
  if not ReadPath(Value, Exists, ValueType) then
    RaiseException('USER_PATH_READ_FAILED');
  if ContainsDirectory(Value, Directory) then exit;
  if Length(Value) + Length(Directory) + 1 > 32760 then
    RaiseException('USER_PATH_TOO_LONG');
  OriginallyMissing := 0;
  if not Exists then OriginallyMissing := 1;
  { Write ownership first so a failed installation can still remove its entry. }
  if not RegWriteStringValue(HKCU, OwnerKey, 'AddedUserPath', Directory) or
    not RegWriteDWordValue(HKCU, OwnerKey, 'UserPathOriginallyMissing', OriginallyMissing) then
    RaiseException('USER_PATH_OWNERSHIP_FAILED');
  if Value = '' then Value := Directory
  else Value := Value + ';' + Directory;
  if not WritePath(Value, ValueType) then
    RaiseException('USER_PATH_WRITE_FAILED');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then UpdateUserPath;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then RemoveOwnedPath;
end;
