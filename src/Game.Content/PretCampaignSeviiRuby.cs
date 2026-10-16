using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Kindle Road and Mt. Ember campaign composition.</summary>
public sealed partial class PretCampaignCatalog
{
    public const string SeviiRubyContentProfile = "firered-sevii-ruby-campaign-v1";
    public PretSeviiRubyRegion SeviiRubyRegion { get; }
    public SeviiRubyTrainerCatalog? SeviiRubyTrainers { get; }
    public SeviiRubyTrainerCatalog? SeviiRubyTrainerParties => SeviiRubyTrainers;
    public WaterEncounterCatalog? SeviiRubyWaterEncounters { get; }
    public SeviiRubyItemContent? SeviiRubyItems { get; }



    private static void ValidateSeviiRubyComposition(PretSeviiRubyRegion region,ImmutableDictionary<MapId, PretWorldMap> maps,GamePokemonCatalog owned,GameOpponentPokemonCatalog opponents,SeviiRubyTrainerCatalog trainers,PretStoryCompilation scripts,CampaignPokemonContent pokemon,SeviiRubyItemContent items)
    {
        if (pokemon.SpeciesNames[78] != "RAPIDASH" || pokemon.SpeciesNames[218] != "SLUGMA" || pokemon.SpeciesNames[219] != "MAGCARGO")
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
        int give = Array.FindIndex(ruby.Instructions.ToArray(), i => i.Opcode == "additem" && i.Args.SequenceEqual(new[] { "ITEM_RUBY","1" }));
        if (flag < 0 || hide <= flag || give <= hide || items.Pickups.Any(p => p.ItemSymbol == "ITEM_RUBY"))
            throw new InvalidDataException("sevii_ruby_bespoke_reward_order");
        foreach (var map in region.NewMaps.Values)
            foreach (var warp in map.FieldWarps)
                if (!maps.TryGetValue(warp.DestinationMapId, out var target) ||
                    warp.DestinationWarpId < 0 || warp.DestinationWarpId >= target.FieldWarps.Length)
                    throw new InvalidDataException("SeviiRuby warp destination is not mounted.");
    }
}
