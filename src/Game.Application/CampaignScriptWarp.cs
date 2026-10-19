using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // One transient view of the actual map-load boundary. Application movement
    // remains precommitted; presenting this snapshot never resumes a script.
    private sealed record ScriptWarpPresentation(CampaignState Origin,FieldTransitionReceipt Receipt,CampaignState Arrival);
    private ScriptWarpPresentation? _scriptWarpPresentation;
    private FieldTransitionReceipt? _scriptWarpReceipt;
    private CampaignState? _scriptWarpArrival;

    private static CampaignState ResetMapScriptContext(CampaignState state)=>state with
    {
        Script=state.Script with{Stack=[],Wait=null,Completed=true,Locked=false},
        Dialogue=null,Motions=[],PresentationEffects=[],PresentationTicks=0
    };

    private CampaignState EnterScriptWarp(CampaignState state,Game.Domain.Ids.MapId destination,int x,int y)
    {
        var map=_content.Maps[destination];
        var anchorCell=map.GetTile(x,y)??throw new InvalidDataException("Script warp out of map");
        var behavior=map.SourceBehaviorAt(x,y);
        var facing=behavior switch
        {
            0x62=>Direction.West,0x63=>Direction.East,0x64=>Direction.South,0x65=>Direction.North,
            0x6c or 0x6e=>Direction.West,0x6d or 0x6f=>Direction.East,_=>Direction.South
        };
        var anchor=new TilePosition(destination,x,y,anchorCell.Elevation,facing);
        if(behavior==0x69)y++;
        var cell=map.GetTile(x,y)??throw new InvalidDataException("Script door exit missing.");
        var landing=new TilePosition(destination,x,y,cell.Elevation,facing);
        var receipt=new FieldTransitionReceipt(FieldTransitionCause.PlainWarp,FieldTransitionDeparture.Immediate,
            FieldTransitionReceipt.ArrivalAt(behavior),state.World.Player,landing,anchor)
        {SuppressMusicFade=state.Script.Flags.GetValueOrDefault("FLAG_DONT_TRANSITION_MUSIC")};
        _scriptWarpReceipt=receipt;_scriptWarpArrival=null;
        // ScrCmd_warp -> CB2_LoadMap replaces the old context. As before, script
        // warps do not increment walking steps or World.Revision.
        return EnterMap(ResetMapScriptContext(state) with
        {
            World=state.World with{Player=landing},FieldTransition=receipt
        },facing);
    }

    private CampaignState CaptureScriptWarpArrival(CampaignState state)
    {
        if(_scriptWarpArrival is null&&_scriptWarpReceipt is {} receipt&&
            ReferenceEquals(state.FieldTransition,receipt)&&
            state.World.Player with{Facing=receipt.To.Facing}==receipt.To)
        {
            // OnWarp can turn the initialized player before the first OnFrame.
            // Keep that committed pose in the producer-owned arrival receipt.
            if(state.World.Player!=receipt.To)
            {
                receipt=receipt with{To=state.World.Player};
                _scriptWarpReceipt=receipt;state=state with{FieldTransition=receipt};
            }
            _scriptWarpArrival=state;
        }
        return state;
    }

    /// <summary>Actual arrival before destination OnFrame scripts, or the ordinary stable destination.</summary>
    public bool TryGetFieldTransitionArrival(CampaignState origin,FieldTransitionReceipt receipt,out CampaignState arrival)
    {
        arrival=State;
        if(!ReferenceEquals(State.FieldTransition,receipt))return false;
        if(_scriptWarpPresentation is {} script&&ReferenceEquals(script.Receipt,receipt))
        {
            // A structurally identical caller-supplied origin is not the accepted
            // command that completed the source script/movement boundary.
            // Scripted entrance movement can hide the player before warp. That
            // visibility remains in the real origin snapshot; it is not a modal
            // restriction on this authenticated, already-committed departure.
            if(!ReferenceEquals(script.Origin,origin)||receipt.From!=origin.World.Player||
                origin.FieldWeather is not null||origin.IsCycling||origin.IsSurfing||
                !IsStableFieldForParty(ResetMapScriptContext(origin) with{PlayerVisible=true})||
                !SupportsFieldTransition(script.Arrival,receipt))return false;
            arrival=script.Arrival;return true;
        }
        return SupportsFieldTransitionOrigin(origin,receipt)&&SupportsFieldTransition(State,receipt);
    }
}
