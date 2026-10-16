using System.Collections.Immutable;
using Game.Domain.World;
namespace Game.Application;

public sealed partial class CampaignSession
{
    // Original local object pool: InitPlayerAvatar occupies slot 0, followed by the
    // first available slots for map templates. SelectedObjectEvent is a SLOT, not a local ID.
    private static ImmutableSortedDictionary<int,string> EmptyObjectPool=>ImmutableSortedDictionary<int,string>.Empty.Add(0,"LOCALID_PLAYER");
    public static CampaignState InitializeObjectSlotsForProfileUpgrade(CampaignState verifiedState)
    {
        if(verifiedState.ObjectSlots is not null)throw new InvalidDataException("object_pool_already_initialized");
        var slots=EmptyObjectPool;int slot=1;
        foreach(var actor in verifiedState.Actors.Values.Where(a=>a.Active).OrderBy(a=>a.SourceLocalId))
        {if(slot>=16)throw new InvalidDataException("object_pool_capacity");slots=slots.Add(slot++,actor.LocalId);}
        return verifiedState with{ObjectSlots=slots,SelectedObjectSlot=0};
    }
    private CampaignState RefreshSlottedActors(CampaignState state)
    {
        state=CaptureVsSeekerObjectMovements(state);
        var player=state.World.Player;
        bool InView(int x,int y)=>x+7>=player.X-2&&x+7<=player.X+17&&y+7>=player.Y&&y+7<=player.Y+16;
        var actors=state.Actors;var slots=state.ObjectSlots!;
        // UpdateObjectEventsForCameraUpdate spawns before removing out-of-view actors.
        foreach(var actor in actors.Values.OrderBy(a=>a.SourceLocalId))
        {
            if(actor.Active||!InView(actor.TemplateX,actor.TemplateY)||(actor.HideFlag is not ("0" or "0x0")&&state.Script.Flags.GetValueOrDefault(actor.HideFlag)))continue;
            int free=Enumerable.Range(1,15).FirstOrDefault(i=>!slots.ContainsKey(i));if(free==0)continue;
            var movement=state.VsSeekerProgress?.TemplateMovements.TryGetValue(actor.SourceLocalId,out int sourceMovement)==true?VsMovementSymbol(sourceMovement):actor.MovementType;
            slots=slots.Add(free,actor.LocalId);actors=actors.SetItem(actor.LocalId,actor with{Active=true,Invisible=false,X=actor.TemplateX,Y=actor.TemplateY,MovementType=movement,Facing=DirectionFromType(movement)});
        }
        foreach(var actor in actors.Values.Where(a=>a.Active).OrderBy(a=>a.SourceLocalId))
        {
            if(InView(actor.X,actor.Y)||InView(actor.TemplateX,actor.TemplateY))continue;
            actors=actors.SetItem(actor.LocalId,actor with{Active=false});var occupied=slots.SingleOrDefault(x=>x.Value==actor.LocalId);if(occupied.Key!=0)slots=slots.Remove(occupied.Key);
        }
        return CaptureVsSeekerObjectMovements(state with{Actors=actors,ObjectSlots=slots});
    }
    private static CampaignState SelectInteractedObject(CampaignState state,string actor)
    {
        if(state.ObjectSlots is null)return state;
        var slot=state.ObjectSlots.SingleOrDefault(x=>x.Value==actor);
        if(slot.Key==0)throw new InvalidDataException("interacted_object_slot_missing");
        return state with{SelectedObjectSlot=slot.Key};
    }
    private static CampaignState FaceSelectedObject(CampaignState state)
    {
        if(state.ObjectSlots is null)return state.LastTalked is {} local?Face(state,local,Opposite(state.World.Player.Facing)):throw new InvalidDataException("faceplayer_without_actor");
        // A stale selected slot may be inactive or reused, exactly as the source global can.
        return state.ObjectSlots.TryGetValue(state.SelectedObjectSlot,out var actor)
            ?Face(state,actor,Opposite(state.World.Player.Facing)):state;
    }
    private string? ValidateObjectSlots(CampaignState state)
    {
        if(state.ObjectSlots is not {} slots||slots.Count is <1 or >16||slots.GetValueOrDefault(0)!="LOCALID_PLAYER"||slots.Keys.Any(x=>x is <0 or >15)||state.SelectedObjectSlot is <0 or >15)return "save_object_pool_invalid";
        if(slots.Values.Distinct(StringComparer.Ordinal).Count()!=slots.Count||slots.Where(x=>x.Key!=0).Any(x=>!state.Actors.TryGetValue(x.Value,out var actor)||!actor.Active)||state.Actors.Values.Any(a=>a.Active&&!slots.Values.Contains(a.LocalId)))return "save_object_pool_actor_mismatch";
        return null;
    }
}
