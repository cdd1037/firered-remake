using System.Collections.Immutable;
using Game.Domain.Battle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // Compose authoritative ownership and badge facts only for an admitted canonical entry.
    // Acquisition metadata, including ModernFatefulEncounter, is not an obedience input.
    private static SourceObedienceContext CampaignObedienceContext(CampaignState state)
    {
        byte badges=0;
        for(int badge=1;badge<=8;badge++)
            if(state.Script.Flags.GetValueOrDefault("FLAG_BADGE0"+badge+"_GET"))badges|=(byte)(1<<(badge-1));
        return new(state.TrainerId,state.PlayerName,badges,state.Party.Select((p,index)=>
            new SourceObedienceMember(new(BattleSide.Player,index),p.OriginalTrainerName)).ToImmutableArray());
    }
}
