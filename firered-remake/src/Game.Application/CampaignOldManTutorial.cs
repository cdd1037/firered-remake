using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Tutorials;
using Game.Domain.World;
namespace Game.Application;

public sealed record CampaignOldManTutorial(string Id,OldManTutorialResult Result,int EventIndex,
    int HiddenPlayerIndex,TilePosition Origin,ImmutableArray<PartyPokemon> PartyBefore,InventoryState InventoryBefore,
    ImmutableSortedSet<int> SeenBefore,ImmutableSortedSet<int> CaughtBefore)
{
    [JsonIgnore] public OldManTutorialEvent CurrentEvent=>Result.Events[EventIndex];
    [JsonIgnore] public string WaitId=>Id+":"+EventIndex;
}

public sealed partial class CampaignSession
{
    private bool TryApplyOldManEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(!_content.OldManTutorialEnabled||effect.Opcode!="special")return false;
        if(effect.Args[0]=="QuestLog_CutRecording")
        {
            if(state.Script.QuestLogState!=0)throw new InvalidDataException("quest_log_recording_not_supported");
            return true; // Explicit already-stopped recorder branch; full Quest Log playback remains outside this profile.
        }
        if(effect.Args[0]!="StartOldManTutorialBattle")return false;
        if(state.World.Player.MapId.Value!="map.viridian-city"||effect.Source.Symbol!="ViridianCity_EventScript_DoTutorialBattle"||state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_VIRIDIAN_CITY_OLD_MAN")!=1||state.OldManCompleted||state.OldManTutorial is not null||state.Battle is not null||state.WildBattle is not null||state.Inventory is null)throw new InvalidDataException("old_man_tutorial_context_invalid");
        int index=Array.FindIndex(state.Party.ToArray(),p=>!p.IsEgg&&p.Pokemon.CurrentHp>0);
        if(index<0)throw new InvalidDataException("old_man_no_living_party_member");
        state=ResetFieldPoisonCounterForBattle(state);
        var result=OldManTutorialRules.Start(_content.PokemonContent!.Pokemon,state.Party[index],state.World.Rng);
        var tutorial=new CampaignOldManTutorial("old-man:"+effect.Sequence,result,0,index,state.World.Player,state.Party,state.Inventory!,state.SeenSpecies,state.CaughtSpecies);
        next=PrepareOldManPresentation(state with{OldManTutorial=tutorial,World=state.World with{Rng=result.Rng},Music="MUS_BATTLE_WILD",Dialogue=null,Motions=[],PresentationTicks=0});return true;
    }
    private static bool HiddenTutorialEvent(OldManTutorialEventKind kind)=>kind is OldManTutorialEventKind.WeedleCreated or OldManTutorialEventKind.BattleSetupCompleted or OldManTutorialEventKind.EntryOrderDetermined or OldManTutorialEventKind.EnemyMoveSelected or OldManTutorialEventKind.RestorePlayerBag;
    private CampaignState PrepareOldManPresentation(CampaignState state)
    {
        var t=state.OldManTutorial!;
        while(t.EventIndex<t.Result.Events.Length&&HiddenTutorialEvent(t.CurrentEvent.Kind))t=t with{EventIndex=t.EventIndex+1};
        if(t.EventIndex>=t.Result.Events.Length)throw new InvalidDataException("old_man_presentation_cursor_invalid");
        if(t.CurrentEvent.Kind==OldManTutorialEventKind.SelectPokeBall)state=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_ITEM_ID",4)}};
        if(t.CurrentEvent.Kind==OldManTutorialEventKind.Caught)state=state with{Music="MUS_CAUGHT"};
        return state with{OldManTutorial=t};
    }
    private Decision<CampaignState> DecideOldManTutorial(CampaignState state,CampaignCommand command)
    {
        var t=state.OldManTutorial!;
        if(command.Kind!="old_man_presented"||command.Text!=t.WaitId)return new(CommandStatus.Rejected,state,"old_man_presentation_pending");
        if(t.CurrentEvent.Kind!=OldManTutorialEventKind.ReturnToField)
            return new(CommandStatus.Accepted,PrepareOldManPresentation(state with{OldManTutorial=t with{EventIndex=t.EventIndex+1}}));
        if(state.Script.Wait is not {Kind:StoryWaitKind.Battle} wait||state.World.Player!=t.Origin||!state.Party.SequenceEqual(t.PartyBefore)||CanonicalJson.Hash(state.Inventory)!=CanonicalJson.Hash(t.InventoryBefore)||!state.SeenSpecies.SetEquals(t.SeenBefore)||!state.CaughtSpecies.SetEquals(t.CaughtBefore))return new(CommandStatus.Fault,state,"old_man_player_state_changed");
        var finish=OldManTutorialRules.Finish(t.Result);
        var next=state with{OldManTutorial=null,OldManCompleted=true,Music=_content.MapDefinition(state.World.Player.MapId).Music,PresentationEffects=[],
            Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",finish.SourceOutcome).SetItem("VAR_ITEM_ID",finish.SelectedItemSourceId)}};
        return new(CommandStatus.Accepted,Pump(Advance(next,StoryRules.Continue(next.Script,wait.CorrelationId,_content))));
    }
    private string? ValidateOldManProgress(CampaignState state)
    {
        if(!_content.OldManTutorialEnabled)return state.OldManTutorial is not null||state.OldManCompleted?"save_old_man_profile_missing":null;
        if(state.OldManTutorial is not null)return "save_event_active";
        int scene=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_VIRIDIAN_CITY_OLD_MAN");
        if(scene is <0 or >2||state.OldManCompleted!=(scene==2))return "save_old_man_scene_inconsistent";
        int tv=QuestQuantity(_content.QuestItems!,state.Inventory!,QuestItemRules.TeachyTvSymbol);
        if(tv>1||tv>0&&!state.OldManCompleted)return "save_old_man_reward_inconsistent";
        return null;
    }
}
