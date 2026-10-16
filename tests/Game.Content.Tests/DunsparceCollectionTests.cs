using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Growth;
using Game.Domain.Battle;
using Game.Domain.Encounters;
using Game.Domain.World;
using Game.Domain.WildBattle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class DunsparceCollectionTests
{
    private static readonly SpeciesId Dunsparce = new("pret:SPECIES_DUNSPARCE");
    private sealed record Inputs(PretCanonicalPokemonCatalog Canonical, CampaignPokemonContent Content,
        SharedMachineCatalog Machines);
    private static readonly Lazy<Inputs> Source = new(() =>
    {
        var canonical = PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root);
        var mechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(canonical, TestAssets.Root);
        var machines = SharedMachineCatalog.LoadProject(TestAssets.Root);
        var game = PretGamePokemonCatalog.Load(TestAssets.Root, canonical, mechanics,
            PretNpcTrades.Load(TestAssets.Root, mechanics.Catalog).Trades, machines);
        var dex = PretPokedexMenu.Load(TestAssets.Root);
        var content = new CampaignPokemonContent(game, game.Catalog.Species.Values.ToImmutableSortedDictionary(
            species => species.SourceSpeciesId, species => dex.Entries[species.SourceSpeciesId].Name),
            PokemonStateValidator.NicknameAlphabet);
        return new(canonical, content, machines);
    });

    [TestMethod]
    public void OwnedDunsparceUsesCanonicalCaptureGrowthAndBreedingMetadata()
    {
        var source = Source.Value;
        var game = source.Content.Pokemon;
        var definition = game.Catalog.Species[Dunsparce];
        Assert.AreSame(source.Canonical.Catalog.Species[Dunsparce], definition);
        Assert.AreSame(definition, game.Opponents.Catalog.Species[Dunsparce]);
        Assert.AreSame(definition, game.TrainerTowerOpponents.Catalog.Species[Dunsparce]);
        Assert.AreEqual(206, definition.SourceSpeciesId);
        Assert.AreEqual("DUNSPARCE", source.Content.SpeciesNames[definition.SourceSpeciesId]);
        Assert.AreEqual(new SixStats(100, 70, 70, 45, 65, 65), definition.BaseStats);
        Assert.AreEqual(190, definition.CatchRate);
        Assert.AreEqual(GrowthRate.MediumFast, definition.GrowthRate);
        Assert.AreEqual("ABILITY_SERENE_GRACE", definition.Ability1);
        Assert.AreEqual("ABILITY_RUN_AWAY", definition.Ability2);
        Assert.IsTrue(definition.Evolutions.IsEmpty);
        Assert.IsTrue(source.Canonical.Metadata[Dunsparce].SourceEvolutions.IsEmpty);
        var breeding = game.Catalog.BreedingData!.Species[Dunsparce];
        Assert.AreEqual(new SourceBreedingSpecies(Dunsparce, 5, 5, 20), breeding.Breeding);
        Assert.AreEqual(Dunsparce, breeding.BaseEggSpecies);
        CollectionAssert.AreEqual(new[] { "BIDE", "ANCIENT_POWER", "ROCK_SLIDE", "BITE", "HEADBUTT", "ASTONISH", "CURSE" },
            breeding.EggMoves.Select(move => move.Value[10..]).ToArray());
    }

    [TestMethod]
    public void DunsparceCanBeRaisedTaughtAndStoredThroughSharedRules()
    {
        var source = Source.Value;
        var content = source.Content;
        var mon = Make(1, 10);
        Assert.IsNull(PokemonStateValidator.ValidateParty(content, mon));
        CollectionAssert.AreEqual(new[] { "pret:MOVE_RAGE", "pret:MOVE_DEFENSE_CURL" },
            mon.Pokemon.Moves.Select(slot => slot.Move.Value).ToArray());
        var raised = RareCandyRules.Apply(content, mon, "MAPSEC_THREE_ISLE_PORT");
        Assert.AreEqual(RareCandyOutcome.LevelRaised, raised.Outcome);
        Assert.AreEqual(11, raised.Pokemon.Pokemon.Level);
        Assert.AreEqual(1331u, raised.Pokemon.Pokemon.Experience);
        Assert.AreEqual(new MoveId("pret:MOVE_YAWN"), raised.LearnedMoves.Single().Move);
        Assert.IsNull(raised.Evolution);
        Assert.IsNull(PokemonStateValidator.ValidateParty(content, raised.Pokemon));

        var tutors = PretMoveTutors.Load(TestAssets.Root, content.Catalog).Catalog;
        var bodySlam = tutors.Definitions.Values.Single(tutor => tutor.Move.Value == "pret:MOVE_BODY_SLAM");
        var megaPunch = tutors.Definitions.Values.Single(tutor => tutor.Move.Value == "pret:MOVE_MEGA_PUNCH");
        Assert.AreEqual(MoveTutorEligibility.Able, MoveTutorRules.Eligibility(bodySlam, raised.Pokemon));
        Assert.AreEqual(MoveTutorEligibility.Incompatible, MoveTutorRules.Eligibility(megaPunch, raised.Pokemon));
        var taught = MoveTutorRules.Teach(tutors, content.Catalog, raised.Pokemon, bodySlam.Index);
        Assert.IsTrue(taught.Succeeded, taught.Error);
        Assert.AreEqual(bodySlam.Move, taught.Pokemon.Pokemon.Moves[^1].Move);
        Assert.IsTrue(source.Machines.Compatibility(new("pret:ITEM_TM28"), Dunsparce).CanLearn);
        Assert.IsTrue(source.Machines.Compatibility(new("pret:ITEM_HM04"), Dunsparce).CanLearn);
        Assert.IsFalse(source.Machines.Compatibility(new("pret:ITEM_HM03"), Dunsparce).CanLearn);

        var guard = Make(2, 5);
        var stored = PokemonStorageRules.Deposit(content, [guard, taught.Pokemon], PokemonStorageState.Empty, 1, 0);
        Assert.IsTrue(stored.Succeeded);
        var withdrawn = PokemonStorageRules.Withdraw(content, stored.Party, stored.Storage, 0, 0);
        Assert.IsTrue(withdrawn.Succeeded);
        Assert.AreEqual(JsonSerializer.Serialize(taught.Pokemon), JsonSerializer.Serialize(withdrawn.Party[1]));
        Assert.IsNull(PokemonStorageValidator.Validate(content, withdrawn.Party, withdrawn.Storage, 3));
    }

    [TestMethod]
    public void DunsparceEggInheritsEggAndMachineMovesAndHatchesNormally()
    {
        var content = Source.Value.Content;
        var female = Make(1, 30, 0);
        var male = Make(2, 30, 255);
        male = male with { Pokemon = male.Pokemon with { Moves = new[] { "ANCIENT_POWER", "THUNDERBOLT" }
            .Select(symbol => content.Catalog.Moves[new("pret:MOVE_" + symbol)])
            .Select(move => new PokemonMoveSlot(move.Id, move.Pp, move.Pp)).ToImmutableArray() } };
        Assert.AreEqual(PokemonGender.Female, female.Pokemon.Gender);
        Assert.AreEqual(PokemonGender.Male, male.Pokemon.Gender);
        var daycare = new FourIslandDayCareState(new(female, 0), new(male, 0), 123, 0);
        var received = SourceEggRules.Receive(content, content.Catalog.BreedingData!, daycare, 3,
            new(42, "RED", 0), "MAPSEC_FOUR_ISLAND", 4, new(0));
        var egg = received.Egg;
        Assert.AreEqual(Dunsparce, egg.Pokemon.Species);
        Assert.IsTrue(egg.IsEgg);
        Assert.AreEqual(20, egg.Pokemon.Friendship);
        Assert.IsNull(PokemonStateValidator.ValidateParty(content, egg));
        CollectionAssert.AreEqual(new[] { "RAGE", "DEFENSE_CURL", "ANCIENT_POWER", "THUNDERBOLT" },
            egg.Pokemon.Moves.Select(slot => slot.Move.Value[10..]).ToArray());
        var hatched = SourceEggRules.Hatch(content, egg, new(42, "RED", 0), "MAPSEC_FOUR_ISLAND", received.Rng).Pokemon;
        Assert.IsFalse(hatched.IsEgg);
        Assert.AreEqual("DUNSPARCE", hatched.Nickname);
        Assert.AreEqual(Dunsparce, hatched.Pokemon.Species);
        Assert.IsNull(PokemonStateValidator.ValidateParty(content, hatched));
    }

    [TestMethod]
    public void OrdinaryBattleAdmissionUsesTheLoadedTableAndExactPendingCandidate()
    {
        var owned = Source.Value.Content.Pokemon;
        var (definition, state) = OrdinaryDefinition(Dunsparce);
        var binding = definition.BindBattle(state);
        var generated = definition.GeneratePokemon(state, new(123), 42).Pokemon;
        Assert.IsTrue(binding.SourceInitialInstance(owned, generated));
        var player = new WildBattleCombatant(Make(1, 25).Pokemon);
        var result = SourceWildBattlePolicy.StartOwned(owned, [player], 0, new(generated), new(0),
            new() { OrdinaryLand = binding }, WildBattleTerrain.Grass);
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State!));
        Assert.AreSame(binding, result.State!.WildEncounter!.OrdinaryLand);
        Assert.AreEqual("ITEM_NONE", result.State.Party(BattleSide.Opponent).Members[0].HeldItem);
        Assert.IsTrue(result.Trace.Any(draw => draw.Purpose == "wild-battle.held-item"));
        Assert.IsFalse(SourceWildBattlePolicy.StartOwned(owned, [player], 0, new(generated), new(0)).Succeeded,
            "Ownership alone must not admit an unbound ordinary species.");
        Assert.IsFalse(binding.Matches(Make(2, 6).Pokemon), "The selected level must remain exact, even inside the table range.");
        Assert.IsFalse(binding.SourceInitialInstance(owned, generated with { Friendship = generated.Friendship - 1 }));
        Assert.IsFalse(SourceWildBattlePolicy.StartOwned(owned, [player], 0, new(generated, Status: 1), new(0),
            new() { OrdinaryLand = binding }).Succeeded);
        Assert.IsFalse(SourceWildBattlePolicy.StartOwned(owned, [player], 0, new(generated), new(0),
            new() { OrdinaryLand = binding, ScriptedSnorlax = true }).Succeeded);
        Assert.Throws<ArgumentException>(() => definition.BindBattle(state with { Pending = state.Pending! with {
            Candidate = state.Pending.Candidate with { Species = new("pret:SPECIES_MEOWTH") } } }));
        Assert.Throws<ArgumentException>(() => definition.BindBattle(state with { Pending = state.Pending! with {
            Candidate = state.Pending.Candidate with { Level = 8 } } }));
        Assert.Throws<InvalidOperationException>(() => definition.BindBattle(state with { Pending = null }));
    }

    [TestMethod]
    public void OrdinaryHeldItemsUseCanonicalCommonAndRareMetadata()
    {
        var owned = Source.Value.Content.Pokemon;
        foreach (var symbol in new[] { "DUNSPARCE", "MEOWTH", "PARAS" })
        {
            var species = new SpeciesId("pret:SPECIES_" + symbol);
            var source = Source.Value.Canonical.Metadata[species];
            var mechanics = owned.Mechanics.SpeciesMechanics[species];
            Assert.AreEqual(source.ItemCommon, mechanics.ItemCommon);
            Assert.AreEqual(source.ItemRare, mechanics.ItemRare);
            var (definition, state) = OrdinaryDefinition(species);
            var binding = definition.BindBattle(state);
            foreach (int roll in new[] { 0, 44, 45, 94, 95, 99 })
                Assert.AreEqual(source.ItemCommon == source.ItemRare ? source.ItemCommon :
                    roll < 45 ? "ITEM_NONE" : roll < 95 ? source.ItemCommon : source.ItemRare,
                    binding.HeldItem(owned, roll));
            Assert.Throws<ArgumentException>(() => binding.HeldItem(owned, 100));
        }
    }

    private static (OrdinaryLandEncounterDefinition, EarlyLandEncounterState) OrdinaryDefinition(SpeciesId species)
    {
        var table = EarlyLandEncounterRules.Route1Table with { Map = new("map.supplied-ordinary-test"),
            Slots = EarlyLandEncounterRules.Route1Table.Slots.Select(slot => slot with {
                Species = species, MinimumLevel = 5, MaximumLevel = 7 }).ToImmutableArray() };
        var definition = new OrdinaryLandEncounterCatalog(
            new Dictionary<MapId, EarlyLandEncounterTable> { [table.Map] = table },
            new Dictionary<MapId, IFieldMap> { [table.Map] = new OrdinaryMap(table.Map) },
            Source.Value.Content.Catalog)[table.Map];
        var state = new EarlyLandEncounterState(new(0), 2, RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion) {
            Pending = new(new(table.Map, 0, species, 5), 2) };
        return (definition, state);
    }

    private sealed record OrdinaryMap(MapId Id) : IFieldMap
    {
        public int Width => 1;
        public int Height => 1;
        public ImmutableArray<FieldWarp> FieldWarps => [];
        public TileCell? GetTile(int x, int y) => x == 0 && y == 0 ? new(false) : null;
        public int? SourceBehaviorAt(int x, int y) => 2;
        public int? SourceEncounterTypeAt(int x, int y) => 1;
    }

    private static PartyPokemon Make(long id, int level, uint personality = 0)
    {
        var content = Source.Value.Content;
        var pokemon = PokemonFactory.Create(content.Catalog, new(Dunsparce, level, 42,
            IvPolicy.Fixed(15), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
        return new(id, pokemon, content.SpeciesNames[pokemon.SourceSpeciesId], "RED", 0, 2,
            "MAPSEC_THREE_ISLE_PORT", level, 4, "ITEM_POKE_BALL", "ITEM_NONE");
    }
}
