#!/usr/bin/env python3
"""
PDF流式转换器 - 将PDF转换为可搜索的文本文件

特点：
- 逐页流式处理，最小化内存占用
- 添加页码标记，便于定位
- 支持中英文财报
"""
import sys
from pathlib import Path
from typing import Dict, Optional

try:
    import PyPDF2
except ImportError:
    print("Warning: PyPDF2 not installed. Install with: pip install PyPDF2")
    PyPDF2 = None


class PDFStreamingConverter:
    """PDF流式转换器"""

    def convert_to_text(self, pdf_path: Path, output_path: Optional[Path] = None) -> Dict:
        """
        流式转换PDF为文本文件

        Args:
            pdf_path: PDF文件路径
            output_path: 输出文本文件路径，默认与PDF同目录同名.txt

        Returns:
            转换结果信息
        """
        if PyPDF2 is None:
            return {'success': False, 'error': 'PyPDF2 not installed'}

        if not pdf_path.exists():
            return {'success': False, 'error': f'PDF file not found: {pdf_path}'}

        # 默认输出路径
        if output_path is None:
            output_path = pdf_path.with_suffix('.txt')

        try:
            with open(pdf_path, 'rb') as pdf_file:
                pdf_reader = PyPDF2.PdfReader(pdf_file)
                total_pages = len(pdf_reader.pages)

                with open(output_path, 'w', encoding='utf-8') as text_file:
                    # 写入文件头
                    text_file.write(f"# Source: {pdf_path.name}\n")
                    text_file.write(f"# Total Pages: {total_pages}\n")
                    text_file.write("=" * 60 + "\n\n")

                    # 逐页流式处理
                    for page_num in range(total_pages):
                        try:
                            page = pdf_reader.pages[page_num]
                            text = page.extract_text()

                            # 写入页码标记
                            text_file.write(f"\n--- Page {page_num + 1} ---\n\n")

                            if text:
                                # 清理文本并写入
                                cleaned_text = self._clean_text(text)
                                text_file.write(cleaned_text)
                                text_file.write("\n")

                        except Exception as e:
                            text_file.write(f"[Error extracting page {page_num + 1}: {e}]\n")
                            continue

            return {
                'success': True,
                'total_pages': total_pages,
                'output_file': str(output_path),
                'output_size': output_path.stat().st_size
            }

        except Exception as e:
            return {'success': False, 'error': str(e)}

    def _clean_text(self, text: str) -> str:
        """清理提取的文本"""
        if not text:
            return ""

        # 移除多余空白行，但保留段落结构
        lines = text.split('\n')
        cleaned_lines = []
        prev_empty = False

        for line in lines:
            stripped = line.strip()
            if stripped:
                cleaned_lines.append(line)
                prev_empty = False
            elif not prev_empty:
                cleaned_lines.append("")
                prev_empty = True

        return '\n'.join(cleaned_lines)


def convert_all_pdfs(reports_dir: Path, skip_existing: bool = True) -> Dict[str, Dict]:
    """
    转换目录下所有PDF文件

    Args:
        reports_dir: 年报PDF目录
        skip_existing: 是否跳过已存在的文本文件（默认True）

    Returns:
        转换结果字典 {文件名: 结果}
    """
    if not reports_dir.exists():
        print(f"目录不存在: {reports_dir}")
        return {}

    converter = PDFStreamingConverter()
    results = {}

    pdf_files = list(reports_dir.glob('*.pdf'))
    if not pdf_files:
        print(f"未找到PDF文件: {reports_dir}")
        return {}

    print(f"\n找到 {len(pdf_files)} 个PDF文件")
    
    skipped = 0
    converted = 0

    for pdf_path in sorted(pdf_files):
        txt_path = pdf_path.with_suffix('.txt')
        
        # 检查是否跳过已存在的文本文件
        if skip_existing and txt_path.exists() and txt_path.stat().st_size > 0:
            skipped += 1
            results[pdf_path.name] = {
                'success': True, 
                'skipped': True,
                'output_file': str(txt_path),
                'output_size': txt_path.stat().st_size
            }
            continue
        
        print(f"  转换: {pdf_path.name}")
        result = converter.convert_to_text(pdf_path)

        if result['success']:
            size_kb = result['output_size'] / 1024
            print(f"    -> {result['output_file']} ({size_kb:.1f} KB, {result['total_pages']} pages)")
            converted += 1
        else:
            print(f"    -> 失败: {result.get('error', 'Unknown error')}")

        results[pdf_path.name] = result

    if skipped > 0:
        print(f"  跳过已存在: {skipped} 个文件")
    print(f"  新转换: {converted} 个文件")

    return results


def main(reports_dir: Path):
    """主函数"""
    print("\n" + "=" * 60)
    print("PDF流式转换器")
    print("=" * 60)

    results = convert_all_pdfs(reports_dir)

    # 统计
    success_count = sum(1 for r in results.values() if r.get('success'))
    print(f"\n转换完成: {success_count}/{len(results)} 成功")

    return results


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: python pdf_to_text.py <reports_directory>")
        print("Example: python pdf_to_text.py ./company_analysis_600519/raw_data/annual_reports")
        sys.exit(1)

    reports_dir = Path(sys.argv[1])
    main(reports_dir)
