using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool OwnedHeldItemSupported(CampaignState state, PartyPokemon owned) =>
        owned.HeldItem == "ITEM_NONE" || CampaignMailHeldItemSupported(state, owned) ||
        CanGiveHeldItem(new("pret:" + owned.HeldItem)) || _binding.EncounterAndPickupHeldItems.Contains(owned.HeldItem);

    private string? ValidateRoute3Progress(CampaignState state)
    {
        var p=state.World.Player;
        if(!_content.IsRoute3StablePosition(p.MapId,p.X,p.Y))return "save_route3_boundary_invalid";
        // The current source program supports first battles and their post-battle branches.
        // VS Seeker acquisition/charging and rematch rosters are separate later content.
        if(!VsSeekerEnabled&&state.Inventory?.KeyItems?.Any(i=>i.Item.Value=="pret:ITEM_VS_SEEKER")==true)return "save_route3_vs_seeker_unsupported";
        return null;
    }

    private bool TryApplyRoute3Effect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;        var a=effect.Args;
        if(effect.Opcode=="specialvar"&&a.Length==2&&a[1]=="ShouldTryRematchBattle")
        {
            if (IsMemorialPillarTrainerMap(state.World.Player.MapId)) RequireMemorialPillarRematchQuery(state,effect);
            if (SeafoamTrainerMap(state.World.Player.MapId)) RequireSeafoamRematchQuery(state,effect);
            if (a[0] == "VAR_RESULT" &&
                state.LastTalked is { } twinsActor && state.Actors.TryGetValue(twinsActor, out var twins) &&
                IsTwinsScript(twins.ScriptId))
            {
                RequireDoubleTrainerRematchQuery(state, effect, twins.ScriptId);
                next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",
                    state.BattledTrainers.Contains(DoubleTrainerId(twins.ScriptId).Value)?1:0)}};
                return true;
            }
            if(a[0]!="VAR_RESULT"||!OrdinaryPostBattleMap(state.World.Player.MapId.Value)||state.LastTalked is not {} actor||!state.Actors.TryGetValue(actor,out var physical))
                throw new InvalidDataException("route3_rematch_context_invalid");
            var definition=OrdinaryTrainerScripts.SingleOrDefault(b=>b.ScriptEntry==physical.ScriptId)??throw new InvalidDataException("route3_rematch_trainer_unknown");
            // ShouldTryRematchBattle falls back to HasRematchTrainerAlreadyBeenFought.
            next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",state.BattledTrainers.Contains(definition.Trainer.Value)?1:0)}};
            return true;
        }
        if(effect.Opcode=="trainerbattle_rematch")
        {
            if(a.Length!=3||!OrdinaryPostBattleMap(state.World.Player.MapId.Value)||state.Script.Wait?.Kind!=StoryWaitKind.Battle)
                throw new InvalidDataException("route3_rematch_context_invalid");
            var id=new Game.Domain.Ids.TrainerId("pret:"+a[0]);
            var battle=OrdinaryTrainerScripts.SingleOrDefault(x=>x.Trainer==id);
            if(battle is null||effect.Source.Path!=battle.Source.Path||effect.Source.Symbol!=battle.ScriptEntry+"Rematch"||a[1]!=battle.IntroText.Replace("Intro","RematchIntro",StringComparison.Ordinal)||a[2]!=battle.DefeatText||!state.BattledTrainers.Contains(id.Value))
                throw new InvalidDataException("route3_rematch_source_mismatch");
            if (IsMemorialPillarTrainer(id)) RequireMemorialPillarRematchContinuation(state,effect,battle);
            if (IsSeafoamTrainer(id)) RequireSeafoamSingleRematch(state,effect,battle);
            // EventScript_TryDoRematchBattle: IsTrainerReadyForRematch is FALSE with no
            // VS Seeker state, so gotopostbattlescript resumes this exact source continuation.
            var changed=state with{Script=state.Script with{Locked=true,Variables=state.Script.Variables.SetItem("VAR_RESULT",0)}};
            next=Advance(changed,StoryRules.Continue(changed.Script,changed.Script.Wait!.CorrelationId,_content));
            return true;
        }
        return false;
    }
    private bool OrdinaryPostBattleMap(string map) => TrainerOccurrences.ActorMaps.Contains(new(map));
}
