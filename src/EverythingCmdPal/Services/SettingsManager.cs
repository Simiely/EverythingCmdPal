using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingCmdPal.Services;

internal sealed class SettingsManager : JsonSettingsManager
{
    private const string Namespace = "EverythingCmdPal";

    private readonly ToggleSetting _showDetails = new(
        Key(nameof(ShowDetails)),
        "显示详情栏",
        "选中结果时在右侧显示缩略图、路径、大小和修改时间",
        true);

    private readonly ToggleSetting _fallback = new(
        Key(nameof(Fallback)),
        "输入即搜",
        "在首页输入任意关键词时显示「在 Everything 中搜索」",
        true);

    private readonly ChoiceSetSetting _pageSize = new(
        Key(nameof(PageSize)),
        "每页条数",
        "每次加载的结果数量",
        [
            new ChoiceSetSetting.Choice("50", "50"),
            new ChoiceSetSetting.Choice("100", "100"),
            new ChoiceSetSetting.Choice("200", "200"),
        ]);

    private readonly TextSetting _instance = new(
        Key(nameof(InstanceName)),
        "Everything 实例名",
        "留空自动检测；Everything 1.5a 可填 1.5a",
        string.Empty);

    private readonly ChoiceSetSetting _previewWidth = new(
        Key(nameof(PreviewWidth)),
        "预览图清晰度",
        "解码宽度；窗口拉宽后越大越清晰（不改变显示大小）",
        [
            new ChoiceSetSetting.Choice("640", "640"),
            new ChoiceSetSetting.Choice("960", "960"),
            new ChoiceSetSetting.Choice("1280", "1280"),
            new ChoiceSetSetting.Choice("240", "240"),
        ]);
    private readonly ChoiceSetSetting _detailsSize = new(
        Key(nameof(DetailsSize)),
        "详情栏宽度（越大，左侧文件名越窄）",
        "小/中/大对应宿主的三档宽度，大 = 详情栏最宽",
        [
            new ChoiceSetSetting.Choice("大", "Large"),
            new ChoiceSetSetting.Choice("中", "Medium"),
            new ChoiceSetSetting.Choice("小", "Small"),
        ]);

    public bool ShowDetails => _showDetails.Value;

    public bool Fallback => _fallback.Value;

    public int PageSize => int.TryParse(_pageSize.Value, out var value) && value > 0 ? value : 50;

    public string InstanceName => _instance.Value ?? string.Empty;

    public int PreviewWidth => int.TryParse(_previewWidth.Value, out var value) && value > 0 ? value : 640;

    public Microsoft.CommandPalette.Extensions.ContentSize DetailsSize => _detailsSize.Value switch
    {
        "Small" => Microsoft.CommandPalette.Extensions.ContentSize.Small,
        "Medium" => Microsoft.CommandPalette.Extensions.ContentSize.Medium,
        _ => Microsoft.CommandPalette.Extensions.ContentSize.Large,
    };

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_showDetails);
        Settings.Add(_fallback);
        Settings.Add(_pageSize);
        Settings.Add(_instance);
        Settings.Add(_previewWidth);
        Settings.Add(_detailsSize);

        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    private static string Key(string name) => $"{Namespace}.{name}";

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("EverythingCmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }
}
