using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record FameCheckerTexture(string Id, int Width, int Height, ReadOnlyMemory<byte> Rgba, ReadOnlyMemory<byte> Png);
public sealed record FameCheckerObjectIcon(string Symbol, int SourceId, string AssetId, int X, int Y, int Width, int Height, int PaletteSlot);

/// <summary>Original presentation only. Unlocks, selection, input and save state belong to Domain/Application.</summary>
public sealed class FameCheckerPresentationData
{
    private readonly IReadOnlyDictionary<string, byte[]> files;
    public JsonElement Manifest { get; }
    public JsonElement Layout { get; }
    public JsonElement Palettes { get; }
    public JsonElement Cues => Manifest.GetProperty("audio").GetProperty("cues");
    public IReadOnlyDictionary<string, FameCheckerTexture> Textures { get; }
    public IReadOnlyDictionary<string, FameCheckerObjectIcon> ObjectIcons { get; }

    internal FameCheckerPresentationData(JsonElement manifest, JsonElement layout, JsonElement palettes, Dictionary<string, byte[]> bytes)
    {
        Manifest = manifest.Clone();
        Layout = layout.Clone();
        Palettes = palettes.GetProperty("palettes").Clone();
        files = new ReadOnlyDictionary<string, byte[]>(bytes);
        Textures = new ReadOnlyDictionary<string, FameCheckerTexture>(manifest.GetProperty("assets").EnumerateArray().ToDictionary(
            r => r.GetProperty("id").GetString()!, r => new FameCheckerTexture(r.GetProperty("id").GetString()!,
                r.GetProperty("width").GetInt32(), r.GetProperty("height").GetInt32(),
                bytes[r.GetProperty("rgbaPath").GetString()!], bytes[r.GetProperty("texturePath").GetString()!]), StringComparer.Ordinal));
        ObjectIcons = new ReadOnlyDictionary<string, FameCheckerObjectIcon>(manifest.GetProperty("objectIcons").EnumerateArray().ToDictionary(
            r => r.GetProperty("symbol").GetString()!, r => new FameCheckerObjectIcon(r.GetProperty("symbol").GetString()!,
                r.GetProperty("sourceId").GetInt32(), r.GetProperty("assetId").GetString()!, r.GetProperty("x").GetInt32(),
                r.GetProperty("y").GetInt32(), r.GetProperty("width").GetInt32(), r.GetProperty("height").GetInt32(),
                r.GetProperty("paletteSlot").GetInt32()), StringComparer.Ordinal));
    }

    /// <summary>Source 64×64 portrait, including the original palette replacement for silhouettes.</summary>
    public FameCheckerTexture Portrait(int sourceIndex, bool silhouette = false)
    {
        if ((uint)sourceIndex >= 16) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        var original = Textures["portrait-" + sourceIndex];
        if (!silhouette) return original;
        byte[] indices = files[PretFameCheckerPresentation.Base + original.Id + ".indices"], pixels = new byte[64 * 64 * 4];
        var colors = Palettes.GetProperty("silhouette");
        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i];
            for (int c = 0; c < 3; c++) pixels[i * 4 + c] = colors[index][c].GetByte();
            pixels[i * 4 + 3] = index == 0 ? (byte)0 : (byte)255;
        }
        return new(original.Id + "-silhouette", 64, 64, pixels, ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>Original south-facing object frame. Its source placement is centered X, bottom Y.</summary>
    public FameCheckerTexture FactIcon(string sourceSymbol, bool dimmed = false)
    {
        ArgumentNullException.ThrowIfNull(sourceSymbol);
        if (!ObjectIcons.TryGetValue(sourceSymbol, out var icon)) throw new ArgumentException("Unknown Fame Checker fact icon.", nameof(sourceSymbol));
        var result = Crop(Textures[icon.AssetId], icon.X, icon.Y, icon.Width, icon.Height, icon.Symbol);
        if (!dimmed) return result;
        // BLDCNT semi-transparent OBJ uses EVA=7/EVB=0. GBA blending truncates in
        // five-bit channels before expansion; ordinary alpha-over changes the colors.
        byte[] pixels = result.Rgba.ToArray();
        for (int i = 0; i < pixels.Length; i += 4)
            for (int c = 0; c < 3; c++)
            {
                int value = (pixels[i + c] >> 3) * 7 >> 4;
                pixels[i + c] = (byte)((value << 3) | (value >> 2));
            }
        return result with { Id = result.Id + "-dimmed", Rgba = pixels };
    }

    public FameCheckerTexture CursorFrame(int frame)
    {
        if ((uint)frame >= 2) throw new ArgumentOutOfRangeException(nameof(frame));
        return Crop(Textures["cursor"], 0, frame * 32, 32, 32, "cursor-" + frame);
    }

    private static FameCheckerTexture Crop(FameCheckerTexture sheet, int x, int y, int width, int height, string id)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int row = 0; row < height; row++)
            sheet.Rgba.Span.Slice(((y + row) * sheet.Width + x) * 4, width * 4).CopyTo(pixels.AsSpan(row * width * 4));
        return new(id, width, height, pixels, ReadOnlyMemory<byte>.Empty);
    }

    /// <summary>Compose original BG3/BG2/BG1 with source tile edits. BG0 windows/text and sprites are separate.
    /// A pickOffset from 0 to 240 exposes the source sliding background without baking animation atlases.</summary>
    public byte[] RenderBackground(bool pickMode = false, int infoBoxState = 4, int? pickOffset = null, bool restoredFromPick = false)
        => RenderBackgroundLayers(pickMode, infoBoxState, pickOffset, restoredFromPick, true, true);

    /// <summary>Opaque BG3/BG2. Draw ordinary fact objects above this, before the BG1 overlay.</summary>
    public byte[] RenderBaseBackground(bool pickMode = false, bool restoredFromPick = false)
        => RenderBackgroundLayers(pickMode, 4, null, restoredFromPick, true, false);

    /// <summary>Transparent BG1 only, including the source info-box state and incoming PICK panel.
    /// Draw above fact objects and below portraits. PICK transitions use infoState 5 and offset 0–240.</summary>
    public byte[] RenderPickOverlay(int offset, int infoState = 5)
        => RenderBackgroundLayers(false, infoState, offset, false, false, true);

    private byte[] RenderBackgroundLayers(bool pickMode, int infoBoxState, int? pickOffset, bool restoredFromPick, bool includeBase, bool includeOverlay)
    {
        if ((uint)infoBoxState > 5 || pickOffset is < 0 or > 240) throw new ArgumentOutOfRangeException(nameof(infoBoxState));
        int offset = pickOffset ?? (pickMode ? 240 : 0);
        ushort[] ReadMap(int number)
        {
            var data = files[PretFameCheckerPresentation.Base + "bg" + number + ".tilemap"];
            return Enumerable.Range(0, 1024).Select(i => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i * 2))).ToArray();
        }
        var bg3 = ReadMap(3);
        var bg2 = ReadMap(2);
        var source1 = ReadMap(1);
        var bg1 = new ushort[64 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) bg1[y * 64 + x + 30] = source1[y * 32 + x];
        void Update(ushort[] target, int width, int state)
        {
            foreach (var row in Layout.GetProperty("infoBox").GetProperty("updates").GetProperty(state.ToString()).EnumerateArray())
                for (int y = row[2].GetInt32(); y < row[2].GetInt32() + row[4].GetInt32(); y++)
                    for (int x = row[1].GetInt32(); x < row[1].GetInt32() + row[3].GetInt32(); x++)
                        target[y * width + x] = (ushort)(row[0].GetInt32() | row[5].GetInt32() << 12);
        }
        // Initial loading keeps tilemap2 verbatim. Exiting pick mode explicitly writes state 2;
        // three right-edge tiles then change from palette 0 to 1 in the original source.
        if (pickMode || restoredFromPick) Update(bg2, 32, pickMode ? 4 : 2);
        Update(bg1, 64, pickMode ? 5 : infoBoxState);
        byte[] result = new byte[240 * 160 * 4];
        var tiles = files[PretFameCheckerPresentation.Base + "background.indices"];
        var colors = Palettes.GetProperty("background");
        if (includeBase) for (int i = 0; i < 240 * 160; i++)
        {
            for (int c = 0; c < 3; c++) result[i * 4 + c] = colors[0][c].GetByte();
            result[i * 4 + 3] = 255;
        }
        void Layer(ushort[] map, int width, int scroll)
        {
            for (int y = 0; y < 160; y++) for (int x = 0; x < 240; x++)
            {
                int sx = (x + scroll) % (width * 8), entry = map[y / 8 * width + sx / 8];
                int tx = (entry & 1024) != 0 ? 7 - sx % 8 : sx % 8, ty = (entry & 2048) != 0 ? 7 - y % 8 : y % 8;
                int index = tiles[(entry & 1023) * 64 + ty * 8 + tx];
                if (index == 0) continue;
                var color = colors[(entry >> 12) * 16 + index];
                for (int c = 0; c < 3; c++) result[(y * 240 + x) * 4 + c] = color[c].GetByte();
                result[(y * 240 + x) * 4 + 3] = 255;
            }
        }
        if (includeBase)
        {
            Layer(bg3, 32, 0);
            Layer(bg2, 32, 0);
        }
        if (includeOverlay) Layer(bg1, 64, offset);
        return result;
    }
}

/// <summary>Exact pinned original source identity and complete, fail-closed presentation inventory.</summary>
public static class PretFameCheckerPresentation
{
    public const string Base = "content/generated/fame-checker-presentation/";
    public const string Raw = "content/raw/fame-checker-presentation/";
    public const string Profile = "firered-fame-checker-presentation-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "aa145898803a698b3613131b86b673c2911612d798b7341b8da007898dd0294e";
    public const string ManifestSha256 = "b9c4e4eb773ed9dd87e67d21bca7a67aba73dfd186c7af6f0292547e5e8f1b4e";

    public static FameCheckerPresentationData Load(string root)
    {
        byte[] Read(string path, string expected, int length)
        {
            if (Path.IsPathRooted(path) || path.Contains('\\') || path.Contains(':') ||
                path.Split('/').Any(p => p is "" or "." or "..") || length is <= 0 or > 1_000_000 ||
                !(path.StartsWith(Base, StringComparison.Ordinal) || path.StartsWith(Raw, StringComparison.Ordinal) ||
                  path == "scripts/fame-checker-presentation.lock.json"))
                throw new InvalidDataException("Unsafe Fame Checker resource.");
            var file = new FileInfo(Path.Combine(root, path));
            if (file.LinkTarget is not null) throw new InvalidDataException("Fame Checker resource symlink rejected.");
            for (var directory = file.Directory; directory is not null; directory = directory.Parent)
                if (directory.LinkTarget is not null) throw new InvalidDataException("Fame Checker resource directory symlink rejected.");
            if (!file.Exists || file.Length != length) throw new InvalidDataException("Fame Checker resource missing or length differs: " + path);
            var bytes = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
                throw new InvalidDataException("Fame Checker resource hash differs: " + path);
            return bytes;
        }
        using var locked = JsonDocument.Parse(Read("scripts/fame-checker-presentation.lock.json", LockSha256, 34133));
        using var document = JsonDocument.Parse(Read(Base + "manifest.json", ManifestSha256, 73192));
        var sourceLock = locked.RootElement;
        var manifest = document.RootElement;
        foreach (var item in new[] { sourceLock, manifest })
            if (item.GetProperty("schemaVersion").GetInt32() != 1 || item.GetProperty("profile").GetString() != Profile ||
                item.GetProperty("sourceCommit").GetString() != SourceCommit || item.GetProperty("sourceTree").GetString() != SourceTree)
                throw new InvalidDataException("Fame Checker presentation source identity differs.");
        if (manifest.GetProperty("lockSha256").GetString() != LockSha256 || manifest.GetProperty("portraits").GetArrayLength() != 16 ||
            manifest.GetProperty("factIconSymbols").GetArrayLength() != 96)
            throw new InvalidDataException("Fame Checker presentation inventory differs.");
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in sourceLock.GetProperty("inputs").EnumerateArray())
        {
            string path = row.GetProperty("rawPath").GetString()!;
            if (!paths.Add(path) || path != Raw + row.GetProperty("path").GetString()) throw new InvalidDataException("Fame Checker raw path differs.");
            var bytes = Read(path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != row.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("Fame Checker source Git blob differs.");
        }
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in manifest.GetProperty("outputs").EnumerateArray())
        {
            string path = Base + row.GetProperty("path").GetString()!;
            files.Add(path, Read(path, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
        }
        foreach (var asset in manifest.GetProperty("assets").EnumerateArray())
        {
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            if (width is <= 0 or > 512 || height is <= 0 or > 512 ||
                files[asset.GetProperty("rgbaPath").GetString()!].Length != width * height * 4 ||
                files[asset.GetProperty("indicesPath").GetString()!].Length != width * height ||
                files[asset.GetProperty("indicesPath").GetString()!].Any(i => i > 15))
                throw new InvalidDataException("Fame Checker image dimensions/indices differ.");
            var image = files[asset.GetProperty("texturePath").GetString()!];
            if (image.Length < 33 || !image.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
                BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(16)) != width || BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(20)) != height)
                throw new InvalidDataException("Fame Checker PNG identity differs.");
        }
        using var layout = JsonDocument.Parse(files[Base + "layout.json"]);
        using var palettes = JsonDocument.Parse(files[Base + "palettes.json"]);
        if (layout.RootElement.GetProperty("profile").GetString() != Profile || palettes.RootElement.GetProperty("profile").GetString() != Profile ||
            manifest.GetProperty("audio").GetProperty("cues").GetArrayLength() != 5)
            throw new InvalidDataException("Fame Checker layout/cue identity differs.");
        return new(manifest, layout.RootElement, palettes.RootElement, files);
    }
}
