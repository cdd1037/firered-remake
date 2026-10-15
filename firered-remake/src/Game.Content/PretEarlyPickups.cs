using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

public sealed record PretEarlyPickupDefinition(string Id, string MapSymbol, string Kind, int SourceEventIndex,
    int? SourceLocalId, int X, int Y, int Elevation, string Script, string ItemSymbol, int SourceItemId,
    string ItemId, string ItemName, string Pocket, int Quantity, string Flag, int SourceFlagId, bool Underfoot);

/// <summary>Imported early-game item balls and hidden pickups.</summary>
public sealed record PretEarlyPickups(ImmutableArray<PretEarlyPickupDefinition> Pickups)
{
    public PretEarlyPickupDefinition? FindItemBall(string mapSymbol, int sourceLocalId) =>
        Pickups.SingleOrDefault(p => p.Kind == "itemBall" && p.MapSymbol == mapSymbol && p.SourceLocalId == sourceLocalId);

    public PretEarlyPickupDefinition? FindHiddenItem(string mapSymbol, int x, int y) =>
        Pickups.SingleOrDefault(p => p.Kind == "hiddenItem" && p.MapSymbol == mapSymbol && p.X == x && p.Y == y);

    public static PretEarlyPickups Load(string projectRoot)
    {
        using var input = File.OpenRead(Path.Combine(projectRoot, "content/generated/data/items/early-pickups.json"));
        var pickups = JsonSerializer.Deserialize<ImmutableArray<PretEarlyPickupDefinition>>(input, ContentJson.CreateOptions());
        if (pickups.IsDefaultOrEmpty) throw new InvalidDataException("Pickup definitions are missing.");
        return new(pickups);
    }
}
