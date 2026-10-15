using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Trainers;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SharedTrainerCatalogTests
{
    [TestMethod]
    public void RegionalAndRematchViewsResolveTheSameImmutableDefinitions()
    {
        var shared = SharedTrainerCatalog.LoadProject(TestAssets.Root);
        var forest = PretForestTrainers.Load(TestAssets.Root);
        var rematches = PretVsSeekerParties.Load(TestAssets.Root);
        var bill = PretBillTrainers.Load(TestAssets.Root);
        var lab = PretLabTrainerCatalog.Load(File.ReadAllText(Path.Combine(TestAssets.Root,
            "content/generated/data/trainer-battles/lab/lab-trainers.json")), shared);
        var twins = PretRoute8Twins.Load(TestAssets.Root);
        foreach (var trainer in forest.Trainers.Values.Concat(bill.Trainers.Values).Concat(lab.Trainers.Values)
                     .Concat(rematches.Trainers.Values))
            Assert.AreSame(shared.Definition(trainer.Id), trainer);
        Assert.AreSame(shared.Definition(twins.Trainer.Id), twins.Trainer);
        Assert.IsTrue(bill.Trainers.Keys.Intersect(rematches.Trainers.Keys).Any());
        Assert.AreSame(shared, SharedTrainerCatalog.LoadProject(Path.Combine(TestAssets.Root, ".")));
    }

    [TestMethod]
    public void EveryRegionalReferenceResolvesWithoutEmbeddingAnotherDefinition()
    {
        var shared = SharedTrainerCatalog.LoadProject(TestAssets.Root);
        var options = shared.CreateJsonOptions();
        var documents = 0;
        foreach (var path in Directory.EnumerateFiles(Path.Combine(TestAssets.Root, "content/generated"), "*.json", SearchOption.AllDirectories))
        {
            if (path == Path.Combine(TestAssets.Root, SharedTrainerCatalog.RelativePath)) continue;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            AssertNoEmbeddedDefinition(document.RootElement, path);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("trainers", out var trainers) ||
                trainers.ValueKind != JsonValueKind.Array || trainers.GetArrayLength() == 0 ||
                trainers[0].ValueKind != JsonValueKind.String) continue;
            var resolved = JsonSerializer.Deserialize<ImmutableArray<TrainerDefinition>>(trainers.GetRawText(), options);
            Assert.AreEqual(trainers.GetArrayLength(), resolved.Length);
            for (var index = 0; index < resolved.Length; index++)
            {
                Assert.AreEqual(trainers[index].GetString(), resolved[index].Id.Value);
                Assert.AreSame(shared.Definition(resolved[index].Id), resolved[index]);
            }
            documents++;
        }
        Assert.IsTrue(documents > 0, "No regional trainer references were checked.");
    }

    [TestMethod]
    public void TypedReferencesRejectExpandedObjectsUnknownIdsAndNull()
    {
        var shared = SharedTrainerCatalog.LoadProject(TestAssets.Root);
        var options = shared.CreateJsonOptions();
        var trainer = shared.Definition(new("pret:TRAINER_YOUNGSTER_BEN"));
        var json = JsonSerializer.Serialize(trainer, options);
        Assert.AreEqual("\"pret:TRAINER_YOUNGSTER_BEN\"", json);
        Assert.AreSame(trainer, JsonSerializer.Deserialize<TrainerDefinition>(json, options));
        Assert.ThrowsExactly<InvalidDataException>(() => JsonSerializer.Deserialize<TrainerDefinition>("\"pret:TRAINER_MISSING\"", options));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<TrainerDefinition>("null", options));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<TrainerDefinition>(JsonSerializer.Serialize(trainer), options));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<TrainerDefinition>("{\"id\":\"pret:TRAINER_YOUNGSTER_BEN\"}", options));
    }

    [TestMethod]
    public void ExplicitRootsRemainIndependentAndSharedCatalogLoadsOnlyGeneratedData()
    {
        var trainer = SharedTrainerCatalog.LoadProject(TestAssets.Root).Definition(new("pret:TRAINER_YOUNGSTER_BEN"));
        var root = Path.Combine(Path.GetTempPath(), "firered-shared-trainers-" + Guid.NewGuid().ToString("N"));
        var first = Path.Combine(root, "first");
        var second = Path.Combine(root, "second");
        try
        {
            WriteCatalog(first, trainer);
            WriteCatalog(second, trainer with { BaseMoney = trainer.BaseMoney + 1 });
            var firstOptions = SharedTrainerCatalog.Options(first);
            var secondOptions = SharedTrainerCatalog.Options(second);
            var firstTrainer = JsonSerializer.Deserialize<TrainerDefinition>("\"pret:TRAINER_YOUNGSTER_BEN\"", firstOptions)!;
            var secondTrainer = JsonSerializer.Deserialize<TrainerDefinition>("\"pret:TRAINER_YOUNGSTER_BEN\"", secondOptions)!;
            Assert.AreEqual(trainer.BaseMoney, firstTrainer.BaseMoney);
            Assert.AreEqual(trainer.BaseMoney + 1, secondTrainer.BaseMoney);
            Assert.AreSame(firstTrainer, JsonSerializer.Deserialize<TrainerDefinition>("\"pret:TRAINER_YOUNGSTER_BEN\"", firstOptions));
            Assert.AreNotSame(firstTrainer, secondTrainer);
            Assert.IsFalse(Directory.Exists(Path.Combine(first, "content/raw")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void SharedDefinitionsRejectDuplicatesInvalidPartiesAndProofFields()
    {
        var trainer = SharedTrainerCatalog.LoadProject(TestAssets.Root).Definition(new("pret:TRAINER_YOUNGSTER_BEN"));
        Assert.ThrowsExactly<InvalidDataException>(() => SharedTrainerCatalog.Load(Serialize(trainer, trainer)));
        Assert.ThrowsExactly<InvalidDataException>(() => SharedTrainerCatalog.Load(Serialize(trainer with {
            Party = trainer.Party.SetItem(0, trainer.Party[0] with { Level = 0 }) })));
        Assert.ThrowsExactly<InvalidDataException>(() => SharedTrainerCatalog.Load(Serialize(trainer with {
            Party = trainer.Party.SetItem(0, trainer.Party[0] with { CustomMoves = [new("pret:MOVE_TACKLE")] }) })));
        var withProof = JsonNode.Parse(Serialize(trainer))!;
        withProof["trainers"]![0]!["source"] = new JsonObject();
        Assert.ThrowsExactly<ContentValidationException>(() => SharedTrainerCatalog.Load(withProof.ToJsonString()));
        Assert.ThrowsExactly<ContentValidationException>(() => SharedTrainerCatalog.Load("{\"trainers\":[],\"trainers\":[]}"));
    }

    private static string Serialize(params TrainerDefinition[] trainers) => JsonSerializer.Serialize(
        new SharedTrainerDocument(trainers.ToImmutableArray()), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private static void AssertNoEmbeddedDefinition(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            Assert.IsFalse(element.TryGetProperty("sourceTrainerId", out _) && element.TryGetProperty("party", out _) &&
                element.TryGetProperty("id", out _), "A regional resource embeds a trainer definition: " + path);
            foreach (var property in element.EnumerateObject()) AssertNoEmbeddedDefinition(property.Value, path + "." + property.Name);
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) AssertNoEmbeddedDefinition(child, path);
    }

    private static void WriteCatalog(string root, TrainerDefinition trainer)
    {
        var path = Path.Combine(root, SharedTrainerCatalog.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Serialize(trainer));
    }
}
