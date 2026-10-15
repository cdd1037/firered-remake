using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretCapeBrinkTutor CapeBrinkTutor { get; }
    public CapeBrinkWaterEncounterCatalog CapeBrinkWaterEncounters { get; }
    private ImmutableArray<ItemfinderUnderfoot> CapeBrinkUnderfootItems { get; }

    private static void ValidateCapeBrinkComposition(PretCapeBrinkTutor source,ImmutableDictionary<MapId, PretWorldMap> maps,ImmutableDictionary<MapId, PretWorldMap> previous,PretStoryCompilation scripts,PretSuperRodService superRod)
    {
        if (source.NewMaps.Count != 2 )
            throw new InvalidDataException("cape_brink_current_map_composition");
        if (superRod.SuperRodTables.Count(table => maps.ContainsKey(table.Map)) != 40)
            throw new InvalidDataException("cape_brink_current_super_rod_scope");
        var town = PretFirstSeviiRegion.ConnectedMap(maps[new("map.two-island")]);
        var cape = PretCapeBrinkTutor.ConnectedMap(maps[PretCapeBrinkTutor.CapeMapId]);
        if (town.Connections.Single(c => c.DestinationMapId == cape.Id) is not { Direction: Direction.North, Offset: 24 } ||
            cape.Connections.Single() is not { Direction: Direction.South, Offset: -24 } edge || edge.DestinationMapId != town.Id)
            throw new InvalidDataException("cape_brink_current_original_edges");
        foreach (var program in scripts.Programs.Values.Where(p => p.Instructions.Any(i =>
            i.Source.Path == "data/maps/TwoIsland_CapeBrink_House/scripts.inc")))
            if (program.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException("cape_brink_story_incomplete:" + program.Id);
        if (scripts.FindProgram("TwoIsland_CapeBrink_House_EventScript_StarterTutor") is not { Instructions.IsEmpty: false })
            throw new InvalidDataException("cape_brink_story_missing");
    }

    // Every runtime slot is checked against the strict loader's authenticated original JSON.
    // The existing owned catalog and all predecessor table instances remain intact.
    private static void ValidateCapeBrinkEncounterSources(PretCapeBrinkTutor source,PokemonCatalog owned,PretSuperRodService superRod)
    {
        var document = source.EncountersAndItems;
        var reference = document.GetProperty("sourceRef");
        var encounter = document.GetProperty("encounters").EnumerateArray().Single();
        string mapSymbol = encounter.GetProperty("map").GetString()!;
        string symbol = encounter.GetProperty("base_label").GetString()!;
        string path = reference.GetProperty("path").GetString()!;
        if (mapSymbol != source.NewMaps[PretCapeBrinkTutor.CapeMapId].SourceMapSymbol ||
            symbol != reference.GetProperty("symbol").GetString() )
            throw new InvalidDataException("cape_brink_encounter_source_identity");
        var species = document.GetProperty("wildSpecies").EnumerateArray().Select(s => s.GetString()!).ToImmutableHashSet();
        var tableSpecies = encounter.EnumerateObject().Where(p => p.Name.EndsWith("_mons", StringComparison.Ordinal))
            .SelectMany(p => p.Value.GetProperty("mons").EnumerateArray()).Select(s => s.GetProperty("species").GetString()!).ToImmutableHashSet();
        if (!species.SetEquals(tableSpecies) ||
            species.Any(s => !owned.Species.ContainsKey(new("pret:" + s))))
            throw new InvalidDataException("cape_brink_encounter_owned_species");
        void Check(string kind, MapId map, string actualPath, string actualSymbol,
            int rate, IEnumerable<(int Index, SpeciesId Species, byte Min, byte Max, int Weight)> slots,
            string? rod = null)
        {
            var sourceTable = encounter.GetProperty(kind);
            var field = document.GetProperty("fields").EnumerateArray().Single(f => f.GetProperty("type").GetString() == kind);
            int[] indexes = rod is null ? Enumerable.Range(0, sourceTable.GetProperty("mons").GetArrayLength()).ToArray() :
                field.GetProperty("groups").GetProperty(rod).EnumerateArray().Select(i => i.GetInt32()).ToArray();
            var actual = slots.ToArray();
            if (map != PretCapeBrinkTutor.CapeMapId || actualPath != path ||
                actualSymbol != symbol || rate != sourceTable.GetProperty("encounter_rate").GetInt32() ||
                actual.Length != indexes.Length)
                throw new InvalidDataException("cape_brink_encounter_table:" + kind + ":" + rod);
            for (int i = 0; i < indexes.Length; i++)
            {
                int index = indexes[i]; var mon = sourceTable.GetProperty("mons")[index]; var slot = actual[i];
                if (slot.Index != index || slot.Species.Value != "pret:" + mon.GetProperty("species").GetString() ||
                    slot.Min != mon.GetProperty("min_level").GetInt32() || slot.Max != mon.GetProperty("max_level").GetInt32() ||
                    slot.Weight != field.GetProperty("encounter_rates")[index].GetInt32())
                    throw new InvalidDataException("cape_brink_encounter_slot:" + kind + ":" + index);
            }
        }
        var land = CapeBrinkEncounterTables.Land.Single();
        Check("land_mons", land.Map, land.SourcePath, land.SourceSymbol,
            land.RawEncounterRate, land.Slots.Select(s => (s.Index, s.Species, s.MinimumLevel, s.MaximumLevel, s.Weight)));
        var water = CapeBrinkEncounterTables.Water.Single();
        if (water.SourceMapSymbol != mapSymbol) throw new InvalidDataException("cape_brink_surf_source_map");
        Check("water_mons", water.Map, water.SourcePath, water.SourceSymbol,
            water.RawEncounterRate, water.Slots.Select(s => (s.Index, s.Species, s.MinimumLevel, s.MaximumLevel, s.Weight)));
        foreach (var (rod, table) in new[] { ("old_rod", CapeBrinkEncounterTables.OldRod.Single()),("good_rod", CapeBrinkEncounterTables.GoodRod.Single()),("super_rod", superRod.SuperRodTables.Single(t => t.Map == PretCapeBrinkTutor.CapeMapId)) })
            Check("fishing_mons", table.Map, table.SourcePath, table.SourceSymbol,
                table.RawEncounterRate, table.Slots.Select(s => (s.Index, s.Species, s.MinimumLevel, s.MaximumLevel, s.Weight)), rod);
    }

    private static ImmutableArray<FieldPickupDefinition> CapeBrinkHiddenPickups(PretCapeBrinkTutor source,EarlyShopCatalog shop,PretStoryCompilation scripts)
    {
        EarlyShopInventoryRules.ValidateCatalog(shop);
        var map = source.NewMaps[PretCapeBrinkTutor.CapeMapId];
        var events = map.Events.GetProperty("bg_events");
        var hidden = source.EncountersAndItems.GetProperty("hiddenItems");
        if (!JsonElement.DeepEquals(events, hidden) || hidden.GetArrayLength() != 2)
            throw new InvalidDataException("cape_brink_hidden_source_events");
        var result = ImmutableArray.CreateBuilder<FieldPickupDefinition>();
        foreach (var row in hidden.EnumerateArray())
        {
            string symbol = row.GetProperty("item").GetString()!,flag = row.GetProperty("flag").GetString()!;
            var item = shop.Items.SingleOrDefault(i => i.SourceItemSymbol == symbol)
                ?? throw new InvalidDataException("cape_brink_hidden_item_missing:" + symbol);
            var declaration = source.EncountersAndItems.GetProperty("itemDeclarations").EnumerateArray()
                .Single(i => i.GetProperty("itemId").GetString() == symbol);
            if (!scripts.TryConstant(flag, out int flagId) || source.Constants[flag] != flagId ||
                source.Constants[symbol] != item.SourceItemId || item.Id.Value != "pret:" + symbol ||
                item.Name != declaration.GetProperty("english").GetString() || item.Pocket != declaration.GetProperty("pocket").GetString() ||
                item.Price != declaration.GetProperty("price").GetInt32() || item.FieldUseFunction != declaration.GetProperty("fieldUseFunc").GetString() ||
                row.GetProperty("quantity").GetInt32() != 1 || row.GetProperty("elevation").GetInt32() != 3 ||
                (symbol == "ITEM_PP_MAX" ? row.GetProperty("x").GetInt32() != 16 || row.GetProperty("y").GetInt32() != 28 || !row.GetProperty("underfoot").GetBoolean() :
                    symbol != "ITEM_RARE_CANDY" || row.GetProperty("x").GetInt32() != 15 || row.GetProperty("y").GetInt32() != 13 || row.GetProperty("underfoot").GetBoolean()))
                throw new InvalidDataException("cape_brink_hidden_source_item:" + symbol);
            result.Add(new("cape-brink:" + flag, map.SourceMapSymbol, true, null,
                row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32(), row.GetProperty("elevation").GetInt32(),
                "EventScript_HiddenItemScript", symbol, item.SourceItemId, item.Id, item.Name, item.Pocket, 1, flag, flagId));
        }
        return result.ToImmutable();
    }
}
