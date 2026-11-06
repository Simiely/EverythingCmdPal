using System.IO;
using CmdPalLinks.Models;

namespace CmdPalLinks.Services;

internal static class ItemHealth
{
    public static bool IsMissing(LinkItem item) => item.Kind switch
    {
        LinkKinds.File => !File.Exists(item.Target),
        LinkKinds.Folder => !Directory.Exists(item.Target),
        _ => false,
    };
}
