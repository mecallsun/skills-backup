import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import json
from collections import Counter

with open(r'C:\Users\Mecall\.skills-pool\skills-pool.json', 'r', encoding='utf-8') as f:
    pool = json.load(f)

print("=" * 80)
print("🔍 全局 Skill 池 v1.0 优化诊断")
print("=" * 80)

# ============================================================
# 问题 1: Tier 划分是否合理？
# ============================================================
print("\n[问题 1] Tier-1 名单是否合理？")
print("-" * 60)
tier1 = [s for s in pool['skills'] if s['tier'] == 1]
print(f"Tier-1 数量: {len(tier1)} / 上限 20")
print("当前名单:")
for s in tier1:
    print(f"  - [{s['category']}] {s['name']}")

# 评估：缺少什么核心 skill？
essential_skills_not_in_t1 = []
essentials = [
    ('07-研究调研', 'market-research'),  # 市场调研
    ('11-安全防护', 'security-scan'),    # 安全扫描
    ('11-安全防护', 'security-review'),  # 安全审查
    ('06-职场效率', 'meeting-notes'),    # 会议记录
    ('05-内容创作', 'seo'),              # SEO
    ('05-内容创作', 'brand-voice'),      # 品牌声音
    ('04-VibeCoding', 'pptx-01'),        # PPT 变体
]
tier1_names = {s['name'] for s in tier1}
for cat, name in essentials:
    if name not in tier1_names:
        essential_skills_not_in_t1.append((cat, name))
        print(f"  ⚠️ 缺失核心: {cat}/{name}")

# ============================================================
# 问题 2: Tier-2 是否过多？
# ============================================================
print("\n[问题 2] Tier-2 数量与构成")
print("-" * 60)
tier2 = [s for s in pool['skills'] if s['tier'] == 2]
print(f"Tier-2 数量: {len(tier2)} / 上限 50")
tier2_by_cat = Counter(s['category'] for s in tier2)
print("按分类分布:")
for cat, cnt in tier2_by_cat.most_common():
    print(f"  - {cat}: {cnt}")

# ============================================================
# 问题 3: 变体 skill 命名混乱
# ============================================================
print("\n[问题 3] 变体 skill 命名分析")
print("-" * 60)
variant_groups = pool['variant_groups']
for vg in variant_groups:
    print(f"\n[{vg['name']}] {vg['variant_count']} 个变体")
    for v in vg['variants']:
        final_name = v['final_name']
        source_short = v['source'].replace('~/.claude/skills', '~/.claude')
        source_short = source_short.replace('E:\\AI工作目录', 'E:\\\\AI工作目录')
        if len(source_short) > 50:
            source_short = source_short[:47] + '...'
        print(f"  - {final_name:25s} ← {source_short}")

# ============================================================
# 问题 4: tags 完整性
# ============================================================
print("\n[问题 4] tags 字段完整性")
print("-" * 60)
empty_tags = 0
weak_tags = 0
for s in pool['skills']:
    tags = s.get('tags', {})
    if not tags:
        empty_tags += 1
    else:
        total = sum(len(v) if isinstance(v, list) else 0 for v in tags.values())
        if total < 4:
            weak_tags += 1
print(f"完全无 tags: {empty_tags}")
print(f"tags < 4 维度: {weak_tags}")

# ============================================================
# 问题 5: triggers 字段空
# ============================================================
print("\n[问题 5] triggers（触发器）字段")
print("-" * 60)
no_triggers = 0
triggers_count = []
for s in pool['skills']:
    t = s.get('triggers', {})
    if not t or not t.get('keywords'):
        no_triggers += 1
    else:
        triggers_count.append(len(t.get('keywords', [])))
print(f"无任何 trigger: {no_triggers} (应自动推断)")
print(f"平均 trigger keywords 数: {sum(triggers_count)/len(triggers_count) if triggers_count else 0:.1f}")

# ============================================================
# 问题 6: backup 路径双重时间戳
# ============================================================
print("\n[问题 6] 备份路径命名")
print("-" * 60)
backup_dir = r'C:\Users\Mecall\.skills-pool\_backup_20260811_20260811_224500'
print(f"当前: {backup_dir}")
print(f"问题: 双重时间戳前缀，应为: _backup_20260811_224500")

# ============================================================
# 问题 7: 使用统计未启动
# ============================================================
print("\n[问题 7] 使用统计系统")
print("-" * 60)
usage_path = r'C:\Users\Mecall\.skills-pool\usage-stats.json'
print(f"usage-stats.json: {'✅ 存在' if os.path.exists(usage_path) else '❌ 不存在'}")
print(f"建议: 启动时初始化 + 每次 skill 调用后更新")

# ============================================================
# 问题 8: 健康监控脚本缺失
# ============================================================
print("\n[问题 8] 健康监控")
print("-" * 60)
health_path = r'C:\Users\Mecall\.skills-pool\health-check.py'
print(f"health-check.py: {'✅ 存在' if os.path.exists(health_path) else '❌ 不存在'}")
print(f"建议: 添加每日健康检查 + 自动告警")

# ============================================================
# 问题 9: 自动匹配引擎未实装
# ============================================================
print("\n[问题 9] 自动匹配引擎")
print("-" * 60)
router_path = r'C:\Users\Mecall\.skills-pool\skill-router\router.py'
print(f"router.py: {'✅ 存在' if os.path.exists(router_path) else '❌ 不存在'}")
print(f"建议: 实装完整打分算法（之前只在 JSON 定义）")

# ============================================================
# 问题 10: Tier-1 自动加载机制
# ============================================================
print("\n[问题 10] Tier-1 启动加载")
print("-" * 60)
print(f"当前状态: 仅在 CLAUDE.md 中描述，未实际启动加载机制")
print(f"建议: 在 Claude Code 启动时检测 Tier-1 并预热")

print("\n" + "=" * 80)
print("📋 优化清单（按优先级）")
print("=" * 80)
print("""
P0 - 必须修复（影响核心功能）
  1. 实装自动匹配引擎（router.py）
  2. 修复备份路径命名（双重时间戳）
  3. 启动 usage-stats.json 统计系统

P1 - 重要优化（影响效率）
  4. 增强 Tier-1 名单（补 7 个核心 skill）
  5. 完善 triggers 字段（自动推断 keywords）
  6. 增强 tags 完整性（6 维度全覆盖）
  7. 添加健康检查脚本

P2 - 体验提升（锦上添花）
  8. Tier-1 启动预热脚本
  9. 跨 AI 使用统计同步机制
  10. 优化变体命名（带 -01 后缀补全）
""")