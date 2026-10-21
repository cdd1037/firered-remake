using System.Collections.Immutable;
using System.Text.Json;
using System.Reflection;
using Game.Domain.Trainers;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Events;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class CampaignBattleDisplayBodiesTests
{
    private static SourceMechanicsCatalog Mechanics=>TestAssets.Cached(()=>SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static WildBattleCombatant Mon(string species="CASTFORM")=>new(SourcePokemonFactory.Create(Mechanics,
        new(new("pret:SPECIES_"+species),30,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon);
    private static ImmutableArray<SourceBattleSlot> Slots(bool doubles=false)=>Enumerable.Range(0,4).Select(i=>
        !doubles&&i>=2?new SourceBattleSlot(new(i),null,0,null):new(new(i),new((BattleSide)(i&1),i/2),1,Mon())).ToImmutableArray();
    private static SourceCalledOverlayChangedEvent Form(long sequence,int slot,SourceCastformForm form)=>
        new(sequence,1,new(slot),new("pret:MOVE_NONE"),null,new(){CastformForm=form});

    [TestMethod,DataRow(false),DataRow(true)]
    public void SourceSlotShapeAndFaintedBodiesAreRetained(bool doubles)
    {
        var slots=Slots(doubles);var body=slots[1].Body!;
        slots=slots.SetItem(1,slots[1] with{Body=body with{Pokemon=body.Pokemon with{CurrentHp=0}}});
        var display=CampaignBattleDisplayBodies.FromSlots(slots);
        Assert.AreEqual(doubles?4:2,display.Bodies.Length);
        Assert.AreEqual(0,display.Bodies[1].Pokemon.CurrentHp);
        Assert.AreEqual(slots[1].Member,display.Members[1]);
        Assert.ThrowsExactly<InvalidDataException>(()=>CampaignBattleDisplayBodies.FromSlots(slots.SetItem(0,slots[0] with{Body=null})));
        Assert.ThrowsExactly<InvalidDataException>(()=>CampaignBattleDisplayBodies.FromSlots(slots.SetItem(1,slots[1] with{Slot=new(3)})));
    }

    [TestMethod]
    public void SkippedCanonicalEventsAndRepeatedTrainerMirrorSequenceApplyOnce()
    {
        var seed=CampaignBattleDisplayBodies.FromSlots(Slots());
        ImmutableArray<SourceBattleEvent> events=[new SourceStartedEvent(1,1),Form(2,1,SourceCastformForm.Rainy),Form(4,1,SourceCastformForm.Snowy),new SourceTurnReadyEvent(5,1,0)];
        var initial=new CampaignBattleDisplayBatch(seed);
        var first=initial.AdvanceThrough(events,2);
        Assert.AreEqual(2,first.NextEventIndex);
        Assert.AreEqual(SourceCastformForm.Rainy,first.Display.Bodies[1].CalledOverlay!.CastformForm);
        Assert.AreEqual(first,first.AdvanceThrough(events,2));
        Assert.IsNull(initial.Display.Bodies[1].CalledOverlay);
        var final=first.AdvanceThrough(events,long.MaxValue);
        Assert.AreEqual(4,final.NextEventIndex);
        Assert.AreEqual(SourceCastformForm.Snowy,final.Display.Bodies[1].CalledOverlay!.CastformForm);
    }

    [TestMethod]
    public void TransformCopiesEventTimeDonorBeforeLaterWeatherChange()
    {
        var seed=CampaignBattleDisplayBodies.FromSlots(Slots());
        var donor=seed.Bodies[1].Pokemon;var actor=seed.Bodies[0].Pokemon;
        ImmutableArray<SourceBattleEvent> events=[Form(1,1,SourceCastformForm.Sunny),
            new SourceTransformedEvent(2,1,new(0),new(1),actor.Species,donor.Species,donor.Personality,donor.OriginalTrainerId,donor),
            Form(3,1,SourceCastformForm.Rainy)];
        var batch=new CampaignBattleDisplayBatch(seed).AdvanceThrough(events,long.MaxValue);
        Assert.AreEqual(SourceCastformForm.Sunny,batch.Display.Bodies[0].CalledOverlay!.CastformForm);
        Assert.AreEqual(SourceCastformForm.Rainy,batch.Display.Bodies[1].CalledOverlay!.CastformForm);
        Assert.IsNotNull(batch.Display.Bodies[0].Transform);
    }

    [TestMethod]
    public void ReplacementOwnsItsMembershipBeforeEntryFormReceipt()
    {
        var seed=CampaignBattleDisplayBodies.FromSlots(Slots()).Apply(Form(1,0,SourceCastformForm.Sunny));
        var replacement=Mon();
        ImmutableArray<SourceBattleEvent> events=[new SourceCalledPartySwitchedEvent(2,1,new(0),0,2,WildBattleCalledSwitchKind.Voluntary)
            {Replacement=new(replacement,new())},Form(3,0,SourceCastformForm.Snowy)];
        var switched=new CampaignBattleDisplayBatch(seed).AdvanceThrough(events,2);
        Assert.IsNull(switched.Display.Bodies[0].CalledOverlay);
        Assert.AreEqual(new PartyMemberKey(BattleSide.Player,2),switched.Display.Members[0]);
        Assert.AreEqual(SourceCastformForm.Snowy,switched.AdvanceThrough(events,3).Display.Bodies[0].CalledOverlay!.CastformForm);
    }

    [TestMethod]
    public void HpProjectionIsImmutableAndUsesCommittedReceipt()
    {
        var seed=CampaignBattleDisplayBodies.FromSlots(Slots());int hp=seed.Bodies[1].Pokemon.CurrentHp;
        var after=seed.Apply(new SourceHpChangedEvent(1,1,new(0),new(1),new("pret:MOVE_POUND"),hp,hp-3,3,false,100));
        Assert.AreEqual(hp,seed.Bodies[1].Pokemon.CurrentHp);Assert.AreEqual(hp-3,after.Bodies[1].Pokemon.CurrentHp);
    }

    [TestMethod]
    public void RealWildEntryCapturesPreAbilityBodiesWithoutChangingReceiptsOrRng()
    {
        var player=Mon();var enemy=Mon("KYOGRE");
        var result=SourceWildBattlePolicy.Start(Mechanics,[player],0,enemy,new(37));
        Assert.IsTrue(result.Succeeded,result.Fault);Assert.IsFalse(result.EntrySlots.IsDefaultOrEmpty);
        Assert.IsNull(result.EntrySlots[0].Body!.CalledOverlay);
        var projected=new CampaignBattleDisplayBatch(CampaignBattleDisplayBodies.FromSlots(result.EntrySlots)).AdvanceThrough(result.Events,long.MaxValue);
        Assert.AreEqual(result.State!.Get(new(0)).Body!.CalledOverlay,projected.Display.Bodies[0].CalledOverlay);
        string before=JsonSerializer.Serialize(result);
        var repeated=SourceWildBattlePolicy.Start(Mechanics,[player],0,enemy,new(37));
        Assert.AreEqual(before,JsonSerializer.Serialize(repeated));
        Assert.IsFalse(before.Contains("EntrySlots",StringComparison.Ordinal));
        Assert.AreEqual(result.State.Rng,repeated.State!.Rng);
    }

    private static CampaignWildBattle Advance(CampaignWildBattle battle, long sequence) =>
        (CampaignWildBattle)typeof(CampaignSession).GetMethod("AdvanceDisplayThrough", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [battle, sequence])!;

    [TestMethod]
    public void TrainerSwitchSnapshotUsesPreReceiptIdentityAndRetainsOneCanonicalCursor()
    {
        var slots = Slots(); var seed = CampaignBattleDisplayBodies.FromSlots(slots).Apply(Form(1, 0, SourceCastformForm.Sunny));
        var replacement = Mon();
        var switched = new SourceCalledPartySwitchedEvent(2, 1, new(0), 0, 2, WildBattleCalledSwitchKind.Voluntary) {
            Replacement = new(replacement, new()) };
        ImmutableArray<SourceBattleEvent> events = [switched, Form(3, 0, SourceCastformForm.Snowy)];
        // Only encounter presence is read by this presentation helper. This is not a gameplay-admission fixture.
        var frame = new SourceBattleFrame { Slots = slots, TrainerEncounter = new(null!, ForestTrainerBattleStyle.Shift, null!, new("pret:MOVE_NONE")) };
        var battle = new CampaignWildBattle("switch-cursor", null!, frame, events, 0,
            seed.Bodies[0].Pokemon, seed.Bodies[1].Pokemon, CampaignBattlePhase.Events, [], 0, []) { DisplayBatch = new(seed) };
        var visible = Advance(battle, switched.Sequence);
        Assert.IsNotNull(visible.SwitchDisplay);
        Assert.AreEqual(seed.Bodies[0], visible.SwitchDisplay.Previous);
        Assert.AreEqual(replacement, visible.SwitchDisplay.Replacement);
        Assert.AreEqual(0, visible.EventIndex); Assert.AreEqual(1, visible.DisplayBatch!.NextEventIndex);
        Assert.AreEqual(new PartyMemberKey(BattleSide.Player, 2), visible.DisplayBatch.Display.Members[0]);
        Assert.IsNull(visible.DisplayBatch.Display.Bodies[0].CalledOverlay);
        Assert.AreEqual(replacement.Pokemon.Species, visible.DisplayPlayer.Species);
        var repeated = Advance(visible, switched.Sequence);
        Assert.AreSame(visible.SwitchDisplay, repeated.SwitchDisplay);
        Assert.AreEqual(visible.DisplayBatch, repeated.DisplayBatch); Assert.AreEqual(visible.EventIndex, repeated.EventIndex);
        var next = Advance(visible with { EventIndex = 1 }, events[1].Sequence);
        Assert.IsNull(next.SwitchDisplay); Assert.AreEqual(2, next.DisplayBatch!.NextEventIndex);
        Assert.AreEqual(SourceCastformForm.Snowy, next.DisplayBatch.Display.Bodies[0].CalledOverlay!.CastformForm);
        Assert.IsNull(visible.DisplayBatch.Display.Bodies[0].CalledOverlay);
    }

    [TestMethod]
    public void HiddenReceiptsKeepCanonicalIndicesWithoutBorrowingFutureHpWithoutACheckpoint()
    {
        var slots = Slots(); var seed = CampaignBattleDisplayBodies.FromSlots(slots); int hp = seed.Bodies[1].Pokemon.CurrentHp;
        ImmutableArray<SourceBattleEvent> events = [
            new SourceRewardLearningEvent(1, 1, new(BattleSide.Player, 0), new(30, new("pret:MOVE_TACKLE")), SourceRewardLearningOutcome.Declined, null),
            new SourceTrainerOpponentSelectedEvent(2, 1, new(BattleSide.Opponent, 0)),
            new SourceTrainerShiftOfferedEvent(3, 1, new(BattleSide.Opponent, 0)),
            new SourceHpChangedEvent(4, 1, new(0), new(1), new("pret:MOVE_TACKLE"), hp, hp - 3, 3, false, 100),
            new SourceHpChangedEvent(5, 1, new(0), new(1), new("pret:MOVE_TACKLE"), hp - 3, hp - 10, 7, false, 100)];
        var battle = new CampaignWildBattle("hidden-cursor", null!, new() { Slots = slots }, events, 0,
            seed.Bodies[0].Pokemon, seed.Bodies[1].Pokemon, CampaignBattlePhase.Events, [], 0, []) { DisplayBatch = new(seed) };
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var initial = session.State with { WildBattle = battle };
        var prepare = typeof(CampaignSession).GetMethod("PrepareWildPresentation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var state = (CampaignState)prepare.Invoke(session, [initial])!; var visible = state.WildBattle!;
        Assert.AreEqual(3, visible.EventIndex); Assert.AreSame(events[3], visible.Events[visible.EventIndex]);
        Assert.AreEqual(4, visible.DisplayBatch!.NextEventIndex); Assert.AreEqual(hp - 3, visible.DisplayOpponent.CurrentHp);
        var following = (CampaignState)prepare.Invoke(session, [state with { WildBattle = visible with { EventIndex = 4 } }])!;
        Assert.AreEqual(4, following.WildBattle!.EventIndex); Assert.AreEqual(5, following.WildBattle.DisplayBatch!.NextEventIndex);
        Assert.AreEqual(hp - 10, following.WildBattle.DisplayOpponent.CurrentHp);
        Assert.AreEqual(hp, seed.Bodies[1].Pokemon.CurrentHp); Assert.AreEqual(initial.World, following.World);
    }

    [TestMethod]
    [DataRow("held-heal")]
    [DataRow("ability-recovery")]
    [DataRow("self-destruct")]
    [DataRow("fainted")]
    public void CanonicalHpReceiptsRefreshVisibleHpAndNonmutatingReceiptsCannotSnapBack(string kind)
    {
        foreach (int position in new[] { 0, 1 })
        {
            var slot = new BattlerSlot(position); var slots = Slots(); var original = slots[position].Body!;
            int hp = original.Pokemon.Stats.MaxHp - 20;
            slots = slots.SetItem(position, slots[position] with { Body = original with { Pokemon = original.Pokemon with { CurrentHp = hp } } });
            var seed = CampaignBattleDisplayBodies.FromSlots(slots);
            int expected = kind is "self-destruct" or "fainted" ? 0 : hp + 5;
            SourceBattleEvent receipt = kind switch {
                "held-heal" => new SourceHeldItemHealEvent(1, 1, slot, slot.Opposite, new("pret:MOVE_TACKLE"), "ITEM_SHELL_BELL", 40, hp, expected, 5),
                "ability-recovery" => new SourceAbilityRecoveredEvent(1, 1, slot, "ABILITY_RAIN_DISH", hp, expected, 5),
                "self-destruct" => new SourceSelfDestructEvent(1, 1, slot, new("pret:MOVE_SELF_DESTRUCT"), hp),
                _ => new SourceFaintedEvent(1, 1, slot) };
            ImmutableArray<SourceBattleEvent> events = [receipt, new SourceAbilityActivatedEvent(2, 1, slot, "ABILITY_RAIN_DISH")];
            var finalSlots = slots.SetItem(position, slots[position] with { Body = original with { Pokemon = original.Pokemon with { CurrentHp = 1 } } });
            var battle = new CampaignWildBattle("hp-refresh", null!, new() { Slots = finalSlots }, events, 0,
                seed.Bodies[0].Pokemon, seed.Bodies[1].Pokemon, CampaignBattlePhase.Events, [], 0, []) { DisplayBatch = new(seed) };
            var visible = Advance(battle, receipt.Sequence);
            Assert.AreEqual(expected, position == 0 ? visible.DisplayPlayer.CurrentHp : visible.DisplayOpponent.CurrentHp);
            Assert.AreEqual(expected, visible.DisplayBatch!.Display.Bodies[position].Pokemon.CurrentHp);
            Assert.AreEqual(1, visible.DisplayBatch.NextEventIndex);
            var following = Advance(visible with { EventIndex = 1 }, events[1].Sequence);
            Assert.AreEqual(expected, position == 0 ? following.DisplayPlayer.CurrentHp : following.DisplayOpponent.CurrentHp);
            Assert.AreEqual(2, following.DisplayBatch!.NextEventIndex);
            Assert.AreEqual(hp, seed.Bodies[position].Pokemon.CurrentHp);
            Assert.AreEqual(1, following.Frame.Slots[position].Body!.Pokemon.CurrentHp,
                "Visible HP is read from the canonical receipt projection, never the future frame.");
        }
    }

    [TestMethod]
    public void StartedReceiptRestoresSeedHpInsteadOfTheFinalFrame()
    {
        var slots = Slots(); var seed = CampaignBattleDisplayBodies.FromSlots(slots);
        var finalSlots = slots.Select(cell => cell.Body is { } body ? cell with {
            Body = body with { Pokemon = body.Pokemon with { CurrentHp = 1 } } } : cell).ToImmutableArray();
        ImmutableArray<SourceBattleEvent> events = [new SourceStartedEvent(1, 0), new SourceAbilityActivatedEvent(2, 0, new(0), "ABILITY_FORECAST")];
        var battle = new CampaignWildBattle("entry-hp", null!, new() { Slots = finalSlots }, events, 0,
            finalSlots[0].Body!.Pokemon, finalSlots[1].Body!.Pokemon, CampaignBattlePhase.Events, [], 0, []) { DisplayBatch = new(seed) };
        var started = Advance(battle, 1);
        Assert.AreEqual(seed.Bodies[0].Pokemon.CurrentHp, started.DisplayPlayer.CurrentHp);
        Assert.AreEqual(seed.Bodies[1].Pokemon.CurrentHp, started.DisplayOpponent.CurrentHp);
        var following = Advance(started with { EventIndex = 1 }, 2);
        Assert.AreEqual(started.DisplayPlayer, following.DisplayPlayer);
        Assert.AreEqual(started.DisplayOpponent, following.DisplayOpponent);
        Assert.AreEqual(1, following.Frame.Slots[0].Body!.Pokemon.CurrentHp);
    }

    [TestMethod]
    public void RewardBodyAdvancesWithoutRevealingLevelOrHpBeforeItsResultPage()
    {
        var slots = Slots(); var seed = CampaignBattleDisplayBodies.FromSlots(slots); var before = seed.Bodies[0].Pokemon;
        var rewarded = before with { Level = before.Level + 1, Stats = before.Stats with { MaxHp = before.Stats.MaxHp + 2 }, CurrentHp = before.CurrentHp + 2 };
        var reward = new PokemonRewardResult(rewarded, 0, SixStats.Zero, 1, 0, 2, [], []);
        ImmutableArray<SourceBattleEvent> events = [
            new SourceRewardAppliedEvent(1, 1, new(new(1), new(BattleSide.Opponent, 0), 1), new(BattleSide.Player, 0), reward),
            new SourceTrainerShiftOfferedEvent(2, 1, new(BattleSide.Opponent, 0))];
        var battle = new CampaignWildBattle("reward-boundary", null!, new() { Slots = slots }, events, 0,
            before, seed.Bodies[1].Pokemon, CampaignBattlePhase.Events, [], 0, []) { DisplayBatch = new(seed) };
        var applied = Advance(battle, 1);
        Assert.AreEqual(rewarded, applied.DisplayBatch!.Display.Bodies[0].Pokemon);
        Assert.AreEqual(before, applied.DisplayPlayer);
        var following = Advance(applied with { EventIndex = 1 }, 2);
        Assert.AreEqual(rewarded, following.DisplayBatch!.Display.Bodies[0].Pokemon);
        Assert.AreEqual(before, following.DisplayPlayer, "Hidden diagnostics cannot reveal the reward before its level-up page.");
    }

    private static SourceBattleFrame TrainerFrame(ImmutableArray<SourceBattleSlot> slots) => new() {
        Slots = slots,
        BattleKind = WildBattleCalledBattleKind.Trainer,
        TrainerEncounter = new(null!, ForestTrainerBattleStyle.Shift, null!, new("pret:MOVE_NONE")),
        Parties = [new(BattleSide.Player, [new(slots[0].Body!.Pokemon, "ITEM_NONE", 0)]),
            new(BattleSide.Opponent, [new(slots[1].Body!.Pokemon, "ITEM_NONE", 0)])] };

    private static CampaignState PrepareTrainer(CampaignSession session, CampaignState state) =>
        (CampaignState)typeof(CampaignSession).GetMethod("PrepareForestTrainerPresentation", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(session, [state])!;

    [TestMethod, DataRow(0), DataRow(1)]
    public void TrainerSwitchCanonicalBodyAdvancesBeforeItsExistingVisibleMirrorBoundary(int position)
    {
        var slots = Slots(); var seed = CampaignBattleDisplayBodies.FromSlots(slots);
        var outgoing = seed.Bodies[position]; var incoming = Mon("DITTO");
        var appearance = new CampaignWildAppearance(outgoing.Pokemon.Species, outgoing.Pokemon.Personality, outgoing.Pokemon.OriginalTrainerId);
        var slot = new BattlerSlot(position);
        var receipt = new SourceCalledPartySwitchedEvent(1, 1, slot, 0, 1,
            position == 0 ? WildBattleCalledSwitchKind.Forced : WildBattleCalledSwitchKind.FaintedReplacement) {
            Replacement = new(incoming, new()) };
        ImmutableArray<SourceBattleEvent> events = [receipt];
        ForestTrainerCombatEvent mirror = position == 0
            ? new SsAnneTrainerPlayerForcedOutEvent(1, 1, 0, 1, outgoing, incoming)
            : new ForestTrainerOpponentReplacedEvent(1, 1, 0, 1, outgoing, incoming);
        var finalSlots = slots.SetItem(position, slots[position] with { Member = new(slot.Side, 1), Body = incoming });
        var battle = new CampaignWildBattle("trainer-switch-boundary", null!, TrainerFrame(finalSlots), events, 0,
            seed.Bodies[0].Pokemon, seed.Bodies[1].Pokemon, CampaignBattlePhase.Events, [], 0, []) {
            DisplayBatch = new(seed), DisplayPlayerAppearance = position == 0 ? appearance : null,
            DisplayOpponentAppearance = position == 1 ? appearance : null };
        var trainer = new CampaignForestTrainerBattle(battle.Id, new("fixture-trainer"), "fixture-actor", battle,
            CampaignForestTrainerStage.Combat, [], [mirror], 0, 0, []);
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var first = PrepareTrainer(session, session.State with { ForestTrainerBattle = trainer });
        var canonical = first.ForestTrainerBattle!.Presentation;
        Assert.AreEqual(0, canonical.EventIndex); Assert.AreEqual(1, canonical.DisplayBatch!.NextEventIndex);
        Assert.AreEqual(incoming, canonical.DisplayBatch.Display.Bodies[position]);
        Assert.AreEqual(new PartyMemberKey(slot.Side, 1), canonical.DisplayBatch.Display.Members[position]);
        Assert.AreEqual(outgoing.Pokemon, position == 0 ? canonical.DisplayPlayer : canonical.DisplayOpponent,
            "Canonical bookkeeping must not reveal the replacement before its existing trainer text boundary.");
        Assert.AreSame(appearance, position == 0 ? canonical.DisplayPlayerAppearance : canonical.DisplayOpponentAppearance);
        Assert.IsNull(first.ForestTrainerBattle.CurrentTrainerEvent);
        var repeated = Advance(canonical, receipt.Sequence);
        Assert.AreEqual(canonical.DisplayPlayer, repeated.DisplayPlayer); Assert.AreEqual(canonical.DisplayOpponent, repeated.DisplayOpponent);
        var second = PrepareTrainer(session, first with { ForestTrainerBattle = first.ForestTrainerBattle with {
            Presentation = canonical with { EventIndex = 1 } } });
        var displayed = second.ForestTrainerBattle!.Presentation;
        Assert.AreSame(mirror, second.ForestTrainerBattle.CurrentTrainerEvent);
        Assert.AreEqual(incoming.Pokemon, position == 0 ? displayed.DisplayPlayer : displayed.DisplayOpponent);
        Assert.IsNull(position == 0 ? displayed.DisplayPlayerAppearance : displayed.DisplayOpponentAppearance);
        Assert.AreEqual(1, displayed.DisplayBatch!.NextEventIndex,
            "Showing the trainer mirror does not consume or apply the canonical receipt twice.");
        Assert.AreEqual(1, displayed.EventIndex); Assert.AreEqual(0, second.ForestTrainerBattle.TrainerEventIndex);
    }

    [TestMethod]
    public void TrainerRewardDrainKeepsPreRewardLevelAndHpOnTheFirstExperiencePage()
    {
        var slots = Slots(); var seed = CampaignBattleDisplayBodies.FromSlots(slots); var before = seed.Bodies[0].Pokemon;
        var rewarded = before with { Level = before.Level + 1, Experience = before.Experience + 100,
            Stats = before.Stats with { MaxHp = before.Stats.MaxHp + 2 }, CurrentHp = before.CurrentHp + 2 };
        var experience = new WildRewardExperienceEvent(before.Experience, rewarded.Experience, 100);
        var level = new WildRewardLevelEvent(before.Level, rewarded.Level, before.Stats, rewarded.Stats, before.CurrentHp, rewarded.CurrentHp);
        var reward = new PokemonRewardResult(rewarded, 100, SixStats.Zero, 1, 0, 2, [experience, level], []);
        ImmutableArray<SourceBattleEvent> events = [new SourceRewardAppliedEvent(1, 1,
            new(new(1), new(BattleSide.Opponent, 0), 1), new(BattleSide.Player, 0), reward)];
        var finalSlots = slots.SetItem(0, slots[0] with { Body = slots[0].Body! with { Pokemon = rewarded } });
        var battle = new CampaignWildBattle("trainer-reward-boundary", null!, TrainerFrame(finalSlots), events, 0,
            before, seed.Bodies[1].Pokemon, CampaignBattlePhase.Events,
            [new("experience page", experience, 0), new("level page", level, 0)], 0, []) { DisplayBatch = new(seed) };
        var trainer = new CampaignForestTrainerBattle(battle.Id, new("fixture-trainer"), "fixture-actor", battle,
            CampaignForestTrainerStage.Combat, [], [], 0, 100, []);
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var state = PrepareTrainer(session, session.State with { ForestTrainerBattle = trainer });
        var visible = state.ForestTrainerBattle!.Presentation;
        Console.WriteLine("trainer_reward_page_boundary " + JsonSerializer.Serialize(new {
            BeforeLevel = before.Level, BeforeHp = before.CurrentHp, FinalLevel = rewarded.Level, FinalHp = rewarded.CurrentHp,
            VisibleLevel = visible.DisplayPlayer.Level, VisibleHp = visible.DisplayPlayer.CurrentHp,
            visible.EventIndex, BodyCursor = visible.DisplayBatch!.NextEventIndex, visible.PageIndex,
            Phase = visible.Phase.ToString(), FirstPage = visible.Pages[visible.PageIndex].Text }));
        Assert.AreEqual(CampaignForestTrainerStage.Rewards, state.ForestTrainerBattle.Stage);
        Assert.AreEqual(CampaignBattlePhase.Results, visible.Phase); Assert.AreEqual(0, visible.PageIndex);
        Assert.AreSame(experience, visible.Pages[visible.PageIndex].Reward);
        Assert.AreEqual(1, visible.EventIndex); Assert.AreEqual(1, visible.DisplayBatch!.NextEventIndex);
        Assert.AreEqual(rewarded, visible.DisplayBatch.Display.Bodies[0].Pokemon);
        Assert.AreEqual(before.Level, visible.DisplayPlayer.Level, "The first EXP page precedes the level-up visible boundary.");
        Assert.AreEqual(before.CurrentHp, visible.DisplayPlayer.CurrentHp, "Reward drain must not reveal future level-up HP.");
    }

    [TestMethod, DataRow(0), DataRow(1)]
    public void TrainerRewardAcknowledgementsRevealEachLevelReceiptOnlyForTheActiveRecipient(int recipient)
    {
        var slots = Slots(); var active = slots[0].Body!.Pokemon;
        var reserve = Mon("DITTO").Pokemon; var before = recipient == 0 ? active : reserve;
        var firstStats = before.Stats with { MaxHp = before.Stats.MaxHp + 2, Attack = before.Stats.Attack + 1 };
        var secondStats = firstStats with { MaxHp = firstStats.MaxHp + 3, Defense = firstStats.Defense + 2 };
        var first = new WildRewardLevelEvent(before.Level, before.Level + 1, before.Stats, firstStats, before.CurrentHp, before.CurrentHp + 2);
        var second = new WildRewardLevelEvent(before.Level + 1, before.Level + 2, firstStats, secondStats, before.CurrentHp + 2, before.CurrentHp + 5);
        var final = before with { Level = second.After, Stats = second.StatsAfter, CurrentHp = second.HpAfter,
            Experience = before.Experience + 500 };
        var finalActive = recipient == 0 ? final : active; var finalReserve = recipient == 1 ? final : reserve;
        var finalSlots = slots.SetItem(0, slots[0] with { Body = slots[0].Body! with { Pokemon = finalActive } });
        var frame = TrainerFrame(finalSlots) with { Parties = [
            new(BattleSide.Player, [new(finalActive, "ITEM_NONE", 0), new(finalReserve, "ITEM_NONE", 0)]),
            new(BattleSide.Opponent, [new(slots[1].Body!.Pokemon, "ITEM_NONE", 0)])] };
        PartyPokemon Owned(long id, PokemonInstance mon) => new(id, mon, null, "RED", 0, 2,
            "MAPSEC_ROUTE_1", mon.Level, 4, "ITEM_POKE_BALL", "ITEM_NONE");
        var experience = new WildRewardExperienceEvent(before.Experience, final.Experience, 500);
        var battle = new CampaignWildBattle("trainer-two-level-pages", null!, frame, [], 0,
            active, slots[1].Body!.Pokemon, CampaignBattlePhase.Results,
            [new("experience page", experience, recipient), new("first level page", first, recipient),
                new("second level page", second, recipient), new("following page", null, recipient)], 0, []) {
            DisplayBatch = new(CampaignBattleDisplayBodies.FromSlots(finalSlots)), ActivePartyIndex = 0 };
        var trainer = new CampaignForestTrainerBattle(battle.Id, new("fixture-trainer"), "fixture-actor", battle,
            CampaignForestTrainerStage.Rewards, [], [], 0, 500, []);
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var state = session.State with { ForestTrainerBattle = trainer, Party = [Owned(101, finalActive), Owned(102, finalReserve)],
            Script = session.State.Script with { Wait = new(StoryWaitKind.Battle, "trainer-two-level-pages") } };
        var party = state.Party; var world = state.World;
        var decide = typeof(CampaignSession).GetMethod("DecideForestTrainerBattle", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var (page, index) in new[] { (first, 1), (second, 2) })
        {
            var decision = (Decision<CampaignState>)decide.Invoke(session, [state, new CampaignCommand("ack")])!;
            Assert.AreEqual(CommandStatus.Accepted, decision.Status, decision.Error);
            state = decision.State; var visible = state.ForestTrainerBattle!.Presentation;
            Console.WriteLine("trainer_reward_ack_boundary " + JsonSerializer.Serialize(new {
                Recipient = recipient, Page = index, ReceiptLevel = page.After, ReceiptHp = page.HpAfter,
                VisibleLevel = visible.DisplayPlayer.Level, VisibleHp = visible.DisplayPlayer.CurrentHp,
                FinalLevel = final.Level, FinalHp = final.CurrentHp }));
            Assert.AreEqual(index, visible.PageIndex); Assert.AreSame(page, visible.Pages[index].Reward);
            Assert.AreEqual(recipient == 0 ? page.After : active.Level, visible.DisplayPlayer.Level);
            Assert.AreEqual(recipient == 0 ? page.StatsAfter : active.Stats, visible.DisplayPlayer.Stats);
            Assert.AreEqual(recipient == 0 ? page.HpAfter : active.CurrentHp, visible.DisplayPlayer.CurrentHp);
            Assert.AreEqual(recipient == 0
                ? GrowthRules.ExperienceAtLevel(Mechanics.Catalog.Species[active.Species].GrowthRate, page.After)
                : active.Experience, visible.DisplayPlayer.Experience,
                "Each level page shows its growth threshold, not the final leftover experience.");
            Assert.AreEqual(active.Species, visible.DisplayPlayer.Species);
            Assert.AreEqual(party, state.Party); Assert.AreEqual(world, state.World);
            Assert.AreSame(frame, visible.Frame); Assert.IsTrue(visible.Events.IsEmpty); Assert.IsTrue(visible.RandomTrace.IsEmpty);
        }
        var previous = state.ForestTrainerBattle!.Presentation.DisplayPlayer;
        var following = (Decision<CampaignState>)decide.Invoke(session, [state, new CampaignCommand("ack")])!;
        Assert.AreEqual(CommandStatus.Accepted, following.Status, following.Error);
        Assert.AreEqual(3, following.State.ForestTrainerBattle!.Presentation.PageIndex);
        Assert.AreEqual(previous, following.State.ForestTrainerBattle.Presentation.DisplayPlayer,
            "A non-level page must not replace event-time display with the final party snapshot.");
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void TrainerRewardCompletionSynchronizesFinalExperienceAndClearsStaleAppearance(bool leveled)
    {
        var slots = Slots(); var before = slots[0].Body!.Pokemon;
        var displayed = leveled ? before with { Level = before.Level + 1,
            Stats = before.Stats with { MaxHp = before.Stats.MaxHp + 2 }, CurrentHp = before.CurrentHp + 2,
            Experience = GrowthRules.ExperienceAtLevel(Mechanics.Catalog.Species[before.Species].GrowthRate, before.Level + 1) }
            : before;
        var final = displayed with { Experience = displayed.Experience + 123 };
        var finalSlots = slots.SetItem(0, slots[0] with { Body = slots[0].Body! with { Pokemon = final } });
        var frame = TrainerFrame(finalSlots);
        var reward = new WildRewardExperienceEvent(before.Experience, final.Experience, checked((int)(final.Experience - before.Experience)));
        var battle = new CampaignWildBattle("trainer-reward-complete", null!, frame, [], 0,
            displayed, slots[1].Body!.Pokemon, CampaignBattlePhase.Results,
            [new("last reward page", reward, 0)], 0, []) {
            DisplayBatch = new(CampaignBattleDisplayBodies.FromSlots(finalSlots)),
            DisplayPlayerAppearance = new(new("pret:SPECIES_DITTO"), 7, 8) };
        var trainer = new CampaignForestTrainerBattle(battle.Id, new("fixture-trainer"), "fixture-actor", battle,
            CampaignForestTrainerStage.Rewards, [], [], 0, reward.Gained, []);
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var state = session.State with { ForestTrainerBattle = trainer };
        var complete = typeof(CampaignSession).GetMethod("ContinueForestTrainerAfterRewards", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var finished = (CampaignState)complete.Invoke(session, [state])!;
        var visible = finished.ForestTrainerBattle!.Presentation;
        Assert.AreEqual(final, visible.DisplayPlayer,
            "After all reward pages, final EXP must replace either the pre-reward total or the last level threshold.");
        Assert.IsNull(visible.DisplayPlayerAppearance);
        Assert.AreEqual(CampaignForestTrainerStage.Combat, finished.ForestTrainerBattle.Stage);
        Assert.AreEqual(CampaignBattlePhase.ChooseMove, visible.Phase);
        Assert.IsTrue(visible.Pages.IsEmpty); Assert.AreEqual(0, visible.PageIndex);
        Assert.AreEqual(displayed, battle.DisplayPlayer); Assert.IsNotNull(battle.DisplayPlayerAppearance);
        Assert.AreSame(frame, visible.Frame); Assert.AreEqual(battle.DisplayBatch, visible.DisplayBatch);
        Assert.AreEqual(state.Party, finished.Party); Assert.AreEqual(state.World, finished.World);
    }

    [TestMethod]
    public void TransientBatchIsExcludedFromCampaignBattleSerialization()
    {
        var slots=Slots();var frame=new SourceBattleFrame{Slots=slots};
        var battle=new CampaignWildBattle("test",null!,frame,[],0,slots[0].Body!.Pokemon,slots[1].Body!.Pokemon,CampaignBattlePhase.Events,[],0,[])
            {DisplayBatch=new(CampaignBattleDisplayBodies.FromSlots(slots))};
        Assert.IsFalse(JsonSerializer.Serialize(battle).Contains("DisplayBatch",StringComparison.Ordinal));
    }
}
