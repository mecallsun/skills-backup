"""
修复被错分类的关键 skill
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

# 修正映射
FIX_CATEGORY = [
    ('meeting-notes', '06-职场效率'),    # 会议记录 → 职场效率
]

promoted = []
for name, correct_cat in FIX_CATEGORY:
    for s in pool['skills']:
        if s['name'] == name and s['category'] != correct_cat:
            old_cat = s['category']
            s['category'] = correct_cat
            s['original_category'] = old_cat
            s['physical_path'] = s['physical_path'].replace(f'\\{old_cat}\\{name}', f'\\{correct_cat}\\{name}')
            s['target_path'] = s['target_path'].replace(f'/{old_cat}/{name}', f'/{correct_cat}/{name}')
            promoted.append(f"{name}: {old_cat} → {correct_cat}")
            break

# 保存
with open(POOL_JSON, 'w', encoding='utf-8') as f:
    json.dump(pool, f, ensure_ascii=False, indent=2)

print("=== 分类修复 ===")
for p in promoted:
    print(f"  ✅ {p}")

print("\n注意：物理目录需要后续手动移动，本步骤仅修复 JSON")