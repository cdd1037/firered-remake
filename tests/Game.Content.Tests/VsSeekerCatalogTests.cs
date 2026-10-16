using Game.Content;
using Game.Domain.Trainers;
using System.Text.Json;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class VsSeekerCatalogTests
{
    [TestMethod]
    public void SharedPartiesCoverEveryRematchIdentityWithoutImportRecipes()
    {
        var catalog = PretVsSeeker.Load(TestAssets.Root).Catalog;
        var parties = PretVsSeekerParties.Load(TestAssets.Root);
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,
            "content/generated/data/services/vs-seeker.json")));
        Assert.IsFalse(json.RootElement.TryGetProperty("rosterRequirements", out _));
        foreach (var row in catalog.Rematches)
            for (int slot = 0; slot < row.Trainers.Length; slot++)
            {
                if (row.Trainers[slot] is VsSeekerRules.None or VsSeekerRules.Skip) continue;
                var trainer = parties.Trainers[new("pret:" + row.Trainers[slot])];
                Assert.AreEqual(row.TrainerSourceIds[slot], trainer.SourceTrainerId);
            }
    }

    [TestMethod]
    public void RematchRanksKeepSkipHolesProgressFallbackAndFinalParty()
    {
        var catalog = PretVsSeeker.Load(TestAssets.Root).Catalog;
        const string trainer = "TRAINER_BUG_CATCHER_COLTON";
        var row = catalog.FindBase(trainer)!;
        var fought = new HashSet<string>();
        Assert.AreEqual((byte)1, VsSeekerRules.NextRank(catalog, trainer, fought));
        fought.Add("TRAINER_BUG_CATCHER_COLTON_2");
        Assert.AreEqual((byte)3, VsSeekerRules.NextRank(catalog, trainer, fought));
        Assert.AreEqual((byte)1, VsSeekerRules.GateRank(catalog, row, 3, new HashSet<string>()));
        fought.Add("TRAINER_BUG_CATCHER_COLTON_3");
        Assert.AreEqual((byte)5, VsSeekerRules.NextRank(catalog, trainer, fought));
        fought.Add("TRAINER_BUG_CATCHER_COLTON_4");
        Assert.AreEqual((byte)5, VsSeekerRules.NextRank(catalog, trainer, fought));
        Assert.AreEqual((byte)3, VsSeekerRules.GateRank(catalog, row, 5, new HashSet<string>()));
        Assert.AreEqual("TRAINER_BUG_CATCHER_COLTON_4", VsSeekerRules.RematchTrainer(catalog, trainer,
            fought, new HashSet<string> { "FLAG_SYS_CAN_LINK_WITH_RS" }));
    }
}
