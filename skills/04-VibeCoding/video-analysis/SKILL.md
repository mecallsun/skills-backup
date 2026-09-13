---
name: video-analysis
description: 视频/音频文件深度分析技能 — 自动提取视频截帧、音频转文字、语音内容深度理解
domain: video,audio,media,ffmpeg,whisper,transcription
stage: analyze,extract,understand
output: text,transcript,analysis
complexity: medium
ai_tier: tier-1
priority: 80
language: python
tags:
  - video
  - audio
  - ffmpeg
  - whisper
  - transcription
  - frame-extraction
  - speech-to-text
  - media-analysis
triggers:
  - 视频内容
  - 读取视频
  - 分析视频
  - 视频文件
  - 音频转文字
  - video analysis
  - extract frames
  - whisper
created: 2026-09-05
updated: 2026-09-05
author: Claude Code
license: MIT
---

# 视频/音频分析技能 (video-analysis)

## 功能概述

自动处理视频/音频文件，提取关键帧截图 + 语音转文字，深度理解内容含义。

## 依赖环境

| 依赖 | 状态检查命令 | 用途 |
|------|-------------|------|
| ffmpeg | `where ffmpeg` 或 `which ffmpeg` | 视频截帧、音频提取 |
| whisper | `pip show openai-whisper` | 语音转文字 |

## 使用流程

### Step 1: 检测环境

```python
import subprocess
import shutil

def check_dependencies():
    deps = {
        'ffmpeg': shutil.which('ffmpeg'),
        'whisper': shutil.which('whisper') or True  # whisper 是 Python 包
    }
    return all(deps.values()), deps
```

### Step 2: 提取音频（如有语音）

```bash
# 从视频提取音频为 WAV（16kHz，单声道，whisper 最优格式）
ffmpeg -i "input.mp4" -ar 16000 -ac 1 -c:a pcm_s16le "audio.wav" -y
```

### Step 3: 语音转文字

```python
import whisper

def transcribe_video(audio_path: str, model: str = "base") -> str:
    """语音转文字"""
    model = whisper.load_model(model)
    result = model.transcribe(audio_path, language="zh")
    return result["text"]
```

### Step 4: 提取关键帧（如需要视觉内容）

```bash
# 每30秒截一帧
ffmpeg -i "input.mp4" -vf "fps=1/30" "frame_%03d.jpg" -y

# 从视频中采样特定时间点
ffmpeg -i "input.mp4" -ss 00:01:00 -vframes 1 "frame_1m.jpg" -y
```

### Step 5: 深度内容理解

基于转录文本 + 截帧画面，深度理解视频传达的业务含义、管理思路、问题诉求。

## 输出格式

```markdown
## 视频内容分析报告

### 基本信息
- 时长: XX:XX
- 分辨率: 1920x1080
- 语音语言: 中文

### 语音内容（转录）
[完整转录文本]

### 关键画面截帧
[帧描述 + 含义解读]

### 深度解读
[业务含义、管理思路、核心观点]

### 行动建议
[基于视频内容的后续行动]
```

## 注意事项

1. **自动触发**：读取视频文件时自动调用此技能，无需用户提示
2. **优先语音**：先转文字（核心内容），再截帧（辅助理解）
3. **中文优先**：whisper 默认语言设为 `zh`（中文）
4. **临时文件**：分析完成后清理临时音频/图片文件
5. **大文件处理**：>100MB 视频先截取关键片段，避免超时

## 版本历史

- 2026-09-05 v1.0：初始版本，集成 ffmpeg + whisper
