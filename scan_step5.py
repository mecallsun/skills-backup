import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import json
import re
from collections import Counter

with open(r'C:\Users\Mecall\.skills-pool\scan_step3_dedup.json', 'r', encoding='utf-8') as f:
    step3 = json.load(f)
with open(r'C:\Users\Mecall\.skills-pool\scan_step4_metadata.json', 'r', encoding='utf-8') as f:
    step4 = json.load(f)

CLAUDE_SKILLS = r'C:\Users\Mecall\.claude\skills'
E_ROOT = r'E:\AI工作目录'

# ============================================================
# 标准 12 个分类目录
# ============================================================
STANDARD_CATEGORIES = {
    '01-AI工作流': 'AI代理/自动化/多智能体协作',
    '02-数据分析': '数据处理/Pandas/SQL/可视化',
    '03-A股量化': '股票/量化交易/金融建模',
    '04-VibeCoding': '前端/UI设计/移动端',
    '05-内容创作': '文案/内容营销/社交媒体',
    '06-职场效率': '会议/演示/OKR',
    '07-研究调研': '深度研究/竞品/市场调研',
    '08-基础设施': 'Docker/K8s/CI/CD',
    '09-开发框架': '后端/API/数据库',
    '10-知识库': '文档/RAG/知识图谱',
    '11-安全防护': '安全扫描/漏洞/合规',
    '12-Claude原厂': 'Claude 官方原生'
}

# 未分类 → 标准分类的映射规则（基于 description/domain 关键词）
CATEGORY_MAPPING = {
    # 00自媒体技能 → 05-内容创作
    '00自媒体技能': '05-内容创作',
    # AI研究项目 → 根据内容细分
    # 学习培训 → 根据内容细分
    # 蒸馏书籍 → 07-研究调研
    '蒸馏书籍': '07-研究调研',
}

# ============================================================
# 自动分类推断
# ============================================================
CATEGORY_KEYWORDS = {
    '01-AI工作流': ['agent', 'llm', 'claude', 'ai', '智能体', 'agentic', 'multi-agent', 'workflow'],
    '02-数据分析': ['data', 'analysis', 'sql', 'pandas', '可视化', 'dashboard', 'scraper', 'xlsx', 'excel'],
    '03-A股量化': ['股票', 'stock', 'a股', 'ashare', 'quant', '交易', 'trading', 'analyst'],
    '04-VibeCoding': ['vue', 'react', 'frontend', '前端', 'ui', 'design', 'css', 'tailwind', 'ppt', 'slides', 'video'],
    '05-内容创作': ['内容', 'content', 'writing', '写作', 'article', 'blog', 'seo', 'marketing', 'social', 'email'],
    '06-职场效率': ['meeting', '会议', 'okr', 'lead', '销售', 'resume', '团队', 'team'],
    '07-研究调研': ['研究', 'research', '调研', '决策', 'r2r', '市场', 'market', 'literature', 'analysis', 'analyst'],
    '08-基础设施': ['docker', 'kubernetes', 'k8s', 'ci/cd', 'deploy', 'terraform', 'aws', 'network', 'firewall', 'mcp-server', 'homelab', 'grafana', 'helm', 'github-actions', 'n8n'],
    '09-开发框架': ['backend', 'api', 'server', 'dotnet', 'csharp', 'python', 'java', 'kotlin', 'spring', 'django', 'fastapi', 'laravel', 'rust', 'go', 'golang', 'react', 'angular', 'vue', 'playwright', 'blazor', 'swift', 'akka', 'test', 'tdd', 'architecture', 'ddd', 'design-pattern', 'webapp', 'browser', 'container'],
    '10-知识库': ['pdf', 'docx', 'document', 'knowledge', 'rag', 'markdown', 'notion', 'codebase'],
    '11-安全防护': ['security', 'secure', 'auth', 'vulnerability', 'compliance', 'hipaa', 'phi', 'scan', 'guard'],
    '12-Claude原厂': ['claude', 'anthropic', 'mcp-builder', 'webapp-testing', 'canvas-design', 'brand-guidelines', 'doc-coauthoring', 'theme-factory', 'web-artifacts-builder', 'skill-creator', 'algorithmic-art', 'slack-gif-creator', 'internal-comms']
}

def infer_category(description, name, current_category):
    """推断 skill 的目标分类"""
    # 已有标准分类 → 保持
    if current_category in STANDARD_CATEGORIES:
        return current_category, 1.0
    # 00自媒体技能 → 05-内容创作
    if '自媒体' in current_category or '00自媒体' in current_category:
        return '05-内容创作', 0.9
    # 蒸馏书籍 → 07-研究调研
    if '蒸馏' in current_category or 'buffett' in current_category or 'charlie' in current_category:
        return '07-研究调研', 0.85
    # 评分推断
    text = (description or '').lower() + ' ' + (name or '').lower()
    scores = Counter()
    for cat, kws in CATEGORY_KEYWORDS.items():
        for kw in kws:
            if kw in text:
                scores[cat] += 1
    if not scores:
        return '09-开发框架', 0.3  # 兜底
    best_cat, best_score = scores.most_common(1)[0]
    confidence = min(best_score / 5, 1.0)  # 归一化
    return best_cat, confidence

# ============================================================
# 变体编号（同 name 不同 hash）
# ============================================================

# 按 (name, content_hash) 分组
by_name_hash = {}
for s in step3['deduped_skills']:
    name = s['name']
    h = s.get('_content_hash', '')
    key = (name, h)
    by_name_hash.setdefault(key, []).append(s)

# 按 name 分组（同 name 视为变体组）
by_name = {}
for s in step3['deduped_skills']:
    by_name.setdefault(s['name'], []).append(s)

# 生成变体编号映射
variant_id_map = {}  # (name, hash) -> variant_suffix
for name, variants in by_name.items():
    if len(variants) == 1:
        # 唯一版本，无后缀
        variant_id_map[(name, variants[0].get('_content_hash', ''))] = ''
    else:
        # 多版本：第一个保持原名，后续加 -01, -02...
        for i, v in enumerate(variants):
            if i == 0:
                variant_id_map[(name, v.get('_content_hash', ''))] = ''
            else:
                variant_id_map[(name, v.get('_content_hash', ''))] = f'-{i:02d}'

print(f"Total skills: {len(step3['deduped_skills'])}")
print(f"Unique (name, hash): {len(by_name_hash)}")
print(f"Variant groups (same name, different hash): {sum(1 for n, vs in by_name.items() if len(vs) > 1)}")
print()

# ============================================================
# 推断每个 skill 的目标分类
# ============================================================
reclassified = []
classification_changes = 0

for s in step4['skills']:
    name = s['name']
    desc = s.get('description', '')
    current_cat = s['category']

    target_cat, confidence = infer_category(desc, name, current_cat)

    # 添加变体编号
    h = ''
    for (n, hh), variants in by_name_hash.items():
        if n == name and s['skill_md_path'] in [v['skill_md_path'] for v in variants]:
            h = hh
            break
    variant_suffix = variant_id_map.get((name, h), '')

    # 重新生成分类后的 skill 对象
    new_skill = dict(s)
    new_skill['original_category'] = current_cat
    new_skill['target_category'] = target_cat
    new_skill['classification_confidence'] = round(confidence, 2)
    new_skill['variant_suffix'] = variant_suffix
    new_skill['final_name'] = name + variant_suffix
    new_skill['content_hash'] = h

    if current_cat != target_cat and not (current_cat in STANDARD_CATEGORIES):
        classification_changes += 1
    reclassified.append(new_skill)

# 统计归集变化
change_counter = Counter()
for s in reclassified:
    if s['original_category'] not in STANDARD_CATEGORIES:
        change_counter[s['target_category']] += 1

print(f"=== Reclassification Result ===")
print(f"Skills needing reclassification: {classification_changes}")
print()
print("=== Reclassification Target Distribution (from non-standard) ===")
for cat, cnt in change_counter.most_common():
    print(f"  → {cat}: {cnt}")

# 按 target_category 重新分组
final_by_cat = Counter()
for s in reclassified:
    final_by_cat[s['target_category']] += 1

print()
print("=== Final Category Distribution ===")
for cat, cnt in final_by_cat.most_common():
    print(f"  {cat}: {cnt}")

# 保存处理结果
result = {
    'scan_date': '2026-08-11',
    'total_skills': len(reclassified),
    'classification_changes': classification_changes,
    'standard_categories': list(STANDARD_CATEGORIES.keys()),
    'skills': reclassified,
    'variant_groups': [
        {
            'name': n,
            'variant_count': len(vs),
            'variants': [
                {
                    'content_hash': v.get('_content_hash', ''),
                    'final_name': v['name'] + variant_id_map.get((v['name'], v.get('_content_hash', '')), ''),
                    'full_path': v['full_path'],
                    'source': v['source']
                }
                for v in vs
            ]
        }
        for n, vs in by_name.items() if len(vs) > 1
    ]
}

out = r'C:\Users\Mecall\.skills-pool\scan_step5_reclassify.json'
with open(out, 'w', encoding='utf-8') as fp:
    json.dump(result, fp, ensure_ascii=False, indent=2)
print(f"\nSaved to: {out}")