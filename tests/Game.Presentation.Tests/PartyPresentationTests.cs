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

    [TestMethod]
    public void DoubleLayoutUsesTwoLeadPanelsAndFourRightPanels()
    {
        Assert.AreEqual(new PartyPresentationAssets.Box(8,8,80,56),PartyPresentationAssets.Slot(0,NativePartyLayout.Double));
        Assert.AreEqual(new PartyPresentationAssets.Box(8,64,80,56),PartyPresentationAssets.Slot(1,NativePartyLayout.Double));
        for(int i=2;i<6;i++)Assert.AreEqual(new PartyPresentationAssets.Box(96,8+(i-2)*32,144,24),PartyPresentationAssets.Slot(i,NativePartyLayout.Double));
        for(int a=0;a<6;a++)for(int b=0;b<6;b++)
        {
            var box=PartyPresentationAssets.Slot(a,NativePartyLayout.Double);
            Assert.AreEqual(a==b,PartyPresentationAssets.Slot(b,NativePartyLayout.Double).Contains(box.X+box.Width/2,box.Y+box.Height/2));
        }
    }
    [TestMethod]
    public void DoubleColumnsRememberTheirCorrespondingRightMember()
    {
        int last=1;
        Assert.AreEqual(2,PartyPresentationAssets.Move(0,6,true,1,0,ref last,NativePartyLayout.Double));
        Assert.AreEqual(0,PartyPresentationAssets.Move(3,6,true,-1,0,ref last,NativePartyLayout.Double));
        Assert.AreEqual(3,PartyPresentationAssets.Move(0,6,true,1,0,ref last,NativePartyLayout.Double));
        Assert.AreEqual(1,PartyPresentationAssets.Move(5,6,true,-1,0,ref last,NativePartyLayout.Double));
        Assert.AreEqual(5,PartyPresentationAssets.Move(1,6,true,1,0,ref last,NativePartyLayout.Double));
        Assert.AreEqual(1,PartyPresentationAssets.Move(1,4,true,1,0,ref last,NativePartyLayout.Double));
        Assert.AreEqual(0,PartyPresentationAssets.Move(5,6,false,0,1,ref last,NativePartyLayout.Double));
    }
    [TestMethod]
    public void DisplayedHpAndStatusDoNotMutateOwnedIdentityOrPersistentBody()
    {
        var pokemon=new Game.Domain.Pokemon.PokemonInstance(new("pret:SPECIES_PIKACHU"),25,20,8000,123,456,
            Game.Domain.Rules.Nature.Hardy,Game.Domain.Pokemon.SixStats.Zero,Game.Domain.Pokemon.SixStats.Zero,
            new(50,20,20,20,20,20),50,Game.Domain.Pokemon.PokemonGender.Female,0,"ABILITY_STATIC",70,[]);
        var owned=new Game.Domain.Pokemon.PartyPokemon(987,pokemon,"SPARK","PLAYER",0,2,"MAP_PALLET_TOWN",5,4,"ITEM_POKE_BALL","ITEM_ORANGE_MAIL") { Mail=2,Status=8 };
        var shown=PartyPresentationAssets.Displayed(owned,pokemon with { CurrentHp=17 },0);
        Assert.AreEqual(50,owned.Pokemon.CurrentHp);Assert.AreEqual(8u,owned.Status);
        Assert.AreEqual(17,shown.Pokemon.CurrentHp);Assert.AreEqual(0u,shown.Status);
        Assert.AreEqual(owned,shown with { Pokemon=owned.Pokemon,Status=owned.Status });
    }
}
