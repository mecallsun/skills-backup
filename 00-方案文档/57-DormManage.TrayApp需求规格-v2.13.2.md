# DormManage.TrayApp — 托盘守护程序需求规格

> **版本**：v2.13.2  
> **日期**：2026-07-15  
> **状态**：已定稿  
> **关联方案**：`56-DormManage.TrayApp技术方案-v2.13.2.md`

---

## 1. 业务背景

### 1.1 现状

- v2.13.0 文档承诺的 `DormManage.TrayApp/` 源码缺失
- 当前部署仅依赖 `publish-final/V1.0/start.bat` 启动 Api/Admin，无托盘 UI、无配置面板、无故障自愈
- 部署后修改端口/数据库需手动编辑 `appsettings.json`，易出错

### 1.2 目标

提供 Windows 托盘守护程序作为正式启动入口，实现：
1. 一键启动整套服务（Api + Admin）
2. 可视化配置核心参数
3. 故障自动重启
4. 状态实时可见

---

## 2. 用户角色

| 角色 | 使用场景 | 权限范围 |
|------|---------|---------|
| **系统管理员** | 部署、运维、配置变更 | 全部配置 + 服务启停 + 退出 |
| **普通操作员** | 仅查看服务状态、打开 Web | 托盘菜单浏览（无配置修改权限） |

> 注：本版本不做角色登录，托盘启动即拥有全部操作权限（**无权限控制**，与 CLAUDE.md 约定一致）。Web 端 RBAC 在 v2.13.0 已实现。

---

## 3. 功能清单

### 3.1 F1：托盘菜单（核心）

| 菜单项 | 功能 | 图标 |
|--------|------|------|
| 打开管理后台 | 浏览器打开 Admin 首页（默认 http://localhost:5001） | 🌐 |
| 打开 API 文档 | 浏览器打开 Swagger（默认 http://localhost:5100/swagger） | 📘 |
| ───── | 分隔线 | - |
| 服务状态 > | 子菜单：Api 状态、Admin 状态 | ●/○/× |
| 重启所有服务 | 停止后重启 Api + Admin | 🔄 |
| 设置... | 打开 SettingsForm | ⚙ |
| 查看日志 | 打开 logs 目录 | 📄 |
| ───── | 分隔线 | - |
| 关于 | 版本信息 | ℹ |
| 退出 | 停止服务并退出托盘 | ✕ |

**左键单击托盘图标**：直接打开管理后台（默认行为）。

### 3.2 F2：配置窗口（SettingsForm）

#### 3.2.1 字段

| 字段 | 类型 | 必填 | 默认值 | 校验规则 |
|------|------|------|--------|---------|
| API 端口 | int | 是 | 5100 | 1024-65535，未被占用 |
| Admin 端口 | int | 是 | 5001 | 1024-65535，未被占用 |
| Api 可执行文件路径 | string | 是 | `Api\DormManage.Api.exe` | 文件必须存在 |
| Admin 可执行文件路径 | string | 是 | `Admin\DormManage.Admin.exe` | 文件必须存在 |
| 数据库类型 | enum | 是 | SqlServer | SqlServer / Sqlite |
| SQL Server 连接串 | string | 条件 | 测试库连接 | 当 Provider=SqlServer 必填 |
| SQLite 数据库路径 | string | 条件 | 空 | 当 Provider=Sqlite 必填，文件可不存在 |
| 图片存储根路径 | string | 否 | `Storage\images` | 目录不存在则自动创建 |
| 启动时自动启动服务 | bool | 否 | true | - |
| 异常时自动重启 | bool | 否 | true | - |
| 健康检查间隔（秒） | int | 否 | 10 | 5-300 |

#### 3.2.2 按钮

| 按钮 | 行为 |
|------|------|
| 启动 | ProcessManager.StartAllAsync() |
| 停止 | ProcessManager.StopAllAsync() |
| 重启 | ProcessManager.RestartAllAsync() |
| 保存 | 写入 appsettings.json，提示"立即重启以生效？" |
| 取消 | 关闭窗口（不保存） |
| 浏览（Api 路径） | OpenFileDialog，筛选 *.exe |
| 浏览（Admin 路径） | OpenFileDialog，筛选 *.exe |
| 浏览（图片路径） | FolderBrowserDialog |

#### 3.2.3 服务状态显示

| 状态 | 颜色 | 文案 |
|------|------|------|
| 已停止 | 灰 ● | "已停止" |
| 启动中 | 黄 ● | "启动中..." |
| 运行中 | 绿 ● | "运行中 (PID:1234, 端口:5100)" |
| 异常 | 红 ● | "异常：HTTP 探测失败" |

定时器 1s 刷新一次状态。

### 3.3 F3：自动启动与故障自愈

| 场景 | 行为 |
|------|------|
| 托盘启动 + AutoStart=true | 自动启动 Api + Admin |
| 托盘启动 + AutoStart=false | 仅显示托盘图标，等待用户手动启动 |
| 子进程 Exited（异常） | 5s 后自动重启，记录日志 |
| 5 分钟内重启 ≥ 3 次 | 停止自愈，弹窗提示"服务反复崩溃，请检查配置" |
| 用户主动停止 | 不触发自动重启 |

### 3.4 F4：单实例

| 场景 | 行为 |
|------|------|
| 已有一个托盘运行 | 第二个托盘启动时弹窗"已在运行"，立即退出 |
| 首个托盘退出后 | 新托盘可正常启动 |

实现：`Mutex` + 全局名 `Global\DormManage.TrayApp.SingleInstance`。

### 3.5 F5：日志

- 路径：`logs/tray-YYYYMMDD.log`
- 格式：`[{yyyy-MM-dd HH:mm:ss.fff}] [{LEVEL}] {message}`
- 记录内容：服务启停、配置变更、故障自愈、退出
- 查看方式：托盘菜单 → 查看日志 → 资源管理器打开 logs 目录

---

## 4. 接口约定

### 4.1 与子进程通信（环境变量）

托盘启动 Api/Admin 时注入：

| 环境变量 | 取值 | Api/Admin 读取位置 |
|---------|------|------------------|
| `DormManage_KESTREL_PORT` | ApiPort/AdminPort 数值 | `Program.cs` 第 75/92 行（已实现） |
| `DormManage_DB_CONN` | SQL Server 连接串 | `Program.cs` 第 21-23 行（已实现） |
| `DormManage_DB_PATH` | SQLite 绝对路径 | `Program.cs` 第 57-59 行（已实现） |

### 4.2 健康检查端点

| 服务 | URL | 期望 |
|------|-----|------|
| Api | `http://localhost:{ApiPort}/swagger/index.html` | HTTP 200 |
| Admin | `http://localhost:{AdminPort}/` | HTTP 200/302 |

> 探测失败不视为崩溃（可能是 Kestrel 启动慢），仅在状态显示中提示；连续 3 次失败才触发重启。

---

## 5. 验收用例

### 5.1 冒烟测试

| 用例ID | 步骤 | 预期 |
|--------|------|------|
| TC-T01 | 双击 DormManage.TrayApp.exe | 出现托盘图标，无窗口 |
| TC-T02 | 等待 15s | Api/Admin 进程已启动（任务管理器可见） |
| TC-T03 | 左键单击托盘 | 浏览器打开 Admin 首页 |
| TC-T04 | 右键 → 打开 API 文档 | 浏览器打开 Swagger |
| TC-T05 | 右键 → 退出 → 确认 | Api/Admin 进程结束，托盘消失 |
| TC-T06 | 双击托盘 EXE（已有一个运行） | 弹窗"已在运行"，立即退出 |

### 5.2 配置窗口测试

| 用例ID | 步骤 | 预期 |
|--------|------|------|
| TC-S01 | 托盘菜单 → 设置 | 打开 SettingsForm，显示当前配置 |
| TC-S02 | 修改 API 端口 5100 → 5200 → 保存 → 确认重启 | Api 重启，新端口可访问 |
| TC-S03 | 浏览 Api 可执行文件路径 → 选择不存在的文件 → 保存 | 提示"文件不存在"，禁止保存 |
| TC-S04 | 数据库类型 SqlServer → Sqlite → 保存 → 重启 | Admin 切换到 SQLite 模式（dorm.db 文件创建） |

### 5.3 故障自愈测试

| 用例ID | 步骤 | 预期 |
|--------|------|------|
| TC-R01 | 任务管理器结束 Api 进程 | 5s 后 Api 自动重启，托盘图标状态变绿 |
| TC-R02 | 5 分钟内结束 Api 进程 3 次 | 弹窗"服务反复崩溃"，停止自愈 |
| TC-R03 | 设置 → 停止 → 任务管理器确认 Api 已退出 | 不触发自动重启 |

### 5.4 单实例测试

| 用例ID | 步骤 | 预期 |
|--------|------|------|
| TC-L01 | 运行托盘 → 再次双击托盘 EXE | 第二个进程弹窗后退出 |
| TC-L02 | 第一个托盘退出 → 第二个托盘启动 | 正常启动 |

---

## 6. 字段映射（与文档冲突检查）

> 按 CLAUDE.md 中"数据逻辑一致性"要求执行。

| 字段 | 本文档 | Admin/Api 代码 | 一致性 |
|------|--------|--------------|--------|
| Api 默认端口 | 5100 | `appsettings.json` Urls=5100；`Program.cs` 默认值=5100 | ✅ |
| Admin 默认端口 | 5001 | `appsettings.json` Urls=5001；`Program.cs` 默认值=5001 | ✅ |
| Provider 枚举 | SqlServer/Sqlite | Program.cs 比较使用 OrdinalIgnoreCase | ✅ |
| 环境变量名 | DormManage_KESTREL_PORT / DB_CONN / DB_PATH | Program.cs 已实现读取 | ✅ |

**冲突发现与修复**：

| 冲突 | 位置 | 处理 |
|------|------|------|
| CLAUDE.md 写 "Api 端口 5000" | `CLAUDE.md` 1.2.3 节 | **本次修复**：改为 5100（与代码一致） |
| start.bat 用 5200 端口 | `publish-final/V1.0/start.bat` | **保留**：start.bat 作为无托盘的快速通道，使用不同端口避免冲突 |
| publish-new.ps1 引用不存在的 05-Standalone 和水电抄表系统路径 | `publish-new.ps1` | **本次修复**：更新为 DormManage 命名空间路径 |

---

## 7. 范围外（明确不做）

- ❌ 多用户切换、托盘登录鉴权
- ❌ 自启动注册（Windows 计划任务 / Run 键）— 留待 v2.13.4
- ❌ 服务端 Web 设置页面的高级功能（备份恢复、用户角色、筛选缓存）
- ❌ 跨平台支持（仅 Windows）

---

## 8. 风险与依赖

| 项 | 说明 |
|----|------|
| 依赖 .NET 8 Desktop Runtime | 部署前需安装 |
| 依赖 Admin/Api EXE 同目录结构 | 发布时需保证 `Api/`、`Admin/` 与 `TrayApp/` 同级 |
| 端口冲突 | 启动前检测，提示用户 |
| WDAC 阻止 | 与 Admin/Api 同样需要管理员权限启动 |

---

## 9. 交付清单

- [x] `56-DormManage.TrayApp技术方案-v2.13.2.md`（已编写）
- [x] `57-DormManage.TrayApp需求规格-v2.13.2.md`（本文档）
- [ ] `DormManage.TrayApp/` 源码
- [ ] `appsettings.json` 默认值
- [ ] 编译 0 错误
- [ ] 冒烟测试报告
- [ ] CLAUDE.md / 01-技术架构 / 05-原型与代码基线对照.md 版本同步