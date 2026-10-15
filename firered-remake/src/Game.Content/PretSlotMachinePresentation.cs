using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Game.Content;

public sealed record SlotMachineTexture(string Id, int Width, int Height, ReadOnlyMemory<byte> Rgba, ReadOnlyMemory<byte> Png);

/// <summary>Slot textures, palette data, layout and cue bindings.</summary>
public sealed class SlotMachinePresentationData
{
    private readonly IReadOnlyDictionary<string, byte[]> files;
    private readonly JsonElement palettes;
    public JsonElement Manifest { get; }
    public JsonElement Layout { get; }
    public JsonElement Texts { get; }
    public JsonElement Cues => Manifest.GetProperty("audio").GetProperty("cues");
    public IReadOnlyDictionary<string, SlotMachineTexture> Textures { get; }

    internal SlotMachinePresentationData(JsonElement manifest, JsonElement layout, JsonElement colors, Dictionary<string, byte[]> bytes)
    {
        Manifest = manifest.Clone();
        Layout = layout.GetProperty("layout").Clone();
        Texts = layout.GetProperty("texts").Clone();
        palettes = colors.GetProperty("palettes").Clone();
        files = new ReadOnlyDictionary<string, byte[]>(bytes);
        Textures = new ReadOnlyDictionary<string, SlotMachineTexture>(manifest.GetProperty("assets").EnumerateArray().ToDictionary(
            r => r.GetProperty("id").GetString()!, r => new SlotMachineTexture(r.GetProperty("id").GetString()!,
                r.GetProperty("width").GetInt32(), r.GetProperty("height").GetInt32(),
                PngRgba8.Decode(bytes[r.GetProperty("texturePath").GetString()!]), bytes[r.GetProperty("texturePath").GetString()!]), StringComparer.Ordinal));
    }

    /// <summary>Original vertically stacked sprite cells; callers supply authoritative icon/digit/animation frame.</summary>
    public ReadOnlyMemory<byte> SpriteFrame(string atlas, int frame)
    {
        var (height, count) = atlas switch
        {
            "reel-icons" => (32, 7), "clefairy" => (32, 6), "digits" => (16, 10),
            _ => throw new ArgumentException("Unknown slot sprite atlas.", nameof(atlas))
        };
        if ((uint)frame >= count) throw new ArgumentOutOfRangeException(nameof(frame));
        var texture = Textures[atlas];
        int size = texture.Width * height * 4;
        return texture.Rgba.Slice(size * frame, size);
    }

    /// <summary>The complete 256-pixel source map, including the offscreen columns used during help scrolling.</summary>
    public byte[] RenderHelpMap()
    {
        var map = files[PretSlotMachinePresentation.Base + "combos_window.tilemap"];
        var tiles = files[PretSlotMachinePresentation.Base + "combos_window.indices"];
                byte[] rgba = new byte[256 * 160 * 4];
        for (int y = 0; y < 160; y++) for (int x = 0; x < 256; x++)
        {
            int entry = BinaryPrimitives.ReadUInt16LittleEndian(map.AsSpan((y / 8 * 32 + x / 8) * 2));
            int tx = (entry & 1024) != 0 ? 7 - x % 8 : x % 8, ty = (entry & 2048) != 0 ? 7 - y % 8 : y % 8;
            int index = tiles[(entry & 1023) * 64 + ty * 8 + tx], p = (y * 256 + x) * 4;
            int bank = entry >> 12;
            var color = bank >= 7 ? palettes.GetProperty("combos_window")[(bank - 7) * 16 + index] :
                bank is 5 or 6 ? palettes.GetProperty("match_lines")[index] : palettes.GetProperty("bg")[bank * 16 + index];
            for (int c = 0; c < 3; c++) rgba[p + c] = color[c].GetByte();
            rgba[p + 3] = index == 0 ? (byte)0 : (byte)255;
        }
        return rgba;
    }

    /// <summary>Source BG2 with supplied display state. Does not evaluate a win or change a wager.</summary>
    public byte[] RenderBackground(int bet, int pressedButtons = 0, int winningLines = 0, int? payoutLightFrame = null)
    {
        if (bet is < 0 or > 3 || pressedButtons is < 0 or > 7 || winningLines is < 0 or > 31 || payoutLightFrame is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(bet), "Slot display state outside source ranges.");
        var mapBytes = files[PretSlotMachinePresentation.Base + "bg.tilemap"];
        var entries = Enumerable.Range(0, 640).Select(i => (int)BinaryPrimitives.ReadUInt16LittleEndian(mapBytes.AsSpan(i * 2))).ToArray();
        foreach (var line in Layout.GetProperty("matchLines").EnumerateArray())
        {
            int id = line.GetProperty("id").GetInt32();
            int bank = (winningLines & (1 << id)) != 0 ? 6 : bet >= line.GetProperty("minimumBet").GetInt32() ? 5 : 4;
            foreach (var cell in line.GetProperty("tileIndices").EnumerateArray())
                entries[cell.GetInt32()] = (entries[cell.GetInt32()] & 0xFFF) | (bank << 12);
        }
        var buttons = Layout.GetProperty("buttonTileIndices");
        for (int reel = 0; reel < 3; reel++)
            if ((pressedButtons & (1 << reel)) != 0)
                for (int tile = 0; tile < 4; tile++) entries[buttons[reel][tile].GetInt32()] = 192 + tile;
        var bg = files[PretSlotMachinePresentation.Base + "bg.indices"];
        var pressed = files[PretSlotMachinePresentation.Base + "button_pressed.indices"];
        byte[] rgba = new byte[240 * 160 * 4];
        for (int y = 0; y < 160; y++)
        for (int x = 0; x < 240; x++)
        {
            int cell = entries[y / 8 * 32 + x / 8], tile = cell & 1023, bank = cell >> 12;
            int tx = (cell & 1024) != 0 ? 7 - x % 8 : x % 8, ty = (cell & 2048) != 0 ? 7 - y % 8 : y % 8;
            int index = tile >= 192 ? pressed[(tile - 192) * 64 + ty * 8 + tx] : bg[tile * 64 + ty * 8 + tx];
            var color = bank is 5 or 6 ? palettes.GetProperty("match_lines")[index] :
                bank == 1 && payoutLightFrame is int light ? palettes.GetProperty("payout_lights")[light * 16 + index] :
                palettes.GetProperty("bg")[bank * 16 + index];
            int p = (y * 240 + x) * 4;
            for (int c = 0; c < 3; c++) rgba[p + c] = color[c].GetByte();
            rgba[p + 3] = index == 0 ? (byte)0 : (byte)255;
        }
        return rgba;
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
        if (!manifest.GetProperty("assets").EnumerateArray().Select(r => r.GetProperty("id").GetString()).SequenceEqual(
                new[] { "reel-icons","clefairy","digits","background","help","controls" }))
            throw new InvalidDataException("Slot texture inventory differs.");
        foreach (var asset in manifest.GetProperty("assets").EnumerateArray())
        {
            int width = asset.GetProperty("width").GetInt32(), height = asset.GetProperty("height").GetInt32();
            var rgba = PngRgba8.Decode(files[asset.GetProperty("texturePath").GetString()!]);
            if (width is <= 0 or > 240 || height is <= 0 or > 224 || rgba.Length != width * height * 4 )
                throw new InvalidDataException("Slot texture identity differs.");
        }
        using var layout = JsonDocument.Parse(files[Base + "layout.json"]);
        using var palettes = JsonDocument.Parse(files[Base + "palettes.json"]);
        return new(manifest, layout.RootElement, palettes.RootElement, files);
    }
}
