using System.Text.Json;
using Game.Content;

/// <summary>Generated-only TM Case/PC presentation pixels and source sine samples.</summary>
internal sealed class InventoryPresentationAssets
{
    internal sealed record Asset(string Id, string TexturePath, int Width, int Height);
    internal Asset[] Assets { get; }
    internal string[] DiscTypes { get; }
    internal int[] ShiftOffsets { get; }
    internal int[] ArrowOffsets { get; }
    internal int[] StatForeground { get; }
    internal int[] StatShadow { get; }
    internal const string Directory = "content/generated/ui/inventory/";
    private InventoryPresentationAssets(JsonElement root)
    {
        if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new InvalidDataException("Unsupported inventory presentation.");
        Assets = root.GetProperty("assets").EnumerateArray().Select(a => new Asset(a.GetProperty("id").GetString()!,
            a.GetProperty("texturePath").GetString()!, a.GetProperty("width").GetInt32(), a.GetProperty("height").GetInt32())).ToArray();
        DiscTypes = root.GetProperty("discTypes").EnumerateArray().Select(v => v.GetString()!).ToArray();
        int[] Values(string key) => root.GetProperty(key).EnumerateArray().Select(v => v.GetInt32()).ToArray();
        ShiftOffsets = Values("shiftOffsets"); ArrowOffsets = Values("arrowOffsets"); StatForeground = Values("statForeground"); StatShadow = Values("statShadow");
        if (Assets.Length == 0 || Assets.Any(a => string.IsNullOrWhiteSpace(a.Id) || a.Width <= 0 || a.Height <= 0) ||
            Assets.Select(a => a.Id).Distinct().Count() != Assets.Length || DiscTypes.Length != 17 ||
            DiscTypes.Distinct().Count() != DiscTypes.Length || ShiftOffsets.Length != 17 || ShiftOffsets[0] != 0 ||
            ShiftOffsets[^1] != 0 || ShiftOffsets.Any(v => v is < 0 or > 16) || ArrowOffsets.Length != 32 || ArrowOffsets.Any(v => v is < -2 or > 2) ||
            StatForeground.Length != 3 || StatShadow.Length != 3 || StatForeground.Concat(StatShadow).Any(v => v is < 0 or > 255))
            throw new InvalidDataException("Invalid inventory presentation geometry.");
        var expected = new Dictionary<string,(int Width,int Height)>(StringComparer.Ordinal)
        {
            ["case-male"]=(240,160),["case-male-shaded"]=(240,160),["case-male-front"]=(240,160),
            ["case-female"]=(240,160),["case-female-shaded"]=(240,160),["case-female-front"]=(240,160),
            ["discs"]=(DiscTypes.Length*32,64),["discs-tutorial"]=(DiscTypes.Length*32,64),["hand-single"]=(32,128),["hand-multi"]=(32,128),
            ["hand-shadow"]=(16,16),["move-labels"]=(40,48),["hm"]=(16,12),["number"]=(8,13),
            ["scroll-up"]=(16,16),["scroll-down"]=(16,16)
        };
        if (!Assets.Select(a=>a.Id).ToHashSet(StringComparer.Ordinal).SetEquals(expected.Keys) ||
            Assets.Any(a=>(a.Width,a.Height)!=expected[a.Id])) throw new InvalidDataException("Inventory sprite roles are incomplete.");
    }
    internal static InventoryPresentationAssets Load(string root)
    {
        using var json = JsonDocument.Parse(GeneratedResourceFile.Read(root, Directory + "inventory.json"));
        var assets = new InventoryPresentationAssets(json.RootElement);
        foreach (var asset in assets.Assets) _ = GeneratedResourceFile.SafePath(root, Directory + asset.TexturePath);
        return assets;
    }
}
