using System;
using System.Linq;
using System.Text.Json.Nodes;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Pages;

internal sealed partial class AddItemForm : FormContent
{
    private readonly LibraryStore _store;

    public AddItemForm(LibraryStore store, string folder = "")
    {
        _store = store;
        TemplateJson = ItemFormTemplate.Build("url", string.Empty, string.Empty, string.Empty, string.Empty, folder);
    }

    public event Action? Added;

    public override ICommandResult SubmitForm(string payload)
    {
        try
        {
            var root = JsonNode.Parse(payload) as JsonObject;
            if (root is null)
            {
                return CommandResult.ShowToast("表单数据无效");
            }

            var result = _store.Add(
                Text(root, "kind"),
                Text(root, "title"),
                Text(root, "target"),
                Split(Text(root, "aliases")),
                Split(Text(root, "tags")),
                folder: Text(root, "folder"));

            switch (result.Status)
            {
                case AddStatus.Added:
                    Added?.Invoke();
                    return CommandResult.ShowToast(new ToastArgs
                    {
                        Message = $"已添加：{result.Item?.Title}",
                        Result = CommandResult.Dismiss(),
                    });

                case AddStatus.Duplicate:
                    return CommandResult.ShowToast(new ToastArgs
                    {
                        Message = $"已存在：{result.Item?.Title}",
                        Result = CommandResult.KeepOpen(),
                    });

                case AddStatus.SaveFailed:
                    Added?.Invoke();
                    return CommandResult.ShowToast(new ToastArgs
                    {
                        Message = result.Message,
                        Result = CommandResult.KeepOpen(),
                    });

                default:
                    return CommandResult.ShowToast(new ToastArgs
                    {
                        Message = result.Message,
                        Result = CommandResult.KeepOpen(),
                    });
            }
        }
        catch (Exception ex)
        {
            HostLog.Error($"add form failed: {ex.GetType().Name}: {ex.Message}");
            return CommandResult.ShowToast(new ToastArgs
            {
                Message = "保存失败，请检查输入",
                Result = CommandResult.KeepOpen(),
            });
        }
    }

    internal static string Text(JsonObject root, string key) => root[key]?.ToString()?.Trim() ?? string.Empty;

    internal static string[] Split(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Replace('，', ',')
            .Replace(';', ',')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
