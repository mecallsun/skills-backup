$ErrorActionPreference = 'Stop'

$connString = "Server=192.168.1.237;Database=WaterMeterDB;User Id=__DB_USER__;Password=__DB_PASSWORD__;TrustServerCertificate=True;"

$sql = @"
SELECT 'BillingStandard.SubsidyAmount' AS ColumnRef, COUNT(*) AS ExistsCnt FROM syscolumns WHERE id=OBJECT_ID('BillingStandard') AND name='SubsidyAmount'
UNION ALL
SELECT 'EmployeeBilling.SubsidyAmount', COUNT(*) FROM syscolumns WHERE id=OBJECT_ID('EmployeeBilling') AND name='SubsidyAmount'
UNION ALL
SELECT 'SysUser.ExpiresAt', COUNT(*) FROM syscolumns WHERE id=OBJECT_ID('SysUser') AND name='ExpiresAt'
"@

Add-Type -AssemblyName System.Data
$conn = New-Object System.Data.SqlClient.SqlConnection $connString
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = $sql

$reader = $cmd.ExecuteReader()
Write-Host "===== v2.13.93 DDL verify ====="
Write-Host ("{0,-40} {1,-5}" -f "Column", "Exists")
Write-Host ("-" * 45)
do {
    while ($reader.Read()) {
        Write-Host ("{0,-40} {1,-5}" -f $reader.GetString(0), $reader.GetInt32(1))
    }
} while ($reader.NextResult())
$reader.Close()
$conn.Close()