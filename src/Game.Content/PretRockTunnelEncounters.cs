using System.Collections.Immutable;
using System.Text;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record RockTunnelEncounterDocument(int SchemaVersion,
    ImmutableArray<EarlyLandEncounterTable> Tables, int HeldItemDrawBound,
    ImmutableArray<EncounterHeldItemRow> HeldItems);

/// <summary>Generated land encounter definitions; method admission remains in campaign rules.</summary>
public sealed class PretRockTunnelEncounters
{
    public const string Profile = "firered-rocktunnel-encounters-v1";
    public ImmutableDictionary<MapId, EarlyLandEncounterTable> Tables { get; }
    public ImmutableArray<EncounterHeldItemRow> HeldItems { get; }

    private PretRockTunnelEncounters(RockTunnelEncounterDocument document)
    {
        Tables = document.Tables.ToImmutableDictionary(table => table.Map);
        HeldItems = document.HeldItems;
    }

    public static PretRockTunnelEncounters Load(string root)
    {
        var bytes = File.ReadAllBytes(GeneratedResourceFile.SafePath(root, "content/generated/data/encounters/rocktunnel.json"));
        var document = ContentJson.ParseStrict<RockTunnelEncounterDocument>(
            Encoding.UTF8.GetString(bytes), ContentJson.CreateOptions());
        GeneratedLandEncounterValidation.Validate(document.SchemaVersion, document.Tables,
            document.HeldItemDrawBound, document.HeldItems,
            row => row.Common == "ITEM_NONE" && row.Rare == (row.Species == "pret:SPECIES_ONIX" ? "ITEM_HARD_STONE" : "ITEM_NONE"));
        return new(document);
    }

}
