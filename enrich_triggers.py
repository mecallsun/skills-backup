"""
完善所有 skill 的 triggers.keywords 和 tags
基于 description 和 name 自动推断 5-15 个关键词
"""
import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import json
import re
from collections import Counter

POOL_JSON = r'C:\Users\Mecall\.skills-pool\skills-pool.json'

with open(POOL_JSON, 'r', encoding='utf-8') as f:
    pool = json.load(f)

# ============================================================
# 关键词自动推断
# ============================================================
def infer_keywords(name, description, category, tags):
    """基于 name/description/tags 推断 5-15 个触发关键词"""
    keywords = set()

    # 1. 名称拆分（kebab-case → 词）
    name_parts = re.split(r'[-_]', name)
    for p in name_parts:
        if len(p) >= 3:
            keywords.add(p.lower())

    # 2. 从 description 提取关键词
    if description:
        # 提取中文词（2-6 字）
        cn_words = re.findall(r'[\u4e00-\u9fff]{2,6}', description)
        # 提取英文词（3+）
        en_words = re.findall(r'[a-zA-Z]{3,}', description)
        # 频次统计
        all_words = cn_words + en_words
        word_count = Counter(w.lower() for w in all_words if len(w) >= 2)
        # 取 top 10
        for word, cnt in word_count.most_common(15):
            if cnt >= 1 and len(word) >= 2:
                keywords.add(word.lower())

    # 3. 从 tags 补充
    tag_keywords = tags.get('domain', []) + tags.get('stage', []) + tags.get('output', [])
    for t in tag_keywords:
        keywords.add(t.lower())

    # 4. 从分类补充
    cat_keywords = re.findall(r'[\u4e00-\u9fff]{2,}', category)
    keywords.update(c.lower() for c in cat_keywords)

    # 5. 限制最多 15 个
    keyword_list = list(keywords)[:15]
    return keyword_list

# ============================================================
# 完善 triggers
# ============================================================
updated_count = 0
for s in pool['skills']:
    name = s.get('name', '')
    desc = s.get('description', '')
    cat = s.get('category', '')
    tags = s.get('tags', {})

    # 推断关键词
    new_keywords = infer_keywords(name, desc, cat, tags)

    if not s.get('triggers'):
        s['triggers'] = {'keywords': [], 'patterns': [], 'intents': []}

    # 保留原 keywords，添加推断的
    old_keywords = s['triggers'].get('keywords', [])
    if old_keywords and isinstance(old_keywords[0], dict):
        old_values = set()
        for ok in old_keywords:
            old_values.update(ok.get('value', []))
        # 合并
        merged = list(old_values | set(new_keywords))
        s['triggers']['keywords'] = [{'value': merged, 'weight': 1.0}]
    else:
        s['triggers']['keywords'] = [{'value': new_keywords, 'weight': 1.0}]

    updated_count += 1

# ============================================================
# 完善 tags（确保 6 维度全覆盖）
# ============================================================
for s in pool['skills']:
    tags = s.get('tags', {})
    if not tags:
        tags = {}
    # 补全缺失维度
    if 'domain' not in tags or not tags['domain']:
        tags['domain'] = ['通用']
    if 'language' not in tags or not tags['language']:
        tags['language'] = ['中文']
    if 'stage' not in tags or not tags['stage']:
        tags['stage'] = ['implement']
    if 'output' not in tags:
        tags['output'] = []
    if 'complexity' not in tags or not tags['complexity']:
        tags['complexity'] = ['medium']
    if 'ai_tier' not in tags or not tags['ai_tier']:
        # Tier 1/2 → required, Tier 3 → optional
        if s.get('tier') == 1:
            tags['ai_tier'] = ['required']
        elif s.get('tier') == 2:
            tags['ai_tier'] = ['recommended']
        else:
            tags['ai_tier'] = ['optional']
    s['tags'] = tags

# 保存
with open(POOL_JSON, 'w', encoding='utf-8') as f:
    json.dump(pool, f, ensure_ascii=False, indent=2)

print(f"✅ 完善 {updated_count} 个 skill 的 triggers 和 tags")
print(f"   - 平均 keyword 数：{sum(len(s['triggers']['keywords'][0]['value']) for s in pool['skills']) / len(pool['skills']):.1f}")

# 抽样展示
print("\n=== Sample (前 5 个 Tier-1) ===")
tier1 = [s for s in pool['skills'] if s['tier'] == 1]
for s in tier1[:5]:
    kw = s['triggers']['keywords'][0]['value'][:8]
    print(f"\n[{s['category']}] {s['name']}")
    print(f"  Keywords: {kw}")
    print(f"  Tags: {s['tags']}")