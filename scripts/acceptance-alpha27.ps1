<#
 Real (installed) acceptance for alpha.27. Run from an elevated PowerShell on a PC with SLX VPN alpha.27
 connected in "Applications" mode, with artifacts\acceptance\alpha27\egress-Selected\SlxEgressSelected.exe added
 to the selected applications (and SlxEgressDirect.exe NOT added). Nothing here changes servers or settings;
 the only system change is a temporary outbound UDP firewall rule in -Step UdpBlock, always removed in finally.
#>
param(
 [ValidateSet('Build','Egress','CoreRestart','UdpBlock','All')][string]$Step = 'All',
 [string]$ServerHost,
 [int]$WaitSeconds = 150
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$out = Join-Path $root 'artifacts/acceptance/alpha27'
$results = [ordered]@{}
function Probe([string]$Role) { Join-Path $out "egress-$Role/SlxEgress$Role.exe" }
function Require-Admin {
 $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
 if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this step from an elevated PowerShell' }
}
function Build-Probes {
 $project = Join-Path $root 'tools/Slx.EgressProbe/Slx.EgressProbe.csproj'
 & dotnet restore $project --locked-mode
 if ($LASTEXITCODE -ne 0) { throw "Probe restore failed: $LASTEXITCODE" }
 foreach ($role in 'Selected','Direct') {
  & dotnet build $project -c Release --no-restore "-p:AssemblyName=SlxEgress$role" -o (Join-Path $out "egress-$role")
  if ($LASTEXITCODE -ne 0) { throw "Probe build failed: $LASTEXITCODE" }
 }
 Write-Output "Probes built. Add $(Probe 'Selected') to SLX selected applications, then reconnect."
}
function Read-Address([string]$Role) {
 $text = & (Probe $Role) ip
 Write-Output "$Role exit=$LASTEXITCODE $text" | Out-Host
 return ($text | ConvertFrom-Json)
}
function Test-Egress {
 $direct = Read-Address 'Direct'; $selected = Read-Address 'Selected'
 $passed = $direct.tcp -and $direct.udp -and $selected.tcp -and $selected.udp -and
  $selected.tcp -ne $direct.tcp -and $selected.udp -ne $direct.udp -and $selected.tcp -eq $selected.udp
 $results['Egress (selected TCP+UDP via VPN, unselected via provider)'] = $passed
 return $direct
}
function Find-CorePid {
 # The VPN core is the xray.exe whose loopback listener speaks SOCKS5; the TUN process listens with HTTP only.
 foreach ($process in @(Get-Process xray -ErrorAction SilentlyContinue)) {
  $ports = @(Get-NetTCPConnection -State Listen -OwningProcess $process.Id -ErrorAction SilentlyContinue |
   Where-Object LocalAddress -eq '127.0.0.1' | Select-Object -ExpandProperty LocalPort)
  foreach ($port in $ports) {
   $client = New-Object Net.Sockets.TcpClient
   try {
    $client.Connect('127.0.0.1', $port); $stream = $client.GetStream(); $stream.ReadTimeout = 2000
    $stream.Write([byte[]](5,1,2), 0, 3); $reply = New-Object byte[] 2
    if ($stream.Read($reply, 0, 2) -eq 2 -and $reply[0] -eq 5) { return $process.Id }
   } catch { } finally { $client.Dispose() }
  }
 }
 throw 'VPN core process not found (is SLX alpha.27 connected?)'
}
function Start-Hold([string]$Role, [int]$Seconds) {
 $log = Join-Path $out "hold-$Role.json"
 if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log }
 $process = Start-Process -FilePath (Probe $Role) -ArgumentList @('hold', "$Seconds") -NoNewWindow -PassThru -RedirectStandardOutput $log
 return @{ Process = $process; Log = $log }
}
function Read-Hold($Hold) {
 $Hold.Process.WaitForExit()
 $value = Get-Content -LiteralPath $Hold.Log -Raw | ConvertFrom-Json
 Write-Output "$(Split-Path $Hold.Log -Leaf): ok=$($value.ok) failed=$($value.failed) longestGapMs=$($value.longestGapMs)" | Out-Host
 return $value
}
function Test-CoreRestart {
 Require-Admin
 $core = Find-CorePid
 $direct = Start-Hold 'Direct' 50; $selected = Start-Hold 'Selected' 50
 Start-Sleep -Seconds 10
 Write-Output "Killing VPN core PID $core at $([DateTimeOffset]::UtcNow.ToString('O'))" | Out-Host
 Stop-Process -Id $core -Force
 $d = Read-Hold $direct; $s = Read-Hold $selected
 $results['Unselected app unaffected while the VPN core is killed and recovers'] = ($d.failed -eq 0 -and $d.longestGapMs -lt 3000)
 $results['Selected app recovers after the core is killed (gap < 30 s)'] = ($s.ok -gt 0 -and $s.longestGapMs -lt 30000)
 $results['A new VPN core process replaced the killed one'] = ((Find-CorePid) -ne $core)
}
function Test-UdpBlock([string]$DirectAddress) {
 Require-Admin
 if (-not $ServerHost) { throw '-ServerHost (the VPN server host or IP of the selected location) is required' }
 $addresses = @([Net.Dns]::GetHostAddresses($ServerHost) | ForEach-Object { $_.IPAddressToString })
 $name = 'SLX alpha27 acceptance - temporary UDP block'
 $direct = Start-Hold 'Direct' $WaitSeconds
 New-NetFirewallRule -DisplayName $name -Direction Outbound -Protocol UDP -RemoteAddress $addresses -Action Block | Out-Null
 $started = [Diagnostics.Stopwatch]::StartNew(); $recovered = $null
 try {
  Write-Output "UDP to $($addresses -join ', ') blocked at $([DateTimeOffset]::UtcNow.ToString('O'))" | Out-Host
  Start-Sleep -Seconds 20
  while ($started.Elapsed.TotalSeconds -lt $WaitSeconds - 10) {
   $now = & (Probe 'Selected') ip | ConvertFrom-Json
   if ($now.tcp -and $now.tcp -ne $DirectAddress) { $recovered = [int]$started.Elapsed.TotalSeconds; break }
   Start-Sleep -Seconds 5
  }
 } finally {
  Remove-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue
  Write-Output 'Temporary UDP block removed.' | Out-Host
 }
 $d = Read-Hold $direct
 Write-Output "Selected app working again after: $recovered s (copy SLX diagnostics now: Transport should be vless-*)" | Out-Host
 $results['Selected app recovers through another transport while UDP is blocked'] = ($null -ne $recovered)
 $results['Unselected app unaffected during the UDP block'] = ($d.failed -eq 0)
}
New-Item -ItemType Directory -Force -Path $out | Out-Null
if ($Step -in 'Build','All') { Build-Probes }
$directAddress = $null
if ($Step -in 'Egress','UdpBlock','All') { $directAddress = (Test-Egress).tcp }
if ($Step -in 'CoreRestart','All') { Test-CoreRestart }
if ($Step -in 'UdpBlock','All') { Test-UdpBlock $directAddress }
$failed = 0
foreach ($entry in $results.GetEnumerator()) {
 Write-Output ("{0}  {1}" -f $(if ($entry.Value) { 'PASS' } else { 'FAIL' }), $entry.Key)
 if (-not $entry.Value) { $failed++ }
}
if ($Step -ne 'Build') { Write-Output "Result: $($results.Count - $failed)/$($results.Count) passed" }
exit $(if ($failed) { 1 } else { 0 })
