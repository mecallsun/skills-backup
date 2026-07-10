---
name: lead-hunter
description: Sales lead collection skill. Given a one-line requirement, automatically plans search strategy, selects execution engine (Apify / Firecrawl / 高德地图 / 天眼查 / search-guided fallback), and outputs a deduplicated structured contact table. Use when asked to find company phone numbers, industry decision-makers, local business contacts (including Chinese domestic markets), or build prospect lists.
---

# Lead Hunter — 销售线索一站式采集

一条命令，覆盖所有线索场景。内部自动完成：需求解析 → 凭据检查 → 引擎链式协同 → 数据处理 → 统一输出。

---

## 前置凭据（均为可选）

| 环境变量 | 用途 | 未配置时 |
|---------|------|---------|
| `APIFY_TOKEN` | Apify 平台抓取（Google Maps / 社交平台） | 跳过 Apify 引擎 |
| `FIRECRAWL_API_KEY` | Firecrawl 网站爬取（行业目录 / 官网） | 跳过 Firecrawl 引擎 |
| `AMAP_API_KEY` | 高德地图 POI 搜索（**国内**本地商家电话） | 跳过高德引擎，降级百度或搜索引导 |
| `TIANYANCHA_TOKEN` | 天眼查开放平台（**国内**企业联系方式富化） | 跳过天眼查，尝试亿企查备选 |
| `YIQICHA_TOKEN` | 亿企查开放平台（天眼查备选） | 跳过亿企查引擎 |

所有凭据均为可选；均未配置时自动降级为搜索引导策略，无需 API Key 也可执行。

---

## 执行流程

### 第 1 步：需求解析

从用户输入中提取以下参数，信息充分直接执行，**最多追问 2 个问题**：

| 参数 | 说明 | 示例 |
|------|------|------|
| 目标类型 | 本地商家 / 公司联系人 / 特定角色 / 单个联系人 | 餐饮老板、销售总监 |
| 地区 | 城市 / 省份 / 全国 | 上海、北京 |
| 行业 | 行业类别或关键词 | 餐饮、SaaS、教育 |
| 规模 | 预期采集数量 | 50、200、1000 |
| 输出格式 | CSV / JSON / 联系人卡片（默认 CSV）| — |

**大规模提示**：预计超过 1000 条或耗时超过 30 分钟时，主动告知用户建议分段执行后再继续。

---

### 第 2 步：凭据检查 + 动态路由

**原则：任务类型优先，凭据状态其次。** 先判断任务适合哪个引擎，再检查该引擎凭据是否可用。

```
任务类型匹配（见引擎矩阵）
        ↓
首选引擎凭据是否可用？
  是 → 执行首选引擎
  否 → 备选引擎凭据是否可用？
         是 → 执行备选引擎
         否 → 搜索引导策略（兜底，无需 API）
```

**引擎选择矩阵**：

| 任务类型 | 首选引擎 | 备选引擎 |
|---------|---------|---------|
| **国内**本地商家 / 高德地图 | 高德地图 API | 百度地图 API → 搜索引导 |
| **国内**企业联系方式 / 工商富化 | 天眼查 API | 亿企查 API → 搜索引导 |
| 本地商家 / Google Maps（国际） | Apify | 搜索引导 |
| 网站 / 行业目录 / 数据库 | Firecrawl | Apify |
| LinkedIn / 社交平台 | Apify | 搜索引导 |
| 单个联系人挖掘 | 搜索引导 | Firecrawl |
| 公司官网联系方式 | Firecrawl | 搜索引导 |

**地区判断规则**：用户提到中国城市（上海/北京/广州/深圳等）或"国内"时，优先走高德 + 天眼查链路；其余默认走 Apify / Firecrawl 链路。

---

### 第 3 步：链式协同采集

引擎之间采用 **发现 → 提取 → 富化** 三阶段链式协同，而非简单二选一：

```
[发现阶段]  找到目标实体（公司名、地址、官网 URL）
     ↓
[提取阶段]  从官网 / 目录页面提取联系方式（电话、邮件）
     ↓
[富化阶段]  补全缺失字段（如仅有公司名，用 contact-info-scraper 挖邮件）
```

#### 引擎 A — Apify（发现阶段主力）

调用 Apify CLI，`--user-agent lead-hunter/1.0`：

| 场景 | Actor ID | 说明 |
|------|----------|------|
| 本地商家发现 | `compass/crawler-google-places` | Google Maps 商家信息 |
| 联系方式富化 | `vdrmota/contact-info-scraper` | 从 URL 批量提取邮件/电话（使用前确认 Actor 当前可用性）|
| Facebook 主页联系 | `apify/facebook-page-contact-information` | 公开 Facebook 主页 |
| Google Maps 邮件提取 | `poidata/google-maps-email-extractor` | Maps 商家官网邮件（社区维护，确认质量）|

```bash
# 运行 Actor（返回 run 元数据，提取 defaultDatasetId）
apify actors call "ACTOR_ID" -i 'JSON_INPUT' \
  --user-agent lead-hunter/1.0 --json 2>/dev/null

# 获取结果（20 条以上强制写文件）
apify datasets get-items DATASET_ID \
  --user-agent lead-hunter/1.0 --format csv 2>/dev/null > YYYY-MM-DD_leads.csv
```

**费用提示**：运行前告知用户预计采集量，1000 条以上建议用户先确认 Apify 配额余额。

#### 引擎 B — Firecrawl（提取阶段主力）

使用 Firecrawl `scrape` / `crawl` / `extract` 接口抓取目标页面。

- 适合：公开可访问的企业官网、行业目录、供应商列表页
- 支持：分页抓取、字段结构化提取、多页并发
- **不适合**：需要登录的付费数据库、反爬严格的平台（不绕过访问控制）

采集字段：公司名、联系人、职位、电话、邮件、官网 URL。

#### 引擎 D — 高德地图 API（国内本地商家发现）

适用场景：国内城市本地商家（餐饮、零售、教育、健身等），需要电话号码 + 地址。

**接口**：`GET https://restapi.amap.com/v3/place/text`

| 参数 | 说明 | 示例 |
|------|------|------|
| `key` | `$AMAP_API_KEY` | — |
| `keywords` | 行业关键词 | `餐厅` / `健身房` |
| `city` | 城市名或行政区代码 | `上海` / `310000` |
| `types` | POI 分类代码（可选） | `050000`（餐饮） |
| `offset` | 每页返回数量，最大 25 | `25` |
| `page` | 页码，最大 100 页 | `1` |

**返回字段**：`name`（商家名）、`tel`（电话）、`address`（地址）、`type`（行业分类）、`location`（坐标）

```bash
# 示例：搜索上海餐饮商家（第1页）
curl "https://restapi.amap.com/v3/place/text?key=$AMAP_API_KEY\
&keywords=餐厅&city=上海&offset=25&page=1&output=json"
```

**费用提示**：¥30/万次；开发版免费额度 5000次/日。1000条线索约 ¥3-6（含翻页）。运行前告知用户预计用量。

**MCP 集成**：若已配置 `amap-maps-mcp-server`，可直接调用 `maps_text_search` 工具，无需手写 HTTP 请求。

**链式用法**：高德返回商家名 + 官网 URL → 喂给引擎 B（Firecrawl）或引擎 E 补全邮件/联系人。

---

#### 引擎 E — 天眼查 / 亿企查 API（国内企业联系方式富化）

适用场景：已知公司名，需要获取注册电话、法人联系方式、主要人员信息。**富化阶段主力**，通常接在高德发现阶段之后使用。

**天眼查开放平台**（首选）：

| 接口 | 用途 | 价格 |
|------|------|------|
| 企业基本信息 | 注册信息（含官网/邮箱） | ¥0.10/次 |
| 企业联系方式 | 注册电话 + 联系邮箱 | ¥0.15/次 |
| 企业基本信息（含主要人员） | 法人/股东/高管 + 联系方式 | ¥0.25/次 |

```bash
# 按公司名获取联系方式
curl "https://open.tianyancha.com/cloud-other-information/companyContact/2.0?name=公司名称" \
  -H "Authorization: $TIANYANCHA_TOKEN"
```

**亿企查开放平台**（天眼查备选）：

- 接口：`https://openapi.yiqicha.com`
- 企业联系方式字段：联系人电话 + 职位 + 归属地
- 价格参考：~¥0.10/次

**调用限制**：天眼查默认 1000次/分钟；批量富化时建议每次请求间隔 100ms 以上。

**费用提示**：1000 条企业联系方式富化约 ¥100-250，运行前告知用户预计成本。

---

#### 引擎 C — 搜索引导（兜底策略，无需 API Key）

当两个引擎均不可用，或任务为单个联系人挖掘时，生成结构化搜索策略供用户执行或 Agent 调用搜索工具：

```
Google 搜索查询：
  "[公司名]" "[职位]" 电话 OR 邮箱
  site:linkedin.com/in "[姓名]" "[公司名]"
  site:[公司域名] contact OR about OR team

邮件格式推断：
  收集已知样本邮件 → 推断规律（如 firstname.lastname@domain.com）
  置信度标注为"推断"，需用户验证后使用
```

---

### 第 4 步：数据处理

| 步骤 | 规则 |
|------|------|
| 跨引擎去重 | 主键：`公司名（标准化）+ 电话（E.164格式）` 或 `公司名 + 邮箱（小写）` |
| 字段标准化 | 电话统一 E.164、邮箱统一小写、官网统一 canonical URL |
| 来源标注 | 每条记录标注采集引擎 + 来源 URL |
| 无效过滤 | 过滤格式错误的电话/邮件、完全空白的联系方式行 |
| 置信度标注 | 公开验证数据 = 高；从官网提取 = 中；推断/间接来源 = 低 |

---

### 第 5 步：统一输出

**⚠️ Token 防护规则：超过 20 条数据必须输出为 `.csv` 或 `.json` 文件，禁止直接在对话中渲染大型表格。**

**标准输出字段**：

| 字段 | 说明 |
|------|------|
| 姓名 | 联系人全名 |
| 职位 | 职务/职称 |
| 公司名称 | 所属公司 |
| 电话 | E.164 格式 |
| 邮箱 | 小写工作邮箱 |
| 公司官网 | Canonical URL |
| 地区 | 城市/省份 |
| 行业 | 所属行业 |
| 置信度 | 高 / 中 / 低（推断） |
| 来源引擎 | Apify / Firecrawl / 高德地图 / 天眼查 / 亿企查 / 搜索引导 |
| 来源 URL | 数据原始出处 |
| 采集时间 | YYYY-MM-DD |

**输出摘要模板**：

```markdown
# 线索采集结果：[目标描述]

## 摘要
- 采集引擎：[引擎链路]
- 原始条目：XX 条 → 去重后：XX 条
- 含有效联系方式：XX 条
- 高置信度：XX 条 / 中：XX 条 / 低（推断）：XX 条
- 数据缺口：[被屏蔽或不可见的字段]

## 文件路径
YYYY-MM-DD_leads.csv

## 重新执行参数
目标: [描述] | 地区: [地区] | 行业: [行业] | 数量: [N] | 格式: csv
```

---

## 失败处理

| 失败场景 | 处理方式 |
|---------|---------|
| API Key 无效 / 余额不足 | 跳过该引擎，降级到下一个可用引擎，告知用户 |
| Actor 不可用 / 返回错误 | 尝试备选 Actor，仍失败则降级搜索引导 |
| 目标网站禁止抓取（robots.txt）| 停止该目标，标注"不可采集"，继续其他目标 |
| 结果为空 | 告知用户并建议放宽条件（扩大地区/行业范围）|
| 重复率超过 80% | 提示数据源可能已枯竭，建议换源 |
| 高德 API `tel` 字段为空 | 该商家未在高德留存电话；降级用 Firecrawl 抓官网，或标注"无联系方式" |
| 天眼查返回"该企业无联系方式" | 联系方式未公开；尝试亿企查备选，仍空则标注置信度"低"并走搜索引导补全 |
| 高德/天眼查单日配额耗尽 | 暂停采集，告知用户剩余配额，建议次日继续或分批执行 |

---

## 禁止项

- 不采集非公开个人隐私信息
- 不绕过登录墙、付费墙、CAPTCHA 或访问控制
- 不将推断邮件标注为"已验证"
- 不在未告知用户成本的情况下运行大规模付费 Actor
- 遵守目标网站 `robots.txt`（Firecrawl 采集前检查）
- 遵守 GDPR / CCPA 及所在地数据保护法规
- **国内特别禁止**：不爬取大众点评、美团、1688、慧聪、微信、抖音等平台数据（违反平台 ToS，且可能构成"非法获取计算机信息系统数据罪"）；国内场景必须走高德/天眼查等官方 API
- **推断数据标注**：通过搜索引导推断的国内手机号/邮箱，置信度标注为"低（推断）"，提醒用户验证后再使用

---

## 调用示例

```
# 国内场景（走高德 + 天眼查链路）
/lead-hunter 找上海徐汇区餐饮商家电话，100条，输出CSV
/lead-hunter 找北京朝阳区健身房联系方式，50条
/lead-hunter 找深圳龙华区制造业企业负责人联系方式，200条
/lead-hunter 找广州天河区 IT 公司销售总监联系方式

# 国际场景（走 Apify / Firecrawl 链路）
/lead-hunter 找上海餐饮行业老板的电话，100条，输出CSV
/lead-hunter 找北京互联网公司的销售总监联系方式
/lead-hunter 找 Stripe 的工程负责人邮件
/lead-hunter 抓取这个行业目录的供应商联系信息：[URL]
```
