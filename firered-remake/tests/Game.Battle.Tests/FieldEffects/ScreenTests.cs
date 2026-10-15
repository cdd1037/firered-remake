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
public sealed class ScreenTests : FieldEffectFixture
{

    [TestMethod]
    public void Reflect_and_Light_Screen_selected_on_one_partner_reduce_later_real_four_slot_damage()
    {
        foreach (var(screen, attack)in new[]
        {
            ("REFLECT", "TACKLE"),
            ("LIGHT_SCREEN", "WATER_GUN")
        }

        )
        {
            var f = Rebind(Rebind(Frame(), 0, Mon("ALAKAZAM", screen)), 1, Mon("SQUIRTLE", attack));
            var(a, e) = Actions(f);
            var set = e.OfType<SourceMtMoonVolatileEvent>().Single(x => x.Reason == "move" && x.Side.Value == 0);
            var hit = e.OfType<SourceHpChangedEvent>().Single(x => x.Actor.Value == 1);
            Assert.IsTrue(hit.Target.Value == 2 && set.Sequence < hit.Sequence, "same-side partner protected after actual setting action");
            var expected = Damage(a.State, new(1), new(2), hit.Critical, hit.RandomPercent);
            Assert.IsTrue(hit.Damage == expected.Damage, "canonical damage uses real side screen/count");
            if (!hit.Critical)
                Assert.IsTrue(expected.Steps.Any(x => x.Operation == (screen == "REFLECT" ? "reflect" : "light-screen")), "screen participates in damage");
            var r = Next(a);
            Assert.IsTrue(r.CompletedCleanTurn, "screen/damage turn reaches Ready");
            turns++;
        }
    }

    [TestMethod]
    public void screen_strength_counts_source_present_bodies_even_at_zero_HP_independently_of_vacancy()
    {
        foreach (string screen in new[]
        {
            "reflect",
            "light-screen"
        }

        )
            foreach (bool absent in new[]
            {
                false,
                true
            }

            )
                foreach (bool living in new[]
                {
                    false,
                    true
                }

                )
                {
                    var f = Rebind(Frame(), 1, Mon("SQUIRTLE", screen == "reflect" ? "TACKLE" : "WATER_GUN"));
                    var partner = f.Get(new(0)).Body!;
                    f = Rebind(f, 0, partner with { Pokemon = partner.Pokemon with { CurrentHp = living ? partner.Pokemon.Stats.MaxHp : 0 } })with
                    {
                        SourceAbsentMask = (byte)(absent ? 1 : 0),
                        TurnStartAbsentMask = (byte)(absent ? 1 : 0),
                        Sides = [new(BattleSide.Player)
                        {
                            ReflectTurnsRemaining = screen == "reflect" ? 4 : 0,
                            LightScreenTurnsRemaining = screen == "light-screen" ? 4 : 0
                        }, new(BattleSide.Opponent)]
                    };
                    var r = SourceBattleKernel.ResolveAction(catalog, f, new(f.Get(new(1)).Occupant!.Value, f.Revision, 0, new(2)));
                    Assert.IsTrue(r.Succeeded, r.Fault ?? "count edge action");
                    var hit = r.Events.OfType<SourceHpChangedEvent>().Single();
                    var expected = Damage(f, new(1), new(2), hit.Critical, hit.RandomPercent);
                    Assert.IsTrue(SourceMoveTargeting.CountSourcePresentDefenders(f, new(2)) == (absent ? 1 : 2) && hit.Damage == expected.Damage, "presence-only screen multiplier");
                }
    }

    [TestMethod]
    public void Mist_and_Safeguard_protect_either_side_during_actual_selected_attacks()
    {
        foreach (int protectedSide in new[]
        {
            0,
            1
        }

        )
        {
            var f = Frame();
            int attacker = protectedSide ^ 1, target = protectedSide == 0 ? 2 : 1;
            f = Rebind(f, attacker, Mon("RATICATE", "SCARY_FACE"));
            f = f with
            {
                Sides = f.Sides.SetItem(protectedSide, new((BattleSide)protectedSide) { MistTurnsRemaining = 2 })
            };
            var(r, e) = Actions(f);
            Assert.IsTrue(e.OfType<SourceMoveFailedEvent>().Any(x => x.Actor.Value == attacker && x.Reason == "mist"), "Mist blocks stat drop on protected side");
            Assert.IsTrue(r.State.Get(new(target)).Body!.SpeedStage == 6, "no speed mutation");
            bool blocked = false;
            for (uint seed = 0; seed < 20 && !blocked; seed++)
            {
                f = Rebind(Frame(), attacker, Mon("VENONAT", "POISON_POWDER"));
                f = f with
                {
                    Rng = new(seed),
                    Sides = f.Sides.SetItem(protectedSide, new((BattleSide)protectedSide) { SafeguardTurnsRemaining = 2 })
                };
                (r, e) = Actions(f);
                blocked = e.OfType<SourceMoveFailedEvent>().Any(x => x.Actor.Value == attacker && x.Reason == "safeguard");
                Assert.IsTrue(r.State.Slots.Where(x => (int)x.Slot.Side == protectedSide).All(x => x.Body!.Status == 0), "no poison through Safeguard");
            }

            Assert.IsTrue(blocked, "Safeguard observed after authentic accuracy gate");
        }
    }
}
