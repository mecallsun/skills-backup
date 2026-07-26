using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using DormManage.Shared.Models;

namespace DormManage.Shared.Data;

/// <summary>
/// 数据库上下文
/// </summary>
public class DormDbContext : DbContext
{
    public DormDbContext(DbContextOptions<DormDbContext> options) : base(options) { }

    /// <summary>
    /// 统一审计字段填充：新增时补 CreatedAt/UpdatedAt，修改时刷新 UpdatedAt。
    /// 修复真实 SQL Server 多表 UpdatedAt NOT NULL DEFAULT(GETDATE()) 时 EF 显式写 NULL 导致 INSERT 失败。
    /// </summary>
    private void ApplyAuditStamps()
    {
        var now = DateTime.Now;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now; // v2.13.161：BaseEntity.UpdatedAt 已非空
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }

    public override int SaveChanges()
    {
        ApplyAuditStamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditStamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    #region 基础资料

    /// <summary>
    /// 部门
    /// </summary>
    public DbSet<Department> Departments { get; set; } = null!;

    /// <summary>
    /// 楼栋
    /// </summary>
    public DbSet<Building> Buildings { get; set; } = null!;

    /// <summary>
    /// 楼层
    /// </summary>
    public DbSet<Floor> Floors { get; set; } = null!;

    /// <summary>
    /// 地址
    /// </summary>
    public DbSet<Address> Addresses { get; set; } = null!;

    /// <summary>
    /// 员工类型
    /// </summary>
    public DbSet<EmployeeType> EmployeeTypes { get; set; } = null!;

    /// <summary>
    /// 考勤班次
    /// </summary>
    public DbSet<AttendanceType> AttendanceTypes { get; set; } = null!;

    /// <summary>
    /// 计量单位
    /// </summary>
    public DbSet<MeterUnit> MeterUnits { get; set; } = null!;

    /// <summary>
    /// 住宿状态
    /// </summary>
    public DbSet<ResidenceStatus> ResidenceStatuses { get; set; } = null!;

    /// <summary>
    /// 在职状态
    /// </summary>
    public DbSet<EmploymentStatus> EmploymentStatuses { get; set; } = null!;

    /// <summary>
    /// 班组
    /// </summary>
    public DbSet<Team> Teams { get; set; } = null!;

    /// <summary>
    /// v2.13.120 设备档案（与 Dorm 1:1，电表/冷水表/热水表 ID）
    /// </summary>
    public DbSet<DormMeter> DormMeters { get; set; } = null!;

    #endregion

    #region 业务表

    /// <summary>
    /// 员工
    /// </summary>
    public DbSet<SysEmployee> Employees { get; set; } = null!;

    /// <summary>
    /// 宿舍
    /// </summary>
    public DbSet<Dorm> Dorms { get; set; } = null!;

    /// <summary>
    /// 办理记录
    /// </summary>
    public DbSet<DormBooking> DormBookings { get; set; } = null!;

    /// <summary>
    /// 智能抄表（v2.13.96 重命名；DB 表名仍为 MeterRecord）
    /// </summary>
    public DbSet<MeterRecord> MeterRecords { get; set; } = null!;

    /// <summary>
    /// 设备读数日志（v2.13.130 新增）— 与 DormMeter 配置层 + MeterRecord 聚合层 共同构成「设备-抄表」三层数据模型
    /// </summary>
    public DbSet<EquipmentReading> EquipmentReadings { get; set; } = null!;

    /// <summary>
    /// 费用标准
    /// </summary>
    public DbSet<BillingStandard> BillingStandards { get; set; } = null!;

    /// <summary>
    /// 宿舍月度账单
    /// </summary>
    public DbSet<DormBilling> DormBillings { get; set; } = null!;

    /// <summary>
    /// 员工分摊账单
    /// </summary>
    public DbSet<EmployeeBilling> EmployeeBillings { get; set; } = null!;

    #endregion

    #region 认证权限

    public DbSet<SysUser> SysUsers { get; set; } = null!;
    public DbSet<SysRole> SysRoles { get; set; } = null!;
    public DbSet<SysUserRole> SysUserRoles { get; set; } = null!;
    public DbSet<SysPermission> SysPermissions { get; set; } = null!;
    public DbSet<SysRolePermission> SysRolePermissions { get; set; } = null!;
    public DbSet<SysFieldPermission> SysFieldPermissions { get; set; } = null!;  // v2.13.92 新增：字段权限清单
    public DbSet<PdaDevice> PdaDevices { get; set; } = null!;
    public DbSet<MeterImage> MeterImages { get; set; } = null!;
    public DbSet<SysConfig> SysConfigs { get; set; } = null!;
    public DbSet<SysUserFilterCache> SysUserFilterCaches { get; set; } = null!;
    public DbSet<AppVersion> AppVersions { get; set; } = null!;
    public DbSet<SysIntegration> SysIntegrations { get; set; } = null!;
    public DbSet<SysOpLog> SysOpLogs { get; set; } = null!;
    public DbSet<SysSystemIntegration> SysSystemIntegrations { get; set; } = null!;

    /// <summary>系统参数表（v2.13.19 数据库连接持久化）</summary>
    public DbSet<SysParameter> SysParameters { get; set; } = null!;

    /// <summary>用户安全问题表（v2.13.26 密码找回）</summary>
    public DbSet<SysUserSecurityQuestion> SysUserSecurityQuestions { get; set; } = null!;

    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // v2.13.162：BaseEntity.IsActive 对 EquipmentReading 表必须 Ignore（其 DB 表无该列）
        modelBuilder.Entity<EquipmentReading>().Ignore(e => e.IsActive);

        // Department
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Department");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();  // v2.13.161：实际 DB NON-IDENTITY
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Building
        modelBuilder.Entity<Building>(entity =>
        {
            entity.ToTable("Building");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Floor
        modelBuilder.Entity<Floor>(entity =>
        {
            entity.ToTable("Floor");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.FloorNo).IsUnique();
        });

        // Address
        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("Address");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AddressText).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.AddressText).IsUnique();
        });

        // EmployeeType
        modelBuilder.Entity<EmployeeType>(entity =>
        {
            entity.ToTable("EmployeeType");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();  // v2.13.161：实际 DB NON-IDENTITY
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SortOrder).HasDefaultValue(0);  // v2.13.61 补充 EF 映射
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // AttendanceType
        modelBuilder.Entity<AttendanceType>(entity =>
        {
            entity.ToTable("AttendanceType");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();  // v2.13.161：实际 DB NON-IDENTITY
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.WorkHours).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // MeterUnit
        modelBuilder.Entity<MeterUnit>(entity =>
        {
            entity.ToTable("MeterUnit");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // ResidenceStatus
        modelBuilder.Entity<ResidenceStatus>(entity =>
        {
            entity.ToTable("ResidenceStatus");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // EmploymentStatus
        modelBuilder.Entity<EmploymentStatus>(entity =>
        {
            entity.ToTable("EmploymentStatus");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // SysEmployee
        modelBuilder.Entity<SysEmployee>(entity =>
        {
            entity.ToTable("SysEmployee");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("EmployeeId"); // 真实 SQL Server 主键列名
            entity.Property(e => e.EmployeeTypeText).HasColumnName("EmployeeType").HasMaxLength(64); // 真实冗余列 EmployeeType(nvarchar)
            entity.Property(e => e.EmployeeCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.RealName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Department).HasMaxLength(50);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.DormCode).HasMaxLength(20);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.EmployeeCode).IsUnique();
            // v2.11.18 新增：在职状态 FK 关联引用基础资料-在职状态表
            entity.HasOne(e => e.EmploymentStatus)
                  .WithMany()
                  .HasForeignKey(e => e.EmploymentStatusId)
                  .OnDelete(DeleteBehavior.Restrict);
            // v2.11.20 新增：住宿状态 FK 关联引用基础资料-住宿状态表
            entity.HasOne(e => e.ResidenceStatus)
                  .WithMany()
                  .HasForeignKey(e => e.ResidenceStatusId)
                  .OnDelete(DeleteBehavior.Restrict);
            // v2.13.78 BUG 修复：班组 FK 关联引用基础资料-班组表 Team（之前 SysEmployee.Team 字符串字段被 Ignore，DTO 直接读永远为 null）
            entity.HasOne(e => e.Team)
                  .WithMany()
                  .HasForeignKey(e => e.TeamId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Dorm
        modelBuilder.Entity<Dorm>(entity =>
        {
            entity.ToTable("Dorm");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("DormId"); // 真实 SQL Server 主键列名
            entity.Property(e => e.DormCode).HasMaxLength(32).IsRequired();
            // v2.13.24 P0-2 新增：9 列 PDA 扫码抄表关键字段
            entity.Property(e => e.Building).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Floor).HasMaxLength(16).IsRequired();
            entity.Property(e => e.RoomNo).HasMaxLength(16).IsRequired();
            entity.Property(e => e.DormAddress).HasMaxLength(128).IsRequired();
            entity.Property(e => e.DormType).HasMaxLength(16).IsRequired();
            entity.Property(e => e.Barcode).HasMaxLength(64).IsRequired();
            entity.Property(e => e.HasColdMeter).HasDefaultValue(true);
            entity.Property(e => e.HasHotMeter).HasDefaultValue(true);
            entity.Property(e => e.HasElectricMeter).HasDefaultValue(true);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            // 原字段保留
            entity.Property(e => e.BuildingName).HasMaxLength(50);
            entity.Property(e => e.AddressText).HasMaxLength(200);
            entity.Property(e => e.BedNumbers).HasMaxLength(1000);
            entity.Property(e => e.Remark).HasMaxLength(256);
            // v2.13.24 P77：抄表相关冗余字段
            entity.Property(e => e.LastReadMonth).HasMaxLength(7);
            entity.Property(e => e.LastColdMeter).HasColumnType("decimal(12,2)");
            entity.Property(e => e.LastHotMeter).HasColumnType("decimal(12,2)");
            entity.Property(e => e.LastElectricMeter).HasColumnType("decimal(12,2)");
            entity.HasIndex(e => e.DormCode).IsUnique();
            entity.HasIndex(e => e.Barcode).IsUnique();  // v2.13.24 新增
        });

        // DormBooking
        modelBuilder.Entity<DormBooking>(entity =>
        {
            entity.ToTable("DormBooking");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("BookingId"); // 真实 SQL Server 主键列名
            entity.Property(e => e.Type).HasColumnName("BookingType").HasConversion<byte>(); // 真实列名 BookingType，TINYINT
            entity.Property(e => e.Status).HasConversion<byte>(); // TINYINT
            // v2.13.59 P0 BUG 修复：移除 EmployeeCode/EmployeeName/DormCode/Registrar 的 IsRequired()
            // 兼容生产数据库历史 NULL 脏数据（EF Core 物化 NULL → string 时抛 SqlNullValueException）
            // FK_DormBooking_Employee + FK_DormBooking_Dorm + CheckInOperator 业务校验保证新建数据合法
            entity.Property(e => e.EmployeeCode).HasMaxLength(64);
            entity.Property(e => e.EmployeeName).HasMaxLength(128);
            entity.Property(e => e.Phone).HasMaxLength(32);
            entity.Property(e => e.Department).HasMaxLength(128);
            entity.Property(e => e.DormCode).HasMaxLength(64);
            entity.Property(e => e.Reason).HasMaxLength(512);  // v2.13.59: Reason 原本 string? 但 IsRequired 与之冲突，同步修复
            entity.Property(e => e.Remark).HasMaxLength(1024);
            entity.Property(e => e.Registrar).HasMaxLength(64);
            // v2.13.24 P75 业务深度字段映射
            entity.Property(e => e.MoveFromDormCode).HasMaxLength(32);
            entity.Property(e => e.CancellationReason).HasMaxLength(512);
            entity.Property(e => e.CheckInOperator).HasMaxLength(64);
            entity.Property(e => e.CheckOutOperator).HasMaxLength(64);
            // v2.13.31: 移除自定义转换 - EF Core 8.0.10+ 已修复 DateOnly/DATE 映射
            // v2.13.24 P75 新增索引
            entity.HasIndex(e => new { e.EmployeeId, e.BookingDate });
            entity.HasIndex(e => new { e.DormCode, e.BookingDate });
            entity.HasIndex(e => new { e.Status, e.BookingDate });  // 列表筛选优化
            entity.HasIndex(e => e.EmployeeId).HasDatabaseName("IX_DormBooking_EmpStatus");
        });

        // MeterRecord
        modelBuilder.Entity<MeterRecord>(entity =>
        {
            entity.ToTable("MeterRecord");
            entity.HasKey(e => e.Id);
            // v2.13.79 修复：MeterRecord.RecordId 是 SQL BIGINT，EF 模型改 long Id + [Column("RecordId")] 映射
            // 原 HasColumnType("int") + Id=int 导致 EF 物化 Int64 → Int32 cast 失败 → /Meter 列表 Error
            // 不再需要 HasColumnName("RecordId").HasColumnType("int")，属性上的 [Column("RecordId")] 自动处理
            entity.Property(e => e.DormCode).HasMaxLength(32).IsRequired();
            // v2.13.24 P76：三表读数对齐 SQL DECIMAL(12,2)
            entity.Property(e => e.ColdMeter).HasColumnType("decimal(12,2)");
            entity.Property(e => e.HotMeter).HasColumnType("decimal(12,2)");
            entity.Property(e => e.ElectricMeter).HasColumnType("decimal(12,2)");
            // v2.13.24 P76：三表用量（SQL NOT NULL 完全缺失）
            entity.Property(e => e.ColdUsage).HasColumnType("decimal(12,2)").IsRequired();
            entity.Property(e => e.HotUsage).HasColumnType("decimal(12,2)").IsRequired();
            entity.Property(e => e.ElectricUsage).HasColumnType("decimal(12,2)").IsRequired();
            // v2.13.24 P76：上月读数参考
            entity.Property(e => e.PreviousColdReading).HasColumnType("decimal(12,2)");
            entity.Property(e => e.PreviousHotReading).HasColumnType("decimal(12,2)");
            entity.Property(e => e.PreviousElectricReading).HasColumnType("decimal(12,2)");
            // v2.13.24 P76：字段长度对齐 SQL NVARCHAR(64/128/512)
            entity.Property(e => e.Operator).HasMaxLength(64).IsRequired();
            // v2.13.80 修正：DB schema DeviceSn/ClientRecordId 都是 NVARCHAR(64)，原 HasMaxLength(128) 与 DB 不一致
            // 注意：虽然 DB NOT NULL，但 EF 模型保留 string?，由 Service 层（MeterController.SaveRecord + EntryModel.OnPostSaveReadingsAsync）保证非空写入
            entity.Property(e => e.DeviceSn).HasMaxLength(64);
            entity.Property(e => e.ClientRecordId).HasMaxLength(64);
            entity.Property(e => e.Remark).HasMaxLength(512);
            // v2.13.24 P76：业务深度字段
            entity.Property(e => e.CorrectionReason).HasMaxLength(512);
            entity.Property(e => e.CorrectedBy).HasMaxLength(64);
            // v2.13.24 P76：索引
            entity.HasIndex(e => new { e.DormCode, e.ReadMonth }).IsUnique().HasDatabaseName("UX_MeterRecord_DormMonth");
            entity.HasIndex(e => new { e.DeviceSn, e.ClientRecordId }).HasDatabaseName("IX_MeterRecord_ClientId");
            entity.HasIndex(e => e.ServerCreatedAt).HasDatabaseName("IX_MeterRecord_ServerCreatedAt");
            entity.HasIndex(e => new { e.ReadMonth, e.Operator }).HasDatabaseName("IX_MeterRecord_ReadMonth_Operator");
            entity.Property(e => e.ReadMonth).HasMaxLength(7).IsRequired();
            entity.Property(e => e.Operator).HasMaxLength(50);
            entity.Property(e => e.DeviceSn).HasMaxLength(50);
            entity.Property(e => e.ClientRecordId).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(1000);
            // EF Core 8.0.10+ 自动处理 DateOnly/DATE 映射，无需自定义转换
            entity.HasIndex(e => new { e.DormCode, e.ReadMonth }).IsUnique();
            entity.HasIndex(e => e.ReadMonth);
        });

        // Team（班组，真实表 Team，主键 Id）
        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("Team");
            entity.HasKey(e => e.Id);
            // v2.13.161：实际 DB Team.Id NON-IDENTITY（seed via EF HasData 用 1-11 显式 Id 注入）
            // 显式声明客户端提供，禁止 EF 试图用 IDENTITY 默认
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(20);
            // v2.13.161：Note — DB Team 表有 Remark 列但 EF Team 模型无该属性（已知遗留问题；不在本次修复）
        });

        // ===== v2.13.120 设备档案（与 Dorm 1:1） =====
        modelBuilder.Entity<DormMeter>(entity =>
        {
            entity.ToTable("DormMeter");
            entity.HasKey(e => e.Id);
            // v2.13.168：实际 DB DormMeterId NON-IDENTITY（seed 通过 HasData 用 Id=1 显式插入），
            // 禁止 EF 试图用 IDENTITY 默认（与 Team/Dict 修复同根因）
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Id).HasColumnName("DormMeterId");
            entity.Property(e => e.DormId).IsRequired();
            // v2.13.120 关键：DormId UNIQUE 约束，强制 1:1 关系
            entity.HasIndex(e => e.DormId).IsUnique().HasDatabaseName("UX_DormMeter_DormId");
            // FK → Dorm.DormId，删除 Dorm 时级联清理 DormMeter
            entity.HasOne(e => e.Dorm)
                  .WithMany()
                  .HasForeignKey(e => e.DormId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.ElectricMeterId).HasMaxLength(64);
            entity.Property(e => e.ColdWaterMeterId).HasMaxLength(64);
            entity.Property(e => e.HotWaterMeterId).HasMaxLength(64);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        });

        // ===== v2.13.130 设备读数日志（与 DormMeter + MeterRecord 解耦，独立日志表） =====
        modelBuilder.Entity<EquipmentReading>(entity =>
        {
            entity.ToTable("EquipmentReading");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ReadingId");
            entity.Property(e => e.EquipmentId).HasMaxLength(64).IsRequired();
            // v2.13.130 索引：支持「按设备查最新读数」「按时间段批量删除」两个高频查询
            entity.HasIndex(e => e.EquipmentId).HasDatabaseName("IX_EquipmentReading_EquipmentId");
            entity.HasIndex(e => e.ReadTime).HasDatabaseName("IX_EquipmentReading_ReadTime");
            entity.HasIndex(e => new { e.EquipmentType, e.ReadTime }).HasDatabaseName("IX_EquipmentReading_Type_Time");
            entity.Property(e => e.Reading).HasColumnType("decimal(12,2)");
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        });

        // ===== 费用管理实体 =====
        modelBuilder.Entity<BillingStandard>(entity =>
        {
            entity.ToTable("BillingStandard");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StandardName).HasMaxLength(100).IsRequired();
            // v2.13.61 修复：适用员工类型改为 FK 关联 EmployeeType.Id
            entity.Property(e => e.ApplicableTypeId).IsRequired();
            entity.HasOne(e => e.ApplicableTypeNav)
                  .WithMany()
                  .HasForeignKey(e => e.ApplicableTypeId)
                  .OnDelete(DeleteBehavior.Restrict);  // 禁止删除已被引用的员工类型
            entity.Property(e => e.ApplicableType).HasMaxLength(40).IsRequired();  // 冗余 Name 字段
            // v2.13.24 P0-3 修复：EF Property 名与 SQL 列名对齐（HasColumnName）
            entity.Property(e => e.HotWaterUnitPrice).HasColumnName("HotWaterPrice").HasColumnType("decimal(10,2)");
            entity.Property(e => e.ColdWaterUnitPrice).HasColumnName("ColdWaterPrice").HasColumnType("decimal(10,2)");
            entity.Property(e => e.ElectricUnitPrice).HasColumnName("ElectricityPrice").HasColumnType("decimal(10,2)");
            // v2.13.93 新增：每员工类型每月补贴标准（元/人·月），DDL: ALTER TABLE BillingStandard ADD SubsidyAmount DECIMAL(12,2) NOT NULL DEFAULT 0
            entity.Property(e => e.SubsidyAmount).HasColumnType("decimal(12,2)").HasDefaultValue(0m);
            // v2.13.24 P0-3 新增：UpdatedAt 列
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
            // EF Core 8.0.10+ 自动处理 DateOnly/DATE 映射，无需自定义转换
            entity.Property(e => e.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<DormBilling>(entity =>
        {
            entity.ToTable("DormBilling");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DormCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.BillingMonth).HasMaxLength(7).IsRequired();
            entity.HasIndex(e => new { e.DormCode, e.BillingMonth }).IsUnique();
            entity.Property(e => e.ColdUsage).HasColumnType("decimal(10,2)");
            entity.Property(e => e.HotUsage).HasColumnType("decimal(10,2)");
            entity.Property(e => e.ElectricityUsage).HasColumnType("decimal(10,2)");
            entity.Property(e => e.ColdAmount).HasColumnType("decimal(12,2)");
            entity.Property(e => e.HotAmount).HasColumnType("decimal(12,2)");
            entity.Property(e => e.ElectricityAmount).HasColumnType("decimal(12,2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(12,2)");
        });

        modelBuilder.Entity<EmployeeBilling>(entity =>
        {
            entity.ToTable("EmployeeBilling");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmployeeCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.EmployeeName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.BillingMonth).HasMaxLength(7).IsRequired();
            entity.HasIndex(e => new { e.EmployeeId, e.BillingMonth }).IsUnique();
            entity.Property(e => e.ShareRatio).HasColumnType("decimal(5,4)");
            entity.Property(e => e.ColdShareAmount).HasColumnType("decimal(12,2)");
            entity.Property(e => e.HotShareAmount).HasColumnType("decimal(12,2)");
            entity.Property(e => e.ElectricityShareAmount).HasColumnType("decimal(12,2)");
            entity.Property(e => e.TotalShareAmount).HasColumnType("decimal(12,2)");
            // v2.13.93 新增：本月实际补贴金额（元，按入住天数折算后），DDL: ALTER TABLE EmployeeBilling ADD SubsidyAmount DECIMAL(12,2) NOT NULL DEFAULT 0
            entity.Property(e => e.SubsidyAmount).HasColumnType("decimal(12,2)").HasDefaultValue(0m);
            // v2.13.93 同步 SQL init_schema.sql:499 (Days)
            entity.Property(e => e.Days).HasDefaultValue(0);
            // v2.13.93 同步 SQL init_schema.sql:496 (Department NVARCHAR(128))
            entity.Property(e => e.Department).HasMaxLength(128);
        });

        // ===== v2.13.7 RBAC 实体与真实 SQL Server schema 对齐 =====
        // SysUser（真实表 SysUser，主键 UserId）
        modelBuilder.Entity<SysUser>(entity =>
        {
            entity.ToTable("SysUser");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("UserId");
            entity.Property(e => e.UserName).HasColumnName("Username").HasMaxLength(32).IsRequired();
            entity.Property(e => e.Phone).HasColumnName("Mobile").HasMaxLength(16);       // 真实列名 Mobile
            entity.Property(e => e.LastLoginTime).HasColumnName("LastLoginAt");           // 真实列名 LastLoginAt
            entity.Property(e => e.WeChatOpenId).HasMaxLength(64);
            entity.Property(e => e.PasswordResetToken).HasMaxLength(128);
            entity.Ignore(e => e.EmployeeId);  // 真实表无此列
            entity.Ignore(e => e.UpdatedAt);   // 真实表无此列（代码仅赋值不查询，忽略即可）
            // v2.13.93 新增：账号有效期至，DDL: ALTER TABLE SysUser ADD ExpiresAt DATETIME NULL
            entity.Property(e => e.ExpiresAt).HasColumnType("datetime");
            entity.HasIndex(e => e.UserName).IsUnique();
            entity.HasIndex(e => e.WeChatOpenId).IsUnique().HasFilter(null);  // SQL Server: WHERE WeChatOpenId IS NOT NULL
        });

        // SysUserSecurityQuestion（v2.13.26 密码找回 - 安全问题表）
        modelBuilder.Entity<SysUserSecurityQuestion>(entity =>
        {
            entity.ToTable("SysUserSecurityQuestion");
            entity.HasIndex(e => new { e.UserId, e.QuestionIndex }).IsUnique();
        });

        // SysRole（真实表 SysRole，主键 RoleId）
        modelBuilder.Entity<SysRole>(entity =>
        {
            entity.ToTable("SysRole");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("RoleId");
            entity.Property(e => e.RoleCode).HasMaxLength(32).IsRequired();
            entity.Property(e => e.RoleName).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.RoleCode).IsUnique();
        });

        // SysUserRole（真实表 SysUserRole，复合主键 UserId+RoleId，无 Id 列）
        modelBuilder.Entity<SysUserRole>(entity =>
        {
            entity.ToTable("SysUserRole");
            entity.Ignore(e => e.Id);                    // 真实表无 Id 列
            entity.HasKey(e => new { e.UserId, e.RoleId }); // 复合主键
        });

        // SysPermission（v2.13.7 新增表，列名与实体 1:1，主键 Id）
        modelBuilder.Entity<SysPermission>(entity =>
        {
            entity.ToTable("SysPermission");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PermissionCode).HasMaxLength(64).IsRequired();
            entity.Property(e => e.PermissionName).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.PermissionCode).IsUnique();
        });

        // SysRolePermission（v2.13.7 新增表，列名与实体 1:1，主键 Id）
        modelBuilder.Entity<SysRolePermission>(entity =>
        {
            entity.ToTable("SysRolePermission");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();
        });

        // SysFieldPermission（v2.13.92 新增表：字段权限清单，PII 字段脱敏元数据）
        modelBuilder.Entity<SysFieldPermission>(entity =>
        {
            entity.ToTable("SysFieldPermission");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FieldKey).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Module).HasMaxLength(32).IsRequired();
            entity.Property(e => e.FieldName).HasMaxLength(64).IsRequired();
            entity.Property(e => e.FieldType).HasMaxLength(16);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.UpdatedBy).HasMaxLength(64);
            entity.HasIndex(e => e.FieldKey).IsUnique();
        });

        // SysUserFilterCache（v2.13.12 新增表，用户筛选条件云端缓存）
        modelBuilder.Entity<SysUserFilterCache>(entity =>
        {
            entity.ToTable("SysUserFilterCache");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.ModuleName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FilterJson).HasMaxLength(4000).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.ModuleName }).IsUnique();
            entity.HasIndex(e => e.UpdatedAt);
        });

        // SysParameter（v2.13.19 数据库连接持久化表）
        modelBuilder.Entity<SysParameter>(entity =>
        {
            entity.ToTable("SysParameter");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ParamKey).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ParamValue).HasMaxLength(2000);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.HasIndex(e => new { e.Category, e.ParamKey }).IsUnique();
        });

        // 种子数据
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        // 部门种子数据
        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 1, Code = "PRODUCTION", Name = "生产部", Remark = "主要生产部门", SortOrder = 1, IsActive = true },
            new Department { Id = 2, Code = "TECH", Name = "技术部", Remark = "技术研发部门", SortOrder = 2, IsActive = true },
            new Department { Id = 3, Code = "ADMIN", Name = "行政部", Remark = "", SortOrder = 3, IsActive = true },
            new Department { Id = 4, Code = "FINANCE", Name = "财务部", Remark = "", SortOrder = 4, IsActive = true },
            new Department { Id = 5, Code = "SALES", Name = "销售部", Remark = "", SortOrder = 5, IsActive = true },
            new Department { Id = 6, Code = "LOGISTICS", Name = "后勤部", Remark = "", SortOrder = 6, IsActive = true },
            new Department { Id = 7, Code = "WAREHOUSE", Name = "仓储部", Remark = "", SortOrder = 7, IsActive = true },
            new Department { Id = 8, Code = "OTHER", Name = "其他", Remark = "", SortOrder = 8, IsActive = true }
        );

        // 楼栋种子数据
        modelBuilder.Entity<Building>().HasData(
            new Building { Id = 1, Name = "1号楼", Remark = "", SortOrder = 1, IsActive = true },
            new Building { Id = 2, Name = "2号楼", Remark = "", SortOrder = 2, IsActive = true },
            new Building { Id = 3, Name = "3号楼", Remark = "", SortOrder = 3, IsActive = true },
            new Building { Id = 4, Name = "4号楼", Remark = "", SortOrder = 4, IsActive = true },
            new Building { Id = 5, Name = "5号楼", Remark = "", SortOrder = 5, IsActive = true }
        );

        // 楼层种子数据
        modelBuilder.Entity<Floor>().HasData(
            new Floor { Id = 1, FloorNo = 1, Remark = "", IsActive = true },
            new Floor { Id = 2, FloorNo = 2, Remark = "", IsActive = true },
            new Floor { Id = 3, FloorNo = 3, Remark = "", IsActive = true },
            new Floor { Id = 4, FloorNo = 4, Remark = "", IsActive = true },
            new Floor { Id = 5, FloorNo = 5, Remark = "", IsActive = true },
            new Floor { Id = 6, FloorNo = 6, Remark = "", IsActive = true }
        );

        // 地址种子数据
        modelBuilder.Entity<Address>().HasData(
            new Address { Id = 1, AddressText = "园区A栋", Remark = "", IsActive = true },
            new Address { Id = 2, AddressText = "园区B栋", Remark = "", IsActive = true },
            new Address { Id = 3, AddressText = "园区C栋", Remark = "", IsActive = true }
        );

        // 员工类型种子数据
        modelBuilder.Entity<EmployeeType>().HasData(
            new EmployeeType { Id = 1, Code = "CONTRACT", Name = "合同工", Remark = "", IsActive = true },
            new EmployeeType { Id = 2, Code = "TEMPORARY", Name = "临时工", Remark = "", IsActive = true },
            new EmployeeType { Id = 3, Code = "OUTSOURCE", Name = "外包", Remark = "", IsActive = true },
            new EmployeeType { Id = 4, Code = "INTERN", Name = "实习生", Remark = "", IsActive = true },
            new EmployeeType { Id = 5, Code = "ONSITE", Name = "驻场", Remark = "", IsActive = true }
        );

        // 考勤班次种子数据
        // ========== v2.13.98 简称显示：考勤班次 Name 全部单字化 ==========
        // DEFAULT="默认" / MORNING="早" / MIDDLE="中" / EVENING="晚" / NIGHT="夜" / OTHER="其他"
        modelBuilder.Entity<AttendanceType>().HasData(
            new AttendanceType { Id = 1, Code = "DEFAULT", Name = "默认", WorkHours = "09:00-18:00", Remark = "标准工时", IsActive = true },
            new AttendanceType { Id = 2, Code = "MORNING", Name = "早",   WorkHours = "06:00-14:00", Remark = "", IsActive = true },
            new AttendanceType { Id = 3, Code = "MIDDLE",  Name = "中",   WorkHours = "14:00-22:00", Remark = "", IsActive = true },
            new AttendanceType { Id = 4, Code = "EVENING", Name = "晚",   WorkHours = "18:00-02:00", Remark = "", IsActive = true },
            new AttendanceType { Id = 5, Code = "NIGHT",   Name = "夜",   WorkHours = "22:00-06:00", Remark = "", IsActive = true },
            new AttendanceType { Id = 6, Code = "OTHER",   Name = "其他", WorkHours = "不定期",     Remark = "", IsActive = true }
        );

        // 计量单位种子数据
        modelBuilder.Entity<MeterUnit>().HasData(
            new MeterUnit { Id = 1, Code = "COLD_WATER", Name = "冷水", Unit = "m³", Remark = "", IsActive = true },
            new MeterUnit { Id = 2, Code = "HOT_WATER", Name = "热水", Unit = "m³", Remark = "", IsActive = true },
            new MeterUnit { Id = 3, Code = "ELECTRICITY", Name = "电", Unit = "度", Remark = "", IsActive = true }
        );

        // 住宿状态种子数据
        modelBuilder.Entity<ResidenceStatus>().HasData(
            new ResidenceStatus { Id = 1, Code = "LODGED", Name = "已住宿", Remark = "", IsActive = true },
            new ResidenceStatus { Id = 2, Code = "NOT_LODGED", Name = "未住宿", Remark = "", IsActive = true },
            new ResidenceStatus { Id = 3, Code = "PENDING", Name = "待入住", Remark = "", IsActive = true }
        );

        // 在职状态种子数据
        modelBuilder.Entity<EmploymentStatus>().HasData(
            new EmploymentStatus { Id = 1, Code = "ACTIVE", Name = "在职", Remark = "", IsActive = true },
            new EmploymentStatus { Id = 2, Code = "ONBOARDING", Name = "待入职", Remark = "", IsActive = true },
            new EmploymentStatus { Id = 3, Code = "LEFT", Name = "已离职", Remark = "", IsActive = true }
        );

        // 员工班组种子数据（v2.13.13）
        modelBuilder.Entity<Team>().HasData(
            new Team { Id = 1, Code = "DEFAULT", Name = "默认班组", SortOrder = 0, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new Team { Id = 2, Code = "TEAM_A", Name = "A班", SortOrder = 1, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new Team { Id = 3, Code = "TEAM_B", Name = "B班", SortOrder = 2, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new Team { Id = 4, Code = "TEAM_C", Name = "C班", SortOrder = 3, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new Team { Id = 5, Code = "TEAM_D", Name = "D班", SortOrder = 4, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new Team { Id = 6, Code = "TEAM_E", Name = "E班", SortOrder = 5, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new Team { Id = 7, Code = "TEAM_F", Name = "F班", SortOrder = 6, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new Team { Id = 8, Code = "TEAM_G", Name = "G班", SortOrder = 7, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new Team { Id = 9, Code = "TEAM_H", Name = "H班", SortOrder = 8, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") }
        );

        /*
        // 宿舍种子数据（v2.13.19 起由行政宿舍 Excel 导入，不再硬编码）
        modelBuilder.Entity<Dorm>().HasData(
            new Dorm { Id = 1, DormCode = "D-001", BuildingId = 1, BuildingName = "1号楼", FloorId = 1, AddressId = 1, AddressText = "园区A栋", Capacity = 4, Gender = 1, IsActive = true },
            new Dorm { Id = 2, DormCode = "D-002", BuildingId = 1, BuildingName = "1号楼", FloorId = 1, AddressId = 1, AddressText = "园区A栋", Capacity = 4, Gender = 1, IsActive = true },
            new Dorm { Id = 3, DormCode = "D-003", BuildingId = 1, BuildingName = "1号楼", FloorId = 2, AddressId = 1, AddressText = "园区A栋", Capacity = 4, Gender = 1, IsActive = true },
            new Dorm { Id = 4, DormCode = "D-004", BuildingId = 2, BuildingName = "2号楼", FloorId = 1, AddressId = 2, AddressText = "园区B栋", Capacity = 6, Gender = 1, IsActive = true },
            new Dorm { Id = 5, DormCode = "D-005", BuildingId = 2, BuildingName = "2号楼", FloorId = 2, AddressId = 2, AddressText = "园区B栋", Capacity = 6, Gender = 2, IsActive = true }
        );

        // 员工种子数据（v2.13.19 起由行政宿舍 Excel 导入，不再硬编码）
        modelBuilder.Entity<SysEmployee>().HasData(
            new SysEmployee { Id = 1, EmployeeCode = "EMP-2026-001", RealName = "张三", DepartmentId = 1, Department = "生产部", EmployeeTypeId = 1, Phone = "13800000001", EmploymentStatusId = 1, Status = 1, HireDate = DateOnly.Parse("2025-01-15"), DormCode = "D-001", ResidenceStatusId = 1, IsActive = true },
            new SysEmployee { Id = 2, EmployeeCode = "EMP-2026-002", RealName = "李四", DepartmentId = 2, Department = "技术部", EmployeeTypeId = 1, Phone = "13800000002", EmploymentStatusId = 1, Status = 1, HireDate = DateOnly.Parse("2025-02-20"), DormCode = "D-002", ResidenceStatusId = 1, IsActive = true },
            new SysEmployee { Id = 3, EmployeeCode = "EMP-2026-003", RealName = "王五", DepartmentId = 3, Department = "行政部", EmployeeTypeId = 1, Phone = "13800000003", EmploymentStatusId = 1, Status = 1, HireDate = DateOnly.Parse("2024-06-10"), DormCode = "D-003", ResidenceStatusId = 1, IsActive = true },
            new SysEmployee { Id = 4, EmployeeCode = "EMP-2026-004", RealName = "赵六", DepartmentId = 2, Department = "技术部", EmployeeTypeId = 2, Phone = "13800000004", EmploymentStatusId = 1, Status = 1, HireDate = DateOnly.Parse("2025-03-01"), DormCode = "D-004", ResidenceStatusId = 1, IsActive = true },
            new SysEmployee { Id = 5, EmployeeCode = "EMP-2026-005", RealName = "孙七", DepartmentId = 1, Department = "生产部", EmployeeTypeId = 1, Phone = "13800000005", EmploymentStatusId = 1, Status = 1, HireDate = DateOnly.Parse("2024-11-05"), DormCode = "D-001", ResidenceStatusId = 1, IsActive = true },
            new SysEmployee { Id = 6, EmployeeCode = "EMP-2026-006", RealName = "周八", DepartmentId = 4, Department = "财务部", EmployeeTypeId = 1, Phone = "13800000006", EmploymentStatusId = 1, Status = 1, HireDate = DateOnly.Parse("2025-04-15"), DormCode = "D-002", ResidenceStatusId = 1, IsActive = true },
            new SysEmployee { Id = 7, EmployeeCode = "EMP-2026-007", RealName = "吴九", DepartmentId = 5, Department = "销售部", EmployeeTypeId = 3, Phone = "13800000007", EmploymentStatusId = 1, Status = 1, HireDate = DateOnly.Parse("2024-09-20"), DormCode = "D-003", ResidenceStatusId = 1, IsActive = true },
            new SysEmployee { Id = 8, EmployeeCode = "EMP-2026-008", RealName = "郑十", DepartmentId = 2, Department = "技术部", EmployeeTypeId = 1, Phone = "13800000008", EmploymentStatusId = 1, Status = 1, HireDate = DateOnly.Parse("2025-01-08"), DormCode = "D-004", ResidenceStatusId = 1, IsActive = true },
            new SysEmployee { Id = 9, EmployeeCode = "EMP-2026-009", RealName = "钱一", DepartmentId = 6, Department = "后勤部", EmployeeTypeId = 1, Phone = "13800000009", EmploymentStatusId = 1, Status = 1, HireDate = DateOnly.Parse("2024-12-01"), DormCode = "D-005", ResidenceStatusId = 1, IsActive = true },
            new SysEmployee { Id = 10, EmployeeCode = "EMP-2026-010", RealName = "陈二", DepartmentId = 3, Department = "行政部", EmployeeTypeId = 4, Phone = "13800000010", EmploymentStatusId = 2, Status = 2, HireDate = DateOnly.Parse("2026-08-01"), ResidenceStatusId = 2, IsActive = true }
        );

        // 办理记录种子数据（v2.13.19 起由行政宿舍 Excel 导入，不再硬编码）
        modelBuilder.Entity<DormBooking>().HasData(
            new DormBooking { Id = 1, EmployeeId = 1, EmployeeCode = "EMP-2026-001", EmployeeName = "张三", Phone = "13800000001", Department = "生产部", DormCode = "D-001", Type = 1, BookingDate = DateOnly.Parse("2025-01-15"), Status = 2, Reason = "入职", RegistrationDate = DateTime.Parse("2025-01-15 10:00:00"), Registrar = "admin", IsActive = true },
            new DormBooking { Id = 2, EmployeeId = 2, EmployeeCode = "EMP-2026-002", EmployeeName = "李四", Phone = "13800000002", Department = "技术部", DormCode = "D-002", Type = 1, BookingDate = DateOnly.Parse("2025-02-20"), Status = 2, Reason = "入职", RegistrationDate = DateTime.Parse("2025-02-20 14:30:00"), Registrar = "admin", IsActive = true },
            new DormBooking { Id = 3, EmployeeId = 3, EmployeeCode = "EMP-2026-003", EmployeeName = "王五", Phone = "13800000003", Department = "行政部", DormCode = "D-003", Type = 1, BookingDate = DateOnly.Parse("2024-06-10"), Status = 2, Reason = "入职", RegistrationDate = DateTime.Parse("2024-06-10 09:15:00"), Registrar = "admin", IsActive = true },
            new DormBooking { Id = 4, EmployeeId = 4, EmployeeCode = "EMP-2026-004", EmployeeName = "赵六", Phone = "13800000004", Department = "技术部", DormCode = "D-004", Type = 1, BookingDate = DateOnly.Parse("2025-03-01"), Status = 2, Reason = "入职", RegistrationDate = DateTime.Parse("2025-03-01 11:00:00"), Registrar = "admin", IsActive = true },
            new DormBooking { Id = 5, EmployeeId = 5, EmployeeCode = "EMP-2026-005", EmployeeName = "孙七", Phone = "13800000005", Department = "生产部", DormCode = "D-001", Type = 1, BookingDate = DateOnly.Parse("2024-11-05"), Status = 2, Reason = "调宿", RegistrationDate = DateTime.Parse("2024-11-05 16:20:00"), Registrar = "admin", IsActive = true },
            new DormBooking { Id = 6, EmployeeId = 6, EmployeeCode = "EMP-2026-006", EmployeeName = "周八", Phone = "13800000006", Department = "财务部", DormCode = "D-002", Type = 1, BookingDate = DateOnly.Parse("2025-04-15"), Status = 2, Reason = "入职", RegistrationDate = DateTime.Parse("2025-04-15 10:45:00"), Registrar = "admin", IsActive = true },
            new DormBooking { Id = 7, EmployeeId = 7, EmployeeCode = "EMP-2026-007", EmployeeName = "吴九", Phone = "13800000007", Department = "销售部", DormCode = "D-003", Type = 1, BookingDate = DateOnly.Parse("2024-09-20"), Status = 2, Reason = "入职", RegistrationDate = DateTime.Parse("2024-09-20 13:30:00"), Registrar = "admin", IsActive = true },
            new DormBooking { Id = 8, EmployeeId = 8, EmployeeCode = "EMP-2026-008", EmployeeName = "郑十", Phone = "13800000008", Department = "技术部", DormCode = "D-004", Type = 1, BookingDate = DateOnly.Parse("2025-01-08"), Status = 2, Reason = "入职", RegistrationDate = DateTime.Parse("2025-01-08 08:00:00"), Registrar = "admin", IsActive = true },
            new DormBooking { Id = 9, EmployeeId = 9, EmployeeCode = "EMP-2026-009", EmployeeName = "钱一", Phone = "13800000009", Department = "后勤部", DormCode = "D-005", Type = 1, BookingDate = DateOnly.Parse("2024-12-01"), Status = 2, Reason = "入职", RegistrationDate = DateTime.Parse("2024-12-01 15:00:00"), Registrar = "admin", IsActive = true },
            new DormBooking { Id = 10, EmployeeId = 1, EmployeeCode = "EMP-2026-001", EmployeeName = "张三", Phone = "13800000001", Department = "生产部", DormCode = "D-001", Type = 2, BookingDate = DateOnly.Parse("2025-06-30"), Status = 3, Reason = "离职", RegistrationDate = DateTime.Parse("2025-06-30 17:00:00"), Registrar = "admin", IsActive = true }
        );
        */

        // 系统角色种子数据
        modelBuilder.Entity<SysRole>().HasData(
            new SysRole { Id = 1, RoleCode = "admin", RoleName = "管理员", Description = "系统超级管理员，拥有全部权限", SortOrder = 0, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRole { Id = 2, RoleCode = "finance", RoleName = "财务", Description = "财务管理角色，可查看费用标准和账单", SortOrder = 1, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRole { Id = 3, RoleCode = "pda_operator", RoleName = "PDA 操作员", Description = "PDA 抄表操作员，仅可访问智能抄表模块", SortOrder = 2, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRole { Id = 4, RoleCode = "viewer", RoleName = "访客", Description = "只读角色，仅可查看首页数据看板", SortOrder = 3, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") }
        );

        // 系统用户种子数据（admin / admin123，密码使用 BCrypt 加密）
        var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123");
        modelBuilder.Entity<SysUser>().HasData(
            new SysUser { Id = 1, UserName = "admin", PasswordHash = adminPasswordHash, DisplayName = "系统管理员", IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") }
        );

        // 用户-角色关联（admin 用户 → 管理员角色）
        modelBuilder.Entity<SysUserRole>().HasData(
            new SysUserRole { UserId = 1, RoleId = 1 } // v2.13.7 复合主键，去除 Id
        );

        // 系统权限种子数据（菜单 + 按钮）
        modelBuilder.Entity<SysPermission>().HasData(
            new SysPermission { Id = 1, PermissionCode = "home:view", PermissionName = "首页看板", PermissionType = 1, ParentId = 0, Route = "/", Icon = "bi-speedometer2", SortOrder = 0, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 2, PermissionCode = "booking:view", PermissionName = "办理登记", PermissionType = 1, ParentId = 0, Route = "/Booking", Icon = "bi-clipboard-check", SortOrder = 1, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 3, PermissionCode = "booking:checkin", PermissionName = "入住办理", PermissionType = 2, ParentId = 2, Route = "/Booking/CheckIn", Icon = "bi-box-arrow-in-right", SortOrder = 2, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 4, PermissionCode = "booking:checkout", PermissionName = "退房办理", PermissionType = 2, ParentId = 2, Route = "/Booking/CheckOut", Icon = "bi-box-arrow-right", SortOrder = 3, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 5, PermissionCode = "dorm:view", PermissionName = "宿舍管理", PermissionType = 1, ParentId = 0, Route = "/Dorms", Icon = "bi-building", SortOrder = 2, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 6, PermissionCode = "dorm:create", PermissionName = "新增宿舍", PermissionType = 2, ParentId = 5, Route = "/Dorms/Create", Icon = "", SortOrder = 4, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 7, PermissionCode = "dorm:edit", PermissionName = "编辑宿舍", PermissionType = 2, ParentId = 5, Route = "/Dorms/Edit", Icon = "", SortOrder = 5, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 8, PermissionCode = "dorm:delete", PermissionName = "删除宿舍", PermissionType = 2, ParentId = 5, Route = "", Icon = "", SortOrder = 6, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 9, PermissionCode = "personnel:view", PermissionName = "人员清单", PermissionType = 1, ParentId = 0, Route = "/Personnel", Icon = "bi-people", SortOrder = 3, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 10, PermissionCode = "personnel:import", PermissionName = "导入员工", PermissionType = 2, ParentId = 9, Route = "/Personnel/Import", Icon = "bi-upload", SortOrder = 7, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 11, PermissionCode = "billing:view", PermissionName = "费用标准", PermissionType = 1, ParentId = 0, Route = "/BillingStandard", Icon = "bi-currency-dollar", SortOrder = 4, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 12, PermissionCode = "dormbilling:view", PermissionName = "宿舍账单", PermissionType = 1, ParentId = 0, Route = "/DormBilling", Icon = "bi-receipt", SortOrder = 5, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 13, PermissionCode = "employeebilling:view", PermissionName = "员工账单", PermissionType = 1, ParentId = 0, Route = "/EmployeeBilling", Icon = "bi-wallet2", SortOrder = 6, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 14, PermissionCode = "meter:view", PermissionName = "智能抄表", PermissionType = 1, ParentId = 0, Route = "/Meter", Icon = "bi-gauge", SortOrder = 7, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 15, PermissionCode = "meter:entry", PermissionName = "手动录入", PermissionType = 2, ParentId = 14, Route = "/Meter/Entry", Icon = "bi-pencil", SortOrder = 8, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 16, PermissionCode = "meter:import", PermissionName = "批量导入", PermissionType = 2, ParentId = 14, Route = "/Meter/Import", Icon = "bi-upload", SortOrder = 9, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 17, PermissionCode = "basics:view", PermissionName = "基础资料", PermissionType = 1, ParentId = 0, Route = "/Basics", Icon = "bi-database", SortOrder = 8, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 18, PermissionCode = "settings:view", PermissionName = "系统设置", PermissionType = 1, ParentId = 0, Route = "/Settings", Icon = "bi-gear", SortOrder = 9, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            // ========== v2.13.88 按钮权限细化（用户反馈：列表内按钮权限独立） ==========
            new SysPermission { Id = 19, PermissionCode = "booking:edit", PermissionName = "修改办理登记", PermissionType = 2, ParentId = 2, Route = "/Booking/Edit", Icon = "", SortOrder = 10, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 20, PermissionCode = "booking:cancel", PermissionName = "撤销办理登记", PermissionType = 2, ParentId = 2, Route = "", Icon = "", SortOrder = 11, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 21, PermissionCode = "dorm:detail", PermissionName = "查看宿舍详情", PermissionType = 2, ParentId = 5, Route = "/Dorms/Details", Icon = "", SortOrder = 12, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 22, PermissionCode = "personnel:edit", PermissionName = "编辑员工", PermissionType = 2, ParentId = 9, Route = "/Personnel/Edit", Icon = "", SortOrder = 13, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 23, PermissionCode = "personnel:markleft", PermissionName = "标记离职", PermissionType = 2, ParentId = 9, Route = "", Icon = "", SortOrder = 14, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 24, PermissionCode = "personnel:delete", PermissionName = "删除员工", PermissionType = 2, ParentId = 9, Route = "", Icon = "", SortOrder = 15, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 25, PermissionCode = "billing:edit", PermissionName = "编辑费用标准", PermissionType = 2, ParentId = 11, Route = "/BillingStandard/Edit", Icon = "", SortOrder = 16, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 26, PermissionCode = "billing:delete", PermissionName = "删除费用标准", PermissionType = 2, ParentId = 11, Route = "", Icon = "", SortOrder = 17, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            // ========== v2.13.88 宿舍账单/员工账单 按钮权限（用户第二轮追加需求） ==========
            new SysPermission { Id = 27, PermissionCode = "dormbilling:generate", PermissionName = "生成宿舍账单", PermissionType = 2, ParentId = 12, Route = "", Icon = "", SortOrder = 18, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 28, PermissionCode = "dormbilling:export", PermissionName = "导出宿舍账单", PermissionType = 2, ParentId = 12, Route = "", Icon = "", SortOrder = 19, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 29, PermissionCode = "employeebilling:generate", PermissionName = "生成分摊账单", PermissionType = 2, ParentId = 13, Route = "", Icon = "", SortOrder = 20, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 30, PermissionCode = "employeebilling:publish", PermissionName = "发布员工账单", PermissionType = 2, ParentId = 13, Route = "", Icon = "", SortOrder = 21, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 31, PermissionCode = "employeebilling:export", PermissionName = "导出员工账单", PermissionType = 2, ParentId = 13, Route = "", SortOrder = 22, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            // ========== v2.13.88 智能抄表 详情/修正/导出 权限（用户第三轮追加需求）；v2.13.96 重命名 ==========
            new SysPermission { Id = 32, PermissionCode = "meter:edit", PermissionName = "修正智能抄表", PermissionType = 2, ParentId = 14, Route = "/Meter/Edit", Icon = "", SortOrder = 23, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 33, PermissionCode = "meter:delete", PermissionName = "删除智能抄表", PermissionType = 2, ParentId = 14, Route = "", Icon = "", SortOrder = 24, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 34, PermissionCode = "meter:export", PermissionName = "导出智能抄表", PermissionType = 2, ParentId = 14, Route = "", Icon = "", SortOrder = 25, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            // ========== v2.13.88 第四轮追加：所有列表页导出按钮统一权限管控 ==========
            new SysPermission { Id = 35, PermissionCode = "booking:export", PermissionName = "导出租住登记", PermissionType = 2, ParentId = 2, Route = "", Icon = "", SortOrder = 26, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysPermission { Id = 36, PermissionCode = "personnel:export", PermissionName = "导出人员清单", PermissionType = 2, ParentId = 9, Route = "", Icon = "", SortOrder = 27, IsActive = true, CreatedAt = DateTime.Parse("2026-07-21") },
            // ========== v2.13.92 字段权限（PermissionType=3 数据权限落地）：3 个权限码 ==========
            new SysPermission { Id = 37, PermissionCode = "settings:fields", PermissionName = "字段权限", PermissionType = 1, ParentId = 18, Route = "/Settings?tab=fields", Icon = "bi-shield-check", SortOrder = 28, IsActive = true, IsSystem = true, Description = "管理敏感字段清单", CreatedAt = DateTime.Parse("2026-07-22") },
            new SysPermission { Id = 38, PermissionCode = "fieldpermission:edit", PermissionName = "编辑字段权限", PermissionType = 2, ParentId = 37, Route = "", Icon = "", SortOrder = 29, IsActive = true, IsSystem = true, Description = "勾选/取消勾选敏感字段", CreatedAt = DateTime.Parse("2026-07-22") },
            new SysPermission { Id = 39, PermissionCode = "privacy:field:enable", PermissionName = "启用隐私字段保护", PermissionType = 3, ParentId = 0, Route = "", Icon = "", SortOrder = 30, IsActive = true, IsSystem = true, Description = "勾选此权限的角色将看不到所有 SysFieldPermission 清单中的字段", CreatedAt = DateTime.Parse("2026-07-22") },
            // ========== v2.13.97 P0 BUG：personnel 子权限补全（用户反馈：缺少「新增」按钮权限） ==========
            new SysPermission { Id = 40, PermissionCode = "personnel:add", PermissionName = "新增人员", PermissionType = 2, ParentId = 9, Route = "/Personnel/Create", Icon = "bi-plus-lg", SortOrder = 7, IsActive = true, CreatedAt = DateTime.Parse("2026-07-22") },
            // ========== v2.13.110 P0 BUG：billing 子权限补全（用户反馈：缺少「新增标准」按钮权限） ==========
            new SysPermission { Id = 41, PermissionCode = "billingstandard:add", PermissionName = "新增费用标准", PermissionType = 2, ParentId = 11, Route = "/BillingStandard/Create", Icon = "bi-plus-lg", SortOrder = 5, IsActive = true, CreatedAt = DateTime.Parse("2026-07-22") },
            // ========== v2.13.120 新增：设备档案（基础资料二级菜单） ==========
            new SysPermission { Id = 42, PermissionCode = "device:view", PermissionName = "查看设备档案", PermissionType = 1, ParentId = 10, Route = "/Basics?tab=device", Icon = "bi-cpu", SortOrder = 31, IsActive = true, CreatedAt = DateTime.Parse("2026-07-23") },
            new SysPermission { Id = 43, PermissionCode = "device:create", PermissionName = "新增设备档案", PermissionType = 2, ParentId = 42, Route = "", Icon = "", SortOrder = 32, IsActive = true, CreatedAt = DateTime.Parse("2026-07-23") },
            new SysPermission { Id = 44, PermissionCode = "device:edit", PermissionName = "修改设备档案", PermissionType = 2, ParentId = 42, Route = "", Icon = "", SortOrder = 33, IsActive = true, CreatedAt = DateTime.Parse("2026-07-23") },
            new SysPermission { Id = 45, PermissionCode = "device:delete", PermissionName = "删除设备档案", PermissionType = 2, ParentId = 42, Route = "", Icon = "", SortOrder = 34, IsActive = true, CreatedAt = DateTime.Parse("2026-07-23") }
        );

        // 角色-权限关联（管理员：全部权限）
        modelBuilder.Entity<SysRolePermission>().HasData(
            new SysRolePermission { Id = 1, RoleId = 1, PermissionId = 1, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 2, RoleId = 1, PermissionId = 2, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 3, RoleId = 1, PermissionId = 3, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 4, RoleId = 1, PermissionId = 4, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 5, RoleId = 1, PermissionId = 5, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 6, RoleId = 1, PermissionId = 6, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 7, RoleId = 1, PermissionId = 7, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 8, RoleId = 1, PermissionId = 8, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 9, RoleId = 1, PermissionId = 9, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 10, RoleId = 1, PermissionId = 10, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 11, RoleId = 1, PermissionId = 11, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 12, RoleId = 1, PermissionId = 12, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 13, RoleId = 1, PermissionId = 13, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 14, RoleId = 1, PermissionId = 14, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 15, RoleId = 1, PermissionId = 15, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 16, RoleId = 1, PermissionId = 16, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 17, RoleId = 1, PermissionId = 17, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 18, RoleId = 1, PermissionId = 18, CreatedAt = DateTime.Parse("2026-07-14") },
            // ========== v2.13.88 admin 新增 8 个按钮权限（Id 19~26） ==========
            new SysRolePermission { Id = 30, RoleId = 1, PermissionId = 19, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 31, RoleId = 1, PermissionId = 20, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 32, RoleId = 1, PermissionId = 21, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 33, RoleId = 1, PermissionId = 22, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 34, RoleId = 1, PermissionId = 23, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 35, RoleId = 1, PermissionId = 24, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 36, RoleId = 1, PermissionId = 25, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 37, RoleId = 1, PermissionId = 26, CreatedAt = DateTime.Parse("2026-07-21") },
            // ========== v2.13.88 admin 第二轮新增 5 个：账单生成/发布/导出 ==========
            new SysRolePermission { Id = 45, RoleId = 1, PermissionId = 27, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 46, RoleId = 1, PermissionId = 28, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 47, RoleId = 1, PermissionId = 29, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 48, RoleId = 1, PermissionId = 30, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 49, RoleId = 1, PermissionId = 31, CreatedAt = DateTime.Parse("2026-07-21") },
            // ========== v2.13.88 admin 第三轮新增 3 个：meter:edit / meter:delete / meter:export ==========
            new SysRolePermission { Id = 50, RoleId = 1, PermissionId = 32, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 51, RoleId = 1, PermissionId = 33, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 52, RoleId = 1, PermissionId = 34, CreatedAt = DateTime.Parse("2026-07-21") },
            // ========== v2.13.88 pda 新增 3 个：PDA 操作员可修正/删除/导出 ==========
            new SysRolePermission { Id = 53, RoleId = 3, PermissionId = 32, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 54, RoleId = 3, PermissionId = 33, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 55, RoleId = 3, PermissionId = 34, CreatedAt = DateTime.Parse("2026-07-21") },
            // ========== v2.13.88 第四轮：admin 导出权限 booking:export + personnel:export ==========
            new SysRolePermission { Id = 56, RoleId = 1, PermissionId = 35, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 57, RoleId = 1, PermissionId = 36, CreatedAt = DateTime.Parse("2026-07-21") },
            // ========== v2.13.92 admin 字段权限（settings:fields / fieldpermission:edit / privacy:field:enable） ==========
            new SysRolePermission { Id = 58, RoleId = 1, PermissionId = 37, CreatedAt = DateTime.Parse("2026-07-22") },
            new SysRolePermission { Id = 59, RoleId = 1, PermissionId = 38, CreatedAt = DateTime.Parse("2026-07-22") },
            new SysRolePermission { Id = 60, RoleId = 1, PermissionId = 39, CreatedAt = DateTime.Parse("2026-07-22") },
            // ========== v2.13.97 admin 新增 personnel:add（用户反馈 P0：缺少「新增人员」按钮权限） ==========
            new SysRolePermission { Id = 61, RoleId = 1, PermissionId = 40, CreatedAt = DateTime.Parse("2026-07-22") },
            // ========== v2.13.110 admin 新增 billingstandard:add（用户反馈 P0：缺少「新增标准」按钮权限） ==========
            new SysRolePermission { Id = 62, RoleId = 1, PermissionId = 41, CreatedAt = DateTime.Parse("2026-07-22") },
            // 财务角色
            new SysRolePermission { Id = 19, RoleId = 2, PermissionId = 1, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 20, RoleId = 2, PermissionId = 11, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 21, RoleId = 2, PermissionId = 12, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 22, RoleId = 2, PermissionId = 13, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 23, RoleId = 2, PermissionId = 17, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 24, RoleId = 2, PermissionId = 18, CreatedAt = DateTime.Parse("2026-07-14") },
            // ========== v2.13.88 finance 新增 2 个：billing:edit + billing:delete ==========
            new SysRolePermission { Id = 38, RoleId = 2, PermissionId = 25, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 39, RoleId = 2, PermissionId = 26, CreatedAt = DateTime.Parse("2026-07-21") },
            // ========== v2.13.88 finance 第二轮新增 5 个：账单生成/发布/导出 ==========
            new SysRolePermission { Id = 40, RoleId = 2, PermissionId = 27, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 41, RoleId = 2, PermissionId = 28, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 42, RoleId = 2, PermissionId = 29, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 43, RoleId = 2, PermissionId = 30, CreatedAt = DateTime.Parse("2026-07-21") },
            new SysRolePermission { Id = 44, RoleId = 2, PermissionId = 31, CreatedAt = DateTime.Parse("2026-07-21") },
            // PDA 操作员
            new SysRolePermission { Id = 25, RoleId = 3, PermissionId = 1, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 26, RoleId = 3, PermissionId = 14, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 27, RoleId = 3, PermissionId = 15, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 28, RoleId = 3, PermissionId = 17, CreatedAt = DateTime.Parse("2026-07-14") },
            // 访客
            new SysRolePermission { Id = 29, RoleId = 4, PermissionId = 1, CreatedAt = DateTime.Parse("2026-07-14") }
        );

        // ========== v2.13.92 SysFieldPermission 默认字段清单（5 个核心敏感字段） ==========
        modelBuilder.Entity<SysFieldPermission>().HasData(
            new SysFieldPermission { Id = 1, FieldKey = "employee.realname",   FieldName = "姓名",     Module = "Personnel", FieldType = "string", SensitivityLevel = 1, SortOrder = 1, IsActive = true, Description = "员工真实姓名（高 PII）",   CreatedAt = DateTime.Parse("2026-07-22") },
            new SysFieldPermission { Id = 2, FieldKey = "employee.phone",      FieldName = "手机号",   Module = "Personnel", FieldType = "string", SensitivityLevel = 1, SortOrder = 2, IsActive = true, Description = "联系电话（高 PII）",       CreatedAt = DateTime.Parse("2026-07-22") },
            new SysFieldPermission { Id = 3, FieldKey = "employee.employeecode", FieldName = "工号",   Module = "Personnel", FieldType = "string", SensitivityLevel = 2, SortOrder = 3, IsActive = true, Description = "公司内唯一标识",         CreatedAt = DateTime.Parse("2026-07-22") },
            new SysFieldPermission { Id = 4, FieldKey = "employee.dormcode",   FieldName = "宿舍房号", Module = "Personnel", FieldType = "string", SensitivityLevel = 2, SortOrder = 4, IsActive = true, Description = "当前入住房号（隐私住址）", CreatedAt = DateTime.Parse("2026-07-22") },
            new SysFieldPermission { Id = 5, FieldKey = "employee.remark",     FieldName = "备注",     Module = "Personnel", FieldType = "string", SensitivityLevel = 2, SortOrder = 5, IsActive = true, Description = "自由文本备注（可能含敏感信息）", CreatedAt = DateTime.Parse("2026-07-22") }
        );

        // 智能抄表种子数据（2026年6月、7月；DB 表名仍为 MeterRecord）
        var currentMonth = DateTime.Now.ToString("yyyy-MM");
        var lastMonth = DateTime.Now.AddMonths(-1).ToString("yyyy-MM");
        modelBuilder.Entity<MeterRecord>().HasData(
            // 2026年6月记录
            new MeterRecord { Id = 1, DormId = 1, DormCode = "D-001", ReadMonth = "2026-06", ColdMeter = 120.50m, HotMeter = 85.30m, ElectricMeter = 350.00m, Operator = "admin", Status = 1, ServerCreatedAt = DateTime.Parse("2026-06-01 00:00:00") },
            new MeterRecord { Id = 2, DormId = 2, DormCode = "D-002", ReadMonth = "2026-06", ColdMeter = 98.20m, HotMeter = 62.40m, ElectricMeter = 280.50m, Operator = "admin", Status = 1, ServerCreatedAt = DateTime.Parse("2026-06-01 00:00:00") },
            new MeterRecord { Id = 3, DormId = 3, DormCode = "D-003", ReadMonth = "2026-06", ColdMeter = 110.00m, HotMeter = 75.60m, ElectricMeter = 310.00m, Operator = "admin", Status = 1, ServerCreatedAt = DateTime.Parse("2026-06-01 00:00:00") },
            new MeterRecord { Id = 4, DormId = 4, DormCode = "D-004", ReadMonth = "2026-06", ColdMeter = 150.30m, HotMeter = 95.80m, ElectricMeter = 420.00m, Operator = "admin", Status = 1, ServerCreatedAt = DateTime.Parse("2026-06-01 00:00:00") },
            new MeterRecord { Id = 5, DormId = 5, DormCode = "D-005", ReadMonth = "2026-06", ColdMeter = 85.00m, HotMeter = 52.20m, ElectricMeter = 220.00m, Operator = "admin", Status = 1, ServerCreatedAt = DateTime.Parse("2026-06-01 00:00:00") },
            // 2026年7月记录（部分未完成）
            new MeterRecord { Id = 6, DormId = 1, DormCode = "D-001", ReadMonth = "2026-07", ColdMeter = 125.80m, HotMeter = 88.50m, ElectricMeter = 365.00m, Operator = "admin", Status = 1, ServerCreatedAt = DateTime.Parse("2026-07-01 00:00:00") },
            new MeterRecord { Id = 7, DormId = 2, DormCode = "D-002", ReadMonth = "2026-07", ColdMeter = 0m, HotMeter = 0m, ElectricMeter = 0m, Operator = "系统自动生成", Status = 0, Remark = "自动生成占位记录", ServerCreatedAt = DateTime.Parse("2026-07-01 00:00:00") },
            new MeterRecord { Id = 8, DormId = 3, DormCode = "D-003", ReadMonth = "2026-07", ColdMeter = 112.50m, HotMeter = 0m, ElectricMeter = 0m, Operator = "admin", Status = 0, Remark = "部分表项待抄", ServerCreatedAt = DateTime.Parse("2026-07-01 00:00:00") },
            new MeterRecord { Id = 9, DormId = 4, DormCode = "D-004", ReadMonth = "2026-07", ColdMeter = 155.00m, HotMeter = 98.30m, ElectricMeter = 435.00m, Operator = "admin", Status = 1, ServerCreatedAt = DateTime.Parse("2026-07-01 00:00:00") },
            new MeterRecord { Id = 10, DormId = 5, DormCode = "D-005", ReadMonth = "2026-07", ColdMeter = 0m, HotMeter = 0m, ElectricMeter = 0m, Operator = "系统自动生成", Status = 0, Remark = "自动生成占位记录", ServerCreatedAt = DateTime.Parse("2026-07-01 00:00:00") }
        );
    }
}
