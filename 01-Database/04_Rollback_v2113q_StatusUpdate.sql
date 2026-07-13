-- =====================================================================
-- 回滚脚本：v2.11.3(q) 状态统一回滚
-- 说明：将 status=4 回滚为 status=0（仅回滚本次迁移）
-- 日期：2026-07-11
-- 警告：此脚本仅回滚 2026-07-11 的迁移修改！
-- =====================================================================

PRINT '===== 开始回滚迁移 =====';
PRINT '警告：此操作将把带特定标记的 status=4 记录回滚为 status=0';
PRINT '仅回滚包含迁移标记的记录！';
PRINT '';

DECLARE @RollbackCount INT;
BEGIN TRANSACTION;

BEGIN TRY
    -- 仅回滚包含迁移标记的记录
    -- 标记格式：[2026-07-11 系统迁移] status: 0→4
    UPDATE dbo.MeterRecord
    SET Status = 0,
        UpdatedAt = GETDATE(),
        -- 移除迁移标记
        Remark = REPLACE(Remark, CHAR(13) + CHAR(10) + '[2026-07-11 系统迁移] status: 0→4（v2.11.3(q)：删除未抄表状态，统一为未完成）', '')
    WHERE Status = 4
      AND Remark LIKE '%[2026-07-11 系统迁移] status: 0→4%';

    SET @RollbackCount = @@ROWCOUNT;
    PRINT CONCAT('已回滚 ', @RollbackCount, ' 条记录（status=4 → status=0）');

    -- 验证
    PRINT '';
    PRINT '===== 回滚后状态分布 =====';
    SELECT
        CASE Status
            WHEN 0 THEN '0-未抄表(已回滚)'
            WHEN 1 THEN '1-正常'
            WHEN 2 THEN '2-已修正'
            WHEN 3 THEN '3-已作废'
            WHEN 4 THEN '4-未完成(无迁移标记)'
            ELSE CONCAT('未知-', Status)
        END AS StatusDesc,
        COUNT(*) AS RecordCount
    FROM dbo.MeterRecord
    GROUP BY Status
    ORDER BY Status;

    COMMIT TRANSACTION;
    PRINT '';
    PRINT '✓ 回滚成功完成！';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT '';
    PRINT '✗ 回滚失败：' + ERROR_MESSAGE();
    RETURN;
END CATCH;
GO
