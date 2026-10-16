using System.Collections.Immutable;
using System.Text.Json;
using System.Security.Cryptography;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Battle.Hazards;
[TestClass, TestCategory("Game")]
public sealed class SpikesTests
{
    private global::Game.Domain.Pokemon.SourceMechanicsCatalog proof = default !;
    private global::Game.Domain.Pokemon.PokemonCatalog catalog = default !;
    private int turns = default !;
    [TestInitialize]
    public void SetUp()
    {
        proof = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
        catalog = proof.Catalog;
        turns = 0;
    }

    private string J<T>(T x) => JsonSerializer.Serialize(x);
    private MoveId Id(string n) => new("pret:MOVE_" + n);
    private WildBattleCombatant Mon(string species = "DROWZEE", string moves = "MEDITATE", int level = 70, int? hp = null, uint pid = 0)
    {
        var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
        foreach (var m in moves.Split(','))
            Assert.IsTrue(catalog.Species[p.Species].LevelUpMoves.Any(x => x.Move == Id(m) && x.Level <= level), "natural move " + species + " " + m);
        return new(p with { CurrentHp = hp ?? p.CurrentHp, Moves = moves.Split(',').Select(m => new PokemonMoveSlot(Id(m), catalog.Moves[Id(m)].Pp, catalog.Moves[Id(m)].Pp)).ToImmutableArray() });
    }

    private WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon, m.HeldItem, m.Status);
    private SourceBattleFrame Frame(WildBattleCombatant[]? mons = null, WildBattleCombatant[]? players = null, WildBattleCombatant[]? opponents = null)
    {
        var m = mons ?? [Mon("SKARMORY", "SPIKES"), Mon(), Mon(), Mon()];
        var p = new[]
        {
            Member(m[0]),
            Member(m[2])
        }.Concat((players ?? []).Select(Member)).ToImmutableArray();
        return new()
        {
            Layout = SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = WildBattleCalledBattleKind.Trainer,
            Rng = new(31),
            Parties = [new(BattleSide.Player, p), new(BattleSide.Opponent, new[] { Member(m[1]), Member(m[3]) }.Concat((opponents ?? []).Select(Member)).ToImmutableArray())],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, m[i])).ToImmutableArray(),
            Rewards = new()
            {
                Context = new(SourceRewardBattleKind.OrdinaryTrainer, 42, "RED", "MAPSEC_ROUTE_1"),
                Metadata = Enumerable.Range(0, p.Length).Select(i => new SourceRewardMetadata(new(BattleSide.Player, i), "RED", "MAPSEC_PALLET_TOWN", "ITEM_POKE_BALL", SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()
            }
        };
    }

    private SourceFourActionResult Good(SourceFourActionResult r)
    {
        Assert.IsTrue(r.Succeeded, r.Fault ?? "success");
        Assert.IsTrue(SourceBattleIdentityRules.Validate(r.State) == null, "identity " + SourceBattleIdentityRules.Validate(r.State));
        return r;
    }

    private SourceBattleFrame Entry(SourceBattleFrame f) => Good(SourceFourEntryAbilities.InitializeEntry(catalog, f, f.Revision)).State;
    private ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f, int[]? moves = null) => f.Slots.Where(c => f.CanAct(c.Slot)).Select(c => new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, moves?[c.Slot.Value] ?? 0, new(catalog.Moves[c.Body!.Pokemon.Moves[moves?[c.Slot.Value] ?? 0].Move].Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : c.Slot.Value ^ 1))).ToImmutableArray();
    private SourceActionSelection Switch(SourceBattleFrame f, int slot, int index) => new(f.Slots[slot].Occupant!.Value, SourceActionKind.Switch, Incoming: new((BattleSide)(slot & 1), index));
    private SourceFourActionResult Begin(SourceBattleFrame f, ImmutableArray<SourceActionSelection>? choices = null) => Good(SourceFourActionTurns.BeginActions(catalog, f, f.Revision, choices ?? Choices(f)));
    private SourceFourActionResult Next(SourceFourActionResult r) => Good(SourceFourActionTurns.Advance(catalog, r.State, r.Continuation!.Value));
    private SourceFourActionResult Choose(SourceFourActionResult r, int slot, int index) => Good(SourceFourActionTurns.ChooseReplacement(catalog, r.State, r.Continuation!.Value, new(slot), new((BattleSide)(slot & 1), index)));
    private SourceFourActionResult Baton(SourceFourActionResult r, int slot, int index) => Good(SourceFourActionTurns.ChooseActionSwitch(catalog, r.State, r.Continuation!.Value, new(slot), new((BattleSide)(slot & 1), index)));
    private SourceBattleFrame Body(SourceBattleFrame f, int slot, WildBattleCombatant b)
    {
        var c = f.Slots[slot];
        var key = c.Member!.Value;
        var party = f.Party(key.Side);
        return f with
        {
            Slots = f.Slots.SetItem(slot, c with { Body = b }),
            Parties = f.Parties.SetItem((int)key.Side, party with { Members = party.Members.SetItem(key.Index, Member(b)) })
        };
    }

    private SourceBattleFrame Hazard(SourceBattleFrame f, BattleSide side, int layers) => f with
    {
        Sides = f.Sides.SetItem((int)side, f.Sides[(int)side] with { Spikes = layers })
    };
    private void Atomic(SourceBattleFrame f, SourceFourActionResult r, string name) => Assert.IsTrue(!r.Succeeded && ReferenceEquals(r.State, f) && r.Events.IsEmpty && r.Trace.IsEmpty, "atomic " + name + " " + r.Fault);
    private (SourceFourActionResult Result, ImmutableArray<SourceBattleEvent> Events) Finish(SourceFourActionResult r)
    {
        var events = r.Events.ToBuilder();
        for (int i = 0; i < 40 && r.State.FourTurn != null && r.State.Outcome == WildBattleOutcome.Ongoing; i++)
        {
            if (r.State.Rewards?.Pending is { } p)
                r = Good(SourceFourRewards.Learn(catalog, r.State, r.Continuation!.Value, new(p.Recipient, p.Moves[0].Level, p.Moves[0].Move, null)));
            else if (r.State.FourTurn.Phase == SourceFourActionPhase.ReplacementsPending)
            {
                var p2 = r.State.PendingSwitches[0];
                r = Choose(r, p2.Slot.Value, p2.LegalPartyIndices[0]);
            }
            else
                r = Next(r);
            events.AddRange(r.Events);
        }

        Assert.IsTrue(r.CompletedCleanTurn || r.State.Outcome != WildBattleOutcome.Ongoing, "completed actual turn or battle");
        turns++;
        return (r, events.ToImmutable());
    }

    private SourceFourActionResult Until(SourceFourActionResult r, Func<SourceFourActionResult, bool> stop)
    {
        for (int i = 0; i < 24 && !stop(r); i++)
            r = Next(r);
        Assert.IsTrue(stop(r), "reached actual boundary");
        return r;
    }

    [TestMethod]
    public void original_layer_fractions_and_minimum_one()
    {
        foreach (int hp in Enumerable.Range(1, 1024))
            foreach (int layer in new[]
            {
                1,
                2,
                3
            }

            )
                Assert.IsTrue(WildBattleSourcePartyEffects.SpikesDamage(hp, layer) == Math.Max(1, hp / (new[] { 8, 6, 4 }[layer - 1])), "source integer fraction");
    }

    [TestMethod]
    public void real_setter_shares_opposing_side_cap_spends_PP_no_hazard_RNG_or_accuracy()
    {
        var f = Entry(Frame());
        for (int layer = 1; layer <= 4; layer++)
        {
            var(r, ev) = Finish(Begin(f));
            f = r.State;
            Assert.IsTrue(f.Sides[1].Spikes == Math.Min(3, layer) && f.Sides[0].Spikes == 0, "side layers");
            Assert.IsTrue(ev.OfType<SourcePpCommandEvent>().Single(x => x.Actor.Value == 0).CommandCost == 1, "base PP");
            var added = ev.OfType<SourceCalledSpikesChangedEvent>().ToArray();
            Assert.IsTrue(added.Length == (layer < 4 ? 1 : 0), "exactly one layer event");
            if (layer < 4)
            {
                Assert.IsTrue(added[0].Sequence < ev.OfType<SourceAttackStringEvent>().Single(x => x.Actor.Value == 0).Sequence, "set before print/PP");
                Assert.IsTrue(ev.OfType<SourceMtMoonVolatileEvent>().Single(x => x.Volatile == "spikes" && x.Reason == "move").Sequence > ev.OfType<SourcePpCommandEvent>().Single(x => x.Actor.Value == 0).Sequence, "Spikes printed result follows actual attack/PP commands");
            }
            else
                Assert.IsTrue(ev.OfType<SourceMoveFailedEvent>().Any(x => x.Reason == "spikes-at-maximum"), "source cap failure");
            Assert.IsTrue(ev.OfType<SourceHpChangedEvent>().Count() == 0, "no damage to existing occupants");
        }

        Assert.IsTrue(f.Slots[0].Body!.Pokemon.Moves[0].CurrentPp == catalog.Moves[Id("SPIKES")].Pp - 4, "four actual PP commands");
    }

    [TestMethod]
    public void two_opposite_Pressure_holders_count_same_side_Pressure_excluded_and_cap_suppresses()
    {
        var f = Entry(Frame([Mon("SKARMORY", "SPIKES"), Mon("LUGIA", "RECOVER"), Mon("LUGIA", "RECOVER"), Mon("LUGIA", "RECOVER")]));
        var begin = Begin(f);
        var r = Until(begin, x => x.Events.OfType<SourceCalledSpikesChangedEvent>().Any());
        Assert.IsTrue(r.Events.OfType<SourcePpCommandEvent>().Single().CommandCost == 3 && r.Trace.IsEmpty, "two opponent Pressure no RNG");
        f = Finish(r).Result.State;
        f = Hazard(f, BattleSide.Opponent, 3);
        r = Until(Begin(f), x => x.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "spikes-at-maximum"));
        Assert.IsTrue(r.Events.OfType<SourcePpCommandEvent>().Single().CommandCost == 1 && r.Trace.IsEmpty, "cap suppresses pressure");
        Finish(r);
    }

    [TestMethod]
    public void voluntary_grounded_Flying_Levitate_Trace_entries_use_actual_incoming_effective_ability()
    {
        foreach (var(species, move, immune)in new[]
        {
            ("DIGLETT", "SAND_ATTACK", false),
            ("GYARADOS", "DRAGON_DANCE", true),
            ("KOFFING", "SMOKESCREEN", true),
            ("PORYGON", "AGILITY", false)
        }

        )
            foreach (var layers in new[]
            {
                1,
                2,
                3
            }

            )
            {
                var incoming = Mon(species, move);
                var f = Entry(Frame(players: [incoming]));
                f = Hazard(f, BattleSide.Player, layers);
                var r = Next(Begin(f, Choices(f).SetItem(0, Switch(f, 0, 2))));
                var hp = r.Events.OfType<SourceHpChangedEvent>().Where(x => x.Move == Id("SPIKES")).ToArray();
                Assert.IsTrue(hp.Length == (immune ? 0 : 1), "immunity " + species);
                var text = r.Events.OfType<SourceMtMoonVolatileEvent>().Where(x => x.Volatile == "spikes" && x.Reason == "entry").ToArray();
                Assert.IsTrue(text.Length == (immune ? 0 : 1) && (immune || text[0].Sequence > hp[0].Sequence), "entry result follows actual HP, immune entry is silent");
                Assert.IsTrue(r.State.Slots[0].Body!.Pokemon.CurrentHp == incoming.Pokemon.CurrentHp - (immune ? 0 : WildBattleSourcePartyEffects.SpikesDamage(incoming.Pokemon.Stats.MaxHp, layers)), "entry HP");
                Assert.IsTrue(r.State.Party(BattleSide.Player).Members[2].Pokemon.CurrentHp == r.State.Slots[0].Body!.Pokemon.CurrentHp, "permanent HP sync");
                Assert.IsTrue(r.State.Slots[0].Registers.Damage is null && r.State.Sides[0].Spikes == layers, "passive damage and persistent layers");
                if (species == "PORYGON")
                    Assert.IsTrue(hp[0].Sequence < r.Events.OfType<SourceAbilityActivatedEvent>().Single(x => x.Ability == "ABILITY_TRACE").Sequence, "Spikes before Trace");
                Finish(r);
            }
    }

    [TestMethod]
    public void two_same_side_actual_switches_each_take_hazard_once_before_queued_moves()
    {
        var f = Entry(Frame(players: [Mon(), Mon()]));
        f = Hazard(f, BattleSide.Player, 2);
        var r = Begin(f, Choices(f).SetItem(0, Switch(f, 0, 2)).SetItem(2, Switch(f, 2, 3)));
        var a = Next(r);
        var b = Next(a);
        Assert.IsTrue(a.Events.OfType<SourceHpChangedEvent>().Single(x => x.Move == Id("SPIKES")).Target.Value == 0 && b.Events.OfType<SourceHpChangedEvent>().Single(x => x.Move == Id("SPIKES")).Target.Value == 2, "side latch cleared per real entrant");
        Finish(b);
    }

    [TestMethod]
    public void Baton_keeps_stages_takes_passive_damage_and_preserves_serialized_continuation()
    {
        var f = Entry(Frame([Mon("GOREBYSS", "AMNESIA,BATON_PASS"), Mon(), Mon(), Mon()], [Mon()]));
        f = Finish(Begin(f)).Result.State;
        f = Hazard(f, BattleSide.Player, 3);
        var r = Until(Begin(f, Choices(f, [1, 0, 0, 0])), x => x.State.FourTurn?.Phase == SourceFourActionPhase.ActionSwitchPending);
        var json = JsonSerializer.Deserialize<SourceBattleFrame>(J(r.State))!;
        var a = Baton(r, 0, 2);
        var b = Good(SourceFourActionTurns.ChooseActionSwitch(catalog, json, r.Continuation!.Value, new(0), new(BattleSide.Player, 2)));
        Assert.IsTrue(J(a) == J(b), "exact restore state events RNG");
        Assert.IsTrue(a.State.Slots[0].Body!.SpecialDefenseStageDelta == 2, "passed stages");
        Assert.IsTrue(a.Events.OfType<SourceHpChangedEvent>().Single().Move == Id("SPIKES"), "entry damage");
        Assert.IsTrue(a.State.Slots[0].Registers.History!.LastResultingMove == Id("NONE") && a.State.Slots[0].Registers.Damage is null, "source NONE after switch passive damage");
        Atomic(a.State, SourceFourActionTurns.ChooseActionSwitch(catalog, a.State, r.Continuation!.Value, new(0), new(BattleSide.Player, 2)), "stale Baton");
        Finish(a);
    }

    [TestMethod]
    public void Roar_entry_damage_precedes_weather_and_invalidates_target_old_action()
    {
        var f = Entry(Frame([Mon("GROWLITHE", "ROAR", 90), Mon("GROWLITHE", "ROAR", 40), Mon(), Mon()], opponents: [Mon("KYOGRE", "CALM_MIND")]));
        f = Hazard(f, BattleSide.Opponent, 3);
        var r = Until(Begin(f), x => x.Events.OfType<SourceForcedOutEvent>().Any());
        var hp = r.Events.OfType<SourceHpChangedEvent>().Single(x => x.Move == Id("SPIKES"));
        Assert.IsTrue(hp.Target.Value == 1 && hp.Sequence < r.Events.OfType<SourcePermanentWeatherChangedEvent>().Single().Sequence, "forced hazard before Drizzle");
        Assert.IsTrue(r.State.Queue.Single(x => x.Slot.Value == 1).Kind == SourceActionKind.Cancelled, "old action cannot migrate");
        Assert.IsTrue(r.Events.OfType<SourceCalledPartyChangedEvent>().Count(x => x.Side == BattleSide.Opponent && x.PartyIndex == 2) == 1, "forced entrant permanent HP reported once");
        Finish(r);
    }

    [TestMethod]
    public void voluntary_hazard_KO_skips_Intimidate_records_incoming_identity_and_waits_until_turn_end()
    {
        var incoming = Mon("GROWLITHE", "LEER", hp: 1);
        var f = Entry(Frame(players: [incoming]));
        f = Hazard(f, BattleSide.Player, 1);
        var r = Next(Begin(f, Choices(f).SetItem(0, Switch(f, 0, 2))));
        Assert.IsTrue(r.State.Slots[0].Body!.Pokemon.CurrentHp == 0 && r.State.FourTurn!.Defeats.Single().Occupant.Member.Index == 2, "new identity defeated");
        Assert.IsTrue(r.State.DeferredKoVacancyMask == 1 && r.State.PendingSwitches.IsEmpty && r.State.FourTurn!.Phase == SourceFourActionPhase.Actions, "approved delayed KO entry");
        Assert.IsTrue(!r.Events.OfType<SourceAbilityActivatedEvent>().Any() && r.Events.OfType<SourceFaintedEvent>().Count() == 1, "no entry ability after KO");
        var(done, events) = Finish(r);
        Assert.IsTrue(done.State.Slots[0].Member!.Value.Index == 0, "real departing healthy reserve used at end turn");
        Assert.IsTrue(events.OfType<SourceMoveUsedEvent>().Any(x => x.Actor.Value == 2), "partner completes queued action");
    }

    [TestMethod]
    public void Baton_hazard_KO_completes_action_and_gives_partner_its_turn_before_replacement()
    {
        var f = Entry(Frame([Mon("GIRAFARIG", "BATON_PASS"), Mon(), Mon(), Mon()], [Mon("GROUDON", "BULK_UP", hp: 1)]));
        f = Hazard(f, BattleSide.Player, 3);
        var r = Until(Begin(f), x => x.State.FourTurn?.Phase == SourceFourActionPhase.ActionSwitchPending);
        r = Baton(r, 0, 2);
        Assert.IsTrue(r.State.DeferredKoVacancyMask == 1 && r.State.PendingSwitches.IsEmpty && r.State.Field.PermanentWeather is null, "KO before Drought; no premature choice");
        Finish(r);
    }

    [TestMethod]
    public void forced_hazard_KO_settles_incoming_opponent_EXP_once_before_remaining_actions()
    {
        var f = Entry(Frame([Mon("GROWLITHE", "ROAR", 90), Mon(), Mon(), Mon()], opponents: [Mon("GROUDON", "BULK_UP", hp: 1)]));
        f = Hazard(f, BattleSide.Opponent, 3);
        var r = Until(Begin(f), x => x.Events.OfType<SourceForcedOutEvent>().Any());
        Assert.IsTrue(r.State.FourTurn!.Defeats.Single().Occupant.Member.Index == 2 && r.State.FourTurn.Phase == SourceFourActionPhase.RewardsPending, "reward new opponent identity");
        Assert.IsTrue(r.State.Field.PermanentWeather is null && r.Events.OfType<SourceFaintedEvent>().Count() == 1, "no Drought and exactly one faint");
        var(done, ev) = Finish(r);
        Assert.IsTrue((done.State.Rewards!.CompletedOpponentMask & 4) != 0 && ev.OfType<SourceOpponentRewardCompletedEvent>().Count(x => x.Defeated.Member.Index == 2) == 1, "incoming reward once");
    }

    [TestMethod]
    public void end_turn_hazard_KO_chain_preserves_residual_cursor_and_takes_distinct_entrants_once()
    {
        var doomed = Mon(hp: 1)with
        {
            Status = WildBattleStatus.Poison
        };
        var f = Entry(Frame([doomed, Mon(), Mon(), Mon()], [Mon("SLAKOTH", "AMNESIA", hp: 1), Mon()]));
        f = Hazard(f, BattleSide.Player, 3);
        f = f with
        {
            Field = new()
            {
                RainTurnsRemaining = 3
            }
        };
        var r = Until(Begin(f), x => x.State.FourTurn?.Phase == SourceFourActionPhase.ReplacementsPending);
        var cursor = r.State.FourTurn!.EndTurn;
        Assert.IsTrue(r.State.Field.RainTurnsRemaining == 2, "one ordinary residual pass");
        r = Choose(r, 0, 2);
        Assert.IsTrue(r.State.Slots[0].Body!.Pokemon.CurrentHp == 0 && !r.State.Slots[0].Body!.TruantLoafing && r.State.PendingSwitches[0].LegalPartyIndices.SequenceEqual(new[] { 3 }), "entry KO before Truant then next actual reserve");
        Assert.IsTrue(r.State.FourTurn!.EndTurn == cursor && r.State.Field.RainTurnsRemaining == 2, "same immutable residual cursor");
        var restored = JsonSerializer.Deserialize<SourceBattleFrame>(J(r.State))!;
        var a = Choose(r, 0, 3);
        var b = Good(SourceFourActionTurns.ChooseReplacement(catalog, restored, r.Continuation!.Value, new(0), new(BattleSide.Player, 3)));
        Assert.IsTrue(J(a) == J(b), "serialized chain exact");
        var done = Finish(a).Result;
        Assert.IsTrue(done.State.Field.RainTurnsRemaining == 2 && done.State.Turn == 1 && done.State.Sides[0].Spikes == 3, "no residual replay and hazards persist");
    }

    [TestMethod]
    public void hazard_KO_of_final_side_member_ends_battle_without_extra_entry_or_ability()
    {
        var dead = Mon(hp: 0);
        var low = Mon(hp: 1)with
        {
            Status = WildBattleStatus.Poison
        };
        var f = Frame([low, Mon(), dead, Mon()], [Mon("GROUDON", "BULK_UP", hp: 1)]);
        f = f with
        {
            SourceAbsentMask = 4
        };
        f = Entry(f);
        f = Hazard(f, BattleSide.Player, 3);
        var r = Until(Begin(f), x => x.State.FourTurn?.Phase == SourceFourActionPhase.ReplacementsPending);
        r = Choose(r, 0, 2);
        Assert.IsTrue(r.State.Outcome == WildBattleOutcome.Lost && r.State.FourTurn!.Phase == SourceFourActionPhase.BattleEnded && r.State.PendingSwitches.IsEmpty, "terminal owner outcome");
        Assert.IsTrue(r.Events.OfType<SourceEndedEvent>().Count() == 1 && r.State.Field.PermanentWeather is null, "one end event without Drought");
        Atomic(r.State, SourceFourActionTurns.Advance(catalog, r.State, r.Continuation!.Value), "terminal advance");
    }

    [TestMethod]
    public void initial_hazards_invalid_layers_unsupported_entries_and_counter_overflow_remain_atomic()
    {
        var raw = Hazard(Frame(players: [Mon()]), BattleSide.Player, 1);
        Atomic(raw, SourceFourEntryAbilities.InitializeEntry(catalog, raw, 0), "initial hazards");
        var f = Entry(Frame(players: [Mon()]));
        f = Hazard(f, BattleSide.Player, 1);
        foreach (var bad in new[]
        {
            f with
            {
                EventSequence = long.MaxValue
            },
            f with
            {
                Revision = long.MaxValue
            },
            Hazard(f, BattleSide.Player, 4),
            Body(f, 0, f.Slots[0].Body!with { Rooted = true })
        }

        )
            Atomic(bad, SourceFourActionTurns.BeginActions(catalog, bad, bad.Revision, Choices(bad).SetItem(0, Switch(bad, 0, 2))), "bad state or exhausted counter");
        var r = Begin(f, Choices(f).SetItem(0, Switch(f, 0, 2)));
        var over = r.State with
        {
            EventSequence = long.MaxValue - 2
        };
        Atomic(over, SourceFourActionTurns.Advance(catalog, over, new(over.Revision, over.EventSequence, over.NextAction, over.FourTurn!.Phase)), "entry event overflow");
        var invalidReserve = Mon()with
        {
            HeldItem = "ITEM_SITRUS_BERRY"
        };
        var p = f.Party(BattleSide.Player);
        var unsupported = f with
        {
            Parties = f.Parties.SetItem(0, p with { Members = p.Members.SetItem(2, Member(invalidReserve)) })
        };
        var chosen = Begin(unsupported, Choices(unsupported).SetItem(0, Switch(unsupported, 0, 2)));
        var switched = Next(chosen);
        Assert.IsTrue(switched.State.Slots[0].Body!.HeldItem == "ITEM_SITRUS_BERRY" && switched.State.Slots[0].Body!.Pokemon.CurrentHp == invalidReserve.Pokemon.CurrentHp - WildBattleSourcePartyEffects.SpikesDamage(invalidReserve.Pokemon.Stats.MaxHp, 1), "supported Sitrus reserve takes hazard once and keeps unneeded berry");
        Finish(switched);
    }

    [TestMethod]
    public void real_same_turn_Spikes_then_partner_Roar_runs_one_shared_side_hazard()
    {
        var f = Entry(Frame([Mon("SKARMORY", "SPIKES"), Mon(), Mon("GROWLITHE", "ROAR"), Mon()], opponents: [Mon()]));
        var(done, ev) = Finish(Begin(f));
        var layer = ev.OfType<SourceCalledSpikesChangedEvent>().Single();
        var hp = ev.OfType<SourceHpChangedEvent>().Single(x => x.Move == Id("SPIKES"));
        Assert.IsTrue(layer.Sequence < hp.Sequence && hp.Target.Value == 3 && done.State.Sides[1].Spikes == 1, "actual field move then forced far-side entry");
    }

    [TestMethod]
    public void Spikes_can_fill_opposing_field_while_both_targets_await_approved_delayed_replacements()
    {
        var f = Entry(Frame([Mon("SKARMORY", "SPIKES", 100), Mon(hp: 1), Mon("ELECTRODE", "EXPLOSION", 100), Mon(hp: 1)], opponents: [Mon(), Mon()]));
        var r = Next(Begin(f));
        Assert.IsTrue(r.Events.OfType<SourceFaintedEvent>().Count() == 3 && r.State.Slots[0].Body!.Pokemon.CurrentHp > 0, "actual Explosion KOs both foes and its user");
        while (r.State.FourTurn!.Phase == SourceFourActionPhase.RewardsPending)
            r = Next(r);
        Assert.IsTrue(r.State.DeferredKoVacancyMask == 14 && r.State.PendingSwitches.IsEmpty, "both opponents await end-turn entry");
        r = Until(r, x => x.Events.OfType<SourceCalledSpikesChangedEvent>().Any());
        Assert.IsTrue(r.State.Sides[1].Spikes == 1 && !r.Events.OfType<SourceMoveFailedEvent>().Any(x => x.Reason == "deferred-ko-no-target"), "field has no live-target requirement");
        Finish(r);
    }

    [TestMethod]
    public void opponent_end_turn_hazard_KO_chains_EXP_and_final_win_before_exposing_readiness()
    {
        foreach (bool final in new[]
        {
            false,
            true
        }

        )
        {
            var dead = Mon(hp: 0);
            var low = Mon(hp: 1)with
            {
                Status = WildBattleStatus.Poison
            };
            var reserves = final ? new[]
            {
                Mon("GROUDON", "BULK_UP", hp: 1)
            }

            : new[]
            {
                Mon("GROUDON", "BULK_UP", hp: 1),
                Mon()
            };
            var f = Frame([Mon("SKARMORY", "SPIKES"), low, Mon(), dead], opponents: reserves);
            f = Entry(f with { SourceAbsentMask = 8 });
            f = Hazard(f, BattleSide.Opponent, 3);
            f = f with
            {
                Field = new()
                {
                    RainTurnsRemaining = 3
                }
            };
            var r = Until(Begin(f), x => x.State.FourTurn?.Phase == SourceFourActionPhase.ReplacementsPending);
            var cursor = r.State.FourTurn!.EndTurn;
            Assert.IsTrue(r.State.Rewards!.CompletedOpponentMask == 1, "first opposing death already rewarded");
            r = Choose(r, 1, 2);
            Assert.IsTrue(r.State.FourTurn!.Phase == SourceFourActionPhase.RewardsPending && r.State.FourTurn.Defeats.Single().Occupant.Member.Index == 2, "new entry death rewards its own member");
            Assert.IsTrue(r.State.Outcome == WildBattleOutcome.Ongoing && !r.Events.OfType<SourceEndedEvent>().Any(), "EXP precedes terminal outcome");
            Assert.IsTrue(r.State.Field.PermanentWeather is null && r.State.FourTurn.EndTurn == cursor, "no Drought or residual cursor replay");
            var(done, ev) = Finish(r);
            Assert.IsTrue(done.State.Rewards!.CompletedOpponentMask == 5 && ev.OfType<SourceOpponentRewardCompletedEvent>().Count(x => x.Defeated.Member.Index == 2) == 1, "no duplicate hazard EXP");
            Assert.IsTrue(final ? done.State.Outcome == WildBattleOutcome.Won && ev.OfType<SourceEndedEvent>().Count() == 1 : done.CompletedCleanTurn && done.State.Slots[1].Member!.Value.Index == 3, "correct exhaustion or final surviving entry");
            Assert.IsTrue(done.State.Field.RainTurnsRemaining == 2, "rain ticks once across chain");
        }
    }

    [TestMethod]
    public void incoming_Trace_takes_hazard_before_copying_opponent_Levitate()
    {
        var incoming = Mon("PORYGON", "AGILITY");
        var f = Entry(Frame([Mon("SKARMORY", "SPIKES"), Mon("KOFFING", "TACKLE"), Mon(), Mon("KOFFING", "TACKLE")], [incoming]));
        f = Hazard(f, BattleSide.Player, 2);
        var r = Next(Begin(f, Choices(f).SetItem(0, Switch(f, 0, 2))));
        Assert.IsTrue(r.State.Slots[0].Body!.Pokemon.CurrentHp == incoming.Pokemon.CurrentHp - incoming.Pokemon.Stats.MaxHp / 6, "Trace has no pre-entry copied immunity");
        Assert.IsTrue(WildBattleEffective.Ability(r.State.Slots[0].Body!) == "ABILITY_LEVITATE" && r.Events.OfType<SourceHpChangedEvent>().Single().Sequence < r.Events.OfType<SourceCalledOverlayChangedEvent>().Single().Sequence, "then copies actual Levitate");
        Finish(r);
    }

    [TestMethod]
    public void field_setting_preserves_resolved_far_target_for_PP_and_source_move_history()
    {
        var f = Frame([Mon("SKARMORY", "SPIKES"), Mon(hp: 0), Mon(), Mon()]);
        f = Entry(f with { SourceAbsentMask = 2 });
        var r = Until(Begin(f), x => x.Events.OfType<SourceCalledSpikesChangedEvent>().Any());
        Assert.IsTrue(r.Events.OfType<SourcePpCommandEvent>().Single().Target.Value == 3, "source resolved target is retained");
        Assert.IsTrue(r.State.Slots[3].Body!.LastLandedMove == Id("SPIKES") && r.State.Slots[1].Body!.LastLandedMove is null, "only actual target move history changes");
        Finish(r);
    }
}
