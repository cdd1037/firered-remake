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
    public WaterEncounterCatalog CapeBrinkWaterEncounters { get; }

    private static void ValidateCapeBrinkComposition(PretCapeBrinkTutor source,ImmutableDictionary<MapId, PretWorldMap> maps,PretStoryCompilation scripts)
    {
        if (source.NewMaps.Count != 2 )
            throw new InvalidDataException("cape_brink_current_map_composition");
        var town = PretWorldMapConnections.Create(maps[new("map.two-island")]);
        var cape = PretWorldMapConnections.Create(maps[PretCapeBrinkTutor.CapeMapId]);
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

    // Legacy land/surf source proof. Fishing definitions are loaded once through the shared provider.
    private static void ValidateCapeBrinkEncounterSources(PretCapeBrinkTutor source,PokemonCatalog owned)
    {
        var document = source.EncountersAndItems;
        var reference = document.GetProperty("sourceRef");
        var encounter = document.GetProperty("encounters").EnumerateArray().Single();
        string mapSymbol = encounter.GetProperty("map").GetString()!;
        string symbol = encounter.GetProperty("base_label").GetString()!;
        if (mapSymbol != source.NewMaps[PretCapeBrinkTutor.CapeMapId].SourceMapSymbol ||
            symbol != reference.GetProperty("symbol").GetString() )
            throw new InvalidDataException("cape_brink_encounter_source_identity");
        var species = document.GetProperty("wildSpecies").EnumerateArray().Select(s => s.GetString()!).ToImmutableHashSet();
        var tableSpecies = encounter.EnumerateObject().Where(p => p.Name.EndsWith("_mons", StringComparison.Ordinal))
            .SelectMany(p => p.Value.GetProperty("mons").EnumerateArray()).Select(s => s.GetProperty("species").GetString()!).ToImmutableHashSet();
        if (!species.SetEquals(tableSpecies) ||
            species.Any(s => !owned.Species.ContainsKey(new("pret:" + s))))
            throw new InvalidDataException("cape_brink_encounter_owned_species");
        void Check(string kind, MapId map,
            int rate, IEnumerable<(int Index, SpeciesId Species, byte Min, byte Max, int Weight)> slots)
        {
            var sourceTable = encounter.GetProperty(kind);
            var field = document.GetProperty("fields").EnumerateArray().Single(f => f.GetProperty("type").GetString() == kind);
            int[] indexes = Enumerable.Range(0, sourceTable.GetProperty("mons").GetArrayLength()).ToArray();
            var actual = slots.ToArray();
            if (map != PretCapeBrinkTutor.CapeMapId || rate != sourceTable.GetProperty("encounter_rate").GetInt32() ||
                actual.Length != indexes.Length)
                throw new InvalidDataException("cape_brink_encounter_table:" + kind);
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
        Check("land_mons", land.Map,
            land.RawEncounterRate, land.Slots.Select(s => (s.Index, s.Species, s.MinimumLevel, s.MaximumLevel, s.Weight)));
        var water = CapeBrinkEncounterTables.Water.Single();
        Check("water_mons", water.Map,
            water.RawEncounterRate, water.Slots.Select(s => (s.Index, s.Species, s.MinimumLevel, s.MaximumLevel, s.Weight)));
    }


}
