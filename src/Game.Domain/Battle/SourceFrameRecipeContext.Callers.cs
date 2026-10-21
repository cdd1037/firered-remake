using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal static bool HasCallerRecipe(string effect) => effect is "EFFECT_METRONOME" or
        "EFFECT_SLEEP_TALK" or "EFFECT_MIRROR_MOVE" or "EFFECT_ASSIST" or "EFFECT_NATURE_POWER";
}

internal sealed partial class FrameRecipeContext
{
    private BattlerSlot? calledMoveTarget;
    private BattlerSlot RetainedMoveTarget => calledMoveTarget ??
        Frame.Queue.FirstOrDefault(a => a.Token?.Owner == action.Actor)?.Target ?? action.Target;

    private void PrintFrameAttackString()
    {
        if ((invocation.Flags & (WildBattleInvocationFlags.AttackStringPrinted | WildBattleInvocationFlags.NoAttackString)) != 0) return;
        Add(n => new SourceAttackStringEvent(n, Turn, invocation.Actor, invocation.ChosenMove, invocation.CurrentMove));
        invocation = invocation with { Flags = invocation.Flags | WildBattleInvocationFlags.AttackStringPrinted };
    }

    private int MirrorSourceId(MoveId? id)
    {
        if (id is null || id.Value.Value == "pret:MOVE_NONE") return 0;
        if (id.Value.Value == "pret:MOVE_UNAVAILABLE") return 65535;
        if (!catalog.Moves.TryGetValue(id.Value, out var definition) || !SourceMoveDefinitionRules.IsValid(definition) ||
            !definition.Flags.Contains("FLAG_MIRROR_MOVE_AFFECTED"))
            throw new SourceRecipeUnsupported("source_kernel_mirror_history");
        return definition.SourceMoveId;
    }

    private PokemonMoveDefinition CalledDefinition(int id)
    {
        var definitions = catalog.Moves.Values.Where(m => m.SourceMoveId == id && SourceMoveDefinitionRules.IsValid(m)).ToArray();
        if (definitions.Length != 1) throw new SourceRecipeUnsupported("source_kernel_called_definition_" + id);
        // The source-selected result is final. Reject the immutable action if its recipe has
        // not migrated; never filter the source pool or draw another result.
        if (!SourceBattleKernel.SupportsRecipe(definitions[0]))
            throw new SourceRecipeUnsupported("source_kernel_called_recipe_not_migrated_" + definitions[0].Id.Value);
        return definitions[0];
    }

    private void ExecuteFrameCaller(string purpose)
    {
        var caller = move;
        if (caller.Effect == "EFFECT_SLEEP_TALK") { ExecuteFrameSleepTalk(purpose); return; }
        PrintFrameAttackString();
        WildBattleSourceCallSelection selection;
        switch (caller.Effect)
        {
            case "EFFECT_METRONOME": selection = WildBattleSourceCallers.Metronome(Frame.Rng); break;
            case "EFFECT_NATURE_POWER":
                selection = new(WildBattleSourceCallers.NaturePower(Frame.Field.Terrain ??
                    throw new SourceRecipeUnsupported("source_kernel_nature_power_terrain")), Frame.Rng, [], []); break;
            case "EFFECT_MIRROR_MOVE":
                var history = Frame.Get(invocation.Actor).Registers.History;
                selection = WildBattleSourceCallers.MirrorMove(invocation.Actor.Value, MirrorSourceId(history?.LastTakenMove),
                    Enumerable.Range(0, Frame.BattlerCount).Select(i => MirrorSourceId(history?.LastTakenFrom[i])).ToImmutableArray(), Frame.Rng); break;
            case "EFFECT_ASSIST":
                var actor = Frame.Get(invocation.Actor);
                var members = Frame.Party(invocation.Actor.Side).Members.Select(m => new WildBattleAssistMember(m.IsEgg,
                    m.Pokemon.Moves.Select(learned => catalog.Moves[learned.Move].SourceMoveId).ToImmutableArray())).ToImmutableArray();
                selection = WildBattleSourceCallers.Assist(members, actor.Member!.Value.Index, Frame.Rng); break;
            default: throw new SourceRecipeUnsupported("source_kernel_caller_effect");
        }
        Frame = Frame with { Rng = selection.Rng }; Trace.AddRange(selection.Trace);
        var current = selection.SourceMoveId is { } id ? CalledDefinition(id) : null;
        Add(n => new SourceCallerSelectionEvent(n, Turn, invocation.Actor, caller.Id, current?.Id, selection.Candidates, Frame.Rng.Cursor));
        if (current is null)
        {
            if (caller.Effect == "EFFECT_MIRROR_MOVE") invocation = invocation with {
                Flags = invocation.Flags | WildBattleInvocationFlags.PpNotAffectedByPressure };
            SpendFramePp(invocation.Actor, invocation.Target, caller, true);
            Add(n => new SourceMoveFailedEvent(n, Turn, invocation.Actor, invocation.Target, caller.Id,
                caller.Effect == "EFFECT_MIRROR_MOVE" ? "mirror-move-no-history" : "assist-no-candidates"));
            return;
        }
        EnterCalledMove(current, caller.Effect == "EFFECT_ASSIST");
    }

    private void ExecuteFrameSleepTalk(string purpose)
    {
        var caller = move;
        SpendFramePp(invocation.Actor, invocation.Target, caller, true);
        if (!WildBattleStatus.IsSleeping(Get(invocation.Actor).Status))
        {
            Add(n => new SourceMoveFailedEvent(n, Turn, invocation.Actor, invocation.Target, caller.Id, "user-awake")); return;
        }
        invocation = invocation with { Flags = invocation.Flags | WildBattleInvocationFlags.NoPpDeduct };
        var actor = Get(invocation.Actor);
        bool Candidate(int index)
        {
            if (!WildBattleRules.SnorlaxSleepTalkCandidate(catalog, actor, null, index)) return false;
            var id = actor.Pokemon.Moves[index].Move;
            return !SourceMoveRestrictionRules.Imprisoned(id, SourceMoveRestrictionRules.Opponents(Frame, invocation.Actor)) &&
                SourceBattleHeldItems.ChoiceAllows(actor, id) &&
                !(actor.CalledVolatile is { Tormented: true } && actor.LastUsedMove == id && id != WildBattleRules.StruggleMove) &&
                !(actor.CalledVolatile is { TauntTurnsRemaining: > 0 } && catalog.Moves[id].Power == 0);
        }
        var slots = Enumerable.Range(0, 4).Where(Candidate).ToArray();
        if (slots.Length == 0)
        {
            Add(n => new SourceMoveFailedEvent(n, Turn, invocation.Actor, invocation.Target, caller.Id, "no-callable-move")); return;
        }
        int selected;
        do { selected = Draw(purpose + ".sleep-talk-slot", 4) & 3; } while (!Candidate(selected));
        var current = CalledDefinition(catalog.Moves[actor.Pokemon.Moves[selected].Move].SourceMoveId);
        Add(n => new SourceSleepTalkEvent(n, Turn, invocation.Actor, selected, current.Id));
        Add(n => new SourceCallerSelectionEvent(n, Turn, invocation.Actor, caller.Id, current.Id,
            slots.Select(i => catalog.Moves[actor.Pokemon.Moves[i].Move].SourceMoveId).ToImmutableArray(), Frame.Rng.Cursor));
        invocation = invocation with { PpSlot = selected };
        EnterCalledMove(current, true);
    }

    private void EnterCalledMove(PokemonMoveDefinition current, bool writesCalled, bool rawTarget = false)
    {
        invocation = invocation with { CurrentMove = current.Id, CalledMove = writesCalled ? current.Id : invocation.CalledMove,
            Flags = invocation.Flags & ~WildBattleInvocationFlags.AttackStringPrinted };
        var selected = SourceMoveTargeting.ResolveCalled(Frame, invocation.Actor, current,
            rawTarget ? null : SourceBattleKernel.SelectedTargetOverride(catalog, Frame, invocation.Actor, current));
        if (selected.Fault is { } fault) throw new SourceRecipeUnsupported(fault);
        Frame = Frame with { Rng = selected.Rng }; Trace.AddRange(selected.Trace);
        invocation = invocation with { Target = selected.Target ?? invocation.Actor.Opposite };
        currentLightningRodRedirected = selected.LightningRodRedirected;
        // GetMoveTarget also writes moveTarget[attacker]. Multi-turn continuations retain
        // this exact called target even when it differs from the menu selection.
        if (selected.Target is { } retained)
        {
            calledMoveTarget = retained;
            Frame = Frame with { Queue = Frame.Queue.Select(queued => queued.Token?.Owner == action.Actor
                ? queued with { Target = retained } : queued).ToImmutableArray() };
        }
        calledMoveExecuted = true;
        move = current;
        Add(n => new SourceCalledMoveTransitionEvent(n, Turn, invocation));
        if (selected.Target is null && current.Effect != "EFFECT_SPIKES")
        {
            BeginScriptTarget(invocation.Target);
            SpendFramePp(invocation.Actor, invocation.Target, current, true);
            Add(n => new SourceMoveFailedEvent(n, Turn, invocation.Actor, invocation.Target, current.Id, "deferred-ko-no-target"));
            commandNoEffect = true; CompleteScriptTarget(); return;
        }
        var calledAction = action with { Target = invocation.Target, LightningRodRedirected = currentLightningRodRedirected };
        if (SourceBattleKernel.ValidateCalledAction(catalog, Frame, calledAction, current) is { } error)
            throw new SourceRecipeUnsupported(error);
        // Only the script attackcanceler's late checks re-enter. Sleep, confusion, Truant,
        // and other action conditions belong to the original chosen action exactly once.
        RunCurrentMove(true);
    }
}
