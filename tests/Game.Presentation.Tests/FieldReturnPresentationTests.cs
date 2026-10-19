using Game.Application;
using System.Text.Json;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FieldReturnPresentationTests
{
    [TestMethod]
    [DataRow(true,"menuReturn",FieldArrivalPalette.FogHorizontal,20)]
    [DataRow(true,"normalMenuReturn",FieldArrivalPalette.Normal,25)]
    [DataRow(true,"shadeMenuReturn",FieldArrivalPalette.Shade,19)]
    [DataRow(false,"scriptReturn",FieldArrivalPalette.FogHorizontal,19)]
    [DataRow(false,"normalScriptReturn",FieldArrivalPalette.Normal,24)]
    [DataRow(false,"shadeScriptReturn",FieldArrivalPalette.Shade,18)]
    public void EverySourceCallbackMatchesWeatherReadinessInputAndFogPhase(bool menu,string traceName,FieldArrivalPalette palette,int completion)
    {
        const string fixture="tests/Game.Presentation.Tests/Fixtures/field-return-source-vectors.json";
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(fixture,
            "python tests/Reference/generate_field_return_vectors.py --reference content/raw/pokefirered/037335f4 --output "+fixture)));
        var trace=source.RootElement.GetProperty("traces").EnumerateArray().Single(t=>t.GetProperty("name").GetString()==traceName);
        var controller=new FieldReturnPresentation.Controller(menu,palette);
        var fog=new FieldHorizontalFogPresentation.Scene(0,0);
        int compared=0;
        foreach(var item in trace.GetProperty("frames").EnumerateArray())
        {
            int[] row=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            if(row[0]>0){controller.Advance();fog=fog.Advance(1,0,0);}
            Assert.AreEqual(row[3],controller.Palette.BackgroundBlend);
            Assert.AreEqual(row[12],controller.Palette.ObjectBlend);
            Assert.AreEqual(row[2]==3,controller.Palette.WeatherReady);
            Assert.AreEqual(menu?row[7]!=0:row[8]!=0,controller.Complete);
            if(row[0]>0&&palette==FieldArrivalPalette.FogHorizontal)
            {
                Assert.AreEqual(row[9],fog.Sample.ScrollOffset);
                Assert.AreEqual(row[10],fog.Sample.ScrollCounter);
                Assert.AreEqual(row[11],fog.Sample.PositionX);
            }
            compared++;
            if(controller.Complete)break;
        }
        Assert.AreEqual(completion+1,compared);
    }

    [TestMethod]
    [DataRow("start","bag","menu_back",true,null,true)]
    [DataRow("closed","bag","menu_back",true,null,false)]
    [DataRow("start","battle","menu_back",true,null,false)]
    [DataRow("start","bag","field_escape_rope",true,null,false)]
    [DataRow("start","bag","menu_back",false,null,false)]
    [DataRow("start","bag","menu_back",true,"bag",false)]
    public void BagExitUsesItsOpeningContextAndAcceptedCancel(string mode,string origin,string command,bool accepted,string? after,bool expected)
    {
        var navigation=new FieldReturnPresentation.BagNavigation();
        Assert.IsFalse(navigation.Observe("bag_open",true,mode,null,origin));
        Assert.AreEqual(expected,navigation.Observe(command,accepted,"closed",origin,after));
    }

    [TestMethod]
    public void StartAndSaveOverlayDoNotDetachWhileNestedMenusHaveOneReturn()
    {
        var lifecycle=new FieldReturnPresentation();
        Assert.IsFalse(lifecycle.Observe(FieldReturnPresentation.Owner.Field,FieldArrivalPalette.FogHorizontal));
        Assert.IsFalse(lifecycle.Observe(FieldReturnPresentation.Owner.StartMenu,FieldArrivalPalette.FogHorizontal));
        Assert.IsFalse(lifecycle.Suspended);
        Assert.IsFalse(lifecycle.Observe(FieldReturnPresentation.Owner.FullScreenMenu,FieldArrivalPalette.FogHorizontal));
        Assert.IsTrue(lifecycle.Suspended);
        Assert.IsFalse(lifecycle.Observe(FieldReturnPresentation.Owner.FullScreenMenu,FieldArrivalPalette.FogHorizontal));
        Assert.IsTrue(lifecycle.Observe(FieldReturnPresentation.Owner.StartMenu,FieldArrivalPalette.FogHorizontal));
        var arrival=lifecycle.Arrival;
        Assert.IsFalse(lifecycle.Observe(FieldReturnPresentation.Owner.StartMenu,FieldArrivalPalette.FogHorizontal));
        Assert.AreSame(arrival,lifecycle.Arrival);
        Assert.IsTrue(arrival!.StartMenu);
    }

    [TestMethod]
    [DataRow(FieldArrivalPalette.Normal,24)]
    [DataRow(FieldArrivalPalette.Shade,18)]
    [DataRow(FieldArrivalPalette.FogHorizontal,19)]
    public void BattleMenusRemainOneAbsenceAndReturnToFieldUsesScriptPriority(FieldArrivalPalette palette,int ready)
    {
        var lifecycle=new FieldReturnPresentation();
        lifecycle.Observe(FieldReturnPresentation.Owner.Battle,palette);
        lifecycle.Observe(FieldReturnPresentation.Owner.Battle,palette);
        Assert.IsTrue(lifecycle.Observe(FieldReturnPresentation.Owner.Field,palette));
        Assert.IsFalse(lifecycle.Arrival!.StartMenu);
        for(int frame=1;frame<=ready;frame++)
        {
            lifecycle.Arrival.Advance();
            Assert.AreEqual(frame==ready,lifecycle.Arrival.Complete);
        }
        lifecycle.Reset();Assert.IsNull(lifecycle.Arrival);Assert.IsFalse(lifecycle.Suspended);
    }

    [TestMethod]
    public void MenuInputWaitsForWeatherAndBothMenuTaskCallbacks()
    {
        var arrival=new FieldReturnPresentation.Controller(true);
        for(int frame=1;frame<=20;frame++)
        {
            arrival.Advance();
            Assert.AreEqual(frame>=18,arrival.Palette.WeatherReady,$"weather {frame}");
            Assert.AreEqual(frame==20,arrival.Complete,$"input {frame}");
            Assert.AreEqual(frame<18?0:frame==18?1:2,arrival.MenuPhase);
        }
        arrival.Advance();Assert.AreEqual(20,arrival.Frames);
    }

    [TestMethod]
    public void SameMapReturnResetsFogClockButRetainsMapCameraOrigin()
    {
        var old=new FieldHorizontalFogPresentation.Scene(100,200).Advance(700,164,232);
        var resumed=new FieldHorizontalFogPresentation.Scene(old.AnchorCameraX,old.AnchorCameraY);
        resumed=resumed.Advance(1,164,232);
        Assert.AreEqual(0,resumed.SpriteCoordOffsetX);Assert.AreEqual(0,resumed.SpriteCoordOffsetY);
        resumed=resumed.Advance(1,164,232);
        Assert.AreEqual(-64,resumed.SpriteCoordOffsetX);Assert.AreEqual(-72,resumed.SpriteCoordOffsetY);
        Assert.AreEqual(1L,resumed.SourceElapsedFrames);
        Assert.AreEqual(699L,old.SourceElapsedFrames);
    }
}
