using System.Collections.Immutable;
using System.Text;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record Route11EncounterDocument(int SchemaVersion,
    ImmutableArray<EarlyLandEncounterTable> Tables, int HeldItemDrawBound,
    ImmutableArray<EncounterHeldItemRow> HeldItems);

/// <summary>Generated land encounter definitions; method admission remains in campaign rules.</summary>
public sealed class PretRoute11Encounters
{
    public const string Profile = "firered-route11-encounters-v1";
    public ImmutableDictionary<MapId, EarlyLandEncounterTable> Tables { get; }
    public ImmutableArray<EncounterHeldItemRow> HeldItems { get; }

    private PretRoute11Encounters(Route11EncounterDocument document)
    {
        Tables = document.Tables.ToImmutableDictionary(table => table.Map);
        HeldItems = document.HeldItems;
    }

    public static PretRoute11Encounters Load(string root)
    {
        var bytes = File.ReadAllBytes(GeneratedResourceFile.SafePath(root, "content/generated/data/encounters/route11.json"));
        var document = ContentJson.ParseStrict<Route11EncounterDocument>(
            Encoding.UTF8.GetString(bytes), ContentJson.CreateOptions());
        GeneratedLandEncounterValidation.Validate(document.SchemaVersion, document.Tables,
            document.HeldItemDrawBound, document.HeldItems,
            row => row.Common == "ITEM_NONE" && row.Rare == "ITEM_NONE");
        return new(document);
    }

}
