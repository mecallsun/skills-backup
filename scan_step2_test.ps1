# 关键修复：用 [uri] 反序列化路径，绕过 PS5.1 中文解析 Bug
$root = "E:\AI工作目录"

# 方法1：通过 FileSystemObject COM 包装（对中文路径最可靠）
$fso = New-Object -ComObject Scripting.FileSystemObject
$folder = $fso.GetFolder($root)
Write-Host ("Folder: " + $folder.Path)
Write-Host ("Sub-folders count: " + $folder.SubFolders.Count)

$allFiles = @()
foreach ($sub in $folder.SubFolders) {
    $dirName = $sub.Name
    Write-Host ("  scanning: " + $dirName)
    $count = 0
    # 递归枚举 SKILL.md
    $files = $sub.Files  # 只看顶层文件
    foreach ($file in $files) {
        if ($file.Name -eq "SKILL.md") {
            $allFiles += [PSCustomObject]@{
                full_path = $file.Path
                directory = $sub.Path
                size_bytes = $file.Size
                top_dir = $dirName
            }
            $count++
        }
    }
    # 递归处理子目录
    $subfolders = $sub.SubFolders
    foreach ($sf in $subfolders) {
        $recursed = @()
        RecurseFolder $sf $recursed
        foreach ($rf in $recursed) {
            if ($rf.Name -eq "SKILL.md") {
                $allFiles += [PSCustomObject]@{
                    full_path = $rf.Path
                    directory = $sf.Path
                    size_bytes = $rf.Size
                    top_dir = $dirName
                }
                $count++
            }
        }
    }
    Write-Host ("    found: " + $count)
}

Write-Host ("Total: " + $allFiles.Count)