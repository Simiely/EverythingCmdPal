using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CmdPalLinks.Models;

namespace CmdPalLinks.Services;

internal sealed record ImportIssue(int Line, string Reason);

internal sealed record PreviewRow(int Line, LinkItem Item);

internal sealed class ImportPreview
{
    public string Source { get; init; } = string.Empty;

    public string Format { get; init; } = string.Empty;

    public int TotalRows { get; set; }

    public List<PreviewRow> Addable { get; } = new();

    public List<PreviewRow> Duplicates { get; } = new();

    public List<ImportIssue> Issues { get; } = new();

    public List<ImportIssue> Warnings { get; } = new();
}

internal readonly record struct ImportParseResult(ImportPreview? Preview, string? FatalError);

/// <summary>
/// CSV/JSON/TXT 解析与 CSV 导出。
/// CSV 表头严格为 type,target,title,alias,tags,emoji,color（顺序一致）。
/// </summary>
internal static class CsvCodec
{
    public const string Header = "type,target,title,alias,tags,emoji,color";

    private const int MaxRows = 10_000;

    private const int MaxBytes = 5 * 1024 * 1024;

    private static readonly string[] HeaderFields = Header.Split(',');

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static ImportParseResult ParseFile(string path, LibraryStore store)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return new ImportParseResult(null, "文件不存在");
            }

            if (info.Length > MaxBytes)
            {
                return new ImportParseResult(null, "文件超过 5 MB，请拆分为多个文件");
            }

            string text;
            try
            {
                var bytes = File.ReadAllBytes(path);
                text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return new ImportParseResult(null, "文件不是 UTF-8 编码，请在编辑器中另存为 UTF-8 后重试（不会按 GBK 猜测解码）");
            }

            if (text.Length > 0 && text[0] == '\uFEFF')
            {
                text = text.Substring(1);
            }

            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension switch
            {
                ".csv" => ParseCsv(text, path, store),
                ".json" => ParseJson(text, path, store),
                ".txt" => ParseTxt(text, path, store),
                _ => new ImportParseResult(null, $"不支持的格式“{extension}”（支持 .csv / .json / .txt）"),
            };
        }
        catch (Exception ex)
        {
            return new ImportParseResult(null, $"读取失败：{ex.Message}");
        }
    }

    public static ImportParseResult ParseCsv(string text, string source, LibraryStore store)
    {
        var records = ParseRecords(text);
        if (records.Count == 0)
        {
            return new ImportParseResult(null, "文件为空");
        }

        var header = records[0].Fields.Select(field => field.Trim()).ToArray();
        if (header.Length != HeaderFields.Length || !header.SequenceEqual(HeaderFields, StringComparer.OrdinalIgnoreCase))
        {
            return new ImportParseResult(null, $"表头不匹配。期望：{Header}；实际：{string.Join(',', header)}");
        }

        if (records.Count - 1 > MaxRows)
        {
            return new ImportParseResult(null, $"超过 {MaxRows} 行，请拆分为多个文件");
        }

        var preview = new ImportPreview { Source = source, Format = "CSV", TotalRows = records.Count - 1 };
        var existing = BuildExistingMap(store);
        var inFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Fields.Count == 1 && string.IsNullOrWhiteSpace(record.Fields[0]))
            {
                continue;
            }

            if (record.Fields.Count != HeaderFields.Length)
            {
                preview.Issues.Add(new ImportIssue(record.Line, $"字段数为 {record.Fields.Count}，应为 {HeaderFields.Length}（含逗号字段请用双引号包裹）"));
                continue;
            }

            var item = BuildItem(record.Fields.ToArray(), record.Line, preview);
            if (item is null)
            {
                continue;
            }

            if (!LibraryStore.DedupeKey(item.Kind, item.Target, out var key, out _))
            {
                preview.Issues.Add(new ImportIssue(record.Line, "target 无法归一化"));
                continue;
            }

            if (inFile.Contains(key))
            {
                preview.Duplicates.Add(new PreviewRow(record.Line, item));
                continue;
            }

            inFile.Add(key);
            if (existing.ContainsKey(key))
            {
                preview.Duplicates.Add(new PreviewRow(record.Line, item));
                continue;
            }

            preview.Addable.Add(new PreviewRow(record.Line, item));
        }

        return new ImportParseResult(preview, null);
    }

    private static ImportParseResult ParseJson(string text, string source, LibraryStore store)
    {
        LibraryData? data;
        try
        {
            data = JsonSerializer.Deserialize<LibraryData>(text, JsonReadOptions);
        }
        catch (JsonException ex)
        {
            return new ImportParseResult(null, $"JSON 解析失败：{ex.Message}");
        }

        if (data?.Items is null)
        {
            return new ImportParseResult(null, "JSON 缺少 items 数组");
        }

        if (data.Items.Count > MaxRows)
        {
            return new ImportParseResult(null, $"超过 {MaxRows} 条，请拆分为多个文件");
        }

        var preview = new ImportPreview { Source = source, Format = "JSON", TotalRows = data.Items.Count };
        var existing = BuildExistingMap(store);
        var inFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < data.Items.Count; i++)
        {
            var raw = data.Items[i];
            var line = i + 1;
            var kind = (raw.Kind ?? string.Empty).Trim().ToLowerInvariant();
            var target = (raw.Target ?? string.Empty).Trim();

            if (!LinkKinds.IsValid(kind))
            {
                preview.Issues.Add(new ImportIssue(line, $"type “{raw.Kind}”非法（应为 url/file/folder）"));
                continue;
            }

            if (!LibraryStore.DedupeKey(kind, target, out var key, out var error))
            {
                preview.Issues.Add(new ImportIssue(line, error));
                continue;
            }

            var item = new LinkItem
            {
                Kind = kind,
                Target = target,
                Title = string.IsNullOrWhiteSpace(raw.Title) ? LibraryStore.DefaultTitle(kind, target) : raw.Title.Trim(),
                Aliases = LibraryStore.Clean(raw.Aliases),
                Tags = LibraryStore.Clean(raw.Tags),
                IconKind = IconKinds.Normalize(raw.IconKind),
                IconColor = (raw.IconColor ?? string.Empty).Trim(),
                IconEmoji = (raw.IconEmoji ?? string.Empty).Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            if (inFile.Contains(key) || existing.ContainsKey(key))
            {
                preview.Duplicates.Add(new PreviewRow(line, item));
                continue;
            }

            inFile.Add(key);
            preview.Addable.Add(new PreviewRow(line, item));
        }

        return new ImportParseResult(preview, null);
    }

    private static ImportParseResult ParseTxt(string text, string source, LibraryStore store)
    {
        var preview = new ImportPreview { Source = source, Format = "TXT", TotalRows = 0 };
        var existing = BuildExistingMap(store);
        var inFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        if (lines.Length > MaxRows)
        {
            return new ImportParseResult(null, $"超过 {MaxRows} 行，请拆分为多个文件");
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i].Trim();
            if (raw.Length == 0)
            {
                continue;
            }

            if (raw.StartsWith('#'))
            {
                continue;
            }

            preview.TotalRows++;
            var line = i + 1;
            var kind = InferKind(raw);
            if (kind is null)
            {
                preview.Issues.Add(new ImportIssue(line, "无法判断是网址还是已存在的文件/文件夹"));
                continue;
            }

            if (!LibraryStore.DedupeKey(kind, raw, out var key, out var error))
            {
                preview.Issues.Add(new ImportIssue(line, error));
                continue;
            }

            var item = new LinkItem
            {
                Kind = kind,
                Target = raw,
                Title = LibraryStore.DefaultTitle(kind, raw),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            if (inFile.Contains(key) || existing.ContainsKey(key))
            {
                preview.Duplicates.Add(new PreviewRow(line, item));
                continue;
            }

            inFile.Add(key);
            preview.Addable.Add(new PreviewRow(line, item));
        }

        return new ImportParseResult(preview, null);
    }

    private static LinkItem? BuildItem(string[] fields, int line, ImportPreview preview)
    {
        var kind = fields[0].Trim().ToLowerInvariant();
        var target = fields[1].Trim();

        if (!LinkKinds.IsValid(kind))
        {
            preview.Issues.Add(new ImportIssue(line, $"type “{fields[0].Trim()}”非法（应为 url/file/folder）"));
            return null;
        }

        if (target.Length == 0)
        {
            preview.Issues.Add(new ImportIssue(line, "target 为空"));
            return null;
        }

        if (!LibraryStore.DedupeKey(kind, target, out _, out var error))
        {
            preview.Issues.Add(new ImportIssue(line, error));
            return null;
        }

        var title = fields[2].Trim();
        var aliases = SplitList(fields[3]);
        var tags = SplitList(fields[4]);
        var emoji = FirstTextElement(fields[5].Trim());

        var colorText = fields[6].Trim();
        var iconKind = IconKinds.Auto;
        var iconColor = string.Empty;

        if (colorText.Length == 0)
        {
            if (emoji.Length > 0)
            {
                iconKind = IconKinds.Emoji;
            }
        }
        else if (string.Equals(colorText, "none", StringComparison.OrdinalIgnoreCase))
        {
            iconKind = emoji.Length > 0 ? IconKinds.Emoji : IconKinds.Transparent;
        }
        else if (IsHexColor(colorText))
        {
            iconColor = colorText.ToUpperInvariant();
            iconKind = emoji.Length > 0 ? IconKinds.Emoji : IconKinds.Block;
        }
        else
        {
            preview.Warnings.Add(new ImportIssue(line, $"color “{colorText}”无效（应为 #RRGGBB 或 none），已按自动处理"));
            if (emoji.Length > 0)
            {
                iconKind = IconKinds.Emoji;
            }
        }

        return new LinkItem
        {
            Kind = kind,
            Target = target,
            Title = title.Length == 0 ? LibraryStore.DefaultTitle(kind, target) : title,
            Aliases = aliases,
            Tags = tags,
            IconKind = iconKind,
            IconColor = iconColor,
            IconEmoji = emoji,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static Dictionary<string, LinkItem> BuildExistingMap(LibraryStore store)
    {
        var map = new Dictionary<string, LinkItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in store.Items)
        {
            if (LibraryStore.DedupeKey(item.Kind, item.Target, out var key, out _))
            {
                map[key] = item;
            }
        }

        return map;
    }

    private static string? InferKind(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri)
            && (string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)))
        {
            return LinkKinds.Url;
        }

        try
        {
            if (File.Exists(target))
            {
                return LinkKinds.File;
            }

            if (Directory.Exists(target))
            {
                return LinkKinds.Folder;
            }
        }
        catch (Exception)
        {
            // 非法路径按无法判断处理。
        }

        return null;
    }

    private static string[] SplitList(string value)
    {
        var text = value.Trim();
        if (text.Length == 0)
        {
            return Array.Empty<string>();
        }

        var parts = text.Contains('|') ? text.Split('|') : text.Split('，');
        var result = new List<string>();
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0 && !result.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(trimmed);
            }
        }

        return result.ToArray();
    }

    private static string FirstTextElement(string value)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        return StringInfo.GetNextTextElement(value).Trim();
    }

    private static bool IsHexColor(string value)
    {
        if (value.Length != 7 || value[0] != '#')
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!Uri.IsHexDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>RFC 4180 记录解析：支持引号包裹、字段内逗号/换行、"" 转义、CRLF/CR/LF。</summary>
    private static List<(int Line, List<string> Fields)> ParseRecords(string text)
    {
        var records = new List<(int Line, List<string> Fields)>();
        var fields = new List<string>();
        var buffer = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        buffer.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else if (ch == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    line++;
                    buffer.Append('\n');
                }
                else if (ch == '\n')
                {
                    line++;
                    buffer.Append('\n');
                }
                else
                {
                    buffer.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(buffer.ToString());
                    buffer.Clear();
                    break;
                case '\r':
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    fields.Add(buffer.ToString());
                    buffer.Clear();
                    records.Add((recordLine, new List<string>(fields)));
                    fields.Clear();
                    line++;
                    recordLine = line;
                    break;
                case '\n':
                    fields.Add(buffer.ToString());
                    buffer.Clear();
                    records.Add((recordLine, new List<string>(fields)));
                    fields.Clear();
                    line++;
                    recordLine = line;
                    break;
                default:
                    buffer.Append(ch);
                    break;
            }
        }

        if (buffer.Length > 0 || fields.Count > 0)
        {
            fields.Add(buffer.ToString());
            records.Add((recordLine, fields));
        }

        return records;
    }

    /// <summary>导出 CSV：UTF-8 BOM + CRLF，表头与编辑页一致，可原样回导。</summary>
    public static byte[] ToCsvBytes(IReadOnlyList<LinkItem> items)
    {
        var builder = new StringBuilder();
        builder.Append(Header).Append("\r\n");

        foreach (var item in items)
        {
            var color = string.Empty;
            if (string.Equals(item.IconKind, IconKinds.Transparent, StringComparison.OrdinalIgnoreCase))
            {
                color = "none";
            }
            else if (ItemHasCustomIcon(item))
            {
                color = item.IconColor;
            }

            builder
                .Append(Escape(item.Kind)).Append(',')
                .Append(Escape(item.Target)).Append(',')
                .Append(Escape(item.Title)).Append(',')
                .Append(Escape(string.Join('|', item.Aliases ?? Array.Empty<string>()))).Append(',')
                .Append(Escape(string.Join('|', item.Tags ?? Array.Empty<string>()))).Append(',')
                .Append(Escape(item.IconEmoji ?? string.Empty)).Append(',')
                .Append(Escape(color))
                .Append("\r\n");
        }

        var body = Encoding.UTF8.GetBytes(builder.ToString());
        var result = new byte[body.Length + 3];
        result[0] = 0xEF;
        result[1] = 0xBB;
        result[2] = 0xBF;
        Array.Copy(body, 0, result, 3, body.Length);
        return result;
    }

    private static bool ItemHasCustomIcon(LinkItem item)
        => IconKinds.IsCustom(item.IconKind, item.IconEmoji, item.IconColor);

    private static string Escape(string value)
    {
        var text = value ?? string.Empty;
        if (text.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return text;
        }

        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
