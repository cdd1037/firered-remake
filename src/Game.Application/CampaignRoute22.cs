using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string Route22RivalActor="LOCALID_ROUTE22_RIVAL";
    private bool IsRoute22GuardButton(CampaignState state)=>state.Script.Wait?.Kind==StoryWaitKind.ButtonPress && !state.Script.Stack.IsEmpty &&
        state.Script.Stack[^1].ProgramId is "Route23_EventScript_MissingBadge" or "Route23_EventScript_MissingBadgeTrigger" or
        "Route23_EventScript_AlreadyRecognizedBoulderBadge" or "Route23_EventScript_RecognizeBoulderBadge" or "Route23_EventScript_RecognizeBoulderBadgeTrigger";

    private Decision<CampaignState> DecideRoute22GuardButton(CampaignState state,CampaignCommand command)
    {
        if(!IsRoute22GuardButton(state)||command.Text is not ("A" or "B"))return new(CommandStatus.Rejected,state,"route22_guard_button_invalid");
        var next=state with{PresentationEffects=[],PresentationTicks=0,Motions=[]};
        return new(CommandStatus.Accepted,Pump(Advance(next,StoryRules.Continue(next.Script,next.Script.Wait!.CorrelationId,_content))));
    }

    private bool TryApplyRoute22Effect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(effect.Opcode=="waitbuttonpress"&&effect.Source.Path=="data/scripts/route23.inc")
        {
            if(!IsRoute22GuardButton(state)||state.Dialogue is null)throw new InvalidDataException("route22_guard_button_context");
            return true;
        }
        if(effect.Opcode!="bufferstdstring"||effect.Source.Path is not ("data/maps/Route22_NorthEntrance/scripts.inc" or "data/maps/Route23/scripts.inc"))return false;
        if(effect.Args.Length!=2)throw new InvalidDataException("route22_badge_string_arity");
        bool boulder=effect.Source.Path=="data/maps/Route22_NorthEntrance/scripts.inc" &&
            state.World.Player.MapId.Value=="map.route-22-north-entrance" && effect.Args[1]=="STDSTRING_BOULDER_BADGE";
        bool cascade=effect.Source.Path=="data/maps/Route23/scripts.inc" &&
            state.World.Player.MapId.Value=="map.route-23" && effect.Args[1]=="STDSTRING_CASCADE_BADGE";
        if(effect.Args.Length!=2||effect.Args[0]!="STR_VAR_1"||!(boulder||cascade))throw new InvalidDataException("route22_badge_string_unsupported");
        next=state with{TextBuffers=state.TextBuffers.SetItem("STR_VAR_1",boulder?"BOULDERBADGE":"CASCADEBADGE")};
        return true;
    }

    private CampaignState BeginRoute22RivalBattle(CampaignState state,StoryEffect effect)
    {
        if(effect.Args.Length!=4||state.World.Player.MapId.Value!="map.route-22"||
            state.Script.Wait?.Kind!=StoryWaitKind.Battle||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE22")!=1||
            !state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET")||!state.Actors.TryGetValue(Route22RivalActor,out var actor)||!actor.Active)
            throw new InvalidDataException("route22_rival_context_invalid");
        var trainer=new TrainerId("pret:"+effect.Args[0]);
        _ = RequireTrainerOccurrence(trainer, effect);
        if (!_content.Route22Trainers!.Battles.TryGetValue(trainer, out var source) ||
            state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON") != source.PlayerStarterVar ||
            state.BattledTrainers.Contains(trainer.Value))
            throw new InvalidDataException("route22_rival_source_mismatch");
        var prepared=PrepareForestTrainerBattle(state,trainer);var frame=prepared.State!;var combat=CampaignTrainerReadProjection.From(frame);
        int active=Enumerable.Range(0,state.Party.Length).First(i=>!state.Party[i].IsEgg&&state.Party[i].Pokemon.CurrentHp>0);
        var owned=state.Party[active];string id="forest-trainer-battle:"+((state.ForestTrainerHistory?.Length??0)+1);
        var presentation=new CampaignWildBattle(id,owned,frame,ProjectTrainerWildEvents(prepared.Events),0,combat.Battle.Player.Pokemon,combat.Battle.Opponent.Pokemon,
            CampaignBattlePhase.Events,[],0,prepared.Trace)
        {DisplayBatch = new(CampaignBattleDisplayBodies.FromSlots(prepared.EntrySlots)), SourceEvents=prepared.Events,ActivePartyIndex=active,FaintedMembers=state.Party.Where(p=>p.Pokemon.CurrentHp==0).Select(p=>p.InstanceId).ToImmutableSortedSet(),
            OpponentHpAtLastSwitch=combat.Battle.Opponent.Pokemon.CurrentHp};
        var battle=new CampaignForestTrainerBattle(id,trainer,Route22RivalActor,presentation,CampaignForestTrainerStage.Combat,
            state.Party.Select(p=>p.Pokemon.Level).ToImmutableArray(),ProjectTrainerEvents(frame,prepared.Events),0,0,[0]);
        return PrepareForestTrainerPresentation(ProjectSourceTrainerOwnedParty(ResetFieldPoisonCounterForBattle(state) with{ForestTrainerBattle=battle,Dialogue=null,Motions=[],PresentationTicks=0,
            World=state.World with{Rng=combat.Battle.Rng},SeenSpecies=state.SeenSpecies.Add(combat.Battle.Opponent.Pokemon.SourceSpeciesId),Music="MUS_VS_TRAINER"}));
    }

    private CampaignState ReturnFromRoute22Rival(CampaignState state)
    {
        var trainer=state.ForestTrainerBattle!;
        if(trainer.Frame.Outcome is WildBattleOutcome.Lost or WildBattleOutcome.Draw)
        {
            var lost=state with{ForestTrainerBattle=null,Dialogue=null,Motions=[],PresentationTicks=0,
                Script=state.Script with{Stack=[],Wait=null,Completed=true,Locked=false,Variables=state.Script.Variables.SetItem("VAR_RESULT",1)}};
            return WhiteOutFromRoute1(lost);
        }
        var next=state with{ForestTrainerBattle=null,Dialogue=null,Motions=[],PresentationTicks=0,
            BattledTrainers=state.BattledTrainers.Add(trainer.Trainer.Value),Exploration=ResetEncounterModifiers(state.Exploration!),
            Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",0)}};
        // CB2_EndTrainerBattle marks the trainer and resumes the original caller stack.
        // Only that script removes the rival and advances Route22 scene to 2.
        return Pump(Advance(next,StoryRules.Continue(next.Script,next.Script.Wait!.CorrelationId,_content)));
    }

    private string? ValidateRoute22Progress(CampaignState state)
    {
        if(ValidateLeagueRivalProgress(state) is {} lateError)return lateError;
        var rivals=(state.ForestTrainerHistory??[]).Where(h=>_content.Route22Trainers!.Trainers.Keys.Any(t=>t.Value==h.TrainerId)).ToArray();
        bool won=rivals.Any(h=>h.Outcome==WildBattleOutcome.Won);
        int scene=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE22");
        bool northRivalStarted=(state.ForestTrainerHistory??[]).Any(h=>IsCeruleanRival(new(h.TrainerId)));
        int expected=state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET")?(won||northRivalStarted?2:1):0;
        if(ViridianGymVictory(state))expected=LeagueRivalVictory(state)?4:3;
        if(scene!=expected||rivals.Any(h=>_content.Route22Trainers!.Battles[new(h.TrainerId)].PlayerStarterVar!=state.Script.Variables.GetValueOrDefault("VAR_STARTER_MON")))
            return "save_route22_rival_progress_invalid";
        int gate=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROUTE23");
        if(gate<0||gate>(8)||Enumerable.Range(1,gate).Any(i=>!state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+i+"_GET")))return "save_route23_gate_invalid";
        if(state.World.Player.MapId.Value=="map.route-23"&&(gate<1||!_content.IsRoute22StablePosition(state.World.Player.MapId,state.World.Player.X,state.World.Player.Y)))return "save_route23_boundary_invalid";
        bool extraBalls=state.Script.Flags.GetValueOrDefault("FLAG_GOT_POKEBALLS_FROM_OAK_AFTER_22_RIVAL");
        if(state.Script.Flags.GetValueOrDefault("FLAG_OAK_SKIP_22_RIVAL_CHECK")&&!extraBalls||extraBalls&&!won)
            return "save_route22_oak_progress_invalid";
        if(state.World.Player.MapId.Value=="map.route-22"&&state.Actors.TryGetValue(Route22RivalActor,out var rival)&&rival.Active)return "save_route22_rival_actor_active";
        return null;
    }
}
