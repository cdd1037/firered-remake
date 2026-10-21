using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record SafariEncounterDocument(int SchemaVersion,
    ImmutableArray<EarlyLandEncounterTable> Tables, ImmutableArray<EncounterHeldItemRow> HeldItems,
    int HeldItemDrawBound);

/// <summary>Generated Safari land definitions. Loading does not admit other encounter methods.</summary>
public sealed class PretSafariEncounters
{
    public const string Profile = "firered-safari-encounters-v1";
    public ImmutableDictionary<MapId, EarlyLandEncounterTable> Tables { get; }
    public ImmutableDictionary<SpeciesId, EncounterHeldItemRow> HeldItems { get; }
    public SafariEncounterDocument Document { get; }

    private PretSafariEncounters(SafariEncounterDocument document)
    {
        Document = document;
        Tables = document.Tables.ToImmutableDictionary(table => table.Map);
        HeldItems = document.HeldItems.ToImmutableDictionary(row => new SpeciesId(row.Species));
    }

    public static PretSafariEncounters Load(PretSafariRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return Parse(region.EncounterInventory.GetRawText(), region.SourceMaps.Keys.ToHashSet());
    }

    internal static PretSafariEncounters Parse(string json, IReadOnlySet<MapId> regionMaps)
    {
        var document = ContentJson.ParseStrict<SafariEncounterDocument>(json, ContentJson.CreateOptions());
        GeneratedLandEncounterValidation.Validate(document.SchemaVersion, document.Tables,
            document.HeldItemDrawBound, document.HeldItems,
            row => row.Species != "pret:SPECIES_CHANSEY" ||
                row is { Common: "ITEM_NONE", Rare: "ITEM_LUCKY_EGG" });
        foreach (var table in document.Tables)
            if (!regionMaps.Contains(table.Map))
                throw new InvalidDataException("Safari encounter map is missing from the region.");
        return new(document);
    }
}
