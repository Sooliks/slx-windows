param([switch]$Force)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$out = Join-Path $root '.runtime'
[IO.Directory]::CreateDirectory($out) | Out-Null
$archive = Join-Path $out 'Xray-windows-64.zip'
$url = 'https://github.com/XTLS/Xray-core/releases/download/v26.9.9/Xray-windows-64.zip'
if (-not (Test-Path -LiteralPath $archive) -or $Force) { Invoke-WebRequest -Uri $url -OutFile $archive -TimeoutSec 120 }
Invoke-WebRequest -Uri ($url + '.dgst') -OutFile ($archive + '.dgst') -TimeoutSec 30
$digest = [IO.File]::ReadAllText($archive + '.dgst')
$sha = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
if ($sha -ne '244deaba2098c2964e49bba90df3707777e5f5f428a82d2f29604015f24beec2' -or $digest -notmatch [regex]::Escape($sha)) { throw 'Upstream archive SHA256 mismatch' }
$unpacked = Join-Path $out 'unpacked'
[IO.Directory]::CreateDirectory($unpacked) | Out-Null
Expand-Archive -LiteralPath $archive -DestinationPath $unpacked -Force
$exe = Join-Path $unpacked 'xray.exe'
$version = (& $exe version | Select-Object -First 1)
if ($version -notmatch '^Xray 26\.9\.9 .*52a412d') { throw 'Unexpected upstream core provenance' }
$files = @()
foreach ($name in @('xray.exe','wintun.dll')) {
 $source = Join-Path $unpacked $name
 if (-not (Test-Path -LiteralPath $source)) { throw ('Missing upstream runtime: ' + $name) }
 Copy-Item -LiteralPath $source -Destination (Join-Path $out $name) -Force
 $files += [ordered]@{ name=$name; sha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $out 'wintun.dll')
if ($signature.Status -ne 'Valid') { throw 'Wintun must have a valid vendor Authenticode signature' }
. (Join-Path $PSScriptRoot 'runtime-inputs.ps1')
Assert-SlxRuntimeInputs -Root $root -Unpacked $unpacked -Source $url -ArchiveSha256 $sha -XrayVersion '26.9.9' -Files $files
# Build consumes reviewed source inputs. Updating the pinned runtime is a separate reviewed source change.
Copy-Item -LiteralPath (Join-Path $root 'runtime/runtime-manifest.json') -Destination (Join-Path $out 'runtime-manifest.json') -Force
Write-Output ('Verified official Xray 26.9.9 archive ' + $sha + '; Wintun signature Valid')
