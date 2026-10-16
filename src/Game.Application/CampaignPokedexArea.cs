namespace Game.Application;

public sealed partial class CampaignSession
{
    /// <summary>Read-only location of the currently active roamer, from the shared world map catalog.</summary>
    public string? ActiveRoamerMapSection
    {
        get
        {
            if (State.Roamer is not { Individual.Active: true } roamer) return null;
            var maps = _content.CelioRoamerMaps ?? throw new InvalidDataException("celio_roamer_source_map_missing");
            var matches = maps.Where(pair => pair.Value == roamer.Runtime.Location).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("celio_roamer_source_map_missing");
            return _content.MapDefinition(matches[0].Key).RegionMapSection;
        }
    }
}
