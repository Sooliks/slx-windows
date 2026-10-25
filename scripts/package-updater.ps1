param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference = 'Stop'
$updaterRoot = [IO.Path]::GetFullPath($Directory)
if (-not (Test-Path -LiteralPath $updaterRoot -PathType Container) -or
 ((Get-Item -LiteralPath $updaterRoot).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Invalid updater package directory' }
$entries = @(foreach ($entry in Get-ChildItem -LiteralPath $updaterRoot -File -Recurse) {
 $relative = [IO.Path]::GetRelativePath($updaterRoot, $entry.FullName).Replace('\','/')
 if ($relative -eq 'updater-manifest.json') { continue }
 if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Updater package refuses reparse files' }
 [ordered]@{ Name=$relative; Size=$entry.Length; Sha256=(Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
if ($entries.Count -eq 0 -or $entries.Count -gt 1024 -or ($entries | Measure-Object Size -Sum).Sum -gt 536870912) { throw 'Updater package exceeds bounds' }
@{ Files=$entries } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $updaterRoot 'updater-manifest.json') -Encoding utf8NoBOM
