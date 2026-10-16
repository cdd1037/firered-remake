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
public sealed class PatternBushCollectionTests
{
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
    [DataRow("LEDYBA", "LEDYBA", 15)]
    [DataRow("LEDIAN", "LEDYBA", 15)]
    [DataRow("SPINARAK", "SPINARAK", 15)]
    [DataRow("ARIADOS", "SPINARAK", 15)]
    [DataRow("HERACROSS", "HERACROSS", 25)]
    public void OwnedSpeciesUseCanonicalMetadataAndRoundTripStorage(string symbol, string eggSpecies, int cycles)
    {
        var source = Source.Value;
        var content = source.Content;
        var id = new SpeciesId("pret:SPECIES_" + symbol);
        Assert.AreEqual(source.Canonical.Catalog.Species[id], content.Catalog.Species[id] with { Evolutions = source.Canonical.Catalog.Species[id].Evolutions });
        Assert.AreEqual(content.Catalog.Species[id].BaseStats, content.Pokemon.Opponents.Catalog.Species[id].BaseStats);
        var breeding = content.Catalog.BreedingData!.Species[id];
        Assert.AreEqual(new SourceBreedingSpecies(id, 3, 3, checked((byte)cycles)), breeding.Breeding);
        Assert.AreEqual(new SpeciesId("pret:SPECIES_" + eggSpecies), breeding.BaseEggSpecies);
        var mon = Make(symbol, 2, 25);
        Assert.IsNull(PokemonStateValidator.ValidateParty(content, mon));
        Assert.IsTrue(source.Machines.Compatibility(new("pret:ITEM_TM06"), id).CanLearn);
        Assert.IsFalse(source.Machines.Compatibility(new("pret:ITEM_HM03"), id).CanLearn);
        var tutors = PretMoveTutors.Load(TestAssets.Root, content.Catalog).Catalog;
        var substitute = tutors.Definitions.Values.Single(tutor => tutor.Move.Value == "pret:MOVE_SUBSTITUTE");
        Assert.AreEqual(MoveTutorEligibility.Able, MoveTutorRules.Eligibility(substitute, mon));
        var taught = MoveTutorRules.Teach(tutors, content.Catalog, mon, substitute.Index, mon.Pokemon.Moves.Length == 4 ? 0 : null);
        Assert.IsTrue(taught.Succeeded, taught.Error);
        var stored = PokemonStorageRules.Deposit(content, [Make(symbol, 1, 5), taught.Pokemon], PokemonStorageState.Empty, 1, 0);
        Assert.IsTrue(stored.Succeeded);
        var withdrawn = PokemonStorageRules.Withdraw(content, stored.Party, stored.Storage, 0, 0);
        Assert.IsTrue(withdrawn.Succeeded);
        Assert.AreEqual(JsonSerializer.Serialize(taught.Pokemon), JsonSerializer.Serialize(withdrawn.Party[1]));
        Assert.IsNull(PokemonStorageValidator.Validate(content, withdrawn.Party, withdrawn.Storage, 3));
    }

    [TestMethod]
    [DataRow("LEDYBA", "LEDIAN", 18)]
    [DataRow("SPINARAK", "ARIADOS", 22)]
    public void CanonicalLevelEvolutionUsesSharedRules(string symbol, string target, int level)
    {
        var content = Source.Value.Content;
        Assert.IsNull(LevelEvolutionRules.Eligibility(content, Make(symbol, 1, level - 1), level - 2));
        var mon = Make(symbol, 1, level);
        Assert.IsNull(LevelEvolutionRules.Eligibility(content, mon, level));
        Assert.IsNull(LevelEvolutionRules.Eligibility(content, mon with { HeldItem = "ITEM_EVERSTONE" }, level - 1));
        var proposal = LevelEvolutionRules.Eligibility(content, mon, level - 1);
        Assert.IsNotNull(proposal);
        Assert.AreEqual(new SpeciesId("pret:SPECIES_" + target), proposal.TargetSpecies);
        var evolved = LevelEvolutionRules.Apply(content, mon, proposal);
        Assert.AreEqual(proposal.TargetSpecies, evolved.Pokemon.Pokemon.Species);
        Assert.AreEqual(mon.InstanceId, evolved.Pokemon.InstanceId);
        Assert.IsNull(PokemonStateValidator.ValidateParty(content, evolved.Pokemon));
    }

    [TestMethod]
    [DataRow("LEDIAN", "LEDYBA")]
    [DataRow("ARIADOS", "SPINARAK")]
    [DataRow("HERACROSS", "HERACROSS")]
    public void FamiliesBreedAndHatchThroughSharedRules(string symbol, string child)
    {
        var content = Source.Value.Content;
        var female = Make(symbol, 1, 30, 0);
        var male = Make(symbol, 2, 30, 255);
        var eggMove = content.Catalog.BreedingData!.Species[new("pret:SPECIES_" + child)].EggMoves[0];
        var inheritedMoves = new[] { eggMove, new MoveId("pret:MOVE_TOXIC") }
            .Select(id => content.Catalog.Moves[id])
            .Select(move => new PokemonMoveSlot(move.Id, move.Pp, move.Pp)).ToImmutableArray();
        male = male with { Pokemon = male.Pokemon with { Moves = inheritedMoves } };
        var received = SourceEggRules.Receive(content, content.Catalog.BreedingData!,
            new(new(female, 0), new(male, 0), 123, 0), 3,
            new(42, "RED", 0), "MAPSEC_FOUR_ISLAND", 4, new(0));
        Assert.AreEqual(new SpeciesId("pret:SPECIES_" + child), received.Egg.Pokemon.Species);
        Assert.IsNull(PokemonStateValidator.ValidateParty(content, received.Egg));
        Assert.IsTrue(received.Egg.Pokemon.Moves.Any(slot => slot.Move == eggMove));
        Assert.IsTrue(received.Egg.Pokemon.Moves.Any(slot => slot.Move == new MoveId("pret:MOVE_TOXIC")));
        var hatched = SourceEggRules.Hatch(content, received.Egg, new(42, "RED", 0), "MAPSEC_FOUR_ISLAND", received.Rng).Pokemon;
        Assert.IsFalse(hatched.IsEgg);
        Assert.AreEqual(child, hatched.Nickname);
        Assert.IsNull(PokemonStateValidator.ValidateParty(content, hatched));
    }

    private static PartyPokemon Make(string symbol, long id, int level, uint personality = 0)
    {
        var content = Source.Value.Content;
        var pokemon = PokemonFactory.Create(content.Catalog, new(new("pret:SPECIES_" + symbol), level, 42,
            IvPolicy.Fixed(15), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
        return new(id, pokemon, content.SpeciesNames[pokemon.SourceSpeciesId], "RED", 0, 2,
            "MAPSEC_PATTERN_BUSH", level, 4, "ITEM_POKE_BALL", "ITEM_NONE");
    }
}
