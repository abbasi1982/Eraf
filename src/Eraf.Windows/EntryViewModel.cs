using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Eraf.Windows;

/// <summary>One row of the file list as the UI binds to it. The thumbnail arrives later, so it raises a change.</summary>
public sealed class EntryViewModel : INotifyPropertyChanged
{
    private ImageSource? _thumbnail;

    public EntryViewModel(BrowserEntry entry) => Entry = entry;

    public BrowserEntry Entry { get; }

    public bool ThumbnailRequested { get; set; }

    public string Name => Entry.Name;

    public string ModifiedText => Entry.ModifiedLocal == DateTime.MinValue ? string.Empty : Entry.ModifiedLocal.ToString("g");

    public string SizeText => Entry.Kind == EntryKind.Folder ? string.Empty : BrowserList.FormatSize(Entry.SizeBytes);

    public string TypeText => FileKinds.TypeLabel(Entry.Kind);

    /// <summary>Segoe Fluent Icons glyph shown when there is no thumbnail.</summary>
    public string Glyph => Entry.Kind switch
    {
        EntryKind.Folder => "\uE8B7",
        EntryKind.Eraf => "\uE8A5",
        _ => "\uE91B",
    };

    /// <summary>What a screen reader says for the whole row.</summary>
    public string SpokenName => Entry.Kind == EntryKind.Folder
        ? $"{Name}, folder, modified {ModifiedText}"
        : $"{Name}, {TypeText}, {SizeText}, modified {ModifiedText}";

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            _thumbnail = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThumbnailVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GlyphVisibility)));
        }
    }

    public Visibility ThumbnailVisibility => _thumbnail is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility GlyphVisibility => _thumbnail is null ? Visibility.Visible : Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>A node in the folder tree. <see cref="ToString"/> is the name so a screen reader and the default template read it.</summary>
public sealed class PlaceNode
{
    public PlaceNode(string name, string path, string glyph)
    {
        Name = name;
        Path = path;
        Glyph = glyph;
    }

    public string Name { get; }

    public string Path { get; }

    public string Glyph { get; }

    public override string ToString() => Name;
}
