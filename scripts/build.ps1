param([string]$InnoCompiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
function Invoke-Dotnet([string[]]$Arguments) {
 & dotnet @Arguments
 if ($LASTEXITCODE -ne 0) { throw ('dotnet failed: ' + ($Arguments -join ' ')) }
}
& (Join-Path $PSScriptRoot 'fetch-runtime.ps1')
& (Join-Path $PSScriptRoot 'create-icon.ps1')
$artifacts = Join-Path $root 'artifacts'
$bundle = Join-Path $artifacts 'bundle'
if (Test-Path -LiteralPath $bundle) {
 $resolved = [IO.Path]::GetFullPath($bundle)
 if ($resolved -ne (Join-Path $root 'artifacts\bundle')) { throw 'Unsafe bundle cleanup path' }
 Remove-Item -LiteralPath $bundle -Recurse -Force
}
[IO.Directory]::CreateDirectory($bundle) | Out-Null
foreach ($name in @('Desktop','Service')) {
 $project = 'src/Slx.Client.' + $name + '/Slx.Client.' + $name + '.csproj'
 $publish = Join-Path $artifacts ('publish-' + $name.ToLowerInvariant())
 if (Test-Path -LiteralPath $publish) {
  $publishResolved = [IO.Path]::GetFullPath($publish)
  if ($publishResolved -notin @((Join-Path $root 'artifacts/publish-desktop'),(Join-Path $root 'artifacts/publish-service'))) { throw 'Unsafe publish cleanup path' }
  Remove-Item -LiteralPath $publishResolved -Recurse -Force
 }
 Invoke-Dotnet -Arguments @('publish',$project,'-c','Release','-r','win-x64','--self-contained','true','-p:RuntimeFrameworkVersion=10.0.13','-p:RestoreLockedMode=true','-p:PublishReadyToRun=false','-p:DebugType=None','-p:DebugSymbols=false','-o',$publish)
 foreach ($file in Get-ChildItem -LiteralPath $publish -File -Recurse) {
  $relative = [IO.Path]::GetRelativePath($publish,$file.FullName)
  $destinationRoot = if ($name -eq 'Service') { Join-Path $bundle 'service' } else { $bundle }
  $target = Join-Path $destinationRoot $relative
  [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
  if (Test-Path -LiteralPath $target) {
   if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw ('Publish collision: ' + $relative) }
  } else { Copy-Item -LiteralPath $file.FullName -Destination $target }
 }
}
[IO.Directory]::CreateDirectory((Join-Path $bundle 'service/runtime')) | Out-Null
foreach ($name in @('xray.exe','wintun.dll','runtime-manifest.json')) {
 Copy-Item -LiteralPath (Join-Path $root ('.runtime/' + $name)) -Destination (Join-Path $bundle ('service/runtime/' + $name))
}
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $bundle -Recurse
Copy-Item -LiteralPath (Join-Path $root 'licenses') -Destination $bundle -Recurse
foreach ($name in @('LICENSE','THIRD_PARTY_NOTICES.md','SECURITY.md','README.md')) { Copy-Item -LiteralPath (Join-Path $root $name) -Destination $bundle }
[IO.Directory]::CreateDirectory((Join-Path $bundle 'scripts')) | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'manage-service.ps1') -Destination (Join-Path $bundle 'scripts/manage-service.ps1')
if (-not (Test-Path -LiteralPath $InnoCompiler)) { throw 'Inno Setup 6 not found; supply -InnoCompiler' }
& $InnoCompiler /Qp (Join-Path $root 'installer/slx.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compile failed' }
$release = Join-Path $artifacts 'release'
$items = Get-ChildItem -LiteralPath $release -File | Where-Object { $_.Extension -in '.exe','.zip','.json' }
$items | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name } | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding utf8NoBOM
Write-Output 'Unsigned alpha installer built. Installation/system acceptance is a separate step.'
