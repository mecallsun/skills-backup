$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $MyInvocation.MyCommand.Path -Parent)
Set-Location '..'

$ts = Get-Date -Format 'yyyyMMdd_HHmmss'
$dest = 'publish-final'
if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
New-Item -ItemType Directory -Path $dest -Force | Out-Null

Write-Host '[1/3] Publishing Admin...' -ForegroundColor Cyan
dotnet publish DormManage.Admin/DormManage.Admin.csproj -c Release -r win-x64 --self-contained true -o "$dest/Admin" --nologo 2>&1 | Select-Object -Last 5

Write-Host '[2/3] Publishing Api...' -ForegroundColor Cyan
dotnet publish DormManage.Api/DormManage.Api.csproj -c Release -r win-x64 --self-contained true -o "$dest/Api" --nologo 2>&1 | Select-Object -Last 5

Write-Host '[3/3] Publishing TrayApp...' -ForegroundColor Cyan
dotnet publish DormManage.TrayApp/DormManage.TrayApp.csproj -c Release -r win-x64 --self-contained true -o "$dest/TrayApp" --nologo 2>&1 | Select-Object -Last 5

$zipName = "Claude_Deploy_v2.13.92_${ts}.zip"
Write-Host "Packing to $zipName..." -ForegroundColor Cyan
Compress-Archive -Path "$dest/*" -DestinationPath $zipName -CompressionLevel Optimal
Get-ChildItem $zipName | Select-Object Name,@{n='MB';e={[math]::Round($_.Length/1MB,1)}} | Format-Table | Out-String | Write-Host
Write-Host 'DONE' -ForegroundColor Green