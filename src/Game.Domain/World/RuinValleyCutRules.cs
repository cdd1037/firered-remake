using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.World;

public sealed record RuinValleyDoorSnapshot(WorldSnapshot World, bool Opened, int CutUses, ImmutableArray<PartyPokemon> Party);
public sealed record RuinValleyDoorMutation(int X,int Y,int MetatileId,int SourceBehavior,int Collision,int Elevation);
/// <summary>Selected owner and complete awaiting snapshot are retained through show-mon. This transient capability is not save data.</summary>
public sealed class RuinValleyCutState
{
    public RuinValleyDoorSnapshot Awaiting {get;}
    public CommandId PendingPresentationId {get;}
    public int PartyIndex {get;}
    public long OwnerInstanceId => Awaiting.Party[PartyIndex].InstanceId;
    public bool ControlsLocked => true;
    public bool PreventStep => true;
    public bool CanSave => false;
    internal RuinValleyCutState(RuinValleyDoorSnapshot snapshot,CommandId command,int partyIndex)
    {Awaiting=snapshot;PendingPresentationId=command;PartyIndex=partyIndex;}
}
public sealed record RuinValleyCutTransition(TransitionStatus Status,RuinValleyDoorSnapshot State,RuinValleyCutState? Active,
    RuinValleyDoorMutation? Mutation,ImmutableArray<string> SourceEffects,Diagnostic? Diagnostic)
{
    public bool CanSave => Active is null;
    public bool ControlsLocked => Active is not null;
}

/// <summary>Party-menu Cut special case, before tree/grass checks. No HP, PP, held-HM or friendship gate; never consume PP.</summary>
public static class RuinValleyCutRules
{
    public const string PermanentFlag="FLAG_USED_CUT_ON_RUIN_VALLEY_BRAILLE";
    public const int ClosedMetatile=0x357,OpenMetatile=0x358;
    public static bool SourcePositionMatches(TilePosition player,bool opened) => !opened &&
        player.MapId==DottedHoleSource.RuinValley && player.X==24 && player.Y==25 && player.Facing==Direction.North;
    public static bool EligibleOwner(PartyPokemon member,bool hasCascadeBadge) => hasCascadeBadge && member is not null &&
        member.InstanceId>0 && !member.IsEgg && member.Pokemon is not null && !member.Pokemon.Moves.IsDefault &&
        member.Pokemon.Moves.Any(m=>m.Move.Value=="pret:MOVE_CUT");
    public static RuinValleyDoorMutation OpenDoor => new(24,24,OpenMetatile,0x60,0,0);

    /// <summary>Cancel is the party-menu exit before invoking this method. There is no extra source Yes/No or used-Cut textbox.</summary>
    public static RuinValleyCutTransition Begin(RuinValleyDoorSnapshot state,FourIslandFieldIntent intent,IFieldMap map,
        int partyIndex,bool hasCascadeBadge,RuinValleyCutState? active=null)
    {
        if(active is not null) return Fail(state,active,"ruin-valley-cut-controls-locked");
        var error=DottedHoleSource.ValidateWorld(state.World,intent,map);
        if(error is not null) return Fail(state,null,error.Code);
        if(intent.CompletedPresentationId is not null || !SourcePositionMatches(state.World.Player,state.Opened))
            return Fail(state,null,"ruin-valley-cut-not-at-closed-door");
        if(!ValidClosedDoor(map) || state.World.Player.Elevation!=3 || state.CutUses is <0 or >0xffffff)
            return Fail(state,null,"ruin-valley-door-source-mismatch");
        if(state.Party.IsDefault || state.Party.Length is <1 or >6 || partyIndex<0 || partyIndex>=state.Party.Length ||
            !EligibleOwner(state.Party[partyIndex],hasCascadeBadge) ||
            state.Party.Any(p=>p is null) || state.Party.Select(p=>p.InstanceId).Distinct().Count()!=state.Party.Length)
            return Fail(state,null,"ruin-valley-cut-owner-or-badge-missing");
        var next=state with {World=state.World with {Revision=state.World.Revision+1},CutUses=Math.Min(0xffffff,state.CutUses+1)};
        return new(TransitionStatus.Accepted,next,new(next,intent.CommandId,partyIndex),null,
            ["FLDEFF_USE_CUT_ON_GRASS","GAME_STAT_USED_CUT","lock-controls-prevent-step","field_move_show_mon"],null);
    }

    public static RuinValleyCutTransition CompletePresentation(RuinValleyDoorSnapshot state,RuinValleyCutState active,
        FourIslandFieldIntent intent,IFieldMap map)
    {
        if(state!=active.Awaiting || intent.ExpectedRevision!=state.World.Revision || !StableId.IsValid(intent.CommandId.Value) ||
            intent.CommandId==active.PendingPresentationId || intent.CompletedPresentationId!=active.PendingPresentationId)
            return Fail(state,active,"ruin-valley-cut-presentation-mismatch");
        if(!ValidClosedDoor(map)) return Fail(state,active,"ruin-valley-door-source-mismatch");
        var next=state with {World=state.World with {Revision=state.World.Revision+1},Opened=true};
        return new(TransitionStatus.Accepted,next,null,OpenDoor,
            ["remove-FLDEFF_USE_CUT_ON_GRASS","MapGridSetMetatileIdAt","DrawWholeMapView","SE_BANG",PermanentFlag,
                "unlock-controls","clear-prevent-step"],null);
    }

    /// <summary>On-load reapplies the permanent mutation without move use, PP spending, sound or Cut-stat increments.</summary>
    public static RuinValleyDoorMutation? OnLoad(bool opened) => opened?OpenDoor:null;
    public static RuinValleyCutTransition Cancel(RuinValleyDoorSnapshot state,RuinValleyCutState active) => Fail(state,active,"ruin-valley-cut-not-cancellable");
    private static bool ValidClosedDoor(IFieldMap map) => map.Id==DottedHoleSource.RuinValley && map.Width==48 && map.Height==40 &&
        map.GetTile(24,24) is {Blocked:true,Elevation:0} && map.SourceBehaviorAt(24,24)==0 &&
        map.GetTile(24,25) is {Blocked:false,Elevation:3} && map.SourceBehaviorAt(24,25)==0 &&
        !map.FieldWarps.IsDefault && map.FieldWarps.Length==1 &&
        map.FieldWarps[0]==new FieldWarp(0,24,24,3,DottedHoleSource.FirstFloor,0);
    private static RuinValleyCutTransition Fail(RuinValleyDoorSnapshot state,RuinValleyCutState? active,string code) =>
        new(TransitionStatus.Rejected,state,active,null,[],new(code,code));
}
