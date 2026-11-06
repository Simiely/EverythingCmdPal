using Microsoft.CommandPalette.Extensions.Toolkit;
using CmdPalLinks.Models;

namespace CmdPalLinks;

internal static class Icons
{
    public static IconInfo App { get; } = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");

    /// <summary>顶层命令项图标：链接字形，与 Provider 的品牌链条图标区分开，避免同一图标显示两遍。</summary>
    public static IconInfo LibraryCommand { get; } = new("\uE71B");

    public static IconInfo Add { get; } = new("\uE710");

    public static IconInfo Copy { get; } = new("\uE8C8");

    public static IconInfo Edit { get; } = new("\uE70F");

    public static IconInfo Delete { get; } = new("\uE74D");

    public static IconInfo FolderOpen { get; } = new("\uE838");

    public static IconInfo Import { get; } = new("\uE8B5");

    public static IconInfo Export { get; } = new("\uE78C");

    public static IconInfo Undo { get; } = new("\uE7A7");

    public static IconInfo ForKind(string kind) => kind switch
    {
        LinkKinds.File => new IconInfo("\uE8A5"),
        LinkKinds.Folder => new IconInfo("\uE8B7"),
        _ => new IconInfo("\uE71B"),
    };
}
