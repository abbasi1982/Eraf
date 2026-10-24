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
        SetViewMode(ViewMode.Details);
    }

    /// <summary>The user opened an item (double-click or Enter).</summary>
    public event Action<BrowserEntry>? EntryActivated;

    /// <summary>The selection changed. The list holds every selected item (empty when nothing is selected).</summary>
    public event Action<IReadOnlyList<BrowserEntry>>? SelectionChanged;

    /// <summary>The first selected item, or null.</summary>
    public BrowserEntry? Selected => (List.SelectedItem as EntryViewModel)?.Entry;

    /// <summary>All selected items in list order.</summary>
    public IReadOnlyList<BrowserEntry> SelectedEntries =>
        List.SelectedItems.OfType<EntryViewModel>().Select(v => v.Entry).ToList();

    public void SelectAll() => List.SelectAll();

    public void SelectNone() => List.SelectedItems.Clear();

    /// <summary>The column the list is sorted by, and whether A to Z / old to new / small to large.</summary>
    public SortKey CurrentSortKey => _sortKey;

    public bool SortAscending => _ascending;

    public void SetSort(SortKey key, bool ascending)
    {
        _sortKey = key;
        _ascending = ascending;
        UpdateHeaders();
        Rebuild();
    }

    /// <summary>Selects just this item (used to show a renamed file or a new folder).</summary>
    public void SelectOnly(string fullPath)
    {
        EntryViewModel? row = _items.FirstOrDefault(i => string.Equals(i.Entry.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return;
        }

        List.SelectedItems.Clear();
        List.SelectedItems.Add(row);
        List.ScrollIntoView(row);
    }

    /// <summary>
    /// A right-click (or the Menu key) asks for the context menu: where to show it, what is selected, and whether it was on an item.
    /// A right-click on an item that is not selected selects it first, as in File Explorer.
    /// </summary>
    public event Action<global::Windows.Foundation.Point, IReadOnlyList<BrowserEntry>, bool>? ContextMenuRequested;

    private void OnContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        EntryViewModel? row = RowOf(e.OriginalSource);
        if (!e.TryGetPosition(List, out global::Windows.Foundation.Point position))
        {
            // From the keyboard: act on the selected row and show the menu near the top of the list.
            row ??= List.SelectedItem as EntryViewModel;
            position = new global::Windows.Foundation.Point(48, 48);
        }

        if (row is not null && !List.SelectedItems.Contains(row))
        {
            List.SelectedItems.Clear();
            List.SelectedItems.Add(row);
        }

        e.Handled = true;
        ContextMenuRequested?.Invoke(position, SelectedEntries, row is not null);
    }

    /// <summary>How many items the list shows right now (after the name filter).</summary>
    public int Count => _items.Count;

    private ViewMode _mode = ViewMode.Details;

    /// <summary>Switches the layout (Details, Tiles, icons, List). Selection and sort stay; thumbnails are loaded again at the new size.</summary>
    public void SetViewMode(ViewMode mode)
    {
        _mode = mode;
        ViewModeInfo info = ViewModes.For(mode);
        string template = mode switch
        {
            ViewMode.List => "ListTemplate",
            ViewMode.SmallIcons => "SmallTemplate",
            ViewMode.MediumIcons => "MediumTemplate",
            ViewMode.LargeIcons => "LargeTemplate",
            ViewMode.Tiles => "TilesTemplate",
            _ => "DetailsTemplate",
        };
        List.ItemTemplate = (DataTemplate)Resources[template];
        List.ItemsPanel = (ItemsPanelTemplate)Resources[info.IsGrid ? "GridPanel" : "RowsPanel"];
        HeaderGrid.Visibility = mode == ViewMode.Details ? Visibility.Visible : Visibility.Collapsed;
        Rebuild();
    }

    /// <summary>
    /// With checkboxes on, every row has a tick box and a click ticks or unticks it (several can be ticked). Without them
    /// the list works like Explorer: Ctrl and Shift select several. A header box ticks or clears everything.
    /// </summary>
    public void SetCheckboxes(bool on)
    {
        List.SelectionMode = on ? ListViewSelectionMode.Multiple : ListViewSelectionMode.Extended;
        HeaderCheck.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        HeaderGrid.Padding = new Thickness(on ? CheckboxIndent : 12, 0, 12, 4);
    }

    /// <summary>How far the row contents move to the right when each row shows its own checkbox.</summary>
    private const double CheckboxIndent = 35;

    private bool _syncingHeader;

    private void OnHeaderChecked(object sender, RoutedEventArgs e)
    {
        if (!_syncingHeader)
        {
            List.SelectAll();
        }
    }

    private void OnHeaderUnchecked(object sender, RoutedEventArgs e)
    {
        if (!_syncingHeader)
        {
            List.SelectedItems.Clear();
        }
    }

    private void SyncHeaderCheck()
    {
        _syncingHeader = true;
        try
        {
            HeaderCheck.IsChecked = _items.Count > 0 && List.SelectedItems.Count == _items.Count;
        }
        finally
        {
            _syncingHeader = false;
        }
    }

    /// <summary>The row an event came from. With checkboxes a double-click ticks twice, so the selection cannot say which row was meant.</summary>
    private static EntryViewModel? RowOf(object? source)
    {
        DependencyObject? node = source as DependencyObject;
        while (node is not null)
        {
            if (node is ListViewItem { Content: EntryViewModel row })
            {
                return row;
            }

            node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
        }

        return null;
    }

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
        var keep = SelectedEntries.Select(e => e.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<BrowserEntry> shown = BrowserList.Sort(BrowserList.Filter(_all, _filter), _sortKey, _ascending);

        _items.Clear();
        foreach (BrowserEntry entry in shown)
        {
            _items.Add(new EntryViewModel(entry));
        }

        foreach (EntryViewModel again in _items.Where(i => keep.Contains(i.Entry.FullPath)))
        {
            List.SelectedItems.Add(again);
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

        SyncHeaderCheck();
        SelectionChanged?.Invoke(SelectedEntries);
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

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncHeaderCheck();
        SelectionChanged?.Invoke(SelectedEntries);
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((RowOf(e.OriginalSource) ?? List.SelectedItem as EntryViewModel) is { } row)
        {
            EntryActivated?.Invoke(row.Entry);
        }
    }

    private void OnListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && (RowOf(e.OriginalSource) ?? List.SelectedItem as EntryViewModel) is { } row)
        {
            e.Handled = true;
            EntryActivated?.Invoke(row.Entry);
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
        _ = LoadThumbnailAsync(vm, ViewModes.For(_mode).ThumbnailPixels);
    }

    private static async Task LoadThumbnailAsync(EntryViewModel vm, int pixels)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(vm.Entry.FullPath);
            using StorageItemThumbnail? thumbnail = await file.GetThumbnailAsync(ThumbnailMode.PicturesView, (uint)pixels, ThumbnailOptions.UseCurrentScale);
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
