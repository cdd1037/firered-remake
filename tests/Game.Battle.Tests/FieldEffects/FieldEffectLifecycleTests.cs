using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;
using Game.Domain.Rules;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Battle.FieldEffects;
[TestClass, TestCategory("Game")]
public sealed class FieldEffectLifecycleTests : FieldEffectFixture
{

    [TestMethod]
    public void eight_authentic_selected_setting_moves_consume_one_PP_and_tick_on_their_setting_turn()
    {
        foreach (var(species, move, effect)in setters)
            foreach (int slot in new[]
            {
                0,
                1,
                2,
                3
            }

            )
            {
                var f = Rebind(Frame(), slot, Mon(species, move));
                var(a, events) = Actions(f);
                var setting = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == effect && e.Reason == "move");
                Assert.IsTrue(setting.Side.Value == slot && setting.Before == 0 && setting.After == 5, "actual setting owner and five turns");
                var pp = events.OfType<SourcePpCommandEvent>().Single(e => e.Actor.Value == slot);
                Assert.IsTrue(pp.Before - pp.After == 1, "exact one setting PP");
                Assert.IsTrue(Timer(a.State, slot & 1, effect) == 5, "side/field mutation during action");
                var r = Next(a);
                Assert.IsTrue(r.CompletedCleanTurn && Timer(r.State, slot & 1, effect) == 4, "first end-turn countdown");
                turns++;
                var key = f.Get(new(slot)).Member!.Value;
                Assert.IsTrue(r.State.Party(key.Side).Members[key.Index].Pokemon.Moves[0].CurrentPp == pp.After, "real member PP synchronized");
            }
    }

    [TestMethod]
    public void same_side_duplicate_settings_fail_after_PP_and_do_not_refresh_five_end_turns_expire()
    {
        foreach (var(species, move, effect)in setters)
        {
            var f = Rebind(Rebind(Frame(), 0, Mon(species, move)), 2, Mon(species, move));
            var(t, events) = Turn(f);
            Assert.IsTrue(events.OfType<SourceMtMoonVolatileEvent>().Count(e => e.Volatile == effect && e.Reason == "move") == 1, "one setting shared across partners");
            Assert.IsTrue(events.OfType<SourceMoveFailedEvent>().Count(e => e.Reason == (effect == "reflect" ? "already-reflecting" : "already-" + effect)) == 1, "duplicate failed");
            for (int i = 1; i < 5; i++)
            {
                (t, events) = Turn(t);
                Assert.IsTrue(Timer(t, 0, effect) == 4 - i, "same weather/side setting never refreshes timer");
            }

            Assert.IsTrue(events.OfType<SourceMtMoonVolatileEvent>().Count(e => e.Volatile == effect && e.Reason == "expired") == 1, "one final expiry");
        }
    }

    [TestMethod]
    public void both_side_protections_expire_in_source_field_order_before_weather_and_battler_residuals()
    {
        var f = Frame()with
        {
            Sides = [new(BattleSide.Player)
            {
                ReflectTurnsRemaining = 1,
                LightScreenTurnsRemaining = 1,
                MistTurnsRemaining = 1,
                SafeguardTurnsRemaining = 1
            }, new(BattleSide.Opponent)
            {
                ReflectTurnsRemaining = 1,
                LightScreenTurnsRemaining = 1,
                MistTurnsRemaining = 1,
                SafeguardTurnsRemaining = 1
            }

            ],
            Field = new()
            {
                SandstormTurnsRemaining = 2
            }
        };
        foreach (var c in f.Slots)
            f = Rebind(f, c.Slot.Value, c.Body!with { Status = WildBattleStatus.Poison });
        var(a, _) = Actions(f);
        var r = Next(a);
        Assert.IsTrue(r.CompletedCleanTurn, "ordered turn complete");
        turns++;
        var timers = r.Events.OfType<SourceMtMoonVolatileEvent>().ToArray();
        Assert.IsTrue(timers.Select(e => $"{e.Volatile}:{e.Side.Value}").SequenceEqual(new[] { "reflect:0", "reflect:1", "light-screen:0", "light-screen:1", "mist:0", "mist:1", "safeguard:0", "safeguard:1", "sandstorm:0" }), "exact field order");
        Assert.IsTrue(timers.Last().Sequence < r.Events.OfType<SourceWeatherDamageEvent>().First().Sequence && r.Events.OfType<SourceWeatherDamageEvent>().Last().Sequence < r.Events.OfType<SourceStatusDamageEvent>().First().Sequence, "field then weather then battlers");
    }

    [TestMethod]
    public void screen_and_permanent_weather_state_validate_after_JSON_unsafe_lifecycles_still_reject()
    {
        var f = Frame()with
        {
            Field = new()
            {
                PermanentWeather = new(SourcePermanentWeatherKind.Rain)
            },
            Sides = [new(BattleSide.Player)
            {
                ReflectTurnsRemaining = 2,
                MistTurnsRemaining = 4
            }, new(BattleSide.Opponent)
            {
                LightScreenTurnsRemaining = 3,
                SafeguardTurnsRemaining = 5
            }

            ]
        };
        var restored = JsonSerializer.Deserialize<SourceBattleFrame>(JsonSerializer.Serialize(f))!;
        Assert.IsTrue(SourceFourActionTurns.ValidateCapability(catalog, restored)is null, "roundtrip capability");
        var a = Turn(f);
        var b = Turn(restored);
        Assert.IsTrue(JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b), "same four-turn result after JSON");
        foreach (var bad in new[]
        {
            f with
            {
                Field = f.Field with
                {
                    RainTurnsRemaining = 1
                }
            },
            f with
            {
                Field = f.Field with
                {
                    PermanentWeather = new((SourcePermanentWeatherKind)99)
                }
            },
            f with
            {
                Field = f.Field with
                {
                    PermanentWeather = new(SourcePermanentWeatherKind.Rain)
                    {
                        RulesVersion = "bad"
                    }
                }
            },
            f with
            {
                SourceMechanicsRulesVersion = "unrecognized"
            },
            f with
            {
                Sides = f.Sides.SetItem(0, f.Sides[0] with { Spikes = 1 })
            },
            f with
            {
                Sides = f.Sides.SetItem(0, f.Sides[0] with { FollowMeTimer = 2, FollowMeTarget = new(2) })
            }
        }

        )
            Atomic(bad);
        foreach (var(species, move)in new[]
        {
            ("CASTFORM", "TACKLE"),
            ("PORYGON", "TACKLE"),
            ("KYOGRE", "CALM_MIND"),
            ("GROUDON", "BULK_UP"),
            ("TYRANITAR", "LEER")
        }

        )
            Atomic(Rebind(Frame(), 2, Mon(species, move)));
    }

    [TestMethod]
    public void Case_944_original_C_setting_commands_field_owner_timers_and_screen_count_vectors()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(TestAssets.GeneratedFile("tests/Game.Battle.Tests/FieldEffects/Fixtures/four-field-vectors.json", "python tests/Game.Battle.Tests/FieldEffects/source_probe.py content/raw/pokefirered/037335f4 tests/Game.Battle.Tests/FieldEffects/Fixtures --fixtures-only")));
        Assert.IsTrue(document.RootElement.GetProperty("sourceCommit").GetString() == catalog.SourceCommit, "exact source commit");
        SourceFieldRegisters Field(int bits, int duration) => bits switch
        {
            1 => new()
            {
                RainTurnsRemaining = duration
            },
            5 => new()
            {
                PermanentWeather = new(SourcePermanentWeatherKind.Rain)
            },
            8 => new()
            {
                SandstormTurnsRemaining = duration
            },
            24 => new()
            {
                PermanentWeather = new(SourcePermanentWeatherKind.Sandstorm)
            },
            32 => new()
            {
                SunTurnsRemaining = duration
            },
            96 => new()
            {
                PermanentWeather = new(SourcePermanentWeatherKind.Sun)
            },
            128 => new()
            {
                HailTurnsRemaining = duration
            },
            _ => new()};
        int Bits(SourceFieldRegisters f) => f.PermanentWeather?.Kind switch
        {
            SourcePermanentWeatherKind.Rain => 5,
            SourcePermanentWeatherKind.Sandstorm => 24,
            SourcePermanentWeatherKind.Sun => 96,
            _ => f.RainTurnsRemaining > 0 ? 1 : f.SandstormTurnsRemaining > 0 ? 8 : f.SunTurnsRemaining > 0 ? 32 : f.HailTurnsRemaining > 0 ? 128 : 0
        };
        var initial = Frame();
        var bindings = BindingFlags.NonPublic | BindingFlags.Instance;
        var type = typeof(SourceFourActionTurns).Assembly.GetType("Game.Domain.Battle.FrameEndTurnContext")!;
        foreach (var row in document.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var x = row.GetProperty("input").EnumerateArray().Select(n => n.GetInt32()).ToArray();
            var y = row.GetProperty("output").EnumerateArray().Select(n => n.GetInt32()).ToArray();
            if (x[0] == 0)
            {
                var(species, move, effect) = setters[x[2]];
                int slot = x[1];
                var f = Rebind(initial, slot, Mon(species, move))with
                {
                    Field = Field(x[3], x[4])
                };
                f = f with
                {
                    Sides = f.Sides.SetItem(slot & 1, new((BattleSide)(slot & 1)) { ReflectTurnsRemaining = x[6], LightScreenTurnsRemaining = x[6], MistTurnsRemaining = x[6], SafeguardTurnsRemaining = x[6] })
                };
                var r = SourceBattleKernel.ResolveAction(catalog, f, new(f.Get(new(slot)).Occupant!.Value, f.Revision, 0, new(slot)));
                Assert.IsTrue(r.Succeeded, r.Fault ?? "oracle setting");
                Assert.IsTrue(r.Trace.IsEmpty, "setting performs no RNG commands");
                Assert.IsTrue(Bits(r.State.Field) == y[0], "setting exact source weather bits");
                Assert.IsTrue(r.Events.OfType<SourceMoveFailedEvent>().Any() == (y[6] != 0), "setting exact source failure");
                if (x[2] < 4)
                    Assert.IsTrue(Timer(r.State, slot & 1, effect) == y[2 + x[2]], "setting exact side timer");
                else if (y[6] == 0)
                    Assert.IsTrue(Timer(r.State, slot & 1, effect) == y[1], "setting exact weather duration");
            }
            else if (x[0] == 1)
            {
                SourceSideRegisters Side(int i, int n) => new((BattleSide)i)
                {
                    ReflectTurnsRemaining = n,
                    LightScreenTurnsRemaining = n,
                    MistTurnsRemaining = n,
                    SafeguardTurnsRemaining = n
                };
                var f = initial with
                {
                    Field = Field(x[3], x[4]),
                    Sides = [Side(0, x[1]), Side(1, x[2])]
                };
                var context = Activator.CreateInstance(type, bindings | BindingFlags.Public, null, [f, catalog], null)!;
                type.BaseType!.GetMethod("FieldEffects", bindings)!.Invoke(context, [catalog, ImmutableArray.Create(new BattlerSlot(0), new(1), new(2), new(3))]);
                var after = (SourceBattleFrame)type.GetProperty("Frame", bindings)!.GetValue(context)!;
                Assert.IsTrue(Bits(after.Field) == y[0], "full source field owner weather expiry");
                Assert.IsTrue(after.Sides[0].ReflectTurnsRemaining == y[2] && after.Sides[1].ReflectTurnsRemaining == y[3] && after.Sides[0].MistTurnsRemaining == y[4] && after.Sides[1].SafeguardTurnsRemaining == y[5], "exact both-side timer decrement");
            }
            else
            {
                bool special = x[6] != 0;
                var actor = Mon("SQUIRTLE", special ? "WATER_GUN" : "TACKLE");
                var target = initial.Get(new(x[4])).Body!;
                int power = catalog.Moves[actor.Pokemon.Moves[0].Move].Power;
                actor = actor with
                {
                    Pokemon = actor.Pokemon with
                    {
                        Stats = actor.Pokemon.Stats with
                        {
                            Attack = x[1] * 50,
                            SpecialAttack = x[1] * 50
                        }
                    }
                };
                target = target with
                {
                    Pokemon = target.Pokemon with
                    {
                        Stats = target.Pokemon.Stats with
                        {
                            Defense = power * 22,
                            SpecialDefense = power * 22
                        }
                    }
                };
                var f = Rebind(Rebind(initial, x[4] ^ 1, actor), x[4], target)with
                {
                    SourceAbsentMask = (byte)x[5],
                    Sides = [new(BattleSide.Player)
                    {
                        ReflectTurnsRemaining = 3,
                        LightScreenTurnsRemaining = 3
                    }, new(BattleSide.Opponent)
                    {
                        ReflectTurnsRemaining = 3,
                        LightScreenTurnsRemaining = 3
                    }

                    ]
                };
                int count = SourceMoveTargeting.CountSourcePresentDefenders(f, new(x[4]));
                Assert.IsTrue(count == y[1], "unchanged source CountAliveMonsInBattle");
                // Scalar arithmetic oracle only: these intentionally edited stats do not claim instance admission.
                if (x[3] == 0)
                    f = f with
                    {
                        Layout = SourceBattleLayout.Singles
                    };
                var d = Damage(f, new(x[4] ^ 1), new(x[4]), x[2] == 2, 100);
                Assert.IsTrue(d.Steps.Single(p => p.Operation == "divide-50").Value == x[1], "source scalar input preserved");
                string operation = special ? "light-screen" : "reflect";
                Assert.IsTrue(x[2] == 2 ? !d.Steps.Any(p => p.Operation == operation) : d.Steps.Single(p => p.Operation == operation).Value == y[0], "unchanged source screen integer arithmetic");
            }

            vectors++;
        }

        Assert.IsTrue(vectors == 944, "nonempty complete original-C vector set");
    }
}
