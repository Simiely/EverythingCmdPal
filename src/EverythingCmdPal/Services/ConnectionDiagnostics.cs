using System.Diagnostics;

namespace EverythingCmdPal.Services;

/// <summary>连接 Everything 的结果分类，决定空态该说「没开」还是「连不上管道」。</summary>
internal enum EverythingConnectionState
{
    /// <summary>还没尝试过连接。</summary>
    Unknown,
    Connected,

    /// <summary>没有 Everything 进程在跑。</summary>
    NotRunning,

    /// <summary>有 Everything 进程，但拿不到 1.5 的 IPC 管道（1.4 / 实例名不符 / 未开 allow_ipc_pipe）。</summary>
    RunningWithoutPipe,
}

internal static class ConnectionDiagnostics
{
    /// <summary>连接成功与否 + 进程是否在跑 → 状态（纯函数，便于自测）。</summary>
    public static EverythingConnectionState Classify(bool connected, bool processRunning)
    {
        if (connected) return EverythingConnectionState.Connected;
        return processRunning
            ? EverythingConnectionState.RunningWithoutPipe
            : EverythingConnectionState.NotRunning;
    }

    /// <summary>是否还有 Everything 进程。1.4 / 1.5 同名，只用来区分提示语，不做版本判断。</summary>
    public static bool IsEverythingProcessRunning()
    {
        try
        {
            return Process.GetProcessesByName("Everything").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>空态文案：连不上时给可操作的原因，而不是笼统的「没有找到结果」。</summary>
    public static (string Title, string Subtitle) FailureText(EverythingConnectionState state) => state switch
    {
        EverythingConnectionState.NotRunning =>
            ("Everything 未运行", "请先启动 Everything 1.5，再回到这里搜索"),
        EverythingConnectionState.RunningWithoutPipe =>
            ("连不上 Everything 1.5 的 IPC", "检测到 Everything 进程，但拿不到 1.5 管道：可能在用 1.4，或实例名 / allow_ipc_pipe 不匹配"),
        _ =>
            ("没有找到结果", "换个关键词，或用 Everything 语法（ext:psd dm:thisweek）"),
    };
}
