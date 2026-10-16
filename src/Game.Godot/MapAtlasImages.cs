using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.Json;
using Godot;

/// <summary>Reconstructs active map layers from regional tiles; the map LRU owns the resulting GPU texture.</summary>
internal static class MapAtlasImages
{
    private sealed record Page(string Path, int Width, int Height, int TileCount);
    private sealed record Layer(string Pack, int Width, int Height, long Offset, int Length);
    private sealed record Catalog(int SchemaVersion, int TileSize, int PageColumns, int TilesPerPage,
        Dictionary<string, Page[]> Pages, Dictionary<string, Layer> Images);
    private static readonly Dictionary<string, Catalog> Catalogs = new(StringComparer.Ordinal);

    internal static Image Load(string logicalPath)
    {
        var absolute = Path.GetFullPath(logicalPath);
        var generated = new DirectoryInfo(Path.GetDirectoryName(absolute)!);
        while (generated is not null && generated.Name != "generated") generated = generated.Parent;
        if (generated is null) throw new InvalidDataException("Map texture is outside generated resources: " + logicalPath);
        var directory = Path.Combine(generated.FullName, "maps/atlases");
        if (!Catalogs.TryGetValue(directory, out var catalog))
        {
            catalog = JsonSerializer.Deserialize<Catalog>(File.ReadAllBytes(Path.Combine(directory, "catalog.json")),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Missing map atlas catalog.");
            if (catalog.SchemaVersion != 1 || catalog.TileSize != 16 || catalog.PageColumns != 64 || catalog.TilesPerPage != 4096)
                throw new InvalidDataException("Unsupported map atlas format.");
            Catalogs.Add(directory, catalog);
        }
        var key = Path.GetRelativePath(generated.FullName, absolute).Replace(Path.DirectorySeparatorChar, '/');
        if (!catalog.Images.TryGetValue(key, out var layer)) throw new InvalidDataException("Missing packed map layer: " + key);
        if (layer.Width <= 0 || layer.Height <= 0 || layer.Width % 16 != 0 || layer.Height % 16 != 0)
            throw new InvalidDataException("Invalid packed map dimensions: " + key);
        var compressed = new byte[layer.Length];
        using (var stream = File.OpenRead(Path.Combine(directory, "indices.bin")))
        {
            stream.Position = layer.Offset;
            stream.ReadExactly(compressed);
        }
        var indices = new byte[checked(layer.Width / 16 * (layer.Height / 16) * 2)];
        using (var input = new MemoryStream(compressed))
        using (var inflater = new ZLibStream(input, CompressionMode.Decompress))
        {
            inflater.ReadExactly(indices);
            if (inflater.ReadByte() != -1) throw new InvalidDataException("Packed map tile count differs: " + key);
        }
        var result = Image.CreateEmpty(layer.Width, layer.Height, false, Image.Format.Rgba8);
        var loaded = new Dictionary<int, Image>();
        try
        {
            var pages = catalog.Pages[layer.Pack];
            int columns = layer.Width / 16;
            for (int i = 0; i < indices.Length / 2; i++)
            {
                int tile = BinaryPrimitives.ReadUInt16LittleEndian(indices.AsSpan(i * 2, 2));
                int pageIndex = tile / 4096, local = tile % 4096;
                if (pageIndex >= pages.Length || local >= pages[pageIndex].TileCount)
                    throw new InvalidDataException("Packed map tile is outside its page: " + key);
                if (!loaded.TryGetValue(pageIndex, out var image))
                {
                    var page = pages[pageIndex];
                    if (Path.GetFileName(page.Path) != page.Path) throw new InvalidDataException("Unsafe map atlas page path.");
                    image = NativeImages.LoadPng(File.ReadAllBytes(Path.Combine(directory, page.Path)), page.Width, page.Height);
                    loaded.Add(pageIndex, image);
                }
                result.BlitRect(image, new Rect2I(local % 64 * 16, local / 64 * 16, 16, 16), new Vector2I(i % columns * 16, i / columns * 16));
            }
            return result;
        }
        catch { result.Dispose(); throw; }
        finally { foreach (var image in loaded.Values) image.Dispose(); }
    }
}
