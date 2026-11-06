using EverythingCmdPal.Pages;
using EverythingCmdPal.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingCmdPal;

internal sealed partial class EverythingFallbackItem : FallbackCommandItem
{
    private readonly SettingsManager _settings;

    public EverythingFallbackItem(SettingsManager settings)
        : base(new NoOpCommand(), "在 Everything 中搜索", "com.everything.cmdpal.fallback")
    {
        _settings = settings;
        Icon = Icons.App;
        Title = string.Empty;
        Subtitle = string.Empty;
    }

    public override void UpdateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.StartsWith('>') || query.StartsWith('$') || query.StartsWith('='))
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = new NoOpCommand();
            return;
        }

        Title = $"在 Everything 中搜索「{query}」";
        Subtitle = "文件与文件夹 · Everything 1.5";
        Icon = Icons.App;
        Command = new ResultsPage(_settings, query);
    }
}
