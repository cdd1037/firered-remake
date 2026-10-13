using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

/// <summary>National Dex successor with the eleven original Kindle Road and Ruby-route maps.</summary>
public sealed partial class PretCampaignCatalog
{
    public const string SeviiRubyContentProfile = "firered-sevii-ruby-campaign-v1";
    public PretSeviiRubyRegion? SeviiRubyRegion { get; }
    public SeviiRubyOwnedPokemonCatalog? SeviiRubyOwnership { get; }
    public SeviiRubyOpponentPokemonCatalog? SeviiRubyOpponents { get; }
    public SeviiRubyTrainerCatalog? SeviiRubyTrainers { get; }
    public SeviiRubyTrainerCatalog? SeviiRubyTrainerParties => SeviiRubyTrainers;
    public SeviiRubyWaterEncounterCatalog? SeviiRubyWaterEncounters { get; }
    public SeviiRubyItemContent? SeviiRubyItems { get; }
    public bool SeviiRubyEnabled => SeviiRubyRegion is not null;

    private static void AppendSeviiRubySources(PretSeviiRubyRegion region, List<PretSourceScriptFile> sources,
        List<string> texts, Dictionary<string, int> constants, List<string> hashes)
    {
        var previous = sources.ToDictionary(s => s.Path, StringComparer.Ordinal);
        foreach (var file in region.Scripts)
        {
            if (previous.TryGetValue(file.Path, out var old))
            {
                if (old.SourceRef.Sha256 != file.SourceRef.Sha256 || old.Text != file.Text)
                    throw new InvalidDataException("sevii_ruby_source_conflict: " + file.Path);
            }
            else { previous.Add(file.Path, file); sources.Add(file); }
            if (!texts.Contains(file.Text, StringComparer.Ordinal)) texts.Add(file.Text);
        }
        foreach (var text in region.TextSources.Values)
            if (!texts.Contains(text, StringComparer.Ordinal)) texts.Add(text);
        MergeRegionalServiceConstants(constants, region.Constants);
        hashes.AddRange(region.AdditionalSourceHashes);
        hashes.Add("sevii-ruby-trainers:" + PretSeviiRubyRules.TrainersSha256);
        hashes.Add("sevii-ruby-owned:" + PretSeviiRubyRules.PokemonSha256);
        hashes.Add("sevii-ruby-rules:" + PretSeviiRubyRules.RulesSha256);
        hashes.Add("sevii-ruby-source-inputs:" + PretSeviiRubyRules.SourceInputsSha256);
        hashes.Add("sevii-ruby-campaign:" + SeviiRubyContentProfile);
    }

    private static void ValidateSeviiRubyAnchors(PretSeviiRubyRegion region, ImmutableDictionary<MapId, PretWorldMap> maps)
    {
        if (maps.Count != 218 || region.NewMaps.Count != 11 || region.NewMaps.Keys.Any(maps.ContainsKey))
            throw new InvalidDataException("sevii_ruby_predecessor_maps");
        foreach (var (name, events) in region.ReusedAnchorEvents)
        {
            var map = maps.Values.SingleOrDefault(m => m.SourceRef.Path == "data/maps/" + name + "/map.json");
            if (map is null || !JsonElement.DeepEquals(map.Events, events))
                throw new InvalidDataException("sevii_ruby_anchor_events: " + name);
        }
    }

    private static IEnumerable<KeyValuePair<int, string>> SeviiRubySpeciesNames(PretPokedexMenu pokedex, SeviiRubyOwnedPokemonCatalog owned) =>
        SeviiRubyOwnedPokemonCatalog.AdditionalSpecies.Select(id =>
        {
            int sourceId = owned.Catalog.Species[id].SourceSpeciesId;
            return KeyValuePair.Create(sourceId, pokedex.Entries[sourceId].Name);
        });

    private static void ValidateSeviiRubyComposition(PretSeviiRubyRegion region,
        ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, PretWorldMap> previous,
        SeviiRubyOwnedPokemonCatalog owned, SeviiRubyOpponentPokemonCatalog opponents, SeviiRubyTrainerCatalog trainers,
        PretStoryCompilation scripts, CampaignPokemonContent pokemon, SeviiRubyItemContent items)
    {
        if (maps.Count != 229 || previous.Count != 218 || previous.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) ||
            region.NewMaps.Any(p => !ReferenceEquals(p.Value, maps[p.Key])) || owned.Catalog.Species.Count != 118 ||
            owned.Predecessor.Catalog.Species.Count != 115 || opponents.Catalog.Species.Count != 135 ||
            opponents.Predecessor.Catalog.Species.Count != 133 || !ReferenceEquals(pokemon.Catalog, owned.Catalog) ||
            pokemon.SpeciesNames[78] != "RAPIDASH" || pokemon.SpeciesNames[218] != "SLUGMA" || pokemon.SpeciesNames[219] != "MAGCARGO")
            throw new InvalidDataException("sevii_ruby_composition_scope");
        SeviiRubyTrainerPartyFactory.ValidateCatalog(trainers);
        foreach (var battle in trainers.BattlesByScript.Values)
            if (scripts.FindProgram(battle.ScriptEntry) is not {} program ||
                !program.Instructions.Any(i => i.Op == StoryOp.TrainerBattle && i.Source.Line >= battle.Source.StartLine &&
                    i.Source.Line <= battle.Source.EndLine && i.Source.Path == battle.Source.Path))
                throw new InvalidDataException("sevii_ruby_trainer_instruction: " + battle.ScriptEntry);
        var ruby = scripts.FindProgram("MtEmber_RubyPath_B5F_EventScript_Ruby")!;
        int flag = Array.FindIndex(ruby.Instructions.ToArray(), i => i.Opcode == "setflag" && i.Args.SequenceEqual(new[] { "FLAG_GOT_RUBY" }));
        int hide = Array.FindIndex(ruby.Instructions.ToArray(), i => i.Opcode == "removeobject" && i.Args.SequenceEqual(new[] { "LOCALID_RUBY" }));
        int give = Array.FindIndex(ruby.Instructions.ToArray(), i => i.Opcode == "additem" && i.Args.SequenceEqual(new[] { "ITEM_RUBY", "1" }));
        if (flag < 0 || hide <= flag || give <= hide || items.Pickups.Length != 8 || items.Pickups.Any(p => p.ItemSymbol == "ITEM_RUBY"))
            throw new InvalidDataException("sevii_ruby_bespoke_reward_order");
        foreach (var symbol in new[] { "MAP_MT_EMBER_SUMMIT", "MAP_MT_EMBER_SUMMIT_PATH_1F", "MAP_MT_EMBER_SUMMIT_PATH_3F" })
            if (maps.ContainsKey(PretSeviiRubyRegion.MapIdFromSourceSymbol(symbol)))
                throw new InvalidDataException("sevii_ruby_excluded_map_admitted");
    }
}
