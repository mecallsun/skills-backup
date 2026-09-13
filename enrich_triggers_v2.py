"""
完善 triggers 和 tags（v2 - 更精确的推断）
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
# 严格关键词推断
# ============================================================
def infer_keywords(name, description, category):
    """基于 name/description/category 推断 5-12 个高质量关键词"""
    keywords = set()

    # 1. 名称拆分（kebab-case → 词）
    name_parts = re.split(r'[-_]', name)
    for p in name_parts:
        if len(p) >= 3:
            keywords.add(p.lower())

    # 2. 从 description 提取高频词
    if description:
        cn_words = re.findall(r'[\u4e00-\u9fff]{2,4}', description)
        en_words = re.findall(r'[a-zA-Z]{4,}', description)
        # 统计并过滤常见停用词
        stop_words = {'the', 'and', 'for', 'with', 'this', 'that', 'are', 'from', 'use', 'when',
                      'will', 'can', 'all', 'has', 'have', 'not', 'but', 'you', 'your', 'their',
                      '一个', '这个', '那个', '什么', '如何', '可以', '应该', '需要', '使用'}
        candidates = [w.lower() for w in cn_words + en_words
                     if len(w) >= 2 and w.lower() not in stop_words]
        word_count = Counter(candidates)
        for word, cnt in word_count.most_common(10):
            if cnt >= 1:
                keywords.add(word)

    # 3. 名称直接作为强关键词
    keywords.add(name.lower())

    # 4. 限制最多 12 个高质量
    keyword_list = sorted(keywords)[:12]
    return keyword_list

# ============================================================
# 严格 tags 推断（基于分类先验）
# ============================================================
CATEGORY_DOMAIN = {
    '01-AI工作流': ['AI'],
    '02-数据分析': ['后端', '数据库'],
    '03-A股量化': ['量化'],
    '04-VibeCoding': ['前端'],
    '05-内容创作': ['写作'],
    '06-职场效率': ['通用'],
    '07-研究调研': ['研究'],
    '08-基础设施': ['DevOps'],
    '09-开发框架': ['后端'],
    '10-知识库': ['PDF', '文档'],
    '11-安全防护': ['安全'],
    '12-Claude原厂': ['AI', 'Skill'],
}

# domain 推断规则（关键词 → domain）
DOMAIN_RULES = {
    '研究': ['研究', '调研', '决策', '分析报告', '市场调研', '对比分析', 'r2r', '深度研究'],
    '写作': ['写', '文章', '博客', '教程', '内容', '文案', 'seo', 'marketing'],
    'PPT': ['ppt', '幻灯片', '演示', 'slides', 'pptx'],
    'PDF': ['pdf', 'pdf文件'],
    'Excel': ['excel', 'xlsx', '表格', 'spreadsheet', 'dashboard'],
    '量化': ['股票', 'a股', 'ashare', 'stock', '交易', 'quant', '量化'],
    '前端': ['vue', 'react', 'angular', '前端', 'ui', 'css', 'tailwind', 'frontend'],
    '后端': ['backend', 'api', '后端', 'server', 'endpoint', 'fastapi', 'django', 'spring', 'laravel', 'dotnet', 'csharp', 'python'],
    'AI': ['agent', 'llm', 'claude', '智能体', 'ai', 'mcp', 'agentic'],
    'Skill': ['skill', '技能'],
    'DevOps': ['docker', 'kubernetes', 'k8s', '部署', 'ci/cd', 'deploy', 'terraform', 'github actions'],
    '安全': ['security', '安全', '扫描', '漏洞', 'vulnerability', 'auth', 'secure'],
    '测试': ['test', 'tdd', '测试', '验证', 'verify'],
    '数据库': ['database', 'db', 'sql', '数据库', 'mysql', 'postgres', 'redis', 'clickhouse'],
    '文档': ['document', 'docx', '文档'],
}

def infer_domain(description, category):
    """推断 domain（最多 3 个，按相关性）"""
    scores = Counter()
    text = (description or '').lower()
    for dom, kws in DOMAIN_RULES.items():
        for kw in kws:
            if kw in text:
                scores[dom] += 1
    # 加入分类先验
    cat_domain = CATEGORY_DOMAIN.get(category, [])
    for d in cat_domain:
        scores[d] += 0.5
    # top 3
    top = [d for d, _ in scores.most_common(3)]
    if not top:
        top = ['通用']
    return top

# ============================================================
# 应用
# ============================================================
for s in pool['skills']:
    name = s.get('name', '')
    desc = s.get('description', '')
    cat = s.get('category', '')

    # 1. 重置 keywords
    new_keywords = infer_keywords(name, desc, cat)
    s['triggers'] = {
        'keywords': [{'value': new_keywords, 'weight': 1.0}],
        'patterns': [],
        'intents': []
    }

    # 2. 重置 domain（精确）
    new_domain = infer_domain(desc, cat)

    # 3. 推断 stage
    text_lower = (desc or '').lower()
    if any(k in text_lower for k in ['研究', '调研', 'research', '探索']):
        new_stage = ['research']
    elif any(k in text_lower for k in ['设计', '架构', 'design', '架构']):
        new_stage = ['design']
    elif any(k in text_lower for k in ['测试', 'test', 'tdd']):
        new_stage = ['test']
    elif any(k in text_lower for k in ['部署', 'deploy', '发布']):
        new_stage = ['deploy']
    elif any(k in text_lower for k in ['debug', '调试', '修复']):
        new_stage = ['debug']
    else:
        new_stage = ['implement']

    # 4. 推断 output
    new_output = []
    if 'docx' in text_lower or 'word' in text_lower or '文档' in text_lower:
        new_output.append('docx')
    if 'xlsx' in text_lower or 'excel' in text_lower or '表格' in text_lower:
        new_output.append('xlsx')
    if 'pptx' in text_lower or 'ppt' in text_lower or '幻灯片' in text_lower:
        new_output.append('pptx')
    if 'pdf' in text_lower:
        new_output.append('pdf')
    if 'markdown' in text_lower or '.md' in text_lower:
        new_output.append('markdown')
    if 'code' in text_lower or '代码' in text_lower:
        new_output.append('code')
    if not new_output:
        new_output = []

    # 5. complexity
    if any(k in text_lower for k in ['架构', 'architecture', 'expert', '高级', '深度', 'deep']):
        new_complexity = ['expert']
    elif any(k in text_lower for k in ['复杂', 'complex', '完整', 'complete']):
        new_complexity = ['complex']
    elif any(k in text_lower for k in ['基础', 'basic', '简单', 'simple']):
        new_complexity = ['simple']
    else:
        new_complexity = ['medium']

    # 6. ai_tier
    if s.get('tier') == 1:
        new_ai_tier = ['required']
    elif s.get('tier') == 2:
        new_ai_tier = ['recommended']
    else:
        new_ai_tier = ['optional']

    s['tags'] = {
        'domain': new_domain,
        'language': ['中文', 'English'],
        'stage': new_stage,
        'output': new_output,
        'complexity': new_complexity,
        'ai_tier': new_ai_tier
    }

# 保存
with open(POOL_JSON, 'w', encoding='utf-8') as f:
    json.dump(pool, f, ensure_ascii=False, indent=2)

print(f"✅ 完善 {len(pool['skills'])} 个 skill 的精确 tags")

# 验证
tier1 = [s for s in pool['skills'] if s['tier'] == 1]
print("\n=== Sample Tier-1 (精确 tags) ===")
for s in tier1[:8]:
    print(f"\n[{s['category']}] {s['name']}")
    print(f"  Keywords: {s['triggers']['keywords'][0]['value'][:8]}")
    print(f"  Domain: {s['tags']['domain']}")
    print(f"  Stage: {s['tags']['stage']}")
    print(f"  Output: {s['tags']['output']}")