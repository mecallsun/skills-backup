import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import json
import re
import hashlib
from collections import defaultdict
from datetime import datetime

# ============================================================
# 加载 Step 1 + Step 2 数据
# ============================================================

# Step 1: ~/.claude/skills/（目录列表，未读 SKILL.md）
with open(r'C:\Users\Mecall\.skills-pool\scan_step1_result.json', 'r', encoding='utf-8-sig') as f:
    step1 = json.load(f)

# Step 2: E:\AI工作目录\（SKILL.md 完整路径列表）
with open(r'C:\Users\Mecall\.skills-pool\scan_step2_result.json', 'r', encoding='utf-8') as f:
    step2 = json.load(f)

CLAUDE_SKILLS = r'C:\Users\Mecall\.claude\skills'
E_ROOT = r'E:\AI工作目录'

# ============================================================
# Step 1 的 skills：补充完整路径（不带 SKILL.md 描述指纹）
# ============================================================

step1_skills = []
skip_cats = {'_DIFF_backup_20260808', 'skills.bak', '.agents'}  # 备份目录
for cat_name, skill_names in step1['details'].items():
    if cat_name in skip_cats:
        continue
    for name in skill_names:
        skill_path = os.path.join(CLAUDE_SKILLS, cat_name, name)
        skill_md = os.path.join(skill_path, 'SKILL.md')
        step1_skills.append({
            'name': name,
            'category': cat_name,
            'full_path': skill_path,
            'skill_md_path': skill_md,
            'source': '~/.claude/skills',
            'has_skill_md': os.path.exists(skill_md)
        })

# ============================================================
# Step 2 的 skills：从 SKILL.md 路径反推 name/category
# ============================================================

step2_skills = []
for f in step2['files']:
    rel = f.replace(E_ROOT, '').lstrip('\\/')
    parts = rel.split('\\')
    # 找包含 SKILL.md 的 skill 目录
    # E:\AI工作目录\AI研究项目\产品对比分析\00自媒体技能3月\pptx\SKILL.md
    # parts = ['AI研究项目', '产品对比分析', '00自媒体技能3月', 'pptx', 'SKILL.md']
    if parts[-1] != 'SKILL.md':
        continue
    name = parts[-2] if len(parts) >= 2 else '?'
    # category：找形如 NN-xxx 的祖先
    category = None
    for p in parts[:-2]:
        if re.match(r'^\d{2}-', p):
            category = p
            break
    # 顶层目录
    top_dir = parts[0] if parts else '?'
    # 父目录（用于定位完整路径）
    full_dir = os.path.dirname(f)
    step2_skills.append({
        'name': name,
        'category': category or f'({top_dir})',
        'top_dir': top_dir,
        'full_path': full_dir,
        'skill_md_path': f,
        'source': 'E:\\AI工作目录',
        'has_skill_md': True
    })

print(f"Step 1 skills: {len(step1_skills)}")
print(f"Step 2 skills: {len(step2_skills)}")
print()

# ============================================================
# 多维去重：按 name 分组，相同 name 视为潜在重复
# ============================================================

all_skills = step1_skills + step2_skills
by_name = defaultdict(list)
for s in all_skills:
    by_name[s['name']].append(s)

# 对每个 name 组做相似度判断（基于 SKILL.md 内容 hash）
def read_first_n_chars(path, n=2000):
    if not os.path.exists(path):
        return ''
    try:
        with open(path, 'r', encoding='utf-8', errors='replace') as fp:
            return fp.read(n)
    except Exception:
        return ''

def content_hash(path):
    content = read_first_n_chars(path, 2000)
    # 去除 frontmatter 元数据（保留正文 hash 稳定）
    content = re.sub(r'^---.*?---', '', content, flags=re.DOTALL)
    return hashlib.md5(content.encode('utf-8', errors='replace')).hexdigest()[:12]

# 去重决策
deduped = []  # 保留的唯一 skill
duplicates = []  # 被标记为重复的 skill
conflict_groups = []  # 同 name 但内容不同的冲突组

for name, group in by_name.items():
    if len(group) == 1:
        deduped.append(group[0])
        continue

    # 多份同 name：按内容 hash 分组
    by_hash = defaultdict(list)
    for s in group:
        h = content_hash(s['skill_md_path'])
        s['_content_hash'] = h
        by_hash[h].append(s)

    if len(by_hash) == 1:
        # 完全相同内容 → 保留 Step 1，标重复
        primary = next((s for s in group if s['source'] == '~/.claude/skills'), group[0])
        deduped.append(primary)
        for s in group:
            if s != primary:
                s['duplicates_of'] = primary['full_path']
                duplicates.append(s)
    else:
        # 同 name 不同内容 → 冲突组
        conflict_groups.append({
            'name': name,
            'variants': [
                {
                    'source': s['source'],
                    'full_path': s['full_path'],
                    'content_hash': s.get('_content_hash', ''),
                    'has_skill_md': s['has_skill_md']
                }
                for s in group
            ]
        })
        # 暂时全部保留，但标记 conflict
        for s in group:
            s['_conflict'] = True
            deduped.append(s)

print(f"=== Dedup Result ===")
print(f"Total input: {len(all_skills)}")
print(f"Unique names: {len(by_name)}")
print(f"Deduplicated entries: {len(duplicates)}")
print(f"Conflict groups (same name, different content): {len(conflict_groups)}")
print()

# ============================================================
# 输出中间清单
# ============================================================

result = {
    'scan_date': '2026-08-11',
    'step1_count': len(step1_skills),
    'step2_count': len(step2_skills),
    'total_input': len(all_skills),
    'unique_names': len(by_name),
    'deduplicated': len(duplicates),
    'conflict_groups': len(conflict_groups),
    'deduped_skills': deduped,
    'duplicates': duplicates,
    'conflicts': conflict_groups
}

out_path = r'C:\Users\Mecall\.skills-pool\scan_step3_dedup.json'
with open(out_path, 'w', encoding='utf-8') as fp:
    json.dump(result, fp, ensure_ascii=False, indent=2)

print(f"Result saved to: {out_path}")
print()

# 显示冲突组详情
print("=== Conflict Groups (same name, different content) ===")
for cg in conflict_groups[:20]:
    print(f"\n[{cg['name']}] - {len(cg['variants'])} variants")
    for v in cg['variants']:
        print(f"  - {v['source']}")
        print(f"    path: {v['full_path']}")
        print(f"    hash: {v['content_hash']}, has_md: {v['has_skill_md']}")

if len(conflict_groups) > 20:
    print(f"\n... and {len(conflict_groups) - 20} more (see JSON)")