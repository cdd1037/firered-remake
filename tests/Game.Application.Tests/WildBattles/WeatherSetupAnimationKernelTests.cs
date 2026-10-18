using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Previous = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using static FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class WeatherSetupAnimationKernelTests
{
    [TestMethod, DataRow(201), DataRow(240), DataRow(241), DataRow(258)]
    public void NaturalWeatherAndExistingHailUseOwnedMetadataAtEverySlotAndBothSinglesSides(int id)
    {
        var spec = Spec(id); var definition = Mechanics.Catalog.Moves[Move(id)];
        Assert.AreEqual(id, definition.SourceMoveId); Assert.AreEqual(spec.Effect, definition.Effect);
        Assert.AreEqual(0, definition.Power); Assert.AreEqual("MOVE_TARGET_USER", definition.Target);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies(id, actor);
            bodies[actor] = bodies[actor] with { AccuracyStage = 0, Protected = true, CalledVolatile = new() { SubstituteHp = 1 } };
            var frame = Frame(bodies, single); var result = Direct(frame, actor, actor);
            var marker = WeatherMarker(result.Events, id); var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(actor, marker.Side.Value); Assert.AreEqual(0, marker.Before); Assert.AreEqual(5, marker.After);
            Assert.AreEqual("move", marker.Reason); Assert.AreEqual(pp.Sequence + 1, marker.Sequence);
            Assert.AreEqual(5, Timer(result.State.Field, spec.Weather!)); Assert.IsNull(result.State.Field.PermanentWeather);
            Assert.AreEqual(frame.Rng, result.State.Rng); Assert.AreEqual(0, result.Trace.Length);
            Check(result, id, single, marker);
        }
    }

    [TestMethod, DataRow(201), DataRow(240), DataRow(241), DataRow(258)]
    public void ExistingWeatherFailsAndDifferentTemporaryWeatherIsReplacedEvenWhenSuppressed(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (string existing in new[] { "rain", "sandstorm", "sun", "hail" })
        foreach (bool suppressed in new[] { false, true })
        {
            var bodies = Bodies(id); if (suppressed) bodies[1] = Mon("RAYQUAZA", "TWISTER", 70);
            var frame = Frame(bodies, single); frame = frame with { Field = Temporary(frame.Field, existing, 3) };
            Assert.AreEqual(suppressed, SourceWeatherRules.IsSuppressed(frame));
            var result = Direct(frame, 0, 0); bool success = existing != Spec(id).Weather;
            if (success)
            {
                Check(result, id, single, WeatherMarker(result.Events, id));
                foreach (string weather in new[] { "rain", "sandstorm", "sun", "hail" })
                    Assert.AreEqual(weather == Spec(id).Weather ? 5 : 0, Timer(result.State.Field, weather));
            }
            else
            {
                Assert.AreEqual(frame.Field, result.State.Field);
                Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "already-" + existing));
                Assert.IsFalse(result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile == existing));
                Check(result, id, single, null);
            }
            Assert.AreEqual(frame.Rng, result.State.Rng); Assert.AreEqual(0, result.Trace.Length);
        }
    }

    [TestMethod, DataRow(201), DataRow(240), DataRow(241), DataRow(258)]
    public void ActualAbilityCreatedPermanentWeatherIsPreservedOnFailureOrReplacedOnSuccess(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (string existing in new[] { "rain", "sandstorm", "sun" })
        {
            var bodies = Bodies(id);
            bodies[1] = existing switch { "rain" => Mon("KYOGRE", "CALM_MIND", 70),
                "sandstorm" => Mon("TYRANITAR", "SCREECH", 70), _ => Mon("GROUDON", "BULK_UP", 70) };
            var frame = Frame(bodies, single); Assert.IsNotNull(frame.Field.PermanentWeather);
            Assert.AreEqual(existing switch { "rain" => SourcePermanentWeatherKind.Rain, "sandstorm" => SourcePermanentWeatherKind.Sandstorm,
                _ => SourcePermanentWeatherKind.Sun }, frame.Field.PermanentWeather.Kind);
            var result = Direct(frame, 0, 0); bool success = existing != Spec(id).Weather;
            Check(result, id, single, success ? WeatherMarker(result.Events, id) : null);
            if (success)
            {
                Assert.IsNull(result.State.Field.PermanentWeather); Assert.AreEqual(5, Timer(result.State.Field, Spec(id).Weather!));
                Assert.AreEqual(5, result.State.Field.RainTurnsRemaining + result.State.Field.SandstormTurnsRemaining +
                    result.State.Field.SunTurnsRemaining + result.State.Field.HailTurnsRemaining);
            }
            else
            {
                Assert.AreEqual(frame.Field, result.State.Field);
                Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "already-" + existing));
            }
        }
    }

    [TestMethod, DataRow(201), DataRow(240), DataRow(241), DataRow(258)]
    public void ForecastFollowsSetupWhileAirLockAndCloudNineNeverSuppressTheSetupRoot(int id)
    {
        foreach (string suppression in new[] { "none", "air-lock", "cloud-nine" })
        {
            var bodies = Bodies(id); bodies[1] = Mon("CASTFORM", "TACKLE", 50);
            if (suppression == "air-lock") bodies[3] = Mon("RAYQUAZA", "TWISTER", 70);
            if (suppression == "cloud-nine") bodies[3] = Mon("PSYDUCK", "SCRATCH", 50, personality: 1);
            var frame = Frame(bodies); Assert.AreEqual(suppression != "none", SourceWeatherRules.IsSuppressed(frame));
            if (suppression == "cloud-nine") Assert.AreEqual("ABILITY_CLOUD_NINE", WildBattleEffective.Ability(frame.Slots[3].Body!));
            var result = Direct(frame, 0, 0); var marker = WeatherMarker(result.Events, id); Check(result, id, false, marker);
            var expected = suppression != "none" ? SourceCastformForm.Normal : id switch {
                240 => SourceCastformForm.Rainy, 241 => SourceCastformForm.Sunny, 258 => SourceCastformForm.Snowy, _ => SourceCastformForm.Normal };
            Assert.AreEqual(expected, result.State.Slots[1].Body!.CalledOverlay?.CastformForm ?? SourceCastformForm.Normal);
            var forms = result.Events.OfType<SourceCalledOverlayChangedEvent>().ToArray();
            Assert.AreEqual(expected == SourceCastformForm.Normal ? 0 : 1, forms.Length);
            Assert.IsTrue(forms.All(e => e.Sequence > marker.Sequence));
        }
    }

    [TestMethod, DataRow(201), DataRow(240), DataRow(241), DataRow(258)]
    public void CountdownAndResidualWeatherPagesCannotReplayTheDedicatedSetupRoot(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Ordered(Frame(Bodies(id), single), 0, 0); Check(result, id, single, WeatherMarker(result.Events, id));
            int setupCount = 1, damage = 0;
            for (int step = 0; !result.CompletedCleanTurn; step++)
            {
                Assert.IsTrue(step < 32); Assert.IsNotNull(result.Continuation);
                result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
                Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
                string snapshot = JsonSerializer.Serialize(result);
                for (int i = 0; i < result.Events.Length; i++)
                {
                    var cue = Project(result.Events, i, single).Dispatch;
                    if (cue?.SourceMoveId == id) setupCount++;
                }
                damage += result.Events.OfType<SourceWeatherDamageEvent>().Count(e => e.Weather == Spec(id).Weather);
                Assert.AreEqual(snapshot, JsonSerializer.Serialize(result));
            }
            Assert.AreEqual(1, setupCount); Assert.AreEqual(4, Timer(result.State.Field, Spec(id).Weather!));
            Assert.AreEqual(id is 201 or 258, damage > 0);
        }
    }

    [TestMethod, DataRow(201), DataRow(240), DataRow(241), DataRow(258)]
    public void AWeatherNumericIdCannotBypassContradictoryEffectPowerOrTargetMetadata(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Direct(Frame(Bodies(id), single), 0, 0).Events;
            int at = events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());
            var moves = Mechanics.Catalog.Moves; var definition = moves[Move(id)];
            foreach (var wrong in new[] { definition with { Effect = "EFFECT_HIT", Power = 1, Target = "MOVE_TARGET_SELECTED" },
                definition with { Power = 1 }, definition with { Target = "MOVE_TARGET_SELECTED" } })
            {
                Assert.IsTrue(SourceMoveDefinitionRules.IsValid(wrong), "Exercise semantic admission rather than malformed-definition rejection.");
                var metadata = moves.SetItem(Move(id), wrong);
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Previous.Single(events, at), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Previous.Double(events, at), SourceBattleScene.On, metadata).Dispatch);
            }
        }
    }

    [TestMethod, DataRow(201), DataRow(240), DataRow(241), DataRow(258)]
    public void WeatherRejectsForeignWrongDurationPermanentResidualDuplicatedAndAbsentMarkers(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Direct(Frame(Bodies(id), single), 0, 0).Events;
            var marker = WeatherMarker(events, id); int at = events.IndexOf(marker);
            foreach (var malformed in new[] { marker with { Side = new(1) }, marker with { Before = 1 }, marker with { After = 0 },
                marker with { After = 4 }, marker with { After = 6 }, marker with { After = int.MaxValue }, marker with { Reason = "permanent" },
                marker with { Reason = "residual" }, marker with { Volatile = id == 240 ? "sun" : "rain" }, marker with { Turn = marker.Turn + 1 } })
                Deny(events.SetItem(at, malformed), single);
            Deny(events.RemoveAt(at), single);
            Deny(events.SetItem(at, new SourcePermanentWeatherChangedEvent(marker.Sequence, marker.Turn, marker.Side,
                "ABILITY_DRIZZLE", null, SourcePermanentWeatherKind.Rain)), single);
            Deny(events.Insert(at + 1, marker with { Sequence = marker.Sequence + 1 }), single);
            Deny(events.Insert(at + 1, marker with { Sequence = marker.Sequence + 1, After = 4 }), single);
            var used = events.OfType<SourceMoveUsedEvent>().Single();
            Deny(events.Replace(used, used with { Target = new(1) }), single);
        }
    }
}
