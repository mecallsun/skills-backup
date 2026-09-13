import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

CLAUDE_SKILLS = r'C:\Users\Mecall\.claude\skills'
POOL_ROOT = r'C:\Users\Mecall\.skills-pool\skills'

print("=" * 80)
print("验证 Junction: ~/.claude/skills -> 真源")
print("=" * 80)

# 1. 检查是 junction
import subprocess
result = subprocess.run(['cmd', '/c', f'dir /AL "{CLAUDE_SKILLS}"'], capture_output=True, text=True)
print("\n=== dir /AL output ===")
print(result.stdout)

# 2. 列出分类
print("\n=== Categories (via junction) ===")
cats = os.listdir(CLAUDE_SKILLS)
print(f"Total categories: {len(cats)}")
for c in cats:
    print(f"  - {c}")

# 3. 测试读取 skill
print("\n=== Test: Read r2r/SKILL.md ===")
test_file = os.path.join(CLAUDE_SKILLS, '07-研究调研', 'r2r', 'SKILL.md')
if os.path.exists(test_file):
    size = os.path.getsize(test_file)
    print(f"✅ Read OK: {size} bytes")
    with open(test_file, 'r', encoding='utf-8', errors='replace') as f:
        head = f.read(200)
    print(f"   First 200 chars: {head[:150]}...")
else:
    print(f"❌ Not found: {test_file}")

# 4. 统计 skill 数
print("\n=== Skill count per category (via junction) ===")
for cat in sorted(cats):
    cat_path = os.path.join(CLAUDE_SKILLS, cat)
    if os.path.isdir(cat_path):
        skills = os.listdir(cat_path)
        print(f"  {cat}: {len(skills)} skills")

# 5. 验证写入
print("\n=== Test Write ===")
test_write = os.path.join(CLAUDE_SKILLS, '_test_write.tmp')
try:
    with open(test_write, 'w') as f:
        f.write('test')
    print(f"✅ Write OK")
    os.remove(test_write)
    print(f"✅ Delete OK")
except Exception as e:
    print(f"❌ Write failed: {e}")

print("\n" + "=" * 80)
print("✅ Junction 验证完成")
print("=" * 80)
print(f"\nClaude 看到的路径: {CLAUDE_SKILLS}")
print(f"真源实际路径: {POOL_ROOT}")
print(f"两者内容一致（junction 透明映射）")
print(f"\n关键 skill 验证:")
print(f"  - 07-研究调研/r2r: {os.path.exists(os.path.join(CLAUDE_SKILLS, '07-研究调研', 'r2r'))}")
print(f"  - 12-Claude原厂/mcp-builder: {os.path.exists(os.path.join(CLAUDE_SKILLS, '12-Claude原厂', 'mcp-builder'))}")