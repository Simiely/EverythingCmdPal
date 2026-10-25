using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingCmdPal;

internal static class Icons
{
    public static IconInfo App { get; } = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");

    public static IconInfo File { get; } = new("\uE8A5");

    public static IconInfo Folder { get; } = new("\uE8B7");

    public static IconInfo Peek { get; } = new("\uE8B3");
}
