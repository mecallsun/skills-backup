using Microsoft.EntityFrameworkCore;
using DormManage.Shared.Models;

namespace DormManage.Shared.Data;

/// <summary>
/// 数据库上下文
/// </summary>
public class DormDbContext : DbContext
{
    public DormDbContext(DbContextOptions<DormDbContext> options) : base(options) { }

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
    /// 抄表记录
    /// </summary>
    public DbSet<MeterRecord> MeterRecords { get; set; } = null!;

    #endregion

    #region 认证权限

    public DbSet<SysUser> SysUsers { get; set; } = null!;
    public DbSet<SysRole> SysRoles { get; set; } = null!;
    public DbSet<SysUserRole> SysUserRoles { get; set; } = null!;
    public DbSet<SysPermission> SysPermissions { get; set; } = null!;
    public DbSet<SysRolePermission> SysRolePermissions { get; set; } = null!;
    public DbSet<PdaDevice> PdaDevices { get; set; } = null!;
    public DbSet<MeterImage> MeterImages { get; set; } = null!;
    public DbSet<SysConfig> SysConfigs { get; set; } = null!;
    public DbSet<SysUserFilterCache> SysUserFilterCaches { get; set; } = null!;
    public DbSet<SysOpLog> SysOpLogs { get; set; } = null!;
    public DbSet<SysSystemIntegration> SysSystemIntegrations { get; set; } = null!;

    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Department
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Department");
            entity.HasKey(e => e.Id);
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
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Remark).HasMaxLength(200);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // AttendanceType
        modelBuilder.Entity<AttendanceType>(entity =>
        {
            entity.ToTable("AttendanceType");
            entity.HasKey(e => e.Id);
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
        });

        // Dorm
        modelBuilder.Entity<Dorm>(entity =>
        {
            entity.ToTable("Dorm");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DormCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.BuildingName).HasMaxLength(50);
            entity.Property(e => e.AddressText).HasMaxLength(200);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.HasIndex(e => e.DormCode).IsUnique();
        });

        // DormBooking
        modelBuilder.Entity<DormBooking>(entity =>
        {
            entity.ToTable("DormBooking");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmployeeCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.EmployeeName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Department).HasMaxLength(50);
            entity.Property(e => e.DormCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(200);
            entity.Property(e => e.Remark).HasMaxLength(500);
            entity.Property(e => e.Registrar).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => new { e.EmployeeId, e.BookingDate });
            entity.HasIndex(e => new { e.DormCode, e.BookingDate });
        });

        // MeterRecord
        modelBuilder.Entity<MeterRecord>(entity =>
        {
            entity.ToTable("MeterRecord");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DormCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ReadMonth).HasMaxLength(7).IsRequired();
            entity.Property(e => e.Operator).HasMaxLength(50);
            entity.Property(e => e.DeviceSn).HasMaxLength(50);
            entity.Property(e => e.ClientRecordId).HasMaxLength(50);
            entity.Property(e => e.Remark).HasMaxLength(1000);
            entity.HasIndex(e => new { e.DormCode, e.ReadMonth }).IsUnique();
            entity.HasIndex(e => e.ReadMonth);
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
        modelBuilder.Entity<AttendanceType>().HasData(
            new AttendanceType { Id = 1, Code = "DEFAULT", Name = "默认", WorkHours = "09:00-18:00", Remark = "标准工时", IsActive = true },
            new AttendanceType { Id = 2, Code = "MORNING", Name = "早班", WorkHours = "06:00-14:00", Remark = "", IsActive = true },
            new AttendanceType { Id = 3, Code = "MIDDLE", Name = "中班", WorkHours = "14:00-22:00", Remark = "", IsActive = true },
            new AttendanceType { Id = 4, Code = "EVENING", Name = "晚班", WorkHours = "18:00-02:00", Remark = "", IsActive = true },
            new AttendanceType { Id = 5, Code = "NIGHT", Name = "夜班", WorkHours = "22:00-06:00", Remark = "", IsActive = true },
            new AttendanceType { Id = 6, Code = "OTHER", Name = "其他", WorkHours = "不定期", Remark = "", IsActive = true }
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

        // 宿舍种子数据
        modelBuilder.Entity<Dorm>().HasData(
            new Dorm { Id = 1, DormCode = "D-001", BuildingId = 1, BuildingName = "1号楼", FloorId = 1, AddressId = 1, AddressText = "园区A栋", Capacity = 4, Gender = 1, IsActive = true },
            new Dorm { Id = 2, DormCode = "D-002", BuildingId = 1, BuildingName = "1号楼", FloorId = 1, AddressId = 1, AddressText = "园区A栋", Capacity = 4, Gender = 1, IsActive = true },
            new Dorm { Id = 3, DormCode = "D-003", BuildingId = 1, BuildingName = "1号楼", FloorId = 2, AddressId = 1, AddressText = "园区A栋", Capacity = 4, Gender = 1, IsActive = true },
            new Dorm { Id = 4, DormCode = "D-004", BuildingId = 2, BuildingName = "2号楼", FloorId = 1, AddressId = 2, AddressText = "园区B栋", Capacity = 6, Gender = 1, IsActive = true },
            new Dorm { Id = 5, DormCode = "D-005", BuildingId = 2, BuildingName = "2号楼", FloorId = 2, AddressId = 2, AddressText = "园区B栋", Capacity = 6, Gender = 2, IsActive = true }
        );

        // 员工种子数据（v2.11.18 新增 EmploymentStatusId FK 字段；v2.11.20 新增 ResidenceStatusId FK 字段）
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

        // 办理记录种子数据
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

        // 系统角色种子数据
        modelBuilder.Entity<SysRole>().HasData(
            new SysRole { Id = 1, RoleCode = "admin", RoleName = "管理员", Description = "系统超级管理员，拥有全部权限", SortOrder = 0, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRole { Id = 2, RoleCode = "finance", RoleName = "财务", Description = "财务管理角色，可查看费用标准和账单", SortOrder = 1, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRole { Id = 3, RoleCode = "pda_operator", RoleName = "PDA 操作员", Description = "PDA 抄表操作员，仅可访问抄表模块", SortOrder = 2, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRole { Id = 4, RoleCode = "viewer", RoleName = "访客", Description = "只读角色，仅可查看首页数据看板", SortOrder = 3, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") }
        );

        // 系统用户种子数据（admin / admin123，密码使用 BCrypt 加密）
        var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123");
        modelBuilder.Entity<SysUser>().HasData(
            new SysUser { Id = 1, UserName = "admin", PasswordHash = adminPasswordHash, DisplayName = "系统管理员", IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") }
        );

        // 用户-角色关联（admin 用户 → 管理员角色）
        modelBuilder.Entity<SysUserRole>().HasData(
            new SysUserRole { Id = 1, UserId = 1, RoleId = 1 }
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
            new SysPermission { Id = 14, PermissionCode = "meter:view", PermissionName = "抄表记录", PermissionType = 1, ParentId = 0, Route = "/Meter", Icon = "bi-gauge", SortOrder = 7, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 15, PermissionCode = "meter:entry", PermissionName = "手动录入", PermissionType = 2, ParentId = 14, Route = "/Meter/Entry", Icon = "bi-pencil", SortOrder = 8, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 16, PermissionCode = "meter:import", PermissionName = "批量导入", PermissionType = 2, ParentId = 14, Route = "/Meter/Import", Icon = "bi-upload", SortOrder = 9, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 17, PermissionCode = "basics:view", PermissionName = "基础资料", PermissionType = 1, ParentId = 0, Route = "/Basics", Icon = "bi-database", SortOrder = 8, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysPermission { Id = 18, PermissionCode = "settings:view", PermissionName = "系统设置", PermissionType = 1, ParentId = 0, Route = "/Settings", Icon = "bi-gear", SortOrder = 9, IsActive = true, CreatedAt = DateTime.Parse("2026-07-14") }
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
            // 财务角色
            new SysRolePermission { Id = 19, RoleId = 2, PermissionId = 1, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 20, RoleId = 2, PermissionId = 11, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 21, RoleId = 2, PermissionId = 12, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 22, RoleId = 2, PermissionId = 13, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 23, RoleId = 2, PermissionId = 17, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 24, RoleId = 2, PermissionId = 18, CreatedAt = DateTime.Parse("2026-07-14") },
            // PDA 操作员
            new SysRolePermission { Id = 25, RoleId = 3, PermissionId = 1, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 26, RoleId = 3, PermissionId = 14, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 27, RoleId = 3, PermissionId = 15, CreatedAt = DateTime.Parse("2026-07-14") },
            new SysRolePermission { Id = 28, RoleId = 3, PermissionId = 17, CreatedAt = DateTime.Parse("2026-07-14") },
            // 访客
            new SysRolePermission { Id = 29, RoleId = 4, PermissionId = 1, CreatedAt = DateTime.Parse("2026-07-14") }
        );

        // 抄表记录种子数据（2026年6月、7月）
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
