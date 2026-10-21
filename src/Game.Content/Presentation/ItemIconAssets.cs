using System.Collections.Immutable;
using System.Text;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record ItemIcon(ItemId Item, int Width, int Height, string PngPath, ImageRegion? Region = null);

/// <summary>Item icon metadata, independent of item definitions and machine compatibility.</summary>
public static class ItemIconAssets
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private sealed record Document(int SchemaVersion, ImmutableArray<ItemIcon> Icons);

    public static ImmutableArray<ItemIcon> Load(string json)
    {
        var document = ContentJson.ParseStrict<Document>(json, ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.Icons.IsDefaultOrEmpty)
            throw new InvalidDataException("Item icon document differs.");
        foreach (var icon in document.Icons) Validate(icon);
        if (document.Icons.Select(icon => icon.Item).Distinct().Count() != document.Icons.Length)
            throw new InvalidDataException("Duplicate item icon.");
        return document.Icons;
    }

    public static ImmutableArray<ItemIcon> LoadProject(string root, string relativePath, params ItemId[] requiredItems)
    {
        var path = GeneratedResourceFile.SafePath(root, relativePath);
        if (new FileInfo(path).Length > ContentJson.MaximumJsonBytes)
            throw new InvalidDataException("Item icon file exceeds the JSON size limit: " + relativePath);
        var icons = Load(Utf8.GetString(File.ReadAllBytes(path)));
        // Bind the identities that a consumer actually displays, not a historical regional catalog.
        return requiredItems.Length == 0 ? icons : requiredItems.Select(item =>
            icons.SingleOrDefault(icon => icon.Item == item) ??
                throw new InvalidDataException("Required item icon missing: " + item.Value)).ToImmutableArray();
    }

    public static void Validate(ItemIcon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        if (string.IsNullOrWhiteSpace(icon.Item.Value) || icon.Width != 24 || icon.Height != 24 ||
            string.IsNullOrWhiteSpace(icon.PngPath) || Path.IsPathRooted(icon.PngPath) ||
            icon.PngPath.Contains('\\') || icon.PngPath.Contains(':') ||
            icon.PngPath.Split('/').Any(part => part is "" or "." or "..") ||
            !icon.PngPath.EndsWith(".png", StringComparison.Ordinal))
            throw new InvalidDataException("Item icon metadata differs.");
        if (icon.Region is {} region &&
            (region.X < 0 || region.Y < 0 || region.Width != icon.Width || region.Height != icon.Height))
            throw new InvalidDataException("Item icon atlas region differs.");
    }
}
