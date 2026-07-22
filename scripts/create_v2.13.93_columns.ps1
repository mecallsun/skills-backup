$ErrorActionPreference = 'Stop'

$connString = "Server=192.168.1.237;Database=WaterMeterDB;User Id=__DB_USER__;Password=__DB_PASSWORD__;TrustServerCertificate=True;"

$sqlLines = @()
$sqlLines += "IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('BillingStandard') AND name='SubsidyAmount')"
$sqlLines += "BEGIN ALTER TABLE BillingStandard ADD SubsidyAmount DECIMAL(12,2) NOT NULL DEFAULT 0; PRINT '[v2.13.93] BillingStandard.SubsidyAmount OK'; END"
$sqlLines += "ELSE PRINT '[v2.13.93] BillingStandard.SubsidyAmount exists'"

$sqlLines += "IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('EmployeeBilling') AND name='SubsidyAmount')"
$sqlLines += "BEGIN ALTER TABLE EmployeeBilling ADD SubsidyAmount DECIMAL(12,2) NOT NULL DEFAULT 0; PRINT '[v2.13.93] EmployeeBilling.SubsidyAmount OK'; END"
$sqlLines += "ELSE PRINT '[v2.13.93] EmployeeBilling.SubsidyAmount exists'"

$sqlLines += "IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('SysUser') AND name='ExpiresAt')"
$sqlLines += "BEGIN ALTER TABLE SysUser ADD ExpiresAt DATETIME NULL; PRINT '[v2.13.93] SysUser.ExpiresAt OK'; END"
$sqlLines += "ELSE PRINT '[v2.13.93] SysUser.ExpiresAt exists'"

$sql = $sqlLines -join "`n"

Add-Type -AssemblyName System.Data

$conn = New-Object System.Data.SqlClient.SqlConnection $connString
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = $sql

$reader = $cmd.ExecuteReader()
do {
    while ($reader.Read()) {
        Write-Host $reader.GetString(0)
    }
} while ($reader.NextResult())
$reader.Close()
$conn.Close()
Write-Host '[v2.13.93] DDL sync complete'