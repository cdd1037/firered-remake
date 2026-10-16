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
}
