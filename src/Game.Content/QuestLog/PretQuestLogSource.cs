using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.World;

namespace Game.Content;
public static class PretQuestLogSource
{
    public static QuestLogSource Load(string root, SourceTextPack? pack = null)
    {
        var path=Path.Combine(root,"content/generated/data/quest-log.json");
        var source=JsonSerializer.Deserialize<QuestLogSource>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("quest_log_content_missing");
        if(source.WindowPixels.IsDefault||source.WindowPixels.Length!=192||source.WindowPixels.Any(i=>i<0||i>15)||source.WindowPalette.IsDefault||source.WindowPalette.Length!=16||source.WindowPalette.Any(c=>c.IsDefault||c.Length!=3||c.Any(v=>v<0||v>255))||source.Texts.Count==0||source.MapNames.Count==0||source.EventSizes.Count!=43||source.ArrivalFlags.IsDefaultOrEmpty||source.DisabledMaps.IsDefaultOrEmpty||source.EventSizes[2]!=8||source.EventSizes[39]!=2||source.EventSizes.Any(x=>x.Value<2||x.Value>32))throw new InvalidDataException("quest_log_content_invalid");
        if(source.Departures.IsDefault||source.Departures.Length!=51||!source.Departures.Select(d=>d.LocationId).Order().SequenceEqual(Enumerable.Range(0,51))||source.Departures.Any(d=>string.IsNullOrEmpty(d.LocationSymbol)||string.IsNullOrEmpty(d.RegionName)||d.Kind=="GYM"&&d.BadgeFlag is null))throw new InvalidDataException("quest_log_departures_invalid");
        var selected = pack ?? SourceTextPack.Current;
        var departures = selected.Apply("quest-log-departures", source.Departures.SelectMany(departure => new[] {
            KeyValuePair.Create(departure.LocationSymbol + "/place", departure.Place),
            KeyValuePair.Create(departure.LocationSymbol + "/region", departure.RegionName)
        }).ToDictionary(pair => pair.Key, pair => pair.Value), preserveControls: true);
        return source with {
            Texts = selected.Apply("quest-log", source.Texts, preserveControls: true),
            MapNames = selected.Apply("quest-log-map-names", source.MapNames, preserveControls: true),
            Departures = source.Departures.Select(departure => departure with {
                Place = departures[departure.LocationSymbol + "/place"],
                RegionName = departures[departure.LocationSymbol + "/region"] }).ToImmutableArray()
        };
    }
}
public sealed partial class PretCampaignCatalog:ICampaignQuestLogCatalog
{
    public QuestLogSource QuestLogSource {get;init;}=null!;
}
