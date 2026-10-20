using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class MoltresCollectionTests
{
    private static readonly SpeciesId Moltres = new("pret:SPECIES_MOLTRES");
    private sealed record Inputs(PretCanonicalPokemonCatalog Canonical, CampaignPokemonContent Content,
        SharedMachineCatalog Machines, PretPokedexMenu Dex);
    private static readonly Lazy<Inputs> Source = new(() =>
    {
        var canonical = PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root);
        var mechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(canonical, TestAssets.Root);
        var machines = SharedMachineCatalog.LoadProject(TestAssets.Root);
        var game = PretGamePokemonCatalog.Load(TestAssets.Root, canonical, mechanics,
            PretNpcTrades.Load(PretCurrentWorld.Load(TestAssets.Root), mechanics.Catalog).Trades, machines);
        var dex = PretPokedexMenu.Load(TestAssets.Root);
        var content = new CampaignPokemonContent(game);
        return new(canonical, content, machines, dex);
    });

    [TestMethod]
    public void OwnedMoltresUsesCanonicalMetadataAndLevelFiftyMoves()
    {
        var source = Source.Value;
        var definition = source.Content.Catalog.Species[Moltres];
        Assert.AreSame(source.Canonical.Catalog.Species[Moltres], definition);
        Assert.AreSame(definition, source.Content.Pokemon.Opponents.Catalog.Species[Moltres]);
        Assert.IsFalse(source.Content.Pokemon.TrainerTowerOpponents.Catalog.Species.ContainsKey(Moltres));
        Assert.AreEqual(146, definition.SourceSpeciesId);
        Assert.AreEqual(new SixStats(90, 100, 90, 90, 125, 85), definition.BaseStats);
        Assert.AreEqual(3, definition.CatchRate);
        Assert.AreEqual(GrowthRate.Slow, definition.GrowthRate);
        Assert.AreEqual("ABILITY_PRESSURE", definition.Ability1);
        Assert.AreEqual("ABILITY_NONE", definition.Ability2);
        Assert.AreEqual("TYPE_FIRE", definition.Type1);
        Assert.AreEqual("TYPE_FLYING", definition.Type2);
        Assert.IsTrue(definition.Evolutions.IsEmpty);
        var mon = Make(Moltres, 1, 50);
        Assert.IsNull(mon.CustomNickname);
        Assert.AreEqual("MOLTRES", source.Dex.Entries[mon.Pokemon.SourceSpeciesId].Name);
        Assert.AreEqual(PokemonGender.Genderless, mon.Pokemon.Gender);
        Assert.AreEqual(156250u, mon.Pokemon.Experience);
        Assert.AreEqual(35, mon.Pokemon.Friendship);
        CollectionAssert.AreEqual(new[] { "FIRE_SPIN", "AGILITY", "ENDURE", "FLAMETHROWER" },
            mon.Pokemon.Moves.Select(slot => slot.Move.Value[10..]).ToArray());
        Assert.IsTrue(mon.Pokemon.Moves.All(slot => slot.CurrentPp == slot.MaximumPp &&
            slot.MaximumPp == source.Content.Catalog.Moves[slot.Move].Pp));
        Assert.IsNull(PokemonStateValidator.ValidateParty(source.Content, mon));
        foreach (var learned in definition.LevelUpMoves)
            Assert.IsTrue(PokemonMoveRules.IsLearningSupported(source.Content.Catalog, learned.Move), learned.Move.Value);
    }

    [TestMethod]
    public void MoltresUsesSharedMachineTeachingGrowthAndStorage()
    {
        var source = Source.Value;
        var content = source.Content;
        var machines = source.Machines.LoadCurrent(content.Pokemon);
        foreach (var symbol in new[] { "TM35", "TM38", "TM50", "HM02", "HM06" })
            Assert.IsTrue(source.Machines.Compatibility(new("pret:ITEM_" + symbol), Moltres).CanLearn, symbol);
        foreach (var symbol in new[] { "TM13", "TM24", "HM01", "HM03", "HM04", "HM05", "HM07", "HM08" })
            Assert.IsFalse(source.Machines.Compatibility(new("pret:ITEM_" + symbol), Moltres).CanLearn, symbol);
        foreach (var machine in machines.Machines.Where(machine => machine.Compatibility.Single(row => row.Species == Moltres).CanLearn))
            Assert.IsTrue(PokemonMoveRules.IsLearningSupported(content.Catalog, machine.Move), machine.Move.Value);
        var fly = new ItemId("pret:ITEM_HM02");
        var inventory = MachineRules.AddMachine(machines, new([], [], OpeningInventoryRules.SourceLimits), fly, 1);
        Assert.IsTrue(inventory.Succeeded, inventory.Error);
        var taught = MachineRules.TeachMachine(machines, content.Pokemon, inventory.State, Make(Moltres, 2, 50), fly,
            "MAPSEC_MT_EMBER", 0);
        Assert.IsTrue(taught.Succeeded, taught.Error);
        Assert.AreEqual(new MoveId("pret:MOVE_FLY"), taught.Pokemon.Pokemon.Moves[0].Move);
        Assert.AreEqual(1, OpeningInventoryRules.Quantity(taught.Inventory.Machines ?? [], fly));
        Assert.IsNull(PokemonStateValidator.ValidateParty(content, taught.Pokemon));
        var raised = RareCandyRules.Apply(content, Make(Moltres, 3, 60), "MAPSEC_MT_EMBER");
        Assert.AreEqual(RareCandyOutcome.LevelRaised, raised.Outcome);
        Assert.AreEqual(61, raised.Pokemon.Pokemon.Level);
        Assert.AreEqual(new MoveId("pret:MOVE_SAFEGUARD"), raised.PendingMoves.Single().Move);
        Assert.IsNull(raised.Evolution);
        var stored = PokemonStorageRules.Deposit(content, [Make(Moltres, 1, 50), taught.Pokemon], PokemonStorageState.Empty, 1, 0);
        Assert.IsTrue(stored.Succeeded);
        var withdrawn = PokemonStorageRules.Withdraw(content, stored.Party, stored.Storage, 0, 0);
        Assert.IsTrue(withdrawn.Succeeded);
        Assert.AreEqual(JsonSerializer.Serialize(taught.Pokemon), JsonSerializer.Serialize(withdrawn.Party[1]));
        Assert.IsNull(PokemonStorageValidator.Validate(content, withdrawn.Party, withdrawn.Storage, 3));
    }

    [TestMethod]
    public void UndiscoveredEggGroupPreventsMoltresBreedingWithDitto()
    {
        var content = Source.Value.Content;
        var breeding = content.Catalog.BreedingData!;
        var metadata = breeding.Species[Moltres];
        Assert.AreEqual(new SourceBreedingSpecies(Moltres, 15, 15, 80), metadata.Breeding);
        Assert.AreEqual(Moltres, metadata.BaseEggSpecies);
        Assert.IsTrue(metadata.EggMoves.IsEmpty);
        var daycare = new FourIslandDayCareState(new(Make(Moltres, 1, 50), 0),
            new(Make(new("pret:SPECIES_DITTO"), 2, 50), 0), 0, 0);
        Assert.AreEqual(0, FourIslandDayCareRules.Compatibility(daycare,
            breeding.Species.ToDictionary(row => row.Key, row => row.Value.Breeding)));
    }

    [TestMethod]
    public void MoltresCollectionIncludesBothBattlePalettesAndCaughtDexPresentation()
    {
        var source = Source.Value;
        var art = PretPokemonCollectionPresentation.Load(TestAssets.Root, source.Content.Pokemon, source.Dex);
        var entry = art.Entries.Single(row => row.Species == Moltres);
        Assert.AreEqual(146, entry.SourceSpeciesId);
        Assert.AreEqual((32, 32), (entry.Icon.Width, entry.Icon.Height));
        Assert.AreEqual((16, 16), (entry.Footprint.Width, entry.Footprint.Height));
        Assert.AreEqual((64, 64), (entry.Portrait.Width, entry.Portrait.Height));
        var sprites = art.Sprites.Where(row => row.Species == Moltres).ToArray();
        Assert.AreEqual(4, sprites.Length);
        foreach (var side in new[] { "front", "back" })
            foreach (var shiny in new[] { false, true })
            {
                var sprite = sprites.Single(row => row.Side == side && row.Shiny == shiny);
                Assert.AreEqual(side == "front" ? (144, 9) : (40, 52), (sprite.X, sprite.Y));
                Assert.IsTrue(art.Textures.ContainsKey(sprite.Texture.TexturePath));
            }
        Assert.AreEqual(entry.Portrait, sprites.Single(row => row.Side == "front" && !row.Shiny).Texture);
        Assert.AreEqual("FLAME", art.CaptureEntries[146].Category);
        Assert.AreEqual(entry.Footprint.TexturePath, art.CaptureEntries[146].FootprintPath);
        var access = new PokedexMenuAccess(true, true, 0xb9, 0x6258);
        var seen = art.Present(source.Dex.BuildPage(146, [146], [], access));
        Assert.AreEqual(entry.Portrait, seen.Portrait);
        Assert.IsNull(seen.Footprint);
        Assert.AreEqual(string.Empty, seen.Description);
        var caught = art.Present(source.Dex.BuildPage(146, [146], [146], access));
        Assert.AreEqual(entry.Footprint, caught.Footprint);
        Assert.AreEqual(source.Dex.Entries[146].Description, caught.Description);
    }

    private static PartyPokemon Make(SpeciesId species, long id, int level)
    {
        var content = Source.Value.Content;
        var pokemon = PokemonFactory.Create(content.Catalog, new(species, level, 42,
            IvPolicy.Fixed(15), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        return new(id, pokemon, null, "RED", 0, 2,
            "MAPSEC_MT_EMBER", level, 4, "ITEM_ULTRA_BALL", "ITEM_NONE");
    }
}
