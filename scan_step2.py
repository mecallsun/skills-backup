import glob, os, json
from collections import defaultdict

root = r'E:\AI工作目录'
files = glob.glob(os.path.join(root, '**', 'SKILL.md'), recursive=True)

print(f'Total: {len(files)}')

# 按顶层目录分组
by_top = defaultdict(list)
for f in files:
    rel = os.path.relpath(f, root)
    top = rel.split(os.sep)[0] if os.sep in rel else '(root)'
    by_top[top].append(f)

# 详细信息
result = {
    'scan_root': root,
    'scan_time': '2026-08-11',
    'total_files': len(files),
    'by_top_dir': {k: len(v) for k, v in sorted(by_top.items(), key=lambda x: -len(x[1]))},
    'files': files
}

out = r'C:\Users\Mecall\.skills-pool\scan_step2_result.json'
with open(out, 'w', encoding='utf-8') as fp:
    json.dump(result, fp, ensure_ascii=False, indent=2)

print(f'Saved to: {out}')
print()
print('=== By Top Directory ===')
for top, cnt in sorted(by_top.items(), key=lambda x: -len(x[1])):
    print(f'  {top}: {cnt}')
print()
print('=== Sample Files ===')
for f in files[:5]:
    print(f'  {f}')
print('  ...')
for f in files[-5:]:
    print(f'  {f}')