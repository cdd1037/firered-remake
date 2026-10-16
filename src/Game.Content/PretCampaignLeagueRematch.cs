using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Events;
using System.Collections.Immutable;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public LeagueRematchTrainerCatalog LeagueRematchTrainerParties { get; }

    private static void ValidateLeagueRematchComposition(LeagueRematchTrainerCatalog trainers,GameOpponentPokemonCatalog opponents,PretStoryCompilation scripts,ImmutableDictionary<string,string>.Builder texts)
    {
        LeagueRematchTrainerPartyFactory.ValidateCatalog(trainers);
        if (            trainers.Trainers.Count != 7 || trainers.Trainers.Values.Sum(t => t.Party.Length) != 38 ||
            trainers.Trainers.Values.SelectMany(t => t.Party).Any(p => !opponents.Catalog.Species.ContainsKey(p.Species)))
            throw new InvalidDataException("league_rematch_composition_scope");
        foreach (var battle in trainers.Battles.Values)
        {
            var program = scripts.FindProgram(battle.ScriptEntry) ?? throw new InvalidDataException("league_rematch_script_missing");
            if (battle.Mode != 3 || battle.BattleTypeFlags != 8 || !texts.ContainsKey(battle.DefeatText) ||
                !program.Instructions.Any(i => i.Op == StoryOp.TrainerBattle && i.Opcode == "trainerbattle_no_intro" &&
                    i.Source.Path == battle.Source.Path && i.Source.Symbol == battle.ScriptEntry && i.Source.Line == battle.Source.StartLine + 1 &&
                    i.Args.SequenceEqual(new[]{battle.Trainer.Value[5..],battle.DefeatText})))
                throw new InvalidDataException("league_rematch_script_binding");
        }
    }
}
