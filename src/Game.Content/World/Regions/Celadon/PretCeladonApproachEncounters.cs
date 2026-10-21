using System.Collections.Immutable;
using System.Text;
using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record EncounterHeldItemRow(string Species, string Common, string Rare);
public sealed record CeladonApproachEncounterDocument(int SchemaVersion,
    ImmutableArray<EarlyLandEncounterTable> Tables, int HeldItemDrawBound,
    ImmutableArray<EncounterHeldItemRow> HeldItems);

public sealed class PretCeladonApproachEncounters
{
    public const string Profile = "firered-celadon-approach-encounters-v1";
    public ImmutableDictionary<MapId, EarlyLandEncounterTable> Tables { get; }
    public ImmutableArray<EncounterHeldItemRow> HeldItems { get; }

    private PretCeladonApproachEncounters(CeladonApproachEncounterDocument document)
    {
        Tables = document.Tables.ToImmutableDictionary(table => table.Map);
        HeldItems = document.HeldItems;
    }

    public static PretCeladonApproachEncounters Load(string root)
    {
        var bytes = PretCeladonApproachSource.Read(root, "content/generated/data/encounters/celadon-approach.json");
        var document = ContentJson.ParseStrict<CeladonApproachEncounterDocument>(
            Encoding.UTF8.GetString(bytes), ContentJson.CreateOptions());
        Need(document.SchemaVersion == 1 && !document.Tables.IsDefaultOrEmpty &&
             !document.HeldItems.IsDefaultOrEmpty, "Missing Celadon encounter data.");
        // The existing held-item factory still owns the draw policy. Keep this
        // actual implementation constraint until that separate authority is unified.
        Need(document.HeldItemDrawBound == 100, "Unsupported held-item draw bound.");
        var maps = new HashSet<MapId>();
        var species = new HashSet<string>(StringComparer.Ordinal);
        foreach (var table in document.Tables)
        {
            Need(table is not null, "Null encounter table.");
            Need(!string.IsNullOrWhiteSpace(table!.Map.Value) && maps.Add(table.Map), "Invalid or duplicate encounter map.");
            Need(table.RawEncounterRate is >= 1 and <= 255 && !table.Slots.IsDefault &&
                 table.Slots.Length == EarlyLandEncounterRules.SlotWeights.Length, "Invalid land rate or slot count.");
            for (int i = 0; i < table.Slots.Length; i++)
            {
                var slot = table.Slots[i];
                Need(slot is not null && slot.Index == i && slot.Weight == EarlyLandEncounterRules.SlotWeights[i] &&
                     slot.MinimumLevel is >= 1 and <= 100 && slot.MaximumLevel >= slot.MinimumLevel &&
                     slot.MaximumLevel <= 100 && !string.IsNullOrWhiteSpace(slot.Species.Value), "Invalid land slot.");
                species.Add(slot!.Species.Value);
            }
        }
        var heldSpecies = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in document.HeldItems)
        {
            Need(row is not null && !string.IsNullOrWhiteSpace(row.Species) && heldSpecies.Add(row.Species),
                 "Invalid or duplicate held-item species.");
            var id = new SpeciesId(row!.Species);
            Need(row.Common == CeladonApproachWildPokemonFactory.HeldItem(id, 45) &&
                 row.Rare == CeladonApproachWildPokemonFactory.HeldItem(id, 95),
                 "Held items differ from the current wild-item implementation.");
        }
        Need(heldSpecies.SetEquals(species), "Held-item species must cover the encounter slots exactly.");
        return new(document);
    }

    private static void Need(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
