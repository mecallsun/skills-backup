#!/usr/bin/env python3
"""
v2 筛选区审计脚本（更精确）：
1) 找出筛选区所在位置（form 父级）
2) 检查父容器 .filter-card、form 上的 .filter-row/flex-nowrap
3) 输出违规清单 + 修复建议
"""
import re
from pathlib import Path
from collections import defaultdict

BASE_DIR = Path(r"E:\AI工作目录\AI编程开发\JINGE开发\住宿管理系统\00-方案文档\04-HTML原型")


def find_filters(content):
    """找出所有筛选 form 及其父容器"""
    filters = []

    # 模式 A：<div class="filter-card..."><form class="...">...</form></div>
    for m in re.finditer(r'<div\s+class="filter-card[^"]*"[^>]*>\s*<form([^>]*)>([\s\S]*?)</form>', content):
        form_attrs = m.group(1)
        inner = m.group(2)
        start = m.start()
        filters.append({
            'kind': 'filter-card-wrapper',
            'start': start,
            'form_attrs': form_attrs,
            'inner': inner,
            'has_filter_card_wrapper': True
        })

    # 模式 B：<form class="..."> 单独存在（非 filter-card 包裹）
    if not filters:
        for m in re.finditer(r'<form\s+([^>]*?)>([\s\S]*?)</form>', content):
            form_attrs = m.group(1) or ''
            inner = m.group(2) or ''
            # 只审计含有筛选关键词的 form
            if 'filter' in form_attrs.lower() or any(k in inner for k in ['筛选', '查询', '重置']):
                filters.append({
                    'kind': 'standalone-form',
                    'start': m.start(),
                    'form_attrs': form_attrs,
                    'inner': inner,
                    'has_filter_card_wrapper': False
                })

    return filters


def audit_filter(filt):
    """审计单个筛选区"""
    form_attrs = filt['form_attrs']
    inner = filt['inner']

    issues = []

    # 1. 必须有 flex-nowrap
    if 'flex-nowrap' not in form_attrs:
        issues.append('缺少 flex-nowrap（必须强制不换行）')

    # 2. 必须使用 .filter-row 类
    if 'filter-row' not in form_attrs:
        issues.append('缺少 filter-row 类')

    # 3. 必须有 .filter-card 包裹
    if not filt['has_filter_card_wrapper']:
        issues.append('缺少 .filter-card 包裹')

    # 4. 检查按钮必须使用 .filter-btn 类
    if 'filter-btn' not in inner:
        issues.append('按钮缺少 .filter-btn 类（无法应用 90×38px 固定尺寸）')

    # 5. 禁止 col-md-N 固定列宽
    if re.search(r'col-md-\d+', inner):
        issues.append('禁止 col-md-N 固定列宽（应使用 .filter-item）')

    # 6. 必须有查询按钮
    if '查询' not in inner:
        issues.append('缺少"查询"按钮')

    # 7. 必须有重置按钮
    if '重置' not in inner:
        issues.append('缺少"重置"按钮')

    # 8. 检查 .filter-item 是否使用（替代 col-md-*）
    filter_items = inner.count('class="filter-item')
    if filter_items == 0 and 'col-md-' in inner:
        issues.append('使用了 col-md-* 列而非 .filter-item')

    return issues


def main():
    html_files = sorted([f for f in BASE_DIR.rglob("*.html") if '_shared' not in str(f)])

    all_filters = {}
    for f in html_files:
        rel = str(f.relative_to(BASE_DIR)).replace("\\", "/")
        content = f.read_text(encoding='utf-8')
        filters = find_filters(content)
        if filters:
            all_filters[rel] = filters

    print('=' * 70)
    print(f'筛选区审计 v2（共 {len(all_filters)} 个文件含筛选区）')
    print('=' * 70)

    total_filters = sum(len(v) for v in all_filters.values())
    total_violations = 0

    for file, filters in all_filters.items():
        for idx, filt in enumerate(filters, 1):
            issues = audit_filter(filt)
            if not issues:
                status = '[PASS]'
            else:
                status = '[FAIL]'
                total_violations += 1

            print(f'\n{status} {file}#{idx}')
            print(f'  form attrs: {filt["form_attrs"][:100]}...')
            if issues:
                for issue in issues:
                    print(f'  - {issue}')
            else:
                print(f'  - 所有 8 项检查通过')

    print('\n' + '=' * 70)
    print(f'总计：{total_filters} 个筛选区，{total_filters - total_violations} 个合规，{total_violations} 个违规')
    print('=' * 70)


if __name__ == "__main__":
    main()