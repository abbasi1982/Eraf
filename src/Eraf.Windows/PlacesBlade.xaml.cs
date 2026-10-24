using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Eraf.Windows;

/// <summary>The folder tree. It only reports which folder the user chose; the page decides what to do.</summary>
public sealed partial class PlacesBlade : UserControl
{
    // A node's content is just its name (the tree shows plain text). What it stands for is looked up here,
    // because the tree's data-template binding cannot take a plain C# class.
    private readonly Dictionary<TreeViewNode, PlaceNode> _places = new();

    public PlacesBlade()
    {
        InitializeComponent();
        Build();
    }

    /// <summary>Raised with the full path of the folder the user chose.</summary>
    public event Action<string>? FolderChosen;

    private void Build()
    {
        TreeViewNode quick = Node(new PlaceNode("Quick access", string.Empty, "\uE734"));
        quick.IsExpanded = true;
        foreach ((string name, string path) in Places.QuickAccess())
        {
            quick.Children.Add(Folder(name, path, QuickGlyph(name)));
        }

        TreeViewNode thisPc = Node(new PlaceNode("This PC", string.Empty, "\uE7F8"));
        thisPc.IsExpanded = true;
        foreach ((string name, string path) in Places.Drives())
        {
            thisPc.Children.Add(Folder(name, path, "\uEDA2"));
        }

        Tree.RootNodes.Add(quick);
        Tree.RootNodes.Add(thisPc);
    }

    private static string QuickGlyph(string name) => name switch
    {
        "Desktop" => "\uE8FC",
        "Documents" => "\uE8A5",
        "Pictures" => "\uE91B",
        "Music" => "\uE8D6",
        _ => "\uE896",
    };

    private TreeViewNode Folder(string name, string path, string glyph)
    {
        TreeViewNode node = Node(new PlaceNode(name, path, glyph));
        node.HasUnrealizedChildren = true;
        return node;
    }

    private TreeViewNode Node(PlaceNode place)
    {
        var node = new TreeViewNode { Content = place.Name };
        _places[node] = place;
        return node;
    }

    /// <summary>Subfolders are read only when a folder is first opened, so a big drive never slows start-up.</summary>
    private async void OnExpanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        TreeViewNode node = args.Node;
        if (!node.HasUnrealizedChildren || !_places.TryGetValue(node, out PlaceNode? place) || place.Path.Length == 0)
        {
            return;
        }

        node.HasUnrealizedChildren = false;
        List<(string Name, string Path)> folders = await Task.Run(() => FolderLister.ListSubfolders(place.Path));
        foreach ((string name, string path) in folders)
        {
            node.Children.Add(Folder(name, path, "\uE8B7"));
        }
    }

    private void OnItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode node && _places.TryGetValue(node, out PlaceNode? place) && place.Path.Length > 0)
        {
            FolderChosen?.Invoke(place.Path);
        }
    }
}
