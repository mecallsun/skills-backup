# 探索远程数据库表结构
$connectionString = 'Server=192.168.1.237;Database=WaterMeterDB;User Id=__DB_USER__;Password=__DB_PASSWORD__;TrustServerCertificate=True;'

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    Write-Host '=== 连接成功 ===' -ForegroundColor Green

    # 所有表
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME"
    $reader = $cmd.ExecuteReader()
    Write-Host ''
    Write-Host '=== 所有数据表 ===' -ForegroundColor Cyan
    while ($reader.Read()) {
        Write-Host ("{0}.{1}" -f $reader['TABLE_SCHEMA'], $reader['TABLE_NAME'])
    }
    $reader.Close()

    # Dorm 表结构
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Dorm' ORDER BY ORDINAL_POSITION"
    $reader = $cmd.ExecuteReader()
    Write-Host ''
    Write-Host '=== Dorm 表结构 ===' -ForegroundColor Cyan
    while ($reader.Read()) {
        Write-Host ("{0,-30} {1,-15} {2}" -f $reader['COLUMN_NAME'], $reader['DATA_TYPE'], $reader['IS_NULLABLE'])
    }
    $reader.Close()

    # Dorm 表行数
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = 'SELECT COUNT(*) FROM Dorm'
    $cnt = $cmd.ExecuteScalar()
    Write-Host ''
    Write-Host "Dorm 表行数: $cnt" -ForegroundColor Yellow

    # MeterRecord 表行数
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = 'SELECT COUNT(*) FROM MeterRecord'
    Write-Host "MeterRecord 表行数: $($cmd.ExecuteScalar())" -ForegroundColor Yellow

    # SysUser 表行数
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = 'SELECT COUNT(*) FROM SysUser'
    Write-Host "SysUser 表行数: $($cmd.ExecuteScalar())" -ForegroundColor Yellow

    $connection.Close()
}
catch {
    Write-Host "错误: $_" -ForegroundColor Red
}