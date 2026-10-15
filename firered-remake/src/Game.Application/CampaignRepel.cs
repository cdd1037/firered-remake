using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public enum CampaignRepelPhase { UseSound, WoreOff }
public sealed record CampaignRepel(CampaignRepelPhase Phase, string WaitId)
{
    public ItemId Item { get; init; } = EarlyRepelRules.Item;
}

public sealed partial class CampaignSession
{
    public const string RepelCounterVariable = "VAR_REPEL_STEP_COUNT";
    public ushort RepelSteps => checked((ushort)State.Script.Variables.GetValueOrDefault(RepelCounterVariable));

    private string? ValidateRepelState(CampaignState state)
    {
        if (state.Repel is not null) return "save_event_active";
        int steps = state.Script.Variables.GetValueOrDefault(RepelCounterVariable);
        return steps < 0 || steps > RepelRules.MaxDuration ? "save_repel_counter_invalid" : null;
    }

    private Decision<CampaignState> UseFieldRepel(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.ItemMenu is not { Origin: "bag", View: "bag" } menu ||
            !(command.Text == EarlyRepelRules.Item.Value ||
                command.Text == LavenderItemRules.SuperRepelId.Value ||
                command.Text == ItemIds.MaxRepelId.Value))
            return No("field_repel_context_invalid");
        var item = new ItemId(command.Text!);
        var error = ValidateCampaignInventory(state.Inventory);
        if (error is not null) return No(error);
        var use = RepelRules.Use(ShopCatalog, _content.Inventory, state.Inventory!,
            checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable)), PotionUseContext.Field, item);
        if (!use.Succeeded)
            return use.Error == "repel_effects_lingered"
                ? new(CommandStatus.Accepted, state with { ItemMenu = menu with { View = "message", MessageSymbol = "gText_RepelEffectsLingered", ReturnView = "bag" } })
                : No(use.Error!);
        // FieldUseFunc_Repel starts SE_REPEL first. Task_UseRepel commits only after
        // the presentation adapter acknowledges this exact sound; it cannot be cancelled.
        long sequence = checked(state.Script.EffectSequence + 1);
        return new(CommandStatus.Accepted, state with
        {
            Repel = new(CampaignRepelPhase.UseSound, "repel-use:" + sequence) { Item = item },
            Script = state.Script with { EffectSequence = sequence },
            PresentationEffects = state.PresentationEffects.Add(new(sequence, "playse", state.World.Player.MapId, ["SE_REPEL"]))
        });
    }

    private Decision<CampaignState> DecideRepel(CampaignState state, CampaignCommand command)
    {
        var repel = state.Repel!;
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (repel.Phase == CampaignRepelPhase.WoreOff)
            return command.Kind == "ack"
                ? new(CommandStatus.Accepted, state with { Repel = null, Dialogue = null })
                : No("repel_expiration_message_pending");
        if (command.Kind != "repel_presented" || command.Text != repel.WaitId) return No("repel_sound_pending");
        if (state.ItemMenu is not { Origin: "bag", View: "bag" } menu)
            throw new InvalidDataException("repel_use_wait_context_invalid");
        var use = RepelRules.Use(ShopCatalog, _content.Inventory, state.Inventory!,
            checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable)), PotionUseContext.Field, repel.Item);
        if (!use.Succeeded) throw new InvalidDataException("repel_use_wait_changed: " + use.Error);
        return new(CommandStatus.Accepted, state with
        {
            Repel = null,
            Inventory = use.Inventory,
            Script = state.Script with { Variables = state.Script.Variables.SetItem(RepelCounterVariable, use.Steps) },
            TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", ShopCatalog.Items.Single(i => i.Id == repel.Item).Name),
            ItemMenu = menu with { View = "message", MessageSymbol = "gText_PlayerUsedVar2", ReturnView = "bag" },
            PresentationEffects = state.PresentationEffects.RemoveAll(e => e.Opcode == "playse" && e.Args.SequenceEqual(new[] { "SE_REPEL" }))
        });
    }

    private CampaignState ApplyRepelWalkingStep(CampaignState state)
    {
        ushort steps = checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable));
        var step = RepelRules.AdvanceStep(steps, EncounterCheckTrigger.WalkingStepCompleted);
        if (step.Steps == steps) return state; // Keep absent zero absent, including upgraded saves.
        state = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem(RepelCounterVariable, step.Steps) } };
        return step.WoreOff ? state with
        {
            Repel = new(CampaignRepelPhase.WoreOff, "repel-expired:" + state.World.StepCount),
            Dialogue = new("Text_RepelWoreOff", _content.TextPages("Text_RepelWoreOff"), 0, false, true)
        } : state;
    }
}
