# 检查清单（4 类）

> Skill: release-bugfix-v2.13.193  
> 适用项目：金智住宿管理系统（DormManage）  
> 创建日期：2026-07-27

---

## 清单 1：代码修改前检查清单

使用时机：开始修改代码之前

- [ ] **修改需求已明确**：用户原话已记录到文档
- [ ] **关联文档已找到**：grep 搜索过相关文档
- [ ] **关联代码已搜索**：data-perm-code、FieldKey 等关键标识符已 grep
- [ ] **数据流已了解**：DTO → Controller → View 完整链路
- [ ] **向后兼容性已考虑**：不破坏现有接口和功能
- [ ] **测试用例已规划**：包括跨权限测试（admin + 未授权）

---

## 清单 2：代码修改后检查清单

使用时机：Edit 操作完成后立即执行

- [ ] **`git diff` 确认修改已保存**：`git diff source.cs` 应有显示
- [ ] **`dotnet build` 0 错误**：`dotnet build DormManage.sln -c Release`
- [ ] **编译警告已知**：未新增警告
- [ ] **修改文件时间戳更新**：`stat -c '%y' source.cs`
- [ ] **DTO 字段已添加**：DTO 类包含新字段
- [ ] **赋值正确**：OnGet/OnPost 中正确赋值
- [ ] **前端引用正确**：cshtml 中正确渲染新字段

---

## 清单 3：发布前检查清单（7 项强制）

使用时机：dotnet publish 完成后，dotnet build 前

- [ ] **源码已修改**：grep 验证关键字
- [ ] **编译 0 错误**：`dotnet build DormManage.sln -c Release`
- [ ] **同步 DLL 到所有子目录**：
  - [ ] `release/latest/Admin/` 已更新
  - [ ] `release/latest/Api/` 已更新
  - [ ] `release/latest/TrayApp/Admin/` 已同步（关键）
  - [ ] `release/latest/TrayApp/Api/` 已同步（关键）
  - [ ] **Shared DLL 已同步到 4 处**
- [ ] **DLL 时间戳与 publish 时间一致**：
  - [ ] `release/latest/Admin/DormManage.Admin.dll`
  - [ ] `release/latest/TrayApp/Admin/DormManage.Admin.dll`
  - [ ] `release/latest/Api/DormManage.Api.dll`
  - [ ] `release/latest/TrayApp/Api/DormManage.Api.dll`
- [ ] **.cshtml Views 已同步到 TrayApp/Admin/Pages/**
- [ ] **使用 sync_publish_to_trayapp.sh**（不允许手动 cp）
- [ ] **ZIP 包生成到 release/_archive/**

---

## 清单 4：部署验证清单（运行时）

使用时机：ZIP 包部署后，启动 TrayApp 之前

- [ ] **停止所有 DormManage 进程**：
  ```bash
  taskkill /F /IM DormManage.TrayApp.exe
  taskkill /F /IM DormManage.Admin.exe
  taskkill /F /IM DormManage.Api.exe
  ```
- [ ] **启动 TrayApp**：
  ```bash
  cd release/latest/TrayApp
  ./DormManage.TrayApp.exe
  ```
- [ ] **检查 TrayApp 日志**：
  - [ ] 注册校验通过（RegInt=1 RegStatus=1）
  - [ ] Api 进程已启动
  - [ ] Admin 进程已启动
  - [ ] HTTP 200 健康检查
- [ ] **curl 验证页面内容**：
  ```bash
  curl -s -c cookies.txt -o login.html http://localhost:5001/Account/Login
  TOKEN=$(grep -o 'name="__RequestVerificationToken"[^>]*value="[^"]*"' login.html | head -1 | sed 's/.*value="\([^"]*\)".*/\1/')
  curl -s -b cookies.txt -c cookies.txt -L \
      --data-urlencode "UserName=admin" \
      --data-urlencode "Password=admin123" \
      --data-urlencode "RememberMe=false" \
      --data-urlencode "__RequestVerificationToken=$TOKEN" \
      -o /dev/null http://localhost:5001/Account/Login?handler=Login
  
  curl -s -b cookies.txt -o details.html "http://localhost:5001/Dorms/Details?id=3"
  ```
  - [ ] `grep -c "bi-gender-male" details.html >= 1`
  - [ ] `grep "床位 [0-9]" details.html` 为空
- [ ] **跨权限 E2E 测试**：
  - [ ] **admin 登录**：所有字段可见
  - [ ] **未授权角色登录**（如 test）：隐私字段隐藏
- [ ] **浏览器最终验证**：必须 **Ctrl+F5 强制刷新**

---

## 10 个常见 BUG 的快速排查表

| BUG 类型 | 排查要点 | 第一步 |
|---------|---------|--------|
| 修改无效 | 双胞胎陷阱 | 检查 TrayApp 兄弟目录同步 |
| 隐私字段仍显示 | deny-by-default 翻转 | 检查 AllowDisplayPrivacyFieldsAsync |
| Admin 启动失败 | TypeLoadException | 检查 Shared DLL 同步 |
| 编译报错 | Razor 语法 | 检查 `@`-变量混淆（如 @bg） |
| 部署后崩溃 | DLL 版本 | 检查所有 DLL 时间戳一致 |
| 浏览器旧版 | 浏览器缓存 | Ctrl+F5 强制刷新 |
| 字段不显示 | DTO 字段未填充 | 检查 OnGetAsync 赋值 |
| 权限未生效 | IPermissionService DI | 检查 Program.cs 注册 |
| 中文乱码 | UTF-16 LE | 用 grep + 编码 |
| TrayApp 日志异常 | 注册码 | 检查 HKLM\Software\JINGE\DormManage |