using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Game.Content;

/// <summary>Three complete replacement BG layers for each open stage of a field door.</summary>
public sealed record FieldDoorArtEntry(string Layout, int MetatileId, string SoundEffect, int Width, int Height, int AtlasY)
{
    public ImageRegion FrameRegion(int openFrame, int layer)
    {
        if (openFrame is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(openFrame));
        if (layer is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(layer));
        return new(layer * Width, AtlasY + openFrame * Height, Width, Height);
    }
}

/// <summary>Immutable generated layout/metatile art; no raw files or map-name dispatch.</summary>
public sealed class GeneratedFieldDoorArt
{
    private sealed record Document(int SchemaVersion, string TexturePath, int Width, int Height,
        ImmutableArray<FieldDoorArtEntry> Entries);
    private readonly ImmutableDictionary<(string Layout, int Metatile), FieldDoorArtEntry> _entries;
    public string TexturePath { get; }
    public ImmutableArray<FieldDoorArtEntry> Entries { get; }

    private GeneratedFieldDoorArt(Document document)
    {
        TexturePath = document.TexturePath;
        Entries = document.Entries;
        _entries = Entries.ToImmutableDictionary(entry => (entry.Layout, entry.MetatileId));
    }

    public static GeneratedFieldDoorArt Load(string generatedRoot)
    {
        var document = ContentJson.ParseStrict<Document>(
            GeneratedResourceFile.Read(generatedRoot, "maps/door-animations.json"), ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.TexturePath != "maps/door-animations.png" ||
            document.Width != 48 || document.Height is < 48 or > 65520 || document.Height % 48 != 0 ||
            document.Entries.IsDefault || document.Entries.Any(entry => entry is null ||
                string.IsNullOrWhiteSpace(entry.Layout) || entry.MetatileId is < 0 or > 1023 ||
                entry.SoundEffect is not ("SE_DOOR" or "SE_SLIDING_DOOR") ||
                entry.Width != 16 || entry.Height is not (16 or 32) || entry.AtlasY < 0 || entry.AtlasY % 48 != 0 ||
                entry.AtlasY > document.Height - 3 * entry.Height) ||
            document.Entries.Select(entry => (entry.Layout, entry.MetatileId)).Distinct().Count() != document.Entries.Length)
            throw new InvalidDataException("Invalid generated field door art bindings.");
        byte[] png = GeneratedResourceFile.Read(generatedRoot, document.TexturePath);
        if (png.Length < 33 || !png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            !png.AsSpan(12, 4).SequenceEqual("IHDR"u8) ||
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)) != document.Width ||
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)) != document.Height || png[24] != 8 || png[25] != 6)
            throw new InvalidDataException("Invalid generated field door PNG.");
        return new(document);
    }

    public FieldDoorArtEntry? Find(string layout, int metatile) => _entries.GetValueOrDefault((layout, metatile));
}
