"""
全局 Skill 池 — 健康检查脚本
每日运行，检查池完整性
"""
import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import json
import subprocess
from datetime import datetime

POOL_ROOT = r'C:\Users\Mecall\.skills-pool\skills'
POOL_JSON = r'C:\Users\Mecall\.skills-pool\skills-pool.json'
CLAUDE_SKILLS = r'C:\Users\Mecall\.claude\skills'
USAGE_STATS = r'C:\Users\Mecall\.skills-pool\usage-stats.json'

def run_check(name, condition, message):
    """运行一项检查"""
    icon = '✅' if condition else '❌'
    print(f"  {icon} [{name}] {message}")
    return condition

def main():
    print("=" * 80)
    print(f"🏥 全局 Skill 池健康检查")
    print(f"   时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print("=" * 80)

    results = {}

    # 1. 真源目录存在
    print("\n[1] 真源目录")
    print("-" * 60)
    results['pool_exists'] = run_check(
        'pool_exists',
        os.path.exists(POOL_ROOT),
        f'真源目录存在: {POOL_ROOT}'
    )

    # 2. 12 个分类
    print("\n[2] 标准分类")
    print("-" * 60)
    if results['pool_exists']:
        cats = os.listdir(POOL_ROOT)
        results['categories'] = run_check(
            'categories',
            len(cats) >= 12,
            f'分类数: {len(cats)} (期望 ≥12)'
        )
        for cat in sorted(cats):
            count = len(os.listdir(os.path.join(POOL_ROOT, cat))) if os.path.isdir(os.path.join(POOL_ROOT, cat)) else 0
            print(f"      {cat}: {count} 个 skill")

    # 3. 主索引存在且完整
    print("\n[3] skills-pool.json")
    print("-" * 60)
    results['pool_json'] = run_check(
        'pool_json',
        os.path.exists(POOL_JSON),
        f'主索引存在: {POOL_JSON}'
    )

    if results['pool_json']:
        with open(POOL_JSON, 'r', encoding='utf-8') as f:
            pool = json.load(f)
        results['pool_version'] = run_check(
            'pool_version',
            pool.get('version'),
            f'版本: {pool.get("version", "N/A")}'
        )
        results['total_skills'] = run_check(
            'total_skills',
            pool['stats']['total_skills'] == 500,
            f'Total skills: {pool["stats"]["total_skills"]} (期望 500)'
        )
        results['tier1_limit'] = run_check(
            'tier1_limit',
            pool['stats']['tier1_count'] <= 20,
            f'Tier-1: {pool["stats"]["tier1_count"]} (上限 20)'
        )
        results['tier2_limit'] = run_check(
            'tier2_limit',
            pool['stats']['tier2_count'] <= 50,
            f'Tier-2: {pool["stats"]["tier2_count"]} (上限 50)'
        )

    # 4. Junction 透明访问
    print("\n[4] Junction（Claude 端）")
    print("-" * 60)
    results['junction_exists'] = run_check(
        'junction_exists',
        os.path.exists(CLAUDE_SKILLS),
        f'Junction 存在: {CLAUDE_SKILLS}'
    )

    if results['junction_exists']:
        # 测试读关键 skill
        test_skill = os.path.join(CLAUDE_SKILLS, '07-研究调研', 'r2r', 'SKILL.md')
        results['junction_readable'] = run_check(
            'junction_readable',
            os.path.exists(test_skill),
            '关键 skill (r2r) 可读'
        )

    # 5. 使用统计
    print("\n[5] usage-stats.json")
    print("-" * 60)
    results['usage_stats'] = run_check(
        'usage_stats',
        os.path.exists(USAGE_STATS),
        f'使用统计存在: {USAGE_STATS}'
    )

    # 6. Tier-1 skill 完整性
    print("\n[6] Tier-1 skill 完整性")
    print("-" * 60)
    tier1_names = [
        'r2r', 'deep-research', 'council', 'ppt-master',
        'mcp-builder', 'skill-creator', 'webapp-testing',
        'pdf', 'docx', 'xlsx', 'markitdown',
        'article-writing', 'seo', 'brand-voice',
        'mcp-server-patterns', 'browser-automation',
        'parallel-deep-research', 'search-first',
        'find-skills', 'market-research'
    ]
    missing = []
    for name in tier1_names:
        # 在任意分类找到即可
        found = False
        for cat in os.listdir(POOL_ROOT) if os.path.exists(POOL_ROOT) else []:
            if os.path.exists(os.path.join(POOL_ROOT, cat, name, 'SKILL.md')):
                found = True
                break
        if not found:
            missing.append(name)
    results['tier1_complete'] = run_check(
        'tier1_complete',
        len(missing) == 0,
        f'Tier-1 skill 完整: {len(tier1_names) - len(missing)}/{len(tier1_names)}'
        + (f' (缺失: {", ".join(missing)})' if missing else '')
    )

    # 7. 备份存在
    print("\n[7] 备份")
    print("-" * 60)
    backup_dir = None
    for d in os.listdir(r'C:\Users\Mecall\.skills-pool') if os.path.exists(r'C:\Users\Mecall\.skills-pool') else []:
        if d.startswith('_backup_'):
            backup_dir = os.path.join(r'C:\Users\Mecall\.skills-pool', d)
            break
    results['backup'] = run_check(
        'backup',
        backup_dir and os.path.exists(backup_dir),
        f'备份存在: {backup_dir or "N/A"}'
    )

    # ============================================================
    # 总结
    # ============================================================
    print("\n" + "=" * 80)
    print("📋 健康检查总结")
    print("=" * 80)
    passed = sum(1 for v in results.values() if v)
    total = len(results)
    print(f"通过: {passed}/{total}")
    if passed == total:
        print("✅ 全部健康")
        return 0
    else:
        print(f"⚠️ {total - passed} 项需关注")
        return 1

if __name__ == '__main__':
    sys.exit(main())