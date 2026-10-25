using System;
using Microsoft.CommandPalette.Extensions;

namespace EverythingCmdPal.Items;

/// <summary>
/// Everything a result item needs from its page, in one place so the constructor
/// stays small and the item does not need to know about pages or settings.
/// </summary>
internal sealed class ResultContext
{
    public required int PreviewWidth { get; init; }

    public required ContentSize DetailsSize { get; init; }

    /// <summary>Prebuilt context-menu item that toggles the details pane (may be null).</summary>
    public required IContextItem? ToggleItem { get; init; }

    /// <summary>Returns the page's current query (for "open in Everything").</summary>
    public required Func<string> QueryProvider { get; init; }

    /// <summary>Everything.exe path, or empty when not found.</summary>
    public required string EverythingExe { get; init; }

    /// <summary>Everything 1.5 instance name, or empty for the default instance.</summary>
    public required string InstanceName { get; init; }
}
