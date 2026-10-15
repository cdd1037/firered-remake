using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
namespace Game.Content;

public sealed record EventIslandMachineRows(ItemId Item, ImmutableArray<MachineCompatibility> Compatibility);
public sealed record EventIslandSpeciesDocument(int OwnedSpeciesCount, int OpponentSpeciesCount,
    ImmutableArray<EventIslandMachineRows> Machines, ImmutableArray<EarlyShopItemDefinition> Items);

/// <summary>Source-locked additive event content; map access and distribution remain Application concerns.</summary>
public static class PretEventIslandRules
{
    public const string Profile = "firered-event-island-species-v1";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private static byte[] Read(string root,string path)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, path));
        return bytes;
    }
    public static EventIslandSpeciesDocument LoadDocument(string root, SharedMachineCatalog? shared = null)
    {
        var d = JsonSerializer.Deserialize<EventIslandSpeciesDocument>(Read(root,"content/generated/data/pokemon/event-island.json"), (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions(Options))
            ?? throw new InvalidDataException("event_island_source_missing");
        if (d.Machines.IsDefault || d.Items.IsDefault) throw new InvalidDataException("event_island_catalog_arrays_missing");
        return d;
    }

}
