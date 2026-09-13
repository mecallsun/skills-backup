# Claude Code × 飞书多维表格实战教学
## 以「小元说」AI内容生产系统为案例，从0到1打通自动化工作流

> 案例主体：元说科技 · 黄兴元
> 系统规模：31条选题 · 19个字段 · 31篇成品脚本 · 飞书云文档全量关联
> 核心工具：Claude Code + 飞书开放平台 API + Python
> 完成时间：单次对话内完成全部搭建

---

## 一、背景与痛点：为什么要做这件事

「小元说」账号每周产出多条短视频，工作流横跨多个环节：

```
选题想法 → 深度研究 → 脚本创作 → 录制发布 → 数据复盘
```

每个环节都有 AI skill 参与，本地积累了大量 `.md` 脚本文件，但管理方式极其原始：

- 选题库 = 一个本地 `.md` 文件，靠人工维护状态
- 脚本文件 = 按编号命名，找文件靠记忆
- 发布数据 = 散落在备忘录，无法汇总复盘

**目标：** 以飞书多维表格为管理中枢，用 Claude Code 打通从「选题登记」到「脚本建档」的全流程自动化。

---

## 二、第一步：梳理业务需求

> 这是整个项目最重要的一步，决定了后续所有设计。跳过这步直接上手建表，大概率返工。

### 2.1 想清楚「管什么」

在动手之前，先和 Claude Code 对话，把业务逻辑说清楚：

**要问自己的三个问题：**

**① 这个表要管理什么对象的全生命周期？**

本案例的答案：「一条内容」从想法到复盘的全过程。

**② 这个对象的生命周期有哪些阶段？**

```
想法 → 研究中 → 脚本完成 → 已采用 → 已录制 → 已发布 → 已复盘
```

每个阶段都意味着不同的操作主体（AI / 人）和不同的数据产出。

**③ 每个阶段需要记录哪些数据？**

| 生命周期阶段 | 需要记录的信息 |
|------------|-------------|
| 选题阶段 | 标题、主题方向、核心观点、目标受众、来源、评分 |
| 生产阶段 | 成品文件编号、关联脚本文档（含多版本）|
| 发布阶段 | 发布日期、抖音链接、视频号链接 |
| 复盘阶段 | 核心数据摘要、综合评级 |
| 系统字段 | 内容状态（贯穿全程）、备注、创建时间 |

### 2.2 确定「两层分离」架构

在梳理需求时发现一个关键判断：**脚本全文不应该放在多维表格里**。

原因：
- 一篇5分钟脚本约1000字，多条就会让表格臃肿
- 飞书多维表格的文本字段不适合长文档阅读
- 飞书云文档才是阅读和编辑长内容的正确载体

**最终架构决策：**
```
多维表格  →  元数据索引层（19个字段）
飞书云文档 →  内容存储层（完整脚本，通过URL链接到表格）
```

这一决策在动手前就定下来，避免后期返工。

### 2.3 确定「字段枚举值」的语义

**字段设计中最容易犯的错误：** 用自由文本代替枚举值。

反例：「来源」字段如果让用户自己填，会出现「自己想的」「雷达」「自创意」等不一致值，后续无法筛选和统计。

本案例的来源字段经历了一次重要的语义澄清：

| 原设计 | 问题 | 修正后 |
|-------|------|-------|
| 自创 | ✅ 准确 | 自创 |
| 选题雷达 | ❌ Alpha School 是用户指定的，不该算雷达 | 选题雷达 |
| — | 缺少「用户给线索，AI研究落地」的分类 | 指定研究（新增）|
| — | 缺少跟拍竞品的来源 | 竞品跟拍（新增）|

**规则：每个枚举值都必须有清晰的判断标准，不能模糊。**

---

## 三、第二步：与Claude Code对话，设计字段方案

### 3.1 正确的对话姿势

不要上来就说「帮我建一个飞书表格」，而是先把业务描述清楚，让 Claude Code 参与字段设计。

**有效的对话示例：**

```
用户：我在做一个自媒体账号，需要管理选题从想法到发布的全过程。
     选题有这些阶段：想法→研究中→脚本完成→已录制→已发布→已复盘。
     我还需要记录每条视频发布后的播放量、完播率这些数据。
     你帮我设计一下多维表格的字段，要考虑哪些维度？

Claude：好的，根据你的需求，字段应该分几个层次来设计...
```

### 3.2 本案例最终字段设计

经过对话迭代，最终确定19个字段，按功能分组：

**基础信息组（选题阶段填写）**

| 字段名 | 类型 | 说明 | 枚举值 |
|--------|------|------|--------|
| 选题标题 | 文本 | 主键，查重依据 | — |
| 所属主题 | 单选 | 内容方向 | 职场开挂/教育思考/商业观察/案例拆解 |
| 核心观点 | 文本 | 一句话总结 | — |
| 目标受众 | 文本 | 具体人群描述 | — |
| 来源 | 单选 | 选题如何产生 | 自创/选题雷达/指定研究/竞品跟拍 |
| 评分 | 文本 | 0-10分，AI主观评估 | — |
| 内容状态 | 单选 | 全程状态机 | 想法/研究中/脚本完成/已采用/已录制/已发布/已复盘 |
| 备注 | 文本 | 自由备注 | — |

**生产信息组（脚本阶段填写）**

| 字段名 | 类型 | 说明 |
|--------|------|------|
| 成品文件编号 | 文本 | 如 031，对应本地文件名前缀 |
| 关联脚本文档 | 文本 | 飞书云文档URL，支持多版本（⭐标记推荐版）|
| 发布平台 | 文本 | 默认：抖音+视频号 |
| 时长 | 文本 | 默认：5分钟 |

**发布复盘组（发布后填写）**

| 字段名 | 类型 | 说明 |
|--------|------|------|
| 发布日期 | 日期 | 实际发布时间 |
| 抖音链接 | 文本 | 抖音视频链接 |
| 视频号链接 | 文本 | 视频号链接 |
| 综合评级 | 单选 | 爆款/良好/普通/待优化 |
| 核心数据 | 文本 | 如「抖音8.2万播 完播率41%」|

**系统自动字段（无需手动维护）**

| 字段名 | 类型 | 说明 |
|--------|------|------|
| 创建人 | 修改人 | 自动记录最后操作者 |
| 记录时间 | 创建时间 | 自动记录创建时间 |

### 3.3 字段设计的核心原则

1. **元数据放表格，内容放云文档** — 超过一行的内容不放表格字段
2. **能用枚举就不用自由文本** — 所有状态、来源、评级都用单选
3. **每个字段都要有明确的「谁来填、什么时候填」** — 没有明确归属的字段是噪音
4. **先把常用字段设计好，复杂需求迭代加** — 本案例从11个字段扩展到19个

---

## 四、第三步：一键导入飞书，快速建立多维表格

> 这是 Claude Code × 飞书最有冲击力的演示点：不用在飞书界面里一个个手动建字段，而是写一个脚本，一次执行，19个字段全部到位。

### 4.1 为什么要用脚本建表

| 手动建表 | 脚本建表 |
|---------|---------|
| 19个字段逐个点击新建 | 一次执行，30秒完成 |
| 枚举值手动输入，容易打错 | 代码里定义，精准无误 |
| 无法复现，换个表格要重建 | 脚本可重复使用 |
| 修改字段顺序要手动拖拽 | 改代码重跑即可 |

### 4.2 字段创建脚本

```python
import urllib.request, json

# 配置
APP_ID     = "cli_xxxx"; APP_SECRET = "xxxx"
APP_TOKEN  = "xxxx"; TABLE_ID = "xxxx"
BASE       = "https://open.feishu.cn/open-apis"

# 获取 token
req = urllib.request.Request(f"{BASE}/auth/v3/tenant_access_token/internal",
    data=json.dumps({"app_id": APP_ID, "app_secret": APP_SECRET}).encode(),
    headers={"Content-Type": "application/json"})
with urllib.request.urlopen(req) as r:
    token = json.loads(r.read())["tenant_access_token"]
h = {"Content-Type": "application/json", "Authorization": f"Bearer {token}"}

# 定义所有字段
FIELDS = [
    # 基础信息
    {"field_name": "所属主题", "type": 3, "property": {"options": [
        {"name": "职场开挂"}, {"name": "教育思考"},
        {"name": "商业观察"}, {"name": "案例拆解"}]}},
    {"field_name": "核心观点", "type": 1},
    {"field_name": "目标受众", "type": 1},
    {"field_name": "来源", "type": 3, "property": {"options": [
        {"name": "自创"}, {"name": "选题雷达"},
        {"name": "指定研究"}, {"name": "竞品跟拍"}]}},
    {"field_name": "评分", "type": 1},
    {"field_name": "内容状态", "type": 3, "property": {"options": [
        {"name": "想法"}, {"name": "研究中"}, {"name": "脚本完成"},
        {"name": "已采用"}, {"name": "已录制"}, {"name": "已发布"}, {"name": "已复盘"}]}},
    {"field_name": "备注", "type": 1},
    # 生产信息
    {"field_name": "成品文件编号", "type": 1},
    {"field_name": "关联脚本文档", "type": 1},
    {"field_name": "发布平台", "type": 1},
    {"field_name": "时长", "type": 1},
    # 发布复盘
    {"field_name": "发布日期", "type": 5, "property": {
        "date_formatter": "yyyy/MM/dd", "auto_fill": False}},
    {"field_name": "抖音链接", "type": 1},
    {"field_name": "视频号链接", "type": 1},
    {"field_name": "综合评级", "type": 3, "property": {"options": [
        {"name": "爆款"}, {"name": "良好"},
        {"name": "普通"}, {"name": "待优化"}]}},
    {"field_name": "核心数据", "type": 1},
]

# 批量创建
for field in FIELDS:
    body = json.dumps(field, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(
        f"{BASE}/bitable/v1/apps/{APP_TOKEN}/tables/{TABLE_ID}/fields",
        data=body, headers=h, method="POST")
    with urllib.request.urlopen(req) as r:
        result = json.loads(r.read())
    status = "✓" if result.get("code") == 0 else f"✗ {result}"
    print(f"{status} {field['field_name']}")
```

**字段类型速查：**

| type 值 | 对应类型 | 适用场景 |
|--------|---------|---------|
| 1 | 文本 | 自由输入的内容 |
| 3 | 单选 | 有固定枚举值的字段 |
| 5 | 日期 | 时间类字段 |

### 4.3 批量导入历史数据

建好表格后，把已有的历史数据一次性批量写入：

```python
# 批量插入记录示例
records = [
    {"fields": {
        "选题标题": "中年人AI觉醒",
        "所属主题": "职场开挂",
        "内容状态": "脚本完成",
        "来源": "自创",
        "评分": "9.1",
        "成品文件编号": "015/016/017/018"
    }},
    # ... 更多记录
]

for record in records:
    body = json.dumps(record, ensure_ascii=False).encode("utf-8")
    req = urllib.request.Request(
        f"{BASE}/bitable/v1/apps/{APP_TOKEN}/tables/{TABLE_ID}/records",
        data=body, headers=h, method="POST")
    with urllib.request.urlopen(req) as r:
        result = json.loads(r.read())
    print("✓" if result.get("code") == 0 else result)
```

本案例一次性导入了14条记录，覆盖所有历史选题。

---

## 五、整体架构与工具设计

完成前三步（需求→设计→建表）后，进入工具开发阶段。

**架构图：**
```
本地 .md 成品文件
       ↕
feishu_sync.py（5个命令行子命令）
       ↕
飞书多维表格（索引层）  ←→  飞书云文档（内容层）
```

**feishu_sync.py 五个核心命令：**

| 命令 | 作用 | 典型使用时机 |
|------|------|------------|
| `add` | 新增选题记录 | 选题确认后立即登记 |
| `update` | 更新任意字段 | 状态变更、发布后填数据 |
| `link-doc` | 创建云文档并关联 | 脚本完成后建档 |
| `list` | 列出全部选题 | 日常查看进度 |
| `get` | 查看单条详情 | 确认某条记录的完整信息 |

**核心命令示例：**

```bash
# 登记新选题
python feishu_sync.py add \
  --title "选题标题" --theme "职场开挂" \
  --point "核心观点" --audience "目标受众" \
  --source "选题雷达" --score "9.2"

# 创建飞书云文档并关联（推荐版置顶）
python feishu_sync.py link-doc \
  --title "选题标题" \
  --md-file "E:\...\031-xxx.md" \
  --label "031-正式版" --recommended

# 发布后更新复盘数据
python feishu_sync.py update \
  --title "选题标题" --status "已发布" \
  --pub-date "2026/03/08" --douyin "https://..." \
  --rating "爆款" --data "抖音8.2万播 完播率41%"
```

---

## 六、关键技术点

### 6.1 飞书 API 认证

```python
def _get_token(self):
    url  = f"{BASE_URL}/auth/v3/tenant_access_token/internal"
    body = json.dumps({"app_id": APP_ID, "app_secret": APP_SECRET}).encode()
    req  = urllib.request.Request(url, data=body,
                                  headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req) as resp:
        data = json.loads(resp.read())
    self._token = data["tenant_access_token"]
    return self._token
```

Token 有效期约2小时，单次脚本运行缓存即可，无需每次请求都重新获取。

### 6.2 Markdown → 飞书 Block 转换

云文档不支持直接传 Markdown，必须转为 Block 结构：

```python
def md_to_blocks(md_text):
    blocks = []
    for line in md_text.splitlines():
        s = line.rstrip()
        if s.startswith("# ") and not s.startswith("## "):
            blocks.append({"block_type": 3,  # heading1
                "heading1": {"elements": [{"text_run": {"content": s[2:]}}]}})
        elif s.startswith("## "):
            blocks.append({"block_type": 4, ...})  # heading2
        else:
            # 处理 **粗体**，拆分为多个 text_run
            parts = re.split(r"(\*\*[^*]+\*\*)", s)
            elements = [{"text_run": {"content": p[2:-2],
                "text_element_style": {"bold": True}}}
                if p.startswith("**") else
                {"text_run": {"content": p}}
                for p in parts if p]
            blocks.append({"block_type": 2, "text": {"elements": elements}})
    return blocks
```

每批最多写入50个 Block，需分批调用接口。

### 6.3 多版本文档关联

同一选题可能有多个脚本版本，设计规则：⭐ 标记推荐版并始终置顶，追加版排在后面，新推荐版自动撤掉旧 ⭐。

```
字段内容示例：
⭐ 031-正式版: https://xxx.feishu.cn/docx/Xws3dpEF...
031-初稿版:   https://xxx.feishu.cn/docx/AbcDefG...
```

---

## 七、踩坑全记录

### 坑①：403 写入权限（最常见）

**现象：** 读取正常，写入报 `code: 91403`
**原因：** 应用机器人无法写入个人空间
**解决：** 多维表格移至「共享空间」→ 将机器人添加为协作者

> 建表之前就要确认这一点，否则建完表才发现写不进去。

### 坑②：批量删除 API 失效

**现象：** 批量 DELETE 返回 `RecordIdNotFound`
**解决：** 改为循环逐条 DELETE，单条接口稳定可靠

### 坑③：Shell `!` 字符转义

**现象：** `bash -c` 内联 Python 代码含 `!=` 时报错
**解决：** 代码写入临时 `.py` 文件后执行，避免 shell 解析干扰

### 坑④：文档链接变成纯文本

**现象：** 表格里显示的是文本，无法点击跳转
**原因：** 读取 API 返回的字段值时 URL 丢失，把纯文本写回了字段
**解决：** 清空字段 → 重新执行 `link-doc` 创建新文档

### 坑⑤：ModifiedUser 字段显示旧机器人名

**分析：**
- `CreatedUser`（type: 1002）= 创建人，**永久不可改**
- `ModifiedUser`（type: 1004）= 修改人，**每次 PUT 记录都会刷新**

**解决：** 批量 PUT 所有记录（写入相同的备注值），触发 ModifiedUser 更新为新名字

---

## 八、字段设计迭代过程

表格不是一次设计好的，本案例经历了三轮迭代：

**第一版（11个字段）：** 基础元数据 + 状态管理
选题标题 / 所属主题 / 核心观点 / 目标受众 / 内容状态 / 来源 / 发布平台 / 时长 / 成品文件编号 / 关联脚本文档 / 备注

**第二版（+1个字段）：** 补充质量评估维度
新增「评分」— 选题阶段由 AI 打分，便于排优先级

**第三版（+5个字段）：** 补充发布复盘维度
新增「发布日期 / 抖音链接 / 视频号链接 / 综合评级 / 核心数据」

**迭代节奏：** 先跑通核心流程，再根据实际使用中发现的缺口补充字段，而不是一开始就把所有可能的字段都加上。

---

## 九、工作流集成：选题雷达7步流程

将 feishu_sync.py 嵌入内容生产工作流，实现 AI → 本地文件 → 飞书 的全程自动化：

```
① WebSearch 扫描近7天AI信号，过三关筛选
         ↓
② feishu_sync.py add   登记到多维表格
         ↓
③ WebSearch 深度研究（数据/案例/反常识角度）
         ↓
④ 按脚本创作师规范生成完整脚本
         ↓
⑤ 保存本地 .md 文件
   命名规则：031-选题关键词-5分钟-抖音视频号.md
         ↓
⑥ feishu_sync.py link-doc   创建飞书云文档，写入内容，关联到表格
         ↓
⑦ feishu_sync.py update     更新状态为「脚本完成」，填写文件编号
```

发布后追加复盘：
```bash
python feishu_sync.py update --title "..." \
  --status "已发布" --pub-date "2026/03/08" \
  --douyin "https://..." --rating "爆款" \
  --data "抖音8.2万播 完播率41%"
```

---

## 十、最终成果与可复用代码

### 10.1 本案例成果

| 指标 | 数量 |
|------|------|
| 多维表格字段数 | 19个 |
| 已入库选题 | 14条 |
| 本地成品脚本 | 31篇（001-031）|
| 飞书云文档 | 14个（自动创建并写入内容）|
| 来源分布 | 自创3 / 选题雷达10 / 指定研究1 |

### 10.2 通用飞书 API 请求封装

```python
import urllib.request, urllib.parse, json

def get_token(app_id, app_secret):
    req = urllib.request.Request(
        "https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal",
        data=json.dumps({"app_id": app_id, "app_secret": app_secret}).encode(),
        headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req) as r:
        return json.loads(r.read())["tenant_access_token"]

def api(method, path, token, body=None, params=None):
    url = f"https://open.feishu.cn/open-apis{path}"
    if params: url += "?" + urllib.parse.urlencode(params)
    data = json.dumps(body, ensure_ascii=False).encode("utf-8") if body else None
    req = urllib.request.Request(url, data=data, method=method, headers={
        "Content-Type": "application/json",
        "Authorization": f"Bearer {token}"})
    with urllib.request.urlopen(req) as r:
        result = json.loads(r.read())
    if result.get("code") != 0: raise RuntimeError(result)
    return result.get("data", {})
```

### 10.3 分页获取全部记录

```python
def list_all_records(token, app_token, table_id, filter_expr=None):
    path = f"/bitable/v1/apps/{app_token}/tables/{table_id}/records"
    params = {"page_size": 100}
    if filter_expr: params["filter"] = filter_expr
    data = api("GET", path, token, params=params)
    records = data.get("items", [])
    while data.get("has_more"):
        params["page_token"] = data["page_token"]
        data = api("GET", path, token, params=params)
        records += data.get("items", [])
    return records
```

---

## 十一、给学员的关键认知

**关于设计思维：**
1. **需求梳理比写代码更重要** — 想清楚「管什么、有哪些阶段、每个阶段记什么」，比上来就建表省时间10倍
2. **两层分离是通用架构** — 凡是「索引+内容」的场景，都适合「多维表格+云文档」的两层设计
3. **枚举值的语义要精确** — 模糊的枚举值在数据量大了以后会让你后悔

**关于工具使用：**
4. **飞书开放平台 = 标准 REST API** — 用 Python 标准库即可调用，无需任何第三方 SDK
5. **脚本建表比手动建表强** — 19个字段一键到位，可复现，可迁移
6. **共享空间是硬性前提** — 机器人无法写入个人空间，建表前就要确认

**关于 Claude Code：**
7. **Claude Code 不只是写代码的** — 需求分析、字段设计、数据建模、工作流规划，全程都可以对话迭代
8. **描述业务比描述技术更有效** — 告诉 Claude「我要管理内容从选题到发布的全过程」，比说「帮我写一个 Python 脚本」得到的结果更好
9. **先跑通一条再扩展** — 先完成一条记录的增删改查，再批量，再复杂工作流，不要一步跨太大

---

*文档生成时间：2026-03-07*
*对应项目：小元说 · AI内容生产系统 v1.0*
