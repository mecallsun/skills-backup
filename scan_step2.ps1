$root = "E:\AI工作目录"
$outputJson = "C:\Users\Mecall\.skills-pool\scan_step2_result.json"

Write-Host "=== Step 2: Scan SKILL.md files under E:\AI工作目录 ==="
Write-Host ("Start time: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))

# 使用 .NET API 绕过 PowerShell 编码 Bug
$topDirs = [System.IO.Directory]::GetDirectories($root)
Write-Host ("Top-level dirs: " + $topDirs.Count)

$allFiles = @()
$skipNames = @(".git", "node_modules", "__pycache__", "venv", ".venv", "Soft")

foreach ($topDir in $topDirs) {
    $dirInfo = New-Object System.IO.DirectoryInfo($topDir)
    $dirName = $dirInfo.Name
    if ($skipNames -contains $dirName) {
        Write-Host ("  skip: " + $dirName)
        continue
    }
    Write-Host ("  scanning: " + $dirName)

    # 使用 .NET 递归枚举
    $files = [System.IO.Directory]::GetFiles($topDir, "SKILL.md", [System.IO.SearchOption]::AllDirectories)
    Write-Host ("    found: " + $files.Count)

    foreach ($filePath in $files) {
        $fileInfo = New-Object System.IO.FileInfo($filePath)
        $allFiles += [PSCustomObject]@{
            full_path = $filePath
            directory = $fileInfo.DirectoryName
            size_bytes = $fileInfo.Length
            last_modified = $fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
            top_dir = $dirName
        }
    }
}

# 扫描根目录自身
$rootFiles = [System.IO.Directory]::GetFiles($root, "SKILL.md", [System.IO.SearchOption]::TopDirectoryOnly)
foreach ($filePath in $rootFiles) {
    $fileInfo = New-Object System.IO.FileInfo($filePath)
    $allFiles += [PSCustomObject]@{
        full_path = $filePath
        directory = $fileInfo.DirectoryName
        size_bytes = $fileInfo.Length
        last_modified = $fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
        top_dir = "(root)"
    }
}

$totalCount = $allFiles.Count
Write-Host ("End time: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
Write-Host ("Total SKILL.md files: " + $totalCount)

# 保存为 JSON
$output = @{
    scan_root = $root
    scan_time = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    total_files = $totalCount
    files = $allFiles
}
$output | ConvertTo-Json -Depth 4 | Out-File -Encoding utf8 $outputJson

Write-Host ""
Write-Host "=== Result Files ==="
$allFiles | Select-Object top_dir, size_bytes, directory | Format-Table -AutoSize
Write-Host ""
Write-Host ("Result saved to: " + $outputJson)