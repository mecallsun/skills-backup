#!/usr/bin/env python3
"""
注入脚本：为已迁移但缺少 mountIconRail/TabManager.open 调用的页面补充初始化代码
"""
import re
from pathlib import Path

BASE_DIR = Path(r"E:\AI工作目录\AI编程开发\JINGE开发\宿舍管理系统\00-方案文档\04-HTML原型")

MODULE_RULES = [
    ("personnel/list.html",        "personnel",     "人员清单",     "bi-people-fill",    "personnel/list.html"),
    ("personnel/create.html",      "personnel",     "新增人员",     "bi-people-fill",    "personnel/create.html"),
    ("personnel/edit.html",        "personnel",     "编辑人员",     "bi-people-fill",    "personnel/edit.html"),
    ("personnel/import.html",      "personnel",     "导入人员",     "bi-people-fill",    "personnel/import.html"),
    ("billing/standards.html",     "billing",       "费用标准",     "bi-cash-stack",     "billing/standards.html"),
    ("billing/standard-form.html", "billing",       "费用标准表单", "bi-cash-stack",     "billing/standard-form.html"),
    ("billing/dorm-bills.html",    "dorm-bills",    "宿舍账单",     "bi-receipt",        "billing/dorm-bills.html"),
    ("billing/employee-bills.html","employee-bills","员工账单",     "bi-wallet2",        "billing/employee-bills.html"),
    ("booking/index.html",         "booking",       "办理登记",     "bi-clipboard-check","booking/index.html"),
    ("booking/check-in.html",      "booking",       "办理入住",     "bi-clipboard-check","booking/check-in.html"),
    ("booking/check-out.html",     "booking",       "办理退房",     "bi-clipboard-check","booking/check-out.html"),
    ("booking/edit.html",          "booking",       "修改登记",     "bi-clipboard-check","booking/edit.html"),
    ("dorms/list.html",            "dorms",         "宿舍管理",     "bi-building",       "dorms/list.html"),
    ("dorms/create.html",          "dorms",         "新增宿舍",     "bi-building",       "dorms/create.html"),
    ("dorms/edit.html",            "dorms",         "编辑宿舍",     "bi-building",       "dorms/edit.html"),
    ("dorms/details.html",         "dorms",         "宿舍详情",     "bi-building",       "dorms/details.html"),
    ("dorms/history.html",         "dorms",         "宿舍历史",     "bi-building",       "dorms/history.html"),
    ("meter/index.html",           "meter",         "抄表记录",     "bi-clipboard-data", "meter/index.html"),
    ("meter/edit.html",            "meter",         "编辑抄表",     "bi-clipboard-data", "meter/edit.html"),
    ("meter/detail.html",          "meter",         "抄表明细",     "bi-clipboard-data", "meter/detail.html"),
    ("meter/entry.html",           "meter",         "手动录入",     "bi-clipboard-data", "meter/entry.html"),
    ("meter/import.html",          "meter",         "批量导入",     "bi-clipboard-data", "meter/import.html"),
    ("basics/index.html",          "basics",        "基础资料",     "bi-database",       "basics/index.html"),
    ("settings/index.html",        "settings",      "系统设置",     "bi-gear",           "settings/index.html"),
    ("index.html",                 "index",         "首页",         "bi-speedometer2",   "index.html"),
]


def get_module_rule(file_path: Path):
    rel = str(file_path.relative_to(BASE_DIR)).replace("\\", "/")
    for url, mod, title, icon, url_full in MODULE_RULES:
        if rel == url:
            return mod, title, icon, url_full, "" if file_path.parent == BASE_DIR else ".."
    return None


def inject_file(file_path: Path):
    rule = get_module_rule(file_path)
    if not rule:
        return False, "无匹配规则"

    module, title, icon, url, base_path = rule
    content = file_path.read_text(encoding='utf-8')

    # 检查是否已有 init 脚本
    if 'mountIconRail({' in content:
        return False, "已挂载"

    # 检查是否已引用共享 CSS
    if '_shared/layout-tab.css' not in content:
        # 注入 head CSS
        content = content.replace('</head>', f'    <link rel="stylesheet" href="{base_path}/_shared/layout-tab.css">\n</head>')

    # 检查是否已引用共享 JS
    if '_shared/storage-keys.js' not in content:
        scripts = f'''    <script src="{base_path}/_shared/storage-keys.js"></script>
    <script src="{base_path}/_shared/icon-rail.js"></script>
    <script src="{base_path}/_shared/tab-bar.js"></script>
'''
        # 在 </body> 之前插入
        init_code = f'''
        document.addEventListener('DOMContentLoaded', function() {{
            const pageMeta = {{ title: '{title}', module: '{module}', icon: '{icon}', url: '{url}' }};
            mountIconRail({{ basePath: '{base_path}', currentModule: '{module}' }});
            mountTabBar({{ basePath: '{base_path}', currentUrl: '{url}' }});
            TabManager.open(pageMeta);
        }});
'''
        content = re.sub(
            r'(\s*</body>)',
            f'\n{scripts}    <script>\n{init_code}    </script>\\1',
            content,
            count=1
        )

    # 同时移除旧的 renderNav() 调用（如果存在）
    content = re.sub(
        r"\s*document\.getElementById\(['\"]nav['\"]\)\.innerHTML\s*=\s*renderNav\([^)]*\);?",
        '',
        content
    )
    content = re.sub(
        r"\s*\$\(['\"]#nav['\"]\)\.html\(renderNav\([^)]*\)\);?",
        '',
        content
    )

    file_path.write_text(content, encoding='utf-8')
    return True, "已注入"


def main():
    html_files = [f for f in BASE_DIR.rglob("*.html") if '_shared' not in str(f)]
    success = 0
    for f in sorted(html_files):
        ok, msg = inject_file(f)
        flag = "OK   " if ok else "SKIP "
        print(f"  [{flag}] {f.relative_to(BASE_DIR)} → {msg}")
        if ok:
            success += 1

    print(f"\n完成：注入 {success} 个文件")


if __name__ == "__main__":
    main()