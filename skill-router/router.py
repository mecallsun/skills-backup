"""
Skill 自动匹配引擎 v1.0
按 priority + tags + keywords + usage 加权打分
"""
import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'

import json
import re
from typing import List, Dict, Tuple, Optional
from datetime import datetime

POOL_JSON = r'C:\Users\Mecall\.skills-pool\skills-pool.json'
USAGE_STATS = r'C:\Users\Mecall\.skills-pool\usage-stats.json'

# ============================================================
# 意图提取
# ============================================================
def extract_intent(user_input: str) -> Dict:
    """提取用户意图"""
    intent = {
        'raw': user_input,
        'keywords': [],
        'domain_hint': None,
        'stage_hint': None,
        'output_hint': None,
        'complexity_hint': None,
        'language': '中文'
    }
    text_lower = user_input.lower()
    # 关键词（jieba-like 简化版）
    keywords = re.findall(r'[\w\u4e00-\u9fff]{2,}', user_input)
    intent['keywords'] = keywords

    # domain 推断
    domain_kws = {
        '研究': ['研究', '调研', '决策', '分析报告', 'r2r', '深度研究', '对比'],
        '写作': ['写', '文章', 'blog', '写作', '教程'],
        'PPT': ['ppt', '幻灯片', '演示', 'slides', 'presentation'],
        'PDF': ['pdf', 'pdf文件'],
        'Excel': ['excel', 'xlsx', '表格', 'spreadsheet'],
        '量化': ['股票', 'a股', 'ashare', 'stock', '交易', '量化'],
        '前端': ['vue', 'react', '前端', 'ui', '界面'],
        '后端': ['backend', 'api', '后端', '服务器'],
        'AI': ['agent', 'llm', 'claude', '智能体', 'ai'],
        'MCP': ['mcp', 'mcp server'],
        'Skill': ['skill', '技能'],
        'DevOps': ['docker', 'k8s', '部署', 'ci', 'cd'],
        '安全': ['security', '安全', '扫描', '漏洞', 'vulnerability'],
        '测试': ['test', 'tdd', '测试', '验证'],
        '数据库': ['database', 'db', 'sql', '数据库', 'mysql', 'postgres'],
    }
    for dom, kws in domain_kws.items():
        if any(kw in text_lower for kw in kws):
            intent['domain_hint'] = dom
            break

    # stage 推断
    stage_kws = {
        'research': ['研究', '调研', '探索'],
        'design': ['设计', '架构', '规划'],
        'implement': ['做', '创建', '开发', '实现', '写'],
        'test': ['测试', 'tdd', '验证'],
        'debug': ['调试', 'fix', 'debug', '修复'],
        'deploy': ['部署', '发布', 'deploy'],
    }
    for stg, kws in stage_kws.items():
        if any(kw in text_lower for kw in kws):
            intent['stage_hint'] = stg
            break

    # output 推断
    if '.docx' in user_input or 'word' in text_lower or '文档' in user_input:
        intent['output_hint'] = 'docx'
    elif '.xlsx' in user_input or 'excel' in text_lower or '表格' in user_input:
        intent['output_hint'] = 'xlsx'
    elif '.pptx' in user_input or 'ppt' in text_lower or '幻灯片' in user_input:
        intent['output_hint'] = 'pptx'
    elif '.pdf' in user_input or 'pdf' in text_lower:
        intent['output_hint'] = 'pdf'
    elif 'markdown' in text_lower or '.md' in user_input:
        intent['output_hint'] = 'markdown'

    return intent

# ============================================================
# 评分
# ============================================================
def score_skill(skill: Dict, intent: Dict) -> Tuple[float, Dict]:
    """打分（0-1）"""
    score = 0.0
    details = {}

    # 1. 关键词命中（最高 0.40）
    triggers = skill.get('triggers', {})
    trigger_keywords = triggers.get('keywords', [])
    matched_keywords = []
    if trigger_keywords:
        # trigger_keywords 是 [{'value': [...], 'weight': float}, ...]
        for tk in trigger_keywords:
            values = tk.get('value', [])
            weight = tk.get('weight', 1.0)
            for kw in intent['keywords']:
                if kw.lower() in [v.lower() for v in values]:
                    matched_keywords.append(kw)
        if matched_keywords:
            kw_score = min(len(matched_keywords) * 0.20, 0.50)
            score += kw_score
            details['keywords_matched'] = matched_keywords[:5]

    # 2. 模式匹配（最高 0.20）
    patterns = triggers.get('patterns', [])
    pattern_matched = False
    for p in patterns:
        if re.search(p.get('regex', ''), intent['raw']):
            score += 0.20 * p.get('weight', 1.0)
            pattern_matched = True
            break
    if pattern_matched:
        details['pattern_matched'] = True

    # 3. domain 标签（0.30）
    skill_tags = skill.get('tags', {})
    skill_domain = skill_tags.get('domain', [])
    if intent['domain_hint'] and intent['domain_hint'] in skill_domain:
        score += 0.30
        details['domain_match'] = intent['domain_hint']

    # 4. stage 标签（0.25）
    skill_stage = skill_tags.get('stage', [])
    if intent['stage_hint'] and intent['stage_hint'] in skill_stage:
        score += 0.25
        details['stage_match'] = intent['stage_hint']

    # 5. output 标签（0.20）
    skill_output = skill_tags.get('output', [])
    if intent['output_hint'] and intent['output_hint'] in skill_output:
        score += 0.20
        details['output_match'] = intent['output_hint']

    # 6. priority 加成（最高 0.10）
    score += (skill.get('priority', 5) / 10) * 0.10

    # 7. 使用统计加成（最高 0.10）
    usage = skill.get('usage_stats', {})
    if usage.get('success_rate', 0) > 0.8:
        score += 0.05
    if usage.get('last_30_days', 0) >= 10:
        score += 0.05

    # 8. Tier-1 加成
    if skill.get('tier') == 1:
        score += 0.05

    # 9. 名字直接命中（高优先级加成）
    name_lower = skill.get('name', '').lower()
    for kw in intent['keywords']:
        if kw.lower() == name_lower or kw.lower() in name_lower:
            score += 0.15
            details['name_match'] = kw
            break

    return min(score, 1.0), details

# ============================================================
# 自动匹配主流程
# ============================================================
class SkillRouter:
    def __init__(self, pool_path: str = POOL_JSON):
        with open(pool_path, 'r', encoding='utf-8') as f:
            self.pool = json.load(f)
        self.config = self.pool.get('router_config', {})
        self.threshold = self.config.get('auto_match_threshold', 0.7)
        self.ambiguous_threshold = self.config.get('ambiguous_threshold', 0.4)

    def match(self, user_input: str, ai_name: str = 'claude', top_k: int = 3) -> Dict:
        """主匹配入口"""
        intent = extract_intent(user_input)

        # 仅在 Tier-1 + Tier-2 中匹配（避免遍历 437 个 Tier-3）
        candidates = [s for s in self.pool['skills'] if s.get('tier', 3) <= 2]

        scores = []
        for s in candidates:
            score, details = score_skill(s, intent)
            scores.append((s, score, details))

        scores.sort(key=lambda x: -x[1])

        if not scores:
            return {
                'action': 'no_match',
                'skill': None,
                'confidence': 0.0,
                'message': '未找到匹配的 skill'
            }

        best_skill, best_score, best_details = scores[0]

        if best_score >= self.threshold:
            # 自动调用
            self._record_usage(best_skill['name'], ai_name)
            return {
                'action': 'auto_invoke',
                'skill': best_skill,
                'confidence': best_score,
                'details': best_details,
                'message': f"🤖 自动匹配: {best_skill['name']}（置信度 {best_score:.0%}）"
            }
        elif best_score >= self.ambiguous_threshold:
            # 询问用户
            alternatives = [
                {'name': s['name'], 'confidence': sc, 'category': s['category']}
                for s, sc, _ in scores[:top_k]
            ]
            return {
                'action': 'ask_user',
                'skill': best_skill,
                'confidence': best_score,
                'alternatives': alternatives,
                'details': best_details,
                'message': f"💡 建议使用: {best_skill['name']}（置信度 {best_score:.0%}），是否确认？"
            }
        else:
            # 提示用 find-skills 检索
            return {
                'action': 'no_match',
                'skill': None,
                'confidence': best_score,
                'alternatives': [
                    {'name': s['name'], 'confidence': sc, 'category': s['category']}
                    for s, sc, _ in scores[:top_k]
                ],
                'message': '🔍 未找到高置信度匹配，建议用 find-skills 检索 Tier-3'
            }

    def _record_usage(self, skill_name: str, ai_name: str):
        """记录使用统计"""
        if not os.path.exists(USAGE_STATS):
            self._init_usage_stats()
        try:
            with open(USAGE_STATS, 'r', encoding='utf-8') as f:
                stats = json.load(f)
            if skill_name not in stats['per_skill']:
                stats['per_skill'][skill_name] = {
                    'total_calls': 0,
                    'last_30_days': 0,
                    'last_7_days': 0,
                    'success_count': 0,
                    'fail_count': 0,
                    'success_rate': 0.0,
                    'last_called': None,
                    'by_ai': {}
                }
            ps = stats['per_skill'][skill_name]
            ps['total_calls'] += 1
            ps['last_30_days'] += 1
            ps['last_7_days'] += 1
            ps['last_called'] = datetime.now().isoformat()
            ps['by_ai'][ai_name] = ps['by_ai'].get(ai_name, 0) + 1
            stats['total_calls'] = stats.get('total_calls', 0) + 1
            stats['last_updated'] = datetime.now().isoformat()

            with open(USAGE_STATS, 'w', encoding='utf-8') as f:
                json.dump(stats, f, ensure_ascii=False, indent=2)
        except Exception as e:
            print(f"⚠️ 记录使用统计失败: {e}")

    def _init_usage_stats(self):
        """初始化使用统计"""
        stats = {
            'version': '1.0',
            'last_updated': datetime.now().isoformat(),
            'total_calls': 0,
            'per_skill': {},
            'per_category': {},
            'per_ai': {}
        }
        with open(USAGE_STATS, 'w', encoding='utf-8') as f:
            json.dump(stats, f, ensure_ascii=False, indent=2)

# ============================================================
# CLI 入口
# ============================================================
if __name__ == '__main__':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

    if len(sys.argv) < 2:
        print("Usage: python router.py \"<user input>\"")
        sys.exit(1)

    user_input = ' '.join(sys.argv[1:])
    router = SkillRouter()
    result = router.match(user_input)

    print("=" * 70)
    print(f"用户输入: {user_input}")
    print("=" * 70)
    print(f"\n{result['message']}\n")

    if result['skill']:
        s = result['skill']
        print(f"  Skill: {s['name']}")
        print(f"  Category: {s['category']}")
        print(f"  Tier: {s['tier']}")
        print(f"  Priority: {s['priority']}")
        print(f"  Path: {s.get('physical_path', 'N/A')}")
        if result.get('details'):
            print(f"  Match details: {result['details']}")

    if result.get('alternatives'):
        print(f"\n  Alternatives:")
        for alt in result['alternatives']:
            print(f"    - [{alt['category']}] {alt['name']} ({alt['confidence']:.0%})")