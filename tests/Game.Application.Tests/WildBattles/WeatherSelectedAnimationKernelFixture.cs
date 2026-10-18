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

// Source-natural learners and authentic committed actions. Controlled stages/HP
// and bounded branch search are integration fixtures, not route-play evidence.
internal static class WeatherSelectedAnimationKernelFixture
{
    internal static SourceMechanicsCatalog Mechanics => Previous.Mechanics;
    internal static (string Name, string Species, int Level, string Effect, string? Weather) Spec(int id) => id switch {
        201 => ("SANDSTORM", "ONIX", 50, "EFFECT_SANDSTORM", "sandstorm"),
        240 => ("RAIN_DANCE", "SQUIRTLE", 50, "EFFECT_RAIN_DANCE", "rain"),
        241 => ("SUNNY_DAY", "MAGMAR", 50, "EFFECT_SUNNY_DAY", "sun"),
        258 => ("HAIL", "SPHEAL", 50, "EFFECT_HAIL", "hail"),
        257 => ("HEAT_WAVE", "CHARIZARD", 50, "EFFECT_BURN_HIT", null),
        345 => ("MAGICAL_LEAF", "ROSELIA", 50, "EFFECT_ALWAYS_HIT", null),
        327 => ("SKY_UPPERCUT", "HITMONCHAN", 50, "EFFECT_SKY_UPPERCUT", null),
        237 => ("HIDDEN_POWER", "UNOWN", 50, "EFFECT_HIDDEN_POWER", null),
        310 => ("ASTONISH", "ZUBAT", 50, "EFFECT_FLINCH_MINIMIZE_HIT", null),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    internal static MoveId Id(string name) => Previous.Id(name);
    internal static MoveId Move(int id) => Id(Spec(id).Name);
    internal static WildBattleCombatant Mon(string species, string name, int level = 50, int iv = 31, uint personality = 0)
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(iv), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
        var move = Id(name);
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learner required: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    internal static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        // Normal-type Snorlax is immune to Astonish; use a natural nonimmune
        // high-HP target for its success and flinch branches.
        var bodies = Enumerable.Range(0, 4).Select(_ => id == 310 ? Mon("WAILORD", "SPLASH", 100)
            : Previous.Mon("SNORLAX", "DEFENSE_CURL", 100)).ToArray();
        var spec = Spec(id);
        bodies[actor] = Mon(spec.Species, spec.Name, spec.Level) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    internal static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single = false) => Previous.Frame(bodies, single);
    internal static SourceKernelResult Direct(SourceBattleFrame frame, int actor = 0, int target = 1) => Previous.Resolve(frame, actor, target);
    internal static SourceFourActionResult Ordered(SourceBattleFrame frame, int actor = 0, int target = 1, bool forcedStruggle = false)
    {
        string before = JsonSerializer.Serialize(frame);
        var choices = frame.Slots.Take(frame.BattlerCount).Select(s => {
            var move = Mechanics.Catalog.Moves[s.Body!.Pokemon.Moves[0].Move];
            int recipient = s.Slot.Value == actor ? target : move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"
                ? s.Slot.Value : s.Slot.Value ^ 1;
            return forcedStruggle&&s.Slot.Value==actor?SourceActionSelection.Struggle(s.Occupant!.Value,new(recipient))
                :new SourceActionSelection(s.Occupant!.Value, SourceActionKind.Move, 0, new(recipient));
        }).ToImmutableArray();
        var start = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        Assert.IsTrue(start.Succeeded, start.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(start.State));
        Assert.AreEqual(actor, start.State.Queue[0].Slot.Value, "Controlled speed must put the tested actor first.");
        Assert.IsNotNull(start.Continuation);
        var result = SourceFourActionTurns.Advance(Mechanics.Catalog, start.State, start.Continuation.Value);
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.AreEqual(actor, result.Events.OfType<SourceMoveUsedEvent>().Single().Actor.Value);
        Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        return result;
    }
    internal static SourceFourActionResult Find(SourceBattleFrame frame, int actor, int target,
        Func<SourceFourActionResult, bool> matches, string branch)
    {
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Ordered(frame with { Rng = new(seed) }, actor, target);
            if (!matches(result)) continue;
            Console.WriteLine($"{branch}: observed public-turn-start seed={seed}");
            return result;
        }
        throw new InvalidOperationException($"No actual {branch} in 1024 public-turn-start seeds.");
    }
    internal static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) => Previous.Hits(events);
    internal static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events, int index, bool single,
        SourceBattleScene scene = SourceBattleScene.On) => single
        ? AnimationDispatchFixture.AtAttackText(Previous.Single(events, index), true, scene)
        : AnimationDispatchFixture.AtAttackText(Previous.Double(events, index), scene);
    internal static void Check(SourceKernelResult result, int id, bool single, SourceBattleEvent? owner) =>
        Check(result.Events, result, id, single, owner);
    internal static void Check(SourceFourActionResult result, int id, bool single, SourceBattleEvent? owner) =>
        Check(result.Events, result, id, single, owner);
    private static void Check(ImmutableArray<SourceBattleEvent> events, object result, int id, bool single, SourceBattleEvent? owner)
    {
        string before = JsonSerializer.Serialize(result);
        var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
        var commands = events.OfType<SourcePpCommandEvent>().ToArray(); Assert.IsTrue(commands.Length > 0);
        Assert.AreEqual(Move(id), used.Move); Assert.AreEqual(used.Move, attack.CurrentMove); Assert.AreEqual(used.Move, attack.ChosenMove);
        Assert.AreEqual(used.Actor, attack.Actor); Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < commands[0].Sequence);
        Assert.AreEqual(used.PpBefore, commands[0].Before); Assert.AreEqual(used.PpAfter, commands[0].After);
        Assert.AreEqual(Math.Max(0, commands[0].Before - commands[0].CommandCost), commands[0].After);
        foreach (var pp in commands)
        {
            Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.PpOwner);
            Assert.AreEqual(used.Move, pp.ChosenMove); Assert.AreEqual(used.Move, pp.CurrentMove); Assert.AreEqual(used.Slot, pp.PpSlot);
        }
        int target = owner switch { SourceHpChangedEvent h => h.Target.Value, SourceSubstituteDamagedEvent s => s.Target.Value,
            SourceMtMoonVolatileEvent w => w.Side.Value, _ => used.Target.Value };
        var ownedPp = commands.Single(p => p.Target.Value == target);
        int count = 0;
        for (int i = 0; i < events.Length; i++)
        {
            var decision = Project(events, i, single); Assert.AreEqual(decision, Project(events, i, single));
            Assert.IsNull(Project(events, i, single, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.AtResult(Previous.Single(events, i), true, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtResult(Previous.Double(events, i), SourceBattleScene.On).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.StatAtResult(Previous.Single(events, i), true)
                : AnimationDispatchFixture.StatAtResult(Previous.Double(events, i)), "No new root prescribes generic stat-change waves.");
            if (single) Assert.IsNull(AnimationDispatchFixture.AtAttackText(Previous.Single(events, i), false, SourceBattleScene.On).Dispatch);
            if (owner is null || events[i] != used) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); count++;
            Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(target, cue.Target.Value);
            Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(ownedPp.Sequence, cue.PpSequence); Assert.AreEqual(owner.Sequence, cue.ResultSequence);
            Assert.AreEqual(owner is SourceSubstituteDamagedEvent, cue.HitsSubstitute); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
            Assert.IsNull(cue.MoveTurn); Assert.IsNull(cue.MoveDamage); Assert.IsNull(cue.MovePower);
        }
        Assert.AreEqual(owner is null ? 0 : 1, count); Assert.AreEqual(before, JsonSerializer.Serialize(result));
    }
    internal static void Deny(ImmutableArray<SourceBattleEvent> events, bool single) => Previous.Deny(events, single, "Unowned weather/hit invocation");
    internal static SourceMtMoonVolatileEvent WeatherMarker(ImmutableArray<SourceBattleEvent> events, int id) =>
        events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == Spec(id).Weather);
    internal static int Timer(SourceFieldRegisters field, string weather) => weather switch {
        "rain" => field.RainTurnsRemaining, "sandstorm" => field.SandstormTurnsRemaining,
        "sun" => field.SunTurnsRemaining, "hail" => field.HailTurnsRemaining, _ => throw new ArgumentOutOfRangeException(nameof(weather)) };
    internal static SourceFieldRegisters Temporary(SourceFieldRegisters field, string weather, int turns) => field with {
        PermanentWeather = null, RainTurnsRemaining = weather == "rain" ? turns : 0, SandstormTurnsRemaining = weather == "sandstorm" ? turns : 0,
        SunTurnsRemaining = weather == "sun" ? turns : 0, HailTurnsRemaining = weather == "hail" ? turns : 0 };
}
