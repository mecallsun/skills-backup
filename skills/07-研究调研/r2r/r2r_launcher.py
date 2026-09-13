#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
/R2R 全局技能启动器（v1.0.0，2026-08-08）

支持三种调用方式：
  1) 命令行参数：    python r2r_launcher.py [复杂研究课题]
  2) 环境变量：       R2R_TOPIC="..." python r2r_launcher.py
  3) 作为 Python 模块：from r2r_launcher import run; run(topic, mcp_client=...)

显式支持 mcp_client 注入，避免宿主接入 MCP 时走占位内容。
"""

import os
import sys
import json
import io
from datetime import datetime
from typing import Optional, Any

# 强制 UTF-8 stdout/stderr，避免 Windows GBK 控制台乱码
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass
if hasattr(sys.stderr, "reconfigure"):
    try:
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

# 确保本地 skill 模块可被导入
SKILL_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SKILL_DIR)


def get_topic() -> Optional[str]:
    """获取研究课题来源"""
    if len(sys.argv) > 1:
        return " ".join(sys.argv[1:])
    topic = os.environ.get("R2R_TOPIC", "").strip()
    return topic or None


def build_mcp_client_from_env() -> Optional[Any]:
    """从环境变量构造 MCP 客户端（如果设置了 R2R_MCP_TRANSPORT）

    支持的 transport:
      - "tavily":   需 TAVILY_API_KEY
      - "exa":      需 EXA_API_KEY
      - "stdio":    通过 R2R_MCP_STDIO_CMD 调用（自定义 MCP stdio 进程）
    """
    transport = os.environ.get("R2R_MCP_TRANSPORT", "").strip().lower()
    if not transport:
        return None

    if transport == "tavily":
        try:
            from tavily import TavilyClient
        except ImportError:
            print("[!] 未安装 tavily-python：pip install tavily-python", file=sys.stderr)
            return None
        key = os.environ.get("TAVILY_API_KEY", "").strip()
        if not key:
            print("[!] TAVILY_API_KEY 未设置", file=sys.stderr)
            return None
        return TavilyClient(api_key=key)

    if transport == "exa":
        try:
            from exa_py import Exa
        except ImportError:
            print("[!] 未安装 exa-py：pip install exa-py", file=sys.stderr)
            return None
        key = os.environ.get("EXA_API_KEY", "").strip()
        if not key:
            print("[!] EXA_API_KEY 未设置", file=sys.stderr)
            return None
        return Exa(api_key=key)

    if transport == "stdio":
        cmd = os.environ.get("R2R_MCP_STDIO_CMD", "").strip()
        if not cmd:
            print("[!] R2R_MCP_STDIO_CMD 未设置", file=sys.stderr)
            return None
        return _StdioMCPClient(cmd)

    print(f"[!] 未知的 R2R_MCP_TRANSPORT={transport}", file=sys.stderr)
    return None


class _StdioMCPClient:
    """最小 stdio MCP 客户端包装：调用外部 MCP 进程"""

    def __init__(self, cmd: str):
        self.cmd = cmd
        # 实际启动在第一次调用时进行

    def tavily_research(self, input: str, model: str = "pro"):
        # 占位：宿主如已注册真正的 MCP 客户端，请走注入路径
        raise NotImplementedError(
            "stdio transport 需要由宿主传入真正的 MCP 客户端；"
            "CLI 模式下请直接调用 run(topic, mcp_client=client)"
        )


def _detect_project_root() -> Optional[str]:
    """自动检测当前调用上下文是否处于某个"项目根目录"

    启发式（按优先级）：
      1. 环境变量 R2R_PROJECT_DIR 显式指定
      2. 当前工作目录包含 .git / package.json / *.csproj / *.sln → 项目根
      3. 当前工作目录属于 E:\\AI工作目录\\...  → 推断为项目根
      4. 找不到则返回 None（由调用方决定 fallback 到 skill 目录）
    """
    env = os.environ.get("R2R_PROJECT_DIR", "").strip()
    if env and os.path.isdir(env):
        return env

    cwd = os.getcwd()
    project_markers = [".git", "package.json", "pyproject.toml", "requirements.txt"]
    code_markers = [".csproj", ".sln", "pom.xml", "build.gradle", "Cargo.toml", "go.mod"]

    for entry in os.listdir(cwd) if os.path.isdir(cwd) else []:
        if entry in project_markers:
            return cwd
        if any(entry.endswith(m) for m in code_markers):
            return cwd

    # E:\AI工作目录\ 下的任意子目录都视为项目根
    ai_root = "E:\\AI工作目录"
    if cwd.upper().startswith(ai_root.upper()):
        return cwd

    return None


def _resolve_output_dir(explicit: Optional[str]) -> str:
    """解析最终输出目录（按 2026-08-08 工作目录规范）

    优先级：显式参数 > 环境变量 R2R_OUTPUT_DIR > 自动检测项目根 + r2r_outputs/ > 报错
    """
    if explicit:
        os.makedirs(explicit, exist_ok=True)
        return explicit

    env = os.environ.get("R2R_OUTPUT_DIR", "").strip()
    if env:
        os.makedirs(env, exist_ok=True)
        return env

    proj = _detect_project_root()
    if proj:
        out = os.path.join(proj, "r2r_outputs")
        os.makedirs(out, exist_ok=True)
        return out

    # 无法推断项目根 → 报错（按规范不写 skill 目录）
    raise SystemExit(
        "❌ [工作目录规范] 无法推断项目根目录。\n"
        "  请通过以下任一方式指定输出目录：\n"
        "    1) 参数 --output-dir <项目目录>/r2r_outputs\n"
        "    2) 环境变量 R2R_OUTPUT_DIR=<项目目录>/r2r_outputs\n"
        "    3) 环境变量 R2R_PROJECT_DIR=<项目根目录>\n"
        "    4) 在 E:\\AI工作目录\\ 下的子目录中执行（自动识别）"
    )


def run(topic: Optional[str] = None,
        answers_initial: Optional[list] = None,
        answers_followup: Optional[list] = None,
        mcp_client: Optional[Any] = None,
        research_dict: Optional[dict] = None,
        research_json_path: Optional[str] = None,
        research_dir: Optional[str] = None,
        output_dir: Optional[str] = None) -> dict:
    """运行 R2R 流程的入口函数（供 Claude 宿主或测试调用）

    Args:
        topic: 研究课题
        research_dict: 预研究内容 {theme_id(int): content(str)} — 推荐注入路径
        research_json_path: 同上，但以 JSON 文件形式传入；格式 {"1": "...", "2": "..."}
        output_dir: 任务输出目录（**强烈推荐**传入项目目录下的 r2r_outputs/）。
                    2026-08-08 规范：自动检测项目根；不传会按规范报错而非默认到 skill 目录。
    """
    from r2r_skill import R2RSkill

    if not topic:
        topic = get_topic()
    if not topic:
        raise SystemExit(
            "错误：未提供研究课题。\n"
            "用法 1：python r2r_launcher.py [课题]\n"
            "用法 2：R2R_TOPIC='...' python r2r_launcher.py\n"
            "用法 3：run(topic='...', research_dict={1: '...', 2: '...'})"
        )

    if mcp_client is None:
        mcp_client = build_mcp_client_from_env()

    if research_dict is None and research_json_path:
        # 从 JSON 文件加载（key 转为 int）
        with open(research_json_path, "r", encoding="utf-8") as f:
            raw = json.load(f)
        research_dict = {int(k): v for k, v in raw.items()}

    # 2026-08-08 规范：解析输出目录
    final_output_dir = _resolve_output_dir(output_dir or research_dir)
    print(f"✅ [工作目录规范] 输出目录: {final_output_dir}")

    skill = R2RSkill(research_dir=final_output_dir, mcp_client=mcp_client)
    return skill.run(
        topic=topic,
        answers_initial=answers_initial,
        answers_followup=answers_followup,
        mcp_client=mcp_client,
        research_dict=research_dict,
        output_dir=final_output_dir,
    )


def main():
    print(f"\n/R2R 全局决策研究智能体  ·  v1.0.0  ·  {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
    topic = get_topic()
    if not topic:
        print("/R2R 使用说明：")
        print("  python r2r_launcher.py [复杂研究课题]")
        print("\n示例：")
        print("  python r2r_launcher.py 跨境电商白牌转型精品品牌")
        print("  python r2r_launcher.py \"佛山跨境电商 ACOS 28% 转型精品品牌\"")
        print("\n环境变量：")
        print("  R2R_TOPIC            课题字符串（命令行参数缺失时使用）")
        print("  R2R_MCP_TRANSPORT    tavily | exa | stdio")
        print("  TAVILY_API_KEY / EXA_API_KEY   对应平台 KEY")
        return

    try:
        result = run(topic=topic)
        print("\n--- 完成。输出文件清单 ---")
        for fp in result["research_files"]:
            print(f"  · {fp}")
        print(f"  · {result['word_path']}")
    except Exception as e:
        print(f"错误：{e}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()