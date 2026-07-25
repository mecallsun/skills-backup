using System.Collections.Generic;

namespace DormManage.Shared.Security;

/// <summary>
/// v2.13.135 暗桩解锁序列 5-2-0 检测器（共享逻辑）
///
/// 复用「仓库物料汇总」Jinge.MaterialSummary FR-07 隐藏键盘序列设计：
/// 维护人员按 5 → 2 → 0 三个数字键，解锁成功。
///
/// 跨平台使用：
/// 1. WinForms（TrayApp）：KeyDown 事件传入 e.KeyValue（ASCII 码）
/// 2. 浏览器（Admin _Layout.cshtml JS）：keydown 事件传入 e.key.charCodeAt(0)
/// 3. WPF（未来扩展）：KeyDown 事件传入 KeyInterop.VirtualKeyFromKey
///
/// 线程安全：所有写操作加锁。缓冲上限 3 个元素（FIFO）。
/// </summary>
public static class UnlockSequenceBuffer
{
    /// <summary>目标解锁序列：'5' → '2' → '0'</summary>
    private static readonly int[] _targetSequence = { 53, 50, 48 }; // ASCII: '5'=53, '2'=50, '0'=48

    private static readonly List<int> _buffer = new();
    private static readonly object _lock = new();

    /// <summary>
    /// 喂入一个 keyCode（数字键 0~9 的 ASCII 码或 KeyCode）
    /// 当连续 3 个键匹配 5-2-0 时返回 true（解锁成功），缓冲自动重置
    /// </summary>
    public static bool Feed(int keyCode)
    {
        lock (_lock)
        {
            _buffer.Add(keyCode);
            if (_buffer.Count > _targetSequence.Length)
                _buffer.RemoveAt(0);

            if (_buffer.Count == _targetSequence.Length)
            {
                bool match = true;
                for (int i = 0; i < _targetSequence.Length; i++)
                {
                    if (_buffer[i] != _targetSequence[i]) { match = false; break; }
                }
                if (match)
                {
                    Reset(); // 解锁成功后重置缓冲
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 判断给定的 keyCode 是否是数字键 0~9 的 ASCII 码
    /// </summary>
    public static bool IsDigitKey(int keyCode)
    {
        return keyCode >= 48 && keyCode <= 57; // ASCII '0'=48, '9'=57
    }

    /// <summary>清除缓冲（超时或解锁后调用）</summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _buffer.Clear();
        }
    }

    /// <summary>当前缓冲（仅供测试/调试，生产代码不应调用）</summary>
    public static int[] CurrentBufferSnapshot()
    {
        lock (_lock)
        {
            return _buffer.ToArray();
        }
    }
}