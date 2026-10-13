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
    public PretFightingDojoRegion FightingDojoRegion { get; }
    public bool FightingDojoEnabled => FightingDojoRegion is not null;
    public FightingDojoOwnedPokemonCatalog FightingDojoOwnership { get; }
    public FightingDojoOpponentPokemonCatalog FightingDojoOpponents { get; }
    public FightingDojoMachineCatalog FightingDojoMachines { get; }
    public FightingDojoTrainerCatalog FightingDojoTrainers { get; }

    private static void AppendFightingDojoSources(PretFightingDojoRegion region,
        List<PretSourceScriptFile> sources, List<string> texts, Dictionary<string, int> constants, List<string> hashes)
    {
        var previous = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var file in region.Scripts)
        {
            if (previous.TryGetValue(file.Path, out var existing))
            {
                if (existing.SourceRef.Sha256 != file.SourceRef.Sha256 || existing.Text != file.Text)
                    throw new InvalidDataException("fighting_dojo_source_conflict:" + file.Path);
            }
            else { previous.Add(file.Path, file); sources.Add(file); }
            if (!texts.Contains(file.Text, StringComparer.Ordinal)) texts.Add(file.Text);
        }
        foreach (var text in region.TextSources.Values)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
        MergeRegionalServiceConstants(constants, region.Constants);
        hashes.AddRange(region.AdditionalSourceHashes);
    }

    private static void ValidateFightingDojoComposition(PretFightingDojoRegion region,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> previous,
        PretStoryCompilation scripts)
    {
        var id = new MapId("map.saffron-city-dojo");
        if (previous.Count != 311 || maps.Count != 312 || region.NewMaps.Count != 1 ||
            previous.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) ||
            !ReferenceEquals(maps[id], region.NewMaps[id])) throw new InvalidDataException("fighting_dojo_map_composition");
        var map = maps[id]; var events = map.Events;
        if (map.Width != 13 || map.Height != 16 || events.GetProperty("object_events").GetArrayLength() != 7 ||
            events.GetProperty("coord_events").GetArrayLength() != 2 || events.GetProperty("bg_events").GetArrayLength() != 4 ||
            events.GetProperty("warp_events").GetArrayLength() != 3 ||
            events.GetProperty("allow_cycling").GetBoolean() || events.GetProperty("allow_escaping").GetBoolean() ||
            events.GetProperty("allow_running").GetBoolean()) throw new InvalidDataException("fighting_dojo_field_scope");
        var entrance = maps[new("map.saffron-city")].Events.GetProperty("warp_events")[2];
        if (entrance.GetProperty("x").GetInt32() != 40 || entrance.GetProperty("y").GetInt32() != 12 ||
            entrance.GetProperty("dest_map").GetString() != "MAP_SAFFRON_CITY_DOJO" || entrance.GetProperty("dest_warp_id").GetString() != "1")
            throw new InvalidDataException("fighting_dojo_city_entrance");
        foreach (var label in new[] { "TriggerMasterBattleLeft", "TriggerMasterBattleRight", "HitmonleeBall", "HitmonchanBall",
            "GiveHitmon", "ReceivedHitmonParty", "ReceivedHitmonPC", "MasterKoichi", "DefeatedMasterKoichi", "Hitoshi", "Hideki", "Aaron", "Mike" })
        {
            var program = scripts.FindProgram("SaffronCity_Dojo_EventScript_" + label);
            if (program is null || program.Instructions.IsEmpty || program.Instructions.Any(i => i.Op == StoryOp.Unsupported))
                throw new InvalidDataException("fighting_dojo_story_incomplete:" + label);
        }
    }
}
