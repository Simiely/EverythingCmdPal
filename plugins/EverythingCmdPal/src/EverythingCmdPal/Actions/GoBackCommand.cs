using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingCmdPal.Actions;

/// <summary>
/// 「显示详细信息」：弹回上一层（详情页），而不是再压一个新页面。
/// 宿主收到 GoBack 会执行 Frame.GoBack()，栈深不会随来回切换增长。
/// </summary>
internal sealed class GoBackCommand : InvokableCommand
{
    public GoBackCommand()
    {
        Name = "显示详细信息";
        Icon = Icons.App;
    }

    public override CommandResult Invoke() => CommandResult.GoBack();
}
