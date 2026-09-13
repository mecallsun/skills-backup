import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import json
import re
from datetime import datetime

# 加载 Step 3 结果
with open(r'C:\Users\Mecall\.skills-pool\scan_step3_dedup.json', 'r', encoding='utf-8') as f:
    step3 = json.load(f)

# ============================================================
# Frontmatter 解析（YAML 简易解析，处理单行 / 多行 description）
# ============================================================

def parse_frontmatter(content):
    """解析 SKILL.md 的 YAML frontmatter"""
    if not content.startswith('---'):
        return {}
    end = content.find('---', 3)
    if end == -1:
        return {}
    fm = content[3:end].strip()
    result = {}
    # 单行 key: value
    for line in fm.split('\n'):
        line = line.rstrip()
        if not line or line.startswith('#'):
            continue
        m = re.match(r'^([a-zA-Z_-]+):\s*(.*)$', line)
        if m:
            key = m.group(1)
            val = m.group(2).strip()
            # 去引号
            if (val.startswith('"') and val.endswith('"')) or (val.startswith("'") and val.endswith("'")):
                val = val[1:-1]
            result[key] = val
    return result

def read_md(path):
    """读取 SKILL.md，限制 5000 字符"""
    if not path or not os.path.exists(path):
        return ''
    try:
        with open(path, 'r', encoding='utf-8', errors='replace') as fp:
            return fp.read(5000)
    except Exception:
        return ''

# ============================================================
# 推断 tags（基于 description 关键词）
# ============================================================

DOMAIN_KEYWORDS = {
    '研究': ['研究', '调研', 'research', 'investigation'],
    '写作': ['写', 'article', 'writing', '内容', 'content', 'blog'],
    'PPT': ['ppt', 'slide', 'presentation', '幻灯片', '演示'],
    'PDF': ['pdf', 'document'],
    'Excel': ['excel', 'xlsx', 'spreadsheet'],
    '量化': ['股票', 'a股', 'ashare', 'stock', 'quant', '交易'],
    '前端': ['vue', 'react', 'frontend', '前端', 'ui', 'css', 'tailwind'],
    '后端': ['backend', 'api', 'server', '后端', 'endpoint'],
    'AI': ['agent', 'llm', 'claude', 'ai', '智能体'],
    'MCP': ['mcp'],
    'Skill': ['skill', '技能'],
    'DevOps': ['docker', 'kubernetes', 'k8s', 'ci/cd', 'deploy', 'terraform'],
    '安全': ['security', 'secure', 'auth', '安全', 'vulnerability'],
    '测试': ['test', 'tdd', 'verify', '验证'],
    '数据库': ['database', 'db', 'sql', 'postgres', 'mysql', 'redis'],
    'PPT': ['ppt', 'powerpoint'],
}

STAGE_KEYWORDS = {
    'research': ['研究', 'research', '调研'],
    'design': ['设计', 'design', '架构', 'architecture'],
    'implement': ['实现', 'implement', '开发', 'develop', 'build'],
    'test': ['test', '测试', 'tdd'],
    'debug': ['debug', '调试', 'fix'],
    'deploy': ['deploy', '部署', 'release', '发布'],
    'monitor': ['monitor', '监控'],
}

OUTPUT_KEYWORDS = {
    'docx': ['docx', 'word'],
    'xlsx': ['xlsx', 'excel'],
    'pptx': ['pptx', 'ppt', 'powerpoint'],
    'pdf': ['pdf'],
    'markdown': ['markdown', '.md'],
    'code': ['code', '代码', 'function', 'class'],
    'image': ['image', '图片'],
}

COMPLEXITY_HINTS = {
    'expert': ['架构', 'architecture', 'expert', '高级', '深度', 'deep'],
    'complex': ['complex', '复杂', '完整', 'complete'],
    'medium': ['standard', '基础', 'basic'],
    'simple': ['simple', '简单', 'hello'],
}

def infer_tags(text, name, category):
    """基于文本推断 tags"""
    text_lower = text.lower()
    tags = {
        'domain': [],
        'language': ['中文', 'English'],
        'stage': [],
        'output': [],
        'complexity': ['medium'],
        'ai_tier': ['optional']
    }
    # domain
    for dom, kws in DOMAIN_KEYWORDS.items():
        if any(kw in text_lower for kw in kws):
            tags['domain'].append(dom)
    # 兜底
    if not tags['domain']:
        tags['domain'] = ['通用']
    # stage
    for stg, kws in STAGE_KEYWORDS.items():
        if any(kw in text_lower for kw in kws):
            tags['stage'].append(stg)
    if not tags['stage']:
        tags['stage'] = ['implement']
    # output
    for out, kws in OUTPUT_KEYWORDS.items():
        if any(kw in text_lower for kw in kws):
            tags['output'].append(out)
    # complexity
    for cplx, kws in COMPLEXITY_HINTS.items():
        if any(kw in text_lower for kw in kws):
            tags['complexity'] = [cplx]
            break
    return tags

# ============================================================
# Priority 推断（1-10）
# ============================================================

# 高优先级分类（核心领域）
HIGH_PRIORITY_CATS = {
    '01-AI工作流': 8,
    '07-研究调研': 8,
    '12-Claude原厂': 9,
    '09-开发框架': 6,
    '08-基础设施': 6,
    '04-VibeCoding': 7,
}

# 高频 skill 关键词（boost）
HIGH_FREQ_KEYWORDS = {
    'r2r': 10, 'deep-research': 9, 'parallel-deep-research': 8,
    'ppt-master': 9, 'pptx': 8, 'pdf': 8, 'docx': 8, 'xlsx': 8,
    'mcp-builder': 9, 'mcp-server-patterns': 8,
    'skill-creator': 8, 'find-skills': 7,
    'council': 7, 'cowork': 7,
}

def calc_priority(name, category, description):
    """计算初始 priority 1-10"""
    # 关键词 boost
    if name in HIGH_FREQ_KEYWORDS:
        return HIGH_FREQ_KEYWORDS[name]
    # 分类基线
    base = HIGH_PRIORITY_CATS.get(category, 5)
    # 描述长度调整（详细描述通常更成熟）
    if description and len(description) > 100:
        base = min(base + 1, 10)
    return base

# ============================================================
# 主流程
# ============================================================

skills_meta = []
errors = []

for i, s in enumerate(step3['deduped_skills']):
    name = s['name']
    category = s['category']
    md_path = s['skill_md_path']
    full_path = s['full_path']

    content = read_md(md_path)
    fm = parse_frontmatter(content) if content else {}

    desc = fm.get('description', '')
    if not desc and content:
        # 取第一段非空文本
        body = content.split('---', 2)[-1] if content.startswith('---') else content
        first_para = next((p.strip() for p in body.split('\n\n') if p.strip() and not p.strip().startswith('#')), '')
        desc = first_para[:300]

    tags = infer_tags(content, name, category)
    priority = calc_priority(name, category, desc)

    meta = {
        'name': name,
        'category': category,
        'source': s['source'],
        'full_path': full_path,
        'skill_md_path': md_path,
        'has_skill_md': s.get('has_skill_md', False),
        'description': desc,
        'version': fm.get('version', '1.0.0'),
        'license': fm.get('license', ''),
        'tags': tags,
        'tier': 3,  # 默认 Tier-3（待使用统计驱动升级）
        'priority': priority,
        'auto_match': priority >= 7,  # 高优先级自动匹配
        'triggers': {
            'keywords': [{'value': [name.lower()], 'weight': 1.0}],
            'patterns': [],
            'intents': []
        },
        'duplicates_of': None,
        'deprecated': False,
        'usage_stats': {
            'total_calls': 0,
            'last_30_days': 0,
            'success_rate': 0.0
        }
    }
    skills_meta.append(meta)

print(f"=== Step 4: Metadata Extraction ===")
print(f"Total skills: {len(skills_meta)}")
print(f"Errors: {len(errors)}")
print()

# 分类统计
from collections import Counter
cat_count = Counter(s['category'] for s in skills_meta)
print("=== By Category ===")
for cat, cnt in cat_count.most_common():
    print(f"  {cat}: {cnt}")
print()

# Tier / priority 分布
tier_count = Counter(s['tier'] for s in skills_meta)
print(f"Tier distribution: {dict(tier_count)}")
prio_count = Counter(s['priority'] for s in skills_meta)
print(f"Priority distribution: {dict(sorted(prio_count.items()))}")
print()

# 输出
out_path = r'C:\Users\Mecall\.skills-pool\scan_step4_metadata.json'
with open(out_path, 'w', encoding='utf-8') as fp:
    json.dump({
        'scan_date': '2026-08-11',
        'total_skills': len(skills_meta),
        'errors': errors,
        'skills': skills_meta
    }, fp, ensure_ascii=False, indent=2)

print(f"Result saved to: {out_path}")