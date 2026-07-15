using Microsoft.AspNetCore.Mvc;
using DormManage.Shared.Data;
using DormManage.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DormManage.Api.Controllers.Auth;

/// <summary>
/// 用户管理 API（P1-13）
///
/// 端点：
/// - GET    /api/v1/auth/users              分页查询
/// - GET    /api/v1/auth/users/{id}         详情
/// - POST   /api/v1/auth/users              创建（密码 BCrypt 加密）
/// - PUT    /api/v1/auth/users/{id}         更新
/// - POST   /api/v1/auth/users/{id}/password 重置密码
/// - DELETE /api/v1/auth/users/{id}         删除
/// - POST   /api/v1/auth/users/{id}/lock    锁定/解锁
/// - GET    /api/v1/auth/roles              角色列表
/// </summary>
[ApiController]
[Route("api/v1/auth/users")]
public class UserController : ControllerBase
{
    private readonly DormDbContext _db;

    public UserController(DormDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ApiResponse<PagedResult<UserDto>>> GetUsers(
        [FromQuery] string? keyword,
        [FromQuery] int? roleId,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.SysUsers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(u => u.UserName.Contains(keyword) || u.DisplayName.Contains(keyword));
        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        if (roleId.HasValue)
        {
            var userIds = await _db.SysUserRoles.Where(ur => ur.RoleId == roleId.Value)
                .Select(ur => ur.UserId).ToListAsync();
            query = query.Where(u => userIds.Contains(u.Id));
        }

        var total = await query.CountAsync();
        var users = await query.OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var userIds2 = users.Select(u => u.Id).ToList();
        var userRoles = await _db.SysUserRoles.Where(ur => userIds2.Contains(ur.UserId)).ToListAsync();
        var roles = await _db.SysRoles.ToListAsync();

        var roleMap = roles.ToDictionary(r => r.Id);
        var userRoleMap = userRoles.GroupBy(ur => ur.UserId)
            .ToDictionary(g => g.Key, g => g.Select(ur => ur.RoleId).ToList());

        var items = users.Select(u => new UserDto
        {
            Id = u.Id,
            UserName = u.UserName,
            DisplayName = u.DisplayName,
            Email = u.Email,
            Phone = u.Phone,
            IsActive = u.IsActive,
            IsLocked = u.IsLocked,
            Roles = userRoleMap.GetValueOrDefault(u.Id, new List<int>())
                .Select(rid => roleMap.GetValueOrDefault(rid))
                .Where(r => r is not null)
                .Select(r => new RoleBrief { Id = r!.Id, RoleCode = r.RoleCode, RoleName = r.RoleName })
                .ToList(),
            LastLoginTime = u.LastLoginTime,
            LastLoginIp = u.LastLoginIp,
            CreatedAt = u.CreatedAt
        }).ToList();

        return ApiResponse<PagedResult<UserDto>>.Ok(new PagedResult<UserDto>
        {
            Items = items,
            Total = total,
            PageIndex = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id}")]
    public async Task<ApiResponse<UserDto>> GetUser(int id)
    {
        var u = await _db.SysUsers.FindAsync(id);
        if (u is null) return ApiResponse<UserDto>.Fail("NOT_FOUND", "用户不存在");

        var userRoles = await _db.SysUserRoles.Where(ur => ur.UserId == id).ToListAsync();
        var roles = await _db.SysRoles.ToListAsync();

        return ApiResponse<UserDto>.Ok(new UserDto
        {
            Id = u.Id,
            UserName = u.UserName,
            DisplayName = u.DisplayName,
            Email = u.Email,
            Phone = u.Phone,
            IsActive = u.IsActive,
            IsLocked = u.IsLocked,
            Roles = userRoles.Select(ur => roles.FirstOrDefault(r => r.Id == ur.RoleId))
                .Where(r => r is not null)
                .Select(r => new RoleBrief { Id = r!.Id, RoleCode = r.RoleCode, RoleName = r.RoleName })
                .ToList(),
            LastLoginTime = u.LastLoginTime,
            LastLoginIp = u.LastLoginIp,
            CreatedAt = u.CreatedAt
        });
    }

    [HttpPost]
    public async Task<ApiResponse<UserDto>> CreateUser([FromBody] UserCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.DisplayName))
            return ApiResponse<UserDto>.Fail("INVALID_INPUT", "用户名、姓名、密码必填");

        if (await _db.SysUsers.AnyAsync(u => u.UserName == request.UserName))
            return ApiResponse<UserDto>.Fail("DUPLICATE_USERNAME", $"用户名 {request.UserName} 已存在");

        var user = new SysUser
        {
            UserName = request.UserName.Trim(),
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Email = request.Email,
            Phone = request.Phone,
            IsActive = true,
            IsLocked = false,
            FailedLoginCount = 0,
            CreatedAt = DateTime.Now
        };
        _db.SysUsers.Add(user);
        await _db.SaveChangesAsync();

        if (request.RoleIds?.Length > 0)
        {
            foreach (var rid in request.RoleIds.Distinct())
            {
                _db.SysUserRoles.Add(new SysUserRole { UserId = user.Id, RoleId = rid });
            }
            await _db.SaveChangesAsync();
        }

        return ApiResponse<UserDto>.Ok(new UserDto { Id = user.Id, UserName = user.UserName, DisplayName = user.DisplayName }, "创建成功");
    }

    [HttpPut("{id}")]
    public async Task<ApiResponse> UpdateUser(int id, [FromBody] UserUpdateRequest request)
    {
        var user = await _db.SysUsers.FindAsync(id);
        if (user is null) return ApiResponse.Fail("NOT_FOUND", "用户不存在");

        user.DisplayName = request.DisplayName?.Trim() ?? user.DisplayName;
        user.Email = request.Email;
        user.Phone = request.Phone;
        user.IsActive = request.IsActive ?? user.IsActive;
        user.UpdatedAt = DateTime.Now;

        if (request.RoleIds is not null)
        {
            var old = _db.SysUserRoles.Where(ur => ur.UserId == id);
            _db.SysUserRoles.RemoveRange(old);
            foreach (var rid in request.RoleIds.Distinct())
            {
                _db.SysUserRoles.Add(new SysUserRole { UserId = id, RoleId = rid });
            }
        }

        await _db.SaveChangesAsync();
        return ApiResponse.Ok("更新成功");
    }

    [HttpPost("{id}/password")]
    public async Task<ApiResponse> ResetPassword(int id, [FromBody] PasswordResetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return ApiResponse.Fail("INVALID_PASSWORD", "密码长度至少 6 位");

        var user = await _db.SysUsers.FindAsync(id);
        if (user is null) return ApiResponse.Fail("NOT_FOUND", "用户不存在");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.FailedLoginCount = 0;
        user.IsLocked = false;
        user.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("密码重置成功");
    }

    [HttpPost("{id}/lock")]
    public async Task<ApiResponse> ToggleLock(int id, [FromBody] LockRequest request)
    {
        var user = await _db.SysUsers.FindAsync(id);
        if (user is null) return ApiResponse.Fail("NOT_FOUND", "用户不存在");
        if (user.UserName == "admin")
            return ApiResponse.Fail("PROTECTED", "内置 admin 账号不允许锁定");

        user.IsLocked = request.IsLocked;
        if (!request.IsLocked)
        {
            user.FailedLoginCount = 0;
        }
        user.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return ApiResponse.Ok(request.IsLocked ? "已锁定" : "已解锁");
    }

    [HttpDelete("{id}")]
    public async Task<ApiResponse> DeleteUser(int id)
    {
        var user = await _db.SysUsers.FindAsync(id);
        if (user is null) return ApiResponse.Fail("NOT_FOUND", "用户不存在");
        if (user.UserName == "admin")
            return ApiResponse.Fail("PROTECTED", "内置 admin 账号不允许删除");

        var urs = _db.SysUserRoles.Where(ur => ur.UserId == id);
        _db.SysUserRoles.RemoveRange(urs);
        _db.SysUsers.Remove(user);
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("删除成功");
    }
}

[ApiController]
[Route("api/v1/auth/roles")]
public class RoleController : ControllerBase
{
    private readonly DormDbContext _db;
    public RoleController(DormDbContext db) { _db = db; }

    [HttpGet]
    public async Task<ApiResponse<List<RoleBrief>>> GetRoles()
    {
        var roles = await _db.SysRoles.OrderBy(r => r.SortOrder).ToListAsync();
        return ApiResponse<List<RoleBrief>>.Ok(roles.Select(r => new RoleBrief
        {
            Id = r.Id, RoleCode = r.RoleCode, RoleName = r.RoleName, IsActive = r.IsActive
        }).ToList());
    }

    [HttpGet("{id}/permissions")]
    public async Task<ApiResponse<List<int>>> GetRolePermissions(int id)
    {
        var pids = await _db.SysRolePermissions.Where(rp => rp.RoleId == id).Select(rp => rp.PermissionId).ToListAsync();
        return ApiResponse<List<int>>.Ok(pids);
    }

    [HttpPost("{id}/permissions")]
    public async Task<ApiResponse> SaveRolePermissions(int id, [FromBody] List<int> permissionIds)
    {
        var role = await _db.SysRoles.FindAsync(id);
        if (role is null) return ApiResponse.Fail("NOT_FOUND", "角色不存在");

        var old = _db.SysRolePermissions.Where(rp => rp.RoleId == id);
        _db.SysRolePermissions.RemoveRange(old);
        if (permissionIds?.Count > 0)
        {
            foreach (var pid in permissionIds.Distinct())
            {
                _db.SysRolePermissions.Add(new SysRolePermission { RoleId = id, PermissionId = pid, CreatedAt = DateTime.Now });
            }
        }
        await _db.SaveChangesAsync();
        return ApiResponse.Ok("权限已更新");
    }
}

public class UserDto
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public List<RoleBrief> Roles { get; set; } = new();
    public DateTime? LastLoginTime { get; set; }
    public string? LastLoginIp { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RoleBrief
{
    public int Id { get; set; }
    public string RoleCode { get; set; } = "";
    public string RoleName { get; set; } = "";
    public bool IsActive { get; set; }
}

public class UserCreateRequest
{
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int[]? RoleIds { get; set; }
}

public class UserUpdateRequest
{
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool? IsActive { get; set; }
    public int[]? RoleIds { get; set; }
}

public class PasswordResetRequest
{
    public string NewPassword { get; set; } = "";
}

public class LockRequest
{
    public bool IsLocked { get; set; }
}