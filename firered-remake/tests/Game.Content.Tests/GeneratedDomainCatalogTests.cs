using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedDomainCatalogTests
{
    private static string GeneratedRoot(params string[] paths)
    {
        var root = TestAssets.Output();
        foreach (var path in paths)
        {
            var target = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(TestAssets.Root, path), target);
        }
        return root;
    }

    // Build the existing bounded ownership proof from generated metadata, without loading maps or art.
    private static PokemonCatalog OwnedCatalog()
    {
        var mechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root);
        var canonical = mechanics.Catalog;
        var species = PokemonFactory.FuchsiaSupportedSpecies.ToImmutableDictionary(id => id, id => canonical.Species[id]);
        using var route = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, PretFuchsiaRoutePokemon.CatalogRelativePath)));
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
        foreach (var row in route.RootElement.GetProperty("species").EnumerateArray())
        {
            var definition = row.Deserialize<PokemonSpeciesDefinition>(options)!;
            species = species.SetItem(definition.Id, definition);
        }
        var predecessor = new PokemonCatalog(canonical.SourceCommit, species, canonical.Moves);
        var candidate = predecessor with { Species = predecessor.Species.AddRange(
            SafariOwnedPokemonCatalog.AdditionalSpecies.Select(id => KeyValuePair.Create(id, canonical.Species[id]))) };
        return SafariOwnedPokemonCatalog.Authenticate(predecessor, candidate, mechanics).Catalog;
    }

    [TestMethod]
    public void CanonicalMovesSpeciesItemsAndMechanicsLoadWithoutSourcesOrRecipes()
    {
        var root = GeneratedRoot(PretCanonicalPokemonCatalog.CatalogRelativePath,
            PretMetronomeMoveCatalog.CatalogRelativePath,
            SourceBattleItemCatalogLoader.RelativePath);
        try
        {
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "scripts")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/sources")));
            var canonical = PretCanonicalPokemonCatalog.Load(root, root);
            var moves = PretMetronomeMoveCatalog.Load(root);
            Assert.AreEqual(386, canonical.Catalog.Species.Count);
            Assert.AreEqual(354, moves.Moves.Count);
            Assert.AreEqual(354, moves.MoveNames.Count);
            Assert.AreEqual(76, canonical.Abilities.Count);
            var mechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(root);
            Assert.AreEqual(386, mechanics.Catalog.Species.Count);
            Assert.IsNotNull(SourceBattleItemCatalogLoader.LoadAuthenticated(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void EncounterAndTrainerCatalogsLoadWithoutImportLocks()
    {
        string[] encounterFamilies = ["rocktunnel", "route11", "vermilion"];
        string[] trainerFamilies = ["rocktunnel", "route11", "vermilion", "ssanne", "vermilion-gym", "celadon-gym", "fuchsia-gym"];
        var paths = encounterFamilies.Select(f => $"content/generated/{f}-encounters/encounters.json")
            .Concat(trainerFamilies.Select(f => $"content/generated/{f}-trainers/trainers.json"))
            .Concat(["content/generated/old-rod/old-rod.json", PretSurfEncounters.CatalogRelativePath, PretNameRaterContract.RelativePath, PretCeruleanTrainers.RelativePath]).ToArray();
        var root = GeneratedRoot(paths);
        try
        {
            Assert.AreEqual(4, PretRockTunnelEncounters.Load(root).Tables.Count);
            Assert.AreEqual(2, PretRoute11Encounters.Load(root).Tables.Count);
            Assert.IsNotNull(PretVermilionEncounters.Load(root));
            Assert.IsNotNull(PretRockTunnelTrainers.Load(root));
            Assert.IsNotNull(PretRoute11Trainers.Load(root));
            Assert.IsNotNull(PretVermilionTrainers.Load(root));
            Assert.AreEqual(7, PretCeruleanTrainers.Load(root).Trainers.Count);
            Assert.IsNotNull(PretSsAnneTrainers.Load(root));
            Assert.IsNotNull(PretVermilionGymTrainers.Load(root));
            Assert.AreEqual(8, PretCeladonGymTrainers.LoadDocument(root).Trainers.Length);
            Assert.AreEqual(7, PretFuchsiaGymTrainers.LoadDocument(root).Trainers.Length);
            var oldRod = PretOldRod.Load(root);
            Assert.AreEqual(10, oldRod.Tables.Count);
            Assert.AreEqual(2, oldRod.Texts.Count);
            Assert.IsTrue(oldRod.Texts.Values.All(t => t.EndsWith("{PAUSE_UNTIL_PRESS}", StringComparison.Ordinal)));
            Assert.AreEqual(21, PretSurfEncounters.Load(root).Tables.Count);
            Assert.IsFalse(string.IsNullOrEmpty(PretNameRaterContract.Load(root).Source.ChoosePokemonText));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "scripts")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/sources")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void ServicePackagesLoadWithOnlyGeneratedFiles()
    {
        var generated = Path.Combine(TestAssets.Root, "content/generated");
        var root = GeneratedRoot(Directory.EnumerateFiles(generated, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(TestAssets.Root, path)).ToArray());
        try
        {
            Assert.IsNotNull(PretFameChecker.Load(root));
            Assert.IsNotNull(PretCapeBrinkTutor.Load(root));
            Assert.IsNotNull(PretFourIslandDayCare.Load(root));
            Assert.IsNotNull(PretGoodRodService.Load(root));
            Assert.IsNotNull(PretNpcTradeElyssa.Load(root));
            Assert.IsNotNull(PretNpcTradeHaden.Load(root));
            Assert.IsNotNull(PretNpcTrades.Load(root, SourceMechanicsCatalogLoader.LoadAuthenticated(root)));
            Assert.IsNotNull(PretOriginalTmAcquisition.Load(root));
            Assert.IsNotNull(PretOriginalTutorRooms.Load(root));
            Assert.IsNotNull(PretRoute5DayCare.Load(root));
            Assert.IsNotNull(PretSuperRodService.Load(root));
            Assert.IsNotNull(PretTrainerFanClub.Load(root));
            Assert.AreEqual(4, PretSafariEncounters.Load(PretSafariRegion.Load(root)).Tables.Count);
            Assert.AreEqual(40, PretPewterMachines.LoadRoute3Project(root).Compatibility.Length);
            Assert.IsNotNull(PretVermilionMachines.LoadCatalogProject(root));
            var towerMachines = PretPokemonTowerMachines.Extend(root, PretRocketHideoutMachines.LoadCatalogProject(root));
            var snorlaxMachines = PretSnorlaxMachines.Extend(root, towerMachines);
            var eeveeMachines = PretEeveeMachines.Extend(root, snorlaxMachines);
            Assert.AreEqual(103, eeveeMachines.Compatibility.Length);
            Assert.IsNotNull(PretSafariItems.LoadProject(root));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "scripts")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/sources")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void TeachingCatalogsLoadGeneratedDefinitionsAndTextsOnly()
    {
        var owned = OwnedCatalog();
        var root = GeneratedRoot(PretMoveTutors.RelativePath, PretMoveReminderService.RelativePath, PretMoveDeleterTexts.RelativePath);
        try
        {
            var tutors = PretMoveTutors.Load(root, owned).Catalog;
            var reminder = PretMoveReminderService.Load(root, owned).Catalog;
            Assert.AreSame(owned, tutors.Pokemon);
            Assert.AreSame(owned, reminder.Pokemon);
            Assert.AreEqual(15, tutors.Definitions.Count);
            Assert.AreEqual(13, tutors.Texts.Count);
            Assert.AreEqual(13, reminder.Texts.Count);
            Assert.AreEqual(4, MoveDeleterCatalog.Authenticate(owned, PretMoveDeleterTexts.LoadProject(root)).Texts.Count);
            foreach (var tutor in tutors.Definitions.Values)
            {
                Assert.AreEqual(owned.Species.Count, tutor.Compatibility.Count);
                Assert.AreEqual(owned.Moves[tutor.Move].Pp, tutor.Pp);
            }
            Assert.IsFalse(tutors.Definitions[0].Compatibility[new SpeciesId("pret:SPECIES_BULBASAUR")]);
            Assert.IsTrue(tutors.Definitions[1].Compatibility[new SpeciesId("pret:SPECIES_BULBASAUR")]);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "scripts")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void TeachingCatalogRejectsInvalidGeneratedCompatibility()
    {
        var owned = OwnedCatalog();
        var root = GeneratedRoot(PretMoveTutors.RelativePath);
        try
        {
            var path = Path.Combine(root, PretMoveTutors.RelativePath);
            var json = JsonNode.Parse(File.ReadAllText(path))!;
            json["compatibility"]![0]!["tutors"] = new JsonArray(15);
            File.WriteAllText(path, json.ToJsonString());
            Assert.ThrowsExactly<ArgumentException>(() => PretMoveTutors.Load(root, owned));
        }
        finally { Directory.Delete(root, true); }
    }
}
