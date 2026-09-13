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
# 优先级：每个 (final_name) 只保留一份（去重 step5 中重复条目）
# ============================================================

seen = set()
unique_skills = []
for s in step5['skills']:
    key = s['final_name']
    if key in seen:
        continue
    seen.add(key)
    unique_skills.append(s)

print(f"Step 5 input: {len(step5['skills'])}")
print(f"After dedup by final_name: {len(unique_skills)}")
print()

# ============================================================
# Tier 划分（更严格）
# ============================================================

# 严格 Tier-1：20 个核心，按"工作流关键节点 + 文档处理"
TIER1_LIST = [
    # 工作流核心（5）
    'r2r', 'deep-research', 'council', 'search-first', 'parallel-deep-research',
    # 文档处理（5）
    'ppt-master', 'pdf', 'docx', 'xlsx', 'markitdown',
    # 写作/MCP（3）
    'article-writing', 'mcp-builder', 'skill-creator',
    # 文件/浏览器自动化（3）
    'browser-automation', 'find-skills', 'pptx',
    # Claude 原厂（4）
    'claude-api', 'webapp-testing', 'mcp-server-patterns', 'everything-claude-code'
]

# Tier-2（50 个以内）：研究 + 数据 + 通用
TIER2_LIST = [
    # 研究调研
    'market-research', 'literature-review', 'research-ops',
    'product-comparison-analysis-skill', 'product-lens', 'product-capability',
    'prd-development', 'architecture-decision-records', 'hexagonal-architecture',
    'investor-materials', 'investor-outreach', 'company-financial-analysis',
    # 数据分析
    'dashboard-builder', 'excel-automation', 'data-scraper-agent',
    'exa-search', 'clickhouse-io', 'redis-patterns',
    # 文档/设计
    'frontend-design', 'frontend-design-direction', 'frontend-patterns',
    'pptx-01', 'pptx-02', 'pptx-03',
    'docx-01', 'pdf-01', 'xlsx-01',
    # 安全
    'security-review', 'security-scan', 'skill-scout',
    # 开发
    'docker', 'docker-patterns', 'dotnet-advisor', 'python-patterns',
    'csharp-coding-standards', 'httpclient-factory', 'e2e-testing',
    # 内容
    'brand-voice', 'content-strategy', 'crosspost', 'content-engine',
    # 职场
    'lead-hunter', 'team-builder', 'meeting-notes',
    # Claude 原厂常用
    'canvas-design', 'theme-factory', 'brand-guidelines',
    'doc-coauthoring', 'internal-comms', 'algorithmic-art',
    'template', 'plankton-code-quality',
    # cowork（AI 工作流）
    'cowork',
]

def determine_tier(final_name, original_name):
    """根据 final_name 和 original_name 判定"""
    # 变体（带 -N 后缀）一律降到 Tier-3（避免与主版本竞争）
    if '-' in final_name and final_name.split('-')[-1].isdigit() and len(final_name.split('-')[-1]) == 2:
        return 3
    # 主版本判定
    if original_name in TIER1_LIST or final_name in TIER1_LIST:
        return 1
    if original_name in TIER2_LIST or final_name in TIER2_LIST:
        return 2
    return 3

# ============================================================
# 构建最终索引
# ============================================================

POOL_ROOT = r'C:\Users\Mecall\.skills-pool\skills'

skills_pool = []
for s in unique_skills:
    final_name = s['final_name']
    target_cat = s['target_category']
    tier = determine_tier(final_name, s['name'])

    # priority：tier 1 = 10, tier 2 = 7, tier 3 = 5
    priority = 10 if tier == 1 else (7 if tier == 2 else 5)

    skill_entry = {
        'name': final_name,
        'original_name': s['name'],
        'variant_suffix': s['variant_suffix'],
        'category': target_cat,
        'original_category': s['original_category'],
        'classification_confidence': s['classification_confidence'],
        'tier': tier,
        'priority': priority,
        'auto_match': tier <= 2,
        'description': s.get('description', '')[:300],
        'version': s.get('version', '1.0.0'),
        'license': s.get('license', ''),
        'tags': s.get('tags', {}),
        'triggers': {
            'keywords': [{'value': [s['name']], 'weight': 1.0}],
            'patterns': [],
            'intents': []
        },
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

# 排序：tier asc → priority desc → category → name
skills_pool.sort(key=lambda x: (x['tier'], -x['priority'], x['category'], x['name']))

# 分类摘要
category_summary = []
seen_cats = set()
for s in skills_pool:
    if s['category'] in seen_cats:
        continue
    seen_cats.add(s['category'])
    cat_skills = [x for x in skills_pool if x['category'] == s['category']]
    category_summary.append({
        'code': s['category'].split('-')[0],
        'name': s['category'],
        'skill_count': len(cat_skills),
        'tier1': sum(1 for x in cat_skills if x['tier'] == 1),
        'tier2': sum(1 for x in cat_skills if x['tier'] == 2),
        'tier3': sum(1 for x in cat_skills if x['tier'] == 3),
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

pool = {
    'version': '1.0.0',
    'schema_version': '1.0',
    'last_updated': '2026-08-11T22:40:00+08:00',
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
print("=== Tier-1 Skills (Auto-load on startup) ===")
tier1 = [s for s in skills_pool if s['tier'] == 1]
for s in tier1:
    print(f"  [P{s['priority']:2d}] {s['category']}/{s['name']}")
print(f"\nTotal Tier-1: {len(tier1)} (≤20 limit: {'✅' if len(tier1) <= 20 else '⚠️ OVER'})")
print()
print("=== Tier-2 Skills (Keyword-triggered) ===")
tier2 = [s for s in skills_pool if s['tier'] == 2]
print(f"Total Tier-2: {len(tier2)} (≤50 limit: {'✅' if len(tier2) <= 50 else '⚠️ OVER'})")
for s in tier2[:15]:
    print(f"  [P{s['priority']}] {s['category']}/{s['name']}")
if len(tier2) > 15:
    print(f"  ... and {len(tier2)-15} more")
print()
print(f"File: {out_path}")
print(f"Size: {os.path.getsize(out_path) / 1024:.1f} KB")