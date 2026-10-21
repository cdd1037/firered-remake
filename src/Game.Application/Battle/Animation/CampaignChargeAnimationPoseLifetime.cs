using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>Per-slot lifetime of authenticated semi-invulnerable setup visibility.
/// Source controllers restore failed releases during move-end, for which this event
/// protocol has no separate visual receipt. That hold ends conservatively after its
/// genuine result batch is fully presented. No final combat state is read.</summary>
public sealed class CampaignChargeAnimationPoseLifetime
{
    private sealed record Owner(BattlerSlot Actor,MoveId Move,long SetupSequence,long? CompletedBatchSequence=null);
    private readonly Dictionary<int,Owner> _owners=[];
    public void DropSlot(int slot)=>_owners.Remove(slot);
    public void Clear()=>_owners.Clear();
    public bool Capture(CampaignMoveAnimationDispatch dispatch,IReadOnlyList<SourceBattleEvent> events)
    {
        if(dispatch.Boundary!=CampaignMoveAnimationBoundary.BeforeChargeResult||dispatch.MoveTurn!=0||dispatch.PpSequence is not {} ppSequence)return false;
        var pp=events.OfType<SourcePpCommandEvent>().Where(p=>p.Sequence==ppSequence).ToArray();
        var setup=events.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Sequence==dispatch.Sequence).ToArray();
        if(pp.Length!=1||setup.Length!=1||pp[0].Actor!=dispatch.Actor||pp[0].PpOwner!=dispatch.Actor||pp[0].CurrentMove!=pp[0].ChosenMove||
            SourceChargeRules.SemiInvulnerable(pp[0].CurrentMove)==WildBattleSemiInvulnerable.None||setup[0] is not {Volatile:"called-charge",Before:0,After:1,Reason:"move"}||
            setup[0].Side!=dispatch.Actor||setup[0].Turn!=pp[0].Turn||ppSequence>=dispatch.Sequence)return false;
        _owners[dispatch.Actor.Value]=new(dispatch.Actor,pp[0].CurrentMove,dispatch.Sequence);return true;
    }
    /// <summary>Only a completed authenticated release may relinquish its own hold
    /// before the batch ends; the actual source callback has restored its sprite.</summary>
    public bool CompleteRelease(CampaignMoveAnimationDispatch dispatch,IReadOnlyList<SourceBattleEvent> events)
    {
        if(dispatch.MoveTurn!=1||dispatch.PpSequence is not {} ppSequence||!_owners.TryGetValue(dispatch.Actor.Value,out var owner))return false;
        var pp=events.OfType<SourcePpCommandEvent>().Where(p=>p.Sequence==ppSequence).ToArray();
        var markers=events.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Sequence>owner.SetupSequence&&e.Sequence<ppSequence&&e.Side==owner.Actor&&e.Volatile=="called-charge").ToArray();
        if(pp.Length!=1||pp[0].Actor!=owner.Actor||pp[0].PpOwner!=owner.Actor||pp[0].CurrentMove!=owner.Move||pp[0].ChosenMove!=owner.Move||pp[0].Before!=pp[0].After||
            markers.Length!=1||markers[0] is not {Before:1,After:0,Reason:"released"}||markers[0].Turn!=pp[0].Turn)return false;
        DropSlot(owner.Actor.Value);return true;
    }
    /// <param name="currentReceiptSequence">Current result page, including the
    /// final page while displayed. Null means this batch has no unacknowledged page.</param>
    /// <returns>One released slot, or null. Repeated calls drain distinct owners only.</returns>
    public int? Observe(IReadOnlyList<SourceBattleEvent> events,long? currentReceiptSequence)
    {
        for(int i=1;i<events.Count;i++)if(events[i].Sequence<=events[i-1].Sequence)return null;
        foreach(var owner in _owners.Values.ToArray())
        {
            // A new higher-sequence batch is also proof the pending batch was consumed.
            if(owner.CompletedBatchSequence is {} prior&&(currentReceiptSequence is null||currentReceiptSequence>prior||events.Count>0&&events[0].Sequence>prior))
            {DropSlot(owner.Actor.Value);return owner.Actor.Value;}
            if(events.Count==0)continue;
            var markers=events.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Sequence>owner.SetupSequence&&e.Side==owner.Actor&&e.Volatile=="called-charge").ToArray();
            if(markers.Length!=1||markers[0] is not {Before:1,After:0})continue;
            var clear=markers[0];
            // Status/canceller clears genuinely have no PP command.
            if(events.OfType<SourcePpCommandEvent>().Any(p=>p.Actor==owner.Actor&&p.Sequence>clear.Sequence&&
                (p.PpOwner!=owner.Actor||p.CurrentMove!=owner.Move||p.ChosenMove!=owner.Move)))continue;
            long boundary=events[^1].Sequence;_owners[owner.Actor.Value]=owner with{CompletedBatchSequence=boundary};
            if(currentReceiptSequence is not null&&currentReceiptSequence<=boundary)continue;
            DropSlot(owner.Actor.Value);return owner.Actor.Value;
        }
        return null;
    }
}
