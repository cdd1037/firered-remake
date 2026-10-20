using Game.Application;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FullMenuReturnPresentationTests
{
    // Independently traced original palette.c/task callbacks, not product-generated goldens.
    [TestMethod]
    [DataRow(FieldReturnPresentation.MenuScreen.Bag,1,"0,4,4,8,8,12,12,16,16,16,16,16,16,16,16","0,0,4,4,8,8,12,12,16,16,16,16,16,16,16")]
    [DataRow(FieldReturnPresentation.MenuScreen.Party,0,"0,4,4,8,8,12,12,16,16,16,16,16,16,16,16","0,0,4,4,8,8,12,12,16,16,16,16,16,16,16")]
    [DataRow(FieldReturnPresentation.MenuScreen.Options,1,"0,2,2,4,4,6,6,8,8,10,10,12,12,14,14,16,16,16,16,16,16,16,16,16","0,0,2,2,4,4,6,6,8,8,10,10,12,12,14,14,16,16,16,16,16,16,16,16")]
    [DataRow(FieldReturnPresentation.MenuScreen.TrainerCard,0,"0,2,4,6,8,10,12,14,16,16,16,16","0,2,4,6,8,10,12,14,16,16,16,16")]
    [DataRow(FieldReturnPresentation.MenuScreen.Pokedex,2,"0,0,2,2,4,4,6,6,8,8,10,10,12,12,14,14,16,16,16,16,16,16,16,16,16","0,0,0,2,2,4,4,6,6,8,8,10,10,12,12,14,14,16,16,16,16,16,16,16,16")]
    public void OutgoingCallbacksRetainEachScreensOriginalCadence(FieldReturnPresentation.MenuScreen screen,int lead,string background,string objects)
    {
        var transition=new FieldReturnPresentation.MenuReturn(screen,5,FieldArrivalPalette.Normal);
        for(int i=0;i<lead;i++)
        {
            Assert.AreEqual(FieldReturnPresentation.MenuReturnPhase.InputCallback,transition.Phase);
            Assert.AreEqual(0,transition.BackgroundBlend);
            Assert.AreEqual(0,transition.BagBlackWipeHeight);
            Assert.IsFalse(transition.Complete);
            transition.Advance();
        }
        int[] bg=background.Split(',').Select(int.Parse).ToArray();
        int[] obj=objects.Split(',').Select(int.Parse).ToArray();
        Assert.AreEqual(bg.Length,obj.Length);
        for(int f=0;f<bg.Length;f++)
        {
            Assert.AreEqual(f,transition.DepartureFrame);
            Assert.AreEqual(bg[f],transition.BackgroundBlend,$"{screen} BG F{f}");
            Assert.AreEqual(obj[f],transition.ObjectBlend,$"{screen} OBJ F{f}");
            Assert.AreEqual(screen==FieldReturnPresentation.MenuScreen.Bag?Math.Min(160,16*(f+1)):0,transition.BagBlackWipeHeight);
            Assert.AreEqual(f==bg.Length-1?FieldReturnPresentation.MenuReturnPhase.ReturnCallback:FieldReturnPresentation.MenuReturnPhase.Departure,transition.Phase);
            Assert.IsNull(transition.Arrival);
            Assert.IsFalse(transition.Complete);
            transition.Advance();
        }
        // Selecting the return callback must not invoke R0 in that same callback.
        Assert.AreEqual(0,transition.ReturnFrame);
        Assert.AreEqual(FieldReturnPresentation.MenuReturnPhase.BlackSetup,transition.Phase);
    }

    [TestMethod]
    [DataRow(5,FieldArrivalPalette.Normal,12,38)]
    [DataRow(6,FieldArrivalPalette.Normal,12,38)]
    [DataRow(7,FieldArrivalPalette.Normal,13,39)]
    [DataRow(8,FieldArrivalPalette.Normal,13,39)]
    [DataRow(5,FieldArrivalPalette.Shade,12,32)]
    [DataRow(7,FieldArrivalPalette.Shade,13,33)]
    [DataRow(5,FieldArrivalPalette.FogHorizontal,12,33)]
    [DataRow(7,FieldArrivalPalette.FogHorizontal,13,34)]
    public void BlackSetupPrecedesWeatherAndInputUnlock(int entries,FieldArrivalPalette weather,int fadeRequest,int input)
    {
        var transition=new FieldReturnPresentation.MenuReturn(FieldReturnPresentation.MenuScreen.Bag,entries,weather);
        for(int i=0;i<100&&transition.ReturnFrame<0;i++)transition.Advance();
        Assert.AreEqual(0,transition.ReturnFrame);
        Assert.AreEqual(fadeRequest,transition.FadeRequestReturnFrame);
        for(int r=0;r<=input;r++)
        {
            Assert.AreEqual(r,transition.ReturnFrame);
            Assert.IsFalse(transition.ShowsDepartingMenu);
            Assert.AreEqual(r==input,transition.Complete);
            if(r<fadeRequest)Assert.IsNull(transition.Arrival);
            else
            {
                Assert.IsNotNull(transition.Arrival);
                Assert.AreEqual(Math.Max(0,r-fadeRequest-1),transition.Arrival.Frames);
            }
            if(r<=fadeRequest+1)
            {
                Assert.AreEqual(16,transition.BackgroundBlend);
                Assert.AreEqual(16,transition.ObjectBlend);
            }
            if(r<input)transition.Advance();
        }
        int frames=transition.Frames;
        transition.Advance();
        Assert.AreEqual(frames,transition.Frames);
    }

    [TestMethod]
    public void OrdinarySaveAndStartOwnersDoNotScheduleFullscreenReturn()
    {
        var owner=new FieldReturnPresentation();
        Assert.IsFalse(owner.Observe(FieldReturnPresentation.Owner.Field,FieldArrivalPalette.Normal));
        Assert.IsFalse(owner.Observe(FieldReturnPresentation.Owner.StartMenu,FieldArrivalPalette.Normal));
        Assert.IsFalse(owner.Observe(FieldReturnPresentation.Owner.Field,FieldArrivalPalette.Normal));
        Assert.IsNull(owner.Arrival);
    }
}
