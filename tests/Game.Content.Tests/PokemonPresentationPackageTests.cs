using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class PokemonPresentationPackageTests
{
    private static readonly Lazy<PretCanonicalPokemonCatalog> Canonical = new(() => PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root));
    private static readonly Lazy<PretPokedexMenu> Dex = new(() => PretPokedexMenu.Load(TestAssets.Root));

    [TestMethod]
    [DataRow("route22")]
    [DataRow("route3")]
    [DataRow("mtmoon")]
    [DataRow("cerulean")]
    [DataRow("bill")]
    [DataRow("vermilion")]
    [DataRow("ssanne")]
    [DataRow("route11")]
    [DataRow("rocktunnel")]
    [DataRow("celadon-approach")]
    [DataRow("celadon-gym")]
    [DataRow("rocket-hideout")]
    public void SharedLoaderPreservesEveryRenderingFieldTextureAndDexBinding(string package)
    {
        var art = Load(TestAssets.Root, package);
        var directory = Path.Combine(TestAssets.Root, "content/generated/sprites/pokemon", package);
        using var battle = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "battle-art.json")));
        using var supplement = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "supplement.json")));
        var options = ContentJson.CreateOptions();
        Assert.IsTrue(JsonElement.DeepEquals(battle.RootElement.GetProperty("sprites"), JsonSerializer.SerializeToElement(art.Sprites, options)));
        Assert.IsTrue(JsonElement.DeepEquals(supplement.RootElement.GetProperty("icons"), JsonSerializer.SerializeToElement(art.Icons, options)));
        Assert.IsTrue(JsonElement.DeepEquals(supplement.RootElement.GetProperty("footprints"), JsonSerializer.SerializeToElement(art.Footprints, options)));
        CollectionAssert.AreEquivalent(supplement.RootElement.GetProperty("dexSpeciesIds").EnumerateArray().Select(row => row.GetInt32()).ToArray(), art.DexEntries.Keys.ToArray());
        foreach (var sprite in art.Sprites)
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(directory, sprite.TexturePath)),
                art.ReadSpriteTexture(new(sprite.SpeciesId), sprite.Side, sprite.PaletteVariant == "shiny"));
        foreach (var icon in art.Icons)
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(directory, icon.TexturePath)), art.ReadIconTexture(new(icon.SpeciesId)));
        foreach (var footprint in art.Footprints)
        {
            var entry = art.DexEntries[footprint.SourceSpeciesId];
            Assert.AreEqual(Dex.Value.Entries[footprint.SourceSpeciesId], entry with { Portrait = null, Footprint = null });
            Assert.AreEqual(entry.Name, art.CaptureDexEntries[footprint.SourceSpeciesId].Name);
            Assert.AreEqual(entry.Portrait!.TexturePath, art.CaptureDexEntries[footprint.SourceSpeciesId].FrontSpritePath);
            Assert.AreEqual(entry.Footprint!.TexturePath, art.CaptureDexEntries[footprint.SourceSpeciesId].FootprintPath);
            var bytes = art.ReadFootprintTexture(footprint.SourceSpeciesId);
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(directory, footprint.TexturePath)), bytes);
            CollectionAssert.AreEqual(bytes, art.ReadTexture(entry.Footprint.TexturePath));
            bytes[0] ^= 0xff;
            Assert.AreNotEqual(bytes[0], art.ReadTexture(entry.Footprint.TexturePath)[0], "Texture accessors must not expose mutable cached storage.");
        }
    }

    [TestMethod]
    public void PresentationNeedsOnlyActualAssetBindingsWithoutHistoricalManifestsOrAudioCopies()
    {
        WithPackage("route22", (root, directory) =>
        {
            File.Delete(Path.Combine(directory, "manifest.json"));
            var art = Load(root, "route22", ownDex: true);
            Assert.IsTrue(art.Sprites.Length > 0);
            Assert.IsTrue(art.DexEntries.Count > 0);
        });
    }

    [TestMethod]
    [DataRow("duplicateSprite")]
    [DataRow("missingVariant")]
    [DataRow("unknownSpecies")]
    [DataRow("invalidWidth")]
    [DataRow("invalidPalette")]
    [DataRow("unsafeTexture")]
    public void SharedLoaderRejectsInvalidRuntimeAssetBindings(string mutation)
    {
        WithPackage("celadon-approach", (root, directory) =>
        {
            var path = Path.Combine(directory, "battle-art.json");
            var node = JsonNode.Parse(File.ReadAllText(path))!;
            var sprites = node["sprites"]!.AsArray();
            switch (mutation)
            {
                case "duplicateSprite": sprites[1] = sprites[0]!.DeepClone(); break;
                case "missingVariant": sprites.RemoveAt(0); break;
                case "unknownSpecies": sprites[0]!["speciesId"] = "pret:SPECIES_NOT_PRESENT"; break;
                case "invalidWidth": sprites[0]!["width"] = 0; break;
                case "invalidPalette": sprites[0]!["paletteRgb555"]![0] = 32768; break;
                case "unsafeTexture": sprites[0]!["texturePath"] = "../outside.png"; break;
                default: Assert.Fail("Unsupported mutation."); break;
            }
            File.WriteAllText(path, node.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => Load(root, "celadon-approach", ownDex: true));
        });
    }

    private static PokemonPresentationPackage Load(string root, string package, bool ownDex = false) =>
        PokemonPresentationPackage.Load(Canonical.Value, root, package, ownDex ? null : Dex.Value);

    private static void WithPackage(string package, Action<string, string> action)
    {
        var root = TestAssets.Output();
        try
        {
            foreach (var sourcePackage in new[] { package, "pokedex-menu" })
            {
                var relative = "content/generated/sprites/pokemon/" + sourcePackage;
                foreach (var file in Directory.EnumerateFiles(Path.Combine(TestAssets.Root, relative), "*", SearchOption.AllDirectories))
                {
                    var destination = Path.Combine(root, Path.GetRelativePath(TestAssets.Root, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(file, destination);
                }
            }
            action(root, Path.Combine(root, "content/generated/sprites/pokemon", package));
        }
        finally { Directory.Delete(root, true); }
    }
}
