using System.Text.Json;
using Game.Domain.World;

namespace Game.Content;
public static class PretQuestLogSource
{
    public static QuestLogSource Load(string root)
    {
        var path=Path.Combine(root,"content/generated/data/quest-log.json");
        var source=JsonSerializer.Deserialize<QuestLogSource>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("quest_log_content_missing");
        if(source.WindowPixels.IsDefault||source.WindowPixels.Length!=192||source.WindowPixels.Any(i=>i<0||i>15)||source.WindowPalette.IsDefault||source.WindowPalette.Length!=16||source.WindowPalette.Any(c=>c.IsDefault||c.Length!=3||c.Any(v=>v<0||v>255))||source.Texts.Count==0||source.MapNames.Count==0||source.EventSizes.Count!=43||source.ArrivalFlags.IsDefaultOrEmpty||source.DisabledMaps.IsDefaultOrEmpty||source.EventSizes[2]!=8||source.EventSizes[39]!=2||source.EventSizes.Any(x=>x.Value<2||x.Value>32))throw new InvalidDataException("quest_log_content_invalid");
        if(source.Departures.IsDefault||source.Departures.Length!=51||!source.Departures.Select(d=>d.LocationId).Order().SequenceEqual(Enumerable.Range(0,51))||source.Departures.Any(d=>string.IsNullOrEmpty(d.LocationSymbol)||string.IsNullOrEmpty(d.RegionName)||d.Kind=="GYM"&&d.BadgeFlag is null))throw new InvalidDataException("quest_log_departures_invalid");
        return source;
    }
}
public sealed partial class PretCampaignCatalog:ICampaignQuestLogCatalog
{
    public QuestLogSource QuestLogSource {get;init;}=null!;
}
