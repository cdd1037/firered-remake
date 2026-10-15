using System.Collections.ObjectModel;
using System.Text.Json;

namespace Game.Content;

public sealed record FameCheckerTexture(string Id, int Width, int Height, string TexturePath, ImageRegion Region);
public sealed record FameCheckerObjectIcon(string Symbol, int SourceId, string AssetId, int X, int Y, int Width, int Height, int PaletteSlot);

/// <summary>Original presentation only. Unlocks, selection, input and save state belong to Domain/Application.</summary>
public sealed class FameCheckerPresentationData
{
    public JsonElement Manifest { get; }
    public JsonElement Layout { get; }
    public JsonElement Palettes { get; }
    public JsonElement Cues => Manifest.GetProperty("audio").GetProperty("cues");
    public IReadOnlyDictionary<string, FameCheckerTexture> Textures { get; }
    public IReadOnlyDictionary<string, FameCheckerObjectIcon> ObjectIcons { get; }

    internal FameCheckerPresentationData(JsonElement manifest, JsonElement layout, JsonElement palettes)
    {
        Manifest = manifest.Clone();
        Layout = layout.Clone();
        Palettes = palettes.GetProperty("palettes").Clone();
        Textures = new ReadOnlyDictionary<string, FameCheckerTexture>(manifest.GetProperty("assets").EnumerateArray().ToDictionary(
            r => r.GetProperty("id").GetString()!, r => new FameCheckerTexture(r.GetProperty("id").GetString()!,
                r.GetProperty("width").GetInt32(), r.GetProperty("height").GetInt32(),
                r.GetProperty("texturePath").GetString()!, new ImageRegion(
                    r.GetProperty("region").GetProperty("x").GetInt32(), r.GetProperty("region").GetProperty("y").GetInt32(),
                    r.GetProperty("region").GetProperty("width").GetInt32(), r.GetProperty("region").GetProperty("height").GetInt32())), StringComparer.Ordinal));
        ObjectIcons = new ReadOnlyDictionary<string, FameCheckerObjectIcon>(manifest.GetProperty("objectIcons").EnumerateArray().ToDictionary(
            r => r.GetProperty("symbol").GetString()!, r => new FameCheckerObjectIcon(r.GetProperty("symbol").GetString()!,
                r.GetProperty("sourceId").GetInt32(), r.GetProperty("assetId").GetString()!, r.GetProperty("x").GetInt32(),
                r.GetProperty("y").GetInt32(), r.GetProperty("width").GetInt32(), r.GetProperty("height").GetInt32(),
                r.GetProperty("paletteSlot").GetInt32()), StringComparer.Ordinal));
    }

    public FameCheckerTexture Portrait(int sourceIndex, bool silhouette = false)
    {
        if ((uint)sourceIndex >= 16) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        return Textures["portrait-" + sourceIndex + (silhouette ? "-silhouette" : "")];
    }

    public FameCheckerTexture FactIcon(string sourceSymbol, bool dimmed = false)
    {
        if (!ObjectIcons.ContainsKey(sourceSymbol)) throw new ArgumentException("Unknown Fame Checker fact icon.", nameof(sourceSymbol));
        return Textures[sourceSymbol + (dimmed ? "-dimmed" : "")];
    }

    public FameCheckerTexture CursorFrame(int frame)
    {
        if ((uint)frame >= 2) throw new ArgumentOutOfRangeException(nameof(frame));
        return Textures["cursor-" + frame];
    }

    public FameCheckerTexture BaseBackground(bool pickMode = false, bool restoredFromPick = false) =>
        Textures[pickMode ? "base-pick" : restoredFromPick ? "base-restored" : "base"];

    public FameCheckerTexture Overlay(int infoState = 5)
    {
        if ((uint)infoState > 5) throw new ArgumentOutOfRangeException(nameof(infoState));
        return Textures["overlay-" + infoState];
    }

}

/// <summary>Generated Fame Checker texture regions, layout and presentation cues.</summary>
public static class PretFameCheckerPresentation
{
    public const string Base = "content/generated/ui/fame-checker-presentation/";
    public const string Profile = "firered-fame-checker-presentation-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    public static FameCheckerPresentationData Load(string root)
    {
        byte[] Read(string path)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") ||
                !path.StartsWith(Base, StringComparison.Ordinal))
                throw new InvalidDataException("Unsafe Fame Checker resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Fame Checker resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Fame Checker resource directory symlink rejected.");
            if (!file.Exists) throw new InvalidDataException("Fame Checker resource missing: " + path);
            var bytes = File.ReadAllBytes(file.FullName);
            return bytes;
        }
        using var document = JsonDocument.Parse(Read(Base + "manifest.json"));
        var manifest = document.RootElement;
        if (manifest.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("Unsupported Fame Checker presentation schema.");
        if (manifest.GetProperty("portraits").GetArrayLength() != 16 ||
            manifest.GetProperty("factIconSymbols").GetArrayLength() != 96)
            throw new InvalidDataException("Fame Checker presentation inventory differs.");
        foreach (var asset in manifest.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("width").GetInt32() <= 0 || asset.GetProperty("height").GetInt32() <= 0)
                throw new InvalidDataException("Fame Checker image metadata differs.");
        }
        using var layout = JsonDocument.Parse(Read(Base + "layout.json"));
        using var palettes = JsonDocument.Parse(Read(Base + "palettes.json"));
        if (            manifest.GetProperty("audio").GetProperty("cues").GetArrayLength() != 5)
            throw new InvalidDataException("Fame Checker audio cue table differs.");
        return new(manifest, layout.RootElement, palettes.RootElement);
    }
}
