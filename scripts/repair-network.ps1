# Explicit emergency disconnect for an installed SLX client, including older alpha releases.
# Run from an elevated Windows PowerShell after completely exiting the SLX desktop/tray UI.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
 throw 'Open Windows PowerShell as Administrator, then run this script again.'
}
$programFiles = [Environment]::GetEnvironmentVariable('ProgramW6432')
if (-not $programFiles) { $programFiles = [Environment]::GetFolderPath('ProgramFiles') }
$installRoot = Join-Path $programFiles 'SLX VPN'
$binary = Join-Path $installRoot 'service\SLX.Service.exe'
$coreBinary = Join-Path $installRoot 'service\runtime\xray.exe'
$desktopBinary = Join-Path $installRoot 'SLX VPN.exe'
if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) {
 throw 'Installed SLX service not found. Repair the installation with the SLX installer.'
}
foreach ($path in @($installRoot, (Split-Path $binary), $binary)) {
 if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) {
  throw 'SLX installation contains an unexpected reparse point; recovery cancelled.'
 }
}
$existing = Get-CimInstance Win32_Service -Filter "Name='SLXVPN'"
if ($existing -and $existing.PathName -ne ('"' + $binary + '"')) {
 throw 'SLXVPN points to a different executable; recovery cancelled without changing it.'
}
$desktop = @(Get-CimInstance Win32_Process -Filter "Name='SLX VPN.exe'" | Where-Object {
 $_.ExecutablePath -and [string]::Equals($_.ExecutablePath, $desktopBinary, [StringComparison]::OrdinalIgnoreCase)
})
if ($desktop.Count -gt 0) {
 throw 'Completely exit SLX from its tray menu first, then run recovery again. Closing the window can leave it in the tray.'
}
# Pinned Xray derives the adapter GUID from its name; retain identity even if it was renamed.
$slxAdapterId = [Guid]'c5656347-93ca-8b73-7336-a92e8199b47b'
$adapters = @(Get-NetAdapter -IncludeHidden | Where-Object {
 $_.Name -eq 'SLX VPN' -or [Guid]$_.InterfaceGuid -eq $slxAdapterId
})
if (@($adapters | Where-Object { -not $_.PnPDeviceID }).Count -gt 0) {
 throw 'Cannot identify the SLX adapter safely. Restart Windows and run recovery again.'
}
$adapterIds = @($adapters | ForEach-Object { $_.PnPDeviceID })
Write-Output 'Disconnecting SLX and releasing its traffic protection. VPN traffic will return to the ordinary connection.'
try {
 if ($existing) {
  $service = Get-Service -Name SLXVPN
  if ($service.Status -ne 'Stopped') {
   $service.Stop()
   $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
  }
  $service.Refresh()
  if ($service.Status -ne 'Stopped') { throw 'SLX service is not stopped; recovery cancelled.' }
 }
 # Service shutdown closes its child job. A path alone cannot prove ownership of a surviving process.
 $cores = @(Get-CimInstance Win32_Process -Filter "Name='xray.exe'")
 if (@($cores | Where-Object { -not $_.ExecutablePath }).Count -gt 0) {
  throw 'Cannot verify a running core identity. Restart Windows and run recovery again.'
 }
 $remainingCores = @($cores | Where-Object {
  [string]::Equals($_.ExecutablePath, $coreBinary, [StringComparison]::OrdinalIgnoreCase)
 })
 if ($remainingCores.Count -gt 0) {
  throw 'An installed SLX core is still running. Restart Windows and run recovery again; no process was forcibly killed.'
 }
 # Windows may keep a detached adapter record. Wait for actual PnP presence, never delete any adapter.
 $deadline = [DateTime]::UtcNow.AddSeconds(12)
 do {
  $present = @(Get-PnpDevice -Class Net -PresentOnly | Where-Object { $_.InstanceId -in $adapterIds })
  if ($present.Count -eq 0) { break }
  if ([DateTime]::UtcNow -ge $deadline) {
   throw 'The SLX adapter has not finished stopping. Restart Windows and run recovery again.'
  }
  Start-Sleep -Milliseconds 250
 } while ($true)
 if ($existing) {
  $service.Refresh()
  if ($service.Status -ne 'Stopped') { throw 'SLX service restarted during recovery; release cancelled.' }
 }
 # The installed service command clears its protected resume intent and its own WFP provider only.
 & $binary --release-protection
 if ($LASTEXITCODE -ne 0) { throw 'SLX could not release its traffic protection. Do not reset the Windows firewall.' }
 Clear-DnsClientCache
 if ($existing) {
  Start-Service -Name SLXVPN
  (Get-Service -Name SLXVPN).WaitForStatus('Running', [TimeSpan]::FromSeconds(20))
 }
 Write-Output 'SLX protection released and DNS cache cleared. Reopen SLX when ready; no automatic VPN reconnect was requested.'
} catch {
 # Never restart desired-on state after an incomplete release and report a false recovery.
 Write-Error ('SLX recovery was not confirmed: ' + $_.Exception.Message)
 exit 1
}
