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
            Assert.IsTrue(card.ReadAssetTexture(id).Length>100);
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
}
