using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class SeviiRubySpeciesDisplayNameTests
{
    private const string PokedexPath = "content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json";
    private static PretCampaignCatalog Original => TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));

    [TestMethod]
    public void CurrentCompositionAcceptsEditedSeviiNamesAndPreservesTrainerEncoding()
    {
        using var output = TestAssets.TemporaryOutput();
        TestAssets.CopyFiles(output.Path, Directory.EnumerateFiles(Path.Combine(TestAssets.Root, "content/generated"),
            "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(TestAssets.Root, path)));
        var expected = SeviiRubyOwnedPokemonCatalog.AdditionalSpecies.Select(id => Original.GamePokemon.Catalog.Species[id]).ToArray();
        var sourceIds = expected.Select(species => species.SourceSpeciesId).ToHashSet();
        // Current party/save names still use the existing nickname alphabet and ten-character limit.
        const string name = "Fire pal";
        TestAssets.EditJson(output.Path, PokedexPath, document =>
        {
            foreach (var entry in document["entries"]!.AsArray())
                if (sourceIds.Contains(entry!["sourceSpeciesId"]!.GetValue<int>())) entry["name"] = name;
        });

        var edited = PretCampaignCatalog.LoadCurrent(output.Path);
        foreach (var species in expected)
        {
            Assert.AreEqual(name, edited.PokemonContent.SpeciesNames[species.SourceSpeciesId]);
            Assert.AreEqual(name, edited.SourceSpeciesName(species.SourceSpeciesId));
            var actual = edited.GamePokemon.Catalog.Species[species.Id];
            Assert.AreEqual(species.SourceSpeciesId, actual.SourceSpeciesId);
            Assert.AreEqual(species.BaseStats, actual.BaseStats);
            CollectionAssert.AreEqual(species.LevelUpMoves.ToArray(), actual.LevelUpMoves.ToArray());
            CollectionAssert.AreEqual(species.Evolutions.ToArray(), actual.Evolutions.ToArray());
        }
        foreach (var (id, trainer) in Original.SeviiRubyTrainers!.Trainers)
        {
            var actual = edited.SeviiRubyTrainers!.Trainers[id];
            Assert.AreEqual(trainer.SourceTrainerName, actual.SourceTrainerName);
            CollectionAssert.AreEqual(trainer.EncodedTrainerName.ToArray(), actual.EncodedTrainerName.ToArray());
            Assert.AreEqual(trainer.Party.Length, actual.Party.Length);
            for (int i = 0; i < trainer.Party.Length; i++)
            {
                Assert.AreEqual(trainer.Party[i].Species, actual.Party[i].Species);
                Assert.AreEqual(trainer.Party[i].SpeciesName, actual.Party[i].SpeciesName);
                CollectionAssert.AreEqual(trainer.Party[i].EncodedSpeciesName.ToArray(), actual.Party[i].EncodedSpeciesName.ToArray());
            }
        }
    }

    [TestMethod]
    [DataRow("pret:SPECIES_RAPIDASH")]
    [DataRow("pret:SPECIES_SLUGMA")]
    [DataRow("pret:SPECIES_MAGCARGO")]
    public void UnchangedDisplayLabelsCannotReplaceRequiredAdmittedSpecies(string speciesId)
    {
        var id = new SpeciesId(speciesId);
        var original = Original.GamePokemon;
        var species = original.Catalog.Species.Remove(id).ToImmutableDictionary(pair => pair.Key, pair => pair.Value with
        {
            Evolutions = pair.Value.Evolutions.Where(edge => edge.Target != id).ToImmutableArray()
        });
        var owned = GamePokemonCatalog.Create(original.Mechanics, original.Catalog with { Species = species },
            original.Opponents.Catalog, original.TrainerTowerOpponents.Catalog);
        var error = Assert.ThrowsExactly<InvalidDataException>(() => Validate(owned, Original.PokemonContent));
        Assert.AreEqual("sevii_ruby_composition_scope", error.Message);
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("blank")]
    [DataRow("missing")]
    [DataRow("numeric-key")]
    public void SeviiSpeciesStillRequireNonblankNamesAtTheirCanonicalNumericIds(string change)
    {
        var species = Original.GamePokemon.Catalog.Species[SeviiRubyOwnedPokemonCatalog.AdditionalSpecies[0]];
        var names = Original.PokemonContent.SpeciesNames;
        names = change switch
        {
            "empty" => names.SetItem(species.SourceSpeciesId, ""),
            "blank" => names.SetItem(species.SourceSpeciesId, " \t\n"),
            "missing" => names.Remove(species.SourceSpeciesId),
            "numeric-key" => names.Remove(species.SourceSpeciesId).Add(9999, names[species.SourceSpeciesId]),
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        var error = Assert.ThrowsExactly<InvalidDataException>(() => Validate(Original.GamePokemon,
            Original.PokemonContent with { SpeciesNames = names }));
        Assert.AreEqual("sevii_ruby_composition_scope", error.Message);
    }

    private static void Validate(GamePokemonCatalog owned, CampaignPokemonContent pokemon) =>
        PretCampaignCatalog.ValidateSeviiRubyComposition(Original.SeviiRubyRegion, Original.SourceMaps.ToImmutableDictionary(),
            owned, owned.Opponents, Original.SeviiRubyTrainers!, Original.ScriptCompilation, pokemon, Original.SeviiRubyItems!);
}
