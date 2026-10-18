using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Domain.Battle;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Battle.FieldEffects;

[TestClass, TestCategory("Game")]
public sealed class WeatherContinuationTests : FieldEffectFixture
{
    private (SourceBattleFrame State, ImmutableArray<SourceBattleEvent> Events) Field(SourceBattleFrame frame)
    {
        // Isolate the maintained field producer from action-order RNG and later
        // battler effects. Reverse order deliberately differs from slot order.
        var type = typeof(SourceFourActionTurns).Assembly.GetType("Game.Domain.Battle.FrameEndTurnContext")!;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var context = Activator.CreateInstance(type, flags | BindingFlags.Public, null, [frame, catalog], null)!;
        var order = Enumerable.Range(0, frame.BattlerCount).Reverse().Select(i => new BattlerSlot(i)).ToImmutableArray();
        type.BaseType!.GetMethod("FieldEffects", flags)!.Invoke(context, [catalog, order]);
        var state = (SourceBattleFrame)type.GetProperty("Frame", flags)!.GetValue(context)!;
        var events = ((ImmutableArray<SourceBattleEvent>.Builder)type.GetField("Events", flags)!.GetValue(context)!).ToImmutable();
        Assert.IsNull(SourceBattleIdentityRules.Validate(state));
        Assert.AreEqual(frame.Rng, state.Rng, "Weather presentation receipts never consume gameplay randomness.");
        return (state, events);
    }

    [TestMethod]
    [DataRow("rain", 2)] [DataRow("sandstorm", 2)] [DataRow("sun", 2)] [DataRow("hail", 2)]
    [DataRow("rain", 1)] [DataRow("sandstorm", 1)] [DataRow("sun", 1)] [DataRow("hail", 1)]
    public void ActualTurnContinuesOnceAfterCountdownAndBeforeDamageButNeverAfterExpiry(string weather, int duration)
    {
        var original = Frame() with { Field = Weather(weather, duration) };
        var (fieldState, _) = Field(original);
        var (state, events) = Turn(original);
        var timer = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == weather);
        var continued = events.OfType<SourceWeatherContinuedEvent>().ToArray();
        Assert.AreEqual(duration - 1, timer.After);
        Assert.AreEqual(Weather(weather, duration - 1), state.Field);
        Assert.AreEqual(fieldState.Field, state.Field);
        foreach (var cell in original.Slots)
        {
            int damage = duration > 1 && weather is ("sandstorm" or "hail") ? Math.Max(1, cell.Body!.Pokemon.Stats.MaxHp / 16) : 0;
            Assert.AreEqual(cell.Body!.Pokemon.CurrentHp - damage, state.Get(cell.Slot).Body!.Pokemon.CurrentHp);
            Assert.AreEqual(fieldState.Get(cell.Slot).Body!.Pokemon.CurrentHp, state.Get(cell.Slot).Body!.Pokemon.CurrentHp);
        }
        if (duration == 1)
        {
            Assert.AreEqual("expired", timer.Reason);
            Assert.AreEqual(0, continued.Length);
            Assert.AreEqual(0, events.OfType<SourceWeatherDamageEvent>().Count());
            return;
        }
        Assert.AreEqual("countdown", timer.Reason);
        Assert.AreEqual(1, continued.Length);
        var cue = continued[0];
        Assert.AreEqual(weather, cue.Weather);
        Assert.AreEqual(new BattlerSlot(0), cue.Controller);
        Assert.AreEqual(timer.Sequence + 1, cue.Sequence);
        Assert.AreEqual(timer.Turn, cue.Turn);
        Assert.IsTrue(events.OfType<SourceWeatherDamageEvent>().All(e => e.Sequence > cue.Sequence));
        Assert.AreEqual(weather is "sandstorm" or "hail" ? 4 : 0, events.OfType<SourceWeatherDamageEvent>().Count());
    }

    [TestMethod]
    [DataRow(SourcePermanentWeatherKind.Rain, "rain")]
    [DataRow(SourcePermanentWeatherKind.Sandstorm, "sandstorm")]
    [DataRow(SourcePermanentWeatherKind.Sun, "sun")]
    public void ActualPermanentWeatherOwnsOneContinuationWithoutInventingTimerChanges(SourcePermanentWeatherKind kind, string weather)
    {
        var original = Frame() with { Field = new() { PermanentWeather = new(kind) } };
        var (fieldState, _) = Field(original);
        var (state, events) = Turn(original);
        var cue = events.OfType<SourceWeatherContinuedEvent>().Single();
        Assert.AreEqual(weather, cue.Weather);
        Assert.AreEqual(new BattlerSlot(0), cue.Controller);
        Assert.AreEqual(original.Field, state.Field);
        Assert.AreEqual(fieldState.Field, state.Field);
        Assert.IsFalse(events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile is "rain" or "sandstorm" or "sun" or "hail"));
        Assert.IsTrue(events.OfType<SourceWeatherDamageEvent>().All(e => e.Sequence > cue.Sequence));
        Assert.AreEqual(kind == SourcePermanentWeatherKind.Sandstorm ? 4 : 0, events.OfType<SourceWeatherDamageEvent>().Count());
        foreach (var cell in original.Slots)
        {
            int damage = kind == SourcePermanentWeatherKind.Sandstorm ? Math.Max(1, cell.Body!.Pokemon.Stats.MaxHp / 16) : 0;
            Assert.AreEqual(cell.Body!.Pokemon.CurrentHp - damage, state.Get(cell.Slot).Body!.Pokemon.CurrentHp);
            Assert.AreEqual(fieldState.Get(cell.Slot).Body!.Pokemon.CurrentHp, state.Get(cell.Slot).Body!.Pokemon.CurrentHp);
        }
    }

    [TestMethod]
    public void StableControllerUsesFirstSourcePresentPositionRegardlessOfSpeedSuppressionOrSubstitute()
    {
        foreach (string weather in new[] { "rain", "sandstorm", "sun", "hail" })
            for (int controller = 0; controller < 4; controller++)
            {
                byte absent = (byte)((1 << controller) - 1);
                var frame = Frame() with { SourceAbsentMask = absent, TurnStartAbsentMask = absent, Field = Weather(weather, 2) };
                var cloud = Mon("PSYDUCK", "TAIL_WHIP", 1);
                Assert.AreEqual("ABILITY_CLOUD_NINE", cloud.Pokemon.AbilityId);
                frame = Rebind(frame, 3, cloud);
                var actor = frame.Get(new(controller)).Body!;
                frame = Rebind(frame, controller, actor with { CalledVolatile = new() { SubstituteHp = 10 } });
                var (state, events) = Field(frame);
                Assert.AreEqual(new BattlerSlot(controller), events.OfType<SourceWeatherContinuedEvent>().Single().Controller);
                Assert.AreEqual(0, events.OfType<SourceWeatherDamageEvent>().Count(), "Suppression prevents damage, not continuing weather presentation.");
                Assert.AreEqual(Weather(weather, 1), state.Field);
                foreach (var cell in frame.Slots) Assert.AreEqual(cell.Body, state.Get(cell.Slot).Body);
            }
    }

    [TestMethod]
    [DataRow("rain")] [DataRow("sandstorm")] [DataRow("sun")] [DataRow("hail")]
    public void HiddenControllerStillContinuesWeatherWithoutChangingDamageOrCharge(string weather)
    {
        var frame = Rebind(Frame() with { Field = Weather(weather, 2) }, 0,
            Mon("DIGLETT", "DIG") with { CalledVolatile = new() { SubstituteHp = 10 } });
        var cell = frame.Get(new(0));
        var turn = new SourceTurnRegisters(WildBattleRules.CalledMoveKernelRulesVersion,
            new(WildBattleCalledLockKind.Charge, Id("DIG"), 0, Id("DIG"), new(1), 1, 0, null, false), 0, null);
        frame = frame with { Slots = frame.Slots.SetItem(0, cell with { Registers = cell.Registers with { Turn = turn } }) };
        var (state, events) = Field(frame);
        Assert.AreEqual(new BattlerSlot(0), events.OfType<SourceWeatherContinuedEvent>().Single().Controller);
        Assert.AreEqual(frame.Get(new(0)).Body, state.Get(new(0)).Body);
        Assert.AreEqual(turn, state.Get(new(0)).Registers.Turn);
        Assert.AreEqual(Weather(weather, 1), state.Field);
        var damage = events.OfType<SourceWeatherDamageEvent>().ToArray();
        Assert.AreEqual(weather is "sandstorm" or "hail" ? 3 : 0, damage.Length);
        foreach (var receipt in damage)
        {
            var body = frame.Get(receipt.Side).Body!;
            Assert.AreEqual(Math.Max(1, body.Pokemon.Stats.MaxHp / 16), receipt.Damage);
            Assert.AreEqual(body.Pokemon.CurrentHp - receipt.Damage, state.Get(receipt.Side).Body!.Pokemon.CurrentHp);
        }
    }

    [TestMethod]
    public void WishAndSafeguardResultsDoNotChooseTheWeatherController()
    {
        foreach (int safeguard in new[] { 1, 2 })
        {
            var frame = Frame() with { SourceAbsentMask = 1, TurnStartAbsentMask = 1, Field = Weather("sun", 2) };
            frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { SafeguardTurnsRemaining = safeguard }) };
            var body = frame.Get(new(3)).Body!;
            frame = Rebind(frame, 3, body with { Pokemon = body.Pokemon with { CurrentHp = body.Pokemon.CurrentHp - 10 } });
            var cell = frame.Get(new(3));
            var caster = frame.Party(cell.Member!.Value.Side).Members[cell.Member.Value.Index].Pokemon;
            frame = frame with { Slots = frame.Slots.SetItem(3, cell with { Position = cell.Position with {
                Wish = new(1, cell.Member.Value.Index, caster.Species, caster.OriginalTrainerId, caster.Personality) } }) };
            var (_, events) = Field(frame);
            var cue = events.OfType<SourceWeatherContinuedEvent>().Single();
            Assert.AreEqual(new BattlerSlot(1), cue.Controller);
            Assert.IsTrue(events.OfType<SourceRecoveredEvent>().Single().Sequence < cue.Sequence);
            Assert.IsTrue(events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "safeguard").Sequence < cue.Sequence);
        }
    }

    [TestMethod]
    public void ContinuationEventsRoundTripAndExportWithoutLosingControllerIdentity()
    {
        foreach (int controller in Enumerable.Range(0, 4))
        {
            SourceBattleEvent source = new SourceWeatherContinuedEvent(12, 3, new(controller), "hail");
            Assert.AreEqual(source, JsonSerializer.Deserialize<SourceBattleEvent>(JsonSerializer.Serialize(source)));
            Assert.IsNull(SourceBattleEventHooks.ValidateRouting(Frame(), source));
            if (controller >= 2)
            {
                Assert.AreEqual("source_event_slot_outside_layout", SourceBattleEventHooks.ValidateRouting(Frame() with { Layout = SourceBattleLayout.Singles }, source));
                Assert.Throws<NotSupportedException>(() => SourceBattleEventAdapter.Export(source));
                continue;
            }
            WildBattleEvent exported = SourceBattleEventAdapter.Export(source);
            Assert.AreEqual(new WildBattleWeatherContinuedEvent(12, 3, (WildBattleSide)controller, "hail"), exported);
            Assert.AreEqual(exported, JsonSerializer.Deserialize<WildBattleEvent>(JsonSerializer.Serialize(exported)));
        }
        Assert.AreEqual("source_event_weather_kind", SourceBattleEventHooks.ValidateRouting(Frame(), new SourceWeatherContinuedEvent(1, 0, new(0), "unknown")));
    }
}
