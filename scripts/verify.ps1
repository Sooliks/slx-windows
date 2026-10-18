$ErrorActionPreference = 'Stop'
Set-Location ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')))
& dotnet restore Slx.Windows.slnx --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed' }
& dotnet build Slx.Windows.slnx -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& dotnet test Slx.Windows.slnx -c Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
$large = Get-ChildItem src,tests -Recurse -File | Where-Object {
 $_.Extension -in '.cs','.xaml' -and $_.FullName -notmatch '[\\/](obj|bin)[\\/]' -and
 @([IO.File]::ReadAllLines($_.FullName) | Where-Object { $_.Trim() }).Count -gt 200
}
if ($large) { throw ('Source size contract failed: ' + ($large.Name -join ', ')) }
Write-Output 'Source checks passed. This is not privileged Windows/TUN acceptance.'
