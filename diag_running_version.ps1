$latestPath = "E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统\release\latest\Admin"
$latestPathForward = "E:/AI工作目录/AI编程开发/JINGE开发/宿舍管理系统/release/latest/Admin"

Write-Host "=== Path Test ==="
Write-Host "Path: $latestPath"
Write-Host "Test-Path: $(Test-Path $latestPath)"

Write-Host ""
Write-Host "=== Files ==="
if (Test-Path $latestPathForward) {
    Get-ChildItem "$latestPathForward/Pages/Dorms/Details.cshtml", "$latestPathForward/DormManage.Admin.dll" |
        Select-Object FullName, LastWriteTime, Length |
        Format-Table -AutoSize
}

Write-Host ""
Write-Host "=== All Admin Dirs ==="
Get-ChildItem -Path "E:/AI工作目录/AI编程开发/JINGE开发/宿舍管理系统/release/latest" -Directory | Select-Object Name

Write-Host ""
Write-Host "=== Process Check ==="
$adminProc = Get-Process -Name "DormManage.Admin" -ErrorAction SilentlyContinue
if ($adminProc) {
    Write-Host "Admin running: PID $($adminProc.Id)"
    Write-Host "Path: $($adminProc.MainModule.FileName)"
} else {
    Write-Host "Admin NOT running"
}

$trayProc = Get-Process -Name "DormManage.TrayApp" -ErrorAction SilentlyContinue
if ($trayProc) {
    Write-Host "TrayApp running: PID $($trayProc.Id)"
    Write-Host "Path: $($trayProc.MainModule.FileName)"
} else {
    Write-Host "TrayApp NOT running"
}