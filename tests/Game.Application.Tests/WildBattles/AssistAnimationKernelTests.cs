using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using F = FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Numbering = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class AssistAnimationKernelTests
{
    private static MoveId Move => Kernel.Id("ASSIST");
    private static SourceBattleFrame Initial(bool single, int actor = 0, string leaf = "TACKLE", bool candidates = true)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon("SKITTY", "ASSIST") with { SpeedStage = 12, AccuracyStage = 12 };
        var donor = candidates ? Kernel.Mon(leaf == "SPLASH" ? "WAILORD" : "PIDGEY", leaf, 100) : Kernel.Mon("SKITTY", "ASSIST");
        if (!single) bodies[actor ^ 2] = donor with { SpeedStage = 0 };
        var frame = Kernel.Frame(bodies, single);
        if (single && candidates)
        {
            var party = frame.Parties[actor & 1];
            frame = frame with { Parties = frame.Parties.SetItem(actor & 1, party with { Members = party.Members.Add(new(donor.Pokemon)) }) };
        }
        Assert.IsNull(SourceBattleIdentityRules.Validate(frame)); return frame;
    }
    private static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events, bool single)
    {
        string before = JsonSerializer.Serialize(events); var attack = events.OfType<SourceAttackStringEvent>().Single(e => e.CurrentMove == Move);
        var selection = events.OfType<SourceCallerSelectionEvent>().Single(); var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single();
        Assert.IsTrue(AnimationDispatchFixture.Supports(274, Move.Value)); Assert.AreEqual(Move, selection.Caller);
        Assert.IsNotNull(selection.Result); Assert.IsTrue(selection.CandidateSourceMoveIds.Contains(Kernel.Mechanics.Catalog.Moves[selection.Result.Value].SourceMoveId));
        Assert.AreEqual(selection.Result, transition.Invocation.CalledMove); Assert.IsTrue(attack.Sequence < selection.Sequence && selection.Sequence < transition.Sequence);
        Assert.IsFalse(events.Any(e => e.Sequence < transition.Sequence && e is SourcePpCommandEvent));
        CampaignMoveAnimationDispatch? found = null;
        for (int i = 0; i < events.Length; i++) foreach (bool result in new[] { false, true })
        {
            var decision = F.Project(events, i, single, result); Assert.AreEqual(decision, F.Project(events, i, single, result));
            Assert.IsNull(F.Project(events, i, single, result, SourceBattleScene.Off).Dispatch);
            if (events[i] != attack || result)
            {
                if (result) Assert.IsNull(decision.Dispatch);
                else CalledLeafAnimationKernelFixture.AssertSelectedTextCue(events, i, selection, transition, decision.Dispatch);
                continue;
            }
            Assert.IsNull(found); found = decision.Dispatch; Assert.IsNotNull(found, decision.Reason);
            Assert.AreEqual(274, found.SourceMoveId); Assert.AreEqual(attack.Sequence, found.Sequence); Assert.AreEqual(attack.Sequence, found.AttackSequence);
            Assert.AreEqual(selection.Sequence, found.ResultSequence); Assert.AreEqual(attack.Sequence, found.SnapshotSequence); Assert.IsNull(found.PpSequence);
            Assert.AreEqual(attack.Actor, found.Actor); Assert.AreEqual(transition.Invocation.Target, found.Target);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterCallerText, found.Boundary); Assert.IsTrue(found.AcknowledgesCurrentReceipt);
            Assert.IsNull(found.MoveDamage); Assert.IsNull(found.MovePower); Assert.IsNull(found.MoveTurn);
        }
        Assert.IsNotNull(found);
        if (CalledLeafAnimationKernelFixture.HasSimpleLeafResult(events, selection.Result!.Value))
            CalledLeafAnimationKernelFixture.Check(events, events, single, Move, Kernel.Mechanics.Catalog.Moves[selection.Result.Value].SourceMoveId);
        else CallerPreludeAnimationKernelFixture.DenyLeaf(events, single, Kernel.Mechanics.Catalog.Moves[selection.Result!.Value].SourceMoveId);
        Assert.AreEqual(before, JsonSerializer.Serialize(events));
        if (single) Assert.IsNull(AnimationDispatchFixture.AtAttackText(F.Single(events, events.IndexOf(attack)), false, SourceBattleScene.On).Dispatch);
        return found;
    }

    [TestMethod, DataRow("TACKLE"), DataRow("SPLASH")]
    public void NaturalAssistPreludeUsesTheActuallySelectedTargetAcrossAllBattlePositions(string leaf)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int requested in new[] { actor, actor ^ 1 })
        {
            var result = Envelope.Resolve(Initial(single, actor, leaf), actor, requested); Check(result.Events, single);
            var transition = result.Events.OfType<SourceCalledMoveTransitionEvent>().Single();
            Assert.AreEqual(Kernel.Id(leaf), transition.Invocation.CurrentMove);
            Assert.AreEqual(leaf == "SPLASH", transition.Invocation.Target == new BattlerSlot(actor));
            CalledLeafAnimationKernelFixture.Check(result, single, Move, leaf == "SPLASH" ? 150 : 33);
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single(); Assert.AreEqual(Move, pp.ChosenMove);
            Assert.AreEqual(Kernel.Id(leaf), pp.CurrentMove); Assert.AreEqual(new BattlerSlot(actor), pp.PpOwner);
            Assert.AreEqual(pp.Before - pp.CommandCost, pp.After);
        }
    }

    [TestMethod]
    public void SelectionAloneOwnsThePreludeBeforeAnyLeafPpOrFailedLeafOutcome()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single); foreach (int target in single ? new[] { 1 } : new[] { 1, 3 }) frame = F.Body(frame, target, b => b with { Protected = true });
            var result = Envelope.Resolve(frame); Check(result.Events, single);
            Assert.IsTrue(result.Events.Any(e => e is SourceMoveMissedEvent or SourceMoveFailedEvent));
            var transition = result.Events.OfType<SourceCalledMoveTransitionEvent>().Single();
            var prefix = result.Events.Take(result.Events.IndexOf(transition) + 1).ToImmutableArray();
            Assert.IsFalse(prefix.OfType<SourcePpCommandEvent>().Any()); Check(prefix, single);
        }
    }

    [TestMethod]
    public void RealLeafPressureSpendsTheAssistSlotAfterThePrelude()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single); foreach (int target in single ? new[] { 1 } : new[] { 1, 3 })
                frame = F.Body(frame, target, _ => Kernel.Mon("ARTICUNO", "MIST", 100) with { SpeedStage = 0 });
            var result = Envelope.Resolve(frame); Check(result.Events, single);
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single(); Assert.AreEqual(2, pp.CommandCost);
            Assert.AreEqual(Move, pp.ChosenMove); Assert.AreEqual(Kernel.Id("TACKLE"), pp.CurrentMove); Assert.AreEqual(0, pp.PpSlot);
            Assert.AreEqual(pp.Before - 2, result.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);
        }
    }

    [TestMethod, DataRow("sleep"), DataRow("no-pp")]
    public void PrimaryCancellationNeverReachesCallerSelection(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(single), 0, b => branch == "sleep" ? b with { Status = 3 }
                : b with { Pokemon = b.Pokemon with { Moves = [b.Pokemon.Moves[0] with { CurrentPp = 0 }] } });
            var result = Envelope.Resolve(frame); Assert.IsFalse(result.Events.Any(e => e is SourceCallerSelectionEvent or SourceCalledMoveTransitionEvent));
            Assert.IsFalse(result.Events.OfType<SourcePpCommandEvent>().Any()); F.Deny(result.Events, single);
        }
    }

    [TestMethod]
    public void AssistIgnoresDonorHpAndPpButExcludesEggAndNoEligiblePartyMoves()
    {
        foreach (bool single in new[] { false, true })
        {
            var empty = Envelope.Resolve(Initial(single, candidates: false));
            Assert.IsTrue(empty.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "assist-no-candidates"));
            Assert.IsNull(empty.Events.OfType<SourceCallerSelectionEvent>().Single().Result);
            Assert.IsFalse(empty.Events.OfType<SourceCalledMoveTransitionEvent>().Any()); F.Deny(empty.Events, single);
            var pp = empty.Events.OfType<SourcePpCommandEvent>().Single(); Assert.AreEqual(Move, pp.CurrentMove); Assert.AreEqual(pp.Before - pp.CommandCost, pp.After);
        }
        var frame = Initial(true); var party = frame.Parties[0]; var donor = party.Members[1];
        var spent = donor with { Pokemon = donor.Pokemon with { CurrentHp = 0, Moves = donor.Pokemon.Moves.Select(m => m with { CurrentPp = 0 }).ToImmutableArray() } };
        frame = frame with { Parties = frame.Parties.SetItem(0, party with { Members = party.Members.SetItem(1, spent) }) };
        Check(Envelope.Resolve(frame).Events, true);
        // Reserve eggs require authenticated owned metadata; a mechanics-only
        // frame cannot turn an ordinary exhausted donor into a carried egg.
        var content = CurrentCampaignFixture.Catalog.PokemonContent!; var owned = content.Pokemon;
        var gift = SourceGiftEggRules.GiveEgg(content, content.Catalog.BreedingData!, SourceGiftEggRules.Togepi, 1, new(42, "RED", 0), 4, new(0)).Egg;
        Assert.IsNull(PokemonStateValidator.ValidateParty(content, gift));
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("SNORLAX", "DEFENSE_CURL", 100)).ToArray();
        var natural = Kernel.Mon("MEOWTH", "SCRATCH");
        Assert.IsTrue(content.Catalog.BreedingData!.Species[natural.Pokemon.Species].EggMoves.Contains(Move));
        int maximum = content.Catalog.Moves[Move].Pp;
        // Controlled source-compatible inherited move, without claiming a breeding route.
        bodies[0] = natural with { Pokemon = natural.Pokemon with { Moves = [new(Move, maximum, maximum)] } };
        frame = Kernel.Frame(bodies, true); party = frame.Parties[0];
        frame = frame with { Campaign = owned, SourceCampaignRulesVersion = GamePokemonCatalog.Profile,
            Parties = frame.Parties.SetItem(0, party with { Members = party.Members.Add(new(gift.Pokemon, IsEgg: true)) }) };
        Assert.IsNull(SourcePokemonValidator.ValidateLive(owned.Mechanics, frame));
        var egg = SourceBattleKernel.ResolveAction(owned.Mechanics.Catalog, frame, new(frame.Slots[0].Occupant!.Value, frame.Revision, 0, new(1)));
        Assert.IsTrue(egg.Succeeded, egg.Fault); Assert.IsNull(egg.Events.OfType<SourceCallerSelectionEvent>().Single().Result);
        Assert.AreEqual(gift.Pokemon, egg.State.Parties[0].Members[1].Pokemon); F.Deny(egg.Events, true);
    }

    [TestMethod, DataRow("selection"), DataRow("duplicate"), DataRow("pool"), DataRow("result"), DataRow("called"),
        DataRow("owner"), DataRow("chosen"), DataRow("flags"), DataRow("target"), DataRow("slot")]
    public void AssistRejectsMissingOrForeignSelectionAndCalledTransitionOwnership(string mutation)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Initial(single)).Events; var selection = events.OfType<SourceCallerSelectionEvent>().Single();
            var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single(); int s = events.IndexOf(selection), t = events.IndexOf(transition);
            var call = transition.Invocation;
            if (mutation == "target")
            {
                if (single)
                {
                    // A canonical slot can exist outside a singles layout; the shared
                    // dispatch must still enforce this envelope's two-position guard.
                    int cursor = events.IndexOf(events.OfType<SourceAttackStringEvent>().First());
                    var exported = F.Single(events, cursor);
                    var legacy = (SourceCalledMoveTransitionEvent)exported.Events[t];
                    var invalid = legacy with { Invocation = legacy.Invocation with { Target = new BattlerSlot(2) } };
                    Assert.IsNull(AnimationDispatchFixture.AtAttackText(exported with { Events = exported.Events.SetItem(t, invalid) }, true, SourceBattleScene.On).Dispatch);
                }
                else Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new BattlerSlot(4));
                continue;
            }
            events = mutation switch {
                "selection" => Numbering.Number(events.Remove(selection)), "duplicate" => Numbering.Number(events.Insert(s + 1, selection)),
                "pool" => events.SetItem(s, selection with { CandidateSourceMoveIds = [] }), "result" => events.SetItem(s, selection with { Result = Kernel.Id("SPLASH") }),
                "called" => events.SetItem(t, transition with { Invocation = call with { CalledMove = null } }),
                "owner" => events.SetItem(t, transition with { Invocation = call with { PpOwner = new(1) } }),
                "chosen" => events.SetItem(t, transition with { Invocation = call with { ChosenMove = Kernel.Id("METRONOME") } }),
                "flags" => events.SetItem(t, transition with { Invocation = call with { Flags = call.Flags | WildBattleInvocationFlags.NoPpDeduct } }),
                _ => events.SetItem(t, transition with { Invocation = call with { PpSlot = 1 } }) };
            F.Deny(events, single);
        }
    }

    [TestMethod]
    public void AssistRequiresCurrentSourceDefinitionAndRealCatalogSupport()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Initial(single)).Events; int index = Array.FindIndex(events.ToArray(), e => e is SourceAttackStringEvent { CurrentMove.Value: "pret:MOVE_ASSIST" });
            var definitions = Kernel.Mechanics.Catalog.Moves; var definition = definitions[Move];
            foreach (var metadata in new[] { null, definitions.Remove(Move), definitions.SetItem(Move, definition with { SourceMoveId = 1 }),
                definitions.SetItem(Move, definition with { Power = 1 }), definitions.SetItem(Move, definition with { Target = "MOVE_TARGET_USER" }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(F.Single(events, index), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, index), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(F.Single(events, index), true, SourceBattleScene.On, definitions, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, index), SourceBattleScene.On, definitions, (_, _) => false).Dispatch);
        }
    }
}
