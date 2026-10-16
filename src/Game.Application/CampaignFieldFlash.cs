using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Transient native field task; only stable FlashLevel and the source flag are saved.</summary>
public sealed record CampaignFlashEffect(int PartyIndex,long InstanceId,int SourceSpeciesId,string Nickname,
    MapId MapId,string Phase,string WaitId,int InitialLevel);

/// <summary>Pinned 037335 source visual parameters, independent of renderer/task scheduling.</summary>
public static class FieldFlashSourcePresentation
{
    public const string ShowMon="show-mon",Illuminate="illuminate";
    public const string ActiveFlag="FLAG_SYS_FLASH_ACTIVE";
    public const int MaximumLevel=4,CenterX=120,CenterY=80,RadiusDelta=2,UpdatesPerRadius=2;
    public static ImmutableArray<int> Radii{get;}=[200,72,56,40,24];
    public static ImmutableArray<int> PlayerFrameDurations{get;}=[4,4,4,4,8];
    public const int IndoorBannerColumns=32,IndoorBannerColumnsPerUpdate=2;
    public const int IndoorBannerOpenFrames=17,IndoorBannerCloseFrames=17;
    public const int MonStartX=320,MonCenterX=120,MonY=80,MonSlidePixelsPerFrame=20;
    public const int MonSlideInFrames=10,MonHoldFrames=30,MonSlideOutFrames=11;
    public const int ShowMonCueFrames=24+IndoorBannerOpenFrames+MonSlideInFrames+MonHoldFrames+MonSlideOutFrames+IndoorBannerCloseFrames;
    public static int Radius(int level)=>level is >=0 and <=MaximumLevel?Radii[level]:throw new ArgumentOutOfRangeException(nameof(level));
    // Draw every radius twice, including the destination, then clear scanline state.
    // The renderer separately acknowledges completion of Task_WaitForFlashUpdate.
    public static int IlluminateCueFrames(int level)=>UpdatesPerRadius*((Radius(0)-Radius(level))/RadiusDelta+1)+1;
    public static int RadiusAtFrame(int initialLevel,int frame)
    {
        int start=Radius(initialLevel);
        return start+Math.Clamp(frame/UpdatesPerRadius,0,(Radius(0)-start)/RadiusDelta)*RadiusDelta;
    }
}

public sealed partial class CampaignSession
{
    private const string FieldFlashMove="pret:MOVE_FLASH";
    private const string FieldFlashProgram="EventScript_FldEffFlash";
    private const string FieldFlashScriptPath="data/scripts/flash.inc";

    public bool CanUseFieldFlash(int partyIndex)=>FieldFlashCommandError(State,partyIndex) is null;
    private static bool KnowsFieldFlash(PartyPokemon mon)=>!mon.IsEgg&&mon.Pokemon.Moves.Any(m=>m.Move.Value==FieldFlashMove);
    private static bool IsFlashOutdoors(FieldMapDefinition map)=>map.MapType is
        "MAP_TYPE_ROUTE" or "MAP_TYPE_TOWN" or "MAP_TYPE_UNDERWATER" or "MAP_TYPE_CITY" or "MAP_TYPE_OCEAN_ROUTE";

    private string? FieldFlashCommandError(CampaignState state,int partyIndex)
    {
        if(!IsStableFieldForParty(state)||state.FlashEffect is not null||state.Fishing is not null||state.CutEffect is not null||
            state.Pc is not null||state.TownMap is not null||state.Repel is not null||state.FieldEffectArguments is not null)
            return "field_flash_event_active";
        if(partyIndex<0||partyIndex>=state.Party.Length)return "field_flash_party_index_invalid";
        if(!KnowsFieldFlash(state.Party[partyIndex]))return "field_flash_move_missing";
        if(!state.Script.Flags.GetValueOrDefault("FLAG_BADGE01_GET"))return "field_flash_badge_required";
        if(!_content.MapDefinition(state.World.Player.MapId).RequiresFlash)return "field_flash_cannot_use_here";
        if(state.Script.Flags.GetValueOrDefault(FieldFlashSourcePresentation.ActiveFlag))return "field_flash_already_active";
        if(state.FlashLevel!=FieldFlashSourcePresentation.MaximumLevel)return "field_flash_level_inconsistent";
        return null;
    }

    private void RequireFieldFlashProgram()
    {
        var program=_content.FindProgram(FieldFlashProgram)??throw new InvalidDataException("field_flash_script_missing");
        if(program.Instructions.Length!=3)throw new InvalidDataException("field_flash_script_invalid");
        var expected=new[]{("animateflash",StoryOp.ScreenFade),("setflashlevel",StoryOp.Special),("end",StoryOp.End)};
        for(int i=0;i<expected.Length;i++)
        {
            var instruction=program.Instructions[i];
            if(instruction.Opcode!=expected[i].Item1||instruction.Op!=expected[i].Item2||
                instruction.Source.Path!=FieldFlashScriptPath||instruction.Source.Symbol!=FieldFlashProgram||
                !instruction.Args.SequenceEqual(i==2?Array.Empty<string>():new[]{"0"}))
                throw new InvalidDataException("field_flash_script_invalid");
        }
    }

    private Decision<CampaignState> StartFieldFlash(CampaignState state,CampaignCommand command)
    {
        var error=FieldFlashCommandError(state,command.PartyIndex);
        if(error is not null)return new(CommandStatus.Rejected,state,error);
        RequireFieldFlashProgram();
        _=checked(Revision+3);_=checked(state.Script.Revision+3);_=checked(state.Script.EffectSequence+4);
        var mon=state.Party[command.PartyIndex];long sequence=state.Script.EffectSequence+1;
        return new(CommandStatus.Accepted,state with
        {
            FlashEffect=new(command.PartyIndex,mon.InstanceId,mon.Pokemon.SourceSpeciesId,mon.Nickname,state.World.Player.MapId,
                FieldFlashSourcePresentation.ShowMon,"field-flash:"+sequence,state.FlashLevel),
            Script=state.Script with{Locked=true,EffectSequence=sequence},
            PresentationEffects=[],PresentationTicks=FieldFlashSourcePresentation.ShowMonCueFrames
        });
    }

    private Decision<CampaignState> DecideFieldFlash(CampaignState state,CampaignCommand command)
    {
        if(command.Kind!="field_flash_complete")return new(CommandStatus.Rejected,state,"field_flash_event_active");
        var flash=state.FlashEffect??throw new InvalidDataException("field_flash_effect_missing");
        if(command.Text!=flash.WaitId)return new(CommandStatus.Rejected,state,"field_flash_callback_mismatch");
        if(!state.Script.Locked||flash.MapId!=state.World.Player.MapId||
            !_content.MapDefinition(flash.MapId).RequiresFlash||flash.PartyIndex<0||flash.PartyIndex>=state.Party.Length||
            state.Party[flash.PartyIndex].InstanceId!=flash.InstanceId||!KnowsFieldFlash(state.Party[flash.PartyIndex])||
            state.FlashLevel!=flash.InitialLevel||flash.InitialLevel!=FieldFlashSourcePresentation.MaximumLevel)
            throw new InvalidDataException("field_flash_context_changed");
        var next=state with{PresentationTicks=0,PresentationEffects=[]};
        if(flash.Phase==FieldFlashSourcePresentation.ShowMon)
        {
            if(!state.Script.Completed||state.Script.Wait is not null||state.Script.Flags.GetValueOrDefault(FieldFlashSourcePresentation.ActiveFlag))
                throw new InvalidDataException("field_flash_show_mon_context_invalid");
            // FldEff_UseFlash runs after the whole native show-mon task has been removed.
            long sequence=checked(state.Script.EffectSequence+1);
            next=next with
            {
                FlashEffect=flash with{Phase=FieldFlashSourcePresentation.Illuminate},
                Script=next.Script with{EffectSequence=sequence,Flags=next.Script.Flags.SetItem(FieldFlashSourcePresentation.ActiveFlag,true)},
                PresentationEffects=[new(sequence,"playse",flash.MapId,["SE_M_REFLECT"])]
            };
            // This exact three-opcode source program was fully checked before field use.
            // Its native task owns these callbacks outside the generic script preflight.
            next=Pump(Advance(next,StoryRules.Start(next.Script,FieldFlashProgram,_content)));
            if(next.Script.Wait is not {Kind:StoryWaitKind.Presentation})throw new InvalidDataException("field_flash_animation_wait_missing");
            return new(CommandStatus.Accepted,next);
        }
        if(flash.Phase!=FieldFlashSourcePresentation.Illuminate||state.Script.Wait is not {Kind:StoryWaitKind.Presentation} wait||
            wait.CorrelationId!=flash.WaitId||!state.Script.Flags.GetValueOrDefault(FieldFlashSourcePresentation.ActiveFlag))
            throw new InvalidDataException("field_flash_animation_context_invalid");
        next=Pump(Advance(next,StoryRules.Continue(next.Script,wait.CorrelationId,_content)));
        if(!next.Script.Completed||next.Script.Wait is not null||next.FlashLevel!=0)throw new InvalidDataException("field_flash_completion_invalid");
        return new(CommandStatus.Accepted,next with{FlashEffect=null,Script=next.Script with{Locked=false},PresentationTicks=0});
    }

    private bool TryApplyFieldFlashEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(effect.Source.Path!=FieldFlashScriptPath||effect.Source.Symbol!=FieldFlashProgram)return false;
        if(effect.Opcode is not ("animateflash" or "setflashlevel"))return false;
        if(state.FlashEffect is not {Phase:FieldFlashSourcePresentation.Illuminate} flash||
            effect.Args.Length!=1||effect.Args[0]!="0"||!state.Script.Flags.GetValueOrDefault(FieldFlashSourcePresentation.ActiveFlag))
            throw new InvalidDataException("field_flash_script_context_invalid");
        if(effect.Opcode=="animateflash")
        {
            if(state.Script.Wait is not {Kind:StoryWaitKind.Presentation} wait)throw new InvalidDataException("field_flash_animation_wait_missing");
            next=state with{FlashEffect=flash with{WaitId=wait.CorrelationId},PresentationTicks=FieldFlashSourcePresentation.IlluminateCueFrames(flash.InitialLevel)};
        }
        else next=state with{FlashLevel=0};
        return true;
    }

    /// <summary>Call on real map entry, never on save restore. Matches LoadMapFromWarp.</summary>
    private CampaignState ApplyFieldFlashMapEntry(CampaignState state)
    {
        if(state.FlashEffect is not null)throw new InvalidDataException("field_flash_map_entry_while_active");
        var map=_content.MapDefinition(state.World.Player.MapId);var flags=state.Script.Flags;
        // Clearing an absent source bit is a no-op; do not modify untouched predecessor payloads.
        if(IsFlashOutdoors(map)&&flags.GetValueOrDefault(FieldFlashSourcePresentation.ActiveFlag))
            flags=flags.SetItem(FieldFlashSourcePresentation.ActiveFlag,false);
        return state with
        {
            FlashLevel=map.RequiresFlash&&!flags.GetValueOrDefault(FieldFlashSourcePresentation.ActiveFlag)?FieldFlashSourcePresentation.MaximumLevel:0,
            Script=state.Script with{Flags=flags}
        };
    }

    private string? ValidateFieldFlashState(CampaignState state)
    {
        if(state.FlashEffect is not null)return "save_field_flash_active";
        bool active=state.Script.Flags.GetValueOrDefault(FieldFlashSourcePresentation.ActiveFlag);
        if(state.FlashLevel is <0 or >FieldFlashSourcePresentation.MaximumLevel)return "save_field_flash_level_invalid";
        var map=_content.MapDefinition(state.World.Player.MapId);
        if(IsFlashOutdoors(map)&&active)return "save_field_flash_outdoor_flag_invalid";
        int expected=map.RequiresFlash&&!active?FieldFlashSourcePresentation.MaximumLevel:0;
        return state.FlashLevel==expected?null:"save_field_flash_level_inconsistent";
    }
}
