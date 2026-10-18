$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
$dirty = & git status --porcelain
if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Commit the reviewed source first' }
$commit = (& git rev-parse HEAD).Trim()
$directory = Join-Path $root 'artifacts/release'
[IO.Directory]::CreateDirectory($directory) | Out-Null
$archive = Join-Path $directory 'SLX-VPN-0.1.0-alpha.1-source-full.zip'
& git archive --format=zip --prefix=slx-windows/ --output=$archive HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source archive failed' }
# Initial release: every source file is new; changed and full inventories are identical.
Copy-Item -LiteralPath $archive -Destination (Join-Path $directory 'SLX-VPN-0.1.0-alpha.1-source-changed.zip') -Force
$manifest = Get-Content -LiteralPath (Join-Path $root 'runtime/runtime-manifest.json') -Raw | ConvertFrom-Json
$dependencies = @()
foreach ($lock in Get-ChildItem src -Filter packages.lock.json -Recurse) {
 $json = Get-Content -LiteralPath $lock.FullName -Raw | ConvertFrom-Json -AsHashtable
 foreach ($target in $json.dependencies.Values) {
  foreach ($entry in $target.GetEnumerator()) {
   if ($entry.Value.resolved) { $dependencies += ($entry.Key + '@' + $entry.Value.resolved) }
  }
 }
}
$provenance = @{
 version='0.1.0-alpha.1'; status='PARTIAL'; source='https://github.com/Sooliks/slx-windows'; commit=$commit
 sourceArchives='Initial release; changed and full contain the same reviewed tracked files. No deletions.'
 core=$manifest; dotnetRuntime='10.0.13'; sdk='10.0.401'; platform='Windows 11 x64'
 applicationSigned=$false; installerSigned=$false; driverSignature='Vendor signature Valid at build'
 nuget=($dependencies | Sort-Object -Unique)
}
$provenance | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $directory 'provenance.json') -Encoding utf8NoBOM
Get-ChildItem -LiteralPath $directory -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
 Sort-Object Name | ForEach-Object {
  (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
 } | Set-Content -LiteralPath (Join-Path $directory 'SHA256SUMS.txt') -Encoding utf8NoBOM
Write-Output ('Source archives and provenance for ' + $commit)
