using System.Collections.ObjectModel;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;

namespace Eraf.Windows;

/// <summary>
/// The details list for the current folder. It owns sorting and the name filter, loads photo thumbnails as rows appear,
/// and reports opening (double-click or Enter) and selection; it never decides what opening means.
/// </summary>
public sealed partial class FileListPane : UserControl
{
    private readonly ObservableCollection<EntryViewModel> _items = new();
    private IReadOnlyList<BrowserEntry> _all = Array.Empty<BrowserEntry>();
    private SortKey _sortKey = SortKey.Name;
    private bool _ascending = true;
    private string _filter = string.Empty;
    private string? _error;
    private string _emptyHint = string.Empty;

    public FileListPane()
    {
        InitializeComponent();
        List.ItemsSource = _items;
        UpdateHeaders();
    }

    /// <summary>The user opened an item (double-click or Enter).</summary>
    public event Action<BrowserEntry>? EntryActivated;

    /// <summary>The selected item changed (null when nothing is selected).</summary>
    public event Action<BrowserEntry?>? EntrySelected;

    public BrowserEntry? Selected => (List.SelectedItem as EntryViewModel)?.Entry;

    /// <summary>Replaces the list with a folder's contents. <paramref name="error"/> is shown instead of the empty-folder hint.</summary>
    public void SetListing(IReadOnlyList<BrowserEntry> entries, string? error, string emptyHint)
    {
        _all = entries;
        _error = error;
        _emptyHint = emptyHint;
        Rebuild();
    }

    public void SetFilter(string? text)
    {
        _filter = text ?? string.Empty;
        Rebuild();
    }

    public void FocusList()
    {
        if (_items.Count > 0 && List.SelectedItem is null)
        {
            List.SelectedIndex = 0;
        }

        List.Focus(FocusState.Programmatic);
    }

    private void Rebuild()
    {
        string? keep = Selected?.FullPath;
        List<BrowserEntry> shown = BrowserList.Sort(BrowserList.Filter(_all, _filter), _sortKey, _ascending);

        _items.Clear();
        foreach (BrowserEntry entry in shown)
        {
            _items.Add(new EntryViewModel(entry));
        }

        if (keep is not null)
        {
            EntryViewModel? again = _items.FirstOrDefault(i => i.Entry.FullPath == keep);
            if (again is not null)
            {
                List.SelectedItem = again;
            }
        }

        if (_items.Count > 0)
        {
            EmptyText.Visibility = Visibility.Collapsed;
        }
        else
        {
            EmptyText.Text = _error
                ?? (_filter.Trim().Length > 0 ? "No items match what you typed." : _emptyHint);
            EmptyText.Visibility = Visibility.Visible;
        }

        EntrySelected?.Invoke(Selected);
    }

    private void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        var key = Enum.Parse<SortKey>((string)((Button)sender).Tag);
        if (key == _sortKey)
        {
            _ascending = !_ascending;
        }
        else
        {
            _sortKey = key;
            _ascending = true;
        }

        UpdateHeaders();
        Rebuild();
    }

    private void UpdateHeaders()
    {
        void Set(Button button, string label, SortKey key)
        {
            string arrow = key == _sortKey ? (_ascending ? "  ▲" : "  ▼") : string.Empty;
            button.Content = label + arrow;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                button,
                key == _sortKey ? $"Sort by {label}, currently {(_ascending ? "ascending" : "descending")}" : $"Sort by {label}");
        }

        Set(NameHeader, "Name", SortKey.Name);
        Set(ModifiedHeader, "Date modified", SortKey.Modified);
        Set(SizeHeader, "Size", SortKey.Size);
        Set(TypeHeader, "Type", SortKey.Type);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => EntrySelected?.Invoke(Selected);

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (Selected is { } entry)
        {
            EntryActivated?.Invoke(entry);
        }
    }

    private void OnListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && Selected is { } entry)
        {
            e.Handled = true;
            EntryActivated?.Invoke(entry);
        }
    }

    /// <summary>Photo thumbnails load when their row first appears, so a folder of thousands never loads them all.</summary>
    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.Item is not EntryViewModel vm || vm.Entry.Kind != EntryKind.Photo || vm.ThumbnailRequested)
        {
            return;
        }

        vm.ThumbnailRequested = true;
        _ = LoadThumbnailAsync(vm);
    }

    private static async Task LoadThumbnailAsync(EntryViewModel vm)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(vm.Entry.FullPath);
            using StorageItemThumbnail? thumbnail = await file.GetThumbnailAsync(ThumbnailMode.PicturesView, 72, ThumbnailOptions.UseCurrentScale);
            if (thumbnail is null || thumbnail.Size == 0)
            {
                return;
            }

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(thumbnail);
            vm.Thumbnail = bitmap;
        }
        catch (Exception)
        {
            // No thumbnail: the row keeps its icon.
        }
    }
}
