$ErrorActionPreference = "Stop"
Set-Location "E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统"
$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$zipName = "DormManage-v2.13.179_$ts.zip"
$dest = "E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统\$zipName"
Compress-Archive -Path "release/latest/Admin","release/latest/Api","release/latest/TrayApp" -DestinationPath $dest -CompressionLevel Optimal -Force
$size = [math]::Round((Get-Item $dest).Length/1MB, 2)
Write-Output "ZIP: $dest"
Write-Output "Size: ${size} MB"
