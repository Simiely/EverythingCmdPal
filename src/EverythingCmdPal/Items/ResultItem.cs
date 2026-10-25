using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EverythingCmdPal.Actions;
using EverythingCmdPal.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingCmdPal.Items;

/// <summary>
/// One search result row. Presentation only: metadata comes from
/// <see cref="ResultMetadata"/>, preview from <see cref="PreviewService"/>, and the
/// details-mode toggle is injected by the page (the item never constructs pages).
/// </summary>
internal sealed class ResultItem : ListItem
{
    private static readonly SemaphoreSlim IconGate = new(4, 4);

    public ResultItem(SearchHit hit, ResultContext ctx)
        : base(new OpenPathCommand(hit.FullPath))
    {
        var name = Path.GetFileName(hit.FullPath.TrimEnd('\\'));

        Title = string.IsNullOrEmpty(name) ? hit.FullPath : name;
        Subtitle = hit.IsFolder ? "文件夹" : Path.GetDirectoryName(hit.FullPath) ?? string.Empty;

        // Immediate fallback icon; replaced by the real shell icon asynchronously.
        Icon = hit.IsFolder ? Icons.Folder : Icons.File;

        // Drag out to Explorer / Outlook / chat apps.
        DataPackage = DataPackageFactory.ForPath(hit.FullPath);

        var details = new Details
        {
            Title = Title,
            Size = ctx.DetailsSize,
        };
        Details = details;

        var more = new List<IContextItem>();
        if (ctx.ToggleItem is not null)
        {
            more.Add(ctx.ToggleItem);
        }

        if (!string.IsNullOrEmpty(ctx.EverythingExe))
        {
            more.Add(new CommandContextItem(new OpenInEverythingCommand(ctx.EverythingExe, ctx.InstanceName, ctx.QueryProvider)));
        }

        more.Add(new CommandContextItem(new PeekCommand(hit.FullPath)));
        more.Add(new CommandContextItem(new RevealInFolderCommand(hit.FullPath)));
        more.Add(new CommandContextItem(new CopyFullPathCommand(hit.FullPath)));
        MoreCommands = [.. more];

        _ = LoadIconAsync(this, hit.FullPath);
        _ = PreviewService.FillAsync(details, hit.FullPath, hit.IsFolder, ctx.PreviewWidth);
    }

    private static async Task LoadIconAsync(ListItem item, string path)
    {
        await IconGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var stream = await ThumbnailHelper.GetThumbnail(path, jumbo: false).ConfigureAwait(false);
            if (stream is not null)
            {
                item.Icon = IconInfo.FromStream(stream);
            }
        }
        catch
        {
            // Keep the fallback icon.
        }
        finally
        {
            IconGate.Release();
        }
    }
}
