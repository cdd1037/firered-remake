using System.Collections.Immutable;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class PartyPresentationTests
{
    private static readonly Game.Content.PresentationTextCatalog Text = Game.Content.PresentationTextCatalog.English("field-menu");
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

    [TestMethod]
    public void FieldActionsKeepCanonicalEnglishNamesAndMoveSlotOrder()
    {
        var names = FireRed.Tests.TestAssets.Cached(() => Game.Content.PretMetronomeMoveCatalog.Load(FireRed.Tests.TestAssets.Root)).MoveNames;
        (string Id, string Label)[] expected =
        [
            ("pret:MOVE_CUT", "CUT"), ("pret:MOVE_FLY", "FLY"), ("pret:MOVE_FLASH", "FLASH"),
            ("pret:MOVE_STRENGTH", "STRENGTH"), ("pret:MOVE_SURF", "SURF"), ("pret:MOVE_WATERFALL", "WATERFALL"),
            ("pret:MOVE_ROCK_SMASH", "ROCK SMASH"), ("pret:MOVE_DIG", "DIG"), ("pret:MOVE_TELEPORT", "TELEPORT"),
            ("pret:MOVE_SWEET_SCENT", "SWEET SCENT"), ("pret:MOVE_SOFT_BOILED", "SOFTBOILED"), ("pret:MOVE_MILK_DRINK", "MILK DRINK")
        ];
        foreach (var slots in expected.Chunk(4))
        {
            var actions = CampaignFieldPartyActions.Build(Text, FieldMember(slots.Select(x => x.Id).ToArray()), id => names[id], true, true, true);
            CollectionAssert.AreEqual(new[] { "summary" }.Concat(slots.Select(x => x.Id)).Concat(["switch", "item", "cancel"]).ToArray(),
                actions.Select(x => x.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "SUMMARY" }.Concat(slots.Select(x => x.Label)).Concat(["SWITCH", "ITEM", "CANCEL"]).ToArray(),
                actions.Select(x => x.Label).ToArray());
        }
    }

    [TestMethod]
    [DataRow(Game.Domain.World.NoncombatFieldMoveRules.Dig)]
    [DataRow(Game.Domain.World.NoncombatFieldMoveRules.Teleport)]
    [DataRow(Game.Domain.World.NoncombatFieldMoveRules.SweetScent)]
    [DataRow(Game.Domain.World.NoncombatFieldMoveRules.SoftBoiled)]
    [DataRow(Game.Domain.World.NoncombatFieldMoveRules.MilkDrink)]
    public void ChangedAndCollidingLabelsCannotChangeActionIdentity(string move)
    {
        var text = Game.Content.PresentationTextCatalog.Parse("""
            {"party.summary":"CANCEL","party.switch":"CANCEL","party.item":"CANCEL","party.mail":"CANCEL","party.cancel":"CANCEL"}
            """);
        var member = FieldMember(move, "pret:MOVE_CUT", move);
        var actions = CampaignFieldPartyActions.Build(text, member, _ => "CANCEL", true, true, true);
        CollectionAssert.AreEqual(new[] { "summary", move, "pret:MOVE_CUT", move, "switch", "item", "cancel" },
            actions.Select(x => x.Id).ToArray());
        Assert.IsTrue(actions.All(x => x.Label == "CANCEL"));
        Assert.IsTrue(Game.Domain.World.NoncombatFieldMoveRules.Knows(member, actions[1].Id));
        Assert.IsTrue(Game.Domain.World.NoncombatFieldMoveRules.Knows(member, actions[3].Id));
        Assert.IsFalse(Game.Domain.World.NoncombatFieldMoveRules.IsSupported(actions[1].Label));
    }

    [TestMethod]
    public void FieldActionsKeepIndependentCatalogGatesAndMailSelection()
    {
        var member = FieldMember("pret:MOVE_FLY", "pret:MOVE_ROCK_SMASH", "pret:MOVE_SURF", "pret:MOVE_DIG")
            with { HeldItem = "ITEM_ORANGE_MAIL" };
        foreach (bool fly in new[] { false, true })
        foreach (bool rockSmash in new[] { false, true })
        foreach (bool heldItems in new[] { false, true })
        {
            var expected = new List<string> { "summary" };
            if (fly) expected.Add("pret:MOVE_FLY");
            if (rockSmash) expected.Add("pret:MOVE_ROCK_SMASH");
            expected.AddRange(["pret:MOVE_SURF", "pret:MOVE_DIG", "switch"]);
            if (heldItems) expected.Add("mail");
            expected.Add("cancel");
            var actions = CampaignFieldPartyActions.Build(Text, member, id => id.Value, fly, rockSmash, heldItems);
            CollectionAssert.AreEqual(expected.ToArray(), actions.Select(x => x.Id).ToArray());
            if (heldItems) Assert.AreEqual("MAIL", actions[^2].Label);
        }
    }

    [TestMethod]
    public void FieldActionsKeepEggMissingMemberAndZeroHpPpBehavior()
    {
        var member = FieldMember("pret:MOVE_TACKLE", "pret:MOVE_DIG");
        member = member with { Pokemon = member.Pokemon with { CurrentHp = 0 } };
        var actions = CampaignFieldPartyActions.Build(Text, member, id => id.Value, true, true, true);
        CollectionAssert.AreEqual(new[] { "summary", "pret:MOVE_DIG", "switch", "item", "cancel" }, actions.Select(x => x.Id).ToArray());
        var egg = CampaignFieldPartyActions.Build(Text, member with { IsEgg = true }, _ => throw new InvalidOperationException("Egg move label lookup"), true, true, true);
        CollectionAssert.AreEqual(new[] { "summary", "switch", "cancel" }, egg.Select(x => x.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "SUMMARY", "SWITCH", "CANCEL" }, egg.Select(x => x.Label).ToArray());
        var missing = CampaignFieldPartyActions.Build(Text, null, _ => throw new InvalidOperationException("Missing member move label lookup"), true, true, true);
        CollectionAssert.AreEqual(new[] { "summary", "switch", "item", "cancel" }, missing.Select(x => x.Id).ToArray());
    }

    private static Game.Domain.Pokemon.PartyPokemon FieldMember(params string[] moves)
    {
        var pokemon = new Game.Domain.Pokemon.PokemonInstance(new("pret:SPECIES_PIKACHU"), 25, 20, 8000, 123, 456,
            Game.Domain.Rules.Nature.Hardy, Game.Domain.Pokemon.SixStats.Zero, Game.Domain.Pokemon.SixStats.Zero,
            new(50, 20, 20, 20, 20, 20), 50, Game.Domain.Pokemon.PokemonGender.Female, 0, "ABILITY_STATIC", 70,
            moves.Select(id => new Game.Domain.Pokemon.PokemonMoveSlot(new(id), 0, 10)).ToImmutableArray());
        return new(987, pokemon, "SPARK", "PLAYER", 0, 2, "MAP_PALLET_TOWN", 5, 4, "ITEM_POKE_BALL", "ITEM_NONE");
    }

}
