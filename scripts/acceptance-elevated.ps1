$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$evidence = Join-Path $root 'artifacts/acceptance/elevated-result.json'
try {
 & (Join-Path $PSScriptRoot 'acceptance-tun.ps1') -Run
 @{ completed=$true; passed=$true } | ConvertTo-Json | Set-Content -LiteralPath $evidence -Encoding utf8NoBOM
} catch {
 @{ completed=$true; passed=$false; message=$_.Exception.Message } | ConvertTo-Json | Set-Content -LiteralPath $evidence -Encoding utf8NoBOM
 exit 1
}
