using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Services;

/// <summary>导入页“上次状态”：与 links.json 分开的小文件，卸载重装丢失属正常。</summary>
internal sealed class ImportRunState
{
    public const string OutcomeImported = "imported";
    public const string OutcomePreviewed = "previewed";
    public const string OutcomeCancelled = "cancelled";
    public const string OutcomeFailed = "failed";

    public int SchemaVersion { get; set; } = 1;

    /// <summary>imported / previewed / cancelled / failed。</summary>
    public string Outcome { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public string UsedUtc { get; set; } = string.Empty;

    public int TotalRows { get; set; }

    public int Addable { get; set; }

    public int Duplicates { get; set; }

    public int Invalid { get; set; }

    public int Warnings { get; set; }
}

/// <summary>
/// 读写 LocalState\import-state.json（<see cref="ImportRunState"/>），写入走临时文件 + 替换，避免半写。
/// 文件损坏或字段缺失时整体回落为“无上次状态”，不影响条目数据。
/// </summary>
internal sealed class ImportStateStore
{
    private const string FileName = "import-state.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;

    public ImportStateStore()
    {
        _path = Path.Combine(Utilities.BaseSettingsPath("CmdPalLinks"), FileName);
    }

    public ImportRunState? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var json = File.ReadAllText(_path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<ImportRunState>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            HostLog.Error($"import state load failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    public void Save(ImportRunState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
            var temp = _path + ".tmp";
            File.WriteAllBytes(temp, payload);
            if (File.Exists(_path))
            {
                File.Replace(temp, _path, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, _path);
            }
        }
        catch (Exception ex)
        {
            HostLog.Error($"import state save failed: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
