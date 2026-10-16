using System.Collections.Immutable;
using System.Globalization;

namespace Game.Application;

// quest_log_events.c RecordEvent/LoadEvent_DefeatedWildMon: one existing event,
// last species in each category, and two saturating u8 counts. No new history.
public static class CampaignQuestLogWildCapture
{
    public static ImmutableArray<CampaignQuestLogAction> Merge(ImmutableArray<CampaignQuestLogAction> actions,CampaignQuestLogAction incoming)
    {
        if(incoming.Event!=CampaignQuestLogEvent.WildWon||!Counts(incoming,out int defeated,out int caught))return actions;
        int index=actions.Length-1;
        while(index>=0&&actions[index].Event==CampaignQuestLogEvent.None)index--;
        if(index<0||actions[index].Event!=CampaignQuestLogEvent.WildWon||!Counts(actions[index],out int priorDefeated,out int priorCaught))return actions;
        var prior=actions[index];
        var args=prior.Arguments.ToBuilder();
        args[0]=incoming.Arguments[0];
        if(defeated>0)args[1]=incoming.Arguments[1];
        if(caught>0)args[3]=incoming.Arguments[3];
        defeated=Math.Min(255,priorDefeated+defeated);caught=Math.Min(255,priorCaught+caught);
        args[2]=defeated.ToString(CultureInfo.InvariantCulture);args[4]=caught.ToString(CultureInfo.InvariantCulture);
        string defeatedKey=defeated==1?"DefeatedWildMon":"DefeatedWildMons";
        string caughtKey=caught==1?"CaughtWildMon":"CaughtWildMons";
        string key=defeated==0?caughtKey:caught==0?defeatedKey:defeatedKey+"And"+caughtKey;
        return actions.SetItem(index,prior with{TextKey=key,Arguments=args.ToImmutable()});
    }
    private static bool Counts(CampaignQuestLogAction action,out int defeated,out int caught)
    {
        defeated=0;caught=0;
        return !action.Arguments.IsDefault&&action.Arguments.Length==6&&
            int.TryParse(action.Arguments[2],NumberStyles.None,CultureInfo.InvariantCulture,out defeated)&&defeated is >=0 and <=255&&
            int.TryParse(action.Arguments[4],NumberStyles.None,CultureInfo.InvariantCulture,out caught)&&caught is >=0 and <=255&&(defeated>0||caught>0);
    }
}
