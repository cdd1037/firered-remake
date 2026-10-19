using System.Collections.Immutable;
using System.Text;

namespace Game.Domain.World;

public sealed record QuestLogDeparture(string Inside,string Outside,string TextKey,string Place,string Kind)
{
    public int LocationId{get;init;}
    public string LocationSymbol{get;init;}="";
    public string RegionName{get;init;}="";
    public string? BadgeFlag{get;init;}
}

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
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal) { ["PLAYER"] = player };
        for (int i = 0; i < values.Count; i++)
        {
            arguments["STR_VAR_" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)] = values[i];
            arguments["DYNAMIC 0x" + i.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)] = values[i];
        }
        var text = Texts[key];
        var result = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '{') { result.Append(text[i]); continue; }
            int end = text.IndexOf('}', i + 1);
            if (end < 0 || !arguments.TryGetValue(text[(i + 1)..end], out var value))
                throw new InvalidDataException("quest_log_text_arguments_missing: " + key);
            result.Append(value); i = end;
        }
        return result.ToString();
    }
}
