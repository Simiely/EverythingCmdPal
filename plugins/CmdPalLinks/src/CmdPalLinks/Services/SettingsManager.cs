using System;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Services;

internal sealed class SettingsManager : JsonSettingsManager
{
    public const string LayoutList = "list";
    public const string LayoutGridMedium = "grid-medium";

    private readonly ChoiceSetSetting _layout = new(
        "layout",
        "列表布局",
        "列表占满窗口宽度；网格为中号卡片（图标 + 标题），由宿主决定列数与卡片尺寸",
        [
            new ChoiceSetSetting.Choice("列表（默认）", LayoutList),
            new ChoiceSetSetting.Choice("网格（中）", LayoutGridMedium),
        ]);

    private readonly ToggleSetting _showFullPath = new(
        "showFullPath",
        "显示完整路径",
        "关闭后副标题只显示条目类型",
        true);

    private readonly ToggleSetting _showSubtitle = new(
        "showSubtitle",
        "显示副标题",
        "关闭后列表只显示标题（缺失路径的警告仍会显示）",
        true);

    private readonly ToggleSetting _showTags = new(
        "showTags",
        "显示标签",
        "关闭后不显示条目自带的标签",
        true);

    private bool _loading;

    public SettingsManager()
    {
        FilePath = Path.Combine(Utilities.BaseSettingsPath("CmdPalLinks"), "settings.json");
        _layout.IgnoreUnknownValue = true;
        Settings.Add(_layout);
        Settings.Add(_showFullPath);
        Settings.Add(_showSubtitle);
        Settings.Add(_showTags);
        Settings.SettingsChanged += (_, _) =>
        {
            if (!_loading)
            {
                SaveSettings();
            }
        };
    }

    public string Layout =>
        string.Equals(_layout.Value, LayoutGridMedium, StringComparison.OrdinalIgnoreCase) ? LayoutGridMedium : LayoutList;

    public bool ShowFullPath => _showFullPath.Value;

    public bool ShowSubtitle => _showSubtitle.Value;

    public bool ShowTags => _showTags.Value;

    /// <summary>
    /// 磁盘读取延后到后台加载阶段，避免拖慢扩展命令注册。
    /// </summary>
    public void Load()
    {
        _loading = true;
        try
        {
            LoadSettings();
        }
        finally
        {
            _loading = false;
        }

        HostLog.Info($"settings loaded: layout={Layout} showFullPath={ShowFullPath} showSubtitle={ShowSubtitle} showTags={ShowTags}");
    }
}
