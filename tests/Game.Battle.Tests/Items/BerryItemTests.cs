using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Battle.Items;
[TestClass, TestCategory("Game")]
public sealed class BerryItemTests
{
    private global::Game.Domain.Pokemon.SourceMechanicsCatalog proof = default !;
    private global::Game.Domain.Pokemon.PokemonCatalog catalog = default !;
    private global::Game.Domain.Trainers.SourceTwinsTrainerDefinition definition = default !;
    private int turns = default !;
    private int oracle = default !;
    [TestInitialize]
    public void SetUp()
    {
        proof = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
        catalog = proof.Catalog;
        definition = PretRoute8Twins.Load(TestAssets.Root);
        turns = 0;
        oracle = 0;
    }

    private string Json<T>(T value) => JsonSerializer.Serialize(value);
    private WildBattleCombatant Mon(string species = "POOCHYENA", int level = 30, string? onlyMove = "HOWL")
    {
        var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        if (level < 5 && species == "POOCHYENA" && onlyMove == "HOWL")
            onlyMove = "TACKLE";
        if (onlyMove is not null)
        {
            MoveId move = new("pret:MOVE_" + onlyMove);
            Assert.IsTrue(catalog.Species[p.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level), "natural move " + species + " " + onlyMove);
            p = p with
            {
                Moves = [new(move, catalog.Moves[move].Pp, catalog.Moves[move].Pp)]
            };
        }

        return new(p);
    }

    private WildBattleCalledPartyMember Member(WildBattleCombatant b) => new(b.Pokemon, b.HeldItem, WildBattleTransformationRules.PersistentStatus(b));
    private SourceBattleFrame Frame(WildBattleCombatant[]? bodies = null, WildBattleCombatant[]? players = null, WildBattleCombatant[]? opponents = null)
    {
        var m = bodies ?? [Mon(), Mon(), Mon(), Mon()];
        return new()
        {
            Layout = SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = WildBattleCalledBattleKind.Trainer,
            Rng = new(31),
            Parties = [new(BattleSide.Player, new[] { Member(m[0]), Member(m[2]) }.Concat((players ?? []).Select(Member)).ToImmutableArray()), new(BattleSide.Opponent, new[] { Member(m[1]), Member(m[3]) }.Concat((opponents ?? []).Select(Member)).ToImmutableArray())],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, m[i])).ToImmutableArray()
        };
    }

    private SourceFourActionResult Good(SourceFourActionResult r)
    {
        Assert.IsTrue(r.Succeeded, r.Fault ?? "success");
        Assert.IsTrue(SourceBattleIdentityRules.Validate(r.State)is null, "identity: " + SourceBattleIdentityRules.Validate(r.State));
        return r;
    }

    private SourceBattleFrame Entry(SourceBattleFrame f) => Good(SourceFourEntryAbilities.InitializeEntry(catalog, f, f.Revision)).State;
    private ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f) => f.Slots.Where(c => f.CanAct(c.Slot)).Select(c =>
    {
        var move = catalog.Moves[c.Body!.Pokemon.Moves[0].Move];
        return new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot : c.Slot.Opposite);
    }).ToImmutableArray();
    private SourceFourActionResult Begin(SourceBattleFrame f, ImmutableArray<SourceActionSelection>? choices = null) => Good(SourceFourActionTurns.BeginActions(catalog, f, f.Revision, choices ?? Choices(f)));
    private SourceFourActionResult Next(SourceFourActionResult r) => Good(SourceFourActionTurns.Advance(catalog, r.State, r.Continuation!.Value));
    private SourceActionSelection Switch(SourceBattleFrame f, int slot, int incoming) => new(f.Slots[slot].Occupant!.Value, SourceActionKind.Switch, Incoming: new((BattleSide)(slot & 1), incoming));
    private SourceFourActionResult EndBoundary(SourceBattleFrame f)
    {
        var r = Begin(f);
        while (r.State.FourTurn!.Phase == SourceFourActionPhase.Actions)
            r = Next(r);
        Assert.IsTrue(r.State.FourTurn.Phase == SourceFourActionPhase.EndTurn, "actual end-turn boundary");
        return r;
    }

    private (SourceBattleFrame State, ImmutableArray<SourceBattleEvent> Events) Finish(SourceFourActionResult r)
    {
        var events = r.Events.ToBuilder();
        for (int i = 0; i < 12 && r.State.FourTurn != null; i++)
        {
            r = Next(r);
            events.AddRange(r.Events);
        }

        Assert.IsTrue(r.CompletedCleanTurn, "complete turn");
        turns++;
        return (r.State, events.ToImmutable());
    }

    private void SameResume(SourceFourActionResult r)
    {
        var copy = JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;
        var a = SourceFourActionTurns.Advance(catalog, r.State, r.Continuation!.Value);
        var b = SourceFourActionTurns.Advance(catalog, copy, r.Continuation.Value);
        Assert.IsTrue(Json(a) == Json(b), "exact serialized state/event/RNG continuation");
    }

    [TestMethod]
    public void actual_Twins_accepts_whole_party_with_reserve_Pecha_Meowth_cures_and_consumes_on_slot_zero_switch()
    {
        var reserve = Mon("MEOWTH", 25, null)with
        {
            Status = WildBattleStatus.Poison,
            HeldItem = "ITEM_PECHA_BERRY"
        };
        var party = ImmutableArray.Create(Member(Mon("RATTATA", 30, "TACKLE")), Member(Mon("SPEAROW", 30, "PECK")), Member(reserve));
        var created = SourceTwinsTrainerEncounter.Begin(catalog, definition, party, new(31));
        Assert.IsTrue(created.Succeeded, created.Fault ?? "Twins entry");
        var f = created.State!;
        Assert.IsTrue(f.Party(BattleSide.Player).Members.SequenceEqual(party), "entry preserves actual reserve berry and poison");
        Assert.IsTrue(!created.Events.OfType<SourceHeldItemConsumedEvent>().Any(), "initial entry does not invent item callback");
        var r = Next(Begin(f, Choices(f).SetItem(0, Switch(f, 0, 2))));
        var actual = r.State.Party(BattleSide.Player).Members[2];
        Assert.IsTrue(r.State.Slots[0].Member!.Value.Index == 2 && actual.Pokemon.Personality == reserve.Pokemon.Personality, "actual reserve identity bound");
        Assert.IsTrue(actual.Status == 0 && actual.HeldItem == "ITEM_NONE" && WildBattleSourcePartyEffects.PersistentHeldItem(actual) == "ITEM_NONE", "permanent cure and genuine consumption");
        Assert.IsTrue(r.Events.OfType<SourceHeldItemConsumedEvent>().Single().Item == "ITEM_PECHA_BERRY" && r.State.Slots[0].Position.UsedHeldItem == "ITEM_PECHA_BERRY", "consumption event and position used-item receipt");
        Assert.IsTrue(r.Events.OfType<SourceCalledPartySwitchedEvent>().Single().Sequence < r.Events.OfType<SourceStatusChangedEvent>().Single().Sequence, "item after actual entry");
        SameResume(r);
    }

    [TestMethod]
    public void voluntary_slot_two_Pecha_entry_waits_until_real_move_end_unlike_slot_zero_callback()
    {
        var incoming = Mon("MEOWTH", 25, null)with
        {
            HeldItem = "ITEM_PECHA_BERRY",
            Status = WildBattleStatus.Poison
        };
        var f = Entry(Frame(players: [incoming]));
        var choices = Choices(f);
        int choice = Array.FindIndex(choices.ToArray(), c => c.Actor.Slot.Value == 2);
        var r = Next(Begin(f, choices.SetItem(choice, Switch(f, 2, 2))));
        Assert.IsTrue(r.State.Slots[2].Body!.Status == WildBattleStatus.Poison && r.State.Slots[2].Body!.HeldItem == "ITEM_PECHA_BERRY", "no invented full item scan on ActionSwitch");
        Assert.IsTrue(!r.Events.OfType<SourceHeldItemConsumedEvent>().Any(), "no early consumption");
        r = Next(r);
        Assert.IsTrue(r.Events.OfType<SourceHeldItemConsumedEvent>().Single().Side.Value == 2 && r.State.Party(BattleSide.Player).Members[2].Status == 0, "next actual move-end cure");
        Finish(r);
    }

    [TestMethod]
    public void switch_away_and_back_never_resurrects_a_consumed_berry_and_retains_the_position_receipt()
    {
        var incoming = Mon("MEOWTH", 25, null)with
        {
            HeldItem = "ITEM_PECHA_BERRY",
            Status = WildBattleStatus.Poison
        };
        var f = Entry(Frame(players: [incoming]));
        var(first, _) = Finish(Begin(f, Choices(f).SetItem(0, Switch(f, 0, 2))));
        var(away, outEvents) = Finish(Begin(first, Choices(first).SetItem(0, Switch(first, 0, 0))));
        var(back, inEvents) = Finish(Begin(away, Choices(away).SetItem(0, Switch(away, 0, 2))));
        Assert.IsTrue(back.Slots[0].Member!.Value.Index == 2 && back.Slots[0].Body!.HeldItem == "ITEM_NONE" && back.Party(BattleSide.Player).Members[2].HeldItem == "ITEM_NONE", "actual consumed reserve stays itemless");
        Assert.IsTrue(back.Slots[0].Position.UsedHeldItem == "ITEM_PECHA_BERRY" && !outEvents.Concat(inEvents).OfType<SourceHeldItemConsumedEvent>().Any(), "used-item receipt survives entrants without duplicate consumption");
    }

    [TestMethod]
    public void all_six_status_berries_cure_numerically_at_move_end_and_retain_unrelated_status_or_confusion()
    {
        foreach (var(item, status, confusion)in new[]
        {
            ("CHERI", WildBattleStatus.Paralysis, 0),
            ("CHESTO", 3u, 0),
            ("PECHA", WildBattleSourceFieldEffects.ToxicPoison | 3840u, 0),
            ("RAWST", WildBattleStatus.Burn, 0),
            ("ASPEAR", WildBattleStatus.Freeze, 0),
            ("PERSIM", 0u, 4)
        }

        )
        {
            var body = Mon()with
            {
                HeldItem = "ITEM_" + item + "_BERRY",
                Status = status,
                ConfusionTurnsRemaining = confusion
            };
            var f = Entry(Frame([body, body, body, Mon("POOCHYENA", 50)]));
            var r = Next(Begin(f));
            Assert.IsTrue(r.Events.OfType<SourceHeldItemConsumedEvent>().Select(e => e.Side.Value).SequenceEqual(new[] { 0, 1, 2 }), "numeric item scan " + item);
            foreach (int slot in new[]
            {
                0,
                1,
                2
            }

            )
                Assert.IsTrue(r.State.Slots[slot].Body is { Status: 0, ConfusionTurnsRemaining: 0, HeldItem: "ITEM_NONE" }, "cure " + item + " " + slot);
            Finish(r);
        }

        var other = Mon()with
        {
            HeldItem = "ITEM_PECHA_BERRY",
            Status = WildBattleStatus.Burn,
            ConfusionTurnsRemaining = 3
        };
        var first = Next(Begin(Entry(Frame([other, Mon(), Mon(), Mon("POOCHYENA", 50)]))));
        Assert.IsTrue(first.State.Slots[0].Body!.HeldItem == "ITEM_PECHA_BERRY" && first.State.Slots[0].Body!.Status == WildBattleStatus.Burn && first.State.Slots[0].Body!.ConfusionTurnsRemaining == 3, "wrong-condition berry remains held");
    }

    [TestMethod]
    public void immunity_precedes_berries_and_preserves_the_unneeded_holder_item()
    {
        var immune = Mon("PERSIAN", 30, "SCRATCH")with
        {
            HeldItem = "ITEM_CHERI_BERRY",
            Status = WildBattleStatus.Paralysis
        };
        Assert.IsTrue(immune.Pokemon.AbilityId == "ABILITY_LIMBER", "actual Limber");
        var f = Entry(Frame([Mon(), Mon(), immune, Mon("POOCHYENA", 70)]));
        var r = Next(Begin(f));
        Assert.IsTrue(r.Events.OfType<SourceAbilityActivatedEvent>().Any(e => e.Side.Value == 2 && e.Ability == "ABILITY_LIMBER"), "immunity activated");
        Assert.IsTrue(r.State.Slots[2].Body is { Status: 0, HeldItem: "ITEM_CHERI_BERRY" } && !r.Events.OfType<SourceHeldItemConsumedEvent>().Any(), "ability saves berry");
    }

    [TestMethod]
    public void Oran_uses_floor_half_HP_and_heals_ten_or_caps_only_in_Items1()
    {
        foreach (var(level, above)in new[]
        {
            (30, false),
            (30, true),
            (1, false)
        }

        )
        {
            var holder = Mon(level: level);
            int hp = holder.Pokemon.Stats.MaxHp / 2 + (above ? 1 : 0);
            holder = holder with
            {
                HeldItem = "ITEM_ORAN_BERRY",
                Pokemon = holder.Pokemon with
                {
                    CurrentHp = hp
                }
            };
            var f = Entry(Frame([holder, Mon(), Mon(), Mon()]));
            var r = EndBoundary(f);
            Assert.IsTrue(r.State.Slots[0].Body!.HeldItem == "ITEM_ORAN_BERRY", "no Oran on entry or move-end");
            SameResume(r);
            r = Next(r);
            Assert.IsTrue(r.CompletedCleanTurn, "Oran endturn completes");
            turns++;
            int healed = above ? 0 : Math.Min(10, holder.Pokemon.Stats.MaxHp - hp);
            Assert.IsTrue(r.State.Slots[0].Body!.Pokemon.CurrentHp == hp + healed, "exact HP threshold and cap");
            Assert.IsTrue(r.Events.OfType<SourceHeldItemConsumedEvent>().Count() == (above ? 0 : 1), "consume only when applicable");
            Assert.IsTrue(r.State.Party(BattleSide.Player).Members[0].HeldItem == (above ? "ITEM_ORAN_BERRY" : "ITEM_NONE"), "party item persisted");
        }
    }

    [TestMethod]
    public void crossing_Oran_threshold_from_poison_waits_through_Items2_and_faint_callbacks_until_next_Items1()
    {
        var body = Mon();
        int hp = body.Pokemon.Stats.MaxHp / 2 + 1;
        body = body with
        {
            HeldItem = "ITEM_ORAN_BERRY",
            Status = WildBattleStatus.Poison,
            Pokemon = body.Pokemon with
            {
                CurrentHp = hp
            }
        };
        var first = Next(EndBoundary(Entry(Frame([body, Mon(), Mon(), Mon()]))));
        Assert.IsTrue(first.CompletedCleanTurn, "first turn complete");
        turns++;
        Assert.IsTrue(first.State.Slots[0].Body!.HeldItem == "ITEM_ORAN_BERRY" && !first.Events.OfType<SourceHeldItemConsumedEvent>().Any(), "source does not heal immediately after poison");
        var second = Next(EndBoundary(first.State));
        Assert.IsTrue(second.CompletedCleanTurn, "second turn complete");
        turns++;
        Assert.IsTrue(second.Events.OfType<SourceHeldItemConsumedEvent>().Single().Sequence < second.Events.OfType<SourceStatusDamageEvent>().Single().Sequence, "next turn Oran before poison");
    }

    [TestMethod]
    public void simultaneous_residual_KOs_retain_both_defeat_identities_while_living_Oran_heals_before_burn()
    {
        var holder = Mon()with
        {
            HeldItem = "ITEM_ORAN_BERRY",
            Status = WildBattleStatus.Burn
        };
        holder = holder with
        {
            Pokemon = holder.Pokemon with
            {
                CurrentHp = 1
            }
        };
        var doomed = Mon()with
        {
            Status = WildBattleStatus.Poison
        };
        doomed = doomed with
        {
            Pokemon = doomed.Pokemon with
            {
                CurrentHp = 1
            }
        };
        var r = Next(EndBoundary(Entry(Frame([holder, doomed, Mon(), doomed], opponents: [Mon()]))));
        Assert.IsTrue(r.State.FourTurn!.Phase == SourceFourActionPhase.RewardsPending && r.State.FourTurn.Defeats.Length == 2, "two actual opponent residual KOs wait for rewards");
        Assert.IsTrue(r.State.Slots[0].Body!.Pokemon.CurrentHp == 11 - Math.Max(1, holder.Pokemon.Stats.MaxHp / 8) && r.State.Slots[0].Body!.HeldItem == "ITEM_NONE", "living Oran holder heals then suffers burn");
        Assert.IsTrue(r.Events.OfType<SourceHeldItemConsumedEvent>().Single().Sequence < r.Events.OfType<SourceStatusDamageEvent>().Single(e => e.Side.Value == 0).Sequence, "item before holder residual");
        Assert.IsTrue(r.State.Party(BattleSide.Player).Members[0].HeldItem == "ITEM_NONE" && r.State.Party(BattleSide.Opponent).Members.Take(2).All(m => m.Pokemon.CurrentHp == 0 && m.Status == 0), "both parties synchronized through reward boundary");
        SameResume(r);
    }

    [TestMethod]
    public void weather_KO_prevents_Oran_consumption_and_KO_replacement_keeps_position_item_history()
    {
        var body = Mon()with
        {
            HeldItem = "ITEM_ORAN_BERRY"
        };
        body = body with
        {
            Pokemon = body.Pokemon with
            {
                CurrentHp = 1
            }
        };
        var reserve = Mon("MEOWTH", 25, null)with
        {
            HeldItem = "ITEM_PECHA_BERRY",
            Status = WildBattleStatus.Poison
        };
        var f = Entry(Frame([body, Mon(), Mon(), Mon()], players: [reserve]))with
        {
            Field = new()
            {
                HailTurnsRemaining = 3
            }
        };
        var r = Next(EndBoundary(f));
        Assert.IsTrue(r.State.FourTurn!.Phase == SourceFourActionPhase.ReplacementsPending, "weather KO waits for replacement");
        Assert.IsTrue(r.State.Party(BattleSide.Player).Members[0].HeldItem == "ITEM_ORAN_BERRY" && !r.Events.OfType<SourceHeldItemConsumedEvent>().Any(), "fainted holder does not eat berry");
        r = Good(SourceFourActionTurns.ChooseReplacement(catalog, r.State, r.Continuation!.Value, new(0), new(BattleSide.Player, 2)));
        Assert.IsTrue(r.State.Slots[0].Body is { Status: 0, HeldItem: "ITEM_NONE" } && r.State.Slots[0].Position.UsedHeldItem == "ITEM_PECHA_BERRY", "real KO reserve cured by source slot zero callback");
        SameResume(r);
        Finish(r);
    }

    [TestMethod]
    public void Baton_Pass_slot_two_runs_the_full_numeric_item_phase_after_actual_reserve_entry()
    {
        var passer = Mon("GIRAFARIG", 50, "BATON_PASS");
        var reserve = Mon("MEOWTH", 25, null)with
        {
            HeldItem = "ITEM_PECHA_BERRY",
            Status = WildBattleStatus.Poison
        };
        var f = Entry(Frame([Mon(), Mon(), passer, Mon()], players: [reserve]));
        var r = Begin(f);
        while (r.State.FourTurn!.Phase == SourceFourActionPhase.Actions)
            r = Next(r);
        Assert.IsTrue(r.State.FourTurn.Phase == SourceFourActionPhase.ActionSwitchPending, "Baton actual choice boundary");
        r = Good(SourceFourActionTurns.ChooseActionSwitch(catalog, r.State, r.Continuation!.Value, new(2), new(BattleSide.Player, 2)));
        Assert.IsTrue(r.Events.OfType<SourceHeldItemConsumedEvent>().Single().Side.Value == 2 && r.State.Slots[2].Body!.Status == 0, "full resumed move-end heals nonzero slot");
        Finish(r);
    }

    [TestMethod]
    public void five_authenticated_HOLD_EFFECT_NONE_berries_remain_held_and_current_active_items_are_admitted()
    {
        foreach (string name in new[]
        {
            "SPELON",
            "PAMTRE",
            "WATMEL",
            "DURIN",
            "BELUE"
        }

        )
        {
            var holder = Mon()with
            {
                HeldItem = "ITEM_" + name + "_BERRY",
                Status = WildBattleStatus.Poison
            };
            var f = Entry(Frame([holder, Mon(), Mon(), Mon()]));
            var(after, events) = Finish(Begin(f));
            Assert.IsTrue(after.Slots[0].Body!.HeldItem == holder.HeldItem && !events.OfType<SourceHeldItemConsumedEvent>().Any(), "real inert source berry remains " + name);
        }

        foreach (string item in new[]
        {
            "ITEM_LUM_BERRY",
            "ITEM_LEPPA_BERRY",
            "ITEM_SITRUS_BERRY",
            "ITEM_ENIGMA_BERRY",
            "ITEM_LEFTOVERS",
            "ITEM_CHOICE_BAND"
        }

        )
        {
            var f = Frame([Mon()with { HeldItem = item }, Mon(), Mon(), Mon()]);
            var r = SourceFourActionTurns.BeginActions(catalog, f, f.Revision, Choices(f));
            if (item == "ITEM_ENIGMA_BERRY")
                Assert.IsTrue(!r.Succeeded && ReferenceEquals(r.State, f) && r.Events.IsEmpty && r.Trace.IsEmpty && r.Fault == "source_item_party_receipt", "unconfigured Enigma remains an atomic receipt failure");
            else
                Assert.IsTrue(r.Succeeded, "current active item admitted " + item + ": " + r.Fault);
            var encounter = SourceTwinsTrainerEncounter.Begin(catalog, definition, [Member(Mon()), Member(Mon()), Member(Mon()with { HeldItem = item })], new(31));
            Assert.IsTrue(item == "ITEM_ENIGMA_BERRY" ? !encounter.Succeeded && encounter.State is null : encounter.Succeeded, "reserve item receipt/admission " + item + ": " + encounter.Fault);
        }
    }

    [TestMethod]
    public void Case_35_exact_original_C_berry_cases_match_shared_frame_phase_adapter()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var assembly = typeof(SourceFourActionTurns).Assembly;
        var type = assembly.GetType("Game.Domain.Battle.SourceFrameBerryItems")!;
        var phaseType = assembly.GetType("Game.Domain.Battle.SourceBerryPhase")!;
        var method = type.GetMethod("Apply", flags)!;
        using var vectors = JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile("tests/Game.Battle.Tests/Items/Fixtures/source-berry-vectors.json", "python tests/Game.Battle.Tests/Items/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Items/Fixtures --fixtures-only")));
        Assert.IsTrue(vectors.RootElement.GetProperty("sourceCommit").GetString() == "037335f4c725d7c9aecdac87066f2002b4bd7e14", "source pin");
        Assert.IsTrue(vectors.RootElement.GetProperty("pins").EnumerateObject().Count() == 6, "bounded six-file source proof");
        foreach (var v in vectors.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var mon = Mon();
            mon = mon with
            {
                HeldItem = v.GetProperty("item").GetString()!,
                Status = v.GetProperty("status").GetUInt32(),
                ConfusionTurnsRemaining = v.GetProperty("confusion").GetInt32(),
                Pokemon = mon.Pokemon with
                {
                    CurrentHp = v.GetProperty("hp").GetInt32(),
                    Stats = mon.Pokemon.Stats with
                    {
                        MaxHp = v.GetProperty("maxHp").GetInt32()
                    }
                }
            };
            var f = Frame([Mon(), Mon(), mon, Mon()]);
            var r = (SourceFourActionResult)method.Invoke(null, [f, Enum.ToObject(phaseType, v.GetProperty("phase").GetInt32()), new BattlerSlot(2)])!;
            var after = r.State.Slots[2].Body!;
            bool consumed = v.GetProperty("effect").GetInt32() != 0;
            Assert.IsTrue(after.Status == v.GetProperty("afterStatus").GetUInt32() && after.ConfusionTurnsRemaining == v.GetProperty("afterConfusion").GetInt32(), "exact original status/confusion");
            Assert.IsTrue(after.Pokemon.CurrentHp == mon.Pokemon.CurrentHp + v.GetProperty("healed").GetInt32(), "exact original HP");
            Assert.IsTrue(after.HeldItem == (consumed ? "ITEM_NONE" : mon.HeldItem) && r.Events.OfType<SourceHeldItemConsumedEvent>().Count() == (consumed ? 1 : 0), "source removeitem exact");
            Assert.IsTrue(r.State.Rng == f.Rng && r.Trace.IsEmpty, "item phases consume zero RNG");
            oracle++;
        }

        Assert.IsTrue(oracle == 35, "nonempty exact bounded oracle");
    }
}
