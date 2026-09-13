import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import json
import shutil
from datetime import datetime

# 加载最终索引
with open(r'C:\Users\Mecall\.skills-pool\skills-pool.json', 'r', encoding='utf-8') as f:
    pool = json.load(f)

CLAUDE_SKILLS = r'C:\Users\Mecall\.claude\skills'
POOL_ROOT = r'C:\Users\Mecall\.skills-pool\skills'
BACKUP_ROOT = r'C:\Users\Mecall\.skills-pool\_backup_20260811'
TIMESTAMP = '20260811_224500'

# ============================================================
# Step 9: 创建真源目录骨架
# ============================================================

print("=" * 80)
print("Step 9: 创建真源目录骨架")
print("=" * 80)

created = []
for cat in pool['categories']:
    cat_dir = os.path.join(POOL_ROOT, cat['name'])
    if not os.path.exists(cat_dir):
        os.makedirs(cat_dir)
        created.append(cat_dir)
        print(f"  + {cat['name']}")
    else:
        print(f"  = {cat['name']} (exists)")

print(f"\nCreated {len(created)} new directories")
print()

# ============================================================
# Step 10: 备份原 ~/.claude/skills/
# ============================================================

print("=" * 80)
print("Step 10: 备份 ~/.claude/skills/")
print("=" * 80)

backup_target = f"{BACKUP_ROOT}_{TIMESTAMP}"

if not os.path.exists(backup_target):
    print(f"Backup to: {backup_target}")
    try:
        # 用 shutil.copytree 镜像复制
        shutil.copytree(
            CLAUDE_SKILLS,
            backup_target,
            ignore=shutil.ignore_patterns('_DIFF_backup_*', 'skills.bak', '.agents')
        )
        print(f"  ✅ Backup created")

        # 统计备份大小
        total_size = sum(
            os.path.getsize(os.path.join(dp, f))
            for dp, dn, fn in os.walk(backup_target)
            for f in fn
        )
        print(f"  Backup size: {total_size / 1024 / 1024:.1f} MB")
    except Exception as e:
        print(f"  ❌ Backup failed: {e}")
else:
    print(f"  = Backup already exists: {backup_target}")

print()

# ============================================================
# Step 11: 复制 skill 到真源
# ============================================================

print("=" * 80)
print("Step 11: 复制 skill 到真源")
print("=" * 80)

success_count = 0
fail_count = 0
skipped = []
errors = []

for skill in pool['skills']:
    src = skill['source_path']
    dst = skill['physical_path']

    if not src or not os.path.exists(src):
        skipped.append({
            'skill': skill['name'],
            'reason': 'source not exists',
            'source': src
        })
        fail_count += 1
        continue

    # 目标目录已存在则跳过
    if os.path.exists(dst):
        skipped.append({
            'skill': skill['name'],
            'reason': 'already exists',
            'destination': dst
        })
        continue

    try:
        # 复制整个目录
        shutil.copytree(src, dst, dirs_exist_ok=False)
        success_count += 1
    except FileExistsError:
        skipped.append({
            'skill': skill['name'],
            'reason': 'FileExistsError',
            'destination': dst
        })
        fail_count += 1
    except Exception as e:
        errors.append({
            'skill': skill['name'],
            'source': src,
            'destination': dst,
            'error': str(e)
        })
        fail_count += 1

print(f"Success: {success_count}")
print(f"Failed/Skipped: {fail_count}")
print(f"Errors: {len(errors)}")
print()

if skipped[:5]:
    print("=== Sample Skipped ===")
    for s in skipped[:5]:
        print(f"  - {s}")

if errors:
    print("\n=== Errors ===")
    for e in errors[:5]:
        print(f"  - {e['skill']}: {e['error']}")

# 保存执行报告
report = {
    'execution_time': datetime.now().isoformat(),
    'pool_root': POOL_ROOT,
    'backup_path': backup_target,
    'success_count': success_count,
    'fail_count': fail_count,
    'skipped_count': len(skipped),
    'errors': errors,
    'skipped_sample': skipped[:20]
}

with open(r'C:\Users\Mecall\.skills-pool\scan_step9_11_report.json', 'w', encoding='utf-8') as fp:
    json.dump(report, fp, ensure_ascii=False, indent=2)

print(f"\nReport saved to: scan_step9_11_report.json")