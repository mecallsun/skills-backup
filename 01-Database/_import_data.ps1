# 读取员工宿舍明细表.xlsx 并导入到远程数据库
# 修复版：分多个 try-catch 块

# 1. 连接参数
$remoteConnStr = 'Server=192.168.1.237;Database=WaterMeterDB;User Id=__DB_USER__;Password=__DB_PASSWORD__;TrustServerCertificate=True;'
$excelPath = 'E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统\行政宿舍资料\员工宿舍明细表.xlsx'

Write-Host '=========================================' -ForegroundColor Cyan
Write-Host '员工宿舍明细表导入工具 (v2.12.40)' -ForegroundColor Cyan
Write-Host '=========================================' -ForegroundColor Cyan
Write-Host ''

# 2. 测试远程数据库连接
try {
    $remoteConn = New-Object System.Data.SqlClient.SqlConnection($remoteConnStr)
    $remoteConn.Open()
    Write-Host '[1/5] 远程数据库连接成功' -ForegroundColor Green
}
catch {
    Write-Host "[1/5] 远程数据库连接失败: $_" -ForegroundColor Red
    exit 1
}

# 3. 读取远程数据库表列表
$existingTables = @()
try {
    $cmd = $remoteConn.CreateCommand()
    $cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        $existingTables += $reader['TABLE_NAME']
    }
    $reader.Close()
    Write-Host "[2/5] 远程数据库现有表: $($existingTables -join ', ')" -ForegroundColor Yellow
}
catch {
    Write-Host "[2/5] 读取表列表失败: $_" -ForegroundColor Red
    $remoteConn.Close()
    exit 1
}

# 4. 检查/创建 SysEmployee 和 DormBooking 表
$needEmployee = $existingTables -notcontains 'SysEmployee'
$needBooking = $existingTables -notcontains 'DormBooking'

if ($needEmployee -or $needBooking) {
    Write-Host '[3/5] 远程数据库缺少目标表，正在创建...' -ForegroundColor Yellow

    $createTableSql = @"
IF OBJECT_ID('dbo.SysEmployee', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SysEmployee (
        EmployeeId    INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeCode  NVARCHAR(32) NOT NULL,
        RealName      NVARCHAR(64) NOT NULL,
        Department    NVARCHAR(64) NULL,
        DepartmentId  INT NULL,
        EmployeeType  NVARCHAR(32) NULL,
        EmployeeTypeId INT NULL,
        TeamId        INT NULL,
        Phone         NVARCHAR(16) NULL,
        HireDate      DATE NULL,
        LeaveDate     DATE NULL,
        Status        INT NOT NULL DEFAULT 1,
        DormCode      NVARCHAR(32) NULL,
        BedNo         INT NULL,
        AttendanceTypeId INT NULL,
        Remark        NVARCHAR(512) NULL,
        IsActive      BIT NOT NULL DEFAULT 1,
        CreatedAt     DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt     DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE UNIQUE INDEX IX_SysEmployee_Code ON dbo.SysEmployee(EmployeeCode);
END

IF OBJECT_ID('dbo.DormBooking', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DormBooking (
        BookingId        INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeId       INT NOT NULL,
        EmployeeCode     NVARCHAR(32) NOT NULL,
        EmployeeName     NVARCHAR(64) NOT NULL,
        Phone            NVARCHAR(16) NULL,
        Department       NVARCHAR(64) NULL,
        DormCode         NVARCHAR(32) NOT NULL,
        BookingType      TINYINT NOT NULL,
        BookingDate      DATE NOT NULL,
        Status           TINYINT NOT NULL,
        Reason           NVARCHAR(256) NULL,
        Remark           NVARCHAR(512) NULL,
        RegistrationDate DATETIME NOT NULL DEFAULT GETDATE(),
        Registrar        NVARCHAR(32) NULL,
        IsActive         BIT NOT NULL DEFAULT 1,
        CreatedAt        DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt        DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE INDEX IX_DormBooking_DormCode ON dbo.DormBooking(DormCode);
    CREATE INDEX IX_DormBooking_EmployeeCode ON dbo.DormBooking(EmployeeCode);
END
"@

    try {
        $cmd = $remoteConn.CreateCommand()
        $cmd.CommandText = $createTableSql
        $cmd.CommandTimeout = 60
        $cmd.ExecuteNonQuery()
        Write-Host '    表结构创建成功' -ForegroundColor Green
    }
    catch {
        Write-Host "    表创建失败: $_" -ForegroundColor Red
        $remoteConn.Close()
        exit 1
    }
} else {
    Write-Host '[3/5] 目标表已存在，跳过创建' -ForegroundColor Green
}

# 5. 读取 Excel 数据
Write-Host '[4/5] 读取 Excel 文件...' -ForegroundColor Yellow

$excelSuccess = $false
try {
    Add-Type -AssemblyName 'System.Data.OleDb' -ErrorAction Stop
}
catch {
    Write-Host "    OleDb 程序集加载失败: $_" -ForegroundColor Red
}

if ('System.Data.OleDb.OleDbConnection' -as [type]) {
    try {
        $excelConn = New-Object System.Data.OleDb.OleDbConnection("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=`"$excelPath`";Extended Properties='Excel 12.0 Xml;HDR=YES;IMEX=1'")
        $excelConn.Open()
        Write-Host '    Excel 连接成功 (OleDb)' -ForegroundColor Green
        $excelSuccess = $true
        $useMethod = 'OleDb'
    }
    catch {
        Write-Host "    OleDb 读取失败: $_" -ForegroundColor Yellow
    }
}

if (-not $excelSuccess) {
    try {
        # 备用方案：使用 OpenXML SDK 直接解析 xlsx（zip 解压 + xml 解析）
        Add-Type -AssemblyName 'System.IO.Compression' -ErrorAction Stop
        Add-Type -AssemblyName 'System.Xml' -ErrorAction Stop
        Write-Host '    使用 OpenXML SDK 读取' -ForegroundColor Yellow
        $useMethod = 'OpenXML'
    }
    catch {
        Write-Host "    OpenXML 加载失败" -ForegroundColor Red
    }
}

# 输出探索结果
if ($excelSuccess -and $useMethod -eq 'OleDb') {
    try {
        $tables = $excelConn.GetOleDbSchemaTable([System.Data.OleDb.OleDbSchemaGuid]::Tables, $null)
        $sheetNames = @()
        foreach ($row in $tables.Rows) {
            $name = ($row.TABLE_NAME -replace "\\$", '' -replace "'", '')
            if ($name -notlike '*Print*' -and $name -notlike '*_xlnm*') {
                $sheetNames += $name
            }
        }

        Write-Host ''
        Write-Host "    === 工作表列表: ===" -ForegroundColor Cyan
        foreach ($n in $sheetNames) { Write-Host "      $n" -ForegroundColor Yellow }

        foreach ($sheetName in $sheetNames) {
            Write-Host ''
            Write-Host "    === 工作表 [$sheetName] ===" -ForegroundColor Cyan
            $cmd = New-Object System.Data.OleDb.OleDbCommand("SELECT TOP 3 * FROM [$sheetName]", $excelConn)
            $reader = $cmd.ExecuteReader()
            $cols = @()
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $cols += $reader.GetName($i)
            }
            Write-Host "    列: $($cols -join ' | ')" -ForegroundColor Gray

            while ($reader.Read()) {
                $vals = @()
                for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                    $v = $reader.GetValue($i)
                    if ($null -eq $v) { $v = '' }
                    $vals += $v.ToString()
                }
                Write-Host "      $($vals -join ' | ')"
            }
            $reader.Close()
        }
        $excelConn.Close()
    }
    catch {
        Write-Host "    Excel 数据读取异常: $_" -ForegroundColor Red
    }
}

# 6. 总结
Write-Host ''
Write-Host '=========================================' -ForegroundColor Cyan
Write-Host '✅ 探索阶段完成' -ForegroundColor Cyan
Write-Host '=========================================' -ForegroundColor Cyan
$remoteConn.Close()