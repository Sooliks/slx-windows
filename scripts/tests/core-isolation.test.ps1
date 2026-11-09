param([string]$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')))
$ErrorActionPreference = 'Stop'
# Real pinned Xray, production front/core link and routing. Only loopback fixture transports;
# no TUN, WFP, live subscription, service installation or physical-network mutation.
if ([Environment]::Version.Major -lt 10) { throw 'Run in PowerShell on .NET 10 after the Release build.' }
$executable = Join-Path $Root '.runtime/xray.exe'
$manifest = Get-Content (Join-Path $Root 'runtime/runtime-manifest.json') -Raw | ConvertFrom-Json
$expected = @($manifest.files | Where-Object name -ceq 'xray.exe')
if ($expected.Count -ne 1 -or (Get-FileHash $executable).Hash.ToLowerInvariant() -cne $expected[0].sha256) { throw 'Pinned core mismatch' }
Add-Type -Path (Join-Path $Root 'src/Slx.Client.Core/bin/Release/net10.0-windows/Slx.Client.Core.dll')
Add-Type -Path (Join-Path $Root 'src/Slx.Client.Windows/bin/Release/net10.0-windows/Slx.Client.Windows.dll')
function New-Port {
 $socket = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
 try { $socket.Start(); return $socket.LocalEndpoint.Port } finally { $socket.Stop() }
}
function Await($Task) {
 if (-not $Task.Wait(5000)) { throw ('Loopback operation exceeded 5 seconds: ' + $script:stage) }
 return $Task.GetAwaiter().GetResult()
}
function Start-Core($Config, [int]$Port) {
 $info = [Diagnostics.ProcessStartInfo]::new($executable)
 $info.UseShellExecute=$false; $info.CreateNoWindow=$true; $info.RedirectStandardInput=$true
 $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
 $info.WorkingDirectory=Split-Path $executable
 foreach ($arg in @('run','-config','stdin:','-format','json')) { $info.ArgumentList.Add($arg) }
 $info.Environment.Clear(); $info.Environment['PATH']=[Environment]::SystemDirectory
 $info.Environment['SYSTEMROOT']=[Environment]::GetFolderPath([Environment+SpecialFolder]::Windows)
 $process=[Diagnostics.Process]::new(); $process.StartInfo=$info
 try {
  if (-not $process.Start()) { throw 'Fixture core failed to start' }
  $process | Add-Member -NotePropertyName FixtureOutput -NotePropertyValue $process.StandardOutput.ReadToEndAsync()
  $process | Add-Member -NotePropertyName FixtureErrors -NotePropertyValue $process.StandardError.ReadToEndAsync()
  $process.StandardInput.Write(($Config | ConvertTo-Json -Depth 35 -Compress)); $process.StandardInput.Close()
  for ($i=0; $i -lt 50; $i++) {
   if ($process.HasExited) { throw 'Fixture core exited before readiness' }
   $socket=[Net.Sockets.TcpClient]::new()
   try { $socket.Connect('127.0.0.1',$Port); return $process } catch { Start-Sleep -Milliseconds 40 } finally { $socket.Dispose() }
  }
  throw 'Fixture core listener did not start'
 } catch { Stop-Core $process; throw }
}
function Stop-Core($Process) {
 if ($null -eq $Process) { return }
 try { if (-not $Process.HasExited) { $Process.Kill(); [void]$Process.WaitForExit(5000) } } finally { $Process.Dispose() }
}
function Open-Tcp([int]$Port, [bool]$Proxy) {
 $socket=[Net.Sockets.TcpClient]::new(); $socket.Connect('127.0.0.1',$Port)
 if ($Proxy) {
  $auth=[Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes('fixtureUser00000:fixturePassword0'))
  [byte[]]$request=[Text.Encoding]::ASCII.GetBytes("CONNECT 198.51.100.17:443 HTTP/1.1`r`nHost: 198.51.100.17:443`r`nProxy-Authorization: Basic $auth`r`n`r`n")
  $stream=$socket.GetStream(); $stream.ReadTimeout=5000; $stream.Write($request,0,$request.Length)
  $header=''; while (-not $header.EndsWith("`r`n`r`n") -and $header.Length -lt 4096) {
   $one=$stream.ReadByte(); if ($one -lt 0) { throw 'Fixture CONNECT ended early' }; $header += [char]$one
  }
  if (-not $header.StartsWith('HTTP/1.1 200 ')) { throw 'Fixture CONNECT rejected' }
 }
 $socket.GetStream().WriteByte(0)
 return $socket
}
function Accept-Peer($Pending) {
 $peer=Await $Pending; $stream=$peer.GetStream(); $stream.ReadTimeout=5000
 if ($stream.ReadByte() -ne 0) { throw 'Fixture stream prelude invalid' }; return $peer
}
function Echo-Tcp($Client, $Peer, [int]$Sequence) {
 [byte[]]$payload=@(0x53,0x4c,0x58,[byte]$Sequence); $buffer=[byte[]]::new(4)
 $clientStream=$Client.GetStream(); $serverStream=$Peer.GetStream()
 $clientStream.ReadTimeout=5000; $serverStream.ReadTimeout=5000
 $clientStream.Write($payload,0,4); $serverStream.ReadExactly($buffer,0,4); $serverStream.Write($buffer,0,4)
 $clientStream.ReadExactly($buffer,0,4)
 if ([Convert]::ToHexString($payload) -cne [Convert]::ToHexString($buffer)) { throw 'TCP echo mismatch' }
}
function Echo-Udp($Client, [int]$Port, [int]$Sequence) {
 [byte[]]$payload=@(0x55,0x44,0x50,[byte]$Sequence)
 $incoming=$udpServer.ReceiveAsync(); $response=$Client.ReceiveAsync()
 [void]$Client.Send($payload,4,[Net.IPEndPoint]::new([Net.IPAddress]::Loopback,$Port))
 $packet=Await $incoming
 # The negative marker may finish its in-flight dial after the core comes back; it is not this new flow.
 if ($packet.Buffer.Length -eq 4 -and $packet.Buffer[3] -eq 99) { $packet=Await ($udpServer.ReceiveAsync()) }
 [void]$udpServer.Send($packet.Buffer,$packet.Buffer.Length,$packet.RemoteEndPoint)
 $reply=Await $response
 if ([Convert]::ToHexString($payload) -cne [Convert]::ToHexString($reply.Buffer)) { throw 'UDP echo mismatch' }
 return $packet.RemoteEndPoint.ToString()
}
$front=$null; $core=$null; $direct=$null; $directPeer=$null; $selected=$null; $selectedPeer=$null
$tcpServer=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0); $tcpServer.Start()
$stubPort=$tcpServer.LocalEndpoint.Port
$udpServer=[Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Loopback,$stubPort))
$directUdp=[Net.Sockets.UdpClient]::new(0); $selectedUdp=[Net.Sockets.UdpClient]::new(0)
try {
 $linkPort=New-Port; $probePort=New-Port; $tunPort=New-Port; $selectedPort=New-Port
 $profile=[Slx.Client.Core.SubscriptionParser]::Parse('hysteria2://fixture-only@vpn.example.com:8444/?sni=vpn.example.com#Fixture').Profiles[0]
 $app=[Slx.Client.Core.AppRule]::new([guid]::NewGuid(),'Fixture','C:\Fixture\app.exe',$true)
 $plan=[Slx.Client.Core.ConnectPlan]::new($profile,[Slx.Client.Core.RoutingMode]::SelectedApplications,[Slx.Client.Core.AppRule[]]@($app))
 $link=[Slx.Client.Core.XrayCoreLink]::new($linkPort,'fixtureUser00000','fixturePassword0',$executable)
 $frontConfig=[Slx.Client.Core.XrayConfigBuilder]::Build($plan,$probePort,'fixtureUser00000','fixturePassword0','auto',$link) | ConvertFrom-Json -AsHashtable
 $coreConfig=[Slx.Client.Core.XrayCoreConfig]::Build($profile,$link) | ConvertFrom-Json -AsHashtable
 $frontConfig.inbounds[0]=@{tag='tun';listen='127.0.0.1';port=$tunPort;protocol='dokodemo-door';settings=@{address='198.51.100.17';port=443;network='tcp,udp'}}
 $frontConfig.inbounds+=@{tag='fixture-selected';listen='127.0.0.1';port=$selectedPort;protocol='dokodemo-door';settings=@{address='198.51.100.17';port=443;network='udp'}}
 # Fixture selects UDP by ingress; process ownership is deliberately outside this loopback contract.
 $rules=$frontConfig.routing.rules
 $frontConfig.routing.rules=@($rules[0],@{type='field';inboundTag=@('fixture-selected');outboundTag='vpn'})+$rules[1..($rules.Count-1)]
 $frontConfig.outbounds[0]=@{tag='direct';protocol='freedom';settings=@{redirect=('127.0.0.1:'+$stubPort)}}
 # This replaces the remote transport only. Pinned Xray blocks private VLESS->freedom destinations by
 # default; allow just this loopback stub, never add such an exception to production configuration.
 $coreConfig.outbounds[0]=@{tag='vpn';protocol='freedom';settings=@{redirect=('127.0.0.1:'+$stubPort);
  finalRules=@(@{action='allow';ip=@('127.0.0.1/32');port=[string]$stubPort})}}
 $core=Start-Core $coreConfig $linkPort; $front=Start-Core $frontConfig $probePort; $frontId=$front.Id
 $control=[Slx.Client.Windows.XrayRouteControl]::new($probePort,'fixtureUser00000','fixturePassword0')
 $script:stage='front revision'; $revision=Await ($control.ReadRevisionAsync([Threading.CancellationToken]::None))
 $script:stage='direct TCP'
 $accept=$tcpServer.AcceptTcpClientAsync(); $direct=Open-Tcp $tunPort $false; $directPeer=Accept-Peer $accept
 Echo-Tcp $direct $directPeer 1
 $script:stage='selected TCP'; $accept=$tcpServer.AcceptTcpClientAsync(); $selected=Open-Tcp $probePort $true; $selectedPeer=Accept-Peer $accept
 Echo-Tcp $selected $selectedPeer 2
 $script:stage='direct UDP'; $udpSource=Echo-Udp $directUdp $tunPort 1
 $script:stage='selected UDP'; $null=Echo-Udp $selectedUdp $selectedPort 2
 Stop-Core $core; $core=$null; $selected.Dispose(); $selectedPeer.Dispose(); $selected=$null; $selectedPeer=$null
 $script:stage='core-off direct traffic'
 for ($i=3; $i -lt 23; $i++) {
  Echo-Tcp $direct $directPeer $i
  if ((Echo-Udp $directUdp $tunPort $i) -cne $udpSource) { throw 'Direct UDP flow was recreated' }
 }
 if ((Await ($control.ReadRevisionAsync([Threading.CancellationToken]::None))) -cne $revision) { throw 'Front revision changed with core outage' }
 $pending=$udpServer.ReceiveAsync(); [byte[]]$marker=@(0x55,0x44,0x50,99)
 [void]$selectedUdp.Send($marker,4,[Net.IPEndPoint]::new([Net.IPAddress]::Loopback,$selectedPort))
 if ($pending.Wait(500)) { throw 'Selected traffic escaped unavailable core' }
 # New stub socket cancels the pending negative observation before restoration.
 $udpServer.Dispose(); $udpServer=[Net.Sockets.UdpClient]::new([Net.IPEndPoint]::new([Net.IPAddress]::Loopback,$stubPort))
 $selectedUdp.Dispose(); $selectedUdp=[Net.Sockets.UdpClient]::new(0)
 $core=Start-Core $coreConfig $linkPort
 $script:stage='restored selected traffic'
 $accept=$tcpServer.AcceptTcpClientAsync(); $selected=Open-Tcp $probePort $true; $selectedPeer=Accept-Peer $accept
 Echo-Tcp $selected $selectedPeer 30; $null=Echo-Udp $selectedUdp $selectedPort 31
 Echo-Tcp $direct $directPeer 32
 if ((Echo-Udp $directUdp $tunPort 33) -cne $udpSource) { throw 'Direct UDP flow changed after restoration' }
 if ($front.HasExited -or $front.Id -ne $frontId) { throw 'Shared front was replaced' }
 Write-Output 'PASS: actual pinned Xray front/core, persistent direct TCP/UDP during 20 core-off exchanges, selected TCP/UDP recovery, no selected UDP fallback, unchanged authenticated front revision.'
 Write-Output 'LIMIT: loopback transport fixture, not installed TUN/WFP/process ownership, physical switching, TLS or remote transport acceptance.'
} catch {
 foreach ($running in @($core,$front)) {
  if ($null -ne $running) {
   if (-not $running.HasExited) { $running.Kill(); [void]$running.WaitForExit(5000) }
   Write-Output $running.FixtureOutput.GetAwaiter().GetResult()
   Write-Output $running.FixtureErrors.GetAwaiter().GetResult()
  }
 }
 throw
} finally {
 Stop-Core $core; Stop-Core $front
 foreach ($socket in @($direct,$directPeer,$selected,$selectedPeer,$udpServer,$directUdp,$selectedUdp)) { if ($null -ne $socket) { $socket.Dispose() } }
 $tcpServer.Stop()
}
