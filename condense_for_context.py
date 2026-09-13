"""
硬限 200 个 skill description 进 Claude context
其他 300 个标 lazy=true，需要时 /skills <name> 显式调用

执行：
  python condense_for_context.py dry-run   # 只统计
  python condense_for_context.py apply     # 实际改 JSON
"""
import os
import json
import io
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

POOL_JSON = r'C:\Users\Mecall\.skills-pool\skills-pool.json'
CONTEXT_LIMIT = 200  # 进 context 的 skill 上限


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ('dry-run', 'apply'):
        print('Usage: python condense_for_context.py [dry-run|apply]')
        sys.exit(1)

    mode = sys.argv[1]
    with open(POOL_JSON, 'r', encoding='utf-8') as f:
        pool = json.load(f)

    skills = pool['skills']
    total = len(skills)

    # 排序：Tier 升序 + priority 降序 + usage_count 降序
    sorted_skills = sorted(
        skills,
        key=lambda s: (s.get('tier', 3), -s.get('priority', 0), -s.get('usage_count', 0))
    )

    # 划分
    in_context = sorted_skills[:CONTEXT_LIMIT]
    lazy = sorted_skills[CONTEXT_LIMIT:]

    # 统计
    in_tiers = {1: 0, 2: 0, 3: 0}
    out_tiers = {1: 0, 2: 0, 3: 0}
    for s in in_context:
        in_tiers[s.get('tier', 3)] += 1
    for s in lazy:
        out_tiers[s.get('tier', 3)] += 1

    print(f'[扫描] total = {total}')
    print(f'[进 context] {len(in_context)} 个 (上限 {CONTEXT_LIMIT})')
    print(f'  Tier-1: {in_tiers[1]}')
    print(f'  Tier-2: {in_tiers[2]}')
    print(f'  Tier-3: {in_tiers[3]}')
    print(f'[懒加载] {len(lazy)} 个（需要时 /skills <name> 调用）')
    print(f'  Tier-1 漏出: {out_tiers[1]}（不应有）')
    print(f'  Tier-2 漏出: {out_tiers[2]}')
    print(f'  Tier-3 漏出: {out_tiers[3]}')

    if out_tiers[1] > 0:
        print(f'\n[WARN] {out_tiers[1]} 个 Tier-1 skill 被挤出 context！')
        print('       这些是 critical，应优先加载。建议把 CONTEXT_LIMIT 提到 220。')

    if mode == 'dry-run':
        print('\n[dry-run] 未修改 JSON。运行 apply 实际执行。')
        return

    # 实际修改
    for s in skills:
        s['context_load'] = False  # 默认不进
    for s in in_context:
        s['context_load'] = True

    # 更新 metadata
    pool['metadata']['context_load_strategy'] = {
        'version': '1.0',
        'limit': CONTEXT_LIMIT,
        'rule': 'tier asc + priority desc + usage desc',
        'lazy_count': len(lazy),
        'loaded_count': len(in_context)
    }
    pool['version'] = '1.2.0'

    # 写回
    with open(POOL_JSON, 'w', encoding='utf-8') as f:
        json.dump(pool, f, ensure_ascii=False, indent=2)

    print(f'\n[OK] skills-pool.json 已更新到 v1.2.0')
    print(f'     context_load=True  →  {len(in_context)} 个')
    print(f'     context_load=False →  {len(lazy)} 个')
    print(f'\n[下一步] 重启 Claude Code，新会话将加载 {len(in_context)} 个 description')


if __name__ == '__main__':
    main()