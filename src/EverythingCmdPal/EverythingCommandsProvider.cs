using EverythingCmdPal.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingCmdPal;

public partial class EverythingCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settings = new();
    private readonly ICommandItem[] _commands;
    private readonly EverythingFallbackItem _fallback;

    public EverythingCommandsProvider()
    {
        Id = "EverythingCmdPal";
        DisplayName = "Everything";
        Icon = Icons.App;

        _fallback = new EverythingFallbackItem(_settings);

        _commands =
        [
            new CommandItem(new Pages.ResultsPage(_settings))
            {
                Title = "Everything 搜索",
                Subtitle = "用 Everything 搜索本机文件（Ctrl+K 可隐藏/显示预览）",
                MoreCommands =
                [
                    new CommandContextItem(_settings.Settings.SettingsPage),
                ],
            },
        ];

        Settings = _settings.Settings;
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override IFallbackCommandItem[] FallbackCommands() =>
        _settings.Fallback ? [_fallback] : [];
}
