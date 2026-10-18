param([Parameter(Mandatory=$true)][ValidateSet('Install','Remove','Stop')][string]$Action)
$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator required' }
$installRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$programFilesRoot = [IO.Path]::GetFullPath($env:ProgramFiles)
if (-not $installRoot.StartsWith($programFilesRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Expected Program Files installation' }
$binary = Join-Path $installRoot 'service/SLX.Service.exe'
$expected = '"' + $binary + '"'
$existing = Get-CimInstance Win32_Service -Filter "Name='SLXVPN'"
if ($existing -and $existing.PathName -ne $expected) { throw 'An unrelated SLXVPN service exists' }
if ($existing) {
 $service = Get-Service -Name SLXVPN
 if ($service.Status -ne 'Stopped') {
  Stop-Service -Name SLXVPN
  $service.WaitForStatus('Stopped',[TimeSpan]::FromSeconds(25))
 }
}
if ($Action -eq 'Stop') { exit 0 }
if ($Action -eq 'Remove') {
 if (Test-Path -LiteralPath $binary) { & $binary --release-protection; if ($LASTEXITCODE -ne 0) { throw 'Could not release SLX traffic protection' } }
 if ($existing) { & "$env:SystemRoot\System32\sc.exe" delete SLXVPN | Out-Null; if ($LASTEXITCODE -ne 0) { throw 'Service removal failed' } }
 exit 0
}
if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) { throw 'Missing service executable' }
if (-not $existing) {
 New-Service -Name SLXVPN -BinaryPathName $expected -DisplayName 'SLX VPN Network Service' -StartupType Automatic -Description 'Owns SLX VPN tunnel and authenticated local control.' | Out-Null
} else { Set-Service -Name SLXVPN -StartupType Automatic }
& "$env:SystemRoot\System32\sc.exe" failure SLXVPN reset= 86400 actions= restart/3000/restart/10000/restart/30000 | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Service recovery configuration failed' }
Start-Service -Name SLXVPN
(Get-Service -Name SLXVPN).WaitForStatus('Running',[TimeSpan]::FromSeconds(20))
