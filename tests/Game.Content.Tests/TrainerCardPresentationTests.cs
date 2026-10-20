using Game.Content;
namespace FireRed.Tests.Content;

[TestClass,TestCategory("Game")]
public sealed class TrainerCardPresentationTests
{
    [TestMethod]
    public void AllSupportedStarColorsAndEarnedStickerTiersHaveSourceAssets()
    {
        var card=PretTrainerCard.Load(TestAssets.Root);
        for(int stars=0;stars<=3;stars++)for(int gender=0;gender<2;gender++)foreach(bool back in new[]{false,true})
        {
            string id=card.BackgroundId(back,gender,stars);Assert.AreEqual(240,card.Asset(id).Width);Assert.AreEqual(160,card.Asset(id).Height);
            Assert.IsTrue(card.ReadTexturePage(card.Asset(id).TexturePath).Length>100);
        }
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>card.BackgroundId(false,0,4));
        for(int kind=0;kind<2;kind++)for(int tier=1;tier<=4;tier++)Assert.AreEqual(16,card.Asset($"sticker-{kind}-{tier}").Width);
        Assert.IsFalse(card.Assets.Any(a=>a.Id.StartsWith("sticker-2-",StringComparison.Ordinal)));
        Assert.AreEqual(120,card.Rules.PhotoY);Assert.AreEqual(24,card.Rules.PhotoX);Assert.AreEqual(32,card.Rules.PhotoSpacing);
        Assert.AreEqual(" 12:34:56",PretTrainerCard.FormatHallOfFameTime((12<<16)|(34<<8)|56));
        Assert.AreEqual("999:59:59",PretTrainerCard.FormatHallOfFameTime((999<<16)|(59<<8)|59));
    }
    [TestMethod]
    public void PhotoPaletteTintsUseSourceFiveBitFixedPointMath()
    {
        Assert.AreEqual(((byte)255,(byte)255,(byte)255),PretTrainerCard.TintPhoto(255,255,255,0));
        Assert.AreEqual(((byte)0,(byte)0,(byte)0),PretTrainerCard.TintPhoto(255,255,255,1));
        Assert.AreEqual(((byte)255,(byte)255,(byte)255),PretTrainerCard.TintPhoto(255,255,255,2));
        Assert.AreEqual(((byte)255,(byte)255,(byte)239),PretTrainerCard.TintPhoto(255,255,255,3));
        Assert.AreEqual(((byte)140,(byte)90,(byte)82),PretTrainerCard.TintPhoto(255,0,0,2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>PretTrainerCard.TintPhoto(1,2,3,4));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DrawnCardFieldsUseSharedSourceBankAndPreserveMoneyAndIdRules(bool translated)
    {
        using var directory=TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(directory.Path,"source"));
        File.WriteAllText(Path.Combine(directory.Path,"source/battle.json"),translated ? """
            {"gText_TrainerCardName":"PLAYER: ","gText_TrainerCardIDNo":"ID#",
             "gText_TrainerCardMoney":"FUNDS","gText_TrainerCardYen":"CR ",
             "gText_TrainerCardPokedex":"DEX","gText_TrainerCardTime":"ELAPSED",
             "gText_Colon2":"·","gText_HallOfFameDebut":"FIRST VICTORY  "}
            """ : "{}");
        var source=PretBattleText.Load(TestAssets.Root,SourceTextPack.Load(directory.Path));
        var card=PretTrainerCard.Load(TestAssets.Root,source);
        CollectionAssert.AreEqual(translated
                ? new[]{"PLAYER: ","ID#","FUNDS","CR ","DEX","ELAPSED","·","FIRST VICTORY  "}
                : new[]{"NAME: ","IDNo.","MONEY","¥","POKéDEX","TIME",":","HALL OF FAME DEBUT  "},
            new[]{card.Texts.Name,card.Texts.Id,card.Texts.Money,card.Texts.Currency,card.Texts.Pokedex,
                card.Texts.Time,card.Texts.Colon,card.Texts.HallOfFame});
        Assert.AreEqual((translated?"CR ":"¥")+"12345",card.FormatMoney(12345));
        Assert.AreEqual(card.Layout.MoneyRightX-card.Layout.DecimalCellWidth*card.FormatMoney(12345).Length,
            card.MoneyValuePosition(12345).X);
        Assert.AreEqual("00001",PretTrainerCard.FormatTrainerId(0x10001));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>card.FormatMoney((uint)card.Rules.MaxMoney+1));
        Assert.AreEqual(1,PretTrainerCard.CountCaught([1,1,152],nationalEnabled:false));
        Assert.AreEqual(2,PretTrainerCard.CountCaught([1,1,152],nationalEnabled:true));
        Assert.AreEqual("front-female-3",card.BackgroundId(false,1,3));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("{UNKNOWN}")]
    [DataRow("NAME\\nSECOND")]
    public void CardLabelsRejectMissingOrUnsupportedSourceText(string? value)
    {
        var source=PretBattleText.Load(TestAssets.Root,SourceTextPack.Load(null))
            .ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        if(value is null)source.Remove("gText_TrainerCardName");else source["gText_TrainerCardName"]=value;
        Assert.ThrowsExactly<InvalidDataException>(()=>PretTrainerCard.Load(TestAssets.Root,source));
    }
}
