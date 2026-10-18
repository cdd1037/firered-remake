using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using static FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

public sealed partial class GeneralResultAnimationDispatchTests
{
    private static string WeatherLabel(string weather) => weather switch {
        "rain" => "General_Rain", "sandstorm" => "General_Sandstorm", "sun" => "General_Sun", "hail" => "General_Hail",
        _ => throw new ArgumentOutOfRangeException(nameof(weather))
    };
    private static SourceMtMoonVolatileEvent WeatherTimer(string weather = "rain", long sequence = 10) =>
        new(sequence, 2, new(0), weather, 5, 4, "countdown");
    private static SourceWeatherContinuedEvent WeatherContinuation(string weather = "rain", int controller = 0, long sequence = 11) =>
        new(sequence, 2, new(controller), weather);

    [TestMethod]
    public void WeatherHasOnePostTextOwnerAndSkipsNeitherHiddenNorSubstituteControllers()
    {
        foreach (bool single in new[] { true, false }) foreach (string weather in new[] { "rain", "sandstorm", "sun", "hail" })
        for (int controller = 0; controller < (single ? 2 : 4); controller++)
        {
            var timer = WeatherTimer(weather); var continued = WeatherContinuation(weather, controller);
            int count = single ? 2 : 4; MoveId?[] entry = new MoveId?[count]; entry[controller] = Id("DIG");
            bool[] substitutes = Enumerable.Repeat(true, count).ToArray();
            var cue = Check([timer, continued], single, timer, continued, controller, controller, WeatherLabel(weather),
                CampaignGeneralAnimationBoundary.AfterText, entry, substitutes);
            Assert.IsFalse(cue.SpriteVisibility[controller]);
            var paired = CampaignWeatherContinuationPresentation.At(new SourceBattleEvent[] { timer, continued }, 0); Assert.IsNotNull(paired);
            Assert.AreEqual(timer.Sequence, paired.Sequence); Assert.AreEqual(continued.Sequence, paired.ReceiptSequence);
            Assert.AreEqual(controller, paired.Controller.Value); Assert.AreEqual(weather, paired.Weather);
            Assert.IsNull(CampaignWeatherContinuationPresentation.At(new SourceBattleEvent[] { timer, continued }, 1));
            var permanent = continued with { Sequence = 10 };
            Check([permanent], single, permanent, permanent, controller, controller, WeatherLabel(weather),
                CampaignGeneralAnimationBoundary.AfterText, entry, substitutes);
            Assert.AreEqual(weather switch { "rain" => "sText_RainContinues", "sandstorm" => "sText_SandstormRages",
                "sun" => "sText_SunlightStrong", _ => "sText_HailContinues" }, CampaignWeatherContinuationPresentation.ContinuationSymbol(weather));
            if (single) Assert.AreEqual(paired, CampaignWeatherContinuationPresentation.At(
                new SourceBattleEvent[] { timer, continued }.Select(SourceBattleEventAdapter.Export).ToArray(), 0));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => CampaignWeatherContinuationPresentation.ContinuationSymbol("snow"));
    }

    [TestMethod]
    public void WeatherExpirySetupMissingAndMalformedContinuationCannotBorrowAnotherPage()
    {
        foreach (bool single in new[] { true, false })
        {
            var timer = WeatherTimer(); var continued = WeatherContinuation();
            foreach (var invalid in new[] { timer with { Side = new(1) }, timer with { Before = 1, After = 0, Reason = "expired" },
                timer with { Before = 0, After = 5, Reason = "move" }, timer with { Before = 6, After = 5 },
                timer with { After = 3 }, timer with { Reason = "unknown" } }) Deny([invalid, continued], single);
            Deny([timer], single); Deny([timer, new SourceTurnReadyEvent(11, 2, 1)], single);
            foreach (var invalid in new[] { continued with { Sequence = 10 }, continued with { Sequence = 12 },
                continued with { Weather = "hail" }, continued with { Weather = "snow" } }) Deny([timer, invalid], single);
            Assert.IsNull(At([timer, continued with { Turn = 3 }], 0, single));
            Deny([continued with { Weather = "snow" }], single);
            ImmutableArray<SourceBattleEvent> duplicate = [timer, continued, continued with { Sequence = 12 }];
            Assert.IsNotNull(At(duplicate, 0, single)); Assert.IsNull(At(duplicate, 1, single)); Assert.IsNull(At(duplicate, 2, single));
        }
    }

    private static void CheckWeatherBatch(Batch batch, bool single, string weather, bool permanent)
    {
        string before = JsonSerializer.Serialize(batch); var events = batch.Result.Events;
        var continued = events.OfType<SourceWeatherContinuedEvent>().Single(e => e.Weather == weather);
        SourceBattleEvent owner = permanent ? continued : events.OfType<SourceMtMoonVolatileEvent>()
            .Single(e => e.Volatile == weather && e.Reason == "countdown");
        var cue = Check(events, single, owner, continued, continued.Controller.Value, continued.Controller.Value,
            WeatherLabel(weather), CampaignGeneralAnimationBoundary.AfterText, Entry(batch), Substitutes(batch));
        Assert.AreEqual(permanent ? continued.Sequence : owner.Sequence + 1, cue.ReceiptSequence);
        foreach (var damage in events.OfType<SourceWeatherDamageEvent>()) Assert.IsTrue(continued.Sequence < damage.Sequence);
        Assert.AreEqual(before, JsonSerializer.Serialize(batch));
    }

    [TestMethod]
    public void ActualWeatherMovesContinueAfterTheirCountdownAndExpireWithoutAnotherGeneralCue()
    {
        foreach (int id in new[] { 240, 201, 241, 258 }) foreach (bool single in new[] { true, false })
        {
            var spec = WeatherSelectedAnimationKernelFixture.Spec(id);
            var frame = Frame(WeatherSelectedAnimationKernelFixture.Bodies(id), single); int continuations = 0;
            for (int turn = 0; turn < 5; turn++)
            {
                var batches = Turn(frame, 0, 0); frame = batches[^1].Result.State;
                var weatherBatch = batches.Single(b => b.Result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e =>
                    e.Volatile == spec.Weather && e.Reason is "countdown" or "expired"));
                if (turn < 4)
                {
                    CheckWeatherBatch(weatherBatch, single, spec.Weather!, false); continuations++;
                    var timer = weatherBatch.Result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == spec.Weather);
                    Assert.AreEqual(5 - turn, timer.Before); Assert.AreEqual(4 - turn, timer.After);
                }
                else
                {
                    Assert.IsFalse(weatherBatch.Result.Events.OfType<SourceWeatherContinuedEvent>().Any());
                    Assert.IsTrue(weatherBatch.Result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Reason == "expired"));
                    Deny(weatherBatch.Result.Events, single);
                }
            }
            Assert.AreEqual(4, continuations);
        }
    }

    [TestMethod]
    public void ActualEntryAbilityPermanentWeatherOwnsStandaloneContinuationTextAndAnimation()
    {
        foreach (bool single in new[] { true, false }) foreach (string weather in new[] { "rain", "sandstorm", "sun" })
        {
            var bodies = Enumerable.Range(0, 4).Select(_ => Mon("SNORLAX", "DEFENSE_CURL", 100)).ToArray();
            bodies[1] = weather switch {
                "rain" => Mon("KYOGRE", "CALM_MIND", 70), "sandstorm" => Mon("TYRANITAR", "SCREECH", 70),
                _ => Mon("GROUDON", "BULK_UP", 70)
            };
            var frame = Frame(bodies, single); Assert.IsNotNull(frame.Field.PermanentWeather);
            for (int turn = 0; turn < 2; turn++)
            {
                var batches = Turn(frame, 0, 0); frame = batches[^1].Result.State;
                var batch = batches.Single(b => b.Result.Events.OfType<SourceWeatherContinuedEvent>().Any());
                Assert.IsFalse(batch.Result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile == weather));
                CheckWeatherBatch(batch, single, weather, true);
            }
        }
    }

    [TestMethod]
    public void ActualHiddenSubstituteControllerStillPlaysWeatherContinuation()
    {
        foreach (bool single in new[] { true, false })
        {
            var bodies = Enumerable.Range(0, 4).Select(_ => Mon("SNORLAX", "DEFENSE_CURL", 100)).ToArray();
            bodies[0] = Mon("DIGLETT", "DIG", 50) with { CalledVolatile = new() { SubstituteHp = 1 } };
            var frame = Frame(bodies, single); frame = frame with { Field = frame.Field with { SunTurnsRemaining = 3 } };
            var batch = Turn(frame, 0, 1).Single(b => b.Result.Events.OfType<SourceWeatherContinuedEvent>().Any());
            Assert.AreEqual(Id("DIG"), Entry(batch)[0]); Assert.IsTrue(Substitutes(batch)[0]);
            CheckWeatherBatch(batch, single, "sun", false);
            var timer = batch.Result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "sun");
            var cue = At(batch.Result.Events, batch.Result.Events.IndexOf(timer), single, Entry(batch), Substitutes(batch));
            Assert.IsNotNull(cue); Assert.IsFalse(cue.SpriteVisibility[0]);
        }
    }
}
