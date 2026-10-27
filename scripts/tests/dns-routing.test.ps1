param([string]$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
$ErrorActionPreference = 'Stop'
# Actual pinned core, generated production routing, local UDP only. No TUN/WFP/service or live subscription.
if ([Environment]::Version.Major -lt 10) { throw 'Run this contract in PowerShell on .NET 10 after the Release build.' }
$core = Join-Path $Root '.runtime/xray.exe'
$manifest = Get-Content -LiteralPath (Join-Path $Root 'runtime/runtime-manifest.json') -Raw | ConvertFrom-Json
$reviewed = @($manifest.files | Where-Object name -ceq 'xray.exe')
if ($reviewed.Count -ne 1 -or (Get-FileHash -LiteralPath $core -Algorithm SHA256).Hash.ToLowerInvariant() -cne $reviewed[0].sha256) {
 throw 'Pinned core integrity mismatch'
}
Add-Type -Path (Join-Path $Root 'src/Slx.Client.Core/bin/Release/net10.0-windows/Slx.Client.Core.dll')
function Get-FreeTcpPort {
 $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
 try { $listener.Start(); return $listener.LocalEndpoint.Port } finally { $listener.Stop() }
}
function Invoke-Mode([Slx.Client.Core.RoutingMode]$Mode) {
 $stub = [Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Loopback,0))
 $client = [Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Loopback,0))
 $inbound = [Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Loopback,0))
 $port = $inbound.Client.LocalEndPoint.Port; $inbound.Dispose()
 $probePort = Get-FreeTcpPort
 $profile = [Slx.Client.Core.SubscriptionParser]::Parse('hysteria2://fixture-only@vpn.example.com:8444/?sni=vpn.example.com#Fixture').Profiles[0]
 $app = [Slx.Client.Core.AppRule]::new([guid]::NewGuid(),'Fixture','C:\Apps\Fixture\app.exe',$true)
 $plan = [Slx.Client.Core.ConnectPlan]::new($profile,$Mode,[Slx.Client.Core.AppRule[]]@($app))
 $config = [Slx.Client.Core.XrayConfigBuilder]::Build($plan,$probePort,'probeUser00000000','probePassword0000') | ConvertFrom-Json -AsHashtable
 # Keep all generated routing. Only fixture transports change: public logical resolver is redirected locally,
 # and VPN becomes unavailable blackhole. This proves router/DNS dispatch rather than a real TUN or TLS exchange.
 $config.inbounds[0] = @{tag='tun';listen='127.0.0.1';port=$port;protocol='dokodemo-door';settings=@{address='8.8.4.4';port=53;network='udp'}}
 $config.dns.servers = @('8.8.4.4')
 $config.outbounds[0] = @{tag='direct';protocol='freedom';settings=@{redirect=('127.0.0.1:'+$stub.Client.LocalEndPoint.Port)}}
 $config.outbounds[1] = @{tag='vpn';protocol='blackhole';settings=@{}}
 $start = [Diagnostics.ProcessStartInfo]::new($core)
 $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.RedirectStandardInput=$true
 $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true; $start.WorkingDirectory=Split-Path $core
 foreach($argument in @('run','-config','stdin:','-format','json')) { $start.ArgumentList.Add($argument) }
 $start.Environment.Clear(); $start.Environment['PATH']=[Environment]::SystemDirectory
 $start.Environment['SYSTEMROOT']=[Environment]::GetFolderPath([Environment+SpecialFolder]::Windows)
 $process = [Diagnostics.Process]::new(); $process.StartInfo=$start
 try {
  if (-not $process.Start()) { throw 'Core did not start' }
  $output=$process.StandardOutput.ReadToEndAsync(); $errors=$process.StandardError.ReadToEndAsync()
  $process.StandardInput.Write(($config | ConvertTo-Json -Depth 30 -Compress)); $process.StandardInput.Close()
  $ready=$false
  for($attempt=0;$attempt -lt 40;$attempt++) {
   if($process.HasExited) { throw ('Core rejected local contract, exit='+$process.ExitCode) }
   $socket=[Net.Sockets.TcpClient]::new()
   try { $socket.Connect('127.0.0.1',$probePort); $ready=$true; break } catch { Start-Sleep -Milliseconds 50 } finally { $socket.Dispose() }
  }
  if(-not $ready) { throw 'Core local listener did not become ready' }
  [byte[]]$query=@(0x4a,0x3b,0x01,0x00,0x00,0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x08)+[Text.Encoding]::ASCII.GetBytes('contract')+
   @(0x07)+[Text.Encoding]::ASCII.GetBytes('example')+@(0x03)+[Text.Encoding]::ASCII.GetBytes('com')+@(0x00,0x00,0x01,0x00,0x01)
  $incoming=$stub.ReceiveAsync(); $answer=$client.ReceiveAsync()
  [void]$client.Send($query,$query.Length,[Net.IPEndPoint]::new([Net.IPAddress]::Loopback,$port))
  $hit=[Threading.Tasks.Task]::WaitAny([Threading.Tasks.Task[]]@($incoming,$answer),2500)
  if($Mode -eq [Slx.Client.Core.RoutingMode]::SelectedApplications) {
   if($hit -ne 0) { throw 'Selected shared DNS depended on unavailable VPN' }
   $packet=$incoming.GetAwaiter().GetResult(); [byte[]]$reply=$packet.Buffer.Clone()
   $reply[2]=0x81; $reply[3]=0x80; $reply[6]=0; $reply[7]=1
   $reply+= [byte[]]@(0xc0,0x0c,0x00,0x01,0x00,0x01,0x00,0x00,0x00,0x3c,0x00,0x04,203,0,113,42)
   [void]$stub.Send($reply,$reply.Length,$packet.RemoteEndPoint)
   if(-not $answer.Wait(2500)) { throw 'Selected DNS answer did not return through actual core' }
   $response=$answer.GetAwaiter().GetResult().Buffer
   if($response.Length -lt 12 -or $response[0] -ne 0x4a -or $response[1] -ne 0x3b -or $response[7] -lt 1) { throw 'Selected DNS response invalid' }
  } else {
   if($incoming.IsCompleted) { throw 'Whole-PC DNS escaped the unavailable VPN through direct' }
   if($answer.IsCompleted -and $answer.GetAwaiter().GetResult().Buffer[7] -ne 0) { throw 'Whole-PC DNS claimed an unavailable answer' }
  }
  Write-Output ($Mode.ToString()+': actual pinned-core DNS routing contract passed (loopback only)')
 } finally {
  try { if($process.Id -and -not $process.HasExited) { $process.Kill(); [void]$process.WaitForExit(5000) } } catch {}
  $process.Dispose(); $stub.Dispose(); $client.Dispose()
 }
}
Invoke-Mode ([Slx.Client.Core.RoutingMode]::SelectedApplications)
Invoke-Mode ([Slx.Client.Core.RoutingMode]::WholeComputer)
