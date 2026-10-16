using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Content;
using Game.Domain;
using Game.Domain.FirstBattle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;

namespace FireRed.Tests.Battle.Tutorials;

[TestClass, TestCategory("Game")]
public sealed class FirstBattleCurrentCatalogTests
{
    private static PretCampaignCatalog Content => TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root));
    private static PokemonCatalog Catalog => Content.PokemonContent!.Catalog;

    // These snapshots freeze bd6a6f5 before the profile refactor. They are regression evidence,
    // not an independent source oracle. Normal tests never generate or rewrite expected values.
    [TestMethod]
    [DataRow("BULBASAUR", "82C29367B74BBA3E32A588F35269951C7E2344BBBF3AAC75075163E8A58481E0")]
    [DataRow("CHARMANDER", "DB6DFB5D2F8C51CA04F482AF6028ADB2D493B74F175D16EEC955A1C803F01692")]
    [DataRow("SQUIRTLE", "C6ED19E6718103E1B9C5BB4A25E8282EB5FF2B52F3CAD0673B4BAD01B98DEA7F")]
    public void OriginalCatalogPreservesEventsRewardsAndEveryRandomDraw(string species, string expected)
    {
        var snapshots = new List<object>();
        foreach (string scenario in new[] { "opening", "status", "status-floor", "potion", "struggle",
            "potion-struggle", "win", "loss", "draw", "complete" })
            snapshots.Add(Scenario(species, scenario));
        string actual = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(snapshots)));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("BULBASAUR")]
    [DataRow("CHARMANDER")]
    [DataRow("SQUIRTLE")]
    public void CatalogPowerAndPpReachAiTurnsAndRewardSettlement(string species)
    {
        var moves = Catalog.Moves.ToBuilder();
        foreach (string symbol in new[] { "TACKLE", "SCRATCH", "GROWL", "TAIL_WHIP" })
        {
            var id = new MoveId("pret:MOVE_" + symbol);
            var move = moves[id];
            moves[id] = move with { Pp = move.Pp == 40 ? 39 : move.Pp + 1,
                Power = move.Power == 0 ? 0 : 80 };
        }
        var edited = Catalog with { Moves = moves.ToImmutable() };
        var original = Start(Catalog, species);
        var changed = Start(edited, species);
        Assert.IsTrue(changed.Started.Succeeded, changed.Started.Fault);
        var before = original.Started.State!;
        var after = changed.Started.State!;
        foreach (var slot in after.Player.Pokemon.Moves)
            Assert.AreEqual(edited.Moves[slot.Move].Pp, slot.MaximumPp);
        var oldTurn = FirstBattleRules.ResolveTurn(Catalog, before, 0, 0);
        var newTurn = FirstBattleRules.ResolveTurn(edited, after, 0, 0);
        Assert.IsTrue(newTurn.Succeeded, newTurn.Fault);
        int PlayerDamage(FirstBattleResult turn) => turn.Events.OfType<FirstBattleHpChangedEvent>()
            .Single(e => e.Actor == FirstBattleSide.Player).Damage;
        Assert.IsTrue(PlayerDamage(newTurn) > PlayerDamage(oldTurn));
        Assert.AreEqual(after.Player.Pokemon.Moves[0].MaximumPp - 1, newTurn.State!.Player.Pokemon.Moves[0].CurrentPp);

        int targetHp = FirstBattleDamage.Calculate(Catalog, before.Opponent.Pokemon, before.Player.Pokemon,
            before.Opponent.Pokemon.Moves[0].Move, 6, 6, false, 100).Damage + 1;
        Assert.IsTrue(FirstBattleDamage.Calculate(edited, after.Opponent.Pokemon, after.Player.Pokemon,
            after.Opponent.Pokemon.Moves[0].Move, 6, 6, false, 85).Damage >= targetHp);
        var oldAi = FirstBattleAi.Choose(Catalog, new(before.Opponent.Pokemon,
            before.Player.Pokemon with { CurrentHp = targetHp }), before.Rng);
        var newAi = FirstBattleAi.Choose(edited, new(after.Opponent.Pokemon,
            after.Player.Pokemon with { CurrentHp = targetHp }), after.Rng);
        Assert.AreEqual(TransitionStatus.Accepted, newAi.Status, newAi.Diagnostic?.Code);
        Assert.AreEqual(oldAi.Scores[0] + 4, newAi.Scores[0]);
        CollectionAssert.AreEqual(oldAi.Trace.ToArray(), newAi.Trace.ToArray());

        var win = FirstBattleRules.ResolveTurn(edited, after with
        { Opponent = after.Opponent with { Pokemon = after.Opponent.Pokemon with { CurrentHp = 1 } } }, 0, 0);
        Assert.AreEqual(FirstBattleOutcome.Won, win.State!.Outcome);
        var reward = LabBattleRewards.Apply(edited, changed.Owned, win.State.Player.Pokemon,
            win.State.Opponent.Pokemon, win.State.Outcome, 3000, changed.Opponent.Trainer, "MAPSEC_PALLET_TOWN");
        Assert.AreEqual(80, reward.MoneyGained);
        Assert.AreEqual(6, reward.Pokemon.Pokemon.Level);
        foreach (var slot in LabBattleRewards.Heal(edited, reward.Pokemon).Pokemon.Moves)
            Assert.AreEqual(edited.Moves[slot.Move].Pp, slot.CurrentPp);
    }

    [TestMethod]
    public void CatalogAccuracyControlsHitChecksAfterTutorialExemption()
    {
        var moves = Catalog.Moves.ToBuilder();
        foreach (string symbol in new[] { "TACKLE", "SCRATCH" })
        {
            var id = new MoveId("pret:MOVE_" + symbol);
            moves[id] = moves[id] with { Accuracy = 1 };
        }
        var edited = Catalog with { Moves = moves.ToImmutable() };
        var battle = Start(edited, "BULBASAUR").Started;
        Assert.IsTrue(battle.Succeeded, battle.Fault);
        var turn = FirstBattleRules.ResolveTurn(edited, battle.State! with
        { TutorialFlags = FirstBattleTutorialFlags.Damage | FirstBattleTutorialFlags.StatChange }, 0, 0);
        Assert.IsTrue(turn.Succeeded, turn.Fault);
        Assert.AreEqual(2, turn.Events.OfType<FirstBattleMoveMissedEvent>().Count());
        Assert.AreEqual(0, turn.Events.OfType<FirstBattleHpChangedEvent>().Count());
        Assert.AreEqual(2, turn.Events.OfType<FirstBattleMoveUsedEvent>().Count());
    }

    [TestMethod]
    [DataRow("effect")]
    [DataRow("type")]
    [DataRow("priority")]
    [DataRow("accuracy-zero")]
    [DataRow("power-one")]
    [DataRow("power-zero")]
    [DataRow("status-power")]
    [DataRow("secondary")]
    [DataRow("target")]
    public void UnsupportedMoveCapabilitiesStillFailAtomically(string field)
    {
        var id = new MoveId(field == "status-power" ? "pret:MOVE_GROWL" : "pret:MOVE_TACKLE");
        var move = Catalog.Moves[id];
        var changed = field switch
        {
            "effect" => move with { Effect = "EFFECT_POISON_HIT" },
            "type" => move with { Type = "TYPE_FIRE" },
            "priority" => move with { Priority = 1 },
            "accuracy-zero" => move with { Accuracy = 0 },
            "power-one" or "status-power" => move with { Power = 1 },
            "power-zero" => move with { Power = 0 },
            "secondary" => move with { SecondaryEffectChance = 10 },
            _ => move with { Target = "MOVE_TARGET_RANDOM" }
        };
        var edited = Catalog with { Moves = Catalog.Moves.SetItem(id, changed) };
        var started = Start(edited, "BULBASAUR").Started;
        Assert.AreEqual("first_battle_move_definition_unsupported", started.Fault);
        Assert.IsNull(started.State);
        Assert.IsTrue(started.Trace.IsEmpty && started.Events.IsEmpty);
        var state = Start(Catalog, "BULBASAUR").Started.State!;
        var turn = FirstBattleRules.ResolveTurn(edited, state, 0, 0);
        Assert.AreSame(state, turn.State);
        Assert.IsTrue(turn.Trace.IsEmpty && turn.Events.IsEmpty);
        var ai = FirstBattleAi.Choose(edited, new(state.Opponent.Pokemon, state.Player.Pokemon), state.Rng);
        Assert.AreEqual("first_battle_ai_move_definition", ai.Diagnostic?.Code);
        Assert.AreEqual(state.Rng, ai.Rng);
        Assert.IsTrue(ai.Trace.IsEmpty);
    }

    [TestMethod]
    [DataRow("type")]
    [DataRow("ability")]
    public void SpecializedAiTypeAndAbilityLimitsRemainEnforced(string field)
    {
        var id = new SpeciesId("pret:SPECIES_BULBASAUR");
        var definition = Catalog.Species[id];
        definition = field == "type" ? definition with { Type1 = "TYPE_NORMAL" } :
            definition with { Ability1 = "ABILITY_INTIMIDATE" };
        var edited = Catalog with { Species = Catalog.Species.SetItem(id, definition) };
        var started = Start(edited, "BULBASAUR");
        Assert.AreEqual("first_battle_species_definition_unsupported", started.Started.Fault);
        var ai = FirstBattleAi.Choose(edited, new(started.Opponent.Party[0].Pokemon, started.Owned.Pokemon), started.Opponent.Rng);
        Assert.AreEqual("first_battle_ai_target_type_ability", ai.Diagnostic?.Code);
        Assert.IsTrue(ai.Trace.IsEmpty);
    }

    private static object Scenario(string species, string scenario)
    {
        var (owned, created, start) = Start(Catalog, species);
        Assert.IsTrue(start.Succeeded, start.Fault);
        var state = start.State!;
        AssertRandomSequence(created.Rng, start.Trace, state.Rng);
        var results = new List<object> { new { created.Trainer, created.Party, created.Rng, created.Trace }, start };
        if (scenario is "potion" or "potion-struggle" or "loss" or "draw")
            state = state with { Player = state.Player with { Pokemon = state.Player.Pokemon with { CurrentHp = 1 } } };
        if (scenario is "win" or "draw")
            state = state with { Opponent = state.Opponent with { Pokemon = state.Opponent.Pokemon with { CurrentHp = 1 } } };
        if (scenario is "struggle" or "draw") state = state with { Player = Exhaust(state.Player), Opponent = Exhaust(state.Opponent) };
        if (scenario == "potion-struggle") state = state with { Opponent = Exhaust(state.Opponent) };
        if (scenario == "status-floor") state = state with
        {
            Player = state.Player with { AttackStage = 0, DefenseStage = 0 },
            Opponent = state.Opponent with { AttackStage = 0, DefenseStage = 0 }
        };
        var rewardBefore = owned with { Pokemon = state.Player.Pokemon };
        // A represented pre-battle owner must be alive, including the one-HP loss/draw cases.
        var turns = new List<FirstBattleResult>();
        for (int i = 0; i < (scenario == "complete" ? 64 : 1); i++)
        {
            int opponentSlot = FirstBattleRules.StruggleSlot;
            if (!FirstBattleRules.RequiresStruggle(state.Opponent))
            {
                var ai = FirstBattleAi.Choose(Catalog, new(state.Opponent.Pokemon, state.Player.Pokemon,
                    state.Opponent.AttackStage, state.Opponent.DefenseStage, state.Player.AttackStage,
                    state.Player.DefenseStage, created.Trainer.AiFlags), state.Rng);
                Assert.AreEqual(TransitionStatus.Accepted, ai.Status, ai.Diagnostic?.Code);
                AssertRandomSequence(state.Rng, ai.Trace, ai.Rng);
                results.Add(ai);
                state = state with { Rng = ai.Rng };
                opponentSlot = scenario is "opening" or "win" or "loss" ? 0 :
                    scenario is "status" or "status-floor" ? 1 : ai.MoveSlot!.Value;
            }
            FirstBattleResult turn;
            if (scenario is "potion" or "potion-struggle")
            {
                var items = Content.OpeningItems!;
                var inventory = OpeningInventoryRules.NewGame(items) with { BagItems = [new(items.Potion.Id, 1)] };
                var potion = OpeningPotionRules.Use(items, Catalog, inventory, state.Player.Pokemon, PotionUseContext.Battle);
                Assert.IsTrue(potion.Succeeded, potion.Error);
                Assert.AreEqual(1, potion.ItemsConsumed);
                Assert.IsTrue(potion.Inventory.BagItems.IsEmpty);
                results.Add(potion);
                turn = FirstBattleRules.ResolvePotionTurn(Catalog, state, potion.Pokemon, opponentSlot);
                Assert.IsInstanceOfType<FirstBattlePotionRecoveredEvent>(turn.Events[0]);
                Assert.AreEqual(1, turn.Events.OfType<FirstBattleMoveUsedEvent>().Count());
                Assert.IsTrue(turn.State!.TutorialFlags.HasFlag(FirstBattleTutorialFlags.HpRestore));
            }
            else
            {
                int playerSlot = FirstBattleRules.RequiresStruggle(state.Player) ? FirstBattleRules.StruggleSlot :
                    scenario is "status" or "status-floor" or "loss" ? 1 : 0;
                turn = FirstBattleRules.ResolveTurn(Catalog, state, playerSlot, opponentSlot);
            }
            Assert.IsTrue(turn.Succeeded, turn.Fault);
            Assert.AreEqual(state.Revision + 1, turn.State!.Revision);
            CollectionAssert.AreEqual(Enumerable.Range(1, turn.Events.Length).Select(n => state.EventSequence + n).ToArray(),
                turn.Events.Select(e => e.Sequence).ToArray());
            AssertRandomSequence(state.Rng, turn.Trace, turn.State.Rng);
            turns.Add(turn);
            results.Add(turn);
            state = turn.State;
            if (state.Outcome != FirstBattleOutcome.Ongoing) break;
        }
        if (scenario is "win" or "loss" or "draw")
            Assert.AreEqual(scenario switch { "win" => FirstBattleOutcome.Won, "loss" => FirstBattleOutcome.Lost, _ => FirstBattleOutcome.Draw }, state.Outcome);
        if (scenario == "complete") Assert.AreNotEqual(FirstBattleOutcome.Ongoing, state.Outcome);
        if (scenario is "struggle" or "draw")
        {
            Assert.IsTrue(turns.SelectMany(t => t.Events).OfType<FirstBattleRecoilEvent>().Any());
            Assert.IsTrue(turns.SelectMany(t => t.Events).OfType<FirstBattleMoveUsedEvent>().All(e => e.Slot == -1 && e.PpBefore == 0 && e.PpAfter == 0));
        }
        if (scenario == "status-floor") Assert.AreEqual(2, turns[0].Events.OfType<FirstBattleStatAtMinimumEvent>().Count());
        if (state.Outcome != FirstBattleOutcome.Ongoing)
        {
            var reward = LabBattleRewards.Apply(Catalog, rewardBefore, state.Player.Pokemon, state.Opponent.Pokemon,
                state.Outcome, 3000, created.Trainer, "MAPSEC_PALLET_TOWN");
            Assert.AreEqual(state.Outcome == FirstBattleOutcome.Won ? 80 : 0, reward.MoneyGained);
            Assert.AreEqual(state.Outcome == FirstBattleOutcome.Won ? 6 : 5, reward.Pokemon.Pokemon.Level);
            results.Add(reward);
            results.Add(LabBattleRewards.Heal(Catalog, reward.Pokemon));
            results.Add(EarlyRivalCallback.Resolve(Content.BattleContent!.Trainers.Battles[created.Trainer.Id], (EarlyRivalOutcome)(int)state.Outcome));
        }
        return new { species, scenario, results };
    }

    private static (PartyPokemon Owned, TrainerPartyCreationResult Opponent, FirstBattleResult Started) Start(PokemonCatalog catalog, string species)
    {
        var pokemon = PokemonFactory.Create(catalog, new(new("pret:SPECIES_" + species), 5, 42,
            IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0x12345678)).Pokemon;
        var owned = new PartyPokemon(1, pokemon, species, "RED", 0, 2, "MAPSEC_PALLET_TOWN", 5, 4, "ITEM_POKE_BALL", "ITEM_NONE");
        string rival = species switch { "BULBASAUR" => "CHARMANDER", "CHARMANDER" => "SQUIRTLE", _ => "BULBASAUR" };
        var created = TrainerPartyFactory.Create(Content.BattleContent!.Trainers, catalog,
            new("pret:TRAINER_RIVAL_OAKS_LAB_" + rival), new(0x12345678));
        return (owned, created, FirstBattleRules.Start(catalog, new(pokemon), new(created.Party[0].Pokemon), created.Rng));
    }

    private static FirstBattleCombatant Exhaust(FirstBattleCombatant combatant) => combatant with
    { Pokemon = combatant.Pokemon with { Moves = combatant.Pokemon.Moves.Select(m => m with { CurrentPp = 0 }).ToImmutableArray() } };

    private static void AssertRandomSequence(RngState before, ImmutableArray<RandomTrace> trace, RngState after)
    {
        uint seed = before.Seed;
        ulong cursor = before.Cursor;
        foreach (var draw in trace)
        {
            uint next = unchecked(seed * 1103515245u + 24691u);
            Assert.AreEqual(++cursor, draw.Cursor);
            Assert.AreEqual(seed, draw.Before);
            Assert.AreEqual(next, draw.After);
            Assert.AreEqual((ushort)(next >> 16), draw.Raw);
            Assert.AreEqual(draw.Raw % draw.Bound, draw.Result);
            seed = next;
        }
        Assert.AreEqual(new RngState(seed, cursor), after);
    }
}
