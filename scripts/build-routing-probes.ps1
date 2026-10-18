$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'tools/Slx.RouteProbe/Slx.RouteProbe.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Probe locked restore failed' }
foreach ($role in @('A','B')) {
 dotnet build $project -c Release --no-restore -p:AssemblyName=SlxRouteProbe$role -o (Join-Path $root "artifacts/acceptance/probe-$role")
 if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
}
Write-Output 'Two distinct executable paths built. No network, service, driver or route changes performed.'
