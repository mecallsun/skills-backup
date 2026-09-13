"""
同步 skills-pool.json 的 stats 字段（与实际 skills 一致）
"""
import json

POOL_JSON = r'C:\Users\Mecall\.skills-pool\skills-pool.json'

with open(POOL_JSON, 'r', encoding='utf-8') as f:
    pool = json.load(f)

# 重新统计
from collections import Counter
tier_counter = Counter(s['tier'] for s in pool['skills'])
cat_counter = Counter(s['category'] for s in pool['skills'])

# 更新 stats
pool['stats']['tier1_count'] = tier_counter[1]
pool['stats']['tier2_count'] = tier_counter[2]
pool['stats']['tier3_count'] = tier_counter[3]
pool['stats']['categories'] = len(cat_counter)

# 更新 categories 摘要
cat_summary = []
seen = set()
for s in sorted(pool['skills'], key=lambda x: (x['category'], -x['priority'])):
    if s['category'] in seen:
        continue
    seen.add(s['category'])
    cat_skills = [x for x in pool['skills'] if x['category'] == s['category']]
    cat_summary.append({
        'code': s['category'].split('-')[0],
        'name': s['category'],
        'skill_count': len(cat_skills),
        'tier1': sum(1 for x in cat_skills if x['tier'] == 1),
        'tier2': sum(1 for x in cat_skills if x['tier'] == 2),
        'tier3': sum(1 for x in cat_skills if x['tier'] == 3),
        'tier1_skills': [x['name'] for x in cat_skills if x['tier'] == 1]
    })
pool['categories'] = cat_summary

# 更新版本号
pool['version'] = '1.1.0'
pool['last_updated'] = '2026-08-11T23:20:00+08:00'

# 更新 metadata
pool['metadata']['optimizations'] = [
    'router.py 自动匹配引擎实装',
    'health-check.py 健康检查脚本',
    'usage-stats.json 使用统计',
    'Tier-1 名单增强（18 → 20）',
    'tags 精确化（6 维度全覆盖）',
    'triggers.keywords 自动推断（平均 14 个）'
]

# 保存
with open(POOL_JSON, 'w', encoding='utf-8') as f:
    json.dump(pool, f, ensure_ascii=False, indent=2)

print("[OK] skills-pool.json synced to v1.1")
print("   Tier-1:", pool['stats']['tier1_count'])
print("   Tier-2:", pool['stats']['tier2_count'])
print("   Tier-3:", pool['stats']['tier3_count'])
print("   Categories:", pool['stats']['categories'])
print("   Total:", pool['stats']['total_skills'])