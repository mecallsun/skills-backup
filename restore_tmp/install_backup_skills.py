# -*- coding: utf-8 -*-
import os
import shutil
from pathlib import Path

BACKUP_DIR = Path("F:/AI生成工具/AI技能备份")
TARGET_DIR = Path(os.path.expanduser("~/.claude/skills"))

CATEGORY_MAP = {
    "01-AI工作流": "01-AI工作流",
    "02-数据分析": "02-数据分析",
    "03-A股量化": "03-A股量化",
    "04-VibeCoding": "04-VibeCoding",
    "05-内容创作": "05-内容创作",
    "06-职场效率": "06-职场效率",
    "07-研究调研": "07-研究调研",
    "08-基础设施": "08-基础设施",
    "09-开发框架": "09-开发框架",
    "10-知识库": "10-知识库",
    "11-安全防护": "11-安全防护",
    "12-Claude原厂": "12-Claude原厂",
}

SKIP_DIRS = {'scripts', 'references', 'assets', 'docs', 'tests',
             'plugins', 'agents', 'hooks', 'rules', 'bin', 'templates',
             'palettes', 'config', '.github', '.claude', '.husky'}

def get_all_backup_skills():
    skills = {}
    for backup_subdir in BACKUP_DIR.iterdir():
        if not backup_subdir.is_dir():
            continue
        for category_dir in backup_subdir.iterdir():
            if not category_dir.is_dir():
                continue
            category_name = category_dir.name
            if category_name not in CATEGORY_MAP:
                continue
            for skill_dir in category_dir.iterdir():
                if skill_dir.is_dir() and (skill_dir / "SKILL.md").exists():
                    skill_name = skill_dir.name
                    if skill_name in SKIP_DIRS:
                        continue
                    if category_name not in skills:
                        skills[category_name] = {}
                    if skill_name not in skills[category_name]:
                        skills[category_name][skill_name] = skill_dir
    return skills

def get_current_skills():
    current = {}
    for category_dir in TARGET_DIR.iterdir():
        if not category_dir.is_dir():
            continue
        category_name = category_dir.name
        if category_name not in CATEGORY_MAP:
            continue
        current[category_name] = set()
        for skill_dir in category_dir.iterdir():
            if skill_dir.is_dir() and (skill_dir / "SKILL.md").exists():
                skill_name = skill_dir.name
                if skill_name in SKIP_DIRS:
                    continue
                current[category_name].add(skill_name)
    return current

def install_skills():
    backup_skills = get_all_backup_skills()
    current_skills = get_current_skills()

    print("=" * 60)
    print("Backup Skills Installer")
    print("=" * 60)

    total_backup = sum(len(v) for v in backup_skills.values())
    total_current = sum(len(v) for v in current_skills.values())

    print(f"\nBackup unique skills: {total_backup}")
    print(f"Currently installed: {total_current}")

    to_install = 0
    for cat, skills in backup_skills.items():
        current = current_skills.get(cat, set())
        new_skills = [s for s in skills.keys() if s not in current]
        to_install += len(new_skills)

    print(f"Will install new: {to_install}")
    print(f"Skip duplicates: {total_backup - to_install}")

    print("\n" + "=" * 60)
    print("Starting installation...")
    print("=" * 60)

    installed = 0
    skipped = 0

    for cat in sorted(backup_skills.keys()):
        skills = backup_skills[cat]
        current = current_skills.get(cat, set())
        target_cat_dir = TARGET_DIR / cat

        print(f"\n[{cat}]")

        for skill_name in sorted(skills.keys()):
            src_skill_dir = skills[skill_name]
            if skill_name in current:
                print(f"  [SKIP] (exists): {skill_name}")
                skipped += 1
                continue

            target_skill_dir = target_cat_dir / skill_name

            try:
                if target_skill_dir.exists():
                    print(f"  [SKIP] (exists): {skill_name}")
                    skipped += 1
                else:
                    shutil.copytree(src_skill_dir, target_skill_dir)
                    print(f"  [NEW] : {skill_name}")
                    installed += 1
            except Exception as e:
                    print(f"  [FAIL] : {skill_name} - {str(e)}")

    print("\n" + "=" * 60)
    print("Installation Complete!")
    print("=" * 60)
    print(f"New installed: {installed}")
    print(f"Duplicates skipped: {skipped}")
    print(f"Total now: {total_current + installed}")

    return installed, skipped

if __name__ == "__main__":
    install_skills()