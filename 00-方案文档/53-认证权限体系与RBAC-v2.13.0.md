# 金戈宿舍管理系统 — 技术架构与系统开发方案

> **版本**：v2.13.0  
> **日期**：2026-07-14  
> **状态**：已定稿  
> **变更内容**：新增认证权限体系（RBAC）、托盘守护程序、强制登录控制、用户/角色独立管理页面

---

## 1. 项目概述

本项目是基于 .NET 8 + Razor + EF Core 的宿舍管理系统，采用 EXE 自托管部署架构。

### 1.1 技术栈

| 层级 | 技术选型 |
|------|---------|
| 后端框架 | .NET 8 ASP.NET Core |
| 前端框架 | Razor Pages + Bootstrap 5 + jQuery |
| ORM | Entity Framework Core 8 |
| 数据库 | SQLite（开发）/ SQL Server（生产） |
| 部署 | EXE 自托管 + 托盘守护进程 |
| 认证 | Cookie 认证（BCrypt 密码加密） |
| 授权 | RBAC（基于角色的访问控制） |

### 1.2 部署环境要求

| 组件 | 支持版本 |
|------|---------|
| **操作系统** | Windows 11 / Windows Server 2019 / 2016 / 2022 |
| **数据库** | SQL Server 2014+（含 Express）/ SQLite（开发） |
| **.NET 运行时** | .NET 8 Desktop Runtime 8.0.x |

### 1.3 支持的终端类型

| 终端类型 | 平台 | 功能范围 | 状态 |
|---------|------|---------|------|
| **Web 管理端** | Win/Mac/Linux 浏览器 | 全套管理功能 | ✅ V2.13.0 |
| **安卓 PDA 终端** | Android 8.0+ | 完整扫码 + 抄表 + 上传 | ✅ V1.0 |
| **安卓平板终端** | Android 8.0+ | 功能范围同 PDA | ✅ V1.0 |
| **托盘守护程序** | Windows | 自动启动 Web/PDA 服务 | ✅ V2.13.0 |

---

## 2. 系统架构

### 2.1 项目结构

```
宿舍管理系统/
├── DormManage.Shared/      # 共享库（Models/DbContext/Services）
├── DormManage.Api/         # REST API 服务
├── DormManage.Admin/       # Web 管理后台（自托管）
├── DormManage.TrayApp/     # 托盘守护程序（自动启动服务）
├── 00-方案文档/            # 需求/架构/设计文档
├── 01-Database/            # 数据库迁移脚本
├── 04-HTML原型/            # HTML 原型页面
└── publish-final/          # 部署包输出
```

### 2.2 架构图

```
┌─────────────────────────────────────────────────────┐
│                   托盘守护程序                        │
│  (DormManage.TrayApp.exe)                           │
│  ├─ 自动启动 Web 管理端 (Admin)                      │
│  ├─ 自动启动 PDA 接口服务 (Api)                      │
│  ├─ 监控服务健康状态                                  │
│  └─ 故障自动重启                                      │
└─────────────────────────────────────────────────────┘
         │                              │
         ▼                              ▼
┌──────────────────┐        ┌──────────────────────┐
│  Web 管理端       │        │  PDA 接口服务         │
│  (Admin:5001)    │        │  (Api:5000)          │
│  ├─ Razor Pages  │        │  ├─ REST API         │
│  ├─ Cookie Auth  │        │  └─ JSON Response    │
│  └─ RBAC 授权    │        └──────────────────────┘
└────────┬─────────┘              │
         │                        │
         ▼                        ▼
┌────────────────────────────────────────┐
│           数据库 (SQL Server/SQLite)     │
│  ├─ 业务表（Dorm/Billing/Meter...）    │
│  ├─ 基础资料表（Department/Building...）│
│  └─ 认证权限表（SysUser/Role/Permission）│
└────────────────────────────────────────┘
```

---

## 3. 认证权限体系（RBAC）— V2.13.0 新增

### 3.1 认证流程

```
用户访问任意页面
    ↓
Cookie 认证中间件检查
    ↓
未登录 → 重定向到 /Account/Login
    ↓
输入用户名 + 密码（BCrypt 验证）
    ↓
构建 ClaimsPrincipal（用户ID/角色/显示名）
    ↓
设置 Cookie（8小时滑动过期，记住我72小时）
    ↓
重定向到目标页面，渲染权限过滤后的菜单
```

### 3.2 权限模型

| 表 | 说明 | 关键字段 |
|----|------|---------|
| **SysUser** | 系统用户 | UserName(唯一), PasswordHash(BCrypt) |
| **SysRole** | 系统角色 | RoleCode(唯一), RoleName |
| **SysPermission** | 系统权限 | PermissionCode(唯一), PermissionType(1=菜单/2=按钮/3=数据) |
| **SysUserRole** | 用户-角色关联 | UserId+RoleId(联合唯一) |
| **SysRolePermission** | 角色-权限关联 | RoleId+PermissionId(联合唯一) |

### 3.3 预置角色

| 角色编码 | 角色名称 | 权限范围 |
|---------|---------|---------|
| `admin` | 管理员 | 全部模块（首页/办理/宿舍/人员/账单/抄表/基础/设置） |
| `finance` | 财务 | 首页/费用标准/宿舍账单/员工账单/基础资料/系统设置 |
| `pda_operator` | PDA 操作员 | 首页/抄表记录/手动录入/基础资料 |
| `viewer` | 访客 | 仅首页数据看板 |

### 3.4 菜单权限控制

- 所有页面默认需要认证（`AuthorizeFolder("/")`）
- 导航栏菜单根据用户角色动态渲染
- 无权限的菜单项自动隐藏
- 用户管理/角色管理为独立子菜单，与部门/员工类型等并列

### 3.5 密码安全

- 密码使用 **BCrypt** 算法加密存储（非明文）
- 系统设置中的数据库连接密码同样使用 MD5 加密保存
- 连接数据库时动态解密验证

---

## 4. 数据库设计

### 4.1 表分类

| 分类 | 表数量 | 说明 |
|------|--------|------|
| 基础资料 | 10 | 部门/楼栋/楼层/地址/员工类型/考勤班次/计量单位/住宿状态/在职状态/班组 |
| 业务表 | 5 | 费用标准/员工/宿舍/办理记录/抄表记录 |
| 认证权限 | 5 | 用户/角色/权限/用户-角色/角色-权限 |
| **合计** | **20** | |

### 4.2 核心表结构

#### SysUser（系统用户）
| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| UserName | nvarchar(50) | 登录用户名（唯一） |
| PasswordHash | nvarchar(255) | BCrypt 加密密码 |
| DisplayName | nvarchar(50) | 显示姓名 |
| EmployeeId | int? | 关联员工ID（可选） |
| Email | nvarchar(100) | 邮箱 |
| Phone | nvarchar(20) | 手机号 |
| IsActive | bit | 是否启用 |
| LastLoginTime | datetime? | 最后登录时间 |
| LastLoginIp | nvarchar(45)? | 最后登录IP |

#### SysRole（系统角色）
| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| RoleCode | nvarchar(50) | 角色编码（唯一） |
| RoleName | nvarchar(50) | 角色名称 |
| Description | nvarchar(200) | 描述 |
| SortOrder | int | 排序号 |
| IsActive | bit | 是否启用 |

#### SysPermission（系统权限）
| 字段 | 类型 | 说明 |
|------|------|------|
| Id | int | 主键 |
| PermissionCode | nvarchar(100) | 权限编码（唯一） |
| PermissionName | nvarchar(100) | 权限名称 |
| PermissionType | int | 类型：1=菜单/2=按钮/3=数据 |
| ParentId | int | 父权限ID |
| Route | nvarchar(200) | 关联路由 |
| Icon | nvarchar(50) | Bootstrap Icons 图标 |
| SortOrder | int | 排序号 |

---

## 5. 部署方式

### 5.1 启动流程

1. 运行 `DormManage.TrayApp.exe`（托盘守护程序）
2. 托盘程序自动启动：
   - `DormManage.Admin.exe`（Web 管理端，端口 5001）
   - `DormManage.Api.exe`（PDA 接口服务，端口 5000）
3. PC 用户通过浏览器访问 `http://localhost:5001`
4. PDA 终端通过 `http://<服务器IP>:5000` 访问抄表接口

### 5.2 发布命令

```bash
dotnet publish DormManage.Admin/DormManage.Admin.csproj -c Release -r win-x64 --self-contained true -o publish-final/Admin
dotnet publish DormManage.Api/DormManage.Api.csproj -c Release -r win-x64 --self-contained true -o publish-final/Api
dotnet publish DormManage.TrayApp/DormManage.TrayApp.csproj -c Release -r win-x64 --self-contained true -o publish-final/TrayApp
```

---

## 6. 版本历史

| 版本 | 日期 | 变更内容 |
|------|------|---------|
| v2.13.0 | 2026-07-14 | 新增 RBAC 认证权限体系、Cookie 认证、强制登录控制、托盘守护程序、用户/角色独立管理页面 |
| v2.12.43 | 2026-07-11 | 页面 500 错误全面修复 |
| v2.12.42 | 2026-07-10 | 数据库 Provider 切换、部署环境规范 |
| v1.0 | 2026-07-01 | 初始版本 |
