using System;
using System.Text.Json.Nodes;
using CmdPalLinks.Models;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Pages;

internal sealed partial class EditItemForm : FormContent
{
    private readonly LibraryStore _store;
    private readonly LinkItem _item;

    public EditItemForm(LibraryStore store, LinkItem item)
    {
        _store = store;
        _item = item;
        TemplateJson = ItemFormTemplate.Build(
            item.Kind,
            item.Target,
            item.Title,
            string.Join(", ", item.Aliases),
            string.Join(", ", item.Tags),
            item.Folder);
    }

    public override ICommandResult SubmitForm(string payload)
    {
        try
        {
            var root = JsonNode.Parse(payload) as JsonObject;
            if (root is null)
            {
                return CommandResult.ShowToast("表单数据无效");
            }

            var result = _store.Update(
                _item.Id,
                AddItemForm.Text(root, "kind"),
                AddItemForm.Text(root, "title"),
                AddItemForm.Text(root, "target"),
                AddItemForm.Split(AddItemForm.Text(root, "aliases")),
                AddItemForm.Split(AddItemForm.Text(root, "tags")),
                folder: AddItemForm.Text(root, "folder"));

            switch (result.Status)
            {
                case UpdateStatus.Updated:
                    return CommandResult.ShowToast(new ToastArgs
                    {
                        Message = $"已保存：{result.Item?.Title}",
                        Result = CommandResult.Dismiss(),
                    });

                case UpdateStatus.Duplicate:
                    return CommandResult.ShowToast(new ToastArgs
                    {
                        Message = $"已存在：{result.Item?.Title}",
                        Result = CommandResult.KeepOpen(),
                    });

                case UpdateStatus.SaveFailed:
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
            HostLog.Error($"edit form failed: {ex.GetType().Name}: {ex.Message}");
            return CommandResult.ShowToast(new ToastArgs
            {
                Message = "保存失败，请检查输入",
                Result = CommandResult.KeepOpen(),
            });
        }
    }
}
