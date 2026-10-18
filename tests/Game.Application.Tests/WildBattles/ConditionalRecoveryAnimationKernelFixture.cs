using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Controlled integration states use source-learnable moves and committed kernel
// actions. This fixture neither grants runtime support nor claims route replay.
internal static class ConditionalRecoveryAnimationKernelFixture
{
    internal static (string Name, string Species, string Effect) Spec(int id) => id switch {
        161 => ("TRI_ATTACK", "DODRIO", "EFFECT_TRI_ATTACK"),
        263 => ("FACADE", "RATICATE", "EFFECT_FACADE"),
        265 => ("SMELLING_SALT", "MAKUHITA", "EFFECT_SMELLINGSALT"),
        348 => ("LEAF_BLADE", "SCEPTILE", "EFFECT_HIGH_CRITICAL"),
        62 => ("AURORA_BEAM", "SEEL", "EFFECT_ATTACK_DOWN_HIT"),
        156 => ("REST", "SNORLAX", "EFFECT_REST"),
        234 => ("MORNING_SUN", "ESPEON", "EFFECT_MORNING_SUN"),
        236 => ("MOONLIGHT", "UMBREON", "EFFECT_MOONLIGHT"),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    internal static MoveId Move(int id) => Kernel.Id(Spec(id).Name);
    internal static WildBattleCombatant MachineMon(string species, string machine, string name, string natural = "TACKLE")
    {
        var body = Kernel.Mon(species, natural, 100); var machines = SharedMachineCatalog.LoadProject(TestAssets.Root);
        ItemId item = new("pret:ITEM_" + machine); var definition = machines.Definition(item);
        Assert.AreEqual(Kernel.Id(name), definition.Move); Assert.IsTrue(machines.Compatibility(item, body.Pokemon.Species).CanLearn);
        Assert.AreEqual(Kernel.Mechanics.Catalog.Moves[definition.Move].Pp, definition.MovePp);
        // Legal machine compatibility is authenticated; obtaining/teaching it is not replayed.
        return body with { Pokemon = body.Pokemon with { Moves = [new(definition.Move, definition.MovePp, definition.MovePp)] } };
    }
    internal static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("SNORLAX", "DEFENSE_CURL", 100)).ToArray();
        var spec = Spec(id); var body = id == 263 ? MachineMon(spec.Species, "TM42", spec.Name)
            : Kernel.Mon(spec.Species, spec.Name, id is 234 or 236 ? 60 : 50);
        bodies[actor] = body with { AccuracyStage = 12, SpeedStage = 12 };
        var definition = Kernel.Mechanics.Catalog.Moves[Move(id)];
        Assert.AreEqual(id, definition.SourceMoveId); Assert.AreEqual(spec.Effect, definition.Effect);
        return bodies;
    }
    internal static CampaignMoveAnimationDecision Result(ImmutableArray<SourceBattleEvent> events, int index, bool single,
        SourceBattleScene scene = SourceBattleScene.On) => single
        ? AnimationDispatchFixture.AtResult(Envelope.Single(events, index), true, scene)
        : AnimationDispatchFixture.AtResult(Envelope.Double(events, index), scene);
    internal static void Deny(ImmutableArray<SourceBattleEvent> events, bool single)
    {
        for (int i = 0; i < events.Length; i++)
        { Assert.IsNull(Kernel.Project(events, i, single).Dispatch); Assert.IsNull(Result(events, i, single).Dispatch); }
    }
    internal static void Check(ImmutableArray<SourceBattleEvent> events, object committed, int id, bool single, SourceBattleEvent? owner)
    {
        string before = JsonSerializer.Serialize(committed); var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Move(id), used.Move); Assert.AreEqual(used.Move, attack.CurrentMove); Assert.AreEqual(used.Move, attack.ChosenMove);
        Assert.AreEqual(used.Actor, attack.Actor); Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.PpOwner);
        Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Move, pp.CurrentMove); Assert.AreEqual(used.Move, pp.ChosenMove);
        Assert.AreEqual(used.Slot, pp.PpSlot); Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After);
        Assert.IsTrue(pp.CommandCost > 0); Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence);
        int count = 0;
        for (int i = 0; i < events.Length; i++)
        {
            var text = Kernel.Project(events, i, single); var result = Result(events, i, single);
            Assert.AreEqual(text, Kernel.Project(events, i, single)); Assert.AreEqual(result, Result(events, i, single));
            Assert.IsNull(Kernel.Project(events, i, single, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(Result(events, i, single, SourceBattleScene.Off).Dispatch);
            if (single)
            {
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Envelope.Single(events, i), false, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(Envelope.Single(events, i), false, SourceBattleScene.On).Dispatch);
            }
            bool late = id == 156; var decision = late ? result : text;
            Assert.IsNull((late ? text : result).Dispatch);
            if (owner is null || events[i] != (late ? owner : used)) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); count++;
            Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target);
            Assert.AreEqual((late ? owner : used).Sequence, cue.Sequence); Assert.AreEqual(owner.Sequence, cue.ResultSequence);
            Assert.AreEqual(attack.Sequence, cue.AttackSequence); Assert.AreEqual(pp.Sequence, cue.PpSequence);
            Assert.AreEqual(late ? CampaignMoveAnimationBoundary.BeforeRecoveryResult : CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary);
            Assert.AreEqual(owner is SourceSubstituteDamagedEvent, cue.HitsSubstitute); Assert.AreEqual(!late, cue.AcknowledgesCurrentReceipt);
            Assert.IsNull(cue.MoveTurn); Assert.IsNull(cue.MoveDamage); Assert.IsNull(cue.MovePower);
        }
        Assert.AreEqual(owner is null ? 0 : 1, count); Assert.AreEqual(before, JsonSerializer.Serialize(committed));
    }
}
