using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;

namespace Game.Application;

/// <summary>The two source counters and current-map rematch ranks, plus movement ownership
/// needed to retain deterministic inactive-slot reset draws through an exact Continue.</summary>
public sealed record CampaignVsSeekerProgress(VsSeekerState State,
    ImmutableSortedDictionary<int, int> TemplateMovements, ImmutableArray<int> ObjectMovements)
{
    public static CampaignVsSeekerProgress Empty { get; } = new(VsSeekerState.Empty,
        ImmutableSortedDictionary<int, int>.Empty, Enumerable.Repeat(0, 16).ToImmutableArray());
}

public sealed partial class CampaignSession
{
    private static readonly ItemId VsSeekerItem = new("pret:ITEM_VS_SEEKER");
    private const string VsSeekerGotFlag = "FLAG_GOT_VS_SEEKER";
    private const string VsSeekerChargingFlag = "FLAG_SYS_VS_SEEKER_CHARGING";
    public bool VsSeekerEnabled => _content.VsSeeker is not null;
    public int VsSeekerCharge => State.VsSeekerProgress?.State.ChargeSteps ?? 0;
    private static CampaignVsSeekerProgress VsProgress(CampaignState state) => state.VsSeekerProgress ?? CampaignVsSeekerProgress.Empty;
    private static bool OwnsVsSeeker(CampaignState state) => OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], VsSeekerItem) == 1;
    private static HashSet<string> VsFought(CampaignState state) => state.BattledTrainers.Select(t => t[5..]).ToHashSet(StringComparer.Ordinal);

    // Only these source values participate in rematch reset. Other movement types retain
    // their existing actor behavior and need no duplicate copy of the full movement enum.
    private static int VsMovementCode(string source) => source switch
    {
        "MOVEMENT_TYPE_FACE_UP" => 7, "MOVEMENT_TYPE_FACE_DOWN" => 8,
        "MOVEMENT_TYPE_FACE_LEFT" => 9, "MOVEMENT_TYPE_FACE_RIGHT" => 10,
        "MOVEMENT_TYPE_RAISE_HAND_AND_STOP" => 77, "MOVEMENT_TYPE_RAISE_HAND_AND_JUMP" => 78,
        "MOVEMENT_TYPE_RAISE_HAND_AND_SWIM" => 79, _ => 0
    };
    private static string VsMovementSymbol(int source) => source switch
    {
        7 => "MOVEMENT_TYPE_FACE_UP", 8 => "MOVEMENT_TYPE_FACE_DOWN",
        9 => "MOVEMENT_TYPE_FACE_LEFT", 10 => "MOVEMENT_TYPE_FACE_RIGHT",
        77 => "MOVEMENT_TYPE_RAISE_HAND_AND_STOP", 78 => "MOVEMENT_TYPE_RAISE_HAND_AND_JUMP",
        79 => "MOVEMENT_TYPE_RAISE_HAND_AND_SWIM", _ => throw new InvalidDataException("vs_seeker_movement_value")
    };

    private ImmutableArray<VsSeekerTrainer> VsTrainers(CampaignState state)
    {
        var result = ImmutableArray.CreateBuilder<VsSeekerTrainer>();
        var templates = _content.MapDefinition(state.World.Player.MapId).Actors;
        var progress = VsProgress(state);
        for (int i = 0; i < templates.Length; i++)
        {
            var template = templates[i];
            if (template.TrainerType is not ("TRAINER_TYPE_NORMAL" or "TRAINER_TYPE_BURIED")) continue;
            var instruction = _content.FindProgram(template.ScriptId)?.Instructions.FirstOrDefault();
            if (instruction is null || instruction.Op != Game.Domain.Events.StoryOp.TrainerBattle || instruction.Args.IsEmpty)
                throw new InvalidDataException("vs_seeker_source_trainer_script:" + template.ScriptId);
            var actor = state.Actors[template.LocalId];
            var occupied = state.ObjectSlots?.FirstOrDefault(s => s.Value == template.LocalId);
            int slot = occupied is { Key: > 0 } found ? found.Key : 16;
            bool sane = slot < 16 && actor.Active && templates.Length >= actor.SourceLocalId;
            if (!_content.TryConstant(template.GraphicsId, out int graphics))
                throw new InvalidDataException("vs_seeker_source_graphics:" + template.GraphicsId);
            result.Add(new(i, template.SourceLocalId, instruction.Args[0], slot, actor.X, actor.Y, sane, graphics,
                progress.TemplateMovements.GetValueOrDefault(template.SourceLocalId, VsMovementCode(template.MovementType)),
                SourceDirectionValue(actor.Facing), slot < 16));
        }
        return result.ToImmutable();
    }

    private static CampaignState CaptureVsSeekerObjectMovements(CampaignState state)
    {
        if (state.VsSeekerProgress is not { } progress || state.ObjectSlots is not { } slots) return state;
        var moves = progress.ObjectMovements;
        foreach (var (slot, local) in slots)
            if (slot != 0 && state.Actors.TryGetValue(local, out var actor)) moves = moves.SetItem(slot, VsMovementCode(actor.MovementType));
        return state with { VsSeekerProgress = progress with { ObjectMovements = moves } };
    }

    private CampaignState ApplyVsMovementChanges(CampaignState state, ImmutableArray<VsSeekerMovementChange> changes)
    {
        var progress = VsProgress(state); var templates = progress.TemplateMovements;
        var movements = progress.ObjectMovements; var actors = state.Actors;
        foreach (var change in changes)
        {
            if (change.TemplateMovement is { } template) templates = templates.SetItem(change.LocalId, template);
            if (change.LiveMovement is not { } movement) continue;
            if (change.ObjectSlot is < 0 or >= 16) throw new InvalidDataException("vs_seeker_live_slot_invalid");
            movements = movements.SetItem(change.ObjectSlot, movement);
            if (state.ObjectSlots?.TryGetValue(change.ObjectSlot, out var id) == true && actors.TryGetValue(id, out var actor))
                actors = actors.SetItem(id, actor with { MovementType = VsMovementSymbol(movement),
                    Facing = movement is >= 7 and <= 10 ? DirectionFromType(VsMovementSymbol(movement)) : actor.Facing });
        }
        return state with { Actors = actors, VsSeekerProgress = progress with { TemplateMovements = templates, ObjectMovements = movements } };
    }

    private CampaignState ResetVsSeekerForMapEntry(CampaignState state)
    {
        if (!VsSeekerEnabled || state.VsSeekerProgress is null) return state;
        state = CaptureVsSeekerObjectMovements(state);
        var progress = VsProgress(state);
        var objects = Enumerable.Range(0, 16).Select(slot =>
        {
            bool active = state.ObjectSlots?.TryGetValue(slot, out _) == true;
            int local = active && slot != 0 ? state.Actors[state.ObjectSlots![slot]].SourceLocalId : 0;
            return new VsSeekerObject(slot, local, progress.ObjectMovements[slot], active, active);
        }).ToImmutableArray();
        var reset = VsSeekerRules.ResetMap(progress.State, objects, state.World.Rng);
        // The new map recreates every object and template after this original reset.
        return state with { World = state.World with { Rng = reset.Rng },
            VsSeekerProgress = CampaignVsSeekerProgress.Empty with { State = reset.State },
            Script = state.Script with { Flags = state.Script.Flags.SetItem(VsSeekerChargingFlag, false) } };
    }

    private CampaignState ApplyVsSeekerWalkingStep(CampaignState state, out bool expired)
    {
        expired = false;
        if (!VsSeekerEnabled || !OwnsVsSeeker(state) && state.VsSeekerProgress is null) return state;
        var progress = VsProgress(state);
        var result = VsSeekerRules.AdvanceStep(progress.State, OwnsVsSeeker(state));
        state = state with { VsSeekerProgress = progress with { State = result.State },
            Script = state.Script with { Flags = state.Script.Flags.SetItem(VsSeekerChargingFlag, result.State.Charging) } };
        if (!(expired = result.RematchesExpired)) return state;
        var reset = VsSeekerRules.ResetAfterChargeComplete(result.State, VsTrainers(state), state.World.Rng);
        return ApplyVsMovementChanges(state with { World = state.World with { Rng = reset.Rng } }, reset.Movements);
    }

    private CampaignState ResetVsSeekerAfterBattle(CampaignState state, TrainerId trainer)
    {
        if (!VsSeekerEnabled || _content.VsSeeker!.FindAny(trainer.Value[5..]) is null) return state;
        var progress = VsProgress(state);
        var result = VsSeekerRules.ResetAfterTrainerBattle(_content.VsSeeker, progress.State, trainer.Value[5..],
            VsTrainers(state), state.SelectedObjectSlot, state.World.Rng);
        return ApplyVsMovementChanges(state with { VsSeekerProgress = progress with { State = result.State },
            World = state.World with { Rng = result.Rng } }, result.Movements);
    }

    private string? ValidateVsSeekerProgress(CampaignState state)
    {
        int count = OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], VsSeekerItem);
        bool got = state.Script.Flags.GetValueOrDefault(VsSeekerGotFlag);
        if (!VsSeekerEnabled) return count != 0 || got || state.VsSeekerProgress is not null ? "save_vs_seeker_not_enabled" : null;
        // Source sets GOT before attempting giveitem; a full bag can legitimately leave only the flag.
        if (count is < 0 or > 1 || count == 1 && !got) return "save_vs_seeker_item_origin";
        if (state.VsSeekerProgress is not { } progress) return state.Script.Flags.GetValueOrDefault(VsSeekerChargingFlag) ? "save_vs_seeker_charging_flag" : null;
        if (progress.State is null || progress.TemplateMovements is null || progress.ObjectMovements.IsDefault || progress.ObjectMovements.Length != 16)
            return "save_vs_seeker_structure";
        try { VsSeekerRules.Validate(progress.State); } catch (ArgumentException) { return "save_vs_seeker_state"; }
        if (state.Script.Flags.GetValueOrDefault(VsSeekerChargingFlag) != progress.State.Charging) return "save_vs_seeker_charging_flag";
        if (!VsSeekerRules.HasReachableCounters(progress.State) || count == 0 && (progress.State.ChargeSteps != 0 || progress.State.Charging || progress.State.RematchSteps != 0 || !progress.State.RematchRanks.IsEmpty))
            return "save_vs_seeker_counter";
        if (!progress.State.Charging && (!progress.State.RematchRanks.IsEmpty || progress.State.RematchSteps != 0)) return "save_vs_seeker_expiry";
        var templates = _content.MapDefinition(state.World.Player.MapId).Actors;
        if (progress.TemplateMovements.Any(p => !templates.Any(a => a.SourceLocalId == p.Key) || p.Value is not (7 or 8 or 9 or 10 or 77 or 78 or 79)) ||
            progress.ObjectMovements.Any(m => m is not (0 or 7 or 8 or 9 or 10 or 77 or 78 or 79)) || progress.ObjectMovements[0] != 0)
            return "save_vs_seeker_movement";
        var fought = VsFought(state);
        foreach (var ready in progress.State.RematchRanks)
        {
            var binding = _content.VsSeeker!.ActorBindings.SingleOrDefault(a =>
                a.SourceMap == _content.MapDefinition(state.World.Player.MapId).SourceMapSymbol && a.LocalId == ready.Key);
            if (binding is null || !fought.Contains(binding.TrainerSymbol)) return "save_vs_seeker_rematch_origin";
            if (ready.Value != VsSeekerRules.NextRank(_content.VsSeeker, binding.TrainerSymbol, fought))
                return "save_vs_seeker_rematch_rank";
        }
        return null;
    }
}
