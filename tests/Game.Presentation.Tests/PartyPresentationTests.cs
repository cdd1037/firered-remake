using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class PartyPresentationTests
{
    [TestMethod]
    public void SourceSingleLayoutHasNonoverlappingOccupiedHitRegions()
    {
        for(int a=0;a<6;a++)for(int b=0;b<6;b++)
        {
            var r=PartyPresentationAssets.Slot(a);var other=PartyPresentationAssets.Slot(b);
            Assert.AreEqual(a==b,other.Contains(r.X+r.Width/2,r.Y+r.Height/2));
        }
        Assert.IsFalse(PartyPresentationAssets.Slot(0).Contains(90,40));
        Assert.IsTrue(PartyPresentationAssets.Cancel.Contains(200,140));
        Assert.IsFalse(PartyPresentationAssets.Cancel.Contains(20,140));
    }
    [TestMethod]
    public void LeftReturnsToLeadAndRightRemembersRealSlot()
    {
        int last=1;
        Assert.AreEqual(0,PartyPresentationAssets.Move(4,6,true,-1,0,ref last));
        Assert.AreEqual(4,last);
        Assert.AreEqual(4,PartyPresentationAssets.Move(0,6,true,1,0,ref last));
        Assert.AreEqual(1,PartyPresentationAssets.Move(0,2,true,1,0,ref last));
        Assert.AreEqual(0,PartyPresentationAssets.Move(0,1,true,1,0,ref last));
    }
    [TestMethod]
    public void UpDownWrapThroughCancelWithoutInventingEmptySlots()
    {
        for(int count=1;count<=6;count++)
        {
            int last=1;
            Assert.AreEqual(count,PartyPresentationAssets.Move(0,count,true,0,-1,ref last));
            Assert.AreEqual(0,PartyPresentationAssets.Move(count,count,true,0,1,ref last));
            Assert.AreEqual(0,PartyPresentationAssets.Move(count-1,count,false,0,1,ref last));
        }
    }
}
