# -*- coding: utf-8 -*-
"""
数据库结构修复脚本 v2.12.42 — 让 DB Schema 与 EF Core 模型完全对齐

BUG 修复列表：
  BUG #6: 缺失基础资料表
    - Building (楼栋)
    - Floor (楼层)
    - Address (地址)
    - MeterUnit (计量单位)
  BUG #7: Dorm 表字段缺失
    - BuildingId INT / BuildingName NVARCHAR(50)
    - FloorId INT
    - AddressId INT / AddressText NVARCHAR(200)
    - Capacity INT
    - Gender INT
    - BedNumbers NVARCHAR(500)
    - RoomCount INT (兼容)
  BUG #8: SysEmployee.Gender 字段缺失
    - Gender INT DEFAULT 1

数据迁移策略：
  - 新表：直接 CREATE
  - 缺失列：ALTER TABLE ADD（默认 NULL 或默认值，不破坏现有数据）
  - 历史数据映射：
    * Dorm.Building (NVARCHAR) → BuildingId (通过 Name 匹配)
    * Dorm.Capacity ← 解析 DormType 字符串（"4人间"→4）
    * Dorm.Gender ← 默认 1（男）

作者：Claude (MiniMax-M3)
日期：2026-07-14
"""

import pyodbc

REMOTE_CONN = (
    'DRIVER={SQL Server};'
    'SERVER=192.168.1.237;'
    'DATABASE=WaterMeterDB;'
    'UID=__DB_USER__;'
    'PWD=__DB_PASSWORD__;'
)


def print_log(msg, level='INFO'):
    icon = {'INFO': '[INFO]', 'OK': '[OK]', 'WARN': '[WARN]', 'ERR': '[ERR]', 'STEP': '[STEP]'}.get(level, '[INFO]')
    print(f'{icon} {msg}', flush=True)


def table_exists(cursor, name):
    cursor.execute("SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = ?", name)
    return cursor.fetchone() is not None


def column_exists(cursor, table, column):
    cursor.execute("""
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME = ? AND COLUMN_NAME = ?
    """, table, column)
    return cursor.fetchone() is not None


def main():
    print_log('=========================================', 'STEP')
    print_log('数据库结构修复 v2.12.42', 'STEP')
    print_log('=========================================', 'STEP')
    print()

    conn = pyodbc.connect(REMOTE_CONN, timeout=30)
    conn.autocommit = False
    cursor = conn.cursor()

    try:
        # ===== BUG #6: 创建缺失的基础资料表 =====
        print_log('===== BUG #6: 创建缺失的基础资料表 =====', 'STEP')

        if not table_exists(cursor, 'Building'):
            print_log('  创建 Building 表...', 'INFO')
            cursor.execute("""
                CREATE TABLE dbo.Building (
                    Id          INT            IDENTITY(1,1) NOT NULL,
                    Name        NVARCHAR(50)   NOT NULL,
                    Remark      NVARCHAR(200)  NULL,
                    SortOrder   INT            NOT NULL DEFAULT 0,
                    IsActive    BIT            NOT NULL DEFAULT 1,
                    CreatedAt   DATETIME       NOT NULL DEFAULT GETDATE(),
                    UpdatedAt   DATETIME       NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT PK_Building PRIMARY KEY (Id),
                    CONSTRAINT UQ_Building_Name UNIQUE (Name)
                )
            """)
            # 种子数据（与 DormDbContext 中一致）
            cursor.execute("SET IDENTITY_INSERT dbo.Building ON")
            for i, name in enumerate(['1号楼', '2号楼', '3号楼', '4号楼', '5号楼'], start=1):
                cursor.execute("""
                    INSERT INTO dbo.Building (Id, Name, SortOrder, IsActive)
                    VALUES (?, ?, ?, 1)
                """, i, name, i)
            cursor.execute("SET IDENTITY_INSERT dbo.Building OFF")
            print_log('  Building 表创建完成 + 5 条种子数据', 'OK')
        else:
            print_log('  Building 表已存在', 'OK')

        if not table_exists(cursor, 'Floor'):
            print_log('  创建 Floor 表...', 'INFO')
            cursor.execute("""
                CREATE TABLE dbo.Floor (
                    Id          INT            IDENTITY(1,1) NOT NULL,
                    FloorNo     INT            NOT NULL,
                    Remark      NVARCHAR(200)  NULL,
                    SortOrder   INT            NOT NULL DEFAULT 0,
                    IsActive    BIT            NOT NULL DEFAULT 1,
                    CreatedAt   DATETIME       NOT NULL DEFAULT GETDATE(),
                    UpdatedAt   DATETIME       NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT PK_Floor PRIMARY KEY (Id),
                    CONSTRAINT UQ_Floor_FloorNo UNIQUE (FloorNo)
                )
            """)
            cursor.execute("SET IDENTITY_INSERT dbo.Floor ON")
            for i in range(1, 7):
                cursor.execute("""
                    INSERT INTO dbo.Floor (Id, FloorNo, SortOrder, IsActive)
                    VALUES (?, ?, ?, 1)
                """, i, i, i)
            cursor.execute("SET IDENTITY_INSERT dbo.Floor OFF")
            print_log('  Floor 表创建完成 + 6 条种子数据（1-6F）', 'OK')
        else:
            print_log('  Floor 表已存在', 'OK')

        if not table_exists(cursor, 'Address'):
            print_log('  创建 Address 表...', 'INFO')
            cursor.execute("""
                CREATE TABLE dbo.Address (
                    Id            INT            IDENTITY(1,1) NOT NULL,
                    AddressText   NVARCHAR(200)  NOT NULL,
                    Remark        NVARCHAR(200)  NULL,
                    SortOrder     INT            NOT NULL DEFAULT 0,
                    IsActive      BIT            NOT NULL DEFAULT 1,
                    CreatedAt     DATETIME       NOT NULL DEFAULT GETDATE(),
                    UpdatedAt     DATETIME       NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT PK_Address PRIMARY KEY (Id),
                    CONSTRAINT UQ_Address_Text UNIQUE (AddressText)
                )
            """)
            cursor.execute("SET IDENTITY_INSERT dbo.Address ON")
            for i, text in enumerate(['A栋宿舍', 'B栋宿舍'], start=1):
                cursor.execute("""
                    INSERT INTO dbo.Address (Id, AddressText, SortOrder, IsActive)
                    VALUES (?, ?, ?, 1)
                """, i, text, i)
            cursor.execute("SET IDENTITY_INSERT dbo.Address OFF")
            print_log('  Address 表创建完成 + 2 条种子数据（A栋/B栋）', 'OK')
        else:
            print_log('  Address 表已存在', 'OK')

        if not table_exists(cursor, 'MeterUnit'):
            print_log('  创建 MeterUnit 表...', 'INFO')
            cursor.execute("""
                CREATE TABLE dbo.MeterUnit (
                    Id          INT            IDENTITY(1,1) NOT NULL,
                    Code        NVARCHAR(20)   NOT NULL,
                    Name        NVARCHAR(50)   NOT NULL,
                    Unit        NVARCHAR(20)   NULL,
                    Remark      NVARCHAR(200)  NULL,
                    SortOrder   INT            NOT NULL DEFAULT 0,
                    IsActive    BIT            NOT NULL DEFAULT 1,
                    CreatedAt   DATETIME       NOT NULL DEFAULT GETDATE(),
                    UpdatedAt   DATETIME       NOT NULL DEFAULT GETDATE(),
                    CONSTRAINT PK_MeterUnit PRIMARY KEY (Id),
                    CONSTRAINT UQ_MeterUnit_Code UNIQUE (Code)
                )
            """)
            cursor.execute("SET IDENTITY_INSERT dbo.MeterUnit ON")
            for i, (code, name, unit) in enumerate([
                ('COLD_WATER', '冷水', 'm³'),
                ('HOT_WATER', '热水', 'm³'),
                ('ELECTRICITY', '电', '度'),
            ], start=1):
                cursor.execute("""
                    INSERT INTO dbo.MeterUnit (Id, Code, Name, Unit, SortOrder, IsActive)
                    VALUES (?, ?, ?, ?, ?, 1)
                """, i, code, name, unit, i)
            cursor.execute("SET IDENTITY_INSERT dbo.MeterUnit OFF")
            print_log('  MeterUnit 表创建完成 + 3 条种子数据（冷水/热水/电）', 'OK')
        else:
            print_log('  MeterUnit 表已存在', 'OK')

        # ===== BUG #7: 补全 Dorm 表缺失字段 =====
        print_log('===== BUG #7: 补全 Dorm 表缺失字段 =====', 'STEP')

        # 加载楼栋/地址映射
        cursor.execute("SELECT Id, Name FROM Building")
        building_map = {r.Name: r.Id for r in cursor.fetchall()}
        print_log(f'  Building 映射: {building_map}', 'INFO')

        cursor.execute("SELECT Id, FloorNo FROM Floor")
        floor_map = {r.FloorNo: r.Id for r in cursor.fetchall()}
        print_log(f'  Floor 映射: {floor_map}', 'INFO')

        cursor.execute("SELECT Id, AddressText FROM Address")
        addr_map = {r.AddressText: r.Id for r in cursor.fetchall()}
        print_log(f'  Address 映射: {addr_map}', 'INFO')

        # 缺失列逐个添加
        columns_to_add = [
            ('BuildingId', 'INT NULL'),
            ('BuildingName', 'NVARCHAR(50) NULL'),
            ('FloorId', 'INT NULL'),
            ('AddressId', 'INT NULL'),
            ('AddressText', 'NVARCHAR(200) NULL'),
            ('Capacity', 'INT NOT NULL DEFAULT 2'),
            ('Gender', 'INT NOT NULL DEFAULT 1'),
            ('BedNumbers', 'NVARCHAR(500) NULL'),
            ('RoomCount', 'INT NOT NULL DEFAULT 1'),
        ]
        for col_name, col_def in columns_to_add:
            if not column_exists(cursor, 'Dorm', col_name):
                print_log(f'  添加 Dorm.{col_name} ({col_def})', 'INFO')
                cursor.execute(f"ALTER TABLE dbo.Dorm ADD {col_name} {col_def}")
            else:
                print_log(f'  Dorm.{col_name} 已存在', 'OK')

        # 数据迁移：Dorm.Building (NVARCHAR) → Dorm.BuildingId (INT)
        print_log('  迁移 Building → BuildingId...', 'INFO')
        for name, bid in building_map.items():
            cursor.execute("UPDATE dbo.Dorm SET BuildingId = ?, BuildingName = ? WHERE Building = ?",
                           bid, name, name)

        # 数据迁移：Dorm.Floor (NVARCHAR "1F") → Dorm.FloorId (INT)
        print_log('  迁移 Floor (NVARCHAR) → FloorId (INT)...', 'INFO')
        cursor.execute("""
            UPDATE dbo.Dorm
            SET FloorId = CASE
                WHEN Floor = '1F' THEN 1
                WHEN Floor = '2F' THEN 2
                WHEN Floor = '3F' THEN 3
                WHEN Floor = '4F' THEN 4
                WHEN Floor = '5F' THEN 5
                WHEN Floor = '6F' THEN 6
                ELSE 1
            END
            WHERE FloorId IS NULL
        """)

        # 数据迁移：Dorm.DormAddress → Dorm.AddressId/AddressText
        print_log('  迁移 DormAddress → AddressId/AddressText...', 'INFO')
        # 当前 AddressText 都是 A栋宿舍/B栋宿舍（基于 BuildingName）
        cursor.execute("""
            UPDATE dbo.Dorm
            SET AddressText = BuildingName + N'宿舍',
                AddressId = CASE
                    WHEN BuildingName = N'A栋' THEN (SELECT Id FROM Address WHERE AddressText = N'A栋宿舍')
                    WHEN BuildingName = N'B栋' THEN (SELECT Id FROM Address WHERE AddressText = N'B栋宿舍')
                    ELSE 1
                END
            WHERE AddressId IS NULL
        """)

        # 数据迁移：Dorm.DormType (NVARCHAR "4人间") → Dorm.Capacity (INT) + BedNumbers
        print_log('  迁移 DormType → Capacity + BedNumbers...', 'INFO')
        cursor.execute("SELECT DormId, DormType FROM Dorm")
        dorms_to_fix = cursor.fetchall()
        for r in dorms_to_fix:
            dorm_type = r.DormType or '2人间'
            if '单' in dorm_type or '1人' in dorm_type:
                cap = 1
            elif '双' in dorm_type or '2人' in dorm_type:
                cap = 2
            elif '3人' in dorm_type:
                cap = 3
            elif '4人' in dorm_type:
                cap = 4
            elif '6人' in dorm_type:
                cap = 6
            elif '8人' in dorm_type:
                cap = 8
            else:
                cap = 2
            bed_numbers = ','.join(str(i) for i in range(1, cap + 1))
            cursor.execute("""
                UPDATE dbo.Dorm SET Capacity = ?, BedNumbers = ? WHERE DormId = ?
            """, cap, bed_numbers, r.DormId)

        print_log(f'  Dorm 表字段补全 + 数据迁移完成（{len(dorms_to_fix)} 条）', 'OK')

        # ===== BUG #8: 补充 SysEmployee.Gender 列 =====
        print_log('===== BUG #8: 补充 SysEmployee.Gender 列 =====', 'STEP')

        if not column_exists(cursor, 'SysEmployee', 'Gender'):
            cursor.execute("ALTER TABLE dbo.SysEmployee ADD Gender INT NOT NULL DEFAULT 1")
            print_log('  SysEmployee.Gender 列已添加（默认 1=男）', 'OK')
        else:
            print_log('  SysEmployee.Gender 已存在', 'OK')

        # ===== 最终验证 =====
        print_log('===== 最终验证 =====', 'STEP')

        # 重新加载所有表/列检查
        for t in ['Building', 'Floor', 'Address', 'MeterUnit', 'ResidenceStatus']:
            exists = table_exists(cursor, t)
            if exists:
                cursor.execute(f'SELECT COUNT(*) FROM {t}')
                cnt = cursor.fetchone()[0]
                print_log(f'  {t}: {cnt} 条 ✓', 'OK')
            else:
                print_log(f'  {t}: 缺失 ✗', 'ERR')

        cursor.execute("SELECT COUNT(*) FROM Dorm")
        dorm_cnt = cursor.fetchone()[0]
        cursor.execute("SELECT COUNT(*) FROM SysEmployee")
        emp_cnt = cursor.fetchone()[0]
        cursor.execute("SELECT COUNT(*) FROM DormBooking")
        bk_cnt = cursor.fetchone()[0]
        cursor.execute("SELECT COUNT(*) FROM Dorm WHERE Capacity IS NOT NULL AND BuildingId IS NOT NULL")
        dorm_full = cursor.fetchone()[0]

        print_log(f'  SysEmployee: {emp_cnt}', 'OK')
        print_log(f'  Dorm: {dorm_cnt} (其中完整字段 {dorm_full})', 'OK')
        print_log(f'  DormBooking: {bk_cnt}', 'OK')

        conn.commit()
        print_log('', 'OK')
        print_log('=========================================', 'OK')
        print_log('  数据库结构修复完成！', 'OK')
        print_log('=========================================', 'OK')

    except Exception as e:
        conn.rollback()
        print_log(f'修复失败: {e}', 'ERR')
        import traceback
        traceback.print_exc()
    finally:
        cursor.close()
        conn.close()


if __name__ == '__main__':
    main()