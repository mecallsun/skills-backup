import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import json
from datetime import datetime
from collections import Counter

# 加载 Step 5 归集结果
with open(r'C:\Users\Mecall\.skills-pool\scan_step5_reclassify.json', 'r', encoding='utf-8') as f:
    step5 = json.load(f)

# ============================================================
# Tier 划分规则
# ============================================================
# Tier-1: priority >= 9（高频核心，启动自动加载）
# Tier-2: priority >= 7 且 priority < 9（中频，关键词触发）
# Tier-3: priority < 7（低频，按需检索）

def assign_tier(priority, name):
    if priority >= 9:
        return 1
    elif priority >= 7:
        return 2
    else:
        return 3

# ============================================================
# 构建最终 skills-pool.json
# ============================================================

POOL_ROOT = r'C:\Users\Mecall\.skills-pool\skills'

skills_pool = []
for s in step5['skills']:
    tier = assign_tier(s['priority'], s['name'])
    final_name = s['final_name']
    target_cat = s['target_category']

    skill_entry = {
        'name': final_name,
        'original_name': s['name'],
        'variant_suffix': s['variant_suffix'],
        'category': target_cat,
        'original_category': s['original_category'],
        'classification_confidence': s['classification_confidence'],
        'tier': tier,
        'priority': s['priority'],
        'auto_match': s['priority'] >= 7,
        'description': s.get('description', ''),
        'version': s.get('version', '1.0.0'),
        'license': s.get('license', ''),
        'tags': s.get('tags', {}),
        'triggers': s.get('triggers', {'keywords': [], 'patterns': [], 'intents': []}),
        'source_path': s.get('full_path', ''),
        'skill_md_path': s.get('skill_md_path', ''),
        'source': s.get('source', ''),
        'content_hash': s.get('content_hash', ''),
        'has_skill_md': s.get('has_skill_md', False),
        'duplicates_of': None,
        'deprecated': False,
        'deprecation_reason': None,
        'usage_stats': {
            'total_calls': 0,
            'last_30_days': 0,
            'last_7_days': 0,
            'success_rate': 0.0,
            'avg_tokens': 0,
            'last_called': None
        },
        'shared_with': ['claude', 'codex', 'cline', 'cursor'],
        'install_date': '2026-08-11',
        'last_updated': '2026-08-11',
        'dependencies': [],
        'requires_mcp': [],
        'requires_env': [],
        'physical_path': f"{POOL_ROOT}\\{target_cat}\\{final_name}",
        'target_path': f"~/.claude/skills/{target_cat}/{final_name}"
    }
    skills_pool.append(skill_entry)

# 按 category 和 priority 排序
skills_pool.sort(key=lambda x: (x['category'], -x['priority'], x['name']))

# ============================================================
# 构建顶层结构
# ============================================================

# 分类摘要
category_summary = []
seen_cats = set()
for s in skills_pool:
    if s['category'] in seen_cats:
        continue
    seen_cats.add(s['category'])
    cat_skills = [x for x in skills_pool if x['category'] == s['category']]
    tier1_count = sum(1 for x in cat_skills if x['tier'] == 1)
    tier2_count = sum(1 for x in cat_skills if x['tier'] == 2)
    tier3_count = sum(1 for x in cat_skills if x['tier'] == 3)
    category_summary.append({
        'code': s['category'].split('-')[0],
        'name': s['category'],
        'skill_count': len(cat_skills),
        'tier1': tier1_count,
        'tier2': tier2_count,
        'tier3': tier3_count,
        'tier1_skills': [x['name'] for x in cat_skills if x['tier'] == 1]
    })

# 路由配置
router_config = {
    'tier1_max_count': 20,
    'tier2_max_count': 50,
    'auto_match_threshold': 0.7,
    'ambiguous_threshold': 0.4,
    'ambiguous_strategy': 'ask_user',
    'fallback_strategy': 'highest_priority',
    'tier1_auto_load_on_startup': True,
    'context_budget_chars': 30000,
    'tag_weights': {
        'domain_match': 0.30,
        'stage_match': 0.25,
        'output_match': 0.20,
        'language_match': 0.10,
        'complexity_match': 0.10,
        'ai_tier_bonus': 0.05
    }
}

# 统计
tier_counter = Counter(s['tier'] for s in skills_pool)
priority_counter = Counter(s['priority'] for s in skills_pool)

# 链接目标
link_targets = {
    'claude': {
        'link_path': 'C:/Users/Mecall/.claude/skills',
        'type': 'symbolic_link',
        'status': 'pending'
    },
    'codex': {
        'link_path': 'C:/Users/Mecall/.codex/skills',
        'type': 'symbolic_link',
        'status': 'pending'
    },
    'cline': {'link_path': 'TBD', 'type': 'pending'},
    'cursor': {'link_path': 'TBD', 'type': 'pending'}
}

# ============================================================
# 最终输出
# ============================================================

pool = {
    'version': '1.0.0',
    'schema_version': '1.0',
    'last_updated': '2026-08-11T22:30:00+08:00',
    'next_review': '2026-09-11',

    'source_dir': 'C:/Users/Mecall/.skills-pool/skills',
    'absolute_path': 'C:\\Users\\Mecall\\.skills-pool\\skills',

    'link_targets': link_targets,

    'categories': category_summary,

    'skills': skills_pool,

    'variant_groups': step5['variant_groups'],

    'router_config': router_config,

    'stats': {
        'total_skills': len(skills_pool),
        'tier1_count': tier_counter[1],
        'tier2_count': tier_counter[2],
        'tier3_count': tier_counter[3],
        'categories': len(category_summary),
        'duplicates_removed': 282,
        'variant_groups': len(step5['variant_groups']),
        'deprecated_count': 0,
        'reclassified_count': 104
    },

    'rules': {
        'classification_auto': True,
        'classification_confidence_threshold': 0.7,
        'variant_suffix_policy': '-N',
        'preserve_all_variants': True,
        'auto_collect_unclassified': True
    },

    'metadata': {
        'owner': 'Mecall',
        'single_user': True,
        'style_consistency': 'personal_habit',
        'created_by': 'Global Skill Pool v3.0 SOP',
        'backup_enabled': True,
        'backup_path': 'F:\\AI生成工具\\AI技能备份\\skills-pool'
    }
}

out_path = r'C:\Users\Mecall\.skills-pool\skills-pool.json'
with open(out_path, 'w', encoding='utf-8') as fp:
    json.dump(pool, fp, ensure_ascii=False, indent=2)

# ============================================================
# 输出统计
# ============================================================
print("=" * 80)
print(f"✅ Final skills-pool.json generated")
print("=" * 80)
print(f"Total skills: {pool['stats']['total_skills']}")
print(f"Tier-1: {pool['stats']['tier1_count']}")
print(f"Tier-2: {pool['stats']['tier2_count']}")
print(f"Tier-3: {pool['stats']['tier3_count']}")
print(f"Categories: {pool['stats']['categories']}")
print(f"Variant groups: {pool['stats']['variant_groups']}")
print(f"Reclassified: {pool['stats']['reclassified_count']}")
print(f"Duplicates removed: {pool['stats']['duplicates_removed']}")
print()
print("=== Tier-1 Skills (Auto-load on startup) ===")
tier1_skills = [s for s in skills_pool if s['tier'] == 1]
for s in tier1_skills:
    print(f"  [P{s['priority']}] {s['category']}/{s['name']}")
print()
print(f"Saved to: {out_path}")
print(f"File size: {os.path.getsize(out_path) / 1024:.1f} KB")