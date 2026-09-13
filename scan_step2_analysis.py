import sys
import os
os.environ['PYTHONIOENCODING'] = 'utf-8'
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

import json
from collections import Counter, defaultdict
import re

with open(r'C:\Users\Mecall\.skills-pool\scan_step2_result.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

print(f"Total SKILL.md files: {data['total_files']}")
print(f"Scan root: {data['scan_root']}")
print()
print("=== By Top Directory ===")
for k, v in data['by_top_dir'].items():
    print(f"  {k}: {v}")
print()

# 按 category 字段模式分类
cat_counter = Counter()
for f in data['files']:
    rel = f.replace(data['scan_root'], '').lstrip('\\/')
    m = re.search(r'(\d{2}-[^\\]+)', rel)
    if m:
        cat_counter[m.group(1)] += 1
    else:
        cat_counter['(no-category)'] += 1

print("=== By Category (NN-xxx pattern) ===")
for k, v in cat_counter.most_common():
    print(f"  {k}: {v}")

# 重复 name 检测
by_name = defaultdict(list)
for f in data['files']:
    parts = f.replace(data['scan_root'], '').replace('\\SKILL.md', '').lstrip('\\/').split('\\')
    name = parts[-1] if parts else '?'
    by_name[name].append(f)

dup_names = {n: paths for n, paths in by_name.items() if len(paths) > 1}
print()
print(f"=== Unique skill names: {len(by_name)} ===")
print(f"=== Duplicated names: {len(dup_names)} ===")
print()
print("=== Top Duplicates ===")
sorted_dups = sorted(dup_names.items(), key=lambda x: -len(x[1]))
for n, paths in sorted_dups[:50]:
    print(f"  [{len(paths)}] {n}")

# 保存分析结果
result = {
    'total': data['total_files'],
    'unique_names': len(by_name),
    'duplicated_names': len(dup_names),
    'by_top_dir': data['by_top_dir'],
    'by_category': dict(cat_counter),
    'top_duplicates': [(n, len(paths)) for n, paths in sorted_dups[:100]]
}
with open(r'C:\Users\Mecall\.skills-pool\scan_step2_analysis.json', 'w', encoding='utf-8') as fp:
    json.dump(result, fp, ensure_ascii=False, indent=2)
print()
print("Analysis saved to: scan_step2_analysis.json")