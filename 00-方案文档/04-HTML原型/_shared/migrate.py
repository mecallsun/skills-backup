#!/usr/bin/env python3
"""
HTML 原型批量重构脚本 v2 — v2.12.0 共用页头 + Tab 页签
更宽松的匹配策略：支持多种旧导航结构
"""

import os
import re
import sys
from pathlib import Path

BASE_DIR = Path(r"E:\AI工作目录\AI编程开发\JINGE开发\住宿管理系统\00-方案文档\04-HTML原型")

# 模块识别规则
MODULE_RULES = [
    ("personnel/list.html",        "personnel",     "人员清单",     "bi-people-fill",    "personnel/list.html"),
    ("personnel/create.html",      "personnel",     "新增人员",     "bi-people-fill",    "personnel/create.html"),
    ("personnel/edit.html",        "personnel",     "编辑人员",     "bi-people-fill",    "personnel/edit.html"),
    ("personnel/import.html",      "personnel",     "导入人员",     "bi-people-fill",    "personnel/import.html"),
    ("billing/standards.html",     "billing",       "费用标准",     "bi-cash-stack",     "billing/standards.html"),
    ("billing/standard-form.html", "billing",       "费用标准表单", "bi-cash-stack",     "billing/standard-form.html"),
    ("billing/dorm-bills.html",    "dorm-bills",    "住宿账单",     "bi-receipt",        "billing/dorm-bills.html"),
    ("billing/employee-bills.html","employee-bills","员工账单",     "bi-wallet2",        "billing/employee-bills.html"),
    ("booking/index.html",         "booking",       "办理登记",     "bi-clipboard-check","booking/index.html"),
    ("booking/check-in.html",      "booking",       "办理入住",     "bi-clipboard-check","booking/check-in.html"),
    ("booking/check-out.html",     "booking",       "办理退房",     "bi-clipboard-check","booking/check-out.html"),
    ("booking/edit.html",          "booking",       "修改登记",     "bi-clipboard-check","booking/edit.html"),
    ("dorms/list.html",            "dorms",         "住宿管理",     "bi-building",       "dorms/list.html"),
    ("dorms/create.html",          "dorms",         "新增住宿",     "bi-building",       "dorms/create.html"),
    ("dorms/edit.html",            "dorms",         "编辑住宿",     "bi-building",       "dorms/edit.html"),
    ("dorms/details.html",         "dorms",         "住宿详情",     "bi-building",       "dorms/details.html"),
    ("dorms/history.html",         "dorms",         "住宿历史",     "bi-building",       "dorms/history.html"),
    ("meter/index.html",           "meter",         "智能抄表",     "bi-clipboard-data", "meter/index.html"),
    ("meter/edit.html",            "meter",         "编辑抄表",     "bi-clipboard-data", "meter/edit.html"),
    ("meter/detail.html",          "meter",         "抄表明细",     "bi-clipboard-data", "meter/detail.html"),
    ("meter/entry.html",           "meter",         "手动录入",     "bi-clipboard-data", "meter/entry.html"),
    ("meter/import.html",          "meter",         "批量导入",     "bi-clipboard-data", "meter/import.html"),
    ("basics/index.html",          "basics",        "基础资料",     "bi-database",       "basics/index.html"),
    ("settings/index.html",        "settings",      "系统设置",     "bi-gear",           "settings/index.html"),
    ("index.html",                 "index",         "首页",         "bi-speedometer2",   "index.html"),
]

# 新布局顶部模板
def new_top_html(base_path):
    return f'''    <!-- ========== Tier 1: 顶部品牌栏 (48px) ========== -->
    <div class="top-bar">
        <div class="d-flex justify-content-between align-items-center h-100">
            <div class="d-flex align-items-center">
                <i class="bi bi-droplet-half brand-icon"></i>
                <span class="brand-text">金智住宿管理系统</span>
                <span class="brand-version">v2.12.0</span>
            </div>
            <div class="d-flex align-items-center gap-2">
                <span class="user-pill">
                    <i class="bi bi-person-circle"></i>
                    <span>管理员</span>
                </span>
                <a href="#" class="btn-exit" title="退出登录" aria-label="退出登录">
                    <i class="bi bi-box-arrow-right"></i> 退出
                </a>
            </div>
        </div>
    </div>

    <!-- ========== Tier 2: 紧凑型图标导航条 (56px) ========== -->
    <div id="icon-rail"></div>

    <!-- ========== Tier 3: Tab 页签栏 (40px) ========== -->
    <div id="tab-bar"></div>

    <!-- ========== Tier 4: 页面内容区 ========== -->
    <div class="page-content">'''


def new_head_extra(base_path):
    return f'    <link rel="stylesheet" href="{base_path}/_shared/layout-tab.css">'


def new_scripts(base_path):
    return f'''    <script src="{base_path}/_shared/storage-keys.js"></script>
    <script src="{base_path}/_shared/icon-rail.js"></script>
    <script src="{base_path}/_shared/tab-bar.js"></script>'''


def init_script(module, title, icon, url, base_path):
    return f'''        document.addEventListener('DOMContentLoaded', function() {{
            const pageMeta = {{ title: '{title}', module: '{module}', icon: '{icon}', url: '{url}' }};
            mountIconRail({{ basePath: '{base_path}', currentModule: '{module}' }});
            mountTabBar({{ basePath: '{base_path}', currentUrl: '{url}' }});
            TabManager.open(pageMeta);
        }});'''


def migrate_file(file_path: Path):
    rel = file_path.relative_to(BASE_DIR)
    rel_str = str(rel).replace("\\", "/")
    is_root = (file_path.parent == BASE_DIR)

    # 查找匹配的模块规则
    rule = None
    for url, mod, title, icon, url_full in MODULE_RULES:
        if rel_str == url:
            rule = (mod, title, icon, url_full)
            break

    if not rule:
        print(f"  [SKIP] {rel_str}: 无匹配模块规则")
        return False

    module, title, icon, url = rule
    base_path = "" if is_root else ".."

    content = file_path.read_text(encoding='utf-8')

    # 检测已迁移状态：包含 mountIconRail 调用说明已迁移
    if 'mountIconRail({' in content and '_shared/icon-rail.js' in content:
        print(f"  [SKIP] {rel_str}: 已迁移，跳过")
        return True

    # 1) 在 </head> 之前注入 layout-tab.css
    if '_shared/layout-tab.css' not in content:
        content = content.replace(
            '</head>',
            f'{new_head_extra(base_path)}\n</head>'
        )

    # 2) 替换 body 中的旧导航结构
    # 策略：找到 body 内容，从 <body> 后开始，到 <nav class="menu-bar">...</nav> 结束的位置
    #      注意要保留 <body> 标签之后的换行和缩进

    body_match = re.search(r'<body>(.*?)</body>', content, re.DOTALL)
    if not body_match:
        print(f"  [WARN] {rel_str}: 未找到 <body>")
        return False

    body_content = body_match.group(1)

    # 找到导航起始位置（关键锚点）—— 支持多种格式：
    # 1) <div class="top-bar">（旧版）
    # 2) <div id="nav"></div>（renderNav 占位符）
    # 3) <nav class="navbar ...">（Bootstrap navbar）
    # 4) <!-- 顶部品牌栏 -->

    nav_patterns = [
        r'<div class="top-bar">',
        r'<div id="nav">',
        r'<nav class="navbar',
        r'<nav class="menu-bar">',
        r'<!--\s*顶部品牌栏'
    ]

    start_pos = -1
    for pat in nav_patterns:
        m = re.search(pat, body_content)
        if m:
            start_pos = m.start()
            break

    if start_pos == -1:
        print(f"  [WARN] {rel_str}: 未找到旧导航结构")
        return False

    # 找到 nav.menu-bar 的结束位置
    nav_match = re.search(r'<nav class="menu-bar">.*?</nav>', body_content, re.DOTALL)

    if nav_match:
        end_pos = nav_match.end()
    else:
        # 没有 nav 菜单（表单/详情页可能没有），找下一个主要内容 div
        next_div = re.search(r'\n\s*<div class="(container-fluid|page-content|container)', body_content[start_pos:])
        if next_div:
            end_pos = start_pos + next_div.start()
        else:
            # 兜底：找第一个 h1 或 form
            h1_match = re.search(r'\n\s*<(h1|form|div class="container)', body_content[start_pos:])
            if h1_match:
                end_pos = start_pos + h1_match.start()
            else:
                end_pos = start_pos

    new_body = body_content[:start_pos] + new_top_html(base_path) + body_content[end_pos:]

    # 3) 移除 <div class="container-fluid px-4 py-3"> 这种旧包裹
    new_body = re.sub(
        r'<div class="container-fluid px-4 py-3">\s*\n?',
        '',
        new_body,
        count=1
    )

    # 4) 在 </body> 之前注入新脚本
    if '_shared/storage-keys.js' not in new_body:
        new_body = re.sub(
            r'(\s*</body>)',
            f'\n{new_scripts(base_path)}\n    {init_script(module, title, icon, url, base_path)}\\1',
            new_body,
            count=1
        )

    # 重新组装
    content = content[:body_match.start(1)] + new_body + content[body_match.end(1):]
    file_path.write_text(content, encoding='utf-8')
    print(f"  [OK]   {rel_str}")
    return True


def main():
    print("=" * 60)
    print("HTML 原型批量重构脚本 v2 — v2.12.0 共用页头 + Tab 页签")
    print("=" * 60)

    html_files = sorted(BASE_DIR.rglob("*.html"))
    # 排除 _shared 目录
    html_files = [f for f in html_files if '_shared' not in str(f)]
    print(f"\n发现 {len(html_files)} 个 HTML 文件\n")

    success = 0
    failed = 0
    for f in html_files:
        try:
            result = migrate_file(f)
            if result:
                success += 1
            else:
                failed += 1
        except Exception as e:
            print(f"  [ERR]  {f.relative_to(BASE_DIR)}: {e}")
            failed += 1

    print(f"\n完成：成功 {success} 个，失败 {failed} 个")


if __name__ == "__main__":
    main()