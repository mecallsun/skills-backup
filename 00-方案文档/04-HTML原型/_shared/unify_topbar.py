#!/usr/bin/env python3
"""
v2.12.12 统一所有页面的顶部品牌栏（Tier 1）样式
删除所有页面自定义的 .top-bar / .brand-* / .user-pill / .btn-exit 样式
让外联 _shared/layout-tab.css 统一规范生效
"""
import re
from pathlib import Path

BASE_DIR = Path(r"E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统\00-方案文档\04-HTML原型")

# 需要清理的 CSS 选择器（外联 layout-tab.css 已统一定义）
TARGET_SELECTORS = [
    r'\.top-bar\s*\{[^}]*\}',
    r'\.top-bar\s+\.brand-icon\s*\{[^}]*\}',
    r'\.top-bar\s+\.brand-text\s*\{[^}]*\}',
    r'\.top-bar\s+\.brand-version\s*\{[^}]*\}',
    r'\.top-bar\s+\.user-pill\s*\{[^}]*\}',
    r'\.top-bar\s+\.user-pill\s+i\s*\{[^}]*\}',
    r'\.top-bar\s+\.user-pill\s+span\s*\{[^}]*\}',
    r'\.top-bar\s+\.btn-exit\s*\{[^}]*\}',
    r'\.top-bar\s+\.btn-exit\s*:\s*hover\s*\{[^}]*\}',
    r'\.brand-icon\s*\{[^}]*\}',
    r'\.brand-text\s*\{[^}]*\}',
    r'\.brand-version\s*\{[^}]*\}',
    r'\.user-pill\s*\{[^}]*\}',
    r'\.user-pill\s+i\s*\{[^}]*\}',
    r'\.user-pill\s+span\s*\{[^}]*\}',
    r'\.btn-exit\s*\{[^}]*\}',
    r'\.btn-exit\s*:\s*hover\s*\{[^}]*\}',
]

# 模式：定位需要删除的 CSS 块（从选择器起始到闭合大括号）
def clean_css(content):
    """删除所有 .top-bar / .brand-* / .user-pill / .btn-exit 自定义 CSS 块"""
    removed = 0
    for selector_pattern in TARGET_SELECTORS:
        # 匹配完整的 CSS 块（包括空白行）
        matches = list(re.finditer(selector_pattern, content, re.DOTALL))
        for m in reversed(matches):  # 倒序删除避免索引错乱
            content = content[:m.start()] + content[m.end():]
            removed += 1
    # 清理连续空行
    content = re.sub(r'\n{3,}', '\n\n', content)
    return content, removed


def process_file(file_path: Path):
    content = file_path.read_text(encoding='utf-8')
    original = content

    # 只处理 <style> 块内的内容（保留 HTML 和 link/script 标签）
    style_blocks = list(re.finditer(r'<style[^>]*>([\s\S]*?)</style>', content))
    if not style_blocks:
        return False, 0

    new_content = content
    total_removed = 0
    for m in reversed(style_blocks):
        css = m.group(1)
        cleaned_css, removed = clean_css(css)
        total_removed += removed
        if removed > 0:
            new_content = new_content[:m.start(1)] + cleaned_css + new_content[m.end(1):]

    if total_removed > 0:
        file_path.write_text(new_content, encoding='utf-8')
        return True, total_removed
    return False, 0


def main():
    html_files = sorted([f for f in BASE_DIR.rglob("*.html") if '_shared' not in str(f)])
    print('=' * 60)
    print('v2.12.12 统一所有页面页头（Tier 1）样式')
    print('=' * 60)

    fixed = 0
    for f in html_files:
        ok, count = process_file(f)
        if ok:
            print(f'  [FIX] {f.relative_to(BASE_DIR)} (清理 {count} 个 CSS 块)')
            fixed += 1
        else:
            print(f'  [SKIP] {f.relative_to(BASE_DIR)} (无自定义 CSS)')

    print(f'\n完成：修复 {fixed} 个文件')


if __name__ == "__main__":
    main()