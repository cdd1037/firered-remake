using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Transient native show-mon or boulder task. Stable actors and the source flag use the existing save payload.</summary>
public sealed record CampaignStrengthEffect(int PartyIndex,long InstanceId,int SourceSpeciesId,string Nickname,
    string ActorId,TilePosition PlayerOrigin,TilePosition BoulderOrigin,TilePosition BoulderDestination,string Phase,string WaitId);

public static class FieldStrengthSourcePresentation
{
    public const string ShowMon="show-mon",Push="push";
    // Indoor ShowMon is the same native field task used by Flash. The boulder uses
    // GetWalkSlowerMovementAction; source quest logging names the 32-update duration.
    public const int ShowMonCueFrames=FieldFlashSourcePresentation.ShowMonCueFrames,PushCueFrames=32;
}

public sealed partial class CampaignSession
{
    private const string FieldStrengthSource="data/scripts/field_moves.inc";
    private const string FieldStrengthMap="map.fuchsia-city-wardens-house";

    public bool CanUseFieldStrength(int partyIndex)=>FieldStrengthCommandError(State,partyIndex) is null;

    private CampaignActor? AdjacentFieldStrengthBoulder(CampaignState state,Direction? direction=null)
    {
        var p=state.World.Player;
        if(!_content.SafariEnabled)return null;
        if(_content.SeviiRubyEnabled&&SeviiRubyMap(p.MapId))return AdjacentSeviiRubyBoulder(state,direction??p.Facing);
        if(_content.VictoryRoadEnabled&&VictoryRoadFieldMap(p.MapId))return AdjacentVictoryRoadBoulder(state,direction??p.Facing);
        if(p.MapId.Value!=FieldStrengthMap)return null;
        var definition=_content.MapDefinition(p.MapId).Actors.SingleOrDefault(a=>a.SourceLocalId==2);
        if(definition is null||definition.GraphicsId!=FieldHmSourceRules.BoulderGraphics||definition.ScriptId!="EventScript_StrengthBoulder"||
            (definition.X,definition.Y,definition.Elevation,definition.MovementType,definition.HideFlag)!=(11,6,3,"MOVEMENT_TYPE_FACE_DOWN","0"))
            throw new InvalidDataException("field_strength_boulder_definition_changed");
        var (dx,dy)=Delta(direction??p.Facing);
        return state.Actors.Values.SingleOrDefault(a=>a.LocalId==definition.LocalId&&a.SourceLocalId==2&&a.GraphicsId==definition.GraphicsId&&
            a.ScriptId==definition.ScriptId&&a.HideFlag=="0"&&a.Active&&!a.Invisible&&a.TemplateX==11&&a.TemplateY==6&&
            a.X==p.X+dx&&a.Y==p.Y+dy&&(p.Elevation==0||a.Elevation==0||p.Elevation==a.Elevation));
    }

    private string? FieldStrengthCommandError(CampaignState state,int partyIndex)
    {
        if(!_content.SafariEnabled)return "field_strength_not_enabled";
        if(state.IsSurfing)return "field_strength_while_surfing";
        if(!IsStableFieldForParty(state)||state.StrengthEffect is not null||state.FlashEffect is not null||state.CutEffect is not null||
            state.Fishing is not null||state.FieldEffectArguments is not null||state.Pc is not null||state.TownMap is not null||state.Repel is not null)
            return "field_strength_event_active";
        if(partyIndex<0||partyIndex>=state.Party.Length)return "field_strength_party_index_invalid";
        if(!FieldHmSourceRules.Knows(state.Party[partyIndex],FieldHmSourceRules.StrengthMove))return "field_strength_move_missing";
        if(!state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthBadge))return "field_strength_badge_required";
        return AdjacentFieldStrengthBoulder(state) is null?"field_strength_nothing_to_push":null;
    }

    private Decision<CampaignState> StartFieldStrength(CampaignState state,CampaignCommand command)
    {
        var error=FieldStrengthCommandError(state,command.PartyIndex);
        if(error is not null)return new(CommandStatus.Rejected,state,error);
        var boulder=AdjacentFieldStrengthBoulder(state)!;
        var context=state with
        {
            LastTalked=boulder.LocalId,
            Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",command.PartyIndex).SetItem("VAR_LAST_TALKED",boulder.SourceLocalId)},
            FieldEffectArguments=ImmutableSortedDictionary<int,int>.Empty.Add(0,command.PartyIndex)
        };
        // Native party callback deliberately permits reactivation and has no Yes/No.
        return new(CommandStatus.Accepted,Pump(StartStory(context,"EventScript_FldEffStrength")));
    }

    private bool TryApplyFieldStrengthEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(!_content.SafariEnabled||effect.Source.Path!=FieldStrengthSource||effect.Source.Symbol is not
            ("EventScript_StrengthBoulder" or "EventScript_FldEffStrength" or "EventScript_UseStrength" or "EventScript_CantMoveBoulder" or
             "EventScript_AlreadyUsedStrength" or "EventScript_DontUseStrength"))return false;
        var a=effect.Args;
        int Value(string token)=>int.TryParse(token,out var n)?n:state.Script.Variables.TryGetValue(token,out n)?n:
            _content.TryConstant(token,out n)?n:throw new InvalidDataException("field_strength_value_missing: "+token);
        bool Is(string token,string symbol)=>token==symbol||_content.TryConstant(symbol,out int value)&&Value(token)==value;
        CampaignActor RequireBoulder()
        {
            var boulder=AdjacentFieldStrengthBoulder(state);
            if(boulder is null||state.LastTalked!=boulder.LocalId||state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED")!=boulder.SourceLocalId)
                throw new InvalidDataException("field_strength_boulder_context_invalid");
            return boulder;
        }
        switch(effect.Opcode)
        {
            case "checkpartymove":
                RequireBoulder();if(a.Length!=1||!Is(a[0],"MOVE_STRENGTH"))throw new InvalidDataException("field_strength_move_unsupported");
                int selected=FieldHmSourceRules.FirstPartyMember(state.Party,FieldHmSourceRules.StrengthMove);
                var vars=state.Script.Variables.SetItem("VAR_RESULT",selected);
                if(selected!=FieldHmSourceRules.NoPartyMember)vars=vars.SetItem("VAR_0x8004",state.Party[selected].Pokemon.SourceSpeciesId);
                next=state with{Script=state.Script with{Variables=vars}};return true;
            case "setfieldeffectargument":
                RequireBoulder();if(a.Length!=2||Value(a[0])!=0)throw new InvalidDataException("field_strength_argument_invalid");
                int index=unchecked((short)Value(a[1]));
                if(index<0||index>=state.Party.Length||!FieldHmSourceRules.Knows(state.Party[index],FieldHmSourceRules.StrengthMove))throw new InvalidDataException("field_strength_party_argument_invalid");
                next=state with{FieldEffectArguments=ImmutableSortedDictionary<int,int>.Empty.Add(0,index)};return true;
            case "dofieldeffect":
                var actor=RequireBoulder();
                if(a.Length!=1||!Is(a[0],"FLDEFF_USE_STRENGTH")||state.StrengthEffect is not null||!state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthBadge)||
                    state.FieldEffectArguments is not {} args||!args.TryGetValue(0,out int partyIndex)||partyIndex<0||partyIndex>=state.Party.Length||
                    !FieldHmSourceRules.Knows(state.Party[partyIndex],FieldHmSourceRules.StrengthMove))throw new InvalidDataException("field_strength_effect_invalid");
                var mon=state.Party[partyIndex];var position=new TilePosition(state.World.Player.MapId,actor.X,actor.Y,actor.Elevation,actor.Facing);
                next=state with
                {
                    StrengthEffect=new(partyIndex,mon.InstanceId,mon.Pokemon.SourceSpeciesId,mon.Nickname,actor.LocalId,state.World.Player,position,position,FieldStrengthSourcePresentation.ShowMon,""),
                    TextBuffers=state.TextBuffers.SetItem("STR_VAR_1",mon.Nickname),PresentationTicks=FieldStrengthSourcePresentation.ShowMonCueFrames
                };return true;
            case "waitstate":
                RequireBoulder();
                if(state.StrengthEffect is not {Phase:FieldStrengthSourcePresentation.ShowMon} strength||state.Script.Wait is not {Kind:StoryWaitKind.Presentation} wait)
                    throw new InvalidDataException("field_strength_wait_without_effect");
                next=state with{StrengthEffect=strength with{WaitId=wait.CorrelationId}};return true;
            case "releaseall":
                next=state with{FieldEffectArguments=null,StrengthEffect=null,Dialogue=null,SignMessage=null,MessagePointer=null};return true;
            default:return false;
        }
    }

    private CampaignState CompleteFieldStrengthPresentation(CampaignState before,CampaignState next)
    {
        if(before.StrengthEffect is not {} effect)
        {
            // The compact interpreter projects msgbox as one wait. Strength's source
            // MSGBOX_SIGN owns Std_MsgboxSign's releaseall after its final text page.
            if(_content.SafariEnabled&&(before.World.Player.MapId.Value==FieldStrengthMap||_content.SeviiRubyEnabled&&SeviiRubyMap(before.World.Player.MapId)||_content.VictoryRoadEnabled&&VictoryRoadFieldMap(before.World.Player.MapId))&&
                before.Script.Wait?.Kind==StoryWaitKind.Dialog&&before.Dialogue is {CloseOnCompletion:true} dialogue&&
                dialogue.TextSymbol is "Text_MonUsedStrengthCanMoveBoulders" or "Text_MonMayPushBoulder" or "Text_StrengthMadeMovingBouldersPossible"&&
                !before.Script.Stack.IsEmpty)
            {
                var frame=before.Script.Stack[^1];var program=_content.FindProgram(frame.ProgramId);
                int index=frame.InstructionIndex-1;
                if(program is null||index<0||index>=program.Instructions.Length)throw new InvalidDataException("field_strength_sign_cursor_invalid");
                var instruction=program.Instructions[index];
                if(instruction.Source.Path!=FieldStrengthSource||instruction.Opcode!="msgbox"||
                    !instruction.Args.SequenceEqual(new[]{dialogue.TextSymbol,"MSGBOX_SIGN"}))throw new InvalidDataException("field_strength_sign_context_invalid");
                return next with{Script=next.Script with{Locked=false},Dialogue=null,SignMessage=null,MessagePointer=null,FieldEffectArguments=null};
            }
            return next;
        }
        if(effect.Phase!=FieldStrengthSourcePresentation.ShowMon||before.Script.Wait is not {Kind:StoryWaitKind.Presentation} wait||
            wait.CorrelationId!=effect.WaitId||!before.PresentationEffects.Any(e=>e.Opcode=="waitstate")||before.World.Player!=effect.PlayerOrigin||
            effect.PartyIndex<0||effect.PartyIndex>=before.Party.Length||before.Party[effect.PartyIndex].InstanceId!=effect.InstanceId||
            !FieldHmSourceRules.Knows(before.Party[effect.PartyIndex],FieldHmSourceRules.StrengthMove)||
            AdjacentFieldStrengthBoulder(before) is not {} boulder||boulder.LocalId!=effect.ActorId||
            (boulder.X,boulder.Y,boulder.Elevation)!=(effect.BoulderOrigin.X,effect.BoulderOrigin.Y,effect.BoulderOrigin.Elevation))
            throw new InvalidDataException("field_strength_show_mon_context_changed");
        // ShowMonCB_UseStrength removes the effect and enables the script. The actual
        // EventScript_UseStrength sets the source flag and presents the original text.
        return next with{StrengthEffect=null,FieldEffectArguments=null};
    }

    private FieldHmCollision FieldStrengthCollision(CampaignState state,CampaignActor boulder,Direction direction,out TilePosition target)
    {
        var p=state.World.Player;var (dx,dy)=Delta(direction);int x=boulder.X+dx,y=boulder.Y+dy;
        var map=EditedFieldMaps(state)[p.MapId];var here=map.CellAt(boulder.X,boulder.Y)??throw new InvalidDataException("field_strength_origin_missing");
        var cell=map.CellAt(x,y);
        bool Occupies(int elevation)=>boulder.Elevation==0||elevation==0||boulder.Elevation==elevation;
        bool occupied=p.X==x&&p.Y==y&&Occupies(p.Elevation)||state.Actors.Values.Any(a=>a.LocalId!=boulder.LocalId&&a.Active&&a.X==x&&a.Y==y&&Occupies(a.Elevation));
        target=new(p.MapId,x,y,cell?.Elevation is null or 0 or 15?boulder.Elevation:cell.Elevation,direction);
        return FieldHmSourceRules.StrengthTargetCollision(here,cell,boulder.Elevation,direction,occupied);
    }

    /// <summary>Before ordinary player actor collision; a push consumes no walking step.</summary>
    private Decision<CampaignState>? TryStartFieldStrengthPush(CampaignState state,CampaignCommand command)
    {
        if(state.IsSurfing||command.Kind!="move"||!state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag))return null;
        var boulder=AdjacentFieldStrengthBoulder(state,command.Direction);if(boulder is null)return null;
        var collision=FieldStrengthCollision(state,boulder,command.Direction,out var destination);
        var target=EditedFieldMaps(state)[state.World.Player.MapId].CellAt(destination.X,destination.Y);
        if(target is null)return null;
        var result=FieldHmSourceRules.ClassifyStrengthPush(true,boulder.GraphicsId,target.SourceBehavior,collision);
        if(result==FieldStrengthPush.FallWarpRequiresHandler&&!VictoryRoadBoulderFallSupported(state,boulder,destination))return new(CommandStatus.Rejected,state,"field_strength_fall_warp_not_supported");
        if(result is not (FieldStrengthPush.OrdinaryPush or FieldStrengthPush.FallWarpRequiresHandler))return null;
        if(!IsStableFieldForParty(state)||state.StrengthEffect is not null)return new(CommandStatus.Rejected,state,"field_strength_event_active");
        string suffix=command.Direction switch{Direction.North=>"up",Direction.South=>"down",Direction.West=>"left",_=>"right"};
        var origin=state.World.Player with{Facing=command.Direction};
        var boulderOrigin=new TilePosition(origin.MapId,boulder.X,boulder.Y,boulder.Elevation,boulder.Facing);
        long sequence=checked(state.Script.EffectSequence+1);
        var next=state with{World=state.World with{Player=origin,Revision=checked(state.World.Revision+1)},PresentationEffects=[],Motions=[]};
        next=ApplyMovementActions(next,"LOCALID_PLAYER",["walk_in_place_normal_"+suffix,"step_end"]);
        next=ApplyMovementActions(next,boulder.LocalId,["walk_slower_"+suffix,"step_end"]);
        return new(CommandStatus.Accepted,next with
        {
            StrengthEffect=new(-1,0,0,"",boulder.LocalId,origin,boulderOrigin,destination,FieldStrengthSourcePresentation.Push,"field-strength:"+sequence),
            Script=next.Script with{Locked=true,EffectSequence=sequence},PresentationTicks=FieldStrengthSourcePresentation.PushCueFrames,
            PresentationEffects=[new(sequence,"playse",origin.MapId,["SE_M_STRENGTH"])]
        });
    }

    private Decision<CampaignState> CompleteFieldStrengthPush(CampaignState state,CampaignCommand command)
    {
        if(command.Kind!="field_strength_complete")return new(CommandStatus.Rejected,state,"field_strength_event_active");
        var effect=state.StrengthEffect??throw new InvalidDataException("field_strength_push_missing");
        if(command.Text!=effect.WaitId)return new(CommandStatus.Rejected,state,"field_strength_callback_mismatch");
        if(effect.Phase!=FieldStrengthSourcePresentation.Push||!state.Script.Locked||!state.Script.Completed||state.Script.Wait is not null||
            !state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag)||state.World.Player!=effect.PlayerOrigin||
            !state.Actors.TryGetValue(effect.ActorId,out var boulder)||!boulder.Active||boulder.Invisible||
            (boulder.X,boulder.Y,boulder.Elevation,boulder.Facing)!=(effect.BoulderDestination.X,effect.BoulderDestination.Y,effect.BoulderDestination.Elevation,effect.BoulderDestination.Facing))
            throw new InvalidDataException("field_strength_push_context_changed");
        var complete=state with{StrengthEffect=null,Script=state.Script with{Locked=false},Motions=[],PresentationTicks=0,PresentationEffects=[]};
        return new(CommandStatus.Accepted,CompleteVictoryRoadBoulderPush(complete,boulder));
    }

    /// <summary>ClearTempFieldEventData runs on real map entry, never on Continue.</summary>
    private CampaignState ApplyFieldStrengthMapEntry(CampaignState state)
    {
        if(state.StrengthEffect is not null)throw new InvalidDataException("field_strength_map_entry_while_active");
        ValidateVictoryRoadMapEntry(state);
        return _content.SafariEnabled&&state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag)
            ?state with{Script=state.Script with{Flags=state.Script.Flags.SetItem(FieldHmSourceRules.StrengthActiveFlag,false)}}:state;
    }

    private string? ValidateFieldStrengthState(CampaignState state)
    {
        if(state.StrengthEffect is not null)return "save_field_strength_active";
        bool active=state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag);
        if(!_content.SafariEnabled)return active?"save_field_strength_profile_missing":null;
        if(active&&(state.World.Player.MapId.Value!=FieldStrengthMap&&!(_content.SeviiRubyEnabled&&SeviiRubyMap(state.World.Player.MapId))&&!(_content.VictoryRoadEnabled&&VictoryRoadFieldMap(state.World.Player.MapId))||!state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthBadge)))return "save_field_strength_flag_invalid";
        if(_content.SeviiRubyEnabled&&SeviiRubyMap(state.World.Player.MapId))return ValidateSeviiRubyBoulders(state);
        if(_content.VictoryRoadEnabled&&VictoryRoadFieldMap(state.World.Player.MapId))return ValidateVictoryRoadBoulders(state);
        if(state.World.Player.MapId.Value!=FieldStrengthMap)return null;
        var boulder=state.Actors.Values.SingleOrDefault(a=>a.SourceLocalId==2);
        if(boulder is null||boulder.TemplateX!=11||boulder.TemplateY!=6||boulder.Elevation!=3||boulder.Invisible||
            (boulder.X!=11||boulder.Y!=6)&&!active)return "save_field_strength_boulder_invalid";
        var cell=EditedFieldMaps(state)[state.World.Player.MapId].CellAt(boulder.X,boulder.Y);
        if(cell is null||cell.Collision!=0||cell.Elevation!=3||cell.SourceBehavior is FieldHmSourceRules.FallWarpBehavior or FieldHmSourceRules.NonAnimatedDoorBehavior)
            return "save_field_strength_boulder_tile_invalid";
        bool SameElevation(int elevation)=>elevation==0||elevation==boulder.Elevation;
        if(state.World.Player.X==boulder.X&&state.World.Player.Y==boulder.Y&&SameElevation(state.World.Player.Elevation)||
            state.Actors.Values.Any(a=>a.LocalId!=boulder.LocalId&&a.Active&&a.X==boulder.X&&a.Y==boulder.Y&&SameElevation(a.Elevation)))
            return "save_field_strength_boulder_overlap";
        return null;
    }
}
