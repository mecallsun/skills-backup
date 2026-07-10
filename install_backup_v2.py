# -*- coding: utf-8 -*-
import os
import shutil
from pathlib import Path

BACKUP_DIR = Path("F:/AI生成工具/AI技能备份")
TARGET_DIR = Path(os.path.expanduser("~/.claude/skills"))

CATEGORIES = ["01-AI工作流", "02-数据分析", "03-A股量化", "04-VibeCoding",
               "05-内容创作", "06-职场效率", "07-研究调研", "08-基础设施",
               "09-开发框架", "10-知识库", "11-安全防护", "12-Claude原厂"]

SKIP_DIRS = {'scripts', 'references', 'assets', 'docs', 'tests', 'plugins',
             'agents', 'hooks', 'rules', 'bin', 'templates', 'palettes',
             'config', '.github', '.claude', '.husky'}

def get_backup_skills():
    """获取所有备份中的唯一技能"""
    skills = {}  # {category: {skill_name: source_path}}

    for backup_subdir in BACKUP_DIR.iterdir():
        if not backup_subdir.is_dir():
            continue

        for cat_dir in backup_subdir.iterdir():
            if not cat_dir.is_dir():
                continue

            cat_name = cat_dir.name
            if cat_name not in CATEGORIES:
                continue

            if cat_name not in skills:
                skills[cat_name] = {}

            for skill_dir in cat_dir.iterdir():
                if not skill_dir.is_dir():
                    continue

                skill_name = skill_dir.name
                if skill_name in SKIP_DIRS:
                    continue

                # 检查SKILL.md（可能在子目录中）
                skill_md = skill_dir / "SKILL.md"
                if not skill_md.exists():
                    # 跳过没有SKILL.md的目录
                    continue

                if skill_name not in skills[cat_name]:
                    skills[cat_name][skill_name] = skill_dir

    return skills

def get_current_skills():
    """获取当前已安装的技能"""
    skills = {}

    for cat_dir in TARGET_DIR.iterdir():
        if not cat_dir.is_dir():
            continue

        cat_name = cat_dir.name
        if cat_name not in CATEGORIES:
            continue

        skills[cat_name] = set()

        for skill_dir in cat_dir.iterdir():
            if not skill_dir.is_dir():
                continue

            skill_name = skill_dir.name
            if skill_name in SKIP_DIRS:
                continue

            if (skill_dir / "SKILL.md").exists():
                skills[cat_name].add(skill_name)

    return skills

def main():
    print("=" * 60)
    print("Backup Skills Installation")
    print("=" * 60)

    backup_skills = get_backup_skills()
    current_skills = get_current_skills()

    # 统计
    backup_total = sum(len(v) for v in backup_skills.values())
    current_total = sum(len(v) for v in current_skills.values())

    print(f"\nBackup unique skills: {backup_total}")
    print(f"Current installed: {current_total}")

    # 计算需要安装的
    to_install = []
    for cat, skills in backup_skills.items():
        current = current_skills.get(cat, set())
        for skill_name, src_path in skills.items():
            if skill_name not in current:
                to_install.append((cat, skill_name, src_path))

    print(f"Will install: {len(to_install)} new")
    print(f"Skip (duplicate): {backup_total - len(to_install)}")

    if not to_install:
        print("\nAll skills already installed!")
        return

    print("\n" + "=" * 60)
    print("Installing new skills...")
    print("=" * 60)

    for cat, skill_name, src_path in to_install:
        target = TARGET_DIR / cat / skill_name
        try:
            shutil.copytree(src_path, target)
            print(f"[NEW] {cat}/{skill_name}")
        except Exception as e:
            print(f"[FAIL] {cat}/{skill_name}: {e}")

    # 最终统计
    final_skills = get_current_skills()
    final_total = sum(len(v) for v in final_skills.values())

    print("\n" + "=" * 60)
    print("Installation Complete!")
    print("=" * 60)
    print(f"New installed: {len(to_install)}")
    print(f"Total skills now: {final_total}")

    # 按分类显示
    print("\nSkills by category:")
    for cat in CATEGORIES:
        count = len(final_skills.get(cat, set()))
        if count > 0:
            print(f"  {cat}: {count}")

if __name__ == "__main__":
    main()