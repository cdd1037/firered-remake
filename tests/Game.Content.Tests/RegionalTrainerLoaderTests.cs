using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;
using Game.Domain.Trainers;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class RegionalTrainerLoaderTests
{
    private sealed record Region(string Path, Func<string, RegionalTrainerDocument> Document,
        Action<string> Catalog, string? EmptyError, bool ByTrainer = false, bool Ordinary = false);

    private static IEnumerable<Region> Regions()
    {
        yield return new("five-island", PretFiveIslandRules.LoadDocument, root => PretFiveIslandRules.LoadTrainers(root), "Missing Five Island trainer records.", Ordinary: true);
        yield return new("six-island", PretSixIslandRules.LoadDocument, root => PretSixIslandRules.LoadTrainers(root), "Missing Six Island trainer records.");
        yield return new("four-island", PretFourIslandRules.LoadDocument, root => PretFourIslandRules.LoadTrainers(root), "Missing Four Island trainer records.", Ordinary: true);
        yield return new("sevii-ruby", PretSeviiRubyRules.LoadDocument, root => PretSeviiRubyRules.LoadTrainers(root), "Missing Ruby trainer records.");
        yield return new("seafoam", PretSeafoamRules.LoadDocument, root => PretSeafoamRules.LoadTrainers(root), "seafoam_trainers_missing");
        yield return new("fighting-dojo", PretFightingDojoRules.LoadDocument, root => PretFightingDojoRules.LoadTrainers(root), "fighting_dojo_trainers_missing", Ordinary: true);
        var machines = SharedMachineCatalog.LoadProject(TestAssets.Root);
        yield return new("seven-island", root => PretSevenIslandRules.LoadDocument(root, machines), root => PretSevenIslandRules.LoadTrainers(root, machines), "seven_island_trainers_missing");
        yield return new("viridian-gym", PretViridianGymRules.LoadDocument, root => PretViridianGymRules.LoadTrainers(root), null, Ordinary: true);
        yield return new("pokemon-league", PretPokemonLeagueRules.LoadDocument, root => PretPokemonLeagueRules.LoadTrainers(root), "Missing Pokemon League trainer or battle records.", true);
        yield return new("league-rematch", PretLeagueRematchRules.LoadDocument, root => PretLeagueRematchRules.LoadTrainers(root), "Missing Pokemon League trainer or battle records.", true);
        yield return new("league-rival", PretLeagueRivalRules.LoadDocument, root => PretLeagueRivalRules.LoadTrainers(root), "Missing League rival trainer or battle records.", true);
    }

    private static string RelativePath(Region region) => region.Path == "league-rival"
        ? "content/generated/data/trainer-battles/league-rival-rules.json"
        : $"content/generated/data/regions/{region.Path}/trainers.json";

    [TestMethod]
    public void AllRegionalReadersRetainDocumentOrderAndSharedDefinitionIdentity()
    {
        var shared = SharedTrainerCatalog.LoadProject(TestAssets.Root);
        foreach (var region in Regions())
        {
            var document = region.Document(TestAssets.Root);
            var source = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, RelativePath(region))))!;
            CollectionAssert.AreEqual(source["trainers"]!.AsArray().Select(t => t!.GetValue<string>()).ToArray(),
                document.Trainers.Select(t => t.Id.Value).ToArray(), region.Path);
            CollectionAssert.AreEqual(source["battles"]!.AsArray().Select(b => b!["scriptEntry"]!.GetValue<string>()).ToArray(),
                document.Battles.Select(b => b.ScriptEntry).ToArray(), region.Path);
            foreach (var trainer in document.Trainers) Assert.AreSame(shared.Definition(trainer.Id), trainer);
            region.Catalog(TestAssets.Root);
        }
    }

    [TestMethod]
    public void BothEntryPointsRejectMalformedMembershipAndKeepRegionalErrors()
    {
        var root = Path.Combine(Path.GetTempPath(), "firered-regional-trainers-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "content/generated/data"));
        File.Copy(Path.Combine(TestAssets.Root, SharedTrainerCatalog.RelativePath), Path.Combine(root, SharedTrainerCatalog.RelativePath));
        try
        {
            foreach (var region in Regions())
            {
                var original = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, RelativePath(region))))!;
                var path = Path.Combine(root, RelativePath(region));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                void Check<T>(Action<JsonNode> mutation, string? message = null) where T : Exception
                {
                    var changed = original.DeepClone();
                    mutation(changed);
                    File.WriteAllText(path, changed.ToJsonString());
                    var documentError = Assert.ThrowsExactly<T>(() => region.Document(root), region.Path);
                    var catalogError = Assert.ThrowsExactly<T>(() => region.Catalog(root), region.Path);
                    if (message is not null)
                    {
                        Assert.AreEqual(message, documentError.Message, region.Path);
                        Assert.AreEqual(message, catalogError.Message, region.Path);
                    }
                }
                // Equal party references still coalesce. Distinct calls remain ordered occurrences;
                // the ordinary catalog must reject duplicate occurrences instead of silently coalescing them.
                var repeated = original.DeepClone();
                repeated["trainers"]!.AsArray().Add(repeated["trainers"]![0]!.DeepClone());
                File.WriteAllText(path, repeated.ToJsonString());
                Assert.AreEqual(original["trainers"]!.AsArray().Count + 1, region.Document(root).Trainers.Length);
                region.Catalog(root);
                if (region.Ordinary)
                    Check<ArgumentException>(node => node["battles"]!.AsArray().Add(node["battles"]![0]!.DeepClone()),
                        "ordinary_trainer_occurrence_identity");
                else
                {
                    repeated["battles"]!.AsArray().Add(repeated["battles"]![0]!.DeepClone());
                    File.WriteAllText(path, repeated.ToJsonString());
                    Assert.AreEqual(original["battles"]!.AsArray().Count + 1, region.Document(root).Battles.Length);
                    region.Catalog(root);
                }
                Check<JsonException>(node => node.AsObject().Remove("trainers"));
                Check<JsonException>(node => node["unexpected"] = true);
                Check<InvalidDataException>(node => node["trainers"]![0] = "pret:TRAINER_MISSING");
                Check<ArgumentException>(node =>
                {
                    var duplicate = node["battles"]![0]!.DeepClone();
                    duplicate["introText"] = "conflicting intro";
                    node["battles"]!.AsArray().Add(duplicate);
                });
                Check<ArgumentException>(node => node["battles"]![0]!["trainer"]!["value"] = "pret:TRAINER_MISSING", "trainer_catalog_battle_reference");
                foreach (var member in new[] { "trainers", "battles" })
                {
                    if (region.EmptyError is not null)
                        Check<InvalidDataException>(node => node[member] = new JsonArray(), region.EmptyError);
                    else Check<ArgumentException>(node => node[member] = new JsonArray());
                }
                if (region.Path == "six-island")
                    Check<NotSupportedException>(node => { node["battles"]![0]!["mode"] = 4; node["battles"]![0]!["notEnoughMonsText"] = ""; }, "six_island_trainer_battle_source");
                else if (!region.ByTrainer && !region.Ordinary)
                    Check<NotSupportedException>(node => node["battles"]![0]!["scriptEntry"] = "invalid");
                else if (region.Ordinary)
                    Check<ArgumentException>(node => node["battles"]![0]!["callback"] = "InvalidCallback",
                        "ordinary_trainer_battle_semantics");
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void OrderedTrainerListsRemainTheSingleSourceForMembership()
    {
        var five = PretFiveIslandRules.LoadDocument(TestAssets.Root);
        var fiveCatalog = PretFiveIslandRules.LoadTrainers(TestAssets.Root);
        CollectionAssert.AreEquivalent(five.Trainers.Select(t => t.Id).ToArray(), fiveCatalog.Trainers.Keys.ToArray());
        CollectionAssert.AreEqual(five.Battles.ToArray(), fiveCatalog.Battles.ToArray());
        CollectionAssert.AreEqual(PretSevenIslandRules.LoadDocument(TestAssets.Root).Trainers.Select(t => t.Id).ToArray(),
            SevenIslandTrainerPartyFactory.AllTrainers.ToArray());
        CollectionAssert.AreEqual(PretSeviiRubyRules.LoadDocument(TestAssets.Root).Trainers.Select(t => t.Id).ToArray(),
            SeviiRubyTrainerPartyFactory.AllTrainers.ToArray());
        Assert.IsTrue(SevenIslandTrainerPartyFactory.TrainerIds.SetEquals(SevenIslandTrainerPartyFactory.AllTrainers));
        CollectionAssert.AreEqual(SeviiRubyTrainerPartyFactory.AllTrainers.Where(id => id != SeviiRubyTrainerPartyFactory.DoubleTrainer).ToArray(),
            SeviiRubyTrainerPartyFactory.SupportedTrainers.ToArray());
    }

    [TestMethod]
    public void LoaderConstructsAndValidatesOncePerPublicEntryPoint()
    {
        int constructions = 0, validations = 0;
        var loader = new RegionalTrainerLoader<FiveIslandTrainerDocument, OrdinaryTrainerCatalog>(
            "content/generated/data/regions/five-island/trainers.json", "missing", document =>
            {
                constructions++;
                return new OrdinaryTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id),
                    document.Battles, "five-island-trainer");
            }, catalog => { validations++; Assert.IsFalse(catalog.Trainers.IsEmpty); });
        loader.LoadDocument(TestAssets.Root);
        Assert.AreEqual(1, constructions);
        Assert.AreEqual(1, validations);
        loader.LoadCatalog(TestAssets.Root);
        Assert.AreEqual(2, constructions);
        Assert.AreEqual(2, validations);
    }
}
