-- ============================================================
-- v2.13.168 设备档案（DormMeter）设备ID 唯一性 —— 过滤唯一索引
-- 日期：2026-07-26
-- 类型：DDL 索引新增（三层防御第 3 层 —— DB 兜底）
--
-- 背景：
--   设备档案的 电表ID / 冷水表ID / 热水表ID 要求全局唯一。
--   Service 层 BasicsService.CheckDeviceIdUniqueAsync 已保证：
--     ① 同一记录内 3 个 ID 互不重复；
--     ② 跨记录：任一 ID 不与其它记录任一字段重复（全局唯一）。
--   本脚本为「同列内重复」这一最常见场景补 DB 层兜底（防绕过 Service 直连 DB 写入）。
--
--   边界说明：过滤唯一索引只能表达「同列内唯一」，无法表达「跨列全局唯一」
--   （如电表ID 与另一记录冷水ID 相同）——跨列唯一仍由 Service 层保证。
--
-- 幂等 + 安全：
--   每列建索引前双重 guard —— ① 该列无重复值；② 索引不存在。
--   若存在重复数据则跳过建索引 + 打印警告（不阻断），交人工清理后重跑。
-- ============================================================

SET NOCOUNT ON;

-- ------------------------------------------------------------
-- 1. ElectricMeterId 过滤唯一索引
-- ------------------------------------------------------------
IF EXISTS (SELECT ElectricMeterId FROM dbo.DormMeter
           WHERE ElectricMeterId IS NOT NULL
           GROUP BY ElectricMeterId HAVING COUNT(*) > 1)
BEGIN
    PRINT N'⚠ 电表ID 存在重复值，跳过 UX_DormMeter_ElectricMeterId 创建，请先清理以下重复项：';
    SELECT ElectricMeterId AS 重复电表ID, COUNT(*) AS 出现次数
    FROM dbo.DormMeter WHERE ElectricMeterId IS NOT NULL
    GROUP BY ElectricMeterId HAVING COUNT(*) > 1;
END
ELSE IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DormMeter_ElectricMeterId' AND object_id = OBJECT_ID('dbo.DormMeter'))
BEGIN
    CREATE UNIQUE INDEX [UX_DormMeter_ElectricMeterId]
        ON dbo.DormMeter([ElectricMeterId])
        WHERE [ElectricMeterId] IS NOT NULL;
    PRINT N'✓ UX_DormMeter_ElectricMeterId 已创建';
END
ELSE
    PRINT N'⊘ UX_DormMeter_ElectricMeterId 已存在，跳过';
GO

-- ------------------------------------------------------------
-- 2. ColdWaterMeterId 过滤唯一索引
-- ------------------------------------------------------------
IF EXISTS (SELECT ColdWaterMeterId FROM dbo.DormMeter
           WHERE ColdWaterMeterId IS NOT NULL
           GROUP BY ColdWaterMeterId HAVING COUNT(*) > 1)
BEGIN
    PRINT N'⚠ 冷水表ID 存在重复值，跳过 UX_DormMeter_ColdWaterMeterId 创建，请先清理以下重复项：';
    SELECT ColdWaterMeterId AS 重复冷水表ID, COUNT(*) AS 出现次数
    FROM dbo.DormMeter WHERE ColdWaterMeterId IS NOT NULL
    GROUP BY ColdWaterMeterId HAVING COUNT(*) > 1;
END
ELSE IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DormMeter_ColdWaterMeterId' AND object_id = OBJECT_ID('dbo.DormMeter'))
BEGIN
    CREATE UNIQUE INDEX [UX_DormMeter_ColdWaterMeterId]
        ON dbo.DormMeter([ColdWaterMeterId])
        WHERE [ColdWaterMeterId] IS NOT NULL;
    PRINT N'✓ UX_DormMeter_ColdWaterMeterId 已创建';
END
ELSE
    PRINT N'⊘ UX_DormMeter_ColdWaterMeterId 已存在，跳过';
GO

-- ------------------------------------------------------------
-- 3. HotWaterMeterId 过滤唯一索引
-- ------------------------------------------------------------
IF EXISTS (SELECT HotWaterMeterId FROM dbo.DormMeter
           WHERE HotWaterMeterId IS NOT NULL
           GROUP BY HotWaterMeterId HAVING COUNT(*) > 1)
BEGIN
    PRINT N'⚠ 热水表ID 存在重复值，跳过 UX_DormMeter_HotWaterMeterId 创建，请先清理以下重复项：';
    SELECT HotWaterMeterId AS 重复热水表ID, COUNT(*) AS 出现次数
    FROM dbo.DormMeter WHERE HotWaterMeterId IS NOT NULL
    GROUP BY HotWaterMeterId HAVING COUNT(*) > 1;
END
ELSE IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DormMeter_HotWaterMeterId' AND object_id = OBJECT_ID('dbo.DormMeter'))
BEGIN
    CREATE UNIQUE INDEX [UX_DormMeter_HotWaterMeterId]
        ON dbo.DormMeter([HotWaterMeterId])
        WHERE [HotWaterMeterId] IS NOT NULL;
    PRINT N'✓ UX_DormMeter_HotWaterMeterId 已创建';
END
ELSE
    PRINT N'⊘ UX_DormMeter_HotWaterMeterId 已存在，跳过';
GO

-- ------------------------------------------------------------
-- 4. 验证完整性
-- ------------------------------------------------------------
DECLARE @idxCount INT = (
    SELECT COUNT(*) FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.DormMeter')
      AND name IN ('UX_DormMeter_ElectricMeterId', 'UX_DormMeter_ColdWaterMeterId', 'UX_DormMeter_HotWaterMeterId')
);
PRINT N'=== v2.13.168 验证 ===';
PRINT N'DormMeter 设备ID 过滤唯一索引 期望 3 / 实际 ' + CAST(@idxCount AS NVARCHAR(10));
IF @idxCount = 3
    PRINT N'✅ v2.13.168 设备ID 唯一索引完整';
ELSE
    PRINT N'⚠ v2.13.168 设备ID 唯一索引未全部创建（可能因存在重复数据被跳过，请检查上方警告）';
GO
