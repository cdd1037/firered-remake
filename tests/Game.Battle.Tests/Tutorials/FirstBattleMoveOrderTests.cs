using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain;
using Game.Domain.FirstBattle;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace FireRed.Tests.Battle.Tutorials;

[TestClass, TestCategory("Game")]
public sealed class FirstBattleMoveOrderTests
{
    private static PretCampaignCatalog Content => TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
    private static PokemonCatalog Catalog => Content.PokemonContent!.Catalog;
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);

    private static (PartyPokemon Owned, TrainerPartyCreationResult Opponent, FirstBattleState State) Start(string species)
    {
        var pokemon = PokemonFactory.Create(Catalog, new(new("pret:SPECIES_" + species), 5, 42,
            IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0x12345678)).Pokemon;
        var owned = new PartyPokemon(1, pokemon, species, "RED", 0, 2, "MAPSEC_PALLET_TOWN", 5, 4, "ITEM_POKE_BALL", "ITEM_NONE");
        string rival = species switch { "BULBASAUR" => "CHARMANDER", "CHARMANDER" => "SQUIRTLE", _ => "BULBASAUR" };
        var opponent = TrainerPartyFactory.Create(Content.BattleContent!.Trainers, Catalog,
            new("pret:TRAINER_RIVAL_OAKS_LAB_" + rival), new(0x12345678));
        var started = FirstBattleRules.Start(Catalog, new(pokemon), new(opponent.Party[0].Pokemon), opponent.Rng);
        Assert.IsTrue(started.Succeeded, started.Fault);
        return (owned, opponent, started.State!);
    }

    [TestMethod]
    [DataRow("BULBASAUR")]
    [DataRow("CHARMANDER")]
    [DataRow("SQUIRTLE")]
    public void ReorderingConsumesNoTurnOrRngAndNextAiAndAttackUseNewSlot(string species)
    {
        var (_, _, state) = Start(species);
        var result = FirstBattleMoveOrderRules.Swap(Catalog, state, state.Revision, 0, 1);
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsTrue(result.Events.IsEmpty && result.Trace.IsEmpty);
        var after = result.State!;
        Assert.AreEqual(state.Player.Pokemon.Moves[1], after.Player.Pokemon.Moves[0]);
        Assert.AreEqual(state.Player.Pokemon.Moves[0], after.Player.Pokemon.Moves[1]);
        Assert.AreEqual(Json(state with { Revision = state.Revision + 1, Player = after.Player }), Json(after));
        var restored = JsonSerializer.Deserialize<FirstBattleState>(Json(after))!;
        Assert.IsNull(FirstBattleRules.Validate(Catalog, restored));
        var ai = FirstBattleAi.Choose(Catalog, new(restored.Opponent.Pokemon, restored.Player.Pokemon), restored.Rng);
        Assert.AreEqual(TransitionStatus.Accepted, ai.Status, ai.Diagnostic?.Code);
        var turn = FirstBattleRules.ResolveTurn(Catalog, restored with { Rng = ai.Rng }, 1, ai.MoveSlot!.Value);
        Assert.IsTrue(turn.Succeeded, turn.Fault);
        var attack = turn.Events.OfType<FirstBattleMoveUsedEvent>().Single(e => e.Actor == FirstBattleSide.Player);
        Assert.AreEqual(1, attack.Slot);
        Assert.AreEqual(state.Player.Pokemon.Moves[0].Move, attack.Move);
        Assert.AreEqual(state.Player.Pokemon.Moves[0].CurrentPp - 1, turn.State!.Player.Pokemon.Moves[1].CurrentPp);
    }

    [TestMethod]
    [DataRow("BULBASAUR")]
    [DataRow("CHARMANDER")]
    [DataRow("SQUIRTLE")]
    public void TerminalRewardsRetainNewOrderAndOriginalPreBattleSnapshot(string species)
    {
        var (owned, opponent, state) = Start(species);
        var swapped = FirstBattleMoveOrderRules.Swap(Catalog, state, state.Revision, 0, 1);
        Assert.IsTrue(swapped.Succeeded, swapped.Fault);
        var after = swapped.State!;
        var win = FirstBattleRules.ResolveTurn(Catalog, after with {
            Opponent = after.Opponent with { Pokemon = after.Opponent.Pokemon with { CurrentHp = 1 } } }, 1, 0);
        Assert.IsTrue(win.Succeeded, win.Fault);
        Assert.AreEqual(FirstBattleOutcome.Won, win.State!.Outcome);
        var reward = LabBattleRewards.Apply(Catalog, owned, win.State.Player.Pokemon, win.State.Opponent.Pokemon,
            win.State.Outcome, 3000, opponent.Trainer, "MAPSEC_PALLET_TOWN");
        Assert.AreEqual(80, reward.MoneyGained);
        Assert.AreEqual(6, reward.Pokemon.Pokemon.Level);
        CollectionAssert.AreEqual(after.Player.Pokemon.Moves.Select(m => m.Move).ToArray(),
            reward.Pokemon.Pokemon.Moves.Select(m => m.Move).ToArray());
        Assert.AreEqual(state.Player.Pokemon.Moves[0], owned.Pokemon.Moves[0]);
        Assert.AreEqual(state.Player.Pokemon.Moves[1], owned.Pokemon.Moves[1]);
    }

    [TestMethod]
    public void SameSlotIsExactNoOpAndZeroPpSlotCanStillBeReordered()
    {
        var state = Start("BULBASAUR").State;
        Assert.AreSame(state, FirstBattleMoveOrderRules.Swap(Catalog, state, state.Revision, 1, 1).State);
        state = state with { Player = state.Player with { Pokemon = state.Player.Pokemon with {
            Moves = state.Player.Pokemon.Moves.SetItem(0, state.Player.Pokemon.Moves[0] with { CurrentPp = 0 }) } } };
        var result = FirstBattleMoveOrderRules.Swap(Catalog, state, state.Revision, 0, 1);
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.AreEqual(0, result.State!.Player.Pokemon.Moves[1].CurrentPp);
    }

    [TestMethod]
    [DataRow("revision")]
    [DataRow("empty")]
    [DataRow("negative")]
    [DataRow("exhausted")]
    [DataRow("counter")]
    [DataRow("wrong-pair")]
    [DataRow("ended")]
    public void InvalidRequestsRetainStateAndProduceNoEventsOrDraws(string scenario)
    {
        var state = Start("BULBASAUR").State; long revision = state.Revision; int first = 0, second = 1;
        if (scenario == "revision") revision--;
        if (scenario == "empty") second = 2;
        if (scenario == "negative") first = -1;
        if (scenario == "exhausted") state = state with { Player = state.Player with { Pokemon = state.Player.Pokemon with {
            Moves = state.Player.Pokemon.Moves.Select(m => m with { CurrentPp = 0 }).ToImmutableArray() } } };
        if (scenario == "counter") { state = state with { Revision = long.MaxValue }; revision = state.Revision; }
        if (scenario == "wrong-pair") state = state with { Player = state.Player with { Pokemon = state.Player.Pokemon with {
            Moves = [state.Player.Pokemon.Moves[0], state.Player.Pokemon.Moves[0]] } } };
        if (scenario == "ended") state = state with { Outcome = FirstBattleOutcome.Won,
            Opponent = state.Opponent with { Pokemon = state.Opponent.Pokemon with { CurrentHp = 0 } } };
        var result = FirstBattleMoveOrderRules.Swap(Catalog, state, revision, first, second);
        Assert.IsFalse(result.Succeeded, scenario);
        Assert.AreSame(state, result.State, scenario);
        Assert.IsTrue(result.Events.IsEmpty && result.Trace.IsEmpty, scenario);
    }
}
