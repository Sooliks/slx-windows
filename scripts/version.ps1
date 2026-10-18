function Get-SlxReleaseMetadata {
 param([string]$Root = ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))))
 [xml]$properties = [IO.File]::ReadAllText((Join-Path $Root 'Directory.Build.props'))
 function Read-Property([string]$Name) {
  $nodes = @($properties.SelectNodes("/Project/PropertyGroup/$Name"))
  if ($nodes.Count -ne 1) { throw "Release property missing or ambiguous: $Name" }
  return $nodes[0].InnerText.Trim()
 }
 $version = Read-Property 'Version'
 $semver = '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z'
 if ($version -notmatch $semver -or $version.Length -gt 100) { throw 'Invalid release SemVer' }
 $baseVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3])"
 $preRelease = $Matches[4]
 $fileVersion = Read-Property 'FileVersion'
 if ($fileVersion -notmatch '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z' -or
  @($fileVersion.Split('.') | Where-Object { [long]$_ -gt 65535 }).Count -gt 0 -or
  -not $fileVersion.StartsWith($baseVersion + '.', [StringComparison]::Ordinal)) { throw 'Invalid Windows file version' }
 if ($preRelease -match '\A(?:alpha|beta|rc)\.([0-9]+)\z' -and $fileVersion -ne ($baseVersion + '.' + $Matches[1])) {
  throw 'Windows file version must track prerelease revision'
 }
 $runtime = Read-Property 'BundledRuntimeVersion'
 if ($runtime -notmatch '\A[0-9]+\.[0-9]+\.[0-9]+\z') { throw 'Invalid bundled .NET runtime version' }
 $sdk = ([IO.File]::ReadAllText((Join-Path $Root 'global.json')) | ConvertFrom-Json).sdk.version
 return [pscustomobject]@{
  Root=$Root; Version=$version; FileVersion=$fileVersion; RuntimeVersion=$runtime; SdkVersion=$sdk
  Tag=('v' + $version); AssetPrefix=('SLX-VPN-' + $version)
  ReleaseDirectory=(Join-Path $Root ('artifacts/release/' + $version))
  WorkDirectory=(Join-Path $Root ('artifacts/work/' + $version))
 }
}
