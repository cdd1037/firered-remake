using Game.Content;
namespace FireRed.Tests.Content;

[TestClass]
public sealed class SummaryPresentationTests
{
    private static readonly Lazy<SummaryPresentation> Art = new(() => SummaryPresentation.Load(TestAssets.Root));
    private static SummaryMemoFacts Facts(bool owner = true, int level = 5, string location = "MAPSEC_PALLET_TOWN", int game = 4, bool fateful = false)
        => new("HARDY", location, level, game, fateful, owner);
    [TestMethod]
    public void OriginalTrainerMemoUsesSourceMapNamesAndHatchLevel()
    {
        Assert.AreEqual("HARDY nature.\nMet in PALLET TOWN at Lv 5.", Art.Value.TrainerMemo(Facts()));
        Assert.AreEqual("HARDY nature.\nHatched: PALLET TOWN\nat Lv 5.", Art.Value.TrainerMemo(Facts(level: 0)));
        Assert.AreEqual("HARDY nature.\nMet in a trade at Lv 5.", Art.Value.TrainerMemo(Facts(location: "MAPSEC_LITTLEROOT_TOWN")));
    }
    [TestMethod]
    public void NonOriginalTrainerUsesOriginalApparentlyAndTradeBranches()
    {
        Assert.AreEqual("HARDY nature.\nApparently met in PALLET TOWN\nat Lv 5.", Art.Value.TrainerMemo(Facts(false)));
        // The English source intentionally uses ApparentlyMet even for an ordinary traded hatch.
        Assert.AreEqual(Art.Value.TrainerMemo(Facts(false)), Art.Value.TrainerMemo(Facts(false, 0)));
        Assert.AreEqual("HARDY nature.\nMet in a trade.", Art.Value.TrainerMemo(Facts(false, game: 15)));
        Assert.AreEqual("HARDY nature.\nMet in a trade.", Art.Value.TrainerMemo(Facts(false, location: "MAPSEC_LITTLEROOT_TOWN")));
    }
    [TestMethod]
    public void FatefulSourceVariantsKeepTheirOwnConditions()
    {
        Assert.AreEqual("HARDY nature.\nMet in a fateful encounter when\nat Lv 5.", Art.Value.TrainerMemo(Facts(location:"METLOC_FATEFUL_ENCOUNTER")));
        StringAssert.Contains(Art.Value.TrainerMemo(Facts(level:0,fateful:true)), "nature. Met in a fateful\nencounter (hatched: PALLET TOWN");
        StringAssert.Contains(Art.Value.TrainerMemo(Facts(false,0,fateful:true)), "Apparently met in\na fateful encounter");
    }
    [TestMethod]
    [DataRow("nature")]
    [DataRow("location")]
    [DataRow("trade")]
    public void InsertedMemoNamesRemainLiteral(string insertion)
    {
        using var temp = TestAssets.TemporaryOutput();
        const string path = "content/generated/ui/summary-screen/text.json";
        const string literal = "literal {DYNAMIC 0x01} {LV_2} {DYNAMIC 0x02} {DYNAMIC 0x00}";
        TestAssets.CopyFiles(temp.Path, [path]);
        TestAssets.EditJson(temp.Path, path, json =>
        {
            if (insertion == "location") json["locations"]!["MAPSEC_PALLET_TOWN"] = literal;
            if (insertion == "trade") json["memos"]!["ATrade"] = literal;
        });
        string nature = insertion == "nature" ? literal : "HARDY";
        string location = insertion == "nature" ? "PALLET TOWN" : literal;
        var facts = Facts(location: insertion == "trade" ? "MAPSEC_LITTLEROOT_TOWN" : "MAPSEC_PALLET_TOWN") with { Nature = nature };
        Assert.AreEqual($"{nature} nature.\nMet in {location} at Lv 5.", SummaryPresentation.Load(temp.Path).TrainerMemo(facts));
    }
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TradeFallbackRequiresNonblankSourceText(bool blank)
    {
        using var temp = TestAssets.TemporaryOutput();
        const string path = "content/generated/ui/summary-screen/text.json";
        TestAssets.CopyFiles(temp.Path, [path]);
        TestAssets.EditJson(temp.Path, path, json =>
        {
            if (blank) json["memos"]!["ATrade"] = " ";
            else json["memos"]!.AsObject().Remove("ATrade");
        });
        Assert.ThrowsExactly<InvalidDataException>(() => SummaryPresentation.Load(temp.Path));
    }
    [TestMethod]
    public void BounceUsesSourceThreeFrameStepsAndStopsAfterTwoCycles()
    {
        Assert.AreEqual(0, Art.Value.PortraitOffset(1,100,100,false,0,false));
        Assert.AreEqual(-5, Art.Value.PortraitOffset(2,100,100,false,0,false));
        Assert.AreEqual(-1, Art.Value.PortraitOffset(2,50,100,false,0,false));
        Assert.AreEqual(0, Art.Value.PortraitOffset(42,100,100,false,0,false));
        Assert.AreEqual(0, Art.Value.PortraitOffset(2,100,100,false,0,true));
        Assert.AreEqual(0, Art.Value.PortraitOffset(2,0,100,false,0,false));
    }
    [TestMethod]
    public void LoadsFromGeneratedOnlyRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "summary-generated-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "content/generated/ui/summary-screen");
        try
        {
            Directory.CreateDirectory(folder);
            File.Copy(Path.Combine(TestAssets.Root, "content/generated/ui/summary-screen/text.json"), Path.Combine(folder, "text.json"));
            var loaded = SummaryPresentation.Load(root);
            Assert.AreEqual(Art.Value.TrainerMemo(Facts()), loaded.TrainerMemo(Facts()));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [TestMethod]
    public void MirroredPortraitKeepsItsNativeLeftPaneBounds()
    {
        foreach (bool flip in new[] { false, true })
        {
            var portrait = SummaryPresentation.PortraitPlacement(false, flip);
            Assert.AreEqual(28, portrait.X);
            Assert.AreEqual(60, portrait.X + portrait.Width / 2);
            Assert.AreEqual(92, portrait.X + portrait.Width);
            Assert.AreEqual(65, portrait.Y + portrait.Height / 2);
            Assert.AreEqual(flip, portrait.FlipHorizontal);
            var icon = SummaryPresentation.PortraitPlacement(true, flip);
            Assert.AreEqual(8, icon.X);
            Assert.AreEqual(24, icon.X + icon.Width / 2);
            Assert.AreEqual(32, icon.Y + icon.Height / 2);
        }
        Assert.AreEqual(28, SummaryPresentation.PortraitPlacement(false, true, -5).Y);
    }
    [TestMethod]
    public void SummaryFacingUsesSpeciesSourceFlag()
    {
        Assert.IsTrue(Art.Value.FlipPortrait("pret:SPECIES_BULBASAUR"));
        Assert.IsFalse(Art.Value.FlipPortrait("pret:SPECIES_UNOWN"));
    }
    [TestMethod]
    public void EggMotionHasSourceDelaysAndTwoCycleLimit()
    {
        Assert.AreEqual(0, Art.Value.PortraitOffset(59,1,1,true,5,false));
        Assert.AreEqual(2, Art.Value.PortraitOffset(60,1,1,true,5,false));
        Assert.AreEqual(0, Art.Value.PortraitOffset(89,1,1,true,10,false));
        Assert.AreEqual(2, Art.Value.PortraitOffset(90,1,1,true,10,false));
        Assert.AreEqual(1, Art.Value.PortraitOffset(120,1,1,true,40,false));
        Assert.AreEqual(0, Art.Value.PortraitOffset(1000,1,1,true,5,false));
    }
}
