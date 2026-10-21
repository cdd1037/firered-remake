using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Battle.Residuals;
[TestClass, TestCategory("Game")]
public sealed class EndTurnResidualTests
{
    private global::Game.Domain.Pokemon.SourceMechanicsCatalog proof = default !;
    private global::Game.Domain.Pokemon.PokemonCatalog catalog = default !;
    private int oracle = default !;
    private int cleanTurns = default !;
    [TestInitialize]
    public void SetUp()
    {
        proof = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
        catalog = proof.Catalog;
        oracle = 0;
        cleanTurns = 0;
    }

    private string Json<T>(T value) => JsonSerializer.Serialize(value);
    private MoveId Id(string name) => new("pret:MOVE_" + name);
    private WildBattleCombatant Mon(string species, int level, string move, uint personality = 0)
    {
        var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
        return new(p with { Moves = [new(Id(move), catalog.Moves[Id(move)].Pp, catalog.Moves[Id(move)].Pp)] });
    }

    private WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon, m.HeldItem, m.Status);
    private SourceBattleFrame Frame(WildBattleCombatant[]? supplied = null, int reserves = 0)
    {
        var m = supplied ?? [Mon("DROWZEE", 50, "MEDITATE"), Mon("SLOWPOKE", 50, "AMNESIA"), Mon("POOCHYENA", 50, "HOWL"), Mon("RATTATA", 50, "SCARY_FACE")];
        return new()
        {
            Layout = SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = WildBattleCalledBattleKind.Trainer,
            Rng = new(31),
            Parties = [new(BattleSide.Player, new[] { Member(m[0]), Member(m[2]) }.Concat(Enumerable.Range(0, reserves).Select(i => Member(Mon("PIDGEY", 50, "TACKLE", (uint)i)))).ToImmutableArray()), new(BattleSide.Opponent, new[] { Member(m[1]), Member(m[3]) }.Concat(Enumerable.Range(0, reserves).Select(i => Member(Mon("RATTATA", 50, "TAIL_WHIP", (uint)i)))).ToImmutableArray())],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, m[i])).ToImmutableArray()
        };
    }

    private ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f, int[]? targets = null) => f.Slots.Where(c => f.CanAct(c.Slot)).Select(c => new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, new(targets?[c.Slot.Value] ?? (c.Slot.Value == 3 ? 0 : c.Slot.Value ^ 1)))).ToImmutableArray();
    private SourceFourActionResult Success(SourceFourActionResult r)
    {
        Assert.IsTrue(r.Succeeded, r.Fault ?? "accepted");
        Assert.IsTrue(SourceBattleIdentityRules.Validate(r.State)is null, "valid output " + SourceBattleIdentityRules.Validate(r.State));
        return r;
    }

    private SourceFourActionResult Begin(SourceBattleFrame f, int[]? targets = null) => Success(SourceFourActionTurns.BeginActions(catalog, f, f.Revision, Choices(f, targets)));
    private SourceFourActionResult Next(SourceFourActionResult r) => Success(SourceFourActionTurns.Advance(catalog, r.State, r.Continuation!.Value));
    private void Rejected(SourceBattleFrame f, SourceFourActionResult r, string label)
    {
        Assert.IsTrue(!r.Succeeded && ReferenceEquals(f, r.State) && r.Events.IsEmpty && r.Trace.IsEmpty, label + " atomic fault: " + r.Fault);
    }

    private SourceBattleFrame RebindBody(SourceBattleFrame f, int slot, WildBattleCombatant body)
    {
        var c = f.Slots[slot];
        var key = c.Member!.Value;
        var party = f.Parties[(int)key.Side];
        return f with
        {
            Slots = f.Slots.SetItem(slot, c with { Body = body }),
            Parties = f.Parties.SetItem((int)key.Side, party with { Members = party.Members.SetItem(key.Index, Member(body)) })
        };
    }

    private SourceFourActionResult EndBoundary(SourceBattleFrame f)
    {
        var r = Begin(f);
        while (r.State.FourTurn!.Phase == SourceFourActionPhase.Actions)
            r = Next(r);
        Assert.IsTrue(r.State.FourTurn.Phase == SourceFourActionPhase.EndTurn, "reached actual four action boundary");
        return r;
    }

    private void RoundTrip(SourceFourActionResult r)
    {
        var restored = JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;
        Assert.IsTrue(SourceBattleIdentityRules.Validate(restored)is null, "restored frame validates");
        var a = SourceFourActionTurns.Advance(catalog, r.State, r.Continuation!.Value);
        var b = SourceFourActionTurns.Advance(catalog, restored, r.Continuation.Value);
        Assert.IsTrue(Json(a) == Json(b), "exact state event RNG and fault after JSON resume");
    }

    [TestMethod]
    public void four_actual_actions_then_poison_burn_and_Toxic_in_retained_order()
    {
        var f = Frame();
        f = RebindBody(f, 0, f.Slots[0].Body!with { Status = WildBattleStatus.Poison });
        f = RebindBody(f, 1, f.Slots[1].Body!with { Status = WildBattleStatus.Burn });
        f = RebindBody(f, 2, f.Slots[2].Body!with { Status = WildBattleSourceFieldEffects.ToxicPoison });
        var r = EndBoundary(f);
        RoundTrip(r);
        r = Next(r);
        Assert.IsTrue(r.CompletedCleanTurn, "status turn reaches Ready");
        cleanTurns++;
        var damage = r.Events.OfType<SourceStatusDamageEvent>().ToArray();
        Assert.IsTrue(damage.Length == 3, "three actual residual commands");
        var order = r.Events.OfType<SourceOrderEvent>().Single().Order;
        Assert.IsTrue(damage.Select(x => x.Side).SequenceEqual(order.Where(x => x.Value < 3)), "fixed battler order");
        foreach (var e in damage)
        {
            var max = f.Get(e.Side).Body!.Pokemon.Stats.MaxHp;
            Assert.IsTrue(e.Damage == Math.Max(1, max / (e.Side.Value == 2 ? 16 : 8)), "source integer floor");
            Assert.IsTrue(r.State.Get(e.Side).Body!.Pokemon.CurrentHp == e.HpAfter, "slot mutation");
            Assert.IsTrue(r.State.Party(e.Side.Side).Members[r.State.Get(e.Side).Member!.Value.Index].Status == (e.Side.Value == 2 ? WildBattleSourceFieldEffects.ToxicPoison : r.State.Get(e.Side).Body!.Status), "persistent status excludes battle-only Toxic counter");
        }

        Assert.IsTrue(r.State.Slots[2].Body!.Status == (WildBattleSourceFieldEffects.ToxicPoison | 256u), "Toxic counter increments");
    }

    [TestMethod]
    public void temporary_damaging_weather_precedes_all_four_battler_poison_commands()
    {
        foreach (var weather in new[]
        {
            "sandstorm",
            "hail"
        }

        )
        {
            var f = Frame();
            foreach (var c in f.Slots)
                f = RebindBody(f, c.Slot.Value, c.Body!with { Status = WildBattleStatus.Poison });
            f = f with
            {
                Field = weather == "sandstorm" ? new()
                {
                    SandstormTurnsRemaining = 2
                }

                : new()
                {
                    HailTurnsRemaining = 2
                }
            };
            var r = Next(EndBoundary(f));
            Assert.IsTrue(r.CompletedCleanTurn, "weather and poison turn complete");
            cleanTurns++;
            var w = r.Events.OfType<SourceWeatherDamageEvent>().ToArray();
            var p = r.Events.OfType<SourceStatusDamageEvent>().ToArray();
            Assert.IsTrue(w.Length == 4 && p.Length == 4 && w.Max(x => x.Sequence) < p.Min(x => x.Sequence), "field before battlers across all four slots");
            Assert.IsTrue(w.Select(x => x.Side).SequenceEqual(r.Events.OfType<SourceOrderEvent>().Single().Order), "weather retained order");
            foreach (var e in w)
                Assert.IsTrue(e.Damage == Math.Max(1, f.Get(e.Side).Body!.Pokemon.Stats.MaxHp / 16), "weather exact floor");
        }
    }

    [TestMethod]
    public void weather_expires_before_damage_and_Rain_Dish_while_suppression_still_ticks_timer()
    {
        foreach (var weather in new[]
        {
            "rain",
            "sandstorm",
            "sun",
            "hail"
        }

        )
        {
            var f = Frame();
            f = f with
            {
                Field = weather switch
                {
                    "rain" => new()
                    {
                        RainTurnsRemaining = 1
                    },
                    "sandstorm" => new()
                    {
                        SandstormTurnsRemaining = 1
                    },
                    "sun" => new()
                    {
                        SunTurnsRemaining = 1
                    },
                    _ => new()
                    {
                        HailTurnsRemaining = 1
                    }
                }
            };
            var r = Next(EndBoundary(f));
            Assert.IsTrue(r.CompletedCleanTurn && !r.Events.OfType<SourceWeatherDamageEvent>().Any(), "expiry suppresses damage");
            Assert.IsTrue(r.Events.OfType<SourceMtMoonVolatileEvent>().Single().After == 0, "expiry event");
            cleanTurns++;
        }

        var dish = Mon("LOTAD", 50, "ABSORB", 1);
        Assert.IsTrue(dish.Pokemon.AbilityId == "ABILITY_RAIN_DISH", "real Lotad ability slot");
        dish = dish with
        {
            Pokemon = dish.Pokemon with
            {
                CurrentHp = dish.Pokemon.Stats.MaxHp - 20
            }
        };
        foreach (int turns in new[]
        {
            1,
            2
        }

        )
        {
            var f = RebindBody(Frame(), 2, dish)with
            {
                Field = new()
                {
                    RainTurnsRemaining = turns
                }
            };
            var r = Next(EndBoundary(f));
            Assert.IsTrue(r.Events.OfType<SourceAbilityRecoveredEvent>().Count() == (turns == 2 ? 1 : 0), "Rain Dish observes ticked weather");
            cleanTurns++;
        }
    }

    [TestMethod]
    public void Rain_Dish_precedes_poison_on_same_real_holder_Speed_Boost_occurs_at_ability_phase()
    {
        var dish = Mon("LOTAD", 50, "ABSORB", 1);
        dish = dish with
        {
            Status = WildBattleStatus.Poison,
            Pokemon = dish.Pokemon with
            {
                CurrentHp = dish.Pokemon.Stats.MaxHp - 20
            }
        };
        var boost = Mon("NINJASK", 50, "SCRATCH");
        Assert.IsTrue(boost.Pokemon.AbilityId == "ABILITY_SPEED_BOOST", "real Ninjask ability");
        var f = RebindBody(RebindBody(Frame(), 0, dish), 2, boost)with
        {
            Field = new()
            {
                RainTurnsRemaining = 2
            }
        };
        var r = Next(EndBoundary(f));
        Assert.IsTrue(r.CompletedCleanTurn, "ability turn complete");
        cleanTurns++;
        Assert.IsTrue(r.Events.OfType<SourceAbilityRecoveredEvent>().Single().Sequence < r.Events.OfType<SourceStatusDamageEvent>().Single().Sequence, "heal before poison");
        Assert.IsTrue(r.State.Slots[2].Body!.SpeedStage == 7, "Speed Boost plus one");
        foreach (int counter in new[]
        {
            0,
            1,
            2
        }

        )
        {
            f = RebindBody(Frame(), 2, boost with { FirstTurnCounter = counter });
            r = Next(EndBoundary(f));
            Assert.IsTrue(r.State.Slots[2].Body!.SpeedStage == (counter == 2 ? 6 : 7), "first-turn counter gate");
            cleanTurns++;
        }

        f = RebindBody(Frame(), 2, boost with { SpeedStage = 12 });
        r = Next(EndBoundary(f));
        Assert.IsTrue(!r.Events.OfType<SourceAbilityActivatedEvent>().Any(), "capped Speed Boost does not reveal");
        cleanTurns++;
    }

    [TestMethod]
    public void Shed_Skin_consumes_one_modulo_draw_at_ability_phase_before_status_residual()
    {
        var mon = Mon("DRATINI", 50, "SLAM");
        Assert.IsTrue(mon.Pokemon.AbilityId == "ABILITY_SHED_SKIN", "real Shed Skin");
        mon = mon with
        {
            Status = WildBattleStatus.Poison
        };
        bool cured = false, kept = false;
        for (uint seed = 0; seed < 12; seed++)
        {
            var f = RebindBody(Frame(), 2, mon)with
            {
                Rng = new(seed)
            };
            var r = EndBoundary(f);
            RoundTrip(r);
            r = Next(r);
            var draw = r.Trace.Single(t => t.Purpose == "source-battle.slot-2.shed-skin");
            bool cure = draw.Raw % 3 == 0;
            Assert.IsTrue((r.State.Slots[2].Body!.Status == 0) == cure, "source modulo decision");
            Assert.IsTrue(r.Events.OfType<SourceStatusDamageEvent>().Count() == (cure ? 0 : 1), "cure before residual");
            cured |= cure;
            kept |= !cure;
            cleanTurns++;
        }

        Assert.IsTrue(cured && kept, "both Shed Skin branches covered");
    }

    [TestMethod]
    public void actual_poison_and_burn_move_effects_continue_into_the_shared_residual_scheduler()
    {
        int poisoned = 0, burned = 0;
        for (uint seed = 0; seed < 128 && (poisoned == 0 || burned == 0); seed++)
        {
            if (poisoned == 0)
            {
                var f = RebindBody(Frame(), 0, Mon("VENONAT", 30, "POISON_POWDER"))with
                {
                    Rng = new(seed)
                };
                var r = EndBoundary(f);
                if (r.State.Slots[1].Body!.Status == WildBattleStatus.Poison)
                {
                    var hp = r.State.Slots[1].Body!.Pokemon.CurrentHp;
                    r = Next(r);
                    Assert.IsTrue(r.CompletedCleanTurn && r.Events.OfType<SourceStatusDamageEvent>().Single().Status == WildBattleStatus.Poison, "same-turn poison residual");
                    Assert.IsTrue(r.State.Slots[1].Body!.Pokemon.CurrentHp < hp, "poison after actual chosen move");
                    poisoned++;
                    cleanTurns++;
                }
            }

            if (burned == 0)
            {
                var f = RebindBody(Frame(), 0, Mon("CHARMANDER", 10, "EMBER"))with
                {
                    Rng = new(seed)
                };
                var r = EndBoundary(f);
                if (r.State.Slots[1].Body!.Status == WildBattleStatus.Burn)
                {
                    var hp = r.State.Slots[1].Body!.Pokemon.CurrentHp;
                    r = Next(r);
                    Assert.IsTrue(r.CompletedCleanTurn && r.Events.OfType<SourceStatusDamageEvent>().Single().Status == WildBattleStatus.Burn, "same-turn burn residual");
                    Assert.IsTrue(r.State.Slots[1].Body!.Pokemon.CurrentHp < hp, "burn after actual chosen move");
                    burned++;
                    cleanTurns++;
                }
            }
        }

        Assert.IsTrue(poisoned == 1 && burned == 1, "both admitted status-creating recipes completed");
    }

    [TestMethod]
    public void source_absence_and_living_HP_are_distinct_weather_queries()
    {
        var cloud = Mon("PSYDUCK", 50, "SCRATCH", 1);
        Assert.IsTrue(cloud.Pokemon.AbilityId == "ABILITY_CLOUD_NINE", "actual Cloud Nine species/ability slot");
        foreach (bool living in new[]
        {
            true,
            false
        }

        )
        {
            var f = RebindBody(Frame(), 3, cloud with { Pokemon = cloud.Pokemon with { CurrentHp = living ? cloud.Pokemon.Stats.MaxHp : 0 } })with
            {
                SourceAbsentMask = 8,
                TurnStartAbsentMask = 8,
                Field = new()
                {
                    SandstormTurnsRemaining = 3
                }
            };
            var r = Next(EndBoundary(f));
            Assert.IsTrue(r.Events.OfType<SourceWeatherDamageEvent>().Count() == (living ? 0 : 3), "absence does not suppress CHECK_ON_FIELD, fainted HP does");
            Assert.IsTrue(!r.Events.OfType<SourceWeatherDamageEvent>().Any(e => e.Side.Value == 3), "absent target never weather damaged");
            Assert.IsTrue(r.State.Field.SandstormTurnsRemaining == 2, "suppressed weather still expires");
            cleanTurns++;
        }
    }

    [TestMethod]
    public void residual_player_KO_preserves_damage_registers_until_delayed_boundary_and_requests_real_reserve()
    {
        var f = Frame(reserves: 1);
        f = RebindBody(f, 0, f.Slots[0].Body!with { Status = WildBattleStatus.Poison, Pokemon = f.Slots[0].Body!.Pokemon with { CurrentHp = 1 } });
        var r = EndBoundary(f);
        var c = r.State.Get(new(0));
        var damage = new SourceDamageRegisters(WildBattleRules.CalledMoveKernelRulesVersion, 0, 3, new(1), 0, null);
        r = r with
        {
            State = r.State with
            {
                Slots = r.State.Slots.SetItem(0, c with { Registers = c.Registers with { Damage = damage } })
            }
        };
        r = Next(r);
        Assert.IsTrue(r.State.FourTurn!.Phase == SourceFourActionPhase.ReplacementsPending && r.State.FourTurn.EndTurn!.Stage == WildBattleSourceEndTurnStage.FutureAttacks, "faint boundary before delayed");
        Assert.IsTrue(r.State.Turn == 0 && r.State.PendingSwitches.Single().Slot.Value == 0, "no early Ready or incoming actor");
        Assert.IsTrue(r.State.Slots[0].Registers.Damage is not null, "damage registers retained until final delayed cleanup");
        Assert.IsTrue(r.State.FourTurn.Defeats.Length == 1 && r.State.DeferredKoVacancyMask == 1, "real defeat ledger and vacancy");
        Assert.IsTrue(r.State.Party(BattleSide.Player).Members[0].Status == 0 && r.State.Party(BattleSide.Player).Members[0].Pokemon.CurrentHp == 0, "permanent faint cleanup");
        RoundTrip(r);
        Rejected(r.State, SourceFourActionTurns.Advance(catalog, r.State, r.Continuation!.Value), "entry continuation explicit");
    }

    [TestMethod]
    public void opponent_residual_KO_waits_for_EXP_remaining_normal_residuals_still_finish()
    {
        var f = Frame(reserves: 1);
        f = RebindBody(f, 1, f.Slots[1].Body!with { Status = WildBattleStatus.Poison, Pokemon = f.Slots[1].Body!.Pokemon with { CurrentHp = 1 } });
        f = RebindBody(f, 2, f.Slots[2].Body!with { Status = WildBattleStatus.Burn });
        var r = Next(EndBoundary(f));
        Assert.IsTrue(r.State.FourTurn!.Phase == SourceFourActionPhase.RewardsPending, "real reward boundary");
        Assert.IsTrue(r.Events.OfType<SourceStatusDamageEvent>().Count() == 2, "remaining normal effects occur before EXP phase");
        Assert.IsTrue(r.State.FourTurn.EndTurn!.Stage == WildBattleSourceEndTurnStage.FutureAttacks && r.State.PendingSwitches.IsEmpty, "cursor retains exact future boundary, no replacement");
        Assert.IsTrue(!r.Events.OfType<SourceTurnReadyEvent>().Any() && r.State.Turn == 0, "no Ready");
        RoundTrip(r);
    }

    [TestMethod]
    public void weather_terminal_opponent_loss_preserves_EXP_and_stops_later_normal_residuals()
    {
        var f = Frame();
        foreach (int slot in new[]
        {
            1,
            3
        }

        )
            f = RebindBody(f, slot, f.Slots[slot].Body!with { Pokemon = f.Slots[slot].Body!.Pokemon with { CurrentHp = 1 } });
        f = f with
        {
            Field = new()
            {
                SandstormTurnsRemaining = 3
            }
        };
        var r = Next(EndBoundary(f));
        Assert.IsTrue(r.State.FourTurn!.Phase == SourceFourActionPhase.RewardsPending && r.State.FourTurn.OutcomeAfterRewards == WildBattleOutcome.Won, "terminal win behind real EXP");
        Assert.IsTrue(r.State.FourTurn.Defeats.Length == 2 && r.State.DeferredKoVacancyMask == 10, "both real enemy identities");
        Assert.IsTrue(!r.Events.OfType<SourceEndedEvent>().Any() && !r.Events.OfType<SourceTurnReadyEvent>().Any(), "no premature completion");
        RoundTrip(r);
    }

    [TestMethod]
    public void malformed_persistent_and_delayed_states_reject_without_spending_costs_or_RNG()
    {
        var f = Frame();
        foreach (var bad in new[]
        {
            RebindBody(f, 0, f.Slots[0].Body!with { ConfusionTurnsRemaining = 6 }),
            RebindBody(f, 0, f.Slots[0].Body!with { HeldItem = "ITEM_NOT_A_SOURCE_ITEM" }),
            RebindBody(f, 0, f.Slots[0].Body!with { CalledVolatile = new() { SubstituteHp = -1 } }),
            f with
            {
                Sides = f.Sides.SetItem(0, new(BattleSide.Player) { Spikes = 1 })
            },
            f with
            {
                Field = new()
                {
                    PermanentWeather = new(SourcePermanentWeatherKind.Rain)
                    {
                        RulesVersion = "bad"
                    }
                }
            }
        }

        )
            Rejected(bad, SourceFourActionTurns.BeginActions(catalog, bad, bad.Revision, Choices(bad)), "malformed residual or unowned hazard");
        var wish = new WildBattleCalledWish(1, 0, f.Slots[0].Body!.Pokemon.Species, 43, 0);
        var cell = f.Slots[2];
        f = f with
        {
            Slots = f.Slots.SetItem(2, cell with { Position = cell.Position with { Wish = wish } })
        };
        Rejected(f, SourceFourActionTurns.BeginActions(catalog, f, f.Revision, Choices(f)), "malformed position Wish attribution");
    }

    [TestMethod]
    public void stale_continuations_and_residual_overflow_roll_back_the_entire_end_turn_command()
    {
        var r = EndBoundary(Frame()with { Field = new() { HailTurnsRemaining = 3 } });
        var token = r.Continuation!.Value;
        var done = Next(r);
        Rejected(done.State, SourceFourActionTurns.Advance(catalog, done.State, token), "stale turn continuation");
        foreach (var bad in new[]
        {
            r.State with
            {
                EventSequence = long.MaxValue
            },
            r.State with
            {
                Revision = long.MaxValue
            },
            r.State with
            {
                Rng = r.State.Rng with
                {
                    Cursor = ulong.MaxValue
                }
            }
        }

        )
        {
            var t = new SourceTurnContinuation(bad.Revision, bad.EventSequence, bad.NextAction, bad.FourTurn!.Phase);
            Rejected(bad, SourceFourActionTurns.Advance(catalog, bad, t), "overflow atomic");
        }
    }

    [TestMethod]
    public void malformed_residual_phase_stage_order_and_cursor_cannot_skip_ordinary_work()
    {
        var r = EndBoundary(Frame()with { Field = new() { HailTurnsRemaining = 3 } });
        var f = r.State;
        ImmutableArray<BattlerSlot> order = [new(0), new(1), new(2), new(3)];
        foreach (var stage in Enum.GetValues<WildBattleSourceEndTurnStage>())
        {
            var forged = f with
            {
                FourTurn = f.FourTurn!with
                {
                    EndTurnOrder = order,
                    EndTurn = new(f.Turn, order, stage, stage == WildBattleSourceEndTurnStage.Complete ? 4 : 0, f.Revision, f.EventSequence, WildBattleRules.SourceEndTurnRulesVersion)
                }
            };
            Assert.IsTrue(SourceBattleIdentityRules.Validate(forged) == "source_frame_four_action_continuation", "forged cursor invalid in EndTurn");
            Rejected(forged, SourceFourActionTurns.Advance(catalog, forged, new(forged.Revision, forged.EventSequence, forged.NextAction, SourceFourActionPhase.EndTurn)), "forged residual cannot bypass weather");
        }

        var competingWeather = f with
        {
            Field = new()
            {
                RainTurnsRemaining = 2,
                HailTurnsRemaining = 3
            }
        };
        Rejected(competingWeather, SourceFourActionTurns.Advance(catalog, competingWeather, r.Continuation!.Value), "competing weather fails at residual boundary");
        var loneOrder = f with
        {
            FourTurn = f.FourTurn!with
            {
                EndTurnOrder = order
            }
        };
        Assert.IsTrue(SourceBattleIdentityRules.Validate(loneOrder)is not null, "order without cursor rejected");
        var ko = Frame(reserves: 1);
        ko = RebindBody(ko, 0, ko.Slots[0].Body!with { Status = WildBattleStatus.Poison, Pokemon = ko.Slots[0].Body!.Pokemon with { CurrentHp = 1 } });
        var pause = Next(EndBoundary(ko));
        var saved = pause.State.FourTurn!;
        foreach (var malformed in new[]
        {
            saved with
            {
                EndTurn = null
            },
            saved with
            {
                EndTurn = saved.EndTurn!with
                {
                    Stage = (WildBattleSourceEndTurnStage)(-1)
                }
            },
            saved with
            {
                EndTurn = saved.EndTurn!with
                {
                    NextBattler = 5
                }
            },
            saved with
            {
                EndTurn = saved.EndTurn!with
                {
                    MinimumRevision = pause.State.Revision + 1
                }
            },
            saved with
            {
                EndTurn = saved.EndTurn!with
                {
                    MinimumEventSequence = pause.State.EventSequence + 1
                }
            }
        }

        )
            Assert.IsTrue(SourceBattleIdentityRules.Validate(pause.State with { FourTurn = malformed })is not null, "inconsistent residual pause rejected");
    }

    [TestMethod]
    public void Case_688_independently_compiled_original_C_weather_function_and_status_case_vectors()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(SourceFourActionTurns).Assembly.GetType("Game.Domain.Battle.FrameEndTurnContext")!;
        var initial = EndBoundary(Frame()).State;
        using var doc = JsonDocument.Parse(File.ReadAllBytes(TestAssets.GeneratedFile("tests/Game.Battle.Tests/Residuals/Fixtures/source-residual-vectors.json", "python tests/Game.Battle.Tests/Residuals/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/Residuals/Fixtures --fixtures-only")));
        foreach (var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var x = row.EnumerateArray().Select(n => n.GetInt32()).ToArray();
            var f = initial;
            var body = f.Get(new(2)).Body!;
            int mode = x[0];
            body = body with
            {
                Pokemon = body.Pokemon with
                {
                    Stats = body.Pokemon.Stats with
                    {
                        MaxHp = x[2]
                    },
                    CurrentHp = x[3]
                }
            };
            if (mode == 0)
            {
                string t = x[4] switch
                {
                    5 => "TYPE_ROCK",
                    15 => "TYPE_ICE",
                    _ => "TYPE_NORMAL"
                };
                body = body with
                {
                    CalledOverlay = new()
                    {
                        Type1 = t,
                        Type2 = "TYPE_NORMAL"
                    },
                    Pokemon = body.Pokemon with
                    {
                        AbilityId = x[5] == 8 ? "ABILITY_SAND_VEIL" : "ABILITY_KEEN_EYE"
                    }
                };
                f = RebindBody(f, 2, body);
                var cloud = f.Get(new(3)).Body!;
                f = RebindBody(f, 3, cloud with { Pokemon = cloud.Pokemon with { AbilityId = "ABILITY_CLOUD_NINE", CurrentHp = x[6] } });
                f = f with
                {
                    SourceAbsentMask = (byte)x[7],
                    Field = x[1] == 24 ? new()
                    {
                        SandstormTurnsRemaining = 3
                    }

                    : new()
                    {
                        HailTurnsRemaining = 3
                    }
                };
            }
            else
            {
                body = body with
                {
                    Status = (uint)x[4]
                };
                f = RebindBody(f, 2, body);
            }

            var context = Activator.CreateInstance(type, flags | System.Reflection.BindingFlags.Public, null, [f, catalog], null)!;
            if (mode == 0)
                type.GetMethod("WeatherDamage", flags)!.Invoke(context, [catalog, new BattlerSlot(2), x[1] == 24 ? "sandstorm" : "hail"]);
            else if (x[1] == 5)
                type.GetMethod("ToxicDamage", flags)!.Invoke(context, [new BattlerSlot(2)]);
            else
                type.GetMethod("StatusDamage", flags)!.Invoke(context, [new BattlerSlot(2), (uint)x[4]]);
            var result = (SourceBattleFrame)type.GetProperty("Frame", flags)!.GetValue(context)!;
            var actualEvents = (ImmutableArray<SourceBattleEvent>.Builder)type.GetField("Events", flags)!.GetValue(context)!;
            if (mode == 0)
            {
                var e = actualEvents.OfType<SourceWeatherDamageEvent>().SingleOrDefault();
                Assert.IsTrue((e?.Damage ?? 0) == x[10], "source weather exact damage/absence/HP-query");
                Assert.IsTrue(result.Get(new(2)).Body!.Pokemon.CurrentHp == x[3] - x[10], "weather exact resulting HP");
            }
            else
            {
                var e = actualEvents.OfType<SourceStatusDamageEvent>().SingleOrDefault();
                Assert.IsTrue((e?.Damage ?? 0) == x[11], "source status exact damage");
                Assert.IsTrue(result.Get(new(2)).Body!.Status == (uint)x[10], "source status exact counter");
                Assert.IsTrue((e is null ? 0 : 1) == x[12], "source status callback");
            }

            oracle++;
        }

        Assert.IsTrue(oracle == 688, "full nonempty independent oracle");
    }
}
