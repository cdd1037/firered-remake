using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Application;

/// <summary>Ephemeral source picture selection. Species owns geometry; RenderSpecies owns only bitmap lookup.</summary>
public sealed record CampaignAdditionalMonRequest(int Species,int RenderSpecies,uint Personality,uint OriginalTrainerId,bool Back,bool Shiny);

public static class CampaignAdditionalMonPresentation
{
    public static CampaignAdditionalMonRequest? RolePlay(CampaignMoveAnimationDispatch dispatch,
        CampaignBattleDisplayBodies displayed,SourceBattleFrame committed,IReadOnlyList<SourceBattleEvent> events)
    {
        int count=displayed.Bodies.Length,actor=dispatch.Actor.Value,target=dispatch.Target.Value;
        if(count is not(2 or 4)||displayed.Members.Length!=count||committed.BattlerCount!=count||actor>=count||target>=count||
            displayed.Members.Where((m,i)=>m.Side!=new BattlerSlot(i).Side).Any()||committed.Parties.Length!=2)return null;
        var anchors=events.Where(e=>e.Sequence==dispatch.SnapshotSequence).ToArray();
        if(anchors.Length!=1||anchors[0] is not SourcePpCommandEvent pp||pp.Actor!=dispatch.Actor||pp.PpOwner!=dispatch.Actor||pp.Target!=dispatch.Target||
            pp.CurrentMove.Value!="pret:MOVE_ROLE_PLAY"||pp.ChosenMove!=pp.CurrentMove||dispatch.PpSequence!=pp.Sequence||
            dispatch.Boundary!=CampaignMoveAnimationBoundary.AfterAttackText)return null;
        if(events.Zip(events.Skip(1),(a,b)=>a.Sequence<b.Sequence).Any(increasing=>!increasing))return null;
        var results=events.Where(e=>e.Sequence==dispatch.ResultSequence).ToArray();
        if(results.Length!=1||results[0] is not SourceCalledOverlayChangedEvent copied||copied.Side!=dispatch.Actor||copied.Source is not null||
            copied.Move!=pp.CurrentMove||copied.Sequence!=pp.Sequence+1||copied.Before!=displayed.Bodies[actor].CalledOverlay||
            copied.After?.Ability!=WildBattleEffective.Ability(displayed.Bodies[target]))return null;
        var identities=committed.Parties.Select(p=>p.Members.Select(m=>m.Pokemon).ToArray()).ToArray();
        if(committed.Parties.Where((p,i)=>(int)p.Side!=i).Any())return null;
        static bool Same(PokemonInstance a,PokemonInstance b)=>a.Species==b.Species&&a.SourceSpeciesId==b.SourceSpeciesId&&
            a.Personality==b.Personality&&a.OriginalTrainerId==b.OriginalTrainerId;
        // Ordinary battle batches synchronize HP/PP/status/items but never
        // change persistent species/PID/OT. Prove that boundary; an unreviewed
        // identity-changing receipt is rejected rather than reconstructed here.
        foreach(var change in events.OfType<SourceCalledPartyChangedEvent>())
        {
            int side=(int)change.Side,index=change.PartyIndex;
            if(side is <0 or >1||index<0||index>=identities[side].Length||!Same(change.Before.Pokemon,change.After.Pokemon)||
                !Same(identities[side][index],change.After.Pokemon))return null;
        }
        for(int i=0;i<count;i++)
        {
            var member=displayed.Members[i];
            if(member.Index>=identities[(int)member.Side].Length||!Same(WildBattleTransformationRules.PersistentPokemon(displayed.Bodies[i]),identities[(int)member.Side][member.Index]))return null;
        }
        var selected=displayed.Members[target];int oppositeActorSide=actor%2==0?1:0;
        if(selected.Index>=identities[oppositeActorSide].Length)return null;
        var identity=identities[oppositeActorSide][selected.Index];
        int species=displayed.Bodies[target].Transform?.CopiedPokemon.SourceSpeciesId??WildBattleTransformationRules.PersistentPokemon(displayed.Bodies[target]).SourceSpeciesId;
        if(species<=0)return null;
        uint pid=identity.Personality,ot=identity.OriginalTrainerId;
        bool shiny=((ot>>16)^(ot&65535)^(pid>>16)^(pid&65535))<8;
        return new(species,UnownFormRules.ResolveRenderSpeciesId(species,pid),pid,ot,actor%2==0,shiny);
    }
}
