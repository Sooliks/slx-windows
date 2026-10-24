function Assert-SlxRuntimeInputs {
 param([string]$Root, [string]$Unpacked, [string]$Source, [string]$ArchiveSha256,
  [string]$XrayVersion, [object[]]$Files)
 $manifestPath = Join-Path $Root 'runtime/runtime-manifest.json'
 $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
 if ($manifest.xrayVersion -cne $XrayVersion -or $manifest.source -cne $Source -or
  $manifest.archiveSha256 -cne $ArchiveSha256 -or $manifest.driverSignatureStatus -cne 'Valid' -or
  $manifest.driverSigner -isnot [string] -or [string]::IsNullOrWhiteSpace($manifest.driverSigner)) {
  throw 'Reviewed runtime manifest does not match the verified upstream runtime'
 }
 if (@($manifest.files).Count -ne 2 -or @($Files).Count -ne 2) { throw 'Reviewed runtime file inventory must contain exactly two files' }
 foreach ($name in @('xray.exe','wintun.dll')) {
  $reviewed = @($manifest.files | Where-Object { $_.name -ceq $name })
  $observed = @($Files | Where-Object { $_.name -ceq $name })
  if ($reviewed.Count -ne 1 -or $observed.Count -ne 1 -or
   $reviewed[0].sha256 -isnot [string] -or $reviewed[0].sha256 -cnotmatch '\A[0-9a-f]{64}\z' -or
   $reviewed[0].sha256 -cne $observed[0].sha256) {
   throw ('Reviewed runtime hash or file inventory mismatch: ' + $name)
  }
 }
 # The pinned DLL bytes and its separately verified valid signature bind the signer.
 # Keep its reviewed display name: OS versions can format certificate OIDs differently.
 $utf8 = [Text.UTF8Encoding]::new($false, $true)
 foreach ($name in @('LICENSE','LICENSE-Wintun')) {
  $reviewedPath = Join-Path $Root ('licenses/' + $name)
  $upstreamPath = Join-Path $Unpacked $name
  if (-not (Test-Path -LiteralPath $reviewedPath -PathType Leaf) -or
   -not (Test-Path -LiteralPath $upstreamPath -PathType Leaf)) { throw ('Required runtime license is missing: ' + $name) }
  $reviewed = $utf8.GetString([IO.File]::ReadAllBytes($reviewedPath)).Replace("`r`n", "`n")
  $upstream = $utf8.GetString([IO.File]::ReadAllBytes($upstreamPath)).Replace("`r`n", "`n")
  if ($reviewed -cne $upstream) { throw ('Reviewed runtime license differs from the pinned archive: ' + $name) }
 }
}
