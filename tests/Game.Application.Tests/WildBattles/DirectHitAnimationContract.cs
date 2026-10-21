using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

// Shared mechanics fixture only: expected move IDs, PP and hit ownership are authored
// by each test family, never inferred from production move metadata.
internal static class DirectHitAnimationFixture
{
    public static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    public static MoveId Id(string name) => new("pret:MOVE_" + name);
    public static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events) =>
        events.Select((e, i) => e with { Sequence = i + 1 }).ToImmutableArray();
    public static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).ToArray();
    public static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var move = Id(name); var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learnset required: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    public static SourceBattleFrame Frame(WildBattleCombatant[] bodies, uint seed)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(31),
            Parties = [new(BattleSide.Player, [Member(bodies[0]), Member(bodies[2])]), new(BattleSide.Opponent, [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(seed) };
    }
    public static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }
    public static SourceFourActionResult Ordered(SourceBattleFrame frame, int actor, int target)
    {
        var selected = frame.Slots[actor].Body!.Pokemon.Moves[0].Move;
        var choices = frame.Slots.Select(c => {
            var move = Mechanics.Catalog.Moves[c.Body!.Pokemon.Moves[0].Move];
            int recipient = c.Slot.Value == actor ? target : move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : c.Slot.Value ^ 1;
            return new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, new(recipient));
        }).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        for (int step = 0; step < 12; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            if (result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == selected)) return result;
            Assert.IsNotNull(result.Continuation, "The selected natural move must execute within this public turn.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new InvalidOperationException("The selected move did not execute in one bounded public turn.");
    }
}

internal enum DirectHitOwnership { AttackText, EachHit }

internal sealed record DirectHitAnimationContract(
    string BattleId, int SourceMoveId, MoveId Move, int Pp,
    DirectHitOwnership Ownership, int MinimumPlannedHits = 1, int MaximumPlannedHits = 1)
{
    public CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index = 0) =>
        new(BattleId, new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    public CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index = 0) =>
        new(BattleId, null!, new(), events, index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []);
    public CampaignMoveAnimationDecision AtText(ImmutableArray<SourceBattleEvent> events, int index) =>
        AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On);
    public CampaignMoveAnimationDecision AtHit(ImmutableArray<SourceBattleEvent> events, int index) =>
        AnimationDispatchFixture.AtResult(Double(events, index), SourceBattleScene.On);

    public ImmutableArray<SourceBattleEvent> Group(int actor = 0, int? target = null, int planned = 2, int? actual = null)
    {
        var a = new BattlerSlot(actor); var t = new BattlerSlot(target ?? (actor ^ 1));
        int count = Ownership == DirectHitOwnership.AttackText ? 1 : actual ?? planned;
        List<SourceBattleEvent> events = [
            new SourceAttackStringEvent(0, 7, a, Move, Move),
            new SourceMoveUsedEvent(0, 7, a, t, 0, Move, Pp, Pp - 1),
            new SourcePpCommandEvent(0, 7, a, t, 0, Move, Move, Pp, Pp - 1, 1, 0) { PpOwner = a }];
        for (int i = 0; i < count; i++) events.Add(new SourceHpChangedEvent(0, 7, a, t, Move, 100 - i * 5, 95 - i * 5, 5, false, 100));
        if (Ownership == DirectHitOwnership.EachHit) events.Add(new SourceMultiHitEvent(0, 7, a, t, Move, planned, count));
        return DirectHitAnimationFixture.Number(events);
    }

    public void AssertDispatches(ImmutableArray<SourceBattleEvent> events, int actor = 0, int target = 1)
    {
        var hits = DirectHitAnimationFixture.Hits(events); Assert.IsTrue(hits.Length > 0);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        bool atText = Ownership == DirectHitOwnership.AttackText;
        if (atText) { Assert.AreEqual(1, hits.Length); Assert.AreEqual(0, events.OfType<SourceMultiHitEvent>().Count()); }
        else
        {
            var summary = events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(Move, summary.Move);
            Assert.AreEqual(hits.Length, summary.ActualHits);
            Assert.IsTrue(summary.ActualHits >= 1 && summary.ActualHits <= summary.PlannedHits);
            Assert.IsTrue(summary.PlannedHits >= MinimumPlannedHits && summary.PlannedHits <= MaximumPlannedHits);
        }
        string before = JsonSerializer.Serialize(events); int count = 0, acknowledgements = 0;
        for (int index = 0; index < events.Length; index++)
        {
            var battle = Double(events, index);
            var text = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On);
            var hit = AnimationDispatchFixture.AtResult(battle, SourceBattleScene.On);
            int ordinal = Array.FindIndex(hits, h => h.Sequence == events[index].Sequence);
            bool eligible = atText ? events[index] == used : ordinal >= 0;
            var decision = atText ? text : hit;
            Assert.IsNull((atText ? hit : text).Dispatch, "Only one receipt ownership model is allowed.");
            if (eligible)
            {
                var dispatch = decision.Dispatch; Assert.IsNotNull(dispatch, decision.Reason); count++;
                var receipt = atText ? hits[0] : hits[ordinal];
                Assert.AreEqual(SourceMoveId, dispatch.SourceMoveId); Assert.AreEqual(BattleId, dispatch.BattleId);
                Assert.AreEqual(actor, dispatch.Actor.Value); Assert.AreEqual(target, dispatch.Target.Value);
                Assert.AreEqual(atText ? used.Sequence : receipt.Sequence, dispatch.Sequence);
                Assert.AreEqual(receipt.Sequence, dispatch.ResultSequence);
                Assert.AreEqual(attack.Sequence, dispatch.AttackSequence); Assert.AreEqual(pp.Sequence, dispatch.PpSequence);
                Assert.AreEqual(receipt is SourceSubstituteDamagedEvent, dispatch.HitsSubstitute);
                Assert.AreEqual(atText, dispatch.AcknowledgesCurrentReceipt);
                if (dispatch.AcknowledgesCurrentReceipt) acknowledgements++;
                if (atText) Assert.IsNull(dispatch.MoveTurn); else Assert.AreEqual((byte)ordinal, dispatch.MoveTurn);
            }
            else Assert.IsNull(decision.Dispatch, "Secondary, summary and faint receipts cannot replay the move root.");
            Assert.AreEqual(decision, atText
                ? AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On)
                : AnimationDispatchFixture.AtResult(battle, SourceBattleScene.On));
            Assert.AreEqual(index, battle.EventIndex, "Projection cannot consume the receipt cursor.");
            if (actor < 2 && target < 2)
            {
                var single = Single(events, index);
                Assert.AreEqual(text.Dispatch, AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
                Assert.AreEqual(hit.Dispatch, AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.On).Dispatch);
                Assert.AreEqual(index, single.EventIndex);
            }
        }
        Assert.AreEqual(atText ? 1 : hits.Length, count);
        Assert.AreEqual(atText ? 1 : 0, acknowledgements, "Per-hit visuals never create an additional authoritative ACK.");
        Assert.AreEqual(before, JsonSerializer.Serialize(events), "Presentation may not mutate committed receipts.");
    }

    public void AssertDenied(ImmutableArray<SourceBattleEvent> events, string branch, bool singles)
    {
        string before = JsonSerializer.Serialize(events);
        for (int index = 0; index < events.Length; index++)
        {
            Assert.AreEqual(AtText(events, index), AtText(events, index), "Denied projection is repeatable.");
            Assert.AreEqual(AtHit(events, index), AtHit(events, index), "Denied result projection is repeatable.");
            Assert.IsNull(AtText(events, index).Dispatch, branch + " attack-text " + index);
            Assert.IsNull(AtHit(events, index).Dispatch, branch + " hit-result " + index);
            if (!singles) continue;
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On).Dispatch, branch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(Single(events, index), true, SourceBattleScene.On).Dispatch, branch);
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(events));
    }
}
