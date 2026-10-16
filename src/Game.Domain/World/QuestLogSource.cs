using System.Collections.Immutable;

namespace Game.Domain.World;

public sealed record QuestLogDeparture(string Inside,string Outside,string TextKey,string Place,string Kind);

public interface ICampaignQuestLogCatalog
{
    QuestLogSource QuestLogSource { get; }
}
public sealed record QuestLogSource(ImmutableDictionary<string,string> Texts,
    ImmutableDictionary<string,string> MapNames,ImmutableDictionary<int,int> EventSizes,
    ImmutableArray<string> ArrivalFlags,ImmutableArray<string> DisabledMaps,ImmutableDictionary<int,string> Graphics,ImmutableArray<QuestLogDeparture> Departures,ImmutableArray<int> ExcludedWildSpecies,ImmutableArray<string> StoryItems,string TownMapGiftMap,ImmutableArray<int> WindowPixels,ImmutableArray<ImmutableArray<int>> WindowPalette)
{
    public const int SceneCount=4,ScriptBytes=256,SceneEndBytes=2,EventCursorCount=32;
    public string MapName(string map)=>MapNames.TryGetValue(map,out var name)?name:throw new InvalidDataException("quest_log_map_name_missing: "+map);
    public string Expand(string key,string player,IReadOnlyList<string> values)
    {
        var text=Texts[key].Replace("{PLAYER}",player,StringComparison.Ordinal);
        for(int i=0;i<values.Count;i++)
            text=text.Replace("{STR_VAR_"+(i+1)+"}",values[i],StringComparison.Ordinal)
                .Replace("{DYNAMIC 0x"+i.ToString("X2")+"}",values[i],StringComparison.Ordinal);
        if(text.Contains('{'))throw new InvalidDataException("quest_log_text_arguments_missing: "+key);
        return text;
    }
}
