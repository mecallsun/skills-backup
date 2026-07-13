-- =====================================================================
-- 数据迁移脚本：v2.11.3(q) 状态统一 + 删除已作废
-- 说明：
--   1. 将 status=0（未抄表）更新为 status=4（未完成）
--   2. 将 status=3（已作废）更新为 status=4（未完成）
-- 日期：2026-07-11
-- 前提：执行前请备份数据库！
-- =====================================================================

PRINT '========================================';
PRINT '  v2.11.3(q) 数据迁移脚本';
PRINT '  执行时间：' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '========================================';
PRINT '';

-- ============================================================
-- 第1步：查看当前状态分布（执行前）
-- ============================================================
PRINT '===== 迁移前状态分布 =====';
SELECT
    CASE Status
        WHEN 0 THEN '0-未抄表(将改为4)'
        WHEN 1 THEN '1-正常'
        WHEN 2 THEN '2-已修正'
        WHEN 3 THEN '3-已作废(将改为4)'
        WHEN 4 THEN '4-未完成'
        ELSE CONCAT('未知-', Status)
    END AS StatusDesc,
    COUNT(*) AS RecordCount
FROM dbo.MeterRecord
GROUP BY Status
ORDER BY Status;

-- ============================================================
-- 第2步：执行迁移
-- ============================================================
PRINT '';
PRINT '===== 开始迁移 =====';

DECLARE @UpdateCount0 INT, @UpdateCount3 INT;
BEGIN TRANSACTION;

BEGIN TRY
    -- 2.1 将 status=0 更新为 status=4
    UPDATE dbo.MeterRecord
    SET Status = 4,
        UpdatedAt = GETDATE(),
        Remark = ISNULL(Remark + CHAR(13) + CHAR(10), '') +
                 '[2026-07-11 系统迁移] status: 0→4（v2.11.3(q)：删除未抄表状态，统一为未完成）'
    WHERE Status = 0;

    SET @UpdateCount0 = @@ROWCOUNT;
    PRINT CONCAT('已更新 ', @UpdateCount0, ' 条记录（status=0 → status=4）');

    -- 2.2 将 status=3 更新为 status=4
    UPDATE dbo.MeterRecord
    SET Status = 4,
        UpdatedAt = GETDATE(),
        Remark = ISNULL(Remark + CHAR(13) + CHAR(10), '') +
                 '[2026-07-11 系统迁移] status: 3→4（v2.11.3(q)：删除已作废状态，统一为未完成，可重新覆盖）'
    WHERE Status = 3;

    SET @UpdateCount3 = @@ROWCOUNT;
    PRINT CONCAT('已更新 ', @UpdateCount3, ' 条记录（status=3 → status=4）');

    -- 验证更新结果
    PRINT '';
    PRINT '===== 迁移后状态分布 =====';
    SELECT
        CASE Status
            WHEN 1 THEN '1-正常'
            WHEN 2 THEN '2-已修正'
            WHEN 4 THEN '4-未完成(含原0和3)'
            ELSE CONCAT('未知-', Status)
        END AS StatusDesc,
        COUNT(*) AS RecordCount
    FROM dbo.MeterRecord
    GROUP BY Status
    ORDER BY Status;

    -- 确认没有遗留的 status=0 或 status=3
    IF EXISTS (SELECT 1 FROM dbo.MeterRecord WHERE Status IN (0, 3))
    BEGIN
        RAISERROR('存在未更新的记录！', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    COMMIT TRANSACTION;
    PRINT '';
    PRINT '✓ 迁移成功完成！';
    PRINT CONCAT('总计更新：', @UpdateCount0 + @UpdateCount3, ' 条记录');

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT '';
    PRINT '✗ 迁移失败：' + ERROR_MESSAGE();
    RETURN;
END CATCH;

-- ============================================================
-- 第3步：更新数据库字段注释
-- ============================================================
PRINT '';
PRINT '===== 更新数据库字段注释 =====';

BEGIN TRY
    EXEC sp_dropextendedproperty
        @name = N'MS_Description',
        @level0type = N'SCHEMA', @level0name = 'dbo',
        @level1type = N'TABLE',  @level1name = 'MeterRecord',
        @level2type = N'COLUMN', @level2name = 'Status';
END TRY
BEGIN CATCH
    -- 忽略错误
END CATCH;

BEGIN TRY
    EXEC sp_addextendedproperty
        @name = N'MS_Description',
        @value = N'4=未完成 1=正常 2=已修正',
        @level0type = N'SCHEMA', @level0name = 'dbo',
        @level1type = N'TABLE',  @level1name = 'MeterRecord',
        @level2type = N'COLUMN', @level2name = 'Status';
    PRINT '✓ Status 字段注释已更新为：4=未完成 1=正常 2=已修正';
END TRY
BEGIN CATCH
    PRINT '✗ 更新注释失败（需要 DBA 权限）：' + ERROR_MESSAGE();
END CATCH;

-- ============================================================
-- 第4步：验证覆盖进度统计
-- ============================================================
PRINT '';
PRINT '===== 验证覆盖进度（最近3个月） =====';

DECLARE @CurrentMonth CHAR(7) = FORMAT(GETDATE(), 'yyyy-MM');
DECLARE @PrevMonth CHAR(7) = FORMAT(DATEADD(MONTH, -1, GETDATE()), 'yyyy-MM');
DECLARE @Prev2Month CHAR(7) = FORMAT(DATEADD(MONTH, -2, GETDATE()), 'yyyy-MM');

PRINT '月份：' + @CurrentMonth;
SELECT
    '已抄(正常/已修正)' = (SELECT COUNT(*) FROM dbo.MeterRecord WHERE ReadMonth = @CurrentMonth AND Status IN (1,2)),
    '未完成' = (SELECT COUNT(*) FROM dbo.MeterRecord WHERE ReadMonth = @CurrentMonth AND Status = 4),
    '启用宿舍总数' = (SELECT COUNT(*) FROM dbo.Dorm WHERE IsActive = 1);

-- ============================================================
-- 迁移完成
-- ============================================================
PRINT '';
PRINT '========================================';
PRINT '  迁移脚本执行完成';
PRINT '========================================';
GO
