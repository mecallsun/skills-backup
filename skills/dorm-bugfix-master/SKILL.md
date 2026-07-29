# Skill: dorm-bugfix-master（DormManage 全项目 BUG 修复主索引）

> **版本**：v2.13.193 终极综合
> **日期**：2026-07-28
> **类型**：Skill 主索引 — 跨项目 BUG 解决经验库

---

## 概述

本 Skill 是 DormManage（金戈宿舍管理系统）所有 BUG 修复经验的**主索引**。
涵盖 5+ 个 BUG 类别、10+ 个具体 BUG 案例、4+ 个发布问题、3+ 个隐私/权限类问题。

**核心价值**：
- 新成员 30 分钟内了解项目所有已知 BUG
- 重复 BUG 不再发生
- 所有 BUG 修复都有统一规范

---

## Skill 目录结构

```
skills/dorm-bugfix-master/
├── SKILL.md                  本文件（主索引）
├── bug-categories.md         7 大 BUG 类别分类
├── known-bugs.md             已知 BUG 案例库
├── bugfix-procedures.md      标准 BUG 修复流程
├── code-style-standards.md   代码规范（防止引入新 BUG）
├── testing-checklist.md      测试清单
├── deployment-checklist.md   部署清单
└── emergency-procedures.md   紧急情况处理流程
```

---

## BUG 类别总览

| # | 类别 | 出现次数 | 严重度 | 文档 |
|---|------|---------|--------|------|
| 1 | 发布目录同步问题 | 3+ | 🔴 P0 | 230 |
| 2 | 隐私/权限语义问题 | 5+ | 🔴 P0 | 231 |
| 3 | 注册/许可证问题 | 3+ | 🔴 P0 | 228 |
| 4 | UI 一致性问题 | 5+ | 🟡 P1 | 225, 227 |
| 5 | 编译错误 | 4+ | 🟡 P1 | 226, 232 |
| 6 | 数据/业务规则 | 2+ | 🟡 P1 | 233 |
| 7 | 部署/运行环境 | 2+ | 🟡 P1 | 232 |

详细列表见 `bug-categories.md`。

---

## 已知 BUG 案例库

详见 `known-bugs.md`，包含：
- v2.13.187 隐私字段 Dorms 接线缺失
- v2.13.188 当前入住人员新增性别列
- v2.13.191 RegStatus 拆分
- v2.13.193 发布目录双胞胎陷阱
- v2.13.193 隐私字段 deny-by-default 翻转
- v2.13.193 账号有效期判定 `>` → `>=`
- v2.13.193 编辑有效期 `datetime-local` → `date`

每个 BUG 都有完整的：症状 / 调查路径 / 根因 / 修复 / 教训 / 文档链接。

---

## 标准 BUG 修复流程

详见 `bugfix-procedures.md`，包含 7 步流程：
1. 确认症状（用户原话）
2. 复现 BUG
3. 5 步深度排查（methodology）
4. 定位根因
5. 设计修复方案
6. 实施修复
7. 验证 + 文档 + 发布

---

## 触发本 Skill 的关键词

当用户报告以下问题时，**自动加载本 Skill**：
- "BUG 没修复"、"修改没生效"、"看不见效果"
- "发布"、"重新发布"、"构建并发布"
- "隐私字段"、"权限"、"有效期"
- "启动失败"、"TypeLoadException"、"DLL 找不到"
- "用户管理"、"角色管理"、"部门"、"班级"
- "重置密码"、"登录失败"

---

## 完整文档引用

### BUG 修复历史（按时间排序）

| 版本 | 主要 BUG | 文档 | Skill |
|------|---------|------|-------|
| v2.13.187 | 隐私字段 Dorms 接线缺失 | 226 | [release-bugfix-v2.13.193] |
| v2.13.188 | 详情页缺性别列 | 227 | [release-bugfix-v2.13.193] |
| v2.13.191 | RegStatus 拆分 | 228 | [release-bugfix-v2.13.193] |
| v2.13.193 | 发布目录双胞胎 | 230 | [release-bugfix-v2.13.193] |
| v2.13.193 | 隐私字段 deny-by-default 翻转 | 231 | [release-bugfix-v2.13.193] |
| v2.13.193 | 账号有效期判定 | 233 | [release-bugfix-v2.13.193] |
| v2.13.193 | 隐私字段接线 | 232 | [release-bugfix-v2.13.193] |

### 综合文档

- `00-方案文档/232-BUG解决经验与防错指南-v2.13.193综述.md`（v2.13.193 综述）
- `00-方案文档/229-v2.13.187到v2.13.191综合方案.md`（早期综合）
- `00-方案文档/99-发布程序包与部署规范-v2.13.193.md`（发布规范）

### Skill 子目录

- `skills/release-bugfix-v2.13.193/`（发布+隐私字段专项）
- `skills/dorm-bugfix-master/`（本 Skill，全项目主索引）

---

## 永久原则（10 条）

1. **文档与代码必须同步 commit**
2. **发布双胞胎必须同步**
3. **Shared DLL 必须全子目录同步**
4. **deny-by-default 是默认安全选择**
5. **方法名必须反映语义**
6. **跨权限测试必须 2 角色**
7. **.NET DLL 字符串是 UTF-16 LE 编码**
8. **git 操作可能回滚修改**
9. **隐私字段 PII 接线必须立即完成**
10. **EF Core 上下文不缓存跨请求**

---

## 何时使用本 Skill

### 适合使用
- 用户报告任何 BUG、问题
- 准备新功能开发前
- 准备发布前
- 准备代码审查前
- 准备部署前

### 不适合使用
- 纯配置问题（看 appsettings.json 即可）
- 纯 UI 样式问题（看 _Layout.css 即可）
- 简单数据查询（直接查 DB 即可）

---

## 相关资源

- `CLAUDE.md`（项目级指令，含 v2.13.193 备注）
- `skills/release-bugfix-v2.13.193/SKILL.md`（专项 Skill）
- `00-方案文档/`（80+ 设计文档）

---

**使用建议**：遇到任何 BUG 修复任务，先加载 `bug-categories.md` 确定类别，再加载 `bugfix-procedures.md` 走 7 步流程，最后加载 `known-bugs.md` 查看是否已有类似案例。