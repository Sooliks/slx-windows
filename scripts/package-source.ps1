param([string]$BaseRef)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $root
. (Join-Path $PSScriptRoot 'version.ps1')
$metadata = Get-SlxReleaseMetadata $root
$dirty = & git status --porcelain
if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Commit the reviewed source first' }
$commit = (& git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -notmatch '\A[0-9a-f]{40}\z') { throw 'Cannot identify source commit' }
$build = Get-Content -LiteralPath (Join-Path $metadata.WorkDirectory 'build-info.json') -Raw | ConvertFrom-Json
if ($build.version -ne $metadata.Version -or $build.fileVersion -ne $metadata.FileVersion -or
 $build.commit -ne $commit -or $build.sourceClean -ne $true -or $build.publicationAllowed -ne $true -or $build.candidate -ne $false) { throw 'A clean matching source build is required' }
$directory = $metadata.ReleaseDirectory
$installer = Join-Path $directory ($metadata.AssetPrefix + '-win-x64-setup.exe')
if ((Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant() -ne $build.installerSha256) { throw 'Installer does not match build metadata' }
foreach ($name in @('source-full.zip','source-changed.zip')) {
 if (Test-Path -LiteralPath (Join-Path $directory ($metadata.AssetPrefix + '-' + $name))) { throw 'Versioned source archive already exists; preserve released artifacts' }
}
$baseCommit = $null
if ($BaseRef) {
 $baseCommit = (& git rev-parse --verify --end-of-options ($BaseRef + '^{commit}')).Trim()
 if ($LASTEXITCODE -ne 0 -or $baseCommit -notmatch '\A[0-9a-f]{40}\z') { throw 'Invalid source archive base commit' }
 $changed = @(& git -c core.quotepath=false diff --name-only --no-renames --diff-filter=ACMT $baseCommit HEAD --)
 if ($LASTEXITCODE -ne 0 -or $changed.Count -eq 0) { throw 'No changed source files for the selected base' }
 $deletions = @(& git -c core.quotepath=false diff --name-only --no-renames --diff-filter=D $baseCommit HEAD --)
 if ($LASTEXITCODE -ne 0) { throw 'Deleted source inventory failed' }
} else {
 $tags = @(& git tag --list)
 if ($LASTEXITCODE -ne 0 -or $tags.Count -gt 0) { throw 'For an update, specify -BaseRef for the previous release' }
 $deletions = @()
}
$archive = Join-Path $directory ($metadata.AssetPrefix + '-source-full.zip')
& git archive --format=zip --prefix=slx-windows/ --output=$archive HEAD
if ($LASTEXITCODE -ne 0) { throw 'Source archive failed' }
$changedArchive = Join-Path $directory ($metadata.AssetPrefix + '-source-changed.zip')
if ($baseCommit) {
 & git archive --format=zip --prefix=slx-windows/ --output=$changedArchive HEAD -- @changed
 if ($LASTEXITCODE -ne 0) { throw 'Changed source archive failed' }
} else { Copy-Item -LiteralPath $archive -Destination $changedArchive }
$(if ($deletions.Count) { $deletions } else { 'No deleted tracked source files.' }) |
 Set-Content -LiteralPath (Join-Path $directory 'DELETIONS.txt') -Encoding utf8NoBOM
$manifest = Get-Content -LiteralPath (Join-Path $root 'runtime/runtime-manifest.json') -Raw | ConvertFrom-Json
$dependencies = @()
foreach ($lock in Get-ChildItem src -Filter packages.lock.json -Recurse) {
 $json = Get-Content -LiteralPath $lock.FullName -Raw | ConvertFrom-Json -AsHashtable
 foreach ($target in $json.dependencies.Values) {
  foreach ($entry in $target.GetEnumerator()) { if ($entry.Value.resolved) { $dependencies += ($entry.Key + '@' + $entry.Value.resolved) } }
 }
}
@{
 version=$metadata.Version; fileVersion=$metadata.FileVersion; status='PARTIAL'
 source='https://github.com/Sooliks/slx-windows'; commit=$commit; baseCommit=$baseCommit
 sourceArchives=$(if ($baseCommit) { 'Changed archive uses git diff from baseCommit; full archive contains all reviewed tracked files. Deletions are separate.' } else { 'Initial source: changed and full inventories are identical.' })
 core=$manifest; build=$build; dotnetRuntime=$metadata.RuntimeVersion; sdk=$metadata.SdkVersion; platform='Windows 11 x64'
 applicationSigned=$false; installerSigned=$false; driverSignature='Vendor signature checked by fetch-runtime.ps1 during build'
 nuget=($dependencies | Sort-Object -Unique)
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $directory 'provenance.json') -Encoding utf8NoBOM
Get-ChildItem -LiteralPath $directory -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
 Sort-Object Name | ForEach-Object {
  (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
 } | Set-Content -LiteralPath (Join-Path $directory 'SHA256SUMS.txt') -Encoding utf8NoBOM
Write-Output ('Versioned source archives, deletions and provenance: ' + $directory)
