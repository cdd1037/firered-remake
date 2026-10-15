using System.Collections.ObjectModel;
using System.Text.Json;

namespace Game.Content;

public sealed record SlotMachineTexture(string Id, int Width, int Height, string TexturePath, ReadOnlyMemory<byte> Png, ImageRegion Region);

/// <summary>Prepared native slot image descriptors, layout and cue bindings.</summary>
public sealed class SlotMachinePresentationData
{
    public JsonElement Manifest { get; }
    public JsonElement Layout { get; }
    public JsonElement Texts { get; }
    public JsonElement Cues => Manifest.GetProperty("audio").GetProperty("cues");
    public IReadOnlyDictionary<string, SlotMachineTexture> Textures { get; }

    internal SlotMachinePresentationData(JsonElement manifest, JsonElement layout, Dictionary<string, byte[]> bytes)
    {
        Manifest = manifest.Clone();
        Layout = layout.GetProperty("layout").Clone();
        Texts = layout.GetProperty("texts").Clone();
        Textures = new ReadOnlyDictionary<string, SlotMachineTexture>(manifest.GetProperty("assets").EnumerateArray().ToDictionary(
            r => r.GetProperty("id").GetString()!, r => new SlotMachineTexture(r.GetProperty("id").GetString()!,
                r.GetProperty("width").GetInt32(), r.GetProperty("height").GetInt32(),
                r.GetProperty("texturePath").GetString()!, bytes[r.GetProperty("texturePath").GetString()!],
                new ImageRegion(r.GetProperty("region").GetProperty("x").GetInt32(), r.GetProperty("region").GetProperty("y").GetInt32(),
                    r.GetProperty("width").GetInt32(), r.GetProperty("height").GetInt32())), StringComparer.Ordinal));
    }

}

/// <summary>Loads slot machine art, layout and audio cue bindings.</summary>
public static class PretSlotMachinePresentation
{
    public const string Base = "content/generated/slot-machine-presentation/";
    public const string Profile = "firered-slot-machine-presentation-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    public static SlotMachinePresentationData Load(string root)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") )
                throw new InvalidDataException("Unsafe slot resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Slot resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Slot resource directory symlink rejected.");
            var bytes = File.ReadAllBytes(file.FullName);
            return bytes;
        }
        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported slot presentation schema.");
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path));
            if (path.EndsWith(".json", StringComparison.Ordinal)) using (JsonDocument.Parse(files[path])) { }
        }
        foreach (var asset in manifest.GetProperty("assets").EnumerateArray())
        {
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            if (width is <= 0 or > 1024 || height is <= 0 or > 1024)
                throw new InvalidDataException("Slot texture dimensions differ.");
        }
        using var layout = JsonDocument.Parse(files[Base + "layout.json"]);
        return new(manifest, layout.RootElement, files);
    }
}
