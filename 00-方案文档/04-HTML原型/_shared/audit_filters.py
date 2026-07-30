#!/usr/bin/env python3
"""
筛选区审计脚本：
1) 找出所有包含筛选区（form.filter-row / form.filter-card / form.row.g-3）的页面
2) 检查每个筛选项是否符合强制一行排列规则
3) 输出违规清单
"""
import re
from pathlib import Path
from collections import defaultdict

BASE_DIR = Path(r"E:\AI工作目录\AI编程开发\JINGE开发\住宿管理系统\00-方案文档\04-HTML原型")

# 检查项
CHECKS = {
    'has_filter':           lambda c, fl: bool(fl),  # 有筛选区
    'has_flex_nowrap':      lambda c, fl: 'flex-nowrap' in fl,  # 必须有 flex-nowrap
    'uses_filter_card':     lambda c, fl: 'filter-card' in fl,  # 必须使用 filter-card 包裹
    'uses_filter_row':      lambda c, fl: 'filter-row' in fl,   # 必须使用 filter-row 类
    'uses_filter_btn':      lambda c, fl: 'filter-btn' in fl,   # 必须使用 filter-btn 类
    'has_query_btn':        lambda c, fl: '查询' in fl,
    'has_reset_btn':        lambda c, fl: '重置' in fl,
    'no_wrap_break':        lambda c, fl: 'flex-wrap: wrap' not in fl and 'wrap' not in fl.lower().replace('nowrap', ''),
    'no_col_md_n':          lambda c, fl: not re.search(r'col-md-\d', fl),  # 禁止 col-md 固定列宽
}


def extract_filter_region(content):
    """提取所有筛选区内容"""
    filters = []
    # 模式1：filter-card 包裹
    for m in re.finditer(r'<form[^>]*filter-card[^>]*>([\s\S]*?)</form>', content):
        filters.append(('filter-card', m.group(0), m.group(1)))
    # 模式2：filter-row（不一定在 form 中）
    if not filters:
        for m in re.finditer(r'<form[^>]*filter-row[^>]*>([\s\S]*?)</form>', content):
            filters.append(('filter-row', m.group(0), m.group(1)))
    # 模式3：旧的 row.g-3 形式
    if not filters:
        for m in re.finditer(r'<form[^>]*class="row[^"]*"[^>]*>([\s\S]*?)</form>', content):
            if 'filter' in m.group(0) or '筛选' in m.group(0):
                filters.append(('legacy-row', m.group(0), m.group(1)))
    return filters


def audit_file(file_path: Path):
    rel = str(file_path.relative_to(BASE_DIR)).replace("\\", "/")
    content = file_path.read_text(encoding='utf-8')

    filters = extract_filter_region(content)
    if not filters:
        return None

    results = []
    for idx, (kind, full, inner) in enumerate(filters):
        checks = {}
        for name, fn in CHECKS.items():
            try:
                checks[name] = fn(content, inner)
            except Exception as e:
                checks[name] = False

        results.append({
            'file': rel,
            'kind': kind,
            'idx': idx + 1,
            'checks': checks,
            'inner': inner[:200]
        })

    return results


def main():
    html_files = sorted([f for f in BASE_DIR.rglob("*.html") if '_shared' not in str(f)])

    all_results = []
    for f in html_files:
        result = audit_file(f)
        if result:
            all_results.extend(result)

    print('=' * 70)
    print(f'筛选区审计报告（共 {len(all_files := [f for f in html_files if audit_file(f)])} 个文件含筛选区）')
    print('=' * 70)

    # 按文件汇总
    by_file = defaultdict(list)
    for r in all_results:
        by_file[r['file']].append(r)

    # 统计违规
    violations = defaultdict(list)
    total_filters = len(all_results)
    pass_count = 0

    for r in all_results:
        all_ok = all(r['checks'].values())
        if all_ok:
            pass_count += 1
        else:
            for check_name, ok in r['checks'].items():
                if not ok:
                    violations[check_name].append(r['file'])

    print(f'\n筛选区总数：{total_filters}')
    print(f'完全合规：{pass_count}')
    print(f'存在违规：{total_filters - pass_count}')

    print(f'\n--- 违规统计 ---')
    for check_name, files in violations.items():
        print(f'  ❌ {check_name}: {len(files)} 处')
        for f in files[:5]:
            print(f'      - {f}')
        if len(files) > 5:
            print(f'      ... 共 {len(files)} 处')

    print(f'\n--- 各文件详细情况 ---')
    for file, results in by_file.items():
        for r in results:
            all_ok = all(r['checks'].values())
            status = '✅' if all_ok else '❌'
            failed_checks = [k for k, v in r['checks'].items() if not v]
            detail = f' ({", ".join(failed_checks)})' if failed_checks else ''
            print(f'  {status} {file}#{r["idx"]} [{r["kind"]}]{detail}')


if __name__ == "__main__":
    main()