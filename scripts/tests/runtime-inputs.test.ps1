$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $root 'scripts/runtime-inputs.ps1')
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('slx-runtime-inputs-' + [guid]::NewGuid().ToString('N'))
$unpacked = Join-Path $fixture 'unpacked'
$manifestPath = Join-Path $fixture 'runtime/runtime-manifest.json'
$source = 'https://github.com/XTLS/Xray-core/releases/download/v26.9.9/Xray-windows-64.zip'
$archiveSha256 = '244deaba2098c2964e49bba90df3707777e5f5f428a82d2f29604015f24beec2'
$files = @(
 [pscustomobject]@{ name='xray.exe'; sha256='0d0fc0ea2b05641acb78c01fc36ad694e7b029861b2d5eb93da0e3e9fda9a98f' }
 [pscustomobject]@{ name='wintun.dll'; sha256='e5da8447dc2c320edc0fc52fa01885c103de8c118481f683643cacc3220dafce' }
)
$inputs = @('runtime/runtime-manifest.json','licenses/LICENSE','licenses/LICENSE-Wintun')
$passed = 0
function Reset-Inputs {
 foreach ($relative in $inputs) {
  $target = Join-Path $fixture $relative
  [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
  Copy-Item -LiteralPath (Join-Path $root $relative) -Destination $target -Force
 }
 [IO.Directory]::CreateDirectory($unpacked) | Out-Null
 foreach ($name in @('LICENSE','LICENSE-Wintun')) {
  Copy-Item -LiteralPath (Join-Path $root ('licenses/' + $name)) -Destination (Join-Path $unpacked $name) -Force
 }
}
function Invoke-Check {
 Assert-SlxRuntimeInputs -Root $fixture -Unpacked $unpacked -Source $source -ArchiveSha256 $archiveSha256 -XrayVersion '26.9.9' -Files $files
}
function Change-Manifest([scriptblock]$Change) {
 $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 & $Change $manifest
 $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
}
function Expect-Rejection([string]$Name, [scriptblock]$Change) {
 Reset-Inputs
 & $Change
 $rejected = $false
 try { Invoke-Check } catch { $rejected = $true }
 if (-not $rejected) { throw ('Expected rejection: ' + $Name) }
 $script:passed++
 Write-Output ('PASS ' + $Name)
}
try {
 Reset-Inputs
 $before = @($inputs | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $fixture $_) -Algorithm SHA256).Hash })
 Invoke-Check
 $after = @($inputs | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $fixture $_) -Algorithm SHA256).Hash })
 if (($before -join ',') -cne ($after -join ',')) { throw 'Validation changed reviewed source inputs' }
 $passed++
 Write-Output 'PASS valid input preserves reviewed bytes'
 $license = Join-Path $unpacked 'LICENSE-Wintun'
 $text = [IO.File]::ReadAllText($license).Replace("`r`n", "`n").Replace("`n", "`r`n")
 [IO.File]::WriteAllText($license, $text, [Text.UTF8Encoding]::new($false))
 Invoke-Check
 $passed++
 Write-Output 'PASS upstream license CRLF normalization'
 Expect-Rejection 'changed runtime hash' { Change-Manifest { param($m) $m.files[0].sha256 = '0' * 64 } }
 Expect-Rejection 'changed runtime name' { Change-Manifest { param($m) $m.files[0].name = 'other.exe' } }
 Expect-Rejection 'runtime name case mismatch' { Change-Manifest { param($m) $m.files[0].name = 'XRAY.EXE' } }
 Expect-Rejection 'missing runtime file' { Change-Manifest { param($m) $m.files = @($m.files[0]) } }
 Expect-Rejection 'duplicate runtime file' { Change-Manifest { param($m) $m.files = @($m.files[0], $m.files[0]) } }
 Expect-Rejection 'extra runtime file' { Change-Manifest { param($m) $m.files += $m.files[0] } }
 Expect-Rejection 'changed runtime version' { Change-Manifest { param($m) $m.xrayVersion = '26.9.10' } }
 Expect-Rejection 'changed upstream source' { Change-Manifest { param($m) $m.source = 'https://example.invalid/xray.zip' } }
 Expect-Rejection 'changed archive hash' { Change-Manifest { param($m) $m.archiveSha256 = '0' * 64 } }
 Expect-Rejection 'invalid signature metadata' { Change-Manifest { param($m) $m.driverSignatureStatus = 'UnknownError' } }
 Expect-Rejection 'missing signer metadata' { Change-Manifest { param($m) $m.driverSigner = '' } }
 Expect-Rejection 'missing manifest' { Remove-Item -LiteralPath $manifestPath }
 Expect-Rejection 'changed reviewed license' { Add-Content -LiteralPath (Join-Path $fixture 'licenses/LICENSE') -Value 'Changed license' }
 Expect-Rejection 'changed upstream license' { Add-Content -LiteralPath (Join-Path $unpacked 'LICENSE-Wintun') -Value 'Changed license' }
 Expect-Rejection 'missing reviewed license' { Remove-Item -LiteralPath (Join-Path $fixture 'licenses/LICENSE-Wintun') }
 Expect-Rejection 'missing upstream license' { Remove-Item -LiteralPath (Join-Path $unpacked 'LICENSE') }
 Write-Output ('Runtime input checks passed: ' + $passed)
} finally {
 if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Recurse -Force }
}
