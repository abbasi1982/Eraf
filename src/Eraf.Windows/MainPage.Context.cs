using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Eraf.Windows;

/// <summary>The right-click menu of the file list and the operations in it: open, make ERAF, rename, delete, new folder, copy path and so on.</summary>
public sealed partial class MainPage
{
    private bool CanUseContextMenu =>
        _workflow.State == SessionState.Idle && !_importBusy && !_showingSettings && !_dialogOpen;

    private void OnFileContextMenu(global::Windows.Foundation.Point position, IReadOnlyList<BrowserEntry> selection, bool onItem)
    {
        if (!CanUseContextMenu)
        {
            return;
        }

        var menu = new MenuFlyout();
        BrowserEntry? single = selection.Count == 1 ? selection[0] : null;

        MenuFlyoutItem Add(string text, string glyph, Action action, bool enabled = true, string? shortcut = null)
        {
            var item = new MenuFlyoutItem
            {
                Text = text,
                Icon = new FontIcon { Glyph = glyph },
                IsEnabled = enabled,
                KeyboardAcceleratorTextOverride = shortcut ?? string.Empty,
            };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
            return item;
        }

        if (onItem && selection.Count > 0)
        {
            Add("Open", "", () => _ = OpenEntryAsync(single!), single is not null, "Enter");
            Add("Make ERAF", "", () => OnMakeErafClick(this, new RoutedEventArgs()), CanMake(selection), "Ctrl+M");
            menu.Items.Add(new MenuFlyoutSeparator());
            Add("Rename", "", () => _ = RenameAsync(single!), single is not null, "F2");
            Add("Delete", "", () => _ = DeleteAsync(selection), true, "Del");
            menu.Items.Add(new MenuFlyoutSeparator());
            Add("Show in File Explorer", "", () => ShowInExplorer(single ?? selection[0]), true);
            Add("Copy path", "", () => CopyPaths(selection), true);
            menu.Items.Add(new MenuFlyoutSeparator());
        }

        Add("New folder", "", () => _ = NewFolderAsync(), true, "Ctrl+Shift+N");
        Add("Refresh", "", () => OnRefreshNavClick(this, new RoutedEventArgs()), true, "F5");
        Add("Select all", "", FileList.SelectAll, true, "Ctrl+A");
        if (selection.Count > 0)
        {
            Add("Select none", "", FileList.SelectNone, true);
        }

        menu.Items.Add(new MenuFlyoutSeparator());

        var layout = new MenuFlyoutSubItem { Text = "Layout", Icon = new FontIcon { Glyph = "" } };
        foreach (ViewModeInfo info in ViewModes.All)
        {
            var radio = new RadioMenuFlyoutItem { Text = info.Label, GroupName = "Layout", IsChecked = info.Mode == AppSettings.Options.ListMode };
            ViewMode chosen = info.Mode;
            radio.Click += (_, _) => SetViewMode(chosen);
            layout.Items.Add(radio);
        }

        menu.Items.Add(layout);

        var sort = new MenuFlyoutSubItem { Text = "Sort by", Icon = new FontIcon { Glyph = "" } };
        foreach ((SortKey key, string label) in new[] { (SortKey.Name, "Name"), (SortKey.Modified, "Date modified"), (SortKey.Size, "Size"), (SortKey.Type, "Type") })
        {
            var radio = new RadioMenuFlyoutItem { Text = label, GroupName = "SortKey", IsChecked = FileList.CurrentSortKey == key };
            SortKey chosen = key;
            radio.Click += (_, _) => FileList.SetSort(chosen, FileList.SortAscending);
            sort.Items.Add(radio);
        }

        sort.Items.Add(new MenuFlyoutSeparator());
        foreach ((bool ascending, string label) in new[] { (true, "Ascending"), (false, "Descending") })
        {
            var radio = new RadioMenuFlyoutItem { Text = label, GroupName = "SortOrder", IsChecked = FileList.SortAscending == ascending };
            bool chosen = ascending;
            radio.Click += (_, _) => FileList.SetSort(FileList.CurrentSortKey, chosen);
            sort.Items.Add(radio);
        }

        menu.Items.Add(sort);
        menu.ShowAt(FileList, new FlyoutShowOptions { Position = position });
    }

    // ---------------------------------------------------------------- operations

    private async Task RenameAsync(BrowserEntry entry)
    {
        if (!CanUseContextMenu)
        {
            return;
        }

        bool isFolder = entry.Kind == EntryKind.Folder;
        (string editable, string suffix) = FileNames.SplitForRename(entry.Name, isFolder);
        string folder = Path.GetDirectoryName(entry.FullPath) ?? string.Empty;
        string? name;
        _dialogOpen = true;
        try
        {
            name = await BatchDialogs.AskNameAsync(
                XamlRoot, "Rename", $"New name for \"{entry.Name}\":", editable, suffix, "Rename",
                typed => FileNames.CheckNew(typed, folder, entry.FullPath));
        }
        finally
        {
            _dialogOpen = false;
        }

        if (name is null)
        {
            return;
        }

        EditResult result = FileEditing.Rename(entry.FullPath, name);
        if (!result.Ok)
        {
            ShowError(result.Error ?? "It could not be renamed.");
            return;
        }

        await ShowFolderAsync(_history.Current ?? folder, addToHistory: false);
        FileList.SelectOnly(result.NewPath!);
        ShowMessage($"Renamed to \"{Path.GetFileName(result.NewPath)}\".", InfoBarSeverity.Success);
    }

    private async Task NewFolderAsync()
    {
        if (!CanUseContextMenu || _history.Current is not { } here)
        {
            return;
        }

        string? name;
        _dialogOpen = true;
        try
        {
            name = await BatchDialogs.AskNameAsync(
                XamlRoot, "New folder", "Name of the new folder:", FileNames.UniqueFolderName(here), string.Empty, "Create",
                typed => FileNames.CheckNew(typed, here, renaming: null));
        }
        finally
        {
            _dialogOpen = false;
        }

        if (name is null)
        {
            return;
        }

        EditResult result = FileEditing.CreateFolder(here, name);
        if (!result.Ok)
        {
            ShowError(result.Error ?? "The folder could not be created.");
            return;
        }

        await ShowFolderAsync(here, addToHistory: false);
        FileList.SelectOnly(result.NewPath!);
        ShowMessage($"Created the folder \"{name}\".", InfoBarSeverity.Success);
    }

    private async Task DeleteAsync(IReadOnlyList<BrowserEntry> items)
    {
        if (!CanUseContextMenu || items.Count == 0)
        {
            return;
        }

        int permanent = items.Count(i => !RecycleSupport.IsRecoverable(i.FullPath));
        bool agreed;
        _dialogOpen = true;
        try
        {
            agreed = await BatchDialogs.ConfirmAsync(
                XamlRoot, DeleteWording.Title(items, permanent), DeleteWording.Detail(items, permanent), DeleteWording.PrimaryButton(items.Count, permanent));
        }
        finally
        {
            _dialogOpen = false;
        }

        if (!agreed)
        {
            return;
        }

        FileRecycler.Outcome outcome = await FileRecycler.RecycleAsync(items);
        await ShowFolderAsync(_history.Current ?? Path.GetDirectoryName(items[0].FullPath) ?? StartFolder(), addToHistory: false);
        if (outcome.Failed.Count == 0)
        {
            ShowMessage(DeleteWording.Done(items, outcome.Moved, permanent), InfoBarSeverity.Success);
        }
        else
        {
            ShowError(
                $"{outcome.Moved} removed; {outcome.Failed.Count} could not be: {string.Join(", ", outcome.Failed.Take(3))}"
                + (outcome.Failed.Count > 3 ? ", ..." : string.Empty) + ". They may be open in another app or protected.");
        }
    }

    private void ShowInExplorer(BrowserEntry entry)
    {
        try
        {
            // "/select," shows the item highlighted in its folder; a folder is opened itself.
            var start = new ProcessStartInfo("explorer.exe")
            {
                Arguments = entry.Kind == EntryKind.Folder ? $"\"{entry.FullPath}\"" : $"/select,\"{entry.FullPath}\"",
                UseShellExecute = true,
            };
            Process.Start(start);
        }
        catch (Exception)
        {
            ShowError("File Explorer could not be opened.");
        }
    }

    private void CopyPaths(IReadOnlyList<BrowserEntry> items)
    {
        try
        {
            var data = new DataPackage();
            data.SetText(string.Join(Environment.NewLine, items.Select(i => i.FullPath)));
            Clipboard.SetContent(data);
            ShowMessage(items.Count == 1 ? "Path copied." : $"{items.Count} paths copied.", InfoBarSeverity.Success);
        }
        catch (Exception)
        {
            ShowError("The path could not be copied.");
        }
    }

    // ---------------------------------------------------------------- keyboard

    /// <summary>True when the keyboard focus is in the file list (so Delete and F2 never act while someone types in a box).</summary>
    private bool ListHasFocus()
    {
        DependencyObject? node = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        while (node is not null)
        {
            if (ReferenceEquals(node, FileList))
            {
                return true;
            }

            node = VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private void AddListShortcuts()
    {
        void Add(VirtualKey key, VirtualKeyModifiers modifiers, Action action, bool needsListFocus)
        {
            var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
            accelerator.Invoked += (_, args) =>
            {
                if (!CanUseContextMenu || (needsListFocus && !ListHasFocus()))
                {
                    return; // leave the key to whatever has the focus
                }

                args.Handled = true;
                action();
            };
            KeyboardAccelerators.Add(accelerator);
        }

        Add(VirtualKey.F2, VirtualKeyModifiers.None, () =>
        {
            if (_selection.Count == 1)
            {
                _ = RenameAsync(_selection[0]);
            }
        }, needsListFocus: true);
        Add(VirtualKey.Delete, VirtualKeyModifiers.None, () => _ = DeleteAsync(_selection), needsListFocus: true);
        Add(VirtualKey.N, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, () => _ = NewFolderAsync(), needsListFocus: false);
    }
}
