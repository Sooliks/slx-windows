# Test-only job ownership. No service install; closing the script also kills its owned Xray child.
if (-not ('SlxAcceptanceJob' -as [type])) {
 Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
public sealed class SlxAcceptanceJob : IDisposable
{
 private IntPtr handle;
 public SlxAcceptanceJob(Process process)
 {
  handle = CreateJobObjectW(IntPtr.Zero, null);
  var limits = new Extended { Basic = new Basic { Flags = 0x2000 } };
  if (handle == IntPtr.Zero || !SetInformationJobObject(handle, 9, ref limits, (uint)Marshal.SizeOf<Extended>())
   || !AssignProcessToJobObject(handle, process.Handle))
  { Dispose(); throw new InvalidOperationException("Acceptance job ownership failed"); }
 }
 public void Dispose() { if (handle != IntPtr.Zero) { CloseHandle(handle); handle = IntPtr.Zero; } }
 [StructLayout(LayoutKind.Sequential)]
 private struct Basic
 {
  public long ProcessTime, JobTime;
  public uint Flags;
  public UIntPtr Minimum, Maximum;
  public uint ActiveProcesses;
  public UIntPtr Affinity;
  public uint Priority, Scheduling;
 }
 [StructLayout(LayoutKind.Sequential)]
 private struct Io { public ulong A, B, C, D, E, F; }
 [StructLayout(LayoutKind.Sequential)]
 private struct Extended { public Basic Basic; public Io Io; public UIntPtr A, B, C, D; }
 [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
 private static extern IntPtr CreateJobObjectW(IntPtr attributes, string name);
 [DllImport("kernel32.dll")]
 [return: MarshalAs(UnmanagedType.Bool)]
 private static extern bool SetInformationJobObject(IntPtr job, int type, ref Extended limits, uint size);
 [DllImport("kernel32.dll")]
 [return: MarshalAs(UnmanagedType.Bool)]
 private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
 [DllImport("kernel32.dll")]
 [return: MarshalAs(UnmanagedType.Bool)]
 private static extern bool CloseHandle(IntPtr handle);
}
'@
}
function Wait-AcceptanceTask($Task) {
 if (-not $Task.Wait(6000)) { throw 'Acceptance local packet timeout' }
 return $Task.GetAwaiter().GetResult()
}
function Receive-AcceptanceAny($First,$Second,$Cancellation,$Probe) {
 $deadline=[Diagnostics.Stopwatch]::StartNew()
 while (-not $First.IsCompleted -and -not $Second.IsCompleted) {
  if ($Probe.HasExited) { $Cancellation.Cancel(); throw 'Acceptance probe exited before packet arrival' }
  if ($deadline.ElapsedMilliseconds -ge 6000) { $Cancellation.Cancel(); throw 'Acceptance local packet timeout' }
  Start-Sleep -Milliseconds 25
 }
 $completed=if($First.IsCompleted){$First}else{$Second}
 $selected=[object]::ReferenceEquals($completed,$First)
 $value=$completed.GetAwaiter().GetResult()
 $Cancellation.Cancel()
 return @{role=$(if($selected){'selected'}else{'other'});value=$value}
}
function Invoke-AcceptanceProbe($Executable,$Role,$TcpA,$UdpA,$TcpB,$UdpB) {
 $script:SlxAcceptancePhase=$Role+'_START'
 $start=[Diagnostics.ProcessStartInfo]::new($Executable)
 $start.ArgumentList.Add($Role)
 $start.UseShellExecute=$false; $start.CreateNoWindow=$true
 $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
 $start.Environment.Clear()
 $start.Environment["PATH"]=[Environment]::SystemDirectory
 $start.Environment["SYSTEMROOT"]=[Environment]::GetFolderPath("Windows")
 $probe=[Diagnostics.Process]::Start($start)
 $probeOut=$probe.StandardOutput.ReadToEndAsync();$probeErr=$probe.StandardError.ReadToEndAsync()
 $tcpBudget=[Threading.CancellationTokenSource]::new()
 $udpBudget=[Threading.CancellationTokenSource]::new()
 try {
  $script:SlxAcceptancePhase=$Role+'_TCP'
  $tcp=Receive-AcceptanceAny ($TcpA.AcceptTcpClientAsync($tcpBudget.Token).AsTask()) ($TcpB.AcceptTcpClientAsync($tcpBudget.Token).AsTask()) $tcpBudget $probe
  $peer=$tcp.value
  try {
   $expected=[Text.Encoding]::ASCII.GetBytes('SLX-TUN-ACCEPTANCE')
   $buffer=[byte[]]::new($expected.Length)
   $offset=0
   while($offset -lt $buffer.Length) {
    $read=Wait-AcceptanceTask ($peer.GetStream().ReadAsync($buffer,$offset,$buffer.Length-$offset))
    if($read -eq 0) {throw 'Synthetic TCP marker truncated'}
    $offset+=$read
   }
   if([Text.Encoding]::ASCII.GetString($buffer) -ne 'SLX-TUN-ACCEPTANCE') {throw 'Unexpected synthetic TCP packet'}
   $response=[Text.Encoding]::ASCII.GetBytes($tcp.role)
   $peer.GetStream().Write($response,0,$response.Length)
  } finally { $peer.Dispose() }
  $script:SlxAcceptancePhase=$Role+'_UDP'
  $packet=Receive-AcceptanceAny ($UdpA.ReceiveAsync($udpBudget.Token).AsTask()) ($UdpB.ReceiveAsync($udpBudget.Token).AsTask()) $udpBudget $probe
  if([Text.Encoding]::ASCII.GetString($packet.value.Buffer) -ne 'SLX-TUN-ACCEPTANCE') {throw 'Unexpected synthetic UDP packet'}
  $response=[Text.Encoding]::ASCII.GetBytes($packet.role)
  $sink=if($packet.role -eq 'selected'){$UdpA}else{$UdpB}
  [void]$sink.Send($response,$response.Length,$packet.value.RemoteEndPoint)
  if (-not $probe.WaitForExit(3000)) { throw 'Acceptance probe did not exit' }
  $result=$probeOut.GetAwaiter().GetResult() | ConvertFrom-Json
  return @{passed=[bool]$result.passed;tcp=$result.tcp;udp=$result.udp;exitCode=$probe.ExitCode}
 } finally {
  $tcpBudget.Cancel(); $udpBudget.Cancel(); $tcpBudget.Dispose(); $udpBudget.Dispose()
  if (-not $probe.HasExited) { $probe.Kill($true); [void]$probe.WaitForExit(2000) }
  $diag=@{exitCode=$(if($probe.HasExited){$probe.ExitCode}else{$null})}
  foreach($entry in @(@{name='stdout';task=$probeOut},@{name='stderr';task=$probeErr})) {
   if($entry.task.Wait(1000)) {try {$text=$entry.task.GetAwaiter().GetResult();if($text.Length -gt 2048){$text=$text.Substring($text.Length-2048)};$diag[$entry.name]=$text} catch {}}
  }
  $script:SlxAcceptanceProbes[$Role]=$diag
  $probe.Dispose()
 }
}
function Test-AcceptanceSubnetOverlap($Prefix) {
 $parts=$Prefix.Split('/')
 $bits=[int]$parts[1]
 if ($bits -eq 0) { return $false }
 $octets=[Net.IPAddress]::Parse($parts[0]).GetAddressBytes()
 $number=[uint64]$octets[0]*16777216+[uint64]$octets[1]*65536+[uint64]$octets[2]*256+[uint64]$octets[3]
 $size=[math]::Pow(2,32-$bits)
 $first=[math]::Floor($number/$size)*$size
 $last=$first+$size-1
 $testFirst=2887712256 # 172.30.254.0
 return $first -le ($testFirst+3) -and $last -ge $testFirst
}
function Get-AcceptanceDefaultRoutes {
 return @(Get-NetRoute -DestinationPrefix '0.0.0.0/0','::/0' -ErrorAction SilentlyContinue |
  Sort-Object DestinationPrefix,InterfaceIndex,NextHop,RouteMetric |
  Select-Object DestinationPrefix,InterfaceIndex,NextHop,RouteMetric | ConvertTo-Json -Compress)
}
