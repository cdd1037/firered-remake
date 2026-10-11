using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Each actual occupant receives ON_SWITCHIN at most once. Pending Intimidate is
/// settled after all required KO entries, independently of their choice order.</summary>
public sealed record SourceEntryAbilityState(ImmutableArray<OccupantToken?> AppliedEntries)
{
    public const string Version = "firered-source-entry-abilities-v1";
    public string RulesVersion { get; init; } = Version;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public byte PendingIntimidateMask { get; init; }
}

public static class SourceFourEntryAbilities
{
    public static bool HasEntryAbility(string ability) => ability is "ABILITY_DRIZZLE" or "ABILITY_DROUGHT" or
        "ABILITY_SAND_STREAM" or "ABILITY_INTIMIDATE" or "ABILITY_TRACE" or "ABILITY_FORECAST";

    internal static string? ValidateState(SourceBattleFrame frame)
    {
        if (frame.EntryAbilities is not { } entry) return null;
        if (frame.Layout != SourceBattleLayout.PresetDoubles || frame.SourceMechanicsRulesVersion != SourceBattleKernel.Version ||
            entry.RulesVersion != SourceEntryAbilityState.Version || entry.AppliedEntries.IsDefault || entry.AppliedEntries.Length != 4 ||
            (entry.PendingIntimidateMask & ~15) != 0) return "source_entry_lifecycle_shape";
        for (int i = 0; i < 4; i++)
        {
            if (entry.AppliedEntries[i] is { } token && (token.Slot.Value != i || token.Member.Side != token.Slot.Side ||
                token.Member.Index >= frame.Party(token.Member.Side).Members.Length ||
                token.EntrySerial < 1 || token.EntrySerial > frame.Slots[i].EntrySerial)) return "source_entry_lifecycle_identity";
            if ((entry.PendingIntimidateMask & (1 << i)) != 0 &&
                (entry.AppliedEntries[i] != frame.Slots[i].Occupant || WildBattleEffective.Ability(frame.Slots[i].Body!) != "ABILITY_INTIMIDATE"))
                return "source_entry_intimidate_pending";
        }
        return null;
    }

    internal static bool OverlaySupported(WildBattleCombatant body)
    {
        if (body.CalledOverlay is not { } overlay) return true;
        if (overlay.RulesVersion != WildBattleRules.CalledMoveKernelRulesVersion) return false;
        bool trace = body.Pokemon.AbilityId == "ABILITY_TRACE";
        if (overlay.Ability is not null && !trace) return false;
        if (overlay.Type1 is null && overlay.Type2 is null && overlay.CastformForm is null) return trace;
        // Conversion and Color Change set both live slots; Trace may retain its copied ability.
        // A legal type overlay does not admit Transform, copied moves, or other callers.
        if (overlay.CastformForm is null)
            return overlay.Type1 == overlay.Type2 && WildBattleEffective.IsSourceType(overlay.Type1);
        if (body.Pokemon.Species.Value != "pret:SPECIES_CASTFORM" || body.Pokemon.AbilityId != "ABILITY_FORECAST" ||
            overlay.Ability is not null || overlay.Type1 != overlay.Type2 || overlay.CastformForm is not { } form) return false;
        return overlay.Type1 == (form switch { SourceCastformForm.Normal => "TYPE_NORMAL", SourceCastformForm.Sunny => "TYPE_FIRE",
            SourceCastformForm.Rainy => "TYPE_WATER", SourceCastformForm.Snowy => "TYPE_ICE", _ => null });
    }

    /// <summary>Actual four-position pre-turn entry, using the source sort and its exact tie RNG.
    /// Legacy raw-frame action fixtures remain usable; authentic entry holders require this boundary.</summary>
    public static SourceFourActionResult InitializeEntry(PokemonCatalog catalog, SourceBattleFrame frame, long expectedRevision)
    {
        if (SourceBattleIdentityRules.Validate(frame) is { } invalid) return Fail(frame, invalid);
        if (frame.Revision != expectedRevision || frame.EntryAbilities is not null || frame.Turn != 0 || frame.FourTurn is not null ||
            !frame.Queue.IsEmpty || frame.NextAction != 0 || !frame.PendingSwitches.IsEmpty || !frame.Reservations.IsEmpty ||
            frame.Sides.Any(s => s.Spikes != 0) || frame.DeferredKoVacancyMask != 0 || frame.Outcome != WildBattleOutcome.Ongoing || frame.Slots.Any(c => c.EntrySerial != 1))
            return Fail(frame, "source_entry_initial_phase");
        if (frame.Layout != SourceBattleLayout.PresetDoubles || frame.Slots.Any(c => c.Body is null)) return Fail(frame, "source_entry_initial_layout");
        if (frame.Slots.Any(c => c.Body!.CalledOverlay is not null || c.Body.SourceTracePending)) return Fail(frame, "source_entry_initial_overlay");
        try
        {
            var next = frame with { EntryAbilities = new(frame.Slots.Select(c => c.Occupant).ToImmutableArray()) };
            if (SourceFourActionTurns.ValidateCapability(catalog, next) is { } capability) return Fail(frame, capability);
            // Sort requires a source-position queue; this temporary queue is never published and
            // represents no move choice, PP owner or action token.
            var orderInput = next with { Queue = next.Slots.Select(c => new SourceQueuedAction(c.Slot, null, SourceActionKind.Cancelled)).ToImmutableArray() };
            var order = SourceBattleTurnOrder.Resolve(catalog, orderInput, WildBattleOrderPhase.Entry);
            if (order.Fault is { } sortError) return Fail(frame, sortError);
            next = next with { Rng = order.Rng };
            var ordered = SourceBattleEventHooks.Publish(next, (n, t) => new SourceOrderEvent(n, t, WildBattleOrderPhase.Entry, order.Order));
            var execution = new FrameEntryContext(ordered.State);
            foreach (var slot in order.Order) execution.SwitchIn(slot);
            execution.Settle(forecast: false, items: false); // Initial source pass has no final Forecast query after Trace.
            next = execution.Frame with { Revision = checked(frame.Revision + 1) };
            if (SourceFourActionTurns.ValidateCapability(catalog, next) is { } error) return Fail(frame, error);
            return new(next, [ordered.Event, .. execution.Events], [.. order.Trace, .. execution.Trace], null);
        }
        catch (SourceRecipeUnsupported e) { return Fail(frame, e.Message); }
        catch (OverflowException) { return Fail(frame, "source_entry_counter_exhausted"); }
        catch (ArgumentException) { return Fail(frame, "source_entry_invalid_transition"); }
    }

    // M06 is the sole replacement owner. It calls this after binding the actual reserve,
    // before committing any callbacks, events or RNG from the replacement transaction.
    internal static SourceFourActionResult OnReplacement(PokemonCatalog catalog, SourceBattleFrame frame, BattlerSlot slot)
    {
        var prior = frame.EntryAbilities ?? new(frame.Slots.Select(c => c.Slot == slot ? (OccupantToken?)null : c.Occupant).ToImmutableArray());
        if (prior.AppliedEntries[slot.Value] == frame.Get(slot).Occupant) return Fail(frame, "source_entry_already_applied");
        var next = frame with { EntryAbilities = prior with { AppliedEntries = prior.AppliedEntries.SetItem(slot.Value, frame.Get(slot).Occupant),
            PendingIntimidateMask = (byte)(prior.PendingIntimidateMask & ~slot.Mask) } };
        var execution = new FrameEntryContext(next);
        execution.Spikes(catalog, slot);
        execution.ReplacementAbilities(slot);
        return new(execution.Frame, execution.Events.ToImmutable(), execution.Trace.ToImmutable(), null);
    }

    internal static SourceFourActionResult Settle(SourceBattleFrame frame)
    {
        var execution = new FrameEntryContext(frame); execution.Settle();
        return new(execution.Frame, execution.Events.ToImmutable(), execution.Trace.ToImmutable(), null);
    }

    private static SourceFourActionResult Fail(SourceBattleFrame frame, string error) => new(frame, [], [], error);
}
