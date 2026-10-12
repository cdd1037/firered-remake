using Game.Domain.Encounters;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private bool IsVictoryRoadWildHistory(CampaignWildHistory history)
    {
        if(!_content.VictoryRoadEnabled||history.VictoryRoad is not {} origin||history.Safari||history.ScriptedMarowak||
            history.ScriptedSnorlaxRoute!=0||history.LostelleHypno is not null||origin.Kind is VictoryRoadEncounterKind.GoodRod or VictoryRoadEncounterKind.SuperRod||
            VictoryRoadWildBattleContract.SourceSlot(origin) is not {} slot)return false;
        return _content.CinnabarOwnership!.Catalog.Species[slot.Species].SourceSpeciesId==history.SourceSpeciesId&&
            history.Level>=slot.MinimumLevel&&history.Level<=slot.MaximumLevel;
    }
}
