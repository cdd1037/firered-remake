using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SafariFishingDocument(int SchemaVersion, ImmutableArray<SafariFishingTable> Tables,
    ImmutableArray<SafariFishingHeldItem> HeldItems);

/// <summary>Only generated gameplay data is read at runtime. Source selection belongs to the importer.</summary>
public static class PretSafariFishing
{
    public static SafariFishingEncounterCatalog Load(PretSafariRegion region, GamePokemonCatalog pokemon)
    {
        var catalog = Parse(File.ReadAllText(Path.Combine(region.GeneratedDirectory, "fishing.json")));
        foreach (var table in catalog.Tables.Values)
            if (!region.SourceMaps.ContainsKey(table.Map) || table.Slots.Any(slot => !pokemon.Catalog.Species.ContainsKey(slot.Species)))
                throw new InvalidDataException("safari_fishing_admission");
        foreach (var row in catalog.HeldItems.Values)
            foreach (var symbol in new[] { row.Common, row.Rare })
                if (symbol != "ITEM_NONE" && !pokemon.Catalog.BattleItems!.Items.ContainsKey(new("pret:" + symbol)))
                    throw new InvalidDataException("safari_fishing_held_item_admission");
        return catalog;
    }

    public static SafariFishingEncounterCatalog Parse(string json)
    {
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<FishingRod>());
        var document = ContentJson.ParseStrict<SafariFishingDocument>(json, options);
        if (document.SchemaVersion != 1) throw new InvalidDataException("safari_fishing_schema");
        try { return new(document.Tables, document.HeldItems); }
        catch (ArgumentException error) { throw new InvalidDataException("safari_fishing_definitions", error); }
    }
}
