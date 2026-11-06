using System;
using System.Collections.Generic;

namespace CmdPalLinks.Models;

internal static class LinkKinds
{
    public const string Url = "url";
    public const string File = "file";
    public const string Folder = "folder";

    public static bool IsValid(string? kind) => kind is Url or File or Folder;
}

/// <summary>
/// 图标形态：auto 沿用 favicon / Shell 图标，其余为自绘图标（色块或透明底 + 单色 emoji/首字）。
/// </summary>
internal static class IconKinds
{
    public const string Auto = "auto";
    public const string Block = "block";
    public const string Transparent = "transparent";
    public const string Emoji = "emoji";

    public static bool IsValid(string? kind) => kind is Auto or Block or Transparent or Emoji;

    public static string Normalize(string? kind)
    {
        var value = (kind ?? string.Empty).Trim().ToLowerInvariant();
        return IsValid(value) ? value : Auto;
    }

    /// <summary>是否需要自绘图标（有任何自定义字段即视为自绘）。</summary>
    public static bool IsCustom(string? kind, string? emoji, string? color)
    {
        var normalized = Normalize(kind);
        if (normalized is Block or Transparent or Emoji)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(emoji) || !string.IsNullOrWhiteSpace(color);
    }
}

internal sealed class LinkItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Kind { get; set; } = LinkKinds.Url;

    public string Title { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string[] Aliases { get; set; } = Array.Empty<string>();

    public string[] Tags { get; set; } = Array.Empty<string>();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public int OpenCount { get; set; }

    public DateTimeOffset? LastOpenedAt { get; set; }

    /// <summary>auto / block / transparent / emoji，默认 auto（沿用旧行为）。</summary>
    public string IconKind { get; set; } = IconKinds.Auto;

    /// <summary>#RRGGBB；空 = 自动（跟随 iconKind），"none" = 透明底。</summary>
    public string IconColor { get; set; } = string.Empty;

    /// <summary>单个 emoji，可空。</summary>
    public string IconEmoji { get; set; } = string.Empty;
}

internal sealed class LibraryData
{
    public int SchemaVersion { get; set; } = 2;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<LinkItem> Items { get; set; } = new();
}
