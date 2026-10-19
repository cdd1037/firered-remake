using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.World;

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
        const string name = "Flamme {literal} compagnon";
        const string eggName = "œuf {literal}";
        TestAssets.EditJson(output.Path, PokedexPath, document =>
        {
            foreach (var entry in document["entries"]!.AsArray())
                if (sourceIds.Contains(entry!["sourceSpeciesId"]!.GetValue<int>())) entry["name"] = name;
        });

        TestAssets.EditJson(output.Path, SourceDisplayNames.RelativePath, document => document["eggName"] = eggName);
        var edited = PretCampaignCatalog.LoadCurrent(output.Path);
        Assert.AreEqual(eggName, edited.SourceSpeciesName(412));
        Assert.AreEqual(eggName, edited.PresentedSpeciesName(412));
        foreach (var species in expected)
        {
            Assert.AreEqual(name, edited.PresentedSpeciesName(species.SourceSpeciesId));
            Assert.AreEqual(name, edited.SourceSpeciesName(species.SourceSpeciesId));
            var pokemon = PokemonFactory.Create(edited.GamePokemon.Catalog,
                new(species.Id, 30, 1, IvPolicy.Fixed(20), PersonalityPolicy.Fixed(1)), new(0)).Pokemon;
            var owned = new PartyPokemon(1, pokemon, null, "RED", 0, 2, "MAPSEC_ROUTE_1", 30, 4, "ITEM_POKE_BALL", "ITEM_NONE");
            Assert.AreEqual(name, edited.PokemonDisplayName(owned));
            string originalEnglish = Original.SourceSpeciesName(species.SourceSpeciesId)!;
            Assert.AreEqual(originalEnglish, edited.PokemonDisplayName(owned with { CustomNickname = originalEnglish }));
            Assert.AreEqual("PETAL", edited.PokemonDisplayName(owned with { CustomNickname = "PETAL" }));
            Assert.AreEqual(eggName, edited.PokemonDisplayName(owned with { IsEgg = true }));

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
    [DataRow("pret:SPECIES_RAPIDASH", false)]
    [DataRow("pret:SPECIES_RAPIDASH", true)]
    [DataRow("pret:SPECIES_SLUGMA", false)]
    [DataRow("pret:SPECIES_SLUGMA", true)]
    [DataRow("pret:SPECIES_MAGCARGO", false)]
    [DataRow("pret:SPECIES_MAGCARGO", true)]
    public void UnchangedDisplayLabelsCannotReplaceRequiredAdmittedSpecies(string speciesId, bool campaignContent)
    {
        var id = new SpeciesId(speciesId);
        var original = Original.GamePokemon;
        var species = original.Catalog.Species.Remove(id).ToImmutableDictionary(pair => pair.Key, pair => pair.Value with
        {
            Evolutions = pair.Value.Evolutions.Where(edge => edge.Target != id).ToImmutableArray()
        });
        var owned = GamePokemonCatalog.Create(original.Mechanics, original.Catalog with { Species = species },
            original.Opponents.Catalog, original.TrainerTowerOpponents.Catalog);
        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
        {
            if (campaignContent) Validate(Original.GamePokemon, new(owned, Original.PokemonContent.NicknameCharacters));
            else Validate(owned, Original.PokemonContent);
        });
        Assert.AreEqual("sevii_ruby_composition_scope", error.Message);
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("blank")]
    [DataRow("missing")]
    [DataRow("numeric-key")]
    public void SeviiSpeciesStillRequireNonblankNamesAtTheirCanonicalNumericIds(string change)
    {
        using var output = TestAssets.TemporaryOutput();
        TestAssets.CopyFiles(output.Path, [PokedexPath]);
        int sourceId = Original.GamePokemon.Catalog.Species[SeviiRubyOwnedPokemonCatalog.AdditionalSpecies[0]].SourceSpeciesId;
        TestAssets.EditJson(output.Path, PokedexPath, document =>
        {
            var entries = document["entries"]!.AsArray();
            var entry = entries.Single(row => row!["sourceSpeciesId"]!.GetValue<int>() == sourceId)!;
            switch (change)
            {
                case "empty": entry["name"] = ""; break;
                case "blank": entry["name"] = " \t\n"; break;
                case "missing": entries.Remove(entry); break;
                case "numeric-key": entry["sourceSpeciesId"] = 9999; break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
        });
        Assert.ThrowsExactly<InvalidDataException>(() => PretPokedexMenu.Load(output.Path));
    }

    private static void Validate(GamePokemonCatalog owned, CampaignPokemonContent pokemon) =>
        PretCampaignCatalog.ValidateSeviiRubyComposition(Original.SeviiRubyRegion, Original.SourceMaps.ToImmutableDictionary(),
            owned, owned.Opponents, Original.SeviiRubyTrainers!, Original.ScriptCompilation, pokemon, Original.SeviiRubyItems!);
}
