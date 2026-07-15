# 探索 Excel 文件结构（简化版）
$xlsx = "E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统\行政宿舍资料\员工宿舍明细表.xlsx"

Write-Host "文件路径: $xlsx" -ForegroundColor Cyan
Write-Host "文件大小: $((Get-Item $xlsx).Length) bytes"

try {
    Add-Type -AssemblyName 'System.Data.OleDb'
    $conn = New-Object System.Data.OleDb.OleDbConnection("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$xlsx;Extended Properties='Excel 12.0 Xml;HDR=YES;IMEX=1'")
    $conn.Open()
    Write-Host ''
    Write-Host 'Excel 连接成功' -ForegroundColor Green

    $tables = $conn.GetOleDbSchemaTable([System.Data.OleDb.OleDbSchemaGuid]::Tables, $null)
    $sheetNames = @()
    foreach ($row in $tables.Rows) {
        $name = ($row.TABLE_NAME -replace "\\$", '' -replace "'", '')
        $sheetNames += $name
    }

    Write-Host ''
    Write-Host '=== 工作表 ===' -ForegroundColor Cyan
    foreach ($n in $sheetNames) { Write-Host "  $n" }

    foreach ($sheetName in $sheetNames) {
        Write-Host ''
        Write-Host "=== 工作表: $sheetName ===" -ForegroundColor Yellow

        $cmd = New-Object System.Data.OleDb.OleDbCommand("SELECT TOP 5 * FROM [$sheetName]", $conn)
        $reader = $cmd.ExecuteReader()
        $cols = @()
        for ($i = 0; $i -lt $reader.FieldCount; $i++) {
            $cols += $reader.GetName($i)
        }
        Write-Host "列名: $($cols -join ' | ')"

        while ($reader.Read()) {
            $values = @()
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $v = $reader.GetValue($i)
                if ($null -eq $v) { $v = '' }
                $values += $v.ToString()
            }
            Write-Host ($values -join ' | ')
        }
        $reader.Close()
    }

    $conn.Close()
}
catch {
    Write-Host "错误: $_" -ForegroundColor Red
}