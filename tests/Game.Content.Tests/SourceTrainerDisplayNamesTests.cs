using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SourceTrainerDisplayNamesTests
{
    private static readonly SourceTextPack English = SourceTextPack.Load(null);
    private static SharedTrainerCatalog Trainers => SharedTrainerCatalog.LoadProject(TestAssets.Root);
    private static string Classes => File.ReadAllText(Path.Combine(TestAssets.Root, SourceTrainerDisplayNames.ClassNamesRelativePath));

    [TestMethod]
    public void SourceClassesKeepGenderGlyphsAndPokemonGlyphExpansion()
    {
        var names = SourceTrainerDisplayNames.Load(Classes, Trainers, English);
        Assert.AreEqual("SWIMMER♂", names.ClassName("TRAINER_CLASS_SWIMMER_M"));
        Assert.AreEqual("SWIMMER♀", names.ClassName("TRAINER_CLASS_SWIMMER_F"));
        Assert.AreEqual("POKéMON BREEDER", names.ClassName("TRAINER_CLASS_PKMN_BREEDER"));
        Assert.AreEqual("POKéMON RANGER", names.ClassName("TRAINER_CLASS_PKMN_RANGER"));
        foreach (var trainer in Trainers.Definitions)
        {
            Assert.AreEqual(trainer.SourceTrainerName, names.Name(trainer.Id));
            Assert.AreEqual(names.ClassName(trainer.TrainerClass), names.ClassName(trainer.Id));
        }
    }

    [TestMethod]
    public void SparseProjectionKeepsCanonicalReferencesEncodedNamesAndAllRules()
    {
        using var output = TestAssets.TemporaryOutput();
        var ben = Trainers.Definition(new("pret:TRAINER_YOUNGSTER_BEN"));
        var fallback = Trainers.Definitions.First(trainer => trainer.Id != ben.Id);
        string before = JsonSerializer.Serialize(Trainers.Definitions.OrderBy(trainer => trainer.SourceTrainerId));
        Write(output.Path, "trainer-names", ben.Id.Value, "Trainer{B_BUFF2}\\p");
        Write(output.Path, "trainer-class-names", ben.TrainerClass, "Junior");
        var localized = SourceTrainerDisplayNames.Load(Classes, Trainers, SourceTextPack.Load(output.Path));
        var english = SourceTrainerDisplayNames.Load(Classes, Trainers, English);
        Assert.AreEqual("Trainer{B_BUFF2}\\p", localized.Name(ben.Id));
        Assert.AreEqual("Junior", localized.ClassName(ben.Id));
        Assert.AreEqual(fallback.SourceTrainerName, localized.Name(fallback.Id));
        Assert.AreEqual("BEN", english.Name(ben.Id));
        Assert.AreEqual("YOUNGSTER", english.ClassName(ben.Id));
        Assert.AreSame(ben, Trainers.Definition(ben.Id));
        Assert.AreEqual(before, JsonSerializer.Serialize(Trainers.Definitions.OrderBy(trainer => trainer.SourceTrainerId)),
            "Projection cannot alter source names, encoded trainer/species bytes, party members, or rule inputs.");
        var regional = PretRoute3Trainers.Load(TestAssets.Root);
        Assert.AreSame(ben, regional.Trainers[ben.Id]);
    }

    [TestMethod]
    public void SameDisplayNameKeepsDistinctTrainerIdsAndClassBindings()
    {
        using var output = TestAssets.TemporaryOutput();
        var first = Trainers.Definition(new("pret:TRAINER_YOUNGSTER_BEN"));
        var second = Trainers.Definitions.First(trainer => trainer.TrainerClass != first.TrainerClass);
        var source = Path.Combine(output.Path, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "trainer-names.json"), JsonSerializer.Serialize(new Dictionary<string, string>
        {
            [first.Id.Value] = "SAME", [second.Id.Value] = "SAME"
        }));
        var names = SourceTrainerDisplayNames.Load(Classes, Trainers, SourceTextPack.Load(output.Path));
        Assert.AreEqual(names.Name(first.Id), names.Name(second.Id));
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreEqual(names.ClassName(first.TrainerClass), names.ClassName(first.Id));
        Assert.AreEqual(names.ClassName(second.TrainerClass), names.ClassName(second.Id));
    }

    [TestMethod]
    [DataRow("TRAINER_CLASS_RIVAL_EARLY")]
    [DataRow("TRAINER_CLASS_RIVAL_LATE")]
    [DataRow("TRAINER_CLASS_CHAMPION")]
    public void SavedRivalNameRemainsLiteralForEveryRivalClass(string trainerClass)
    {
        var trainer = Trainers.Definitions.First(row => row.TrainerClass == trainerClass);
        const string rival = "{B_TRAINER1_NAME}\\p";
        ICampaignCatalog? catalog = null;
        Assert.AreEqual(rival, catalog.TrainerDisplayName(trainer, rival));
        Assert.AreSame(trainer, Trainers.Definition(trainer.Id));
    }

    [TestMethod]
    [DataRow("trainer-names", "pret:TRAINER_MISSING", "Missing")]
    [DataRow("trainer-class-names", "TRAINER_CLASS_MISSING", "Missing")]
    [DataRow("trainer-names", "pret:TRAINER_YOUNGSTER_BEN", " ")]
    [DataRow("trainer-class-names", "TRAINER_CLASS_YOUNGSTER", "\t")]
    public void UnknownOrEmptyOverridesFailWithoutChangingSharedDefinitions(string group, string id, string value)
    {
        using var output = TestAssets.TemporaryOutput();
        Write(output.Path, group, id, value);
        var ben = Trainers.Definition(new("pret:TRAINER_YOUNGSTER_BEN"));
        Assert.ThrowsExactly<InvalidDataException>(() => SourceTrainerDisplayNames.Load(Classes, Trainers, SourceTextPack.Load(output.Path)));
        Assert.AreSame(ben, Trainers.Definition(ben.Id));
        Assert.AreEqual("BEN", ben.SourceTrainerName);
    }

    [TestMethod]
    public void ClassCatalogRejectsMissingBindingsEmptyNamesAndDuplicateKeys()
    {
        var changed = JsonNode.Parse(Classes)!.AsObject();
        changed.Remove("TRAINER_CLASS_YOUNGSTER");
        Assert.ThrowsExactly<InvalidDataException>(() => SourceTrainerDisplayNames.Load(changed.ToJsonString(), Trainers, English));
        changed = JsonNode.Parse(Classes)!.AsObject();
        changed["TRAINER_CLASS_YOUNGSTER"] = " ";
        Assert.ThrowsExactly<InvalidDataException>(() => SourceTrainerDisplayNames.Load(changed.ToJsonString(), Trainers, English));
        Assert.ThrowsExactly<ContentValidationException>(() => SourceTrainerDisplayNames.Load(
            "{\"TRAINER_CLASS_YOUNGSTER\":\"A\",\"TRAINER_CLASS_YOUNGSTER\":\"B\"}", Trainers, English));
        var names = SourceTrainerDisplayNames.Load(Classes, Trainers, English);
        Assert.ThrowsExactly<InvalidDataException>(() => names.Name(new("pret:TRAINER_MISSING")));
        Assert.ThrowsExactly<InvalidDataException>(() => names.ClassName("TRAINER_CLASS_MISSING"));
    }

    [TestMethod]
    public void RawTrainerAndSpeciesEncodingValidationIsStillRequired()
    {
        var trainer = Trainers.Definition(new("pret:TRAINER_YOUNGSTER_BEN"));
        string Serialize(TrainerDefinition candidate) => JsonSerializer.Serialize(new SharedTrainerDocument([candidate]),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.ThrowsExactly<InvalidDataException>(() => SharedTrainerCatalog.Load(Serialize(trainer with { EncodedTrainerName = [] })));
        Assert.ThrowsExactly<InvalidDataException>(() => SharedTrainerCatalog.Load(Serialize(trainer with
        {
            Party = trainer.Party.SetItem(0, trainer.Party[0] with { EncodedSpeciesName = [256] })
        })));
        Assert.ThrowsExactly<InvalidDataException>(() => SharedTrainerCatalog.Load(Serialize(trainer with { SourceTrainerName = " " })));
    }

    private static void Write(string root, string group, string id, string value)
    {
        string source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, group + ".json"), JsonSerializer.Serialize(new Dictionary<string, string> { [id] = value }));
    }
}
