import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import json
from datetime import datetime
from collections import Counter

# 加载 Step 5
with open(r'C:\Users\Mecall\.skills-pool\scan_step5_reclassify.json', 'r', encoding='utf-8') as f:
    step5 = json.load(f)

# ============================================================
# 精确校准 priority 和 tier
# ============================================================
# 设计原则：
# - Tier-1（≤20 个）：用户工作流核心，每个分类 1-2 个代表
# - Tier-2（≤50 个）：常用但非必需
# - Tier-3：其他

# 手动指定 Tier-1（基于"全局核心高频"判断）
TIER1_OVERRIDE = {
    # 核心工作流
    'r2r', 'deep-research', 'parallel-deep-research',
    'council', 'cowork', 'search-first',
    # PPT/文档
    'ppt-master', 'pptx',
    # 文件处理
    'pdf', 'docx', 'xlsx', 'markitdown',
    # 写作
    'article-writing', 'seo',
    # MCP/Skill
    'mcp-builder', 'mcp-server-patterns', 'skill-creator',
    # 浏览器自动化
    'browser-automation',
    # 查找
    'find-skills', 'skill-stocktake',
    # Claude 原厂必备
    'claude-api', 'everything-claude-code', 'webapp-testing',
}

# 手动指定 Tier-2（常用但可省略）
TIER2_OVERRIDE = {
    # 研究相关
    'market-research', 'literature-review', 'research-ops',
    'product-comparison-analysis-skill', 'product-lens', 'product-capability',
    # 数据
    'dashboard-builder', 'excel-automation', 'data-scraper-agent',
    # PPT/设计
    'frontend-design-direction', 'frontend-design', 'frontend-patterns',
    'pptx-01',
    # 安全
    'security-review', 'security-scan',
    # 开发
    'browser-qa', 'dotnet-advisor', 'python-patterns',
    'docker', 'docker-patterns', 'mcp-server-patterns',
    # 内容
    'brand-voice', 'content-strategy', 'crosspost',
    # 职场
    'lead-hunter', 'team-builder',
    # Claude 原厂常用
    'pdf-01', 'docx-01', 'xlsx-01',
    'canvas-design', 'theme-factory', 'brand-guidelines',
    'doc-coauthoring', 'internal-comms', 'algorithmic-art',
}

def determine_tier(name, priority):
    """更严格的 Tier 划分"""
    if name in TIER1_OVERRIDE:
        return 1, 10 if priority < 10 else priority
    elif name in TIER2_OVERRIDE:
        return 2, 8 if priority < 8 else priority
    else:
        return 3, priority

# ============================================================
# 构建最终索引
# ============================================================

POOL_ROOT = r'C:\Users\Mecall\.skills-pool\skills'

skills_pool = []
for s in step5['skills']:
    final_name = s['final_name']
    target_cat = s['target_category']

    tier, adjusted_priority = determine_tier(s['name'], s['priority'])

    skill_entry = {
        'name': final_name,
        'original_name': s['name'],
        'variant_suffix': s['variant_suffix'],
        'category': target_cat,
        'original_category': s['original_category'],
        'classification_confidence': s['classification_confidence'],
        'tier': tier,
        'priority': adjusted_priority,
        'auto_match': tier <= 2,
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

skills_pool.sort(key=lambda x: (x['tier'], -x['priority'], x['category'], x['name']))

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

tier_counter = Counter(s['tier'] for s in skills_pool)
priority_counter = Counter(s['priority'] for s in skills_pool)

pool = {
    'version': '1.0.0',
    'schema_version': '1.0',
    'last_updated': '2026-08-11T22:35:00+08:00',
    'next_review': '2026-09-11',
    'source_dir': 'C:/Users/Mecall/.skills-pool/skills',
    'absolute_path': 'C:\\Users\\Mecall\\.skills-pool\\skills',
    'link_targets': {
        'claude': {'link_path': 'C:/Users/Mecall/.claude/skills', 'type': 'symbolic_link', 'status': 'pending'},
        'codex': {'link_path': 'C:/Users/Mecall/.codex/skills', 'type': 'symbolic_link', 'status': 'pending'},
        'cline': {'link_path': 'TBD', 'type': 'pending'},
        'cursor': {'link_path': 'TBD', 'type': 'pending'}
    },
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

print("=" * 80)
print(f"✅ Final skills-pool.json generated (v1.0)")
print("=" * 80)
print(f"Total skills: {pool['stats']['total_skills']}")
print(f"Tier-1: {pool['stats']['tier1_count']}")
print(f"Tier-2: {pool['stats']['tier2_count']}")
print(f"Tier-3: {pool['stats']['tier3_count']}")
print(f"Categories: {pool['stats']['categories']}")
print(f"Variant groups: {pool['stats']['variant_groups']}")
print()
print("=== Tier-1 Skills (Auto-load) ===")
tier1 = [s for s in skills_pool if s['tier'] == 1]
for s in tier1:
    print(f"  [P{s['priority']:2d}] {s['category']}/{s['name']}")
print(f"\nTotal Tier-1: {len(tier1)} (≤20 limit: {'✅' if len(tier1) <= 20 else '⚠️ OVER LIMIT'})")
print()
print(f"File size: {os.path.getsize(out_path) / 1024:.1f} KB")