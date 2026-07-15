# -*- coding: utf-8 -*-
"""
v2.12.43 Schema 修复脚本
1) 创建 BillingStandard 表（费用标准）+ 3 条种子数据
2) 为 MeterRecord 表补齐 CreatedAt / UpdatedAt / IsActive 审计字段（统一 BaseEntity 规范）
幂等：可重复执行。
"""
import pyodbc, sys

CONN = ('DRIVER={SQL Server};SERVER=192.168.1.237;DATABASE=WaterMeterDB;'
        'UID=__DB_USER__;PWD=__DB_PASSWORD__;TrustServerCertificate=yes;')

try:
    conn = pyodbc.connect(CONN, timeout=30)
except Exception as e:
    print("CONN_FAIL:", e); sys.exit(1)
conn.autocommit = True
cur = conn.cursor()

def table_exists(t):
    cur.execute("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME=?", t)
    return cur.fetchone()[0] > 0

def col_exists(t, c):
    cur.execute("SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME=? AND COLUMN_NAME=?", t, c)
    return cur.fetchone()[0] > 0

# ---- 1) BillingStandard 表 ----
if not table_exists('BillingStandard'):
    cur.execute("""
        CREATE TABLE BillingStandard (
            Id               INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            StandardName     NVARCHAR(50)  NOT NULL,
            ApplicableType   NVARCHAR(20)  NOT NULL,
            HotWaterPrice    DECIMAL(10,2) NOT NULL,
            ColdWaterPrice   DECIMAL(10,2) NOT NULL,
            ElectricityPrice DECIMAL(10,2) NOT NULL,
            EffectiveFrom    DATE          NOT NULL,
            EffectiveTo      DATE          NULL,
            IsActive         BIT           NOT NULL DEFAULT 1,
            CreatedAt        DATETIME      NOT NULL DEFAULT GETDATE(),
            UpdatedAt        DATETIME      NULL
        );
    """)
    print("[BillingStandard] 表已创建")
else:
    print("[BillingStandard] 表已存在，跳过建表")

# 种子数据（仅当表为空时插入）
cur.execute("SELECT COUNT(*) FROM BillingStandard")
if cur.fetchone()[0] == 0:
    seeds = [
        ('合同工水电气单价', '合同工', 8.50, 4.20, 1.20, '2026-01-01', None, 1),
        ('临时工水电气单价', '临时工', 10.00, 5.00, 1.50, '2026-01-01', None, 1),
        ('外包人员水电气单价', '外包', 12.00, 6.00, 1.80, '2026-01-01', None, 1),
    ]
    cur.executemany("""
        INSERT INTO BillingStandard
        (StandardName, ApplicableType, HotWaterPrice, ColdWaterPrice, ElectricityPrice, EffectiveFrom, EffectiveTo, IsActive, CreatedAt)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, GETDATE())
    """, seeds)
    print(f"[BillingStandard] 已插入 {len(seeds)} 条种子数据")
else:
    print("[BillingStandard] 已有数据，跳过种子插入")

# ---- 2) MeterRecord 审计字段补齐 ----
for col, ddl in [
    ('CreatedAt', "ALTER TABLE MeterRecord ADD CreatedAt DATETIME NOT NULL DEFAULT GETDATE()"),
    ('UpdatedAt', "ALTER TABLE MeterRecord ADD UpdatedAt DATETIME NULL"),
    ('IsActive',  "ALTER TABLE MeterRecord ADD IsActive BIT NOT NULL DEFAULT 1"),
]:
    if not col_exists('MeterRecord', col):
        cur.execute(ddl)
        print(f"[MeterRecord] 已补列 {col}")
    else:
        print(f"[MeterRecord] 列 {col} 已存在，跳过")

# ---- 验证 ----
cur.execute("SELECT COUNT(*) FROM BillingStandard")
print("BillingStandard 记录数:", cur.fetchone()[0])
cur.execute("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='MeterRecord' AND COLUMN_NAME IN ('CreatedAt','UpdatedAt','IsActive')")
print("MeterRecord 审计列:", [r[0] for r in cur.fetchall()])
conn.close()
print("DONE")
