// Sanitize the elevated child's loader environment before any managed or PowerShell process starts.
function DeleteEnvironment(Name: String; Value: LongWord): Boolean;
 external 'SetEnvironmentVariableW@kernel32.dll stdcall';
function SetEnvironment(Name, Value: String): Boolean;
 external 'SetEnvironmentVariableW@kernel32.dll stdcall';
procedure SecureManagedEnvironment();
var Names, Name, Prefix: String; Separator, I: Integer;
begin
 Names := 'DOTNET_STARTUP_HOOKS|DOTNET_ADDITIONAL_DEPS|DOTNET_SHARED_STORE|DOTNET_ROOT|DOTNET_ROOT_X64|DOTNET_ROOT_X86|DOTNET_ROOT(x86)|' +
  'DOTNET_HOST_PATH|DOTNET_SERVICING|DOTNET_BUNDLE_EXTRACT_BASE_DIR|' +
  'CORECLR_PROFILER|CORECLR_PROFILER_PATH|CORECLR_PROFILER_PATH_32|CORECLR_PROFILER_PATH_64|CORECLR_PROFILER_PATH_ARM64|' +
  'COR_PROFILER|COR_PROFILER_PATH|COR_PROFILER_PATH_32|COR_PROFILER_PATH_64|COR_PROFILER_PATH_ARM64|' +
  'DOTNET_PROFILER|DOTNET_PROFILER_PATH|DOTNET_PROFILER_PATH_32|DOTNET_PROFILER_PATH_64|DOTNET_PROFILER_PATH_ARM64|';
 while Length(Names) > 0 do begin
  Separator := Pos('|', Names); Name := Copy(Names, 1, Separator - 1); Delete(Names, 1, Separator);
  DeleteEnvironment(Name, 0);
 end;
 for I := 1 to 2 do begin
  if I = 1 then Prefix := 'DOTNET_' else Prefix := 'COMPlus_';
  DeleteEnvironment(Prefix + 'GCPath', 0); DeleteEnvironment(Prefix + 'GCName', 0);
  DeleteEnvironment(Prefix + 'JitName', 0); DeleteEnvironment(Prefix + 'AltJitName', 0);
 end;
 if not SetEnvironment('DOTNET_EnableDiagnostics', '0') or not SetEnvironment('COMPlus_EnableDiagnostics', '0')
  or not SetEnvironment('CORECLR_ENABLE_PROFILING', '0') or not SetEnvironment('COR_ENABLE_PROFILING', '0')
  or not SetEnvironment('DOTNET_ENABLE_PROFILING', '0')
  or not SetEnvironment('PATH', ExpandConstant('{sys};{win}'))
  or not SetEnvironment('SystemRoot', ExpandConstant('{win}')) or not SetEnvironment('windir', ExpandConstant('{win}'))
  or not SetEnvironment('PSModulePath', ExpandConstant('{sys}\WindowsPowerShell\v1.0\Modules')) then
  RaiseException('Could not prepare the installation environment.');
end;
