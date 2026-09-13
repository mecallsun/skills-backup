@echo off
setlocal
set ROOT=E:\AI工作目录
set OUTPUT=C:\Users\Mecall\.skills-pool\scan_step2_result.json
set SCRIPT=C:\Users\Mecall\.skills-pool\scan_step2_inner.ps1

echo === Step 2: Scan SKILL.md under E:\AI工作目录 ===

rem 写入内部 PS1 脚本（用 chcp 65001 + UTF-8 BOM 兼容中文）
> "%SCRIPT%" echo # Scan inner
>>"%SCRIPT%" echo $ErrorActionPreference = "SilentlyContinue"
>>"%SCRIPT%" echo chcp 65001 ^| Out-Null
>>"%SCRIPT%" echo $root = $env:ROOT_PATH
>>"%SCRIPT%" echo Write-Host ("Root: " + $root)
>>"%SCRIPT%" echo if (-not (Test-Path $root)) { Write-Host "ROOT NOT FOUND"; exit 1 }
>>"%SCRIPT%" echo.
>>"%SCRIPT%" echo $topDirs = Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue
>>"%SCRIPT%" echo Write-Host ("Top-level dirs: " + $topDirs.Count)
>>"%SCRIPT%" echo.
>>"%SCRIPT%" echo $allFiles = @()
>>"%SCRIPT%" echo foreach ($topDir in $topDirs) {
>>"%SCRIPT%" echo     Write-Host ("  scanning: " + $topDir.Name)
>>"%SCRIPT%" echo     $files = Get-ChildItem -Path $topDir.FullName -Recurse -Filter "SKILL.md" -File -ErrorAction SilentlyContinue
>>"%SCRIPT%" echo     Write-Host ("    found: " + $files.Count)
>>"%SCRIPT%" echo     foreach ($f in $files) {
>>"%SCRIPT%" echo         $allFiles += [PSCustomObject]@{
>>"%SCRIPT%" echo             full_path = $f.FullName
>>"%SCRIPT%" echo             directory = $f.DirectoryName
>>"%SCRIPT%" echo             size_bytes = $f.Length
>>"%SCRIPT%" echo             top_dir = $topDir.Name
>>"%SCRIPT%" echo         }
>>"%SCRIPT%" echo     }
>>"%SCRIPT%" echo }
>>"%SCRIPT%" echo.
>>"%SCRIPT%" echo $totalCount = $allFiles.Count
>>"%SCRIPT%" echo Write-Host ("Total SKILL.md: " + $totalCount)
>>"%SCRIPT%" echo.
>>"%SCRIPT%" echo $output = @{
>>"%SCRIPT%" echo     scan_root = $root
>>"%SCRIPT%" echo     scan_time = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
>>"%SCRIPT%" echo     total_files = $totalCount
>>"%SCRIPT%" echo     files = $allFiles
>>"%SCRIPT%" echo }
>>"%SCRIPT%" echo $output ^| ConvertTo-Json -Depth 4 ^| Out-File -Encoding utf8 $env:OUTPUT_PATH
>>"%SCRIPT%" echo.
>>"%SCRIPT%" echo Write-Host ("Result saved to: " + $env:OUTPUT_PATH)
>>"%SCRIPT%" echo.
>>"%SCRIPT%" echo $allFiles ^| Select-Object top_dir, size_bytes, directory ^| Format-Table -AutoSize

set ROOT_PATH=%ROOT%
set OUTPUT_PATH=%OUTPUT%
powershell -ExecutionPolicy Bypass -File "%SCRIPT%"