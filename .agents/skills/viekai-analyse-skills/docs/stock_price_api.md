# 股票实时价格API使用指南

## 概述

company-financial-analysis skill 现已集成实时股票价格获取功能，可以自动获取A股、港股、美股的实时行情数据。

## 技术架构

```
company-financial-analysis
    ↓
stock_price_fetcher.py
    ↓
uu_server API (39.96.211.212:5000)
    ↓
FutuOpenD + 新浪财经 + 聚合数据
```

### 数据来源

- **A股（沪深）**: FutuOpenD API
- **港股**: FutuOpenD API
- **美股**: 新浪财经API（主） + 聚合数据API（备）

## 功能特性

### 1. 自动获取实时股价

在运行 `analyze_company.py` 时，会自动获取目标股票的实时行情：

```bash
python analyze_company.py 601127.SH
```

输出示例：
```
================================================================================
获取实时股价和市值数据
================================================================================

  ✓ 当前股价: 104.97 元
  ✓ 数据来源: futu_api
  ✓ 更新时间: 2026-01-30 13:43:01
```

### 2. 市值计算

使用 `stock_price_fetcher.py` 可以计算股票市值：

```python
from stock_price_fetcher import StockPriceFetcher

fetcher = StockPriceFetcher()

# 计算赛力斯市值（总股本17.42亿股）
result = fetcher.calculate_market_cap("601127.SH", 17.42)

print(result)
# 输出:
# {
#   "stock_code": "601127.SH",
#   "price": 104.96,
#   "total_shares": 17.42,
#   "market_cap": 182840320000.0,
#   "market_cap_display": "1828.40亿"
# }
```

### 3. 独立查询工具

可以单独使用 `stock_price_fetcher.py` 查询任意股票：

```bash
# 查询赛力斯
python stock_price_fetcher.py 601127.SH

# 查询腾讯
python stock_price_fetcher.py 00700.HK

# 查询苹果
python stock_price_fetcher.py AAPL.US
```

## API接口说明

### 端点1: 获取股票行情

```http
GET http://39.96.211.212:5000/api/stock/quote/{stock_code}
```

**示例**:
```bash
curl "http://39.96.211.212:5000/api/stock/quote/601127.SH"
```

**响应**:
```json
{
  "code": 0,
  "message": "success",
  "data": {
    "stock_code": "601127.SH",
    "name": "赛力斯",
    "price": 104.97,
    "prev_close": 0.0,
    "change": 0.0,
    "change_percent": 0.0,
    "source": "futu_api"
  },
  "timestamp": "2026-01-30 13:43:01"
}
```

### 端点2: 计算市值

```http
GET http://39.96.211.212:5000/api/stock/market-cap/{stock_code}?total_shares={shares}
```

**参数**:
- `total_shares`: 总股本（单位：亿股）

**示例**:
```bash
curl "http://39.96.211.212:5000/api/stock/market-cap/601127.SH?total_shares=17.42"
```

**响应**:
```json
{
  "code": 0,
  "data": {
    "stock_code": "601127.SH",
    "price": 104.96,
    "total_shares": 17.42,
    "market_cap": 182840320000.0,
    "market_cap_display": "1828.40亿"
  }
}
```

### 端点3: 批量查询

```http
POST http://39.96.211.212:5000/api/stock/batch-quote
Content-Type: application/json

{
  "stock_codes": ["601127.SH", "00700.HK", "AAPL.US"]
}
```

## 在分析报告中的应用

获取的实时数据会自动保存到 `processed_data/company_info.json`：

```json
{
  "stock_code": "601127.SH",
  "market": "SH",
  "market_type": "A股",
  "market_data": {
    "price": 104.97,
    "prev_close": 0.0,
    "change": 0.0,
    "change_percent": 0.0,
    "update_time": "2026-01-30 13:43:01",
    "source": "futu_api"
  }
}
```

AI分析时会看到：

```markdown
## 实时市场数据

| 指标 | 数值 |
|------|------|
| 当前股价 | 104.97 元 |
| 昨日收盘 | 0.0 元 |
| 涨跌额 | 0.0 元 |
| 涨跌幅 | 0.0% |
| 更新时间 | 2026-01-30 13:43:01 |

**注意**: 使用此实时价格计算市值、PE等估值指标时，需结合财报中的净利润和总股本数据。
```

## 估值指标计算示例

结合财报数据和实时股价，可以计算：

### 市值
```
市值 = 当前股价 × 总股本
     = 104.97元 × 17.42亿股
     = 1828.40亿元
```

### 市盈率（PE-TTM）
```
PE = 市值 / TTM净利润
   = 1828.40亿 / 76.8亿  (假设TTM净利润)
   = 23.81倍
```

### 市净率（PB）
```
PB = 市值 / 净资产
   = 1828.40亿 / 278.03亿  (从财报获取)
   = 6.58倍
```

## 错误处理

如果API服务不可用，分析流程会继续执行，只是缺少实时数据：

```
================================================================================
获取实时股价和市值数据
================================================================================

  ⚠ 股价API服务不可用（将继续分析）
```

## 配置说明

默认API服务器: `http://39.96.211.212:5000`

如需更改服务器地址，修改 `stock_price_fetcher.py`:

```python
fetcher = StockPriceFetcher(server_url="http://your-server:5000")
```

## 限制和注意事项

1. **数据延迟**:
   - A股/港股: 实时（FutuOpenD）
   - 美股: 延迟约15分钟（新浪财经）

2. **请求频率**:
   - 单次查询无限制
   - 批量查询建议每次不超过50只股票

3. **交易时段**:
   - 非交易时段返回最后收盘价
   - `change` 和 `change_percent` 可能为0

4. **依赖服务**:
   - 需要 uu_server 运行在 39.96.211.212:5000
   - 需要 FutuOpenD 连接（由uu_server管理）

## 故障排查

### 问题1: "股价API服务不可用"

**原因**: uu_server 服务未运行

**解决**:
```bash
ssh obsidian-server "cd ~/tools && nohup python3 uu_server.py > uu_server.log 2>&1 &"
```

### 问题2: "无法获取股票价格"

**可能原因**:
1. 股票代码格式错误
2. FutuOpenD连接失败
3. 股票不存在或已退市

**解决**:
- 检查股票代码格式（如 601127.SH）
- 查看服务器日志: `ssh obsidian-server "tail -50 ~/tools/uu_server.log"`

### 问题3: 价格为0或昨收为0

**原因**: 非交易时段或首次获取

**说明**: 这是正常现象，使用 `price` 字段即可

## 更新日志

### v1.0.0 (2026-01-30)
- ✅ 集成 uu_server Stock Query API
- ✅ 支持 A股/港股/美股实时行情
- ✅ 自动市值计算功能
- ✅ 在 company_info.json 中保存市场数据
- ✅ 在分析上下文中展示实时数据

---

**维护者**: Kai + Claude Code
**最后更新**: 2026-01-30
