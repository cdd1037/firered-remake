using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application;

internal static class TrainerDisplayTextFixture
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal const string Name = "Trainer{B_BUFF2}\\p";
    internal const string Class = "Junior{B_TRAINER1_NAME}";

    internal static PretCampaignCatalog WithTranslations(PretCampaignCatalog original)
    {
        using var output = TestAssets.TemporaryOutput();
        var shared = SharedTrainerCatalog.LoadProject(TestAssets.Root);
        var trainers = shared.Definitions.Where(trainer => trainer.Id.Value == "pret:TRAINER_YOUNGSTER_BEN" ||
            trainer.Id == original.Route8Twins!.Trainer.Id || trainer.TrainerClass is
                "TRAINER_CLASS_RIVAL_EARLY" or "TRAINER_CLASS_RIVAL_LATE" or "TRAINER_CLASS_CHAMPION").ToArray();
        string source = Path.Combine(output.Path, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "trainer-names.json"),
            JsonSerializer.Serialize(trainers.ToDictionary(trainer => trainer.Id.Value, _ => Name)));
        File.WriteAllText(Path.Combine(source, "trainer-class-names.json"),
            JsonSerializer.Serialize(trainers.Select(trainer => trainer.TrainerClass).Distinct()
                .ToDictionary(id => id, _ => Class)));
        var display = SourceTrainerDisplayNames.LoadProject(TestAssets.Root, shared, SourceTextPack.Load(output.Path));
        var copy = (PretCampaignCatalog)typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        typeof(PretCampaignCatalog).GetField("<TrainerDisplayNames>k__BackingField", Private)!.SetValue(copy, display);
        Assert.AreSame(original.Route8Twins, copy.Route8Twins);
        Assert.AreNotSame(original.TrainerDisplayNames, copy.TrainerDisplayNames);
        return copy;
    }
}

[TestClass, TestCategory("Game")]
public sealed class TrainerDisplayTextTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExistingOrdinaryAndDoubleClassResolversUseOneProjection(bool doubles)
    {
        var original = CurrentCampaignFixture.Catalog;
        var translated = TrainerDisplayTextFixture.WithTranslations(original);
        var session = CampaignSession.CreateCurrent(translated);
        TrainerId id = doubles ? translated.Route8Twins!.Trainer.Id : new("pret:TRAINER_YOUNGSTER_BEN");
        var before = session.State;
        var method = typeof(CampaignSession).GetMethod(doubles ? "DoubleTrainerClass" : "OrdinaryTrainerClass",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.AreEqual(TrainerDisplayTextFixture.Class, method.Invoke(session, [id]));
        Assert.AreEqual(TrainerDisplayTextFixture.Name, translated.TrainerDisplayName(id));
        Assert.AreNotEqual(translated.TrainerDisplayName(id), original.TrainerDisplayName(id));
        Assert.AreSame(before, session.State);
    }

    [TestMethod]
    [DataRow("TRAINER_CLASS_RIVAL_EARLY")]
    [DataRow("TRAINER_CLASS_RIVAL_LATE")]
    [DataRow("TRAINER_CLASS_CHAMPION")]
    public void RivalCustomizationWinsOverBothSourceAndDisplayOverrides(string trainerClass)
    {
        var translated = TrainerDisplayTextFixture.WithTranslations(CurrentCampaignFixture.Catalog);
        var trainer = SharedTrainerCatalog.LoadProject(TestAssets.Root).Definitions.First(row => row.TrainerClass == trainerClass);
        const string rival = "Rival{B_BUFF2}\\p";
        Assert.AreEqual(TrainerDisplayTextFixture.Name, translated.TrainerDisplayName(trainer.Id));
        Assert.AreEqual(rival, translated.TrainerDisplayName(trainer, rival));
    }
}
