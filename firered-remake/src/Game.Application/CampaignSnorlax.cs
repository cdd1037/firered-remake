using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Application;

public sealed record CampaignSnorlaxPreparation(int Route,Route1WildPokemonResult Generated);
public sealed record CampaignSnorlaxReturn(int Route,int SourceOutcome);

public sealed partial class CampaignSession
{
    private const string SpecialWildFlag="FLAG_SYS_SPECIAL_WILD_BATTLE";
    private static int SnorlaxRoute(string map)=>map switch{"map.route-12"=>12,"map.route-16"=>16,_=>0};
    private static string SnorlaxHideFlag(int route)=>"FLAG_HIDE_ROUTE_"+route+"_SNORLAX";
    private static string SnorlaxScript(int route)=>"Route"+route+"_EventScript_Snorlax";
    private bool SeenBySnorlax(CampaignState s,int species)=>_content.SnorlaxEnabled&&species==143&&s.Exploration!.Battles.Any(b=>b.ScriptedSnorlaxRoute is 12 or 16);
    private bool IsSnorlaxHistory(CampaignWildHistory b)=>_content.SnorlaxEnabled&&b.ScriptedSnorlaxRoute is 12 or 16&&!b.ScriptedMarowak&&b.SourceSpeciesId==143&&b.Level==30&&
        (b.Outcome==WildBattleOutcome.Won||b.ExperienceGained==0);
    private bool IsSnorlaxCapture(CampaignState s,CampaignCapturedPokemon c)=>c.SourceSpeciesId==143&&c.MetLevel==30&&s.Exploration!.Battles.Any(b=>b.Id==c.BattleId&&IsSnorlaxHistory(b)&&b.Outcome==WildBattleOutcome.Caught&&c.MetLocation=="MAPSEC_ROUTE_"+b.ScriptedSnorlaxRoute);
    private void RequireSnorlaxCaller(CampaignState s,StoryEffect e,int route)
    {
        if(route is not(12 or 16)||e.Source.Path!=$"data/maps/Route{route}/scripts.inc"||e.Source.Symbol!=SnorlaxScript(route)||
            s.LastTalked is not{} actorId||!s.Actors.TryGetValue(actorId,out var actor)||actor.SourceLocalId!=(route==12?5:10)||actor.ScriptId!=SnorlaxScript(route)||
            s.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED")!=actor.SourceLocalId||!s.Script.Flags.GetValueOrDefault("FLAG_GOT_POKE_FLUTE"))
            throw new InvalidDataException("snorlax_script_context");
    }
    private bool TryApplySnorlaxEffect(CampaignState s,StoryEffect e,out CampaignState next)
    {
        next=s;if(!_content.SnorlaxEnabled)return false;
        int route=SnorlaxRoute(s.World.Player.MapId.Value);var a=e.Args;
        int Value(string token)=>s.Script.Variables.TryGetValue(token,out var n)?n:int.TryParse(token,out n)?n:_content.TryConstant(token,out n)?n:throw new InvalidDataException("snorlax_operand: "+token);
        bool caller=e.Source.Path==$"data/maps/Route{route}/scripts.inc"&&e.Source.Symbol==SnorlaxScript(route);
        if(e.Opcode=="special"&&caller&&a.SequenceEqual(new[]{"QuestLog_CutRecording"}))
        {RequireSnorlaxCaller(s,e,route);return true;}
        if(e.Opcode=="setwildbattle"&&caller)
        {
            RequireSnorlaxCaller(s,e,route);
            if(a.Length is <2 or >3||Value(a[0])!=143||Value(a[1])!=30||a.Length==3&&Value(a[2])!=0||s.SnorlaxPreparation is not null||s.SnorlaxReturn is not null||s.Script.Flags.GetValueOrDefault(SnorlaxHideFlag(route))||s.Script.Flags.GetValueOrDefault(SpecialWildFlag))
                throw new InvalidDataException("snorlax_preparation_invalid");
            var made=SnorlaxBattleContract.Create(BattlePokemon,s.TrainerId,s.World.Rng,fuchsiaRules:_content.FuchsiaRouteEnabled);
            next=s with{SnorlaxPreparation=new(route,new(made.Pokemon,made.Rng,made.Trace)),World=s.World with{Rng=made.Rng}};return true;
        }
        if(e.Opcode=="playmoncry"&&caller)
        {
            RequireSnorlaxCaller(s,e,route);
            if(a.Length!=2||Value(a[0])!=143||Value(a[1])!=2||s.SnorlaxPreparation?.Route!=route)throw new InvalidDataException("snorlax_encounter_cry_context");
            return true;
        }
        if(e.Opcode=="dowildbattle")
        {
            RequireSnorlaxCaller(s,e,route);
            var pending=s.SnorlaxPreparation;
            if(a.Length!=0||pending?.Route!=route||s.Script.Wait?.Kind!=StoryWaitKind.Battle||!s.Script.Flags.GetValueOrDefault(SnorlaxHideFlag(route))||!s.Script.Flags.GetValueOrDefault(SpecialWildFlag)||route==12&&!s.Script.Flags.GetValueOrDefault("FLAG_WOKE_UP_ROUTE_12_SNORLAX")||s.World.Rng!=pending.Generated.GlobalRng)
                throw new InvalidDataException("snorlax_battle_start_context");
            next=BeginWildBattle(s with{SnorlaxPreparation=null},pending.Generated,scriptedSnorlaxRoute:route);return true;
        }
        if(e.Opcode=="specialvar"&&a.Length==2&&a[1]=="GetBattleOutcome")
        {
            RequireSnorlaxCaller(s,e,route);
            if(a[0]!="VAR_RESULT"||s.SnorlaxReturn is not{} result||result.Route!=route||s.Script.Flags.GetValueOrDefault(SpecialWildFlag))throw new InvalidDataException("snorlax_battle_result_context");
            next=s with{SnorlaxReturn=null,Script=s.Script with{Variables=s.Script.Variables.SetItem("VAR_RESULT",result.SourceOutcome)}};return true;
        }
        return false;
    }
    private static int SnorlaxSourceOutcome(CampaignWildBattle battle)=>battle.State.Outcome switch
    {
        WildBattleOutcome.Won=>1,WildBattleOutcome.Lost=>2,WildBattleOutcome.Draw=>3,
        WildBattleOutcome.Escaped=>battle.Events.Any(e=>e is WildBattleForcedOutEvent {TrainerBattle:false})?5:4,
        WildBattleOutcome.Caught=>7,_=>throw new InvalidDataException("snorlax_battle_not_ended")
    };
    private CampaignState ReturnFromSnorlaxBattle(CampaignState s,CampaignWildBattle battle)
    {
        int route=battle.ScriptedSnorlaxRoute;
        if(route!=SnorlaxRoute(s.World.Player.MapId.Value)||s.Script.Wait is not {Kind:StoryWaitKind.Battle} wait||!s.Script.Flags.GetValueOrDefault(SpecialWildFlag))throw new InvalidDataException("snorlax_return_context");
        var caller=s.Script;
        // The map OnResume callback runs before the suspended event continues. It
        // must remove the current actor, without clearing temp vars/re-entering the map.
        var resume=_content.MapDefinition(s.World.Player.MapId).Programs.OnResume;
        if(resume!=$"Route{route}_OnResume")throw new InvalidDataException("snorlax_resume_source");
        var next=s with{WildBattle=null,Dialogue=null,Music=_content.MapDefinition(s.World.Player.MapId).Music,
            Exploration=ResetEncounterModifiers(s.Exploration!),SnorlaxReturn=new(route,SnorlaxSourceOutcome(battle)),
            Script=caller with{Stack=[],Wait=null,Completed=true}};
        next=StartStory(next,resume);
        for(int i=0;i<8&&next.Script.Wait is {Kind:StoryWaitKind.NativeEffect} effect;i++)
            next=Advance(next,StoryRules.Continue(next.Script,effect.CorrelationId,_content));
        if(!next.Script.Completed||next.Script.Wait is not null||!next.Script.Stack.IsEmpty||next.Actors[s.LastTalked!].Active)
            throw new InvalidDataException("snorlax_resume_incomplete");
        next=next with{Script=next.Script with{Stack=caller.Stack,Wait=caller.Wait,Completed=caller.Completed,Locked=caller.Locked}};
        return Pump(Advance(next,StoryRules.Continue(next.Script,wait.CorrelationId,_content)));
    }
    private string? ValidateSnorlaxProgress(CampaignState s)
    {
        if(s.SnorlaxPreparation is not null||s.SnorlaxReturn is not null)return "save_snorlax_event_active";
        var battles=s.Exploration?.Battles??[];
        if(!_content.SnorlaxEnabled)return battles.Any(b=>b.ScriptedSnorlaxRoute!=0)||s.Script.Flags.GetValueOrDefault(SnorlaxHideFlag(12))||s.Script.Flags.GetValueOrDefault(SnorlaxHideFlag(16))||s.Script.Flags.GetValueOrDefault("FLAG_WOKE_UP_ROUTE_12_SNORLAX")?"save_snorlax_profile_missing":null;
        if(!_content.IsSnorlaxStablePosition(s.World.Player.MapId,s.World.Player.X,s.World.Player.Y))return "save_snorlax_boundary_invalid";
        if(s.Script.Flags.GetValueOrDefault(SpecialWildFlag))return "save_snorlax_special_flag_active";
        foreach(int route in new[]{12,16})
        {
            var receipts=battles.Where(b=>b.ScriptedSnorlaxRoute==route).ToArray();
            bool cleared=s.Script.Flags.GetValueOrDefault(SnorlaxHideFlag(route));
            if(receipts.Length>1||cleared!=(receipts.Length==1)||receipts.Any(b=>!IsSnorlaxHistory(b))||cleared&&!s.Script.Flags.GetValueOrDefault("FLAG_GOT_POKE_FLUTE"))return "save_snorlax_progress_receipt";
            if(route==12&&cleared!=s.Script.Flags.GetValueOrDefault("FLAG_WOKE_UP_ROUTE_12_SNORLAX"))return "save_snorlax_wake_flag";
            if(SnorlaxRoute(s.World.Player.MapId.Value)==route&&s.Actors.Values.Any(a=>a.ScriptId==SnorlaxScript(route)&&a.Active&&cleared))return "save_snorlax_actor_visible";
        }
        if(s.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE16")!=0)return "save_snorlax_cycling_road_not_open";
        return null;
    }
}
