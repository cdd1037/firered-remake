using System.Collections.Immutable;
using Game.Domain.Encounters;

namespace Game.Content;

public sealed record OrdinaryFishingDocument(int SchemaVersion, ImmutableArray<FishingRodTable> Tables);

/// <summary>Complete ordinary rod/map definitions. Mounted-map admission is a separate composition step.</summary>
public static class PretOrdinaryFishing
{
    public static ImmutableArray<FishingRodTable> Load(string projectRoot) =>
        Parse(File.ReadAllText(Path.Combine(projectRoot, "content/generated/data/encounters/fishing.json")));

    public static ImmutableArray<FishingRodTable> Parse(string json)
    {
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<FishingRod>());
        var document = ContentJson.ParseStrict<OrdinaryFishingDocument>(json, options);
        if (document.SchemaVersion != 1) throw new InvalidDataException("ordinary_fishing_schema");
        try
        {
            var profile = new FishingTaskProfile("ordinary-fishing-definitions", document.Tables, []);
            if (profile.Tables.Keys.Any(k => k.Map.Value.StartsWith("map.safari-zone-", StringComparison.Ordinal)) ||
                profile.Tables.Keys.Select(k => k.Map).Distinct().Any(map =>
                    Enum.GetValues<FishingRod>().Any(rod => !profile.HasTable(rod, map))))
                throw new ArgumentException("ordinary_fishing_map_or_rod_coverage");
        }
        catch (ArgumentException error) { throw new InvalidDataException("ordinary_fishing_definitions", error); }
        return document.Tables;
    }
}
