# -*- coding: utf-8 -*-
"""创建远程数据库缺失的基础资料表（分批执行）"""

import pyodbc

REMOTE_CONN = (
    'DRIVER={SQL Server};'
    'SERVER=192.168.1.237;'
    'DATABASE=WaterMeterDB;'
    'UID=__DB_USER__;'
    'PWD=__DB_PASSWORD__;'
)

STATEMENTS = [
    # Department
    """
    IF OBJECT_ID('dbo.Department', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Department (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            Code NVARCHAR(20) NOT NULL,
            Name NVARCHAR(50) NOT NULL,
            Remark NVARCHAR(200) NULL,
            SortOrder INT NOT NULL DEFAULT 0,
            IsActive BIT NOT NULL DEFAULT 1,
            CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
            UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
        );
        CREATE UNIQUE INDEX IX_Department_Code ON dbo.Department(Code);
    END
    """,
    # EmployeeType
    """
    IF OBJECT_ID('dbo.EmployeeType', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.EmployeeType (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            Code NVARCHAR(20) NOT NULL,
            Name NVARCHAR(50) NOT NULL,
            Remark NVARCHAR(200) NULL,
            SortOrder INT NOT NULL DEFAULT 0,
            IsActive BIT NOT NULL DEFAULT 1,
            CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
            UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
        );
        CREATE UNIQUE INDEX IX_EmployeeType_Code ON dbo.EmployeeType(Code);
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.EmployeeType)
        INSERT INTO dbo.EmployeeType (Code, Name, SortOrder) VALUES
        ('CONTRACT', N'合同工', 1),
        ('TEMPORARY', N'临时工', 2),
        ('OUTSOURCE', N'外包', 3),
        ('INTERN', N'实习生', 4),
        ('ONSITE', N'驻场', 5);
    """,
    # AttendanceType
    """
    IF OBJECT_ID('dbo.AttendanceType', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AttendanceType (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            Code NVARCHAR(20) NOT NULL,
            Name NVARCHAR(50) NOT NULL,
            WorkHours NVARCHAR(50) NULL,
            Remark NVARCHAR(200) NULL,
            SortOrder INT NOT NULL DEFAULT 0,
            IsActive BIT NOT NULL DEFAULT 1,
            CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
            UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
        );
        CREATE UNIQUE INDEX IX_AttendanceType_Code ON dbo.AttendanceType(Code);
        INSERT INTO dbo.AttendanceType (Code, Name, WorkHours, SortOrder) VALUES
            ('DEFAULT', N'默认', N'09:00-18:00', 0),
            ('MORNING', N'早班', N'06:00-14:00', 1),
            ('MIDDLE', N'中班', N'14:00-22:00', 2),
            ('EVENING', N'晚班', N'18:00-02:00', 3),
            ('NIGHT', N'夜班', N'22:00-06:00', 4),
            ('OTHER', N'其他', N'不定期', 5);
    END
    """,
    # EmploymentStatus
    """
    IF OBJECT_ID('dbo.EmploymentStatus', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.EmploymentStatus (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            Code NVARCHAR(20) NOT NULL,
            Name NVARCHAR(50) NOT NULL,
            Remark NVARCHAR(200) NULL,
            SortOrder INT NOT NULL DEFAULT 0,
            IsActive BIT NOT NULL DEFAULT 1,
            CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
            UpdatedAt DATETIME NOT NULL DEFAULT GETDATE()
        );
        CREATE UNIQUE INDEX IX_EmploymentStatus_Code ON dbo.EmploymentStatus(Code);
        INSERT INTO dbo.EmploymentStatus (Code, Name, SortOrder) VALUES
            ('ACTIVE', N'在职', 1),
            ('ONBOARDING', N'待入职', 2),
            ('LEFT', N'已离职', 3);
    END
    """,
    # ResidenceStatus
    """
    IF OBJECT_ID('dbo.ResidenceStatus', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ResidenceStatus (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            Code NVARCHAR(20) NOT NULL,
            Name NVARCHAR(50) NOT NULL,
            Remark NVARCHAR(200) NULL,
            SortOrder INT NOT NULL DEFAULT 0,
            IsActive BIT NOT NULL DEFAULT 1,
            CreatedAt DATETIME NOT NULL DEFAULT DEFAULT GETDATE()
        );
    END
    """,
]


def main():
    try:
        conn = pyodbc.connect(REMOTE_CONN, timeout=30)
        conn.autocommit = True
        cursor = conn.cursor()
        print('连接成功')

        for i, stmt in enumerate(STATEMENTS):
            try:
                cursor.execute(stmt)
                print(f'  [{i}] OK')
            except Exception as e:
                print(f'  [{i}] ERR: {e}')

        cursor.execute("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME")
        tables = [row[0] for row in cursor.fetchall()]
        print(f'\n所有表 ({len(tables)}): {", ".join(tables)}')

        cursor.close()
        conn.close()
        print('完成')
    except Exception as e:
        print(f'错误: {e}')


if __name__ == '__main__':
    main()