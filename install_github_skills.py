# -*- coding: utf-8 -*-
import os
import shutil
from pathlib import Path

GITHUB_REPO = Path("/tmp/skills-backup")
TARGET_DIR = Path(os.path.expanduser("~/.claude/skills"))

CATEGORIES = ["01-AI工作流", "02-数据分析", "03-A股量化", "04-VibeCoding",
               "05-内容创作", "06-职场效率", "07-研究调研", "08-基础设施",
               "09-开发框架", "10-知识库", "11-安全防护", "12-Claude原厂"]

SKIP_DIRS = {'scripts', 'references', 'assets', 'docs', 'tests', 'plugins',
             'agents', 'hooks', 'rules', 'bin', 'templates', 'palettes',
             'config', '.github', '.claude', '.husky', '.git'}

def get_github_skills():
    """从GitHub仓库获取所有技能"""
    skills = {}

    # 1. .agents/skills 目录
    agents_skills = GITHUB_REPO / ".agents" / "skills"
    if agents_skills.exists():
        for skill_dir in agents_skills.iterdir():
            if skill_dir.is_dir() and (skill_dir / "SKILL.md").exists():
                skill_name = skill_dir.name
                if skill_name not in SKIP_DIRS:
                    skills[skill_name] = skill_dir

    # 2. 顶级独立技能目录（有SKILL.md的直接子目录）
    for item in GITHUB_REPO.iterdir():
        if not item.is_dir() or item.name in ['.git', '.agents', '00自媒体技能']:
            continue
        if (item / "SKILL.md").exists():
            skill_name = item.name
            if skill_name not in SKIP_DIRS:
                skills[skill_name] = item

    return skills

def get_current_skills():
    """获取当前已安装的技能"""
    skills = {}

    for cat_dir in TARGET_DIR.iterdir():
        if not cat_dir.is_dir() or cat_dir.name not in CATEGORIES:
            continue

        for skill_dir in cat_dir.iterdir():
            if skill_dir.is_dir() and (skill_dir / "SKILL.md").exists():
                skill_name = skill_dir.name
                if skill_name not in SKIP_DIRS:
                    skills[skill_name] = skill_dir

    return skills

def main():
    print("=" * 90)
    print("GitHub 备份技能安装程序")
    print("=" * 90)

    github_skills = get_github_skills()
    current_skills = get_current_skills()

    print(f"\nGitHub仓库技能总数: {len(github_skills)}")
    print(f"当前系统技能总数: {len(current_skills)}")

    # 计算差异
    new_skills = {k: v for k, v in github_skills.items() if k not in current_skills}
    existing = {k: v for k, v in github_skills.items() if k in current_skills}

    print(f"\n需要安装的新技能: {len(new_skills)}")
    print(f"已存在（跳过）: {len(existing)}")

    print("\n" + "=" * 90)
    print("GitHub 仓库技能完整清单 (294个)")
    print("=" * 90)
    print(f"\n{'序号':<5} {'技能名称':<45} {'GitHub':<8} {'本地':<8} {'状态':<15}")
    print("-" * 85)

    sorted_github = sorted(github_skills.keys())
    for i, skill_name in enumerate(sorted_github, 1):
        github_status = "[Yes]"
        local_status = "[Yes]" if skill_name in current_skills else "[No]"
        status = "已安装" if skill_name in current_skills else "待安装"
        print(f"{i:<5} {skill_name:<45} {github_status:<8} {local_status:<8} {status:<15}")

    print("-" * 85)
    print(f"总计: {len(sorted_github)} 个技能 (GitHub仓库)")

    # 额外检查：本地有但GitHub没有的
    extra_local = set(current_skills.keys()) - set(github_skills.keys())
    if extra_local:
        print(f"\n本地额外技能（GitHub仓库中没有，共 {len(extra_local)} 个）:")
        print("-" * 60)
        for skill in sorted(extra_local):
            print(f"  + {skill}")

    # 安装新技能
    if new_skills:
        print("\n" + "=" * 90)
        print(f"开始安装 {len(new_skills)} 个新技能...")
        print("=" * 90)

        # 确定每个技能应该放到哪个分类
        skill_category = {}
        for cat_dir in TARGET_DIR.iterdir():
            if cat_dir.name not in CATEGORIES:
                continue
            for skill_dir in cat_dir.iterdir():
                if skill_dir.name in current_skills:
                    skill_category[skill_dir.name] = cat_dir.name

        for skill_name in sorted(new_skills.keys()):
            src_path = new_skills[skill_name]
            cat = skill_category.get(skill_name, "01-AI工作流")
            target = TARGET_DIR / cat / skill_name

            try:
                shutil.copytree(src_path, target)
                print(f"[NEW] {cat}/{skill_name}")
            except Exception as e:
                print(f"[FAIL] {skill_name}: {e}")

        print("\n安装完成!")
    else:
        print("\n所有技能已是最新状态，无需安装。")

    # 最终统计
    final_current = get_current_skills()
    final_github = get_github_skills()

    print(f"\n最终统计:")
    print(f"  GitHub仓库: {len(final_github)} 个技能")
    print(f"  本地系统: {len(final_current)} 个技能")

    if len(final_current) > len(final_github):
        print(f"  本地比GitHub多 {len(final_current) - len(final_github)} 个技能")

if __name__ == "__main__":
    main()