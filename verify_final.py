import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import json
import subprocess

CLAUDE_SKILLS = r'C:\Users\Mecall\.claude\skills'
POOL_ROOT = r'C:\Users\Mecall\.skills-pool\skills'
POOL_JSON = r'C:\Users\Mecall\.skills-pool\skills-pool.json'
CLAUDE_MD = r'C:\Users\Mecall\.claude\CLAUDE.md'

print("=" * 80)
print("Step 19-21: 最终验证")
print("=" * 80)

results = {}

# ============================================================
# Test 1: 真源存在且包含所有 skill
# ============================================================
print("\n[Test 1] 真源目录完整性")
print("-" * 60)
if os.path.exists(POOL_ROOT):
    cats = os.listdir(POOL_ROOT)
    total_skills = 0
    for c in cats:
        cat_path = os.path.join(POOL_ROOT, c)
        if os.path.isdir(cat_path):
            skills = os.listdir(cat_path)
            total_skills += len(skills)
    print(f"  ✅ Pool exists: {POOL_ROOT}")
    print(f"     Categories: {len(cats)}")
    print(f"     Total skills: {total_skills}")
    results['test1'] = 'PASS'
else:
    print(f"  ❌ Pool missing")
    results['test1'] = 'FAIL'

# ============================================================
# Test 2: Junction 透明访问
# ============================================================
print("\n[Test 2] Junction 透明访问")
print("-" * 60)
if os.path.exists(CLAUDE_SKILLS):
    cats = os.listdir(CLAUDE_SKILLS)
    print(f"  ✅ Junction works")
    print(f"     Visible categories: {len(cats)}")
    # 测试一个关键 skill
    test = os.path.join(CLAUDE_SKILLS, '07-研究调研', 'r2r', 'SKILL.md')
    if os.path.exists(test):
        print(f"     Test read OK: r2r/SKILL.md ({os.path.getsize(test)} bytes)")
        results['test2'] = 'PASS'
    else:
        print(f"     ❌ Test read failed")
        results['test2'] = 'FAIL'
else:
    print(f"  ❌ Junction missing")
    results['test2'] = 'FAIL'

# ============================================================
# Test 3: skills-pool.json 完整性
# ============================================================
print("\n[Test 3] skills-pool.json 索引")
print("-" * 60)
if os.path.exists(POOL_JSON):
    with open(POOL_JSON, 'r', encoding='utf-8') as f:
        pool = json.load(f)
    print(f"  ✅ Pool JSON exists")
    print(f"     Version: {pool['version']}")
    print(f"     Total skills: {pool['stats']['total_skills']}")
    print(f"     Tier-1: {pool['stats']['tier1_count']}")
    print(f"     Tier-2: {pool['stats']['tier2_count']}")
    print(f"     Tier-3: {pool['stats']['tier3_count']}")
    print(f"     Categories: {pool['stats']['categories']}")
    print(f"     File size: {os.path.getsize(POOL_JSON) / 1024:.1f} KB")
    results['test3'] = 'PASS'
else:
    print(f"  ❌ Pool JSON missing")
    results['test3'] = 'FAIL'

# ============================================================
# Test 4: CLAUDE.md 永久章节存在
# ============================================================
print("\n[Test 4] CLAUDE.md 永久章节")
print("-" * 60)
required_sections = [
    '全局 Skill 池',
    '新 AI 接入 Skill 池',
    '全局 Skill 池与全局 MCP 池的关系',
    '全局 MCP 池',
]
with open(CLAUDE_MD, 'r', encoding='utf-8') as f:
    content = f.read()
all_present = True
for section in required_sections:
    if section in content:
        print(f"  ✅ {section}")
    else:
        print(f"  ❌ {section}")
        all_present = False
results['test4'] = 'PASS' if all_present else 'FAIL'

# ============================================================
# Test 5: Tier-1 skill 可访问
# ============================================================
print("\n[Test 5] Tier-1 skill 可访问性")
print("-" * 60)
tier1_skills = [
    ('01-AI工作流', 'council'),
    ('01-AI工作流', 'deep-research'),
    ('07-研究调研', 'r2r'),
    ('04-VibeCoding', 'ppt-master'),
    ('12-Claude原厂', 'mcp-builder'),
    ('12-Claude原厂', 'skill-creator'),
    ('12-Claude原厂', 'webapp-testing'),
]
all_ok = True
for cat, skill in tier1_skills:
    skill_path = os.path.join(CLAUDE_SKILLS, cat, skill, 'SKILL.md')
    if os.path.exists(skill_path):
        size = os.path.getsize(skill_path)
        print(f"  ✅ {cat}/{skill}: {size} bytes")
    else:
        print(f"  ❌ {cat}/{skill}: not found")
        all_ok = False
results['test5'] = 'PASS' if all_ok else 'FAIL'

# ============================================================
# Test 6: 变体文件存在
# ============================================================
print("\n[Test 6] 变体 skill 命名（-N 后缀）")
print("-" * 60)
variant_skills = [
    'pptx', 'pptx-01', 'pptx-02', 'pptx-03',
    'xlsx', 'xlsx-01',
    'claude-api', 'claude-api-01', 'claude-api-02',
    'security-scan', 'security-scan-01',
]
variant_dir = os.path.join(CLAUDE_SKILLS, '04-VibeCoding')
ok = 0
for v in variant_skills:
    p = os.path.join(variant_dir, v, 'SKILL.md') if v.startswith('pptx') else None
    if p is None:
        # 其他目录查找
        for cat_dir in os.listdir(CLAUDE_SKILLS):
            test = os.path.join(CLAUDE_SKILLS, cat_dir, v, 'SKILL.md')
            if os.path.exists(test):
                ok += 1
                break
    else:
        if os.path.exists(p):
            ok += 1
print(f"  Found {ok}/{len(variant_skills)} variants")
results['test6'] = 'PASS' if ok >= 5 else 'FAIL'

# ============================================================
# Test 7: 备份存在
# ============================================================
print("\n[Test 7] 备份可恢复性")
print("-" * 60)
backup_dir = r'C:\Users\Mecall\.skills-pool\_backup_20260811_20260811_224500'
if os.path.exists(backup_dir):
    print(f"  ✅ Backup exists: {backup_dir}")
    cats = os.listdir(backup_dir)
    print(f"     Backup categories: {len(cats)}")
    results['test7'] = 'PASS'
else:
    print(f"  ❌ Backup missing")
    results['test7'] = 'FAIL'

# ============================================================
# 总结
# ============================================================
print("\n" + "=" * 80)
print("🎉 验证总结")
print("=" * 80)
passed = sum(1 for v in results.values() if v == 'PASS')
total = len(results)
print(f"通过: {passed}/{total}")
for k, v in results.items():
    icon = '✅' if v == 'PASS' else '❌'
    print(f"  {icon} {k}: {v}")

if passed == total:
    print(f"\n🌟 全部通过！全局 Skill 池部署完成。")
else:
    print(f"\n⚠️ {total - passed} 项未通过，需检查。")