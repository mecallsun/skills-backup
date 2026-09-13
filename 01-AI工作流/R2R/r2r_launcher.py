#!/usr/bin/env python3
"""
R2R Skill Launcher
通过MCP工具调用执行深度研究流程
"""

import os
import sys
from datetime import datetime

# 添加skills目录到路径
skill_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, skill_dir)

def get_topic_from_input():
    """获取用户输入的研究课题"""
    # 检查命令行参数
    if len(sys.argv) > 1:
        return ' '.join(sys.argv[1:])

    # 检查环境变量
    topic = os.environ.get('R2R_TOPIC')
    if topic:
        return topic

    return None

def main():
    topic = get_topic_from_input()

    if not topic:
        print("/R2R 使用说明:")
        print("  /R2R [复杂研究课题]")
        print("\n例如:")
        print("  /R2R 人工智能对未来就业市场的影响")
        print("  /R2R 全球新能源汽车市场发展趋势分析")
        print("  /R2R 中国半导体产业国产替代机遇与挑战")
        return

    print(f"\n开始深度研究: {topic}")
    print(f"时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")

    # 导入并运行skill
    try:
        from r2r_skill import R2RSkill
        skill = R2RSkill()
        result = skill.run(topic)
        print("\n研究完成!")
    except ImportError as e:
        print(f"错误: 无法导入R2R模块 - {e}")
        print("请确保r2r_skill.py文件存在")
    except Exception as e:
        print(f"错误: {e}")

if __name__ == "__main__":
    main()