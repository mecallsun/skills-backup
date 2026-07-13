#!/usr/bin/env python3
"""
清理脚本：移除迁移后遗留的 <div class="container-fluid px-4"> 旧包装
"""
import re
from pathlib import Path

BASE_DIR = Path(r"E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统\00-方案文档\04-HTML原型")

# 清理规则
CLEANUP_RULES = [
    # 移除 <div class="container-fluid px-4"> 开标签
    (r'<div class="container-fluid px-4">\s*\n?', ''),
    # 移除 <div class="container-fluid px-4 py-3"> 开标签
    (r'<div class="container-fluid px-4 py-3">\s*\n?', ''),
    # 移除简单的 <div class="container py-4">
    (r'<div class="container py-4">\s*\n?', ''),
    # 移除 <div id="nav"></div>
    (r'<div id="nav"></div>\s*\n?', ''),
    # 移除重复的 Tier 1 注释
    (r'(<!-- ========== Tier 1: 顶部品牌栏 \(48px\) ========== -->\s*){2,}',
     '<!-- ========== Tier 1: 顶部品牌栏 (48px) ========== -->\n'),
    # 移除重复的 page-content 开标签
    (r'(<div class="page-content">\s*\n?\s*){2,}',
     '<div class="page-content">\n'),
]

def cleanup_file(file_path: Path):
    content = file_path.read_text(encoding='utf-8')
    original = content

    for pattern, replacement in CLEANUP_RULES:
        content = re.sub(pattern, replacement, content)

    if content != original:
        file_path.write_text(content, encoding='utf-8')
        return True
    return False


def main():
    html_files = [f for f in BASE_DIR.rglob("*.html") if '_shared' not in str(f)]
    cleaned = 0
    for f in html_files:
        if cleanup_file(f):
            print(f"  [CLEAN] {f.relative_to(BASE_DIR)}")
            cleaned += 1

    print(f"\n清理完成：{cleaned} 个文件已更新")


if __name__ == "__main__":
    main()