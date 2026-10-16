using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class PokemonCollectionPresentationTests
{
    private sealed record Inputs(GamePokemonCatalog Pokemon, PretPokedexMenu Dex);
    private static readonly Lazy<Inputs> Source = new(() =>
        new(SharedMachineCatalogTests.Pokemon(), PretPokedexMenu.Load(TestAssets.Root)));

    [TestMethod]
    public void TrainerOnlySpeciesHaveBattleArtWithoutOwnershipOrCollectionEntries()
    {
        var art = Load(TestAssets.Root);
        foreach (var symbol in new[] { "SNUBBULL", "PINSIR" })
        {
            var species = new SpeciesId("pret:SPECIES_" + symbol);
            Assert.IsFalse(Source.Value.Pokemon.Catalog.Species.ContainsKey(species));
            Assert.IsTrue(Source.Value.Pokemon.Opponents.Catalog.Species.ContainsKey(species));
            Assert.IsFalse(art.Entries.Any(row => row.Species == species));
            Assert.AreEqual(4, art.Sprites.Count(row => row.Species == species));
            int id = Source.Value.Pokemon.Opponents.Catalog.Species[species].SourceSpeciesId;
            var page = art.Present(Source.Value.Dex.BuildPage(id, [id], [], new(true, true, 0xb9, 0x6258)));
            Assert.IsNotNull(page.Portrait, "A seen opponent reuses its battle portrait in the Pokedex");
            Assert.IsNull(page.Footprint);
            Assert.AreEqual(string.Empty, page.Description);
            Assert.IsFalse(art.CaptureEntries.ContainsKey(id));
        }
    }

    [TestMethod]
    public void NewOwnedFamiliesHaveCompleteMenuAndBattlePresentation()
    {
        var art = Load(TestAssets.Root);
        foreach (var symbol in new[] { "LEDYBA", "LEDIAN", "SPINARAK", "ARIADOS", "HERACROSS" })
        {
            var entry = art.Entries.Single(row => row.Species.Value == "pret:SPECIES_" + symbol);
            Assert.IsTrue(art.Textures.ContainsKey(entry.Icon.TexturePath));
            Assert.IsTrue(art.CaptureEntries.ContainsKey(entry.SourceSpeciesId));
            if (symbol is "LEDIAN" or "HERACROSS") continue;
            var sprites = art.Sprites.Where(row => row.Species == entry.Species).ToArray();
            Assert.AreEqual(4, sprites.Length);
            foreach (var side in new[] { "front", "back" })
                foreach (var shiny in new[] { false, true })
                    Assert.IsTrue(art.Textures.ContainsKey(sprites.Single(row => row.Side == side && row.Shiny == shiny).Texture.TexturePath));
        }
    }

    [TestMethod]
    public void SharedCollectionSuppliesOwnedIconsCaptureAndPokedexArtFromGeneratedFiles()
    {
        var expected = Load(TestAssets.Root);
        var root = TestAssets.Output();
        try
        {
            foreach (var path in expected.Textures.Keys.Append(PretPokemonCollectionPresentation.RelativePath))
            {
                var destination = Path.Combine(root, path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(TestAssets.Root, path), destination);
            }
            var art = Load(root);
            var row = art.Entries.Single(entry => entry.Species == new SpeciesId("pret:SPECIES_DUNSPARCE"));
            Assert.AreEqual(206, row.SourceSpeciesId);
            Assert.AreEqual((32, 32), (row.Icon.Width, row.Icon.Height));
            Assert.AreEqual((64, 64), (row.Portrait.Width, row.Portrait.Height));
            Assert.AreEqual((16, 16), (row.Footprint.Width, row.Footprint.Height));
            Assert.AreEqual("content/generated/sprites/pokemon/trainer-tower/dunsparce-front-normal.png", row.Portrait.TexturePath);
            Assert.IsTrue(art.Textures.Values.All(bytes => bytes.Length > 0));
            Assert.AreEqual(Source.Value.Dex.Entries[206], art.DexEntries[206] with { Portrait = null, Footprint = null });
            Assert.AreEqual(row.Footprint.TexturePath, art.CaptureEntries[206].FootprintPath);
            Assert.AreEqual(row.Portrait.TexturePath, art.CaptureEntries[206].FrontSpritePath);
            Assert.AreEqual("LAND SNAKE", art.CaptureEntries[206].Category);
            var access = new PokedexMenuAccess(true, true, 0xb9, 0x6258);
            var seen = art.Present(Source.Value.Dex.BuildPage(206, [206], [], access));
            Assert.IsNotNull(seen.Portrait);
            Assert.IsNull(seen.Footprint);
            Assert.AreEqual(string.Empty, seen.Description);
            var caught = art.Present(Source.Value.Dex.BuildPage(206, [206], [206], access));
            Assert.AreEqual(row.Portrait, caught.Portrait);
            Assert.AreEqual(row.Footprint, caught.Footprint);
            Assert.AreEqual(Source.Value.Dex.Entries[206].Description, caught.Description);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "scripts")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("duplicate")]
    [DataRow("unknown-species")]
    [DataRow("wrong-source-id")]
    [DataRow("wrong-dimensions")]
    [DataRow("unsafe-path")]
    public void SharedCollectionRejectsInvalidBindings(string mutation)
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root,
            PretPokemonCollectionPresentation.RelativePath)))!;
        var entries = json["entries"]!.AsArray();
        switch (mutation)
        {
            case "empty": entries.Clear(); break;
            case "duplicate": entries.Add(entries[0]!.DeepClone()); break;
            case "unknown-species": entries[0]!["species"]!["value"] = "pret:SPECIES_UNKNOWN"; break;
            case "wrong-source-id": entries[0]!["sourceSpeciesId"] = 16; break;
            case "wrong-dimensions": entries[0]!["icon"]!["width"] = 16; break;
            case "unsafe-path": entries[0]!["icon"]!["texturePath"] = "content/generated/../raw/escape.png"; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        var root = TestAssets.Output();
        try
        {
            var path = Path.Combine(root, PretPokemonCollectionPresentation.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => Load(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static PretPokemonCollectionPresentation Load(string root) =>
        PretPokemonCollectionPresentation.Load(root, Source.Value.Pokemon, Source.Value.Dex);
}
