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
public sealed class WeatherTests : FieldEffectFixture
{

    [TestMethod]
    public void permanent_rain_sandstorm_and_sun_persist_beyond_five_real_four_action_turns()
    {
        foreach (var(ability, kind)in new[]
        {
            ("ABILITY_DRIZZLE", SourcePermanentWeatherKind.Rain),
            ("ABILITY_SAND_STREAM", SourcePermanentWeatherKind.Sandstorm),
            ("ABILITY_DROUGHT", SourcePermanentWeatherKind.Sun)
        }

        )
        {
            var f = Frame()with
            {
                Field = SourceWeatherRules.ApplyEntry(new(), ability)
            };
            for (int i = 0; i < 6; i++)
            {
                var(t, e) = Turn(f);
                f = t;
                Assert.IsTrue(f.Field.PermanentWeather?.Kind == kind, "permanent weather retained");
                Assert.IsTrue(!e.OfType<SourceMtMoonVolatileEvent>().Any(x => x.Volatile is "rain" or "sandstorm" or "sun" or "hail"), "no invented countdown");
                Assert.IsTrue(e.OfType<SourceWeatherDamageEvent>().Count() == (kind == SourcePermanentWeatherKind.Sandstorm ? 4 : 0), "real permanent weather damage");
            }
        }
    }

    [TestMethod]
    public void temporary_weather_moves_replace_different_permanent_weather_and_same_weather_fails()
    {
        foreach (var(species, move, effect)in setters.Skip(4))
            foreach (var kind in Enum.GetValues<SourcePermanentWeatherKind>())
            {
                var f = Rebind(Frame(), 2, Mon(species, move))with
                {
                    Field = new()
                    {
                        PermanentWeather = new(kind)
                    }
                };
                var(a, e) = Actions(f);
                string current = kind == SourcePermanentWeatherKind.Rain ? "rain" : kind == SourcePermanentWeatherKind.Sun ? "sun" : "sandstorm";
                if (effect == current)
                {
                    Assert.IsTrue(a.State.Field.PermanentWeather?.Kind == kind && e.OfType<SourceMoveFailedEvent>().Any(x => x.Actor.Value == 2 && x.Reason == "already-" + effect), "same permanent fails without replacement");
                }
                else
                    Assert.IsTrue(a.State.Field.PermanentWeather is null && Timer(a.State, 0, effect) == 5, "replacement clears permanent and starts five-turn timer");
                Assert.IsTrue(e.OfType<SourcePpCommandEvent>().Single(x => x.Actor.Value == 2).CommandCost == 1, "failed and successful replacement cost actual PP");
            }
    }

    [TestMethod]
    public void Rain_Dish_uses_permanent_rain_at_its_ability_phase_before_poison()
    {
        var dish = Mon("LOTAD", "ABSORB", 1);
        dish = dish with
        {
            Status = WildBattleStatus.Poison,
            Pokemon = dish.Pokemon with
            {
                CurrentHp = dish.Pokemon.Stats.MaxHp - 30
            }
        };
        var f = Rebind(Frame(), 2, dish)with
        {
            Field = new()
            {
                PermanentWeather = new(SourcePermanentWeatherKind.Rain)
            }
        };
        var(a, _) = Actions(f);
        var r = Next(a);
        Assert.IsTrue(r.CompletedCleanTurn, "Rain Dish turn complete");
        turns++;
        Assert.IsTrue(r.Events.OfType<SourceAbilityRecoveredEvent>().Single().Sequence < r.Events.OfType<SourceStatusDamageEvent>().Single().Sequence, "permanent Rain Dish before poison");
    }

    [TestMethod]
    public void permanent_weather_speed_and_suppression_use_all_actual_slot_bodies()
    {
        foreach (var(species, move, kind)in new[]
        {
            ("LOTAD", "GROWL", SourcePermanentWeatherKind.Rain),
            ("ODDISH", "SWEET_SCENT", SourcePermanentWeatherKind.Sun)
        }

        )
        {
            // Only order is exercised: these authentic selected moves need not belong to this action subset.
            var f = Rebind(Frame(), 2, Mon(species, move));
            var committed = SourceBattleIdentityRules.CommitSelections(f, f.Revision, Choices(f));
            Assert.IsTrue(committed.Succeeded, "real queue committed");
            f = committed.State;
            var perm = f with
            {
                Field = new()
                {
                    PermanentWeather = new(kind)
                }
            };
            var temp = f with
            {
                Field = Weather(kind == SourcePermanentWeatherKind.Rain ? "rain" : "sun", 3)
            };
            foreach (var phase in new[]
            {
                WildBattleOrderPhase.Actions,
                WildBattleOrderPhase.EndTurn
            }

            )
            {
                var a = SourceBattleTurnOrder.Resolve(catalog, perm, phase);
                var b = SourceBattleTurnOrder.Resolve(catalog, temp, phase);
                Assert.IsTrue(a.Fault is null && JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b), "permanent and temporary exact speed order/RNG");
                var dry = SourceBattleTurnOrder.Resolve(catalog, f, phase);
                Assert.IsTrue(!a.Order.SequenceEqual(dry.Order), "actual permanent speed boost changes retained order");
            }

            foreach (var(holder, pid, holderMove)in new[]
            {
                ("PSYDUCK", 1u, "SCREECH"),
                ("RAYQUAZA", 0u, "SCARY_FACE")
            }

            )
                foreach (bool living in new[]
                {
                    true,
                    false
                }

                )
                {
                    var cloud = Mon(holder, holderMove, pid);
                    cloud = cloud with
                    {
                        Pokemon = cloud.Pokemon with
                        {
                            CurrentHp = living ? cloud.Pokemon.Stats.MaxHp : 0
                        }
                    };
                    var suppressed = Rebind(perm, 3, cloud)with
                    {
                        SourceAbsentMask = 8,
                        TurnStartAbsentMask = 8
                    };
                    var dry = suppressed with
                    {
                        Field = new()
                    };
                    var a = SourceBattleTurnOrder.Resolve(catalog, suppressed, WildBattleOrderPhase.Actions);
                    var b = SourceBattleTurnOrder.Resolve(catalog, dry, WildBattleOrderPhase.Actions);
                    Assert.IsTrue(SourceWeatherRules.IsSuppressed(suppressed) == living, "living source-absent holder suppression");
                    if (living)
                        Assert.IsTrue(JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b), "suppressed permanent matches dry order");
                }
        }
    }

    [TestMethod]
    public void Cloud_Nine_and_Air_Lock_spectators_suppress_permanent_residual_without_clearing_weather()
    {
        foreach (var(holder, pid, move)in new[]
        {
            ("PSYDUCK", 1u, "SCREECH"),
            ("RAYQUAZA", 0u, "SCARY_FACE")
        }

        )
            foreach (bool living in new[]
            {
                true,
                false
            }

            )
            {
                var cloud = Mon(holder, move, pid);
                cloud = cloud with
                {
                    Pokemon = cloud.Pokemon with
                    {
                        CurrentHp = living ? cloud.Pokemon.Stats.MaxHp : 0
                    }
                };
                var f = Rebind(Frame(), 3, cloud)with
                {
                    SourceAbsentMask = 8,
                    TurnStartAbsentMask = 8,
                    Field = new()
                    {
                        PermanentWeather = new(SourcePermanentWeatherKind.Sandstorm)
                    }
                };
                var(t, e) = Turn(f);
                Assert.IsTrue(e.OfType<SourceWeatherDamageEvent>().Count() == (living ? 0 : 3), "source-absent living suppression, fainted exclusion");
                Assert.IsTrue(t.Field.PermanentWeather?.Kind == SourcePermanentWeatherKind.Sandstorm, "suppression never clears permanent weather");
            }
    }
}
