# Skill: 发布修复综合技能（v2.13.193）

## 名称
release-bugfix-v2.13.193

## 描述
金智住宿管理系统 v2.13.193 hotfix 修复综合技能。
包含：
- 发布目录双胞胎同步机制
- 隐私字段 deny-by-default 语义
- BUG 排查方法论
- 防错检查清单

## 触发条件

当用户的请求匹配以下任一条件时，应调用此 Skill：
- "发布程序包"、"重新发布"、"构建并发布"
- "隐私字段不显示"、"隐私字段没生效"、"权限没生效"
- "BUG 没修复"、"修改没生效"、"看不见我的修改"
- "发布目录"、"TrayApp"、"启动失败"
- 涉及强托管规则、隐私字段、发布脚本、TrayApp 加载路径

## Skill 内容文件

| 文件 | 用途 |
|------|------|
| `SKILL.md`（本文件） | Skill 元数据 + 触发条件 |
| `methodology.md` | BUG 排查 5 步方法论 |
| `checklists.md` | 4 类检查清单 |
| `privacyfield-guide.md` | 隐私字段 deny-by-default 实施指南 |
| `release-guide.md` | 发布同步完整指南 |

## 快速调用

```bash
# 触发本 Skill 时，按以下顺序执行：
# 1. 加载 methodology.md 进行排查
# 2. 加载 checklists.md 选择对应清单
# 3. 按用户需求加载指南文档
```

## 核心原则

1. **deny-by-default 是默认安全选择**：不勾选 → 隐藏
2. **发布路径必须同步两个目录**：`release/latest/` 和 `release/latest/TrayApp/`
3. **跨权限测试必须 2 角色**：admin + 未授权角色
4. **StringEncoding 差异**：.NET DLL 中文用 UTF-16 LE 不是 ASCII
5. **git 操作可能回滚**：Edit 后立即 git diff

## 当前最新文档引用

- BUG 解决经验综述：`00-方案文档/232-BUG解决经验与防错指南-v2.13.193综述.md`
- 隐私字段修复：`00-方案文档/231-隐私字段语义翻转终极修复-v2.13.193.md`
- 发布双胞胎陷阱：`00-方案文档/230-发布目录双胞胎陷阱-TrayApp加载路径不一致-v2.13.193.md`
- 发布规范：`00-方案文档/99-发布程序包与部署规范-v2.13.193.md`
- 同步脚本：`scripts/sync_publish_to_trayapp.sh`
- 检查清单：`scripts/publish_checklist.md`

## 永久教训（已在 232 文档中详述）

1. **文档与代码必须同步 commit**
2. **发布双胞胎必须同步**
3. **Shared DLL 必须全子目录同步**
4. **deny-by-default 是默认安全选择**
5. **方法名必须反映语义**
6. **跨权限测试必须 2 种角色**
7. **git 操作可能回滚修改**
8. **.NET DLL 字符串是 UTF-16 LE 编码**
9. **TypeLoadException 是 DLL 版本不一致**
10. **cp -r 前必须先 rm -rf**

## 使用流程

当你（AI）遇到用户报告 "BUG 没修复" 类问题时：

### 第 1 步：识别场景
确认用户报告与以下哪一类相符：
- A. 隐私字段不生效 → 加载 `privacyfield-guide.md`
- B. 发布没生效 → 加载 `release-guide.md`
- C. 启动失败 → 加载 `release-guide.md` + `methodology.md`

### 第 2 步：执行 5 步排查
按 `methodology.md` 的步骤排查：
1. 源码层确认
2. 编译层确认
3. 发布时间戳确认
4. DLL 内容确认
5. 运行环境确认

### 第 3 步：执行修复
按对应指南执行修复。

### 第 4 步：验证
按对应检查清单验证。

### 第 5 步：发布
使用 `release-guide.md` 中的 sync 脚本发布。

---

**版本**：v2.13.193  
**适用项目**：金智住宿管理系统（DormManage）  
**创建日期**：2026-07-27