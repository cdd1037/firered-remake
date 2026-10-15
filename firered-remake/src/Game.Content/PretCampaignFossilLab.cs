using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretFossilLabRegion FossilLabRegion { get; }
    public bool FossilLabEnabled => FossilLabRegion is not null;
    public FossilLabOwnedPokemonCatalog FossilLabOwnership { get; }
    public FossilLabOpponentPokemonCatalog FossilLabOpponents { get; }
    public FossilLabMachineCatalog FossilLabMachines { get; }
    public FossilLabShopCatalog FossilLabShop { get; }

    private static void AppendFossilLabSources(PretFossilLabRegion region,List<PretSourceScriptFile> sources,List<string> texts,Dictionary<string, int> constants)
        => AppendAuthenticatedRegionalSources(region.Scripts,region.TextSources.Values,region.Constants,sources,texts,constants);

    private static void AppendAuthenticatedRegionalSources(IEnumerable<PretSourceScriptFile> scripts,IEnumerable<string> textSources,IReadOnlyDictionary<string, int> sourceConstants,List<PretSourceScriptFile> sources,List<string> texts,Dictionary<string, int> constants)
    {
        var previous = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var file in scripts)
        {
            if (previous.TryGetValue(file.Path, out var existing))
            {
                if (existing.Text != file.Text)
                    throw new InvalidDataException("current_regional_source_conflict:" + file.Path);
            }
            else { previous.Add(file.Path, file); sources.Add(file); }
            if (!texts.Contains(file.Text, StringComparer.Ordinal)) texts.Add(file.Text);
        }
        foreach (var text in textSources)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
        MergeRegionalServiceConstants(constants, sourceConstants);
    }

    private static void ValidateFossilLabComposition(PretFossilLabRegion region,ImmutableDictionary<MapId, PretWorldMap> maps,ImmutableDictionary<MapId, PretWorldMap> previous,PretStoryCompilation scripts)
    {
        if (region.NewMaps.Count != 2 )
            throw new InvalidDataException("fossil_lab_map_composition");
        foreach (var (name, width) in new[] { ("entrance", 28),("experiment-room", 15) })
        {
            var map = maps[new("map.cinnabar-island-pokemon-lab-" + name)];
            if (map.Width != width || map.Height != 11 || map.Events.GetProperty("allow_cycling").GetBoolean() ||
                map.Events.GetProperty("allow_escaping").GetBoolean() || map.Events.GetProperty("allow_running").GetBoolean())
                throw new InvalidDataException("fossil_lab_field_scope");
        }
        var entrance = maps[new("map.cinnabar-island")].Events.GetProperty("warp_events")[2];
        if (entrance.GetProperty("x").GetInt32() != 8 || entrance.GetProperty("y").GetInt32() != 9 ||
            entrance.GetProperty("dest_map").GetString() != "MAP_CINNABAR_ISLAND_POKEMON_LAB_ENTRANCE" ||
            entrance.GetProperty("dest_warp_id").GetString() != "1") throw new InvalidDataException("fossil_lab_city_entrance");
        foreach (var label in new[] { "FossilScientist","ShowHelixFossil","ShowDomeFossil","GiveOmanyte","GiveKabuto" })
        {
            var program = scripts.FindProgram("CinnabarIsland_PokemonLab_ExperimentRoom_EventScript_" + label);
            if (program is null || program.Instructions.IsEmpty || program.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException("fossil_lab_story_incomplete:" + label);
        }
        var ready = scripts.FindProgram("CinnabarIsland_PokemonLab_Entrance_EventScript_ReadyFossil");
        if (ready is null || ready.Instructions.Any(i => i.Op == StoryOp.Unsupported))
            throw new InvalidDataException("fossil_lab_ready_callback");
    }
}
