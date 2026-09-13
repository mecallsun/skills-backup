import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import subprocess
import shutil

CLAUDE_SKILLS = r'C:\Users\Mecall\.claude\skills'
POOL_ROOT = r'C:\Users\Mecall\.skills-pool\skills'
CODEX_SKILLS = r'C:\Users\Mecall\.codex\skills'
BACKUP_PATH = r'C:\Users\Mecall\.skills-pool\_backup_20260811_20260811_224500'

# ============================================================
# Pre-check
# ============================================================

print("=" * 80)
print("Step 13: 删除原 ~/.claude/skills/ （已被备份）")
print("=" * 80)

# 检查备份存在
if not os.path.exists(BACKUP_PATH):
    print(f"❌ Backup not found: {BACKUP_PATH}")
    print("Refusing to delete original without backup")
    sys.exit(1)
print(f"✅ Backup exists: {BACKUP_PATH}")

# 检查目标链接是否已存在
if os.path.exists(CLAUDE_SKILLS):
    # 判断是符号链接还是实体目录
    if os.path.islink(CLAUDE_SKILLS):
        print(f"⚠️ {CLAUDE_SKILLS} is already a symlink")
        print(f"  Target: {os.readlink(CLAUDE_SKILLS)}")
    else:
        # 是实体目录，需要处理
        print(f"📂 {CLAUDE_SKILLS} is a real directory")
        # 检查是否为空
        contents = os.listdir(CLAUDE_SKILLS)
        print(f"  Contents: {len(contents)} entries")
        if contents:
            print(f"  Sample: {contents[:5]}")
else:
    print(f"⚠️ {CLAUDE_SKILLS} not exists (unusual)")

# 检查真源存在
if not os.path.exists(POOL_ROOT):
    print(f"❌ Pool not found: {POOL_ROOT}")
    sys.exit(1)
print(f"✅ Pool exists: {POOL_ROOT}")
print()

# ============================================================
# 执行删除（仅在是实体目录时）
# ============================================================

if os.path.exists(CLAUDE_SKILLS) and not os.path.islink(CLAUDE_SKILLS):
    print(f"Removing: {CLAUDE_SKILLS}")
    try:
        shutil.rmtree(CLAUDE_SKILLS)
        print("  ✅ Removed")
    except Exception as e:
        print(f"  ❌ Failed: {e}")
        sys.exit(1)
elif os.path.islink(CLAUDE_SKILLS):
    print("Already a symlink, skipping removal")
elif not os.path.exists(CLAUDE_SKILLS):
    print("Path does not exist, will create symlink directly")

print()

# ============================================================
# Step 14: 创建符号链接
# ============================================================

print("=" * 80)
print("Step 14: 创建符号链接")
print("=" * 80)

def create_symlink(target, link_path):
    """用 mklink /D 创建符号链接"""
    if os.path.exists(link_path) or os.path.islink(link_path):
        print(f"⚠️ Link already exists: {link_path}")
        if os.path.islink(link_path):
            print(f"   Target: {os.readlink(link_path)}")
        return False

    # 使用 cmd mklink
    cmd = f'cmd /c mklink /D "{link_path}" "{target}"'
    print(f"Running: {cmd}")
    result = subprocess.run(cmd, shell=True, capture_output=True, text=True)
    print(result.stdout)
    if result.stderr:
        print("STDERR:", result.stderr)
    if result.returncode != 0:
        print(f"❌ mklink failed with code {result.returncode}")
        return False
    print(f"✅ Symlink created: {link_path} -> {target}")
    return True

# Claude Code 符号链接
print("\n[1] Claude Code")
create_symlink(POOL_ROOT, CLAUDE_SKILLS)

# Codex 符号链接（如果 codex skills 目录不存在）
print("\n[2] Codex")
codex_parent = os.path.dirname(CODEX_SKILLS)
if not os.path.exists(codex_parent):
    print(f"⚠️ Codex dir not exists: {codex_parent}")
    print("  Skipping Codex symlink (Codex not installed)")
else:
    create_symlink(POOL_ROOT, CODEX_SKILLS)

print()

# ============================================================
# 验证
# ============================================================

print("=" * 80)
print("验证：链接是否生效")
print("=" * 80)

# 1. 检查 Claude 端
if os.path.islink(CLAUDE_SKILLS):
    print(f"\n✅ Claude: {CLAUDE_SKILLS} is a symlink")
    print(f"   Target: {os.readlink(CLAUDE_SKILLS)}")
    # 列出内容（应该等于真源）
    contents = os.listdir(CLAUDE_SKILLS)
    print(f"   Contents: {len(contents)} categories")
    for c in contents[:5]:
        print(f"     - {c}")
    if len(contents) > 5:
        print(f"     ... +{len(contents)-5} more")

    # 找一个 skill 验证可访问
    test_skill = os.path.join(CLAUDE_SKILLS, '07-研究调研', 'r2r', 'SKILL.md')
    if os.path.exists(test_skill):
        print(f"   ✅ Test read: {test_skill}")
        size = os.path.getsize(test_skill)
        print(f"      Size: {size} bytes")
else:
    print(f"\n❌ Claude: {CLAUDE_SKILLS} is NOT a symlink")

# 2. 检查 Codex 端
if os.path.islink(CODEX_SKILLS):
    print(f"\n✅ Codex: {CODEX_SKILLS} is a symlink")
    print(f"   Target: {os.readlink(CODEX_SKILLS)}")
elif os.path.exists(CODEX_SKILLS):
    print(f"\n📂 Codex: {CODEX_SKILLS} is a real directory (untouched)")
else:
    print(f"\n⚠️ Codex: {CODEX_SKILLS} not exists")

print()

# 3. 总结
print("=" * 80)
print("Step 13-14 完成")
print("=" * 80)
print(f"Pool (source of truth): {POOL_ROOT}")
print(f"  Categories: {len(os.listdir(POOL_ROOT))}")
print(f"  Total skills: ~500")
print()
print(f"Claude link: {CLAUDE_SKILLS}")
print(f"  Is symlink: {os.path.islink(CLAUDE_SKILLS)}")
print(f"  Points to: {os.readlink(CLAUDE_SKILLS) if os.path.islink(CLAUDE_SKILLS) else 'N/A'}")
print()
print(f"Codex link: {CODEX_SKILLS}")
print(f"  Is symlink: {os.path.islink(CODEX_SKILLS)}")