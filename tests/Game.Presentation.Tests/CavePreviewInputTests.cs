using Game.Application;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class CavePreviewInputTests
{
    [TestMethod]
    [DataRow("overlap",57)]
    [DataRow("release",97)]
    [DataRow("disconnect",97)]
    [DataRow("focus",97)]
    public void ExistingPhysicalOwnersDriveHeldBWithoutInventingPresses(string action,int expectedFrames)
    {
        var buttons=new CampaignButtonInput();
        buttons.Observe(-1,8,CampaignButton.B,true);
        buttons.Observe(2,1,CampaignButton.B,true);
        var palette=new ushort[16];var white=Enumerable.Repeat((ushort)32767,16).ToArray();
        var preview=new FieldCaveTransitionPresentation.CavePreview(palette,white,palette,true);
        // Pre-held input survives acquiring the transition, and an early release
        // is observed before the hold phase, rather than queued as a skip edge.
        preview.Advance(true,true,(buttons.Held&CampaignButton.B)!=0);
        buttons.Observe(-1,8,CampaignButton.B,false);
        switch(action)
        {
            case "release":buttons.Observe(2,1,CampaignButton.B,false);break;
            case "disconnect":buttons.Disconnect(2);break;
            case "focus":buttons.Reset();break;
        }
        buttons.Observe(-1,13,CampaignButton.A,true);
        for(int frame=1;frame<200&&!preview.Complete;frame++)
            preview.Advance(true,true,(buttons.Held&CampaignButton.B)!=0);
        Assert.IsTrue(preview.Complete);
        Assert.AreEqual(expectedFrames,preview.Frames);
        buttons.Reset();Assert.AreEqual(CampaignButton.None,buttons.Held);
    }
}
