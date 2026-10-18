using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Previous = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Reuses the existing public-kernel frame, projection and denial helpers. Hydro
// Cannon is learned through the actual Cape Brink tutor, not a fabricated learnset.
internal static class FieldRootAnimationKernelFixture
{
    internal static SourceMechanicsCatalog Mechanics => Previous.Mechanics;
    internal static (string Name, string Species, string Effect) Spec(int id) => id switch {
        219 => ("SAFEGUARD", "BUTTERFREE", "EFFECT_SAFEGUARD"),
        275 => ("INGRAIN", "TANGELA", "EFFECT_INGRAIN"),
        139 => ("POISON_GAS", "GRIMER", "EFFECT_POISON"),
        308 => ("HYDRO_CANNON", "BLASTOISE", "EFFECT_RECHARGE"),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    internal static MoveId Move(int id) => Previous.Id(Spec(id).Name);
    private static MoveTutorCatalog Tutors => TestAssets.Cached(() =>
        PretMoveTutors.Load(TestAssets.Root, Mechanics.Catalog).Catalog.WithCapeBrink(CapeBrinkTutorCatalog.Authenticate(Mechanics.Catalog)));
    private static WildBattleCombatant HydroCannonLearner()
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_BLASTOISE"), 50, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var party = new PartyPokemon(1, pokemon with { Friendship = 255 }, "BLASTOISE", "TEST", 0, 2,
            "MAP_PALLET_TOWN", 50, 4, "ITEM_POKE_BALL", "ITEM_NONE");
        var offer = CapeBrinkTutorRules.Offer(Tutors.CapeBrink!, [party], ImmutableHashSet<string>.Empty);
        Assert.IsTrue(offer.Allowed, offer.Error); Assert.AreEqual(Move(308), offer.Tutor!.Move);
        var taught = MoveTutorRules.Teach(Tutors, Mechanics.Catalog, party, offer.Tutor.Index,
            pokemon.Moves.Length == 4 ? 0 : null);
        Assert.IsTrue(taught.Succeeded, taught.Error); pokemon = taught.Pokemon.Pokemon;
        Assert.AreEqual(Move(308), pokemon.Moves[taught.LearnedSlot].Move);
        int pp = Mechanics.Catalog.Moves[Move(308)].Pp;
        return new(pokemon with { Moves = [new(Move(308), pp, pp)] });
    }
    internal static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        // Wailord has no poison immunity or water absorption and is a genuine
        // natural Splash learner. Its retained HP bounds the ordinary-hit cases.
        var bodies = Enumerable.Range(0, 4).Select(_ => Previous.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        var spec = Spec(id);
        bodies[actor] = (id == 308 ? HydroCannonLearner() : Previous.Mon(spec.Species, spec.Name)) with {
            AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    internal static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events, int index, bool single,
        SourceBattleScene scene = SourceBattleScene.On) => WeatherSelectedAnimationKernelFixture.Project(events, index, single, scene);
    internal static void Deny(ImmutableArray<SourceBattleEvent> events, bool single) => Previous.Deny(events, single, "Unowned field-root invocation");
    internal static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events) =>
        events.Select((e, i) => e with { Sequence = i + 1 }).ToImmutableArray();
    internal static void Check(SourceKernelResult result, int id, bool single, SourceBattleEvent? owner)
    {
        string before = JsonSerializer.Serialize(result); var events = result.Events;
        var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Move(id), used.Move); Assert.AreEqual(used.Move, attack.ChosenMove); Assert.AreEqual(used.Move, attack.CurrentMove);
        Assert.AreEqual(used.Actor, attack.Actor); Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.PpOwner);
        Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Slot, pp.PpSlot);
        Assert.AreEqual(used.Move, pp.ChosenMove); Assert.AreEqual(used.Move, pp.CurrentMove);
        Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After);
        Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence);
        int count = 0;
        for (int i = 0; i < events.Length; i++)
        {
            var decision = Project(events, i, single); Assert.AreEqual(decision, Project(events, i, single));
            Assert.IsNull(Project(events, i, single, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.AtResult(Previous.Single(events, i), true, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtResult(Previous.Double(events, i), SourceBattleScene.On).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.StatAtResult(Previous.Single(events, i), true)
                : AnimationDispatchFixture.StatAtResult(Previous.Double(events, i)));
            if (single) Assert.IsNull(AnimationDispatchFixture.AtAttackText(Previous.Single(events, i), false, SourceBattleScene.On).Dispatch);
            if (owner is null || events[i] != used) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); count++;
            Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target);
            Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(owner.Sequence, cue.ResultSequence);
            Assert.AreEqual(owner is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
            Assert.IsNull(cue.MoveTurn); Assert.IsNull(cue.MovePower); Assert.IsNull(cue.MoveDamage);
        }
        Assert.AreEqual(owner is null ? 0 : 1, count); Assert.AreEqual(before, JsonSerializer.Serialize(result));
    }
}
