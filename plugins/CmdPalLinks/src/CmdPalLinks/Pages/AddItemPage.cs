using System;
using CmdPalLinks.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CmdPalLinks.Pages;

internal sealed partial class AddItemPage : ContentPage
{
    private readonly AddItemForm _form;

    public AddItemPage(LibraryStore store, string folder = "")
    {
        Icon = Icons.Add;
        Title = "添加条目";
        Name = "添加条目";
        _form = new AddItemForm(store, folder);
        _form.Added += () => Added?.Invoke();
    }

    public event Action? Added;

    public override IContent[] GetContent() => new IContent[] { _form };
}
