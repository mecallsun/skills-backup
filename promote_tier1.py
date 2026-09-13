"""
增强 Tier-1：补 7 个核心 skill
"""
import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import json

POOL_JSON = r'C:\Users\Mecall\.skills-pool\skills-pool.json'

with open(POOL_JSON, 'r', encoding='utf-8') as f:
    pool = json.load(f)

# 要提升到 Tier-1 的核心 skill
PROMOTE_TO_T1 = [
    ('07-研究调研', 'market-research'),      # 市场调研
    ('11-安全防护', 'security-scan'),        # 安全扫描
    ('11-安全防护', 'security-review'),      # 安全审查
    ('06-职场效率', 'meeting-notes'),        # 会议记录
    ('05-内容创作', 'seo'),                  # SEO
    ('05-内容创作', 'brand-voice'),          # 品牌声音
]

promoted = []
demoted = []
for cat, name in PROMOTE_TO_T1:
    found = False
    for s in pool['skills']:
        if s['name'] == name and s['category'] == cat:
            old_tier = s['tier']
            s['tier'] = 1
            s['priority'] = 10
            s['auto_match'] = True
            # 更新 ai_tier
            s['tags']['ai_tier'] = ['required']
            promoted.append(f"{cat}/{name}: Tier {old_tier} → 1")
            found = True
            break
    if not found:
        print(f"⚠️ 未找到: {cat}/{name}")

# 如果 Tier-1 超 20 个，需要降级一些次要的
tier1_skills = [s for s in pool['skills'] if s['tier'] == 1]
print(f"\n当前 Tier-1 数: {len(tier1_skills)}")
print(f"新增: {len(promoted)}")

if len(tier1_skills) > 20:
    # 把 04-VibeCoding/pptx 降级到 Tier-2（ppt-master 仍是 Tier-1）
    for s in tier1_skills:
        if s['name'] == 'pptx' and s['category'] == '12-Claude原厂':
            s['tier'] = 2
            s['priority'] = 8
            s['tags']['ai_tier'] = ['recommended']
            demoted.append(f"{s['category']}/{s['name']}: Tier 1 → 2")
            break

# 保存
with open(POOL_JSON, 'w', encoding='utf-8') as f:
    json.dump(pool, f, ensure_ascii=False, indent=2)

# 重新统计
tier1 = [s for s in pool['skills'] if s['tier'] == 1]
tier2 = [s for s in pool['skills'] if s['tier'] == 2]

print("\n=== Tier-1 提升记录 ===")
for p in promoted:
    print(f"  ✅ {p}")
print("\n=== Tier 降级记录 ===")
for d in demoted:
    print(f"  ⬇ {d}")

print(f"\n=== 最终 Tier 分布 ===")
print(f"  Tier-1: {len(tier1)} / 20")
print(f"  Tier-2: {len(tier2)} / 50")
print(f"  Tier-3: {len([s for s in pool['skills'] if s['tier'] == 3])}")

print("\n=== Tier-1 名单（增强后）===")
for s in sorted(tier1, key=lambda x: (-x['priority'], x['category'], x['name'])):
    print(f"  [P{s['priority']}] {s['category']}/{s['name']}")