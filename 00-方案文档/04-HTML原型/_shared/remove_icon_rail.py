#!/usr/bin/env python3
"""
v2.12.3 移除 Tier 2 紧凑型图标导航条
- 删除 <div id="icon-rail"></div>
- 删除 mountIconRail() 调用
- 删除 icon-rail.js 引用
- 删除 icon-rail.js 注释
- 调整 layout 为三层架构
"""
import re
from pathlib import Path

BASE_DIR = Path(r"E:\AI工作目录\AI编程开发\JINGE开发\住宿管理系统\00-方案文档\04-HTML原型")

def remove_icon_rail(file_path: Path):
    content = file_path.read_text(encoding='utf-8')
    original = content

    # 1) 删除整个 icon-rail div 块（含注释）
    content = re.sub(
        r'\s*<!--\s*=+\s*Tier 2:.*?-->\s*<div\s+id="icon-rail">\s*</div>',
        '\n',
        content,
        flags=re.DOTALL
    )

    # 2) 删除 mountIconRail() 调用
    content = re.sub(
        r'\s*mountIconRail\(\{[^}]*?\}\);',
        '',
        content,
        flags=re.DOTALL
    )

    # 3) 删除 icon-rail.js 引用
    content = re.sub(
        r'\s*<script\s+src="[^"]*_shared/icon-rail\.js"\s*></script>\s*\n?',
        '\n',
        content
    )

    # 4) 删除 "挂载共用页头" 注释（现在只剩 mountTabBar）
    content = re.sub(
        r'\s*//\s*挂载共用页头',
        '',
        content
    )

    # 5) 清理多余空行
    content = re.sub(r'\n{3,}', '\n\n', content)

    if content != original:
        file_path.write_text(content, encoding='utf-8')
        return True
    return False


def main():
    html_files = [f for f in BASE_DIR.rglob("*.html") if '_shared' not in str(f)]
    success = 0
    for f in sorted(html_files):
        if remove_icon_rail(f):
            print(f"  [OK]   {f.relative_to(BASE_DIR)}")
            success += 1
        else:
            print(f"  [SKIP] {f.relative_to(BASE_DIR)} (无变化)")

    print(f"\n完成：{success} 个文件已更新")


if __name__ == "__main__":
    main()