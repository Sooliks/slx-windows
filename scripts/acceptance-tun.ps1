param([switch]$Run)
$ErrorActionPreference='Stop'
# Opt-in native classifier test. No service install, real profiles, DNS change or default-route change.
# Fixture only: autoOutboundsInterface='' keeps redirected localhost UDP unbound; production retains 'auto'.
if (-not $Run) {
 Write-Output 'Review: temporary SLX Acceptance TUN, 198.18.0.2/32 host route plus 172.30.254.0/30 connected subnet, local TCP/UDP sinks and two probe EXEs.'
 Write-Output 'Build scripts/build-routing-probes.ps1; run this script with -Run in administrator PowerShell 7.'
 return
}
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Administrator PowerShell 7 required' }
if (-not ([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
 throw 'Administrator permission required for the isolated native TUN test'
}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'acceptance-native.ps1')
$core=Join-Path $root '.runtime/xray.exe'
$driver=Join-Path $root '.runtime/wintun.dll'
$hashes=@{ 'xray.exe'='0d0fc0ea2b05641acb78c01fc36ad694e7b029861b2d5eb93da0e3e9fda9a98f';'wintun.dll'='e5da8447dc2c320edc0fc52fa01885c103de8c118481f683643cacc3220dafce' }
foreach($name in $hashes.Keys) {
 if((Get-FileHash -LiteralPath (Join-Path $root ".runtime/$name") -Algorithm SHA256).Hash -ne $hashes[$name]) {throw 'Pinned runtime mismatch'}
}
if ((Get-AuthenticodeSignature -LiteralPath $driver).Status -ne 'Valid') { throw 'Vendor Wintun signature invalid' }
if (Get-NetAdapter -Name 'SLX Acceptance' -IncludeHidden -ErrorAction SilentlyContinue) { throw 'Acceptance adapter already exists' }
if (Get-NetRoute -DestinationPrefix '198.18.0.2/32' -ErrorAction SilentlyContinue) { throw 'Reserved test route already exists' }
if (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object IPAddress -in @('172.30.254.0','172.30.254.1','172.30.254.2','172.30.254.3')) {throw 'Acceptance subnet address already exists'}
if (Get-NetRoute -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {Test-AcceptanceSubnetOverlap $_.DestinationPrefix}) {throw 'Acceptance subnet overlaps an existing route'}
$before=Get-AcceptanceDefaultRoutes
$workLeaf='SLX-Acceptance-'+[guid]::NewGuid().ToString('N')
$programFiles=[Environment]::GetFolderPath('ProgramFiles')
$work=Join-Path $programFiles $workLeaf
$acl=[Security.AccessControl.DirectorySecurity]::new()
$acl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))
$acl.SetAccessRuleProtection($true,$false)
foreach($sid in @('S-1-5-18','S-1-5-32-544')) {
 $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
}
$tcpA=$null;$tcpB=$null;$udpA=$null;$udpB=$null;$process=$null;$job=$null;$adapter=$null;$stderr=$null
$script:SlxAcceptancePhase='PREPARE'
$script:SlxAcceptanceProbes=@{}
$result=@{passed=$false;scope='real IPv4 TUN TCP/UDP process classifier to localhost synthetic sinks';selected=$null;other=$null;cleanupErrors=@();realVpnVerified=$false;ipv6Verified=$false;killSwitchVerified=$false}
$failed=$false
try {
 [IO.FileSystemAclExtensions]::Create([IO.DirectoryInfo]::new($work),$acl)
 Copy-Item -LiteralPath $core,$driver -Destination $work
 foreach($name in $hashes.Keys) {
  if((Get-FileHash -LiteralPath (Join-Path $work $name) -Algorithm SHA256).Hash -ne $hashes[$name]) {throw 'Protected runtime copy hash mismatch'}
 }
 foreach($role in @('A','B')) {
  $source=Join-Path $root "artifacts/acceptance/probe-$role"
  $target=Join-Path $work "probe-$role"
  [IO.Directory]::CreateDirectory($target) | Out-Null
  foreach($extension in @('exe','dll','deps.json','runtimeconfig.json')) {
   $name="SlxRouteProbe$role.$extension"
   $sourceFile=Join-Path $source $name
   $expected=(Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
   Copy-Item -LiteralPath $sourceFile -Destination (Join-Path $target $name)
   if((Get-FileHash -LiteralPath (Join-Path $target $name) -Algorithm SHA256).Hash -ne $expected) {throw 'Protected probe copy mismatch'}
  }
 }
 $tcpA=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
 $tcpB=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
 $tcpA.Start();$tcpB.Start()
 $udpA=[Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Loopback,$tcpA.LocalEndpoint.Port))
 $udpB=[Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Loopback,$tcpB.LocalEndpoint.Port))
 $appA=Join-Path $work 'probe-A/SlxRouteProbeA.exe'
 $appB=Join-Path $work 'probe-B/SlxRouteProbeB.exe'
 $config=@{
  log=@{access='none';error='';loglevel='debug'}
  inbounds=@(@{tag='tun';protocol='tun';settings=@{name='SLX Acceptance';desc='SLX isolated acceptance';mtu=1500;gateway=@('172.30.254.1/30');autoSystemRoutingTable=@('198.18.0.2/32');autoOutboundsInterface='' }})
  outbounds=@(
   @{tag='selected-synthetic';protocol='freedom';settings=@{redirect=("127.0.0.1:"+$tcpA.LocalEndpoint.Port)}},
   @{tag='other-synthetic';protocol='freedom';settings=@{redirect=("127.0.0.1:"+$tcpB.LocalEndpoint.Port)}},
   @{tag='background-blocked';protocol='blackhole';settings=@{}})
  routing=@{domainStrategy='AsIs';rules=@(
   @{type='field';ip=@('198.18.0.2');port='45467';process=@($appA.Replace('\','/'));outboundTag='selected-synthetic'},
   @{type='field';ip=@('198.18.0.2');port='45467';network='tcp,udp';outboundTag='other-synthetic'},
   @{type='field';network='tcp,udp';outboundTag='background-blocked'})}
 } | ConvertTo-Json -Depth 12 -Compress
 $start=[Diagnostics.ProcessStartInfo]::new((Join-Path $work 'xray.exe'))
 foreach($argument in @('run','-config','stdin:','-format','json')) {$start.ArgumentList.Add($argument)}
 $start.WorkingDirectory=$work;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
 $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 $start.Environment.Clear();$start.Environment['PATH']=[Environment]::SystemDirectory
 $start.Environment['SYSTEMROOT']=[Environment]::GetFolderPath('Windows')
 $script:SlxAcceptancePhase='CORE_START'
 $result.syntheticCore=@{version='26.9.9';arguments=@('run','-config','stdin:','-format','json');processRule=$appA.Replace($work,'<acceptance>');destination='198.18.0.2:45467'}
 $process=[Diagnostics.Process]::Start($start)
 $job=[SlxAcceptanceJob]::new($process)
 $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
 $process.StandardInput.Write($config);$process.StandardInput.Close()
 $script:SlxAcceptancePhase='TUN_ROUTE_READY'
 $ownedRoute=$null;$ownedAddress=$null
 for($attempt=0;$attempt -lt 60;$attempt++) {
  if($process.HasExited) {throw 'Verified core exited before TUN readiness'}
  $adapter=Get-NetAdapter -Name 'SLX Acceptance' -IncludeHidden -ErrorAction SilentlyContinue
  if($adapter -and $adapter.Status -eq 'Up') {
   $ownedRoute=Get-NetRoute -InterfaceIndex $adapter.ifIndex -DestinationPrefix '198.18.0.2/32' -ErrorAction SilentlyContinue
   $ownedAddress=Get-NetIPAddress -InterfaceIndex $adapter.ifIndex -IPAddress '172.30.254.1' -ErrorAction SilentlyContinue
   if($ownedRoute -and $ownedAddress.AddressState -eq 'Preferred'){break}
  }
  Start-Sleep -Milliseconds 250
 }
 if($process.HasExited -or -not $adapter -or -not $ownedRoute -or $ownedAddress.AddressState -ne 'Preferred') {throw 'TUN route/address readiness failed'}
 $result.readiness=@{route=$ownedRoute.DestinationPrefix;address=$ownedAddress.IPAddress;addressState=[string]$ownedAddress.AddressState}
 $result.selected=Invoke-AcceptanceProbe $appA 'selected' $tcpA $udpA $tcpB $udpB
 $result.other=Invoke-AcceptanceProbe $appB 'other' $tcpA $udpA $tcpB $udpB
 $script:SlxAcceptancePhase='CLASSIFIER_ASSERT'
 if(-not $result.selected.passed -or -not $result.other.passed) {throw 'TUN process classification mismatch'}
 $result.passed=$true
} catch {
 $failed=$true
 $result.failurePhase=$script:SlxAcceptancePhase
 $result.failureMessage=$_.Exception.Message.Replace($work,'<acceptance>').Replace($root,'<workspace>')
 $result.code='NATIVE_ACCEPTANCE_FAILED'
} finally {
 if($job) {$job.Dispose()}
 if($process) {
  if(-not $process.HasExited) {$process.Kill($true);if(-not $process.WaitForExit(5000)) {$result.cleanupErrors+='CORE_NOT_EXITED'}}
  foreach($entry in @(@{name='syntheticCoreStdout';task=$stdout},@{name='syntheticCoreStderr';task=$stderr})) {
   if($entry.task) {try {if($entry.task.Wait(1000)) {$tail=$entry.task.GetAwaiter().GetResult();if($tail.Length -gt 8192){$tail=$tail.Substring($tail.Length-8192)};$result[$entry.name]=$tail.Replace($work,'<acceptance>').Replace($root,'<workspace>')}} catch {}}
  }
  $process.Dispose()
 }
 if($tcpA){$tcpA.Stop()};if($tcpB){$tcpB.Stop()}
 if($udpA){$udpA.Dispose()};if($udpB){$udpB.Dispose()}
 if(-not $adapter -and $process){$adapter=Get-NetAdapter -Name 'SLX Acceptance' -IncludeHidden -ErrorAction SilentlyContinue}
 if($adapter) {
  Get-NetRoute -InterfaceIndex $adapter.ifIndex -DestinationPrefix '198.18.0.2/32' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue
 }
 for($attempt=0;$attempt -lt 20;$attempt++) {
  if(-not(Get-NetAdapter -Name 'SLX Acceptance' -IncludeHidden -ErrorAction SilentlyContinue)){break}
  Start-Sleep -Milliseconds 250
 }
 if(Get-NetAdapter -Name 'SLX Acceptance' -IncludeHidden -ErrorAction SilentlyContinue){$result.cleanupErrors+='ADAPTER_REMAINS'}
 if(Get-NetRoute -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {Test-AcceptanceSubnetOverlap $_.DestinationPrefix}){$result.cleanupErrors+='SUBNET_REMAINS'}
 if(Get-NetIPAddress -IPAddress '172.30.254.1' -ErrorAction SilentlyContinue){$result.cleanupErrors+='ADDRESS_REMAINS'}
 $after=Get-AcceptanceDefaultRoutes
 $result.defaultsUnchanged=($before -join '') -eq ($after -join '')
 if(-not $result.defaultsUnchanged){$result.cleanupErrors+='DEFAULTS_CHANGED'}
 $resolved=[IO.Path]::GetFullPath($work)
 if($resolved -ne (Join-Path $programFiles $workLeaf) -or $workLeaf -notmatch '^SLX-Acceptance-[0-9a-f]{32}$'){throw 'Acceptance cleanup boundary failed'}
 if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
 if($result.cleanupErrors.Count){$failed=$true;$result.passed=$false}
 $result.probeDiagnostics=$script:SlxAcceptanceProbes
 $evidence=Join-Path $root 'artifacts/acceptance/tun-routing-result.json'
 $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $evidence -Encoding utf8NoBOM
}
if($failed){throw ('Native acceptance failed at '+$result.failurePhase+'; inspect artifacts/acceptance/tun-routing-result.json')}
Write-Output ('Native isolated classifier test passed. Evidence: '+$evidence)
