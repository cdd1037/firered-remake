using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

// Source: pret 037335f4 battle_scripts_1.s BattleScript_CastformChange,
// battle_script_commands.c Cmd_docastformchangeanimation,
// battle_gfx_sfx_util.c TryHandleLaunchBattleTableAnimation, and
// battle_anim_scripts.s General_CastformChange.
[TestClass, TestCategory("Game")]
public sealed class CampaignCastformAnimationDispatchTests
{
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static readonly MoveId NoMove = new("pret:MOVE_NONE");
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static void SameCue(CampaignCastformAnimationDispatch? expected, CampaignCastformAnimationDispatch? actual, string? message = null) =>
        Assert.AreEqual(Json(expected), Json(actual), message);
    private static SourceCastformForm Form(WildBattleCombatant body) => body.CalledOverlay?.CastformForm ?? SourceCastformForm.Normal;
    private static string Type(SourceCastformForm form) => form switch {
        SourceCastformForm.Normal => "TYPE_NORMAL", SourceCastformForm.Sunny => "TYPE_FIRE",
        SourceCastformForm.Rainy => "TYPE_WATER", SourceCastformForm.Snowy => "TYPE_ICE",
        _ => throw new ArgumentOutOfRangeException(nameof(form))
    };
    private static WildBattleCalledOverlay Overlay(SourceCastformForm form, WildBattleCalledOverlay? before = null) =>
        (before ?? new()) with { Type1 = Type(form), Type2 = Type(form), CastformForm = form };
    private static WildBattleCombatant Mon(string species = "CASTFORM", string move = "TACKLE", bool shiny = false, int level = 70)
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(shiny ? 42u : 0u)), new(0)).Pokemon;
        var id = new MoveId("pret:MOVE_" + move);
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == id && m.Level <= level),
            species + " must naturally learn " + move + " at the fixture level.");
        int pp = Mechanics.Catalog.Moves[id].Pp;
        Assert.AreEqual(shiny, pokemon.IsShiny);
        return new(pokemon with { Moves = [new(id, pp, pp)] });
    }
    private static WildBattleCombatant[] PlainBodies() => Enumerable.Range(0, 4).Select(_ => Mon("SNORLAX", "DEFENSE_CURL")).ToArray();
    private static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
        new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
    private static SourceBattleFrame RawFrame(WildBattleCombatant[] bodies, bool single = false,
        WildBattleCombatant[]? playerReserves = null, WildBattleCombatant[]? opponentReserves = null) => new() {
        Layout = single ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
        KoReplacementTiming = single ? KoReplacementTiming.SourceImmediate : KoReplacementTiming.EndOfTurn,
        SourceMechanicsRulesVersion = SourceBattleKernel.Version,
        BattleKind = single ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer,
        SourceAbsentMask = (byte)(single ? 12 : 0), TurnStartAbsentMask = (byte)(single ? 12 : 0), Rng = new(31),
        Parties = [new(BattleSide.Player, (single ? new[] { bodies[0] } : new[] { bodies[0], bodies[2] }).Concat(playerReserves ?? []).Select(Member).ToImmutableArray()),
            new(BattleSide.Opponent, (single ? new[] { bodies[1] } : new[] { bodies[1], bodies[3] }).Concat(opponentReserves ?? []).Select(Member).ToImmutableArray())],
        Slots = Enumerable.Range(0, 4).Select(i => single && i >= 2 ? new SourceBattleSlot(new(i), null, 0, null)
            : new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
    };
    private static SourceFourActionResult Entry(SourceBattleFrame frame)
    {
        var result = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        return result;
    }
    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor, int target)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Get(new(actor)).Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }
    private static MoveId?[] EntryCharges(SourceBattleFrame before) => before.Slots.Take(before.BattlerCount).Select(c =>
        c.Registers.Turn?.Lock is { Kind: WildBattleCalledLockKind.Charge } charge ? (MoveId?)charge.Move : null).ToArray();
    private static CampaignBattleDisplayBodies BeforePage(CampaignBattleDisplayBodies seed, ImmutableArray<SourceBattleEvent> events, int index)
    {
        var display = seed;
        for (int i = 0; i < index; i++) display = display.Apply(events[i]);
        return display;
    }
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index, CampaignBattleDisplayBodies seed,
        SourceBattleFrame? final = null)
    {
        var display = BeforePage(seed, events, index);
        return new("castform", new("test"), "test", final ?? new(), CampaignDoubleBattleStage.Combat, events, index,
            [], [], 0, 0, display.Bodies, display.Members, [], [], 0);
    }
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index, CampaignBattleDisplayBodies seed,
        SourceBattleFrame? final = null)
    {
        // PrepareWildPresentation has already applied the current canonical event.
        var batch = new CampaignBattleDisplayBatch(seed).AdvanceThrough(events, events[index].Sequence);
        return new("castform", null!, final ?? new(), events, index,
            batch.Display.Bodies[0].Pokemon, batch.Display.Bodies[1].Pokemon, CampaignBattlePhase.Events, [], 0, []) { DisplayBatch = batch
        };
    }
    private static CampaignCastformAnimationDispatch? At(CampaignDoubleBattle battle, MoveId?[]? entry = null,
        SourceBattleScene scene = SourceBattleScene.On) => CampaignCastformAnimationPresentation.AtResult(battle, scene, entry ?? new MoveId?[4]);
    private static CampaignCastformAnimationDispatch? At(CampaignWildBattle battle, MoveId?[]? entry = null,
        SourceBattleScene scene = SourceBattleScene.On) => CampaignCastformAnimationPresentation.AtResult(battle, true, scene, entry ?? new MoveId?[2]);
    private static CampaignBattleDisplayBodies Seed(int slot = 0, bool single = false, WildBattleCombatant? actor = null)
    {
        var bodies = PlainBodies(); bodies[slot] = actor ?? Mon();
        return CampaignBattleDisplayBodies.FromSlots(RawFrame(bodies, single).Slots);
    }
    private static ImmutableArray<SourceBattleEvent> Pair(int slot = 0, WildBattleCalledOverlay? before = null,
        SourceCastformForm after = SourceCastformForm.Rainy, long first = 1, int turn = 7) => [
        new SourceAbilityActivatedEvent(first, turn, new(slot), "ABILITY_FORECAST"),
        new SourceCalledOverlayChangedEvent(first + 1, turn, new(slot), NoMove, before, Overlay(after, before))
    ];
    private static void AssertBodies(CampaignCastformAnimationDispatch cue, SourceCalledOverlayChangedEvent receipt,
        WildBattleCombatant before, bool immediate = false)
    {
        Assert.AreEqual("castform", cue.BattleId); Assert.AreEqual(receipt.Sequence, cue.Sequence);
        Assert.AreEqual(receipt.Sequence - 1, cue.AbilitySequence); Assert.AreEqual(receipt.Side, cue.Affected);
        Assert.AreEqual(Json(before), Json(cue.BeforeBody)); Assert.AreEqual(Json(before with { CalledOverlay = receipt.After }), Json(cue.AfterBody));
        Assert.AreEqual(immediate, cue.Immediate);
        Assert.IsFalse(cue.SpriteVisibility.IsDefaultOrEmpty);
        Assert.AreEqual(!immediate, cue.SpriteVisibility[receipt.Side.Value]);
    }
    private static int AssertBatch(SourceBattleFrame before, SourceBattleFrame after, ImmutableArray<SourceBattleEvent> events,
        int expected, SourceCastformForm? expectedForm = null)
    {
        string unchanged = Json(new { before, after, events });
        var seed = CampaignBattleDisplayBodies.FromSlots(before.Slots); var charges = EntryCharges(before); int count = 0;
        for (int i = 0; i < events.Length; i++)
        {
            var prior = BeforePage(seed, events, i);
            CampaignCastformAnimationDispatch? cue;
            if (before.Layout == SourceBattleLayout.Singles)
            {
                var battle = Single(events, i, seed, after); string saved = Json(battle);
                cue = At(battle, charges); SameCue(cue, At(battle, charges));
                SameCue(cue, At(battle with { Frame = new() }, charges), "Final gameplay Frame must not supply event-time material or visibility.");
                SameCue(cue, At(battle, charges, SourceBattleScene.Off), "General_CastformChange ignores Battle Scene.");
                Assert.AreEqual(saved, Json(battle));
            }
            else
            {
                var battle = Double(events, i, seed, after); string saved = Json(battle);
                cue = At(battle, charges); SameCue(cue, At(battle, charges));
                SameCue(cue, At(battle with { Frame = new() }, charges));
                SameCue(cue, At(battle, charges, SourceBattleScene.Off));
                Assert.AreEqual(saved, Json(battle));
            }
            if (cue is null) continue;
            count++; Assert.IsInstanceOfType<SourceCalledOverlayChangedEvent>(events[i]);
            var receipt = (SourceCalledOverlayChangedEvent)events[i];
            Assert.IsTrue(i > 0); Assert.IsInstanceOfType<SourceAbilityActivatedEvent>(events[i - 1]);
            var ability = (SourceAbilityActivatedEvent)events[i - 1];
            Assert.AreEqual("ABILITY_FORECAST", ability.Ability); Assert.AreEqual(receipt.Side, ability.Side);
            Assert.AreEqual(receipt.Turn, ability.Turn); AssertBodies(cue, receipt, prior.Bodies[receipt.Side.Value]);
            if (expectedForm is { } form) Assert.AreEqual(form, Form(cue.AfterBody));
        }
        Assert.AreEqual(expected, count); Assert.AreEqual(unchanged, Json(new { before, after, events }));
        return count;
    }

    [TestMethod]
    [DataRow("KYOGRE", "CALM_MIND", SourceCastformForm.Rainy)]
    [DataRow("GROUDON", "BULK_UP", SourceCastformForm.Sunny)]
    public void NaturalEntryWeatherUsesPreAbilityBodiesAtEverySingleAndDoublePosition(string species, string move, SourceCastformForm form)
    {
        foreach (bool single in new[] { false, true })
            for (int actor = 0; actor < (single ? 2 : 4); actor++)
            {
                var bodies = PlainBodies(); bodies[actor] = Mon(); bodies[actor ^ 1] = Mon(species, move);
                var frame = RawFrame(bodies, single); string unchanged = Json(frame); var result = Entry(frame);
                AssertBatch(frame, result.State, result.Events, 1, form);
                Assert.AreEqual(unchanged, Json(frame)); Assert.AreEqual(Json(result), Json(Entry(frame)), "Presentation cannot consume source entry-sort RNG.");
            }
    }

    [TestMethod, DataRow(0), DataRow(1)]
    public void RealWildEntryUsesOwnedPreAbilitySnapshotAndRetainsSourceReceipts(int actor)
    {
        var player = actor == 0 ? Mon() : Mon("KYOGRE", "CALM_MIND");
        var opponent = actor == 1 ? Mon() : Mon("KYOGRE", "CALM_MIND");
        var result = SourceWildBattlePolicy.Start(Mechanics, [player], 0, opponent, new(37));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsFalse(result.EntrySlots.IsDefaultOrEmpty);
        Assert.IsNull(result.EntrySlots[actor].Body!.CalledOverlay);
        var before = result.State! with { Slots = result.EntrySlots };
        string unchanged = Json(result);
        AssertBatch(before, result.State!, result.Events, 1, SourceCastformForm.Rainy);
        Assert.AreEqual(unchanged, Json(result));
        Assert.AreEqual(unchanged, Json(SourceWildBattlePolicy.Start(Mechanics, [player], 0, opponent, new(37))));
    }

    [TestMethod]
    [DataRow("SUNNY_DAY", SourceCastformForm.Sunny)]
    [DataRow("RAIN_DANCE", SourceCastformForm.Rainy)]
    [DataRow("HAIL", SourceCastformForm.Snowy)]
    public void NaturalWeatherMovesEmitOneGeneralReceiptWithoutChangingMoveOrRngOwnership(string move, SourceCastformForm form)
    {
        foreach (bool single in new[] { false, true })
            for (int actor = 0; actor < (single ? 2 : 4); actor++)
            {
                var bodies = PlainBodies(); bodies[actor] = Mon(move: move);
                var before = Entry(RawFrame(bodies, single)).State; var result = Resolve(before, actor, actor);
                Assert.AreEqual(1, result.Events.OfType<SourceMoveUsedEvent>().Count());
                AssertBatch(before, result.State, result.Events, 1, form);
                Assert.AreEqual(Json(result), Json(Resolve(before, actor, actor)));
                var repeated = Resolve(result.State, actor, actor);
                Assert.IsTrue(repeated.Events.OfType<SourceMoveFailedEvent>().Any());
                AssertBatch(result.State, repeated.State, repeated.Events, 0);
            }
    }

    [TestMethod, DataRow(true, false), DataRow(false, true)]
    public void NaturalTransformKeepsActorPaletteAndCopiedEventTimeDonorAcrossLaterWeather(bool actorShiny, bool donorShiny)
    {
        var bodies = PlainBodies(); bodies[0] = Mon("DITTO", "TRANSFORM", actorShiny);
        bodies[1] = Mon(move: "HAIL", shiny: donorShiny); bodies[2] = Mon(move: "SUNNY_DAY");
        var entry = Entry(RawFrame(bodies)).State;
        var sunny = Resolve(entry, 2, 2); var transform = Resolve(sunny.State, 0, 1);
        Assert.AreEqual(1, transform.Events.OfType<SourceTransformedEvent>().Count());
        var hail = Resolve(transform.State, 1, 1);
        var events = sunny.Events.AddRange(transform.Events).AddRange(hail.Events);
        AssertBatch(entry, hail.State, events, 5); // Two Sunny entries, then all three Snowy entries.
        int index = events.IndexOf(events.OfType<SourceCalledOverlayChangedEvent>().Last(e => e.Side.Value == 0));
        var cue = At(Double(events, index, CampaignBattleDisplayBodies.FromSlots(entry.Slots), hail.State));
        Assert.IsNotNull(cue); Assert.AreEqual(SourceCastformForm.Sunny, Form(cue.BeforeBody));
        Assert.AreEqual(SourceCastformForm.Snowy, Form(cue.AfterBody)); Assert.IsNotNull(cue.BeforeBody.Transform);
        Assert.AreEqual(cue.BeforeBody.Transform, cue.AfterBody.Transform); Assert.IsNotNull(cue.AfterBody.Transform);
        Assert.AreEqual(bodies[0].Pokemon.Personality, cue.AfterBody.Transform.OriginalPokemon.Personality);
        Assert.AreEqual(bodies[1].Pokemon.Personality, cue.AfterBody.Transform.CopiedPokemon.Personality);
        foreach (var body in new[] { cue.BeforeBody, cue.AfterBody })
        {
            var appearance = CampaignBattleFormAppearance.From(body);
            Assert.IsTrue(appearance.Transformed); Assert.AreEqual(actorShiny, appearance.Shiny);
            Assert.AreEqual(donorShiny, appearance.RenderPokemon.IsShiny);
        }
    }

    [TestMethod]
    public void NaturalAirLockSuppressesWeatherFormWithoutSuppressingWeatherGameplay()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = PlainBodies(); bodies[actor] = Mon(move: "HAIL"); bodies[actor ^ 1] = Mon("RAYQUAZA", "DRAGON_DANCE");
            var before = Entry(RawFrame(bodies)).State; var result = Resolve(before, actor, actor);
            Assert.AreEqual(5, result.State.Field.HailTurnsRemaining); Assert.AreEqual(SourceCastformForm.Normal, Form(result.State.Get(new(actor)).Body!));
            Assert.IsFalse(result.Events.OfType<SourceAbilityActivatedEvent>().Any(e => e.Ability == "ABILITY_FORECAST"));
            AssertBatch(before, result.State, result.Events, 0); Assert.AreEqual(Json(result), Json(Resolve(before, actor, actor)));
        }
    }

    [TestMethod]
    public void NaturalSuppressorSwitchRevertsExistingRainyCastformBeforeQueuedMoves()
    {
        var bodies = PlainBodies(); bodies[0] = Mon(move: "SUNNY_DAY"); bodies[1] = Mon("KYOGRE", "CALM_MIND");
        var before = Entry(RawFrame(bodies, playerReserves: [Mon("RAYQUAZA", "DRAGON_DANCE")])).State;
        Assert.AreEqual(SourceCastformForm.Rainy, Form(before.Get(new(0)).Body!));
        var choices = before.Slots.Select(c => new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, c.Slot)).ToImmutableArray();
        choices = choices.SetItem(2, new(before.Get(new(2)).Occupant!.Value, SourceActionKind.Switch, Incoming: new(BattleSide.Player, 2)));
        var begun = SourceFourActionTurns.BeginActions(Mechanics.Catalog, before, before.Revision, choices);
        Assert.IsTrue(begun.Succeeded, begun.Fault); Assert.IsNotNull(begun.Continuation);
        var switched = SourceFourActionTurns.Advance(Mechanics.Catalog, begun.State, begun.Continuation.Value);
        Assert.IsTrue(switched.Succeeded, switched.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(switched.State));
        Assert.AreEqual(1, switched.Events.OfType<SourceCalledPartySwitchedEvent>().Count());
        Assert.IsFalse(switched.Events.OfType<SourceMoveUsedEvent>().Any(), "The switch and Forecast settle before later queued moves.");
        Assert.AreEqual(SourcePermanentWeatherKind.Rain, switched.State.Field.PermanentWeather!.Kind);
        AssertBatch(begun.State, switched.State, switched.Events, 1, SourceCastformForm.Normal);
        Assert.AreEqual(Json(switched), Json(SourceFourActionTurns.Advance(Mechanics.Catalog, begun.State, begun.Continuation.Value)));
    }

    [TestMethod]
    public void NaturalSwitchEntryBindsIncomingCastformBeforeForecastAtEveryPosition()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = PlainBodies(); bodies[actor ^ 1] = Mon("KYOGRE", "CALM_MIND");
            var incoming = Mon(move: "SUNNY_DAY", shiny: true);
            var raw = RawFrame(bodies, playerReserves: (actor & 1) == 0 ? [incoming] : null,
                opponentReserves: (actor & 1) == 1 ? [incoming] : null);
            var before = Entry(raw).State;
            var choices = before.Slots.Select(c => new SourceActionSelection(c.Occupant!.Value,
                SourceActionKind.Move, 0, c.Slot)).ToImmutableArray();
            choices = choices.SetItem(actor, new(before.Get(new(actor)).Occupant!.Value,
                SourceActionKind.Switch, Incoming: new((BattleSide)(actor & 1), 2)));
            var begun = SourceFourActionTurns.BeginActions(Mechanics.Catalog, before, before.Revision, choices);
            Assert.IsTrue(begun.Succeeded, begun.Fault); Assert.IsNotNull(begun.Continuation);
            var switched = SourceFourActionTurns.Advance(Mechanics.Catalog, begun.State, begun.Continuation.Value);
            Assert.IsTrue(switched.Succeeded, switched.Fault);
            var change = switched.Events.OfType<SourceCalledPartySwitchedEvent>().Single();
            var overlay = switched.Events.OfType<SourceCalledOverlayChangedEvent>().Single();
            Assert.AreEqual(actor, change.Side.Value); Assert.AreEqual(2, change.IncomingIndex);
            Assert.IsTrue(change.Sequence < overlay.Sequence); Assert.IsNotNull(change.Replacement);
            Assert.AreEqual(incoming.Pokemon, change.Replacement.Body.Pokemon);
            AssertBatch(begun.State, switched.State, switched.Events, 1, SourceCastformForm.Rainy);
            int index = switched.Events.IndexOf(overlay);
            var cue = At(Double(switched.Events, index, CampaignBattleDisplayBodies.FromSlots(begun.State.Slots), switched.State));
            Assert.IsNotNull(cue); Assert.AreEqual(incoming.Pokemon, cue.BeforeBody.Pokemon);
            Assert.IsNull(cue.BeforeBody.CalledOverlay); Assert.IsTrue(cue.AfterBody.Pokemon.IsShiny);
        }
    }

    [TestMethod]
    public void NaturalSuppressorDepartureRestoresFormOnlyAfterTheActualSwitchReceipt()
    {
        var bodies = PlainBodies(); bodies[0] = Mon(move: "SUNNY_DAY"); bodies[1] = Mon("KYOGRE", "CALM_MIND");
        var before = Entry(RawFrame(bodies, playerReserves: [Mon("RAYQUAZA", "DRAGON_DANCE")])).State;
        ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame frame, int incoming) => frame.Slots.Select(c =>
            c.Slot.Value == 2 ? new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Switch, Incoming: new(BattleSide.Player, incoming)) :
                new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, c.Slot)).ToImmutableArray();
        var current = SourceFourActionTurns.BeginActions(Mechanics.Catalog, before, before.Revision, Choices(before, 2));
        int transitions = 0;
        for (int step = 0; ; step++)
        {
            Assert.IsTrue(step < 24, "The bounded public turn must reach Ready.");
            Assert.IsTrue(current.Succeeded, current.Fault);
            int expected = current.Events.OfType<SourceCalledOverlayChangedEvent>().Count();
            transitions += AssertBatch(before, current.State, current.Events, expected);
            if (current.CompletedCleanTurn) break;
            Assert.IsNotNull(current.Continuation); before = current.State;
            current = SourceFourActionTurns.Advance(Mechanics.Catalog, before, current.Continuation.Value);
        }
        Assert.AreEqual(1, transitions); // Rainy -> Normal when Air Lock enters.
        var ready = current.State;
        Assert.IsTrue(SourceWeatherRules.HasSun(ready.Field)); Assert.AreEqual(SourceCastformForm.Normal, Form(ready.Get(new(0)).Body!));
        var begun = SourceFourActionTurns.BeginActions(Mechanics.Catalog, ready, ready.Revision, Choices(ready, 1));
        Assert.IsTrue(begun.Succeeded, begun.Fault); Assert.IsNotNull(begun.Continuation);
        var left = SourceFourActionTurns.Advance(Mechanics.Catalog, begun.State, begun.Continuation.Value);
        Assert.IsTrue(left.Succeeded, left.Fault);
        var change = left.Events.OfType<SourceCalledPartySwitchedEvent>().Single();
        var overlay = left.Events.OfType<SourceCalledOverlayChangedEvent>().Single();
        Assert.AreEqual(2, change.Side.Value); Assert.AreEqual(1, change.IncomingIndex);
        Assert.IsTrue(change.Sequence < overlay.Sequence); AssertBatch(begun.State, left.State, left.Events, 1, SourceCastformForm.Sunny);
    }

    [TestMethod]
    public void SinglesPrepareAndDoublesAckOwnOppositeSidesOfTheSameImmutableReceipt()
    {
        foreach (bool single in new[] { false, true })
            for (int slot = 0; slot < (single ? 2 : 4); slot++)
            {
                var seed = Seed(slot, single); var events = Pair(slot); var receipt = (SourceCalledOverlayChangedEvent)events[1];
                CampaignCastformAnimationDispatch? cue;
                if (single)
                {
                    var battle = Single(events, 1, seed);
                    Assert.AreEqual(receipt.After, battle.DisplayBatch!.Display.Bodies[slot].CalledOverlay);
                    cue = At(battle); SameCue(cue, At(battle));
                    Assert.IsNull(At(battle with { EventIndex = 0 }));
                    var repeated = battle with { DisplayBatch = battle.DisplayBatch.AdvanceThrough(events, receipt.Sequence) };
                    SameCue(cue, At(repeated), "Repeated Prepare/render remains stable at the current receipt.");
                    Assert.IsNull(At(battle with { EventIndex = events.Length }));
                }
                else
                {
                    var battle = Double(events, 1, seed); Assert.IsNull(battle.DisplayedSlots[slot].CalledOverlay);
                    cue = At(battle); SameCue(cue, At(battle));
                    var ack = new CampaignBattleDisplayBodies(battle.DisplayedSlots, battle.DisplayedMembers).Apply(receipt);
                    Assert.IsNull(At(battle with { EventIndex = events.Length, DisplayedSlots = ack.Bodies, DisplayedMembers = ack.Members }));
                    Assert.IsNull(battle.DisplayedSlots[slot].CalledOverlay, "Rendering must not pre-apply doubles ACK.");
                }
                Assert.IsNotNull(cue); AssertBodies(cue, receipt, seed.Bodies[slot]);
                Assert.IsNull(seed.Bodies[slot].CalledOverlay, "Request body selection never mutates the seed.");
            }
    }

    [TestMethod]
    public void MultipleFormChangesRetainTheirOwnBeforeAfterMaterialsEvenWhenFinalFrameDiffers()
    {
        foreach (bool single in new[] { false, true })
        {
            int second = single ? 1 : 2; var bodies = PlainBodies(); bodies[0] = Mon(); bodies[second] = Mon(shiny: true);
            var raw = RawFrame(bodies, single); var seed = CampaignBattleDisplayBodies.FromSlots(raw.Slots);
            var sunny = Overlay(SourceCastformForm.Sunny);
            var events = Pair().SetItem(1, new SourceCalledOverlayChangedEvent(2, 7, new(0), NoMove, null, sunny))
                .AddRange(Pair(second, after: SourceCastformForm.Rainy, first: 3))
                .AddRange(Pair(0, sunny, SourceCastformForm.Snowy, 5));
            var first = single ? At(Single(events, 1, seed)) : At(Double(events, 1, seed));
            var other = single ? At(Single(events, 3, seed)) : At(Double(events, 3, seed));
            var last = single ? At(Single(events, 5, seed)) : At(Double(events, 5, seed));
            Assert.IsNotNull(first); Assert.IsNotNull(other); Assert.IsNotNull(last);
            Assert.AreEqual(SourceCastformForm.Normal, Form(first.BeforeBody)); Assert.AreEqual(SourceCastformForm.Sunny, Form(first.AfterBody));
            Assert.AreEqual(SourceCastformForm.Normal, Form(other.BeforeBody)); Assert.AreEqual(SourceCastformForm.Rainy, Form(other.AfterBody));
            Assert.AreEqual(SourceCastformForm.Sunny, Form(last.BeforeBody)); Assert.AreEqual(SourceCastformForm.Snowy, Form(last.AfterBody));
            Assert.AreEqual(second, other.Affected.Value); Assert.IsTrue(other.AfterBody.Pokemon.IsShiny);
            Assert.AreNotEqual(first.Sequence, last.Sequence); Assert.AreEqual(SourceCastformForm.Sunny, Form(first.AfterBody));
        }
    }

    [TestMethod]
    public void SwitchReceiptSelectsIncomingCastformAndClearsOldOccupantChargeEvidence()
    {
        for (int slot = 0; slot < 4; slot++)
        {
            var outgoing = Mon("DITTO", "TRANSFORM"); var incoming = Mon(shiny: true); var seed = Seed(slot, actor: outgoing);
            ImmutableArray<SourceBattleEvent> events = [new SourceCalledPartySwitchedEvent(1, 7, new(slot), slot / 2, 2, WildBattleCalledSwitchKind.Voluntary)
                { Replacement = new(incoming, new()) }, .. Pair(slot, first: 2)];
            var charges = new MoveId?[4]; charges[slot] = new("pret:MOVE_FLY");
            var battle = Double(events, 2, seed); var cue = At(battle, charges);
            Assert.IsNotNull(cue); Assert.AreEqual(incoming, cue.BeforeBody); Assert.IsFalse(cue.Immediate);
            Assert.AreEqual(new PartyMemberKey((BattleSide)(slot & 1), 2), battle.DisplayedMembers[slot]);
            Assert.AreEqual(outgoing, seed.Bodies[slot]); Assert.IsTrue(cue.AfterBody.Pokemon.IsShiny);
        }
    }

    [TestMethod]
    public void SubstituteSkipsGeneralCueButReceiptStillChangesDisplayedForm()
    {
        foreach (bool single in new[] { false, true })
            for (int slot = 0; slot < (single ? 2 : 4); slot++)
            {
                var actor = Mon() with { CalledVolatile = new() { SubstituteHp = 20 } };
                var seed = Seed(slot, single, actor); var events = Pair(slot);
                var cue = single ? At(Single(events, 1, seed)) : At(Double(events, 1, seed));
                Assert.IsNull(cue);
                var after = seed.Apply(events[1]); Assert.AreEqual(SourceCastformForm.Rainy, Form(after.Bodies[slot]));
                Assert.AreEqual(20, after.Bodies[slot].CalledVolatile!.SubstituteHp);
                Assert.AreEqual(SourceCastformForm.Normal, Form(seed.Bodies[slot]));
                // A later substitute break cannot make the current receipt animate.
                var later = events.Add(new SourceMtMoonVolatileEvent(3, 7, new(slot), "substitute", 20, 0, "broken"));
                Assert.IsNull(single ? At(Single(later, 1, seed)) : At(Double(later, 1, seed)));
            }
    }

    private static SourcePpCommandEvent Pp(int slot, string move, long sequence = 1, int turn = 7) =>
        new(sequence, turn, new(slot), new(slot ^ 1), 0, new("pret:MOVE_METRONOME"), new("pret:MOVE_" + move), 10, 9, 1, 0);
    private static SourceMtMoonVolatileEvent Charge(int slot, long sequence, int after, string reason = "move", int turn = 7) =>
        new(sequence, turn, new(slot), "called-charge", after == 0 ? 1 : 0, after, reason);

    [TestMethod, DataRow("FLY"), DataRow("BOUNCE"), DataRow("DIG"), DataRow("DIVE")]
    public void KnownInvisibleLivingBattlerUsesImmediateSwapAtEverySlotAndSceneOff(string move)
    {
        foreach (bool single in new[] { false, true })
            for (int slot = 0; slot < (single ? 2 : 4); slot++)
            {
                var seed = Seed(slot, single); var pair = Pair(slot); var charges = new MoveId?[single ? 2 : 4]; charges[slot] = new("pret:MOVE_" + move);
                var cue = single ? At(Single(pair, 1, seed), charges, SourceBattleScene.Off) : At(Double(pair, 1, seed), charges, SourceBattleScene.Off);
                Assert.IsNotNull(cue); AssertBodies(cue, (SourceCalledOverlayChangedEvent)pair[1], seed.Bodies[slot], true);
                ImmutableArray<SourceBattleEvent> sameBatch = [Pp(slot, move), Charge(slot, 2, 1), .. Pair(slot, first: 3)];
                var during = single ? At(Single(sameBatch, 3, seed)) : At(Double(sameBatch, 3, seed));
                Assert.IsNotNull(during); Assert.IsTrue(during.Immediate);
                // A later release is not evidence that the sprite was visible earlier.
                var later = sameBatch.Add(Charge(slot, 5, 0, "released"));
                var retained = single ? At(Single(later, 3, seed)) : At(Double(later, 3, seed));
                SameCue(during, retained);
            }
    }

    [TestMethod]
    public void NaturalHiddenPartnerDoesNotMakeVisibleForecastActorUseImmediateBranch()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int partner = actor ^ 2;
            var bodies = PlainBodies(); bodies[actor] = Mon(move: "HAIL"); bodies[partner] = Mon("DIGLETT", "DIG");
            var before = Entry(RawFrame(bodies)).State;
            var dig = Resolve(before, partner, partner ^ 1);
            var charge = dig.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-charge");
            Assert.AreEqual(partner, charge.Side.Value); Assert.AreEqual(1, charge.After);
            var hail = Resolve(dig.State, actor, actor); var events = dig.Events.AddRange(hail.Events);
            AssertBatch(before, hail.State, events, 1, SourceCastformForm.Snowy);
            var overlay = hail.Events.OfType<SourceCalledOverlayChangedEvent>().Single();
            var cue = At(Double(events, events.IndexOf(overlay), CampaignBattleDisplayBodies.FromSlots(before.Slots), hail.State));
            Assert.IsNotNull(cue); Assert.AreEqual(actor, cue.Affected.Value); Assert.IsFalse(cue.Immediate);
            Assert.AreEqual(4, cue.SpriteVisibility.Length);
            for (int slot = 0; slot < 4; slot++) Assert.AreEqual(slot != partner, cue.SpriteVisibility[slot]);
            // The immutable per-slot scene request and existing general consumer must
            // agree about the same committed pre-command charge evidence.
            var charges = EntryCharges(dig.State); Assert.AreEqual(new MoveId("pret:MOVE_DIG"), charges[partner]);
            CampaignDoubleBattle Held(int slot) => Double([new SourceHeldItemHealEvent(1, 1, new(slot), new(slot ^ 1),
                NoMove, "ITEM_LEFTOVERS", 0, 10, 12, 2)], 0, CampaignBattleDisplayBodies.FromSlots(dig.State.Slots));
            Assert.IsNotNull(CampaignHeldItemAnimationPresentation.AtResult(Held(actor), SourceBattleScene.On, charges));
            Assert.IsNull(CampaignHeldItemAnimationPresentation.AtResult(Held(partner), SourceBattleScene.On, charges));
        }
    }

    [TestMethod]
    public void UnknownChargeEvidenceFailsClosedAndExplicitReleaseRestoresVisibility()
    {
        var seed = Seed();
        foreach (var prefix in new ImmutableArray<SourceBattleEvent>[] {
            [Charge(0, 1, 1)], [Pp(0, "FLY", turn: 6), Charge(0, 2, 1)],
            [Pp(0, "FLY"), Charge(0, 2, 2)], [Pp(0, "FLY"), Charge(0, 2, 1, "unrecognized")]
        })
        {
            var events = prefix.AddRange(Pair(first: prefix.Length + 1));
            Assert.IsNull(At(Double(events, events.Length - 1, seed)), "Unknown visibility is not the immediate-hidden branch.");
            var released = prefix.Add(Charge(0, prefix.Length + 1, 0, "sleep")).AddRange(Pair(first: prefix.Length + 2));
            var cue = At(Double(released, released.Length - 1, seed)); Assert.IsNotNull(cue); Assert.IsFalse(cue.Immediate);
        }
        foreach (string move in new[] { "RAZOR_WIND", "SOLAR_BEAM", "SKULL_BASH", "SKY_ATTACK" })
        {
            var cue = At(Double(Pair(), 1, seed), [new("pret:MOVE_" + move), null, null, null]);
            Assert.IsNotNull(cue); Assert.IsFalse(cue.Immediate);
        }
        ImmutableArray<SourceBattleEvent> unknownPartner = [Charge(2, 1, 1), .. Pair(first: 2)];
        Assert.IsNull(At(Double(unknownPartner, 2, seed)), "Unknown partner visibility must not invent a monbg partner selection.");
        ImmutableArray<SourceBattleEvent> started = [new SourceStartedEvent(1, 7), .. Pair(first: 2)];
        var reset = At(Double(started, 2, seed), [new("pret:MOVE_DIG"), null, null, null]);
        Assert.IsNotNull(reset); Assert.IsFalse(reset.Immediate);
    }

    [TestMethod]
    public void HiddenIsNotFaintedAndFinalFrameCannotInventAnActorOrMissingDisplaySnapshot()
    {
        var dead = Mon(); dead = dead with { Pokemon = dead.Pokemon with { CurrentHp = 0 } };
        var events = Pair(); var seed = Seed(actor: dead);
        Assert.IsNull(At(Double(events, 1, seed), [new("pret:MOVE_DIG"), null, null, null]));
        var live = Double(events, 1, Seed()); Assert.IsNotNull(At(live));
        Assert.IsNull(At(live with { DisplayedSlots = [], DisplayedMembers = [] }));
        var single = Single(events, 1, Seed(single: true));
        Assert.IsNull(At(single with { DisplayBatch = null }));
        Assert.IsNull(At(single with { DisplayBatch = single.DisplayBatch! with { NextEventIndex = 1 } }));
        Assert.IsNull(At(single with { DisplayBatch = single.DisplayBatch! with { NextEventIndex = 3 } }));
    }

    [TestMethod]
    public void MalformedForeignNonadjacentOrNonForecastPairsFailClosed()
    {
        var seed = Seed(); var good = Pair(); var ability = (SourceAbilityActivatedEvent)good[0]; var receipt = (SourceCalledOverlayChangedEvent)good[1];
        var after = receipt.After!;
        var malformed = new (string Name, ImmutableArray<SourceBattleEvent> Events)[] {
            ("missing ability", [receipt]),
            ("wrong ability", good.SetItem(0, ability with { Ability = "ABILITY_TRACE" })),
            ("foreign ability owner", good.SetItem(0, ability with { Side = new(1) })),
            ("foreign turn", good.SetItem(0, ability with { Turn = 6 })),
            ("same sequence", good.SetItem(0, ability with { Sequence = receipt.Sequence })),
            ("sequence gap", good.SetItem(1, receipt with { Sequence = 3 })),
            ("intervening boundary", [ability, new SourceTurnReadyEvent(2, 7, 0), receipt with { Sequence = 3 }]),
            ("wrong move", good.SetItem(1, receipt with { Move = new("pret:MOVE_SKILL_SWAP") })),
            ("Trace donor", good.SetItem(1, receipt with { Source = new(1) })),
            ("foreign overlay owner", good.SetItem(1, receipt with { Side = new(1) })),
            ("missing after", good.SetItem(1, receipt with { After = null })),
            ("missing form", good.SetItem(1, receipt with { After = after with { CastformForm = null } })),
            ("invalid form", good.SetItem(1, receipt with { After = after with { CastformForm = (SourceCastformForm)99 } })),
            ("wrong first type", good.SetItem(1, receipt with { After = after with { Type1 = "TYPE_FIRE" } })),
            ("wrong second type", good.SetItem(1, receipt with { After = after with { Type2 = "TYPE_ICE" } })),
            ("ability mutation", good.SetItem(1, receipt with { After = after with { Ability = "ABILITY_FORECAST" } })),
            ("wrong overlay version", good.SetItem(1, receipt with { After = after with { RulesVersion = "foreign" } })),
            ("before mismatch", good.SetItem(1, receipt with { Before = Overlay(SourceCastformForm.Sunny) })),
            ("identical overlay", good.SetItem(1, receipt with { Before = after, After = after })),
            ("duplicate prefix", [new SourceTurnReadyEvent(1, 7, 0), ability, receipt]),
            ("descending prefix", [new SourceTurnReadyEvent(5, 7, 0), ability, receipt])
        };
        Assert.IsNotNull(At(Double(good, 1, seed)), "The positive control must be admitted.");
        foreach (var row in malformed)
        {
            // Do not run malformed prefixes through the strict display reducer:
            // the admission matcher must itself reject them, not rely on a reducer exception.
            var battle = Double(good, 1, seed) with { Events = row.Events, EventIndex = row.Events.Length - 1 };
            Assert.IsNull(At(battle), row.Name);
        }
        Assert.IsNull(At(Double(good, 1, Seed(actor: Mon("SNORLAX", "DEFENSE_CURL")))));
        Assert.IsNull(At(Double(good, 1, Seed(actor: Mon() with { CalledOverlay = new() { Ability = "ABILITY_NONE" } }))));
    }

    [TestMethod]
    public void BeforeTypesCanDifferFromRetainedFormAndUnchangedNonvisualOverlayRegistersArePreserved()
    {
        // Acquired Color Change/Conversion can leave a prior visual form behind.
        // Forecast must match receipt.Before exactly, not re-derive it from weather.
        var before = Overlay(SourceCastformForm.Sunny) with { Type1 = "TYPE_GRASS", Type2 = "TYPE_GRASS", Ability = "ABILITY_FORECAST" };
        var actor = Mon() with { CalledOverlay = before }; var seed = Seed(actor: actor); var events = Pair(before: before);
        var cue = At(Double(events, 1, seed)); Assert.IsNotNull(cue);
        AssertBodies(cue, (SourceCalledOverlayChangedEvent)events[1], actor);
        Assert.AreEqual("ABILITY_FORECAST", cue.AfterBody.CalledOverlay!.Ability);
        var sameForm = Pair(before: before, after: SourceCastformForm.Sunny);
        var restored = At(Double(sameForm, 1, seed)); Assert.IsNotNull(restored);
        Assert.AreEqual(SourceCastformForm.Sunny, Form(restored.BeforeBody));
        Assert.AreEqual(SourceCastformForm.Sunny, Form(restored.AfterBody));
        Assert.AreEqual("TYPE_GRASS", restored.BeforeBody.CalledOverlay!.Type1);
        Assert.AreEqual("TYPE_FIRE", restored.AfterBody.CalledOverlay!.Type1);
        Assert.IsFalse(restored.Immediate, "Source Forecast animates a same-form type restoration.");
    }

    [TestMethod]
    public void SinglesCanonicalMirrorAndAppliedAfterMustAgreeExactly()
    {
        var events = Pair(); var seed = Seed(single: true); var battle = Single(events, 1, seed);
        var receipt = (SourceCalledOverlayChangedEvent)battle.Events[1];
        Assert.IsNotNull(At(battle));
        // A canonical receipt stream remains mandatory; there is no separate legacy mirror.
        Assert.IsNull(At(battle with { Events = [] }));
        Assert.IsNull(At(battle with { Events = battle.Events.SetItem(1, receipt with { After = Overlay(SourceCastformForm.Snowy) }) }));
        // A visual Sunny -> Rainy canonical receipt is valid. The retired duplicate-array
        // disagreement is replaced by an actual retained-register inconsistency.
        Assert.IsNull(At(battle with { Events = battle.Events.SetItem(1, receipt with {
            Before = Overlay(SourceCastformForm.Sunny) with { Ability = "ABILITY_NONE" } }) }));
        var sunny = Overlay(SourceCastformForm.Sunny); var canonical = Pair(before: sunny);
        var actor = Mon() with { CalledOverlay = sunny };
        var singleCue = At(Single(canonical, 1, Seed(single: true, actor: actor)));
        var doubleCue = At(Double(canonical, 1, Seed(actor: actor)));
        Assert.IsNotNull(singleCue); Assert.IsNotNull(doubleCue);
        SameCue(doubleCue with { SpriteVisibility = doubleCue.SpriteVisibility.Take(2).ToImmutableArray() }, singleCue,
            "Both layouts consume the same authoritative Forecast receipt without a duplicate stream.");
        var display = battle.DisplayBatch!.Display;
        Assert.IsNull(At(battle with { DisplayBatch = battle.DisplayBatch with {
            Display = display with { Bodies = display.Bodies.SetItem(0, display.Bodies[0] with { CalledOverlay = Overlay(SourceCastformForm.Snowy) }) }
        } }));
        Assert.IsNull(At(battle with { EventIndex = -1 }));
    }

    [TestMethod]
    public void SceneOffPreservesCastformButPhasesSourceProfileAndEntryShapeRemainStrict()
    {
        var events = Pair(); var d = Double(events, 1, Seed()); var s = Single(events, 1, Seed(single: true));
        SameCue(At(d), At(d, scene: SourceBattleScene.Off)); SameCue(At(s), At(s, scene: SourceBattleScene.Off));
        Assert.IsNull(At(d with { Stage = CampaignDoubleBattleStage.Intro }));
        Assert.IsNull(At(d with { Stage = CampaignDoubleBattleStage.Finished }));
        Assert.IsNull(At(d with { EventIndex = -1 })); Assert.IsNull(At(d with { EventIndex = 2 }));
        Assert.IsNull(CampaignCastformAnimationPresentation.AtResult(s, false, SourceBattleScene.On, new MoveId?[2]));
        Assert.IsNull(At(s with { Phase = CampaignBattlePhase.ChooseMove }));
        Assert.Throws<ArgumentException>(() => CampaignCastformAnimationPresentation.AtResult(d, SourceBattleScene.On, new MoveId?[2]));
        Assert.Throws<ArgumentNullException>(() => CampaignCastformAnimationPresentation.AtResult(d, SourceBattleScene.On, null!));
        Assert.Throws<ArgumentException>(() => CampaignCastformAnimationPresentation.AtResult(s, true, SourceBattleScene.On, new MoveId?[4]));
        Assert.Throws<ArgumentNullException>(() => CampaignCastformAnimationPresentation.AtResult(s, true, SourceBattleScene.On, null!));
    }
}
