#!/usr/bin/env python3
"""
完整的公司分析流程（包含自动总结和归档）

使用方法:
  python3 analyze_and_summarize.py <stock_code> [--keep-original] [--skip-archive]
"""

import sys
import subprocess
from pathlib import Path


def run_analysis(stock_code):
    """运行公司分析"""
    print(f"\n{'='*80}")
    print(f"步骤 1/2: 运行公司分析")
    print(f"{'='*80}\n")

    result = subprocess.run(
        ['python3', 'analyze_company.py', stock_code],
        cwd=Path(__file__).parent
    )

    if result.returncode != 0:
        print("\n错误: 公司分析失败")
        return None

    # 查找最新的分析目录
    scripts_dir = Path(__file__).parent
    analysis_dirs = list(scripts_dir.glob(f'company_analysis_{stock_code}*'))

    if not analysis_dirs:
        print(f"\n错误: 未找到分析目录")
        return None

    # 返回最新的目录
    latest_dir = max(analysis_dirs, key=lambda p: p.stat().st_mtime)
    return latest_dir


def run_summarize_and_archive(analysis_dir, keep_original=True, skip_archive=False):
    """运行总结和归档"""
    if skip_archive:
        print(f"\n跳过归档步骤")
        return

    print(f"\n{'='*80}")
    print(f"步骤 2/2: 生成知识摘要和归档")
    print(f"{'='*80}\n")

    cmd = ['python3', 'summarize_and_archive.py', str(analysis_dir)]
    if not keep_original:
        cmd.append('--no-keep-original')

    result = subprocess.run(cmd, cwd=Path(__file__).parent)

    if result.returncode != 0:
        print("\n警告: 总结和归档过程出现错误")
        return False

    return True


def main():
    if len(sys.argv) < 2:
        print("\n完整的公司分析流程（包含自动总结和归档）\n")
        print("用法:")
        print("  python3 analyze_and_summarize.py <stock_code> [options]")
        print("\n选项:")
        print("  --keep-original    保留原始数据文件（默认）")
        print("  --no-keep-original 删除原始数据文件，仅保留压缩包")
        print("  --skip-archive     跳过归档步骤")
        print("\n示例:")
        print("  python3 analyze_and_summarize.py 09992")
        print("  python3 analyze_and_summarize.py 600519 --no-keep-original")
        print("  python3 analyze_and_summarize.py 00700 --skip-archive")
        print()
        sys.exit(0)

    stock_code = sys.argv[1]
    keep_original = '--no-keep-original' not in sys.argv
    skip_archive = '--skip-archive' in sys.argv

    print(f"\n{'='*80}")
    print(f"完整的公司分析流程")
    print(f"{'='*80}\n")
    print(f"公司代码: {stock_code}")
    print(f"保留原始文件: {'是' if keep_original else '否'}")
    print(f"跳过归档: {'是' if skip_archive else '否'}")
    print()

    # 步骤1: 运行分析
    analysis_dir = run_analysis(stock_code)
    if not analysis_dir:
        sys.exit(1)

    print(f"\n✓ 分析完成，目录: {analysis_dir}")

    # 步骤2: 总结和归档
    if run_summarize_and_archive(analysis_dir, keep_original, skip_archive):
        print(f"\n{'='*80}")
        print(f"全部完成!")
        print(f"{'='*80}\n")
        print(f"分析目录: {analysis_dir}")
        print(f"\n可以使用以下命令快速加载知识:")
        print(f"  python3 quick_learn.py load {stock_code}")
        print()
    else:
        print(f"\n分析已完成，但归档过程出现问题")
        print(f"可以手动运行: python3 summarize_and_archive.py {analysis_dir}")


if __name__ == '__main__':
    main()
