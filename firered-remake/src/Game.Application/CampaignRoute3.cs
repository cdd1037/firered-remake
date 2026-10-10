using Game.Domain.Events;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool OwnedHeldItemSupported(CampaignState state,PartyPokemon owned)=>owned.HeldItem=="ITEM_NONE" ||
        _content.VermilionEnabled&&VermilionPickupRules.IsKnownHeldItem(owned.HeldItem)||
        _content.MtMoonEnabled&&owned.HeldItem is "ITEM_ORAN_BERRY" or "ITEM_TINY_MUSHROOM" or "ITEM_BIG_MUSHROOM" or "ITEM_MOON_STONE" ||
        _content.Route3Enabled&&owned.HeldItem=="ITEM_ORAN_BERRY"&&
        state.Exploration?.Captured?.Any(c=>c.InstanceId==owned.InstanceId&&c.SourceSpeciesId==39)==true;

    private string? ValidateRoute3Progress(CampaignState state)
    {
        if(!_content.Route3Enabled)return null;
        var p=state.World.Player;
        if(!_content.IsRoute3StablePosition(p.MapId,p.X,p.Y))return "save_route3_boundary_invalid";
        // The current source program supports first battles and their post-battle branches.
        // VS Seeker acquisition/charging and rematch rosters are separate later content.
        if(state.Inventory?.KeyItems?.Any(i=>i.Item.Value=="pret:ITEM_VS_SEEKER")==true)return "save_route3_vs_seeker_unsupported";
        return null;
    }

    private bool TryApplyRoute3Effect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.Route3Enabled)return false;
        var a=effect.Args;
        if(effect.Opcode=="specialvar"&&a.Length==2&&a[1]=="ShouldTryRematchBattle")
        {
            if(a[0]!="VAR_RESULT"||(state.World.Player.MapId.Value!="map.route-3"&&!(_content.VermilionEnabled&&state.World.Player.MapId.Value=="map.route-6"))||state.LastTalked is not {} actor||!state.Actors.TryGetValue(actor,out var physical))
                throw new InvalidDataException("route3_rematch_context_invalid");
            var definition=OrdinaryTrainerScripts.SingleOrDefault(b=>b.ScriptEntry==physical.ScriptId)??throw new InvalidDataException("route3_rematch_trainer_unknown");
            // ShouldTryRematchBattle falls back to HasRematchTrainerAlreadyBeenFought.
            next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",state.BattledTrainers.Contains(definition.Trainer.Value)?1:0)}};
            return true;
        }
        if(effect.Opcode=="trainerbattle_rematch")
        {
            if(a.Length!=3||(state.World.Player.MapId.Value!="map.route-3"&&!(_content.VermilionEnabled&&state.World.Player.MapId.Value=="map.route-6"))||state.Script.Wait?.Kind!=StoryWaitKind.Battle)
                throw new InvalidDataException("route3_rematch_context_invalid");
            var id=new Game.Domain.Ids.TrainerId("pret:"+a[0]);
            var battle=OrdinaryTrainerScripts.SingleOrDefault(x=>x.Trainer==id);
            if(battle is null||effect.Source.Path!=battle.Source.Path||effect.Source.Symbol!=battle.ScriptEntry+"Rematch"||a[1]!=battle.IntroText.Replace("Intro","RematchIntro",StringComparison.Ordinal)||a[2]!=battle.DefeatText||!state.BattledTrainers.Contains(id.Value))
                throw new InvalidDataException("route3_rematch_source_mismatch");
            // EventScript_TryDoRematchBattle: IsTrainerReadyForRematch is FALSE with no
            // VS Seeker state, so gotopostbattlescript resumes this exact source continuation.
            var changed=state with{Script=state.Script with{Locked=true,Variables=state.Script.Variables.SetItem("VAR_RESULT",0)}};
            next=Advance(changed,StoryRules.Continue(changed.Script,changed.Script.Wait!.CorrelationId,_content));
            return true;
        }
        return false;
    }
}
