using CmdPalLinks.Models;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Pages;

internal sealed partial class EditItemPage : ContentPage
{
    private readonly EditItemForm _form;

    public EditItemPage(LibraryStore store, LinkItem item)
    {
        Icon = Icons.Edit;
        Title = "编辑条目";
        Name = "编辑";
        _form = new EditItemForm(store, item);
    }

    public override IContent[] GetContent() => new IContent[] { _form };
}
