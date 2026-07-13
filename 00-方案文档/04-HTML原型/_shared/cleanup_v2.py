#!/usr/bin/env python3
"""
v2.12.2 适配脚本：
- 移除所有页面的 TabManager.open(pageMeta) 调用
- 简化 mountTabBar 调用（不再需要传 currentModule）
- mountTabBar({ basePath: '..', currentUrl: 'dorms/list.html' })
- mountIconRail 仍保留 currentModule（用于图标高亮）
"""
import re
from pathlib import Path

BASE_DIR = Path(r"E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统\00-方案文档\04-HTML原型")

# 模块识别规则
MODULE_RULES = [
    ("personnel/list.html",        "personnel",     "personnel/list.html"),
    ("personnel/create.html",      "personnel",     "personnel/create.html"),
    ("personnel/edit.html",        "personnel",     "personnel/edit.html"),
    ("personnel/import.html",      "personnel",     "personnel/import.html"),
    ("billing/standards.html",     "billing",       "billing/standards.html"),
    ("billing/standard-form.html", "billing",       "billing/standard-form.html"),
    ("billing/dorm-bills.html",    "dorm-bills",    "billing/dorm-bills.html"),
    ("billing/employee-bills.html","employee-bills","billing/employee-bills.html"),
    ("booking/index.html",         "booking",       "booking/index.html"),
    ("booking/check-in.html",      "booking",       "booking/check-in.html"),
    ("booking/check-out.html",     "booking",       "booking/check-out.html"),
    ("booking/edit.html",          "booking",       "booking/edit.html"),
    ("dorms/list.html",            "dorms",         "dorms/list.html"),
    ("dorms/create.html",          "dorms",         "dorms/create.html"),
    ("dorms/edit.html",            "dorms",         "dorms/edit.html"),
    ("dorms/details.html",         "dorms",         "dorms/details.html"),
    ("dorms/history.html",         "dorms",         "dorms/history.html"),
    ("meter/index.html",           "meter",         "meter/index.html"),
    ("meter/edit.html",            "meter",         "meter/edit.html"),
    ("meter/detail.html",          "meter",         "meter/detail.html"),
    ("meter/entry.html",           "meter",         "meter/entry.html"),
    ("meter/import.html",          "meter",         "meter/import.html"),
    ("basics/index.html",          "basics",        "basics/index.html"),
    ("settings/index.html",        "settings",      "settings/index.html"),
    ("index.html",                 "index",         "index.html"),
]


def get_module_rule(file_path: Path):
    rel = str(file_path.relative_to(BASE_DIR)).replace("\\", "/")
    for url, mod, url_full in MODULE_RULES:
        if rel == url:
            return mod, url_full, "" if file_path.parent == BASE_DIR else ".."
    return None


def simplify_file(file_path: Path):
    rule = get_module_rule(file_path)
    if not rule:
        return False, "无匹配规则"

    module, url, base_path = rule
    content = file_path.read_text(encoding='utf-8')

    # 检查是否已是 v2.12.2 格式（无 TabManager.open）
    if 'TabManager.open' not in content and 'mountTabBar({ basePath' in content and 'currentUrl:' in content:
        return False, "已为 v2.12.2 格式"

    # 替换 mountTabBar 调用（简化 currentUrl）
    new_mount_tab = f'''mountTabBar({{ basePath: '{base_path}', currentUrl: '{url}' }});'''

    # 模式1: 包含 pageMeta 的标准格式
    pattern1 = re.compile(
        r'mountTabBar\(\{[^}]*?\}\);',
        re.DOTALL
    )
    content = pattern1.sub(new_mount_tab, content)

    # 模式2: 移除 pageMeta 定义和 TabManager.open 调用
    # 移除 const pageMeta = {...};
    content = re.sub(
        r'\s*const\s+pageMeta\s*=\s*\{[^}]*?\};',
        '',
        content,
        flags=re.DOTALL
    )

    # 移除 TabManager.open(pageMeta) 调用
    content = re.sub(
        r'\s*//\s*打开/激活当前页 Tab\s*\n?\s*TabManager\.open\(pageMeta\);?',
        '',
        content
    )
    content = re.sub(
        r'\s*TabManager\.open\(pageMeta\);?',
        '',
        content
    )

    # 清理可能的多余空行
    content = re.sub(r'\n{3,}', '\n\n', content)

    file_path.write_text(content, encoding='utf-8')
    return True, "已简化为 v2.12.2"


def main():
    html_files = [f for f in BASE_DIR.rglob("*.html") if '_shared' not in str(f)]
    success = 0
    for f in sorted(html_files):
        ok, msg = simplify_file(f)
        flag = "OK   " if ok else "SKIP "
        print(f"  [{flag}] {f.relative_to(BASE_DIR)} → {msg}")
        if ok:
            success += 1

    print(f"\n完成：简化 {success} 个文件")


if __name__ == "__main__":
    main()