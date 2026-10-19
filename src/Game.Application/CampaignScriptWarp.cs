using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // One transient view of the actual map-load boundary. Application movement
    // remains precommitted; presenting this snapshot never resumes a script.
    private sealed record FieldWarpEntryPresentation(CampaignState Origin,FieldTransitionReceipt Receipt,CampaignState Arrival);
    private FieldWarpEntryPresentation? _fieldWarpEntryPresentation;
    private FieldTransitionReceipt? _fieldWarpEntryReceipt;
    private CampaignState? _fieldWarpEntryArrival;

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
        // ScrCmd_warp -> CB2_LoadMap replaces the old context. As before, script
        // warps do not increment walking steps or World.Revision.
        return EnterMap(ResetMapScriptContext(state) with
        {
            World=state.World with{Player=landing},FieldTransition=receipt
        },facing);
    }

    private CampaignState CaptureFieldWarpArrival(CampaignState state)
    {
        // Throwaway script preflight must never replace an accepted entry view.
        if(!_preflightingStory&&_fieldWarpEntryArrival is null&&_fieldWarpEntryReceipt is {} receipt&&
            ReferenceEquals(state.FieldTransition,receipt)&&
            state.World.Player with{Facing=receipt.To.Facing}==receipt.To)
        {
            // OnWarp can turn the initialized player before the first OnFrame.
            // Keep that committed pose in the producer-owned arrival receipt.
            if(state.World.Player!=receipt.To)
            {
                receipt=receipt with{To=state.World.Player};
                _fieldWarpEntryReceipt=receipt;state=state with{FieldTransition=receipt};
            }
            _fieldWarpEntryArrival=state;
        }
        return state;
    }

    /// <summary>Actual arrival before destination OnFrame scripts, or the ordinary stable destination.</summary>
    public bool TryGetFieldTransitionArrival(CampaignState origin,FieldTransitionReceipt receipt,out CampaignState arrival)
    {
        arrival=State;
        if(!ReferenceEquals(State.FieldTransition,receipt))return false;
        if(_fieldWarpEntryPresentation is {} entry&&ReferenceEquals(entry.Receipt,receipt))
        {
            // A structurally identical caller-supplied origin is not the accepted
            // command that completed the source script/movement boundary.
            // Scripted entrance movement can hide the player before warp. That
            // visibility remains in the real origin snapshot; it is not a modal
            // restriction on this authenticated, already-committed departure.
            var departure=ResetMapScriptContext(origin) with{PlayerVisible=true};
            if(!ReferenceEquals(entry.Origin,origin)||!SupportsFieldTransitionOrigin(departure,receipt)||
                !IsStableFieldForParty(departure)||
                !SupportsFieldTransition(entry.Arrival,receipt))return false;
            arrival=entry.Arrival;return true;
        }
        return SupportsFieldTransitionOrigin(origin,receipt)&&SupportsFieldTransition(State,receipt);
    }
}
