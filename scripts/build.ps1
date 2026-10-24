param([string]$InnoCompiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", [switch]$Candidate)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
. (Join-Path $PSScriptRoot 'version.ps1')
$metadata = Get-SlxReleaseMetadata $root
if ($Candidate) {
 $candidateId = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8)
 $candidateRoot = Join-Path $root ('artifacts/candidates/' + $metadata.Version + '/' + $candidateId)
 $metadata.WorkDirectory = Join-Path $candidateRoot 'work'
 $metadata.ReleaseDirectory = Join-Path $candidateRoot 'output'
}
function Invoke-Dotnet([string[]]$Arguments) {
 & dotnet @Arguments
 if ($LASTEXITCODE -ne 0) { throw ('dotnet failed: ' + ($Arguments -join ' ')) }
}
function Remove-OwnedBuildDirectory([string]$Path) {
 $expected = @('bundle','publish-desktop','publish-service') | ForEach-Object { Join-Path $metadata.WorkDirectory $_ }
 $resolved = [IO.Path]::GetFullPath($Path)
 if ($resolved -notin $expected) { throw 'Unsafe build cleanup path' }
 if (Test-Path -LiteralPath $resolved) {
  if ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Build cleanup refuses reparse points' }
  Remove-Item -LiteralPath $resolved -Recurse -Force
 }
}
$commit = (& git rev-parse --verify HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '\A[0-9a-f]{40}\z') { throw 'Cannot identify source commit' }
$dirty = @(& git status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect source worktree' }
& (Join-Path $PSScriptRoot 'fetch-runtime.ps1')
& (Join-Path $PSScriptRoot 'create-icon.ps1')
$bundle = Join-Path $metadata.WorkDirectory 'bundle'
Remove-OwnedBuildDirectory $bundle
[IO.Directory]::CreateDirectory($bundle) | Out-Null
[IO.Directory]::CreateDirectory($metadata.ReleaseDirectory) | Out-Null
$installer = Join-Path $metadata.ReleaseDirectory ($metadata.AssetPrefix + '-win-x64-setup.exe')
if (Test-Path -LiteralPath $installer) { throw 'Versioned installer already exists; do not overwrite release assets' }
foreach ($name in @('Desktop','Service')) {
 $project = 'src/Slx.Client.' + $name + '/Slx.Client.' + $name + '.csproj'
 $publish = Join-Path $metadata.WorkDirectory ('publish-' + $name.ToLowerInvariant())
 Remove-OwnedBuildDirectory $publish
 Invoke-Dotnet -Arguments @('publish',$project,'-c','Release','-r','win-x64','--self-contained','true',
  ('-p:RuntimeFrameworkVersion=' + $metadata.RuntimeVersion),'-p:RestoreLockedMode=true','-p:PublishReadyToRun=false',
  '-p:DebugType=None','-p:DebugSymbols=false','-o',$publish)
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
foreach ($name in @('docs','licenses')) { Copy-Item -LiteralPath (Join-Path $root $name) -Destination $bundle -Recurse }
foreach ($name in @('LICENSE','THIRD_PARTY_NOTICES.md','SECURITY.md','README.md')) { Copy-Item -LiteralPath (Join-Path $root $name) -Destination $bundle }
[IO.Directory]::CreateDirectory((Join-Path $bundle 'scripts')) | Out-Null
foreach ($script in @('manage-service.ps1','repair-network.ps1')) {
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot $script) -Destination (Join-Path $bundle ('scripts/' + $script))
}
if (-not (Test-Path -LiteralPath $InnoCompiler)) { throw 'Inno Setup 6 not found; supply -InnoCompiler' }
& $InnoCompiler /Qp ('/DAppVersion=' + $metadata.Version) ('/DAppFileVersion=' + $metadata.FileVersion) ('/DBundleDir=' + $bundle) ('/DReleaseDir=' + $metadata.ReleaseDirectory) (Join-Path $root 'installer/slx.iss')
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installer)) { throw 'Inno Setup compile failed' }
$afterCommit = (& git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot recheck source commit after build' }
$afterDirty = @(& git status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Cannot recheck source worktree after build' }
$sourceClean = -not $Candidate -and $dirty.Count -eq 0 -and $afterDirty.Count -eq 0 -and $afterCommit -eq $commit
@{
 version=$metadata.Version; fileVersion=$metadata.FileVersion; commit=$commit; sourceClean=$sourceClean
 candidate=[bool]$Candidate; publicationAllowed=([bool]$sourceClean -and -not $Candidate)
 builtAtUtc=[DateTimeOffset]::UtcNow.ToString('O'); dotnetRuntime=$metadata.RuntimeVersion; sdk=$metadata.SdkVersion
 installer=([IO.Path]::GetFileName($installer)); installerSha256=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $metadata.WorkDirectory 'build-info.json') -Encoding utf8NoBOM
Write-Output ('Unsigned installer: ' + $installer)
Write-Output ('Source clean for publication: ' + $sourceClean + '. Run package-source.ps1 after the reviewed clean build.')
