using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Battle.Switching;
[TestClass, TestCategory("Game")]
public sealed class SwitchingTests
{
    private global::Game.Domain.Pokemon.SourceMechanicsCatalog proof = default !;
    private global::Game.Domain.Pokemon.PokemonCatalog catalog = default !;
    private string reference = default !;
    private string tm = default !;
    private int turns = default !;
    private int switches = default !;
    private int sourceVectors = default !;
    [TestInitialize]
    public void SetUp()
    {
        proof = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
        catalog = proof.Catalog;
        reference = TestAssets.Source;
        tm = File.ReadAllText(Path.Combine(reference, "src/data/pokemon/tmhm_learnsets.h"));
        turns = 0;
        switches = 0;
        sourceVectors = 0;
    }

    private string Json<T>(T x) => JsonSerializer.Serialize(x);
    private MoveId Id(string m) => new("pret:MOVE_" + m);
    private WildBattleCombatant Mon(string species, string moves = "TACKLE", int level = 70, uint pid = 0)
    {
        var mon = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
        foreach (var move in moves.Split(','))
        {
            var block = Regex.Match(tm, @"\[SPECIES_" + species + @"\].*?(?=\[SPECIES_|\z)", RegexOptions.Singleline).Value;
            Assert.IsTrue(catalog.Species[mon.Species].LevelUpMoves.Any(x => x.Move == Id(move) && x.Level <= level) || Regex.IsMatch(block, @"TM\d\d_" + move + @"\)"), "authentic learned move " + species + " " + move);
        }

        return new(mon with { Moves = moves.Split(',').Select(m => new PokemonMoveSlot(Id(m), catalog.Moves[Id(m)].Pp, catalog.Moves[Id(m)].Pp)).ToImmutableArray() });
    }

    private WildBattleCalledPartyMember Member(WildBattleCombatant body) => new(body.Pokemon, body.HeldItem, body.Status);
    private SourceBattleFrame Frame(WildBattleCombatant[] mons, WildBattleCombatant[]? players = null, WildBattleCombatant[]? opponents = null, uint seed = 31) => new()
    {
        Layout = SourceBattleLayout.PresetDoubles,
        KoReplacementTiming = KoReplacementTiming.EndOfTurn,
        SourceMechanicsRulesVersion = SourceBattleKernel.Version,
        BattleKind = WildBattleCalledBattleKind.Trainer,
        Rng = new(seed),
        Parties = [new(BattleSide.Player, new[] { Member(mons[0]), Member(mons[2]) }.Concat((players ?? []).Select(Member)).ToImmutableArray()), new(BattleSide.Opponent, new[] { Member(mons[1]), Member(mons[3]) }.Concat((opponents ?? []).Select(Member)).ToImmutableArray())],
        Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, mons[i])).ToImmutableArray()
    };
    private SourceBattleFrame Plain(WildBattleCombatant[]? players = null, WildBattleCombatant[]? opponents = null) => Frame([Mon("PIDGEY"), Mon("RATTATA"), Mon("SPEAROW", "PECK"), Mon("DODUO", "PECK")], players, opponents);
    private SourceFourActionResult Good(SourceFourActionResult r)
    {
        Assert.IsTrue(r.Succeeded, r.Fault ?? "success");
        Assert.IsTrue(SourceBattleIdentityRules.Validate(r.State) == null, "valid state " + SourceBattleIdentityRules.Validate(r.State));
        switches += r.Events.OfType<SourceCalledPartySwitchedEvent>().Count();
        return r;
    }

    private SourceBattleFrame Entry(SourceBattleFrame f) => Good(SourceFourEntryAbilities.InitializeEntry(catalog, f, f.Revision)).State;
    private ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f, int[]? slots = null, int[]? targets = null) => f.Slots.Where(c => f.CanAct(c.Slot)).Select(c =>
    {
        int index = slots?[c.Slot.Value] ?? 0;
        var move = catalog.Moves[c.Body!.Pokemon.Moves[index].Move];
        return new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, index, new(move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : targets?[c.Slot.Value] ?? (c.Slot.Value ^ 1)));
    }).ToImmutableArray();
    private SourceFourActionResult Begin(SourceBattleFrame f, ImmutableArray<SourceActionSelection> c) => Good(SourceFourActionTurns.BeginActions(catalog, f, f.Revision, c));
    private SourceFourActionResult Next(SourceFourActionResult r) => Good(SourceFourActionTurns.Advance(catalog, r.State, r.Continuation!.Value));
    private (SourceBattleFrame State, ImmutableArray<SourceBattleEvent> Events) Finish(SourceFourActionResult r)
    {
        var events = r.Events.ToBuilder();
        for (int i = 0; i < 12 && r.State.FourTurn != null; i++)
        {
            r = Next(r);
            events.AddRange(r.Events);
        }

        Assert.IsTrue(r.CompletedCleanTurn, "complete four-slot turn");
        turns++;
        return (r.State, events.ToImmutable());
    }

    private void Atomic(SourceBattleFrame f, SourceFourActionResult r, string why) => Assert.IsTrue(!r.Succeeded && ReferenceEquals(f, r.State) && r.Events.IsEmpty && r.Trace.IsEmpty, "atomic " + why + " " + r.Fault);
    private SourceActionSelection Switch(SourceBattleFrame f, int slot, int incoming) => new(f.Slots[slot].Occupant!.Value, SourceActionKind.Switch, Incoming: new((BattleSide)(slot & 1), incoming));
    private SourceFourActionResult Choose(SourceFourActionResult r, int slot, int index) => Good(SourceFourActionTurns.ChooseActionSwitch(catalog, r.State, r.Continuation!.Value, new(slot), new((BattleSide)(slot & 1), index)));
    [TestMethod]
    public void numeric_voluntary_switches_bind_real_reserves_before_every_queued_move()
    {
        var f = Entry(Plain([Mon("GYARADOS", "DRAGON_DANCE"), Mon("GROUDON", "BULK_UP")]));
        var c = Choices(f).SetItem(0, Switch(f, 0, 2)).SetItem(2, Switch(f, 2, 3));
        var r = Begin(f, c);
        Assert.IsTrue(r.State.Queue.Take(2).Select(a => a.Slot.Value).SequenceEqual(new[] { 0, 2 }), "numeric switches first");
        var first = Next(r);
        Assert.IsTrue(first.State.Slots[0].Member!.Value.Index == 2 && first.State.Slots[0].EntrySerial == 2, "actual first identity");
        Assert.IsTrue(first.Events.OfType<SourceStatChangedEvent>().Count() == 2, "incoming Intimidate reaches both opposing positions");
        var second = Next(first);
        Assert.IsTrue(second.State.Field.PermanentWeather?.Kind == SourcePermanentWeatherKind.Sun, "actual Groudon entry weather");
        var(after, events) = Finish(second);
        Assert.IsTrue(after.Slots[0].Member!.Value.Index == 2 && after.Slots[2].Member!.Value.Index == 3, "both actual party indices persist");
        Assert.IsTrue(!events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Side == BattleSide.Player), "incoming occupants cannot borrow outgoing actions");
        Assert.IsTrue(events.OfType<SourceHpChangedEvent>().Any(e => e.Target.Value == 0), "remaining attacks hit new occupant in selected position");
    }

    [TestMethod]
    public void side_reservation_exclusions_and_stale_switch_calls_reject_atomically()
    {
        var f = Entry(Plain([Mon("GYARADOS", "DRAGON_DANCE")]));
        var c = Choices(f);
        Atomic(f, SourceFourActionTurns.BeginActions(catalog, f, f.Revision, c.SetItem(0, Switch(f, 0, 2)).SetItem(2, Switch(f, 2, 2))), "duplicate reserve");
        Atomic(f, SourceFourActionTurns.BeginActions(catalog, f, f.Revision, c.SetItem(0, Switch(f, 0, 1))), "active partner");
        var r = Begin(f, c.SetItem(0, Switch(f, 0, 2)));
        var done = Next(r);
        Atomic(done.State, SourceFourActionTurns.Advance(catalog, done.State, r.Continuation!.Value), "old action cursor");
        Finish(done);
    }

    [TestMethod]
    public void Baton_Pass_pauses_the_actual_action_and_resumes_exact_serialized_continuation()
    {
        var f = Entry(Frame([Mon("GIRAFARIG", "CALM_MIND,BATON_PASS"), Mon("RATTATA"), Mon("SPEAROW", "PECK"), Mon("DODUO", "PECK")], [Mon("GYARADOS", "DRAGON_DANCE")]));
        var(boosted, _) = Finish(Begin(f, Choices(f)));
        Assert.IsTrue(boosted.Slots[0].Body!.SpecialAttackStageDelta == 1, "actual Calm Mind stage");
        var r = Begin(boosted, Choices(boosted, [1, 0, 0, 0]));
        r = Next(r);
        Assert.IsTrue(r.State.FourTurn!.Phase == SourceFourActionPhase.ActionSwitchPending && r.State.NextAction == 1, "Baton boundary before other actions");
        Assert.IsTrue(r.State.Slots[0].Member!.Value.Index == 0 && r.State.PendingSwitches[0].LegalPartyIndices.SequenceEqual(new[] { 2 }), "old identity until choice");
        Assert.IsTrue(r.State.Party(BattleSide.Player).Members[0].Pokemon.Moves[1].CurrentPp == catalog.Moves[Id("BATON_PASS")].Pp - 1, "real outgoing PP");
        Atomic(r.State, SourceFourActionTurns.Advance(catalog, r.State, r.Continuation!.Value), "choice required");
        var restored = JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;
        var a = Choose(r, 0, 2);
        var b = Good(SourceFourActionTurns.ChooseActionSwitch(catalog, restored, r.Continuation!.Value, new(0), new(BattleSide.Player, 2)));
        Assert.IsTrue(Json(a.State) == Json(b.State) && Json(a.Events) == Json(b.Events) && Json(a.Trace) == Json(b.Trace), "exact state/event/RNG resume");
        Assert.IsTrue(a.State.Slots[0].Body!.SpecialAttackStageDelta == 1 && a.State.Slots[0].Body!.Pokemon.AbilityId == "ABILITY_INTIMIDATE", "stages pass actual reserve ability does not copy");
        Assert.IsTrue(a.State.Slots[0].Body!.LastUsedMove == Id("BATON_PASS") && a.State.Slots[0].Registers.History!.LastResultingMove == Id("NONE"), "Baton move end after entry uses NONE current move");
        Atomic(a.State, SourceFourActionTurns.ChooseActionSwitch(catalog, a.State, r.Continuation!.Value, new(0), new(BattleSide.Player, 2)), "replayed choice");
        Finish(a);
    }

    [TestMethod]
    public void Baton_failure_without_a_reserve_spends_PP_and_completes_the_turn()
    {
        var f = Entry(Frame([Mon("GIRAFARIG", "BATON_PASS"), Mon("RATTATA"), Mon("SPEAROW", "PECK"), Mon("DODUO", "PECK")]));
        var(after, events) = Finish(Begin(f, Choices(f)));
        Assert.IsTrue(events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "no-eligible-reserve"), "source Baton failure");
        Assert.IsTrue(after.PendingSwitches.IsEmpty, "no fabricated choice");
    }

    [TestMethod]
    public void Roar_invalidates_a_slower_queued_Roar_and_enters_an_actual_weather_ability()
    {
        var f = Entry(Frame([Mon("GROWLITHE", "ROAR", 90), Mon("GROWLITHE", "ROAR", 40), Mon("SPEAROW", "PECK"), Mon("DODUO", "PECK")], opponents: [Mon("KYOGRE", "CALM_MIND")]));
        var r = Begin(f, Choices(f));
        while (r.State.Queue[r.State.NextAction].Slot.Value != 0)
            r = Next(r);
        r = Next(r);
        Assert.IsTrue(r.Events.OfType<SourceForcedOutEvent>().Single().Target.Value == 1, "real selected target");
        Assert.IsTrue(r.State.Slots[1].Member!.Value.Index == 2, "forced legal reserve");
        Assert.IsTrue(r.State.Queue.Single(a => a.Slot.Value == 1).Kind == SourceActionKind.Cancelled, "outgoing queued action cancelled");
        Assert.IsTrue(r.State.Field.PermanentWeather?.Kind == SourcePermanentWeatherKind.Rain, "actual entering Drizzle");
        var(_, events) = Finish(r);
        Assert.IsTrue(!events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == 1), "new Kyogre does not execute old Roar");
    }

    [TestMethod]
    public void Whirlwind_works_against_Soundproof_but_Roar_consumes_PP_before_Soundproof()
    {
        foreach (var(species, move, blocked)in new[]
        {
            ("GROWLITHE", "ROAR", true),
            ("PIDGEOT", "WHIRLWIND", false)
        }

        )
        {
            var f = Entry(Frame([Mon(species, move), Mon("MR_MIME", "CONFUSION"), Mon("SPEAROW", "PECK"), Mon("DODUO", "PECK")], opponents: [Mon("RATTATA")]));
            var(_, events) = Finish(Begin(f, Choices(f)));
            Assert.IsTrue(events.OfType<SourceCalledPartySwitchedEvent>().Any() == !blocked, "actual sound distinction " + move);
            Assert.IsTrue(events.OfType<SourcePpCommandEvent>().Any(e => e.Actor.Value == 0), "PP command " + move);
        }
    }

    [TestMethod]
    public void Suction_Cups_prevents_phasing_after_PP_and_before_accuracy()
    {
        var f = Entry(Frame([Mon("GROWLITHE", "ROAR"), Mon("OCTILLERY", "WATER_GUN"), Mon("SPEAROW", "PECK"), Mon("DODUO", "PECK")], opponents: [Mon("RATTATA")]));
        var r = Begin(f, Choices(f));
        while (r.State.Queue[r.State.NextAction].Slot.Value != 0)
            r = Next(r);
        r = Next(r);
        Assert.IsTrue(r.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_SUCTION_CUPS"), "authentic Suction Cups");
        Assert.IsTrue(r.Trace.IsEmpty, "no phasing accuracy or party RNG");
        Finish(r);
    }

    [TestMethod]
    public void trainer_doubles_three_living_threshold_includes_source_absent_and_HP_distinctions()
    {
        var dead = Mon("DODUO", "PECK");
        dead = dead with
        {
            Pokemon = dead.Pokemon with
            {
                CurrentHp = 0
            }
        };
        var f = Entry(Frame([Mon("GROWLITHE", "ROAR"), Mon("RATTATA"), Mon("SPEAROW", "PECK"), dead], opponents: [Mon("PIDGEY")])with { SourceAbsentMask = 8, TurnStartAbsentMask = 8 });
        var(_, events) = Finish(Begin(f, Choices(f)));
        Assert.IsTrue(events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "no-force-out-reserve"), "one legal reserve but fewer than three live fails");
        Assert.IsTrue(!events.OfType<SourceCalledPartySwitchedEvent>().Any(), "no invented forced replacement");
    }

    [TestMethod]
    public void voluntary_traps_inspect_actual_absent_fainted_bodies_and_ally_Magnet_Pull()
    {
        foreach (var(species, move, own, expected)in new[]
        {
            ("WOBBUFFET", "SAFEGUARD", "PIDGEY", "SHADOW_TAG"),
            ("DIGLETT", "SCRATCH", "RATTATA", "ARENA_TRAP")
        }

        )
        {
            var trap = Mon(species, move, pid: species == "DIGLETT" ? 1u : 0u);
            trap = trap with
            {
                Pokemon = trap.Pokemon with
                {
                    CurrentHp = 0
                }
            };
            var f = Entry(Frame([Mon(own), trap, Mon("SPEAROW", "PECK"), Mon("DODUO", "PECK")], [Mon("GYARADOS", "DRAGON_DANCE")])with { SourceAbsentMask = 2, TurnStartAbsentMask = 2 });
            var r = SourceFourActionTurns.BeginActions(catalog, f, f.Revision, Choices(f).SetItem(0, Switch(f, 0, 2)));
            Atomic(f, r, expected);
            Assert.IsTrue(r.Fault!.EndsWith("ABILITY_" + expected), "specific retained trap");
        }

        var ally = Entry(Frame([Mon("MAGNEMITE", "TACKLE"), Mon("RATTATA"), Mon("MAGNEMITE", "TACKLE"), Mon("DODUO", "PECK")], [Mon("GYARADOS", "DRAGON_DANCE")]));
        var result = SourceFourActionTurns.BeginActions(catalog, ally, ally.Revision, Choices(ally).SetItem(0, Switch(ally, 0, 2)));
        Atomic(ally, result, "ally Magnet Pull");
        Assert.IsTrue(result.Fault!.EndsWith("ABILITY_MAGNET_PULL"), "same-side source trap");
    }

    [TestMethod]
    public void actual_Natural_Cure_departure_and_switch_in_Truant_timing()
    {
        var cured = Mon("CHANSEY", "SOFT_BOILED")with
        {
            Status = WildBattleStatus.Poison
        };
        Assert.IsTrue(cured.Pokemon.AbilityId == "ABILITY_NATURAL_CURE", "native Natural Cure");
        var f = Entry(Frame([cured, Mon("RATTATA"), Mon("SPEAROW", "PECK"), Mon("DODUO", "PECK")], [Mon("SLAKOTH", "SCRATCH")]));
        var r = Next(Begin(f, Choices(f).SetItem(0, Switch(f, 0, 2))));
        Assert.IsTrue(r.State.Party(BattleSide.Player).Members[0].Status == 0, "permanent outgoing cure");
        Assert.IsTrue(r.State.Slots[0].Body!.TruantLoafing, "Truant switch-in counter set before abilities");
        var(after, _) = Finish(r);
        Assert.IsTrue(!after.Slots[0].Body!.TruantLoafing, "endturn toggles switch-in Truant before next real turn");
        var(_, events) = Finish(Begin(after, Choices(after)));
        Assert.IsTrue(events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == 0), "new Slakoth acts on first full turn");
    }

    [TestMethod]
    public void slot_three_Baton_into_a_one_move_reserve_uses_current_MOVE_NONE_priority()
    {
        var f = Entry(Frame([Mon("GIRAFARIG", "TACKLE,GROWL,AGILITY,BATON_PASS"), Mon("RATTATA"), Mon("SPEAROW", "PECK"), Mon("DODUO", "PECK")], [Mon("GYARADOS", "DRAGON_DANCE")]));
        var r = Next(Begin(f, Choices(f, [3, 0, 0, 0])));
        Assert.IsTrue(r.State.PendingSwitches.Length == 1, "actual slot-three Baton pending");
        var(after, events) = Finish(Choose(r, 0, 2));
        Assert.IsTrue(after.Turn == 1 && events.OfType<SourceOrderEvent>().Any(e => e.Phase == WildBattleOrderPhase.EndTurn), "short moveset entry completes endturn order");
    }

    [TestMethod]
    public void six_bounded_actual_Roar_actions_match_original_C_level_and_six_slot_selection_RNG()
    {
        foreach (var(seed, level, live, egg)in new[]
        {
            (0u, 90, 15, 0),
            (7u, 90, 15, 0),
            (31u, 90, 7, 0),
            (0u, 20, 15, 0),
            (31u, 20, 15, 0),
            (7u, 90, 5, 0)
        }

        )
        {
            var other = Mon("DODUO", "PECK");
            if ((live & 2) == 0)
                other = other with
                {
                    Pokemon = other.Pokemon with
                    {
                        CurrentHp = 0
                    }
                };
            var reserve = Mon("GYARADOS", "DRAGON_DANCE");
            var excluded = Mon("PIDGEY");
            var f = Frame([Mon("GROWLITHE", "ROAR", level), Mon("KYOGRE", "CALM_MIND", 80), Mon("MAGIKARP", "SPLASH"), other], opponents: [reserve, excluded], seed: seed)with
            {
                SourceAbsentMask = 12,
                TurnStartAbsentMask = 12
            };
            var enemy = f.Party(BattleSide.Opponent);
            f = f with
            {
                Parties = f.Parties.SetItem(1, enemy with { Members = enemy.Members.Select((m, i) => m with { IsEgg = (egg & (1 << i)) != 0, Pokemon = m.Pokemon with { CurrentHp = (live & (1 << i)) != 0 ? m.Pokemon.CurrentHp : 0 } }).ToImmutableArray() }),
                Slots = f.Slots.SetItem(1, f.Slots[1] with { Body = f.Slots[1].Body!with { Pokemon = f.Slots[1].Body!.Pokemon with { CurrentHp = (live & 1) != 0 ? f.Slots[1].Body!.Pokemon.CurrentHp : 0 } } })
            };
            f = Entry(f);
            var r = Begin(f, Choices(f));
            while (r.State.Queue[r.State.NextAction].Slot.Value != 0)
                r = Next(r);
            uint afterAccuracy = unchecked(r.State.Rng.Seed * 1103515245u + 24691u);
            using var vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Fixtures("Game.Battle.Tests/Switching"), "source-switch-vectors.json")));
            var key = string.Join(",", afterAccuracy, live, egg, level, 80);
            var values = vectors.RootElement.GetProperty("rows").GetProperty(key).EnumerateArray().Select(x => x.GetInt64()).ToArray();
            r = Next(r);
            int actual = r.Events.OfType<SourceCalledPartySwitchedEvent>().Select(e => e.IncomingIndex).DefaultIfEmpty(-1).Single();
            Assert.IsTrue(actual == values[0] && r.State.Rng.Seed == (uint)values[1] && r.Trace.Length == values[2] + 1, "exact original C choice/seed/draw count");
            sourceVectors++;
            Finish(r);
        }
    }

    [TestMethod]
    public void Roar_entry_settles_immediately_while_a_different_KO_waits_for_endturn()
    {
        var f = Entry(Frame([Mon("PIDGEY", "TACKLE", 1), Mon("RATTATA", "QUICK_ATTACK", 100), Mon("GROWLITHE", "ROAR", 100), Mon("DODUO", "PECK", 40)], players: [Mon("GYARADOS", "DRAGON_DANCE")], opponents: [Mon("GYARADOS", "DRAGON_DANCE")]));
        var r = Begin(f, Choices(f, targets: [1, 0, 3, 2]));
        while (r.State.Queue[r.State.NextAction].Slot.Value != 2)
            r = Next(r);
        Assert.IsTrue(r.State.IsDeferredKoVacant(new(0)), "real prior player KO still waiting");
        r = Next(r);
        Assert.IsTrue(r.State.Slots[3].Member!.Value.Index == 2 && r.State.IsDeferredKoVacant(new(0)), "Roar entered while separate vacancy retained");
        Assert.IsTrue(r.Events.OfType<SourceAbilityActivatedEvent>().Any(e => e.Side.Value == 3 && e.Ability == "ABILITY_INTIMIDATE"), "new Intimidate settles in Roar action");
        Assert.IsTrue(r.Events.OfType<SourceStatChangedEvent>().Select(e => e.Target.Value).SequenceEqual(new[] { 0, 2 }), "actual entry applies source no-HP target scan before deferred KO replacement");
        while (r.State.FourTurn!.Phase == SourceFourActionPhase.Actions)
            r = Next(r);
        r = Next(r);
        Assert.IsTrue(r.State.FourTurn!.Phase == SourceFourActionPhase.ReplacementsPending, "KO still enters only at endturn");
        r = Good(SourceFourActionTurns.ChooseReplacement(catalog, r.State, r.Continuation!.Value, new(0), new(BattleSide.Player, 2)));
        Finish(r);
    }

    [TestMethod]
    public void voluntary_switch_runs_shared_immunity_after_entry_with_permanent_status_synchronization()
    {
        var incoming = Mon("WAILMER", "WATER_GUN")with
        {
            Status = WildBattleStatus.Burn
        };
        Assert.IsTrue(incoming.Pokemon.AbilityId == "ABILITY_WATER_VEIL", "authentic incoming Water Veil");
        var f = Entry(Plain([incoming]));
        var r = Next(Begin(f, Choices(f).SetItem(0, Switch(f, 0, 2))));
        Assert.IsTrue(r.State.Slots[0].Body!.Status == 0 && r.State.Party(BattleSide.Player).Members[2].Status == 0, "actual entry followed by source immunity");
        long enter = r.Events.OfType<SourceCalledPartySwitchedEvent>().Single().Sequence;
        Assert.IsTrue(r.Events.OfType<SourceStatusChangedEvent>().Single().Sequence > enter, "immunity after actual bind");
        Finish(r);
    }

    [TestMethod]
    public void switch_overflow_and_forged_Baton_owner_retain_input_state_events_and_RNG()
    {
        var f = Entry(Plain([Mon("GYARADOS", "DRAGON_DANCE")]));
        f = f with
        {
            Slots = f.Slots.SetItem(0, f.Slots[0] with { EntrySerial = long.MaxValue }),
            EntryAbilities = f.EntryAbilities!with
            {
                AppliedEntries = f.EntryAbilities.AppliedEntries.SetItem(0, new(new(0), new(BattleSide.Player, 0), long.MaxValue))
            }
        };
        var r = Begin(f, Choices(f).SetItem(0, Switch(f, 0, 2)));
        Atomic(r.State, SourceFourActionTurns.Advance(catalog, r.State, r.Continuation!.Value), "entry serial exhausted");
        var b = Entry(Frame([Mon("GIRAFARIG", "BATON_PASS"), Mon("RATTATA"), Mon("SPEAROW", "PECK"), Mon("DODUO", "PECK")], [Mon("GYARADOS", "DRAGON_DANCE")]));
        var pending = Next(Begin(b, Choices(b)));
        var forged = pending.State with
        {
            PendingSwitches = [pending.State.PendingSwitches[0] with
            {
                Invocation = pending.State.PendingSwitches[0].Invocation!with
                {
                    PpOwner = new(2)
                }
            }

            ]
        };
        Atomic(forged, SourceFourActionTurns.ChooseActionSwitch(catalog, forged, pending.Continuation!.Value, new(0), new(BattleSide.Player, 2)), "forged suspended PP owner");
        var max = pending.State with
        {
            EventSequence = long.MaxValue
        };
        Atomic(max, SourceFourActionTurns.ChooseActionSwitch(catalog, max, new(max.Revision, max.EventSequence, max.NextAction, max.FourTurn!.Phase), new(0), new(BattleSide.Player, 2)), "event counter exhausted");
    }

    [TestMethod]
    public void invalid_Spikes_layer_remains_an_atomic_boundary()
    {
        var f = Entry(Plain([Mon("GYARADOS", "DRAGON_DANCE")]));
        f = f with
        {
            Sides = f.Sides.SetItem(0, f.Sides[0] with { Spikes = 4 })
        };
        Atomic(f, SourceFourActionTurns.BeginActions(catalog, f, f.Revision, Choices(f).SetItem(0, Switch(f, 0, 2))), "Spikes capability");
    }
}
