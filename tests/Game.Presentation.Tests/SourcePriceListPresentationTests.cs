namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Presentation")]
public sealed class SourcePriceListPresentationTests
{
    [TestMethod]
    public void PriceControlsAreGenericAndTheSourceSevenRowListScrollsToExit()
    {
        var price=SourceMenuLabelPresentation.Parse("CUSTOM{CLEAR_TO 0x74}{FONT_SMALL}50");
        Assert.AreEqual("CUSTOM",price.Label);Assert.AreEqual(116,price.SuffixX);Assert.AreEqual("50",price.Suffix);
        Assert.AreEqual(new SourceMenuLabelPresentation("EXIT",null,null),SourceMenuLabelPresentation.Parse("EXIT"));
        Assert.Throws<InvalidDataException>(()=>SourceMenuLabelPresentation.Parse("CUSTOM{COLOR RED}"));
        var list=new SourceVerticalMenuPresentation(Enumerable.Range(0,12).Select(i=>i.ToString()),16,1,visibleRows:7,heightTiles:12);
        for(int i=0;i<11;i++)list.Move(1);
        Assert.AreEqual(11,list.Cursor);Assert.AreEqual(5,list.ScrollOffset);Assert.IsFalse(list.CanScrollDown);
        var frame=list.Frame(130);Assert.IsTrue(frame.X+frame.Width<=240);Assert.AreEqual(112,frame.Height);
    }

    [TestMethod]
    [DataRow("gText_50Coins_1000", " 50 COINS", 0x45, "¥1,000")]
    [DataRow("gText_500Coins_10000", "500 COINS", 0x40, "¥10,000")]
    public void CoinRowsReuseSourceLayoutAndPermitChangedLabels(string symbol, string label, int offset, string price)
    {
        var source = TestAssets.Cached(() => Game.Content.PretBattleText.Load(TestAssets.Root));
        var row = SourceMenuLabelPresentation.Parse(source[symbol]);
        Assert.AreEqual(new SourceMenuLabelPresentation(label, offset, price), row);
        var changed = SourceMenuLabelPresentation.Parse(source[symbol].Replace(label, "LOCAL", StringComparison.Ordinal));
        Assert.AreEqual(new SourceMenuLabelPresentation("LOCAL", offset, price), changed);
    }

    [TestMethod]
    [DataRow("{FONT_SMALL}COINS{CLEAR_TO 0x45}{UNKNOWN}50")]
    [DataRow("{FONT_SMALL}COINS{CLEAR_TO 45}50")]
    [DataRow("{FONT_SMALL}COINS{CLEAR_TO 0x45}{FONT_SMALL}50")]
    [DataRow("{FONT_SMALL}COINS{CLEAR_TO 0x45}")]
    public void CoinRowsDoNotDiscardUnsupportedOrIncompleteControls(string value) =>
        Assert.ThrowsExactly<InvalidDataException>(() => SourceMenuLabelPresentation.Parse(value));

}
