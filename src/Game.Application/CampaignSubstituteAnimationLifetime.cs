using Game.Domain.Battle;

namespace Game.Application;

/// <summary>Transient source controller flag_x8. Revealing a doll belongs to its
/// current occupant and survives individual multi-hit result pages. No save state.</summary>
public sealed class CampaignSubstituteAnimationLifetime
{
    private readonly Dictionary<int,PartyMemberKey> _revealed=[];
    public bool IsRevealed(int slot,PartyMemberKey member)=>_revealed.TryGetValue(slot,out var owner)&&owner==member;
    public void Observe(int slot,PartyMemberKey member,bool behindSubstitute)
    {if(!behindSubstitute||!IsRevealed(slot,member))_revealed.Remove(slot);}
    public void Reveal(int slot,PartyMemberKey member)
    {
        if(new BattlerSlot(slot).Side!=member.Side)throw new ArgumentException("Substitute controller occupant differs.");
        _revealed[slot]=member;
    }
    public void Restore(int slot)=>_revealed.Remove(slot);
    public void Clear()=>_revealed.Clear();
}
