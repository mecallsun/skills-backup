#!/bin/bash
# scripts/verify-archive.sh
# 用途：验证发布压缩包是否符合"扁平层级 + 仅 3 组件"规范
# 来源：00-方案文档/238-发布程序包压缩层级规范-v2.13.208.md
# 用法：bash scripts/verify-archive.sh release/_archive/<archive>.zip

set -e

if [ $# -lt 1 ]; then
    echo "用法: $0 <archive.zip>"
    echo "示例: $0 release/_archive/DormManage-v2.13.208_20260728_150512.zip"
    exit 1
fi

ARCHIVE="$1"

if [ ! -f "$ARCHIVE" ]; then
    echo "❌ 归档包不存在: $ARCHIVE"
    exit 1
fi

echo "=========================================="
echo " 归档包层级规范验证 (v2.13.208)"
echo "=========================================="
echo " 归档: $ARCHIVE"
echo

# 规则 1: 第一层目录必须是 3 个组件名（Admin/, Api/, TrayApp/）
echo "【规则 1】检查第一层目录..."

# 提取所有顶级目录条目（仅目录，路径深度 = 2，如 Admin/, ./Admin/ 等）
TOP_LEVEL=$(tar -tzf "$ARCHIVE" 2>/dev/null | \
    grep -E '^[A-Za-z][A-Za-z0-9_]*/$' | \
    sort -u)

EXPECTED="Admin/
Api/
TrayApp/"

if [ "$TOP_LEVEL" != "$EXPECTED" ]; then
    echo "  ❌ 第一层目录不规范"
    echo "    实际:"
    echo "$TOP_LEVEL" | sed 's/^/      /'
    echo "    期望:"
    echo "$EXPECTED" | sed 's/^/      /'
    exit 1
fi
echo "  ✓ 第一层目录:"
echo "$TOP_LEVEL" | sed 's/^/      /'
echo

# 规则 2: 禁止嵌套压缩文件
echo "【规则 2】检查无嵌套压缩文件..."
NESTED=$(tar -tzf "$ARCHIVE" 2>/dev/null | grep -E '\.(zip|7z|tar|gz|rar|bz2|zip\.001)$' || true)

if [ -n "$NESTED" ]; then
    echo "  ❌ 归档包内禁止嵌套其他压缩文件"
    echo "    违规条目:"
    echo "$NESTED" | sed 's/^/      /'
    exit 1
fi
echo "  ✓ 未发现嵌套压缩文件"
echo

# 规则 3: 每个组件下必须有可执行文件（.exe）
echo "【规则 3】检查每个组件包含可执行文件..."
for component in Admin Api TrayApp; do
    if ! tar -tzf "$ARCHIVE" 2>/dev/null | grep -q "^$component/.*\.exe$"; then
        echo "  ❌ 组件 $component 下缺少 .exe 文件"
        exit 1
    fi
    EXE_FILE=$(tar -tzf "$ARCHIVE" 2>/dev/null | grep "^$component/.*\.exe$" | head -1)
    echo "  ✓ $component: $EXE_FILE"
done
echo

# 规则 4: 不应包含调试产物 bin/Debug 或 obj/
echo "【规则 4】检查无调试产物..."
DEBUG_PATHS=$(tar -tzf "$ARCHIVE" 2>/dev/null | grep -E '/(bin/Debug|obj/|bin/Release)/' || true)

if [ -n "$DEBUG_PATHS" ]; then
    echo "  ❌ 归档包内禁止包含 bin/Debug 或 obj/ 目录"
    echo "    违规条目 (前 5 条):"
    echo "$DEBUG_PATHS" | head -5 | sed 's/^/      /'
    exit 1
fi
echo "  ✓ 未发现调试产物"
echo

# 规则 5: 文件名格式校验
echo "【规则 5】检查归档包文件名格式..."
FILENAME=$(basename "$ARCHIVE")
if [[ ! "$FILENAME" =~ ^DormManage-v[0-9]+\.[0-9]+\.[0-9]+_[0-9]{8}_[0-9]{6}\.zip$ ]]; then
    echo "  ⚠ 文件名格式不符合规范（期望: DormManage-v{MAJOR}.{MINOR}.{BUILD}_{YYYYMMDD_HHMMSS}.zip）"
    echo "    当前: $FILENAME"
    exit 1
fi
echo "  ✓ 文件名: $FILENAME"
echo

# 全部通过
echo "=========================================="
echo " ✓ 归档包结构验证通过 (v2.13.208 规范)"
echo "=========================================="
exit 0
