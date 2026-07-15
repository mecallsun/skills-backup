# 初始化远程数据库（创建 SysEmployee 和 DormBooking 表）
# Python 版本：使用 pyodbc 连接 SQL Server

import os
import sys

REMOTE_CONN = 'DRIVER={SQL Server};SERVER=192.168.1.237;DATABASE=WaterMeterDB;UID=__DB_USER__;PWD=__DB_PASSWORD__;'

CREATE_SQL = """
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
        EmploymentStatusId INT NOT NULL DEFAULT 1,
        ResidenceStatusId INT NOT NULL DEFAULT 2,
        Remark        NVARCHAR(512) NULL,
        IsActive      BIT NOT NULL DEFAULT 1,
        CreatedAt     DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt     DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE UNIQUE INDEX IX_SysEmployee_Code ON dbo.SysEmployee(EmployeeCode);
    PRINT 'SysEmployee created';
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
    PRINT 'DormBooking created';
END

IF OBJECT_ID('dbo.Team', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Team (
        Id        INT IDENTITY(1,1) PRIMARY KEY,
        Code      NVARCHAR(20) NOT NULL,
        Name      NVARCHAR(50) NOT NULL,
        Remark    NVARCHAR(200) NULL,
        SortOrder INT NOT NULL DEFAULT 0,
        IsActive  BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
        UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
    CREATE UNIQUE INDEX IX_Team_Code ON dbo.Team(Code);
    INSERT INTO dbo.Team (Code, Name, SortOrder) VALUES
        ('DEFAULT', N'默认', 0),
        ('A', N'A班', 1),
        ('B', N'B班', 2),
        ('C', N'C班', 3),
        ('D', N'D班', 4),
        ('E', N'E班', 5),
        ('F', N'F班', 6),
        ('G', N'G班', 7),
        ('H', N'H班', 8);
    PRINT 'Team created and seeded';
END
"""

def main():
    try:
        import pyodbc
    except ImportError:
        print('pyodbc not installed. Install with: pip install pyodbc')
        return

    try:
        conn = pyodbc.connect(REMOTE_CONN, timeout=30)
        conn.autocommit = True
        cursor = conn.cursor()
        print('=== 连接成功 ===')

        # 1. 列出所有表
        cursor.execute("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME")
        tables = [row[0] for row in cursor.fetchall()]
        print(f'现有表 ({len(tables)}): {", ".join(tables)}')

        # 2. 检查 Dorm 表数据
        cursor.execute('SELECT COUNT(*) FROM Dorm')
        dorm_count = cursor.fetchone()[0]
        print(f'Dorm 表行数: {dorm_count}')

        if dorm_count > 0:
            cursor.execute('SELECT DormCode, Building, Floor, DormAddress, DormType FROM Dorm ORDER BY DormCode')
            print('\nDorm 表前 10 行:')
            print(f'{"房号":<10} {"楼栋":<10} {"楼层":<8} {"地址":<20} {"类型"}')
            for row in cursor.fetchall()[:10]:
                print(f'{row.DormCode:<10} {row.Building or "":<10} {row.Floor or "":<8} {row.DormAddress or "":<20} {row.DormType or ""}')

        # 3. 创建缺失的表
        print('\n=== 创建缺失的表 ===')
        cursor.execute(CREATE_SQL)

        # 4. 重新列出所有表
        cursor.execute("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME")
        tables = [row[0] for row in cursor.fetchall()]
        print(f'更新后表 ({len(tables)}): {", ".join(tables)}')

        cursor.close()
        conn.close()
        print('\n✅ 完成')
    except Exception as e:
        print(f'错误: {e}')


if __name__ == '__main__':
    main()