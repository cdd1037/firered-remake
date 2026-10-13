using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    public bool CanUseFieldWaterfall(int partyIndex) => FieldWaterfallError(State,partyIndex) is null;
    private string? FieldWaterfallError(CampaignState state,int index)
    {
        if(!_content.FourIslandEnabled)return "field_waterfall_not_enabled";
        if(!IsStableFieldForParty(state)||state.FieldEffectArguments is not null||state.Fishing is not null||state.Repel is not null)return "field_waterfall_event_active";
        if(index<0||index>=state.Party.Length)return "field_waterfall_party_index_invalid";
        var p=state.World.Player;var(dx,dy)=Delta(p.Facing);
        if(p.MapId!=FourIslandFieldSource.Entrance||p.Y!=21||p.X is <15 or >19||
            !WaterfallSourceRules.CanUse(state.Party[index],state.Script.Flags.GetValueOrDefault(WaterfallSourceRules.Badge),state.IsSurfing,
                state.FourIslandMovementDirection??p.Facing,_content.Maps[p.MapId].SourceBehaviorAt(p.X+dx,p.Y+dy)??-1))return "field_waterfall_cannot_use";
        return null;
    }
    private Decision<CampaignState> StartFieldWaterfall(CampaignState state,CampaignCommand command)
    {
        if(FieldWaterfallError(state,command.PartyIndex) is {} error)return new(CommandStatus.Rejected,state,error);
        return BeginFieldWaterfall(state,command.PartyIndex);
    }
    private Decision<CampaignState> BeginFieldWaterfall(CampaignState state,int index)
    {
        if(index<0||index>=state.Party.Length)throw new InvalidDataException("waterfall_owner_index");
        var result=FourIslandForcedMovementRules.StartWaterfall(state.World,new(new("four:waterfall:"+state.World.Revision),state.World.Revision),
            _content.Maps[state.World.Player.MapId],state.Party[index],state.Script.Flags.GetValueOrDefault(WaterfallSourceRules.Badge),state.IsSurfing,
            state.FourIslandMovementDirection??state.World.Player.Facing,true,occupied:(x,y,e)=>ActorOccupies(state,x,y,e));
        return AcceptFourIslandMovement(state with{Dialogue=null,SignMessage=null,MessagePointer=null},result);
    }
    private Decision<CampaignState>? TryStartFieldWaterfallInteraction(CampaignState state)
    {
        if(!_content.FourIslandEnabled)return null;
        var p=state.World.Player;var(dx,dy)=Delta(p.Facing);
        if(_content.Maps[p.MapId].SourceBehaviorAt(p.X+dx,p.Y+dy)!=FourIslandFieldSource.Waterfall)return null;
        bool allowed=state.IsSurfing&&state.Script.Flags.GetValueOrDefault(WaterfallSourceRules.Badge)&&
            (state.FourIslandMovementDirection??p.Facing)==Direction.North;
        return new(CommandStatus.Accepted,Pump(StartStory(state,allowed?"EventScript_Waterfall":"EventScript_CantUseWaterfall")));
    }
    private bool TryApplyWaterfallEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(effect.Source.Path!="data/scripts/field_moves.inc"||effect.Source.Symbol is not
            ("EventScript_Waterfall" or "EventScript_CantUseWaterfall" or "EventScript_WaterCrashingDown" or "EventScript_EndWaterfall"))return false;
        if(state.World.Player.MapId!=FourIslandFieldSource.Entrance)throw new InvalidDataException("waterfall_script_map");
        var a=effect.Args;
        int Value(string token)=>SeviiValue(state,token);
        switch(effect.Opcode)
        {
            case "checkpartymove":
                if(a.Length!=1||a[0]!="MOVE_WATERFALL")throw new InvalidDataException("waterfall_check_move");
                int index=WaterfallSourceRules.FirstPartyMember(state.Party);var vars=state.Script.Variables.SetItem("VAR_RESULT",index);
                if(index!=6)vars=vars.SetItem("VAR_0x8004",state.Party[index].Pokemon.SourceSpeciesId);
                next=state with{Script=state.Script with{Variables=vars}};return true;
            case "bufferpartymonnick":
                if(a.Length!=2||a[0]!="STR_VAR_1"||Value(a[1]) is var n&&(n<0||n>=state.Party.Length))throw new InvalidDataException("waterfall_nickname");
                next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],state.Party[Value(a[1])].Nickname)};return true;
            case "setfieldeffectargument":
                if(a.Length!=2||Value(a[0])!=0||Value(a[1]) is var selected&&(selected<0||selected>=state.Party.Length||!WaterfallSourceRules.Knows(state.Party[selected])))throw new InvalidDataException("waterfall_argument");
                next=state with{FieldEffectArguments=ImmutableSortedDictionary<int,int>.Empty.Add(0,Value(a[1]))};return true;
            case "dofieldeffect":
                if(a.Length!=1||a[0]!="FLDEFF_USE_WATERFALL"||state.FieldEffectArguments is not {} args||!args.TryGetValue(0,out int owner))throw new InvalidDataException("waterfall_effect_argument");
                var start=BeginFieldWaterfall(state,owner);
                if(start.Status!=CommandStatus.Accepted)throw new InvalidDataException(start.Error);
                next=start.State;return true;
            case "releaseall":
                next=state with{FieldEffectArguments=state.FourIslandMovement is null?null:state.FieldEffectArguments,
                    Script=state.Script with{Locked=state.FourIslandMovement is not null},Dialogue=null,SignMessage=null,MessagePointer=null};return true;
            default:return false;
        }
    }
}
