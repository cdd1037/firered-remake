using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class MapDisplayTextTests
{
    [TestMethod]
    [DataRow("town")]
    [DataRow("fly")]
    [DataRow("sevii")]
    public void OneCompleteMapSectionProjectionReachesEveryMapReaderWithoutChangingGeometry(string kind)
    {
        using var output = TestAssets.TemporaryOutput();
        var original = PretFlyMap.Load(TestAssets.Root, SourceTextPack.Load(null));
        var names = original.MapNames.Keys.Order(StringComparer.Ordinal).Select((id, index) => (id, name: "Location " + index))
            .ToDictionary(pair => pair.id, pair => pair.name);
        Directory.CreateDirectory(Path.Combine(output.Path, "source"));
        File.WriteAllText(Path.Combine(output.Path, "source/map-section-names.json"), JsonSerializer.Serialize(names));
        var translated = PretFlyMap.Load(TestAssets.Root, SourceTextPack.Load(output.Path));
        var before = Cells(kind, original); var after = Cells(kind, translated);
        Assert.IsTrue(before.Length > 0);
        Assert.IsTrue(after.Any(cell => cell.Name.Length != 0));
        Assert.IsTrue(after.Any(cell => cell.DungeonName.Length != 0));
        foreach (var (a, b) in before.Zip(after))
        {
            Assert.AreEqual(a, b with { Name = a.Name, DungeonName = a.DungeonName });
            Assert.AreEqual(a.Name.Length == 0 ? "" : names[a.SectionId], b.Name);
            Assert.AreEqual(a.DungeonName.Length == 0 ? "" : names[a.DungeonSectionId], b.DungeonName);
        }
        CollectionAssert.AreEqual(original.VisibleDestinationIcons(0, new HashSet<string>()).ToArray(),
            translated.VisibleDestinationIcons(0, new HashSet<string>()).ToArray());
        Assert.AreEqual(original.CellAt(2, 2, 2, new HashSet<string>()).SectionId,
            translated.CellAt(2, 2, 2, new HashSet<string>()).SectionId);
    }

    [TestMethod]
    [DataRow("MAPSEC_UNKNOWN", "Location")]
    [DataRow("MAPSEC_PALLET_TOWN", "")]
    [DataRow("MAPSEC_PALLET_TOWN", "{PLAYER}")]
    public void MapNamesRejectUnknownIdentityEmptyTextAndNewControls(string id, string value)
    {
        using var output = TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(output.Path, "source"));
        File.WriteAllText(Path.Combine(output.Path, "source/map-section-names.json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [id] = value }));
        Assert.ThrowsExactly<InvalidDataException>(() => PretFlyMap.Load(TestAssets.Root, SourceTextPack.Load(output.Path)));
    }

    private static ImmutableArray<TownMapCell> Cells(string kind, PretFlyMap fly) => kind switch {
        "town" => PretTownMap.Load(TestAssets.Root, mapNames: fly.MapNames).Cells,
        "fly" => fly.Regions.SelectMany(region => region.Cells).ToImmutableArray(),
        "sevii" => PretFirstSeviiTownMap.Load(TestAssets.Root, fly.MapNames).Cells,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
