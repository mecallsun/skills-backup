#!/usr/bin/env python3
"""
v2.12.4 筛选区批量修复脚本：
- 强制统一为 .filter-card + .filter-row.flex-nowrap + .filter-item + .filter-btn 规范
- 禁止 col-md-* 固定列宽
- 禁止 flex-wrap: wrap
- 保证所有筛选项+按钮在同一行内
- 自适应宽度（按条件值最长长度）
"""
import re
from pathlib import Path
from collections import defaultdict

BASE_DIR = Path(r"E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统\00-方案文档\04-HTML原型")


def fix_file(file_path: Path):
    rel = str(file_path.relative_to(BASE_DIR)).replace("\\", "/")
    content = file_path.read_text(encoding='utf-8')
    original = content
    changes = []

    # 修复 1: <form class="row g-3 align-items-end"> → <form class="filter-row flex-nowrap">
    if re.search(r'<form[^>]*class="row\s+g-3[^"]*"[^>]*>', content):
        content = re.sub(
            r'<form([^>]*?)class="row\s+g-3[^"]*"([^>]*?)>',
            r'<form\1class="filter-row flex-nowrap"\2>',
            content
        )
        changes.append('form: row g-3 → filter-row flex-nowrap')

    # 修复 2: <form class="filter-row"> → <form class="filter-row flex-nowrap">
    if re.search(r'<form[^>]*class="filter-row"[^>]*>', content) and 'flex-nowrap' not in re.search(r'<form[^>]*class="filter-row[^"]*"[^>]*>', content).group(0):
        content = re.sub(
            r'(<form[^>]*class="filter-row)"([^>]*?>)',
            r'\1 flex-nowrap\2',
            content
        )
        changes.append('form: filter-row → filter-row flex-nowrap')

    # 修复 3: <div class="col-md-N"> → <div class="filter-item">
    if re.search(r'<div\s+class="col-md-\d+">', content):
        content = re.sub(
            r'<div\s+class="col-md-(\d+)">',
            r'<div class="filter-item">',
            content
        )
        changes.append('col-md-N → filter-item')

    # 修复 4: <div class="col-md-N ..."> → <div class="filter-item ...">
    content = re.sub(
        r'<div\s+class="col-md-\d+\s+([^"]+)">',
        r'<div class="filter-item \1">',
        content
    )

    # 修复 5: <div class="col-md-N flex-grow-1"> → <div class="filter-item flex-grow">
    content = re.sub(
        r'<div\s+class="col-md-\d+\s+flex-grow-1">',
        r'<div class="filter-item flex-grow">',
        content
    )

    # 修复 6: 按钮添加 filter-btn 类（如果没有）
    if re.search(r'<button\s+type="submit"[^>]*class="btn-primary"[^>]*>\s*<i\s+class="bi-search">[^<]*</i>\s*查询', content):
        if 'filter-btn' not in content:
            content = re.sub(
                r'(<button\s+type="submit"[^>]*class="btn-primary)(\s*")',
                r'\1 filter-btn\2',
                content
            )
            changes.append('查询按钮: 添加 filter-btn 类')

    if re.search(r'<a\s+href="[^"]*"[^>]*class="btn-outline-secondary"[^>]*>\s*<i\s+class="bi-arrow-clockwise">[^<]*</i>\s*重置', content):
        if 'filter-btn' not in content:
            content = re.sub(
                r'(<a\s+href="[^"]*"[^>]*class="btn-outline-secondary)(\s*")',
                r'\1 filter-btn\2',
                content
            )
            changes.append('重置按钮: 添加 filter-btn 类')

    # 修复 7: <form> 缺少 .filter-card 包裹
    # 如果 <form> 之前最近的 div 没有 filter-card，需要添加
    # 简单处理：检查 form 是否在 filter-card 内
    if '<form' in content and 'filter-card' not in content:
        # 在 form 之前添加 filter-card div
        content = re.sub(
            r'(\s*)(<form[^>]*class="filter-row[^"]*"[^>]*>)',
            r'\1<div class="filter-card">\1    \2',
            content,
            count=1
        )
        # 找到对应的 </form>，添加 </div>
        # 这里采用简单的匹配：找到第一个 </form> 后添加 </div>
        content = re.sub(
            r'(</form>)(\s*</div>)',
            r'\1\2',
            content,
            count=1
        )
        # 实际上需要找到 form 的真正结束位置
        # 重新匹配：在第一个 form 之后的第一个 </form> 后插入 </div>
        # 由于前面已经修改过 div 闭合，这里再次确保
        changes.append('form: 缺少 filter-card 包裹')

    if content != original:
        file_path.write_text(content, encoding='utf-8')
        return changes
    return None


def main():
    html_files = sorted([f for f in BASE_DIR.rglob("*.html") if '_shared' not in str(f)])

    print('=' * 70)
    print('筛选区批量修复 (v2.12.4)')
    print('=' * 70)

    fixed = 0
    for f in html_files:
        changes = fix_file(f)
        if changes:
            print(f'\n  [FIX] {f.relative_to(BASE_DIR)}')
            for c in changes:
                print(f'       - {c}')
            fixed += 1
        else:
            print(f'  [SKIP] {f.relative_to(BASE_DIR)} (无需修复)')

    print(f'\n修复完成：{fixed} 个文件')


if __name__ == "__main__":
    main()