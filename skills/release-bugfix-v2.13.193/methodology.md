# BUG 排查 5 步方法论

> Skill: release-bugfix-v2.13.193  
> 适用项目：金戈宿舍管理系统（DormManage）  
> 创建日期：2026-07-27

---

## 概述

当用户报告"修改无效"、"BUG 没修复"、"看不到效果"类问题时，按本方法论系统化排查。

---

## 5 步排查法

### 步骤 1：源码层确认

**目标**：确认修改是否真的保存到源文件

```bash
# 检查文件是否有修改
grep -c "新增关键字" source.cs
# 必须 > 0

# 如果是 .cshtml，检查 Razor 表达式
grep -c "IsFieldHiddenAsync\|bi-gender" source.cshtml
```

**可能失败原因**：
- git stash/checkout 静默回滚
- Edit 操作实际未应用
- 工作目录错误

**补救**：重新应用修改，Edit 后立即 git diff

---

### 步骤 2：编译层确认

**目标**：确认编译没有引入错误

```bash
# 编译整个解决方案
dotnet build DormManage.sln -c Release

# 必须输出：
#   0 个错误
```

**可能失败原因**：
- Razor 语法错误（如 `@bg-` 被误识别为变量）
- 类型导入缺失
- Publish 与 project 之间的版本不一致

**补救**：修复编译错误后重新编译

---

### 步骤 3：发布时间戳确认

**目标**：确认发布包是最新的

```bash
# 发布包 DLL 时间戳
stat -c '%y  %n' release/latest/DormManage.Admin.dll

# 必须是 publish 时间（5 分钟内）
```

**可能失败原因**：
- 发布命令未执行
- 发布到错误目录
- 文件被另一个进程锁定

---

### 步骤 4：DLL 内容确认

**目标**：确认 DLL 真的包含修改

```bash
# 检查 ASCII 字符串
grep -ao "bi-gender-male" release/latest/DormManage.Admin.dll | wc -l

# 检查中文字符串（UTF-16 LE 编码）
python3 -c "
import codecs
with open('release/latest/DormManage.Admin.dll', 'rb') as f:
    data = f.read()
target = codecs.encode('性别', 'utf-16-le')
print(f'性别 UTF-16 LE count: {data.count(target)}')
"
```

**关键点**：.NET DLL 中所有字符串都以 UTF-16 LE 编码，包括中文字符。

**补救**：删除 obj/Release 缓存重新编译

```bash
rm -rf DormManage.Admin/obj/Release/net8.0/
rm -rf DormManage.Shared/obj/Release/net8.0/
dotnet build DormManage.sln -c Release --force
```

---

### 步骤 5：运行环境确认

**目标**：确认运行中的进程加载的是最新 DLL

```powershell
# 查看 Admin 进程加载的 DLL 路径
Get-Process -Name DormManage.Admin | ForEach-Object {
    $_.Modules | Where-Object { $_.FileName -like "*DormManage*" } | 
    Select-Object FileName, FileVersion
}
```

**关键检查**：
- TrayApp 启动时通过 `..\Admin\` 相对路径加载
- 实际加载的是 `release/latest/TrayApp/Admin/`，**不是** `release/latest/Admin/`
- 必须同步两个目录！

**补救**：使用 sync 脚本同步：
```bash
cp -f release/latest/Admin/DormManage.Admin.dll release/latest/TrayApp/Admin/
cp -f release/latest/Admin/DormManage.Shared.dll release/latest/TrayApp/Admin/
```

---

## 总结

按顺序执行 5 步，每步都可能定位到不同层次的 BUG：
- 步骤 1-2：源码/编译层问题（最常见）
- 步骤 3：发布问题
- 步骤 4：DLL 内容问题（缓存、强制编译）
- 步骤 5：**运行环境问题**（最容易被忽视！）← v2.13.193 关键修复点

---

**使用方式**：每次遇到"修改无效"BUG 都按此 5 步走一遍，95% 的 BUG 能在前 2 步找到。