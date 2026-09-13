#!/usr/bin/env python3
"""
视频/音频分析脚本 — video-analysis skill
功能：自动提取视频截帧 + 语音转文字 + 深度内容理解
依赖：ffmpeg (已在 PATH), openai-whisper (已安装)
"""
import sys
import io

# Windows 控制台 UTF-8 修复
if sys.platform == 'win32':
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

import os
import sys
import json
import shutil
import subprocess
import tempfile
from pathlib import Path
from datetime import datetime


def check_dependencies() -> tuple[bool, dict]:
    """检查依赖是否满足"""
    deps = {
        'ffmpeg': shutil.which('ffmpeg'),
        'python_whisper': True  # whisper 是 Python 包，导入测试
    }
    try:
        import whisper
    except ImportError:
        deps['python_whisper'] = False

    return all(deps.values()), deps


def get_video_info(video_path: str) -> dict:
    """获取视频基本信息"""
    cmd = [
        'ffprobe', '-v', 'quiet', '-print_format', 'json',
        '-show_format', '-show_streams', video_path
    ]
    try:
        result = subprocess.run(cmd, capture_output=True, text=True, check=True)
        data = json.loads(result.stdout)

        video_stream = next((s for s in data.get('streams', []) if s.get('codec_type') == 'video'), {})
        audio_stream = next((s for s in data.get('streams', []) if s.get('codec_type') == 'audio'), {})
        fmt = data.get('format', {})

        return {
            'duration': float(fmt.get('duration', 0)),
            'size': int(fmt.get('size', 0)),
            'format': fmt.get('format_name', ''),
            'width': int(video_stream.get('width', 0)),
            'height': int(video_stream.get('height', 0)),
            'has_audio': bool(audio_stream),
            'bitrate': int(fmt.get('bit_rate', 0))
        }
    except Exception as e:
        return {'error': str(e)}


def extract_audio(video_path: str, output_path: str) -> bool:
    """从视频提取音频（16kHz 单声道，whisper 最优格式）"""
    cmd = [
        'ffmpeg', '-i', video_path,
        '-ar', '16000', '-ac', '1', '-c:a', 'pcm_s16le',
        output_path, '-y'
    ]
    try:
        subprocess.run(cmd, capture_output=True, check=True)
        return True
    except subprocess.CalledProcessError as e:
        print(f"音频提取失败: {e.stderr}", file=sys.stderr)
        return False


def transcribe_audio(audio_path: str, model: str = "base", language: str = "zh") -> dict:
    """语音转文字"""
    import whisper

    print(f"正在加载 Whisper {model} 模型...")
    model = whisper.load_model(model)

    print(f"正在转录音频（语言: {language}）...")
    result = model.transcribe(audio_path, language=language, verbose=False)

    return {
        'text': result.get('text', ''),
        'language': result.get('language', language),
        'segments': result.get('segments', []),
        'duration': result.get('segments', [{}])[-1].get('end', 0) if result.get('segments') else 0
    }


def extract_frames(video_path: str, output_dir: str, interval: int = 30) -> list:
    """按时间间隔提取视频帧"""
    frames = []
    cmd = [
        'ffmpeg', '-i', video_path,
        '-vf', f'fps=1/{interval}',
        os.path.join(output_dir, 'frame_%03d.jpg'),
        '-y'
    ]
    try:
        subprocess.run(cmd, capture_output=True, check=True)
        # 获取提取的帧列表
        frames = sorted(Path(output_dir).glob('frame_*.jpg'))
    except subprocess.CalledProcessError as e:
        print(f"帧提取失败: {e.stderr}", file=sys.stderr)

    return frames


def analyze_video(video_path: str, output_dir: str = None,
                  extract_frames_flag: bool = False,
                  frame_interval: int = 30) -> dict:
    """
    完整视频分析流程

    Args:
        video_path: 视频文件路径
        output_dir: 输出目录（默认临时目录）
        extract_frames_flag: 是否提取帧
        frame_interval: 帧提取间隔（秒）

    Returns:
        分析结果字典
    """
    video_path = Path(video_path)
    if not video_path.exists():
        return {'error': f'视频文件不存在: {video_path}'}

    # 创建临时目录
    temp_dir = output_dir or tempfile.mkdtemp(prefix='video_analysis_')
    temp_dir = Path(temp_dir)
    temp_dir.mkdir(parents=True, exist_ok=True)

    audio_path = temp_dir / 'audio.wav'
    frames_dir = temp_dir / 'frames'
    frames_dir.mkdir(exist_ok=True)

    result = {
        'video_path': str(video_path),
        'video_name': video_path.name,
        'analysis_time': datetime.now().isoformat(),
        'temp_dir': str(temp_dir)
    }

    try:
        # 1. 获取视频信息
        print("正在获取视频信息...")
        result['video_info'] = get_video_info(str(video_path))

        # 2. 提取音频
        if result['video_info'].get('has_audio', False):
            print("正在提取音频...")
            if extract_audio(str(video_path), str(audio_path)):
                # 3. 语音转文字
                print("正在进行语音识别...")
                transcribe_result = transcribe_audio(str(audio_path))
                result['transcript'] = transcribe_result
            else:
                result['transcript'] = {'error': '音频提取失败'}
        else:
            result['transcript'] = {'error': '视频无音频轨道'}

        # 4. 提取帧（如需要）
        if extract_frames_flag:
            print(f"正在提取视频帧（每 {frame_interval} 秒一帧）...")
            result['frames'] = [str(f) for f in extract_frames(str(video_path), str(frames_dir), frame_interval)]

    except Exception as e:
        result['error'] = str(e)
        import traceback
        traceback.print_exc()

    return result


def generate_report(analysis_result: dict) -> str:
    """生成 Markdown 分析报告"""
    video_info = analysis_result.get('video_info', {})
    transcript = analysis_result.get('transcript', {})

    # 格式化时长
    duration = video_info.get('duration', 0)
    minutes, seconds = divmod(int(duration), 60)
    hours, minutes = divmod(minutes, 60)
    duration_str = f"{hours:02d}:{minutes:02d}:{seconds:02d}" if hours else f"{minutes:02d}:{seconds:02d}"

    # 格式化文件大小
    size = video_info.get('size', 0)
    size_str = f"{size / 1024 / 1024:.1f} MB"

    report = f"""# 视频内容分析报告

## 📹 基本信息

| 属性 | 值 |
|------|-----|
| 文件名 | {analysis_result.get('video_name', 'N/A')} |
| 时长 | {duration_str} |
| 分辨率 | {video_info.get('width', 'N/A')}x{video_info.get('height', 'N/A')} |
| 文件大小 | {size_str} |
| 格式 | {video_info.get('format', 'N/A')} |
| 分析时间 | {analysis_result.get('analysis_time', 'N/A')} |

## 🎙️ 语音内容（转录）

"""

    if 'error' not in transcript:
        report += f"""**识别语言**: {transcript.get('language', 'zh')}

{transcript.get('text', '（无语音内容）')}

---
*共 {len(transcript.get('segments', []))} 个语音片段*
"""
    else:
        report += f"*⚠️ {transcript.get('error', '转录失败')}*\n"

    # 帧信息
    frames = analysis_result.get('frames', [])
    if frames:
        report += f"""
## 🖼️ 关键帧截取

已提取 {len(frames)} 张关键帧，保存在: `{analysis_result.get('temp_dir')}/frames/`

"""

    report += """
## 📝 深度解读

*（AI 基于语音内容和视觉画面进行深度理解）*

"""

    return report


def cleanup(temp_dir: str):
    """清理临时文件"""
    try:
        shutil.rmtree(temp_dir)
        print(f"已清理临时目录: {temp_dir}")
    except Exception as e:
        print(f"清理失败: {e}", file=sys.stderr)


def main():
    """主入口"""
    if len(sys.argv) < 2:
        print("用法: python video_analysis.py <视频文件路径> [输出目录] [--extract-frames] [--interval 秒数]")
        print("示例: python video_analysis.py '销售忙死业绩半死.mp4'")
        sys.exit(1)

    video_path = sys.argv[1]
    output_dir = sys.argv[2] if len(sys.argv) > 2 else None
    extract_frames_flag = '--extract-frames' in sys.argv
    frame_interval = 30

    for i, arg in enumerate(sys.argv):
        if arg == '--interval' and i + 1 < len(sys.argv):
            try:
                frame_interval = int(sys.argv[i + 1])
            except ValueError:
                pass

    # 检查依赖
    ok, deps = check_dependencies()
    if not ok:
        print("❌ 依赖检查失败:", file=sys.stderr)
        for name, status in deps.items():
            if not status:
                print(f"  - {name}: 未安装", file=sys.stderr)
        print("\n请先安装缺失依赖:", file=sys.stderr)
        if not deps.get('ffmpeg'):
            print("  ffmpeg: pip install ffmpeg-python 或从 https://ffmpeg.org 下载")
        if not deps.get('python_whisper'):
            print("  whisper: pip install openai-whisper")
        sys.exit(1)

    print("=" * 50)
    print("🎬 视频内容分析")
    print("=" * 50)

    # 执行分析
    result = analyze_video(
        video_path,
        output_dir,
        extract_frames_flag=extract_frames_flag,
        frame_interval=frame_interval
    )

    if 'error' in result and 'video_info' not in result:
        print(f"❌ 分析失败: {result['error']}", file=sys.stderr)
        sys.exit(1)

    # 生成报告
    report = generate_report(result)
    print("\n" + report)

    # 保存报告
    report_path = Path(output_dir or '.') / f"{Path(video_path).stem}_analysis.md"
    with open(report_path, 'w', encoding='utf-8') as f:
        f.write(report)
    print(f"\n📄 报告已保存: {report_path}")

    # 输出完整 JSON（供 AI 深度解读）
    json_path = Path(output_dir or '.') / f"{Path(video_path).stem}_analysis.json"
    with open(json_path, 'w', encoding='utf-8') as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
    print(f"📊 JSON 数据已保存: {json_path}")

    return result


if __name__ == '__main__':
    main()
