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
    private CinnabarShopCatalog CinnabarShopProof=>(_content.FirstSeviiEnabled?FirstSeviiShopProof.Predecessor:CinnabarShopCatalog.For(ShopCatalog))??throw new InvalidDataException("cinnabar_shop_proof_missing");
    private InventoryState RepelInventory(InventoryState inventory)=>_content.CinnabarEnabled?CinnabarShopProof.InventoryForPredecessor(FirstSeviiPredecessorInventory(ShopInventoryView(inventory))):ShopInventoryView(inventory);
    private SaffronGymShopCatalog SaffronGymShopProof=>(_content.CinnabarEnabled?CinnabarShopProof.Predecessor:SaffronGymShopCatalog.For(ShopCatalog))??throw new InvalidDataException("saffron_gym_shop_proof_missing");
    public const string RepelCounterVariable = "VAR_REPEL_STEP_COUNT";
    public ushort RepelSteps => checked((ushort)State.Script.Variables.GetValueOrDefault(RepelCounterVariable));

    private string? ValidateRepelState(CampaignState state)
    {
        if (state.Repel is not null) return "save_event_active";
        int steps = state.Script.Variables.GetValueOrDefault(RepelCounterVariable);
        if (!_content.RepelEnabled) return steps != 0 ? "save_repel_profile_missing" : null;
        int maximum = _content.SaffronGymEnabled ? SaffronGymRepelRules.MaxDuration : _content.CeladonApproachEnabled ? EarlyRepelRules.SuperDuration : EarlyRepelRules.Duration;
        return steps < 0 || steps > maximum ? "save_repel_counter_invalid" : null;
    }

    private Decision<CampaignState> UseFieldRepel(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (!_content.RepelEnabled) return No("repel_not_enabled");
        if (state.ItemMenu is not { Origin: "bag", View: "bag" } menu ||
            !(command.Text == EarlyRepelRules.Item.Value ||
                _content.CeladonApproachEnabled && command.Text == LavenderItemRules.SuperRepelId.Value ||
                _content.SaffronGymEnabled && command.Text == SaffronGymShopCatalog.MaxRepelId.Value))
            return No("field_repel_context_invalid");
        var item = new ItemId(command.Text!);
        var error = ValidateCampaignInventory(state.Inventory);
        if (error is not null) return No(error);
        var use = _content.SaffronGymEnabled ? SaffronGymRepelRules.Use(SaffronGymShopProof,RepelInventory(state.Inventory!),
            checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable)),PotionUseContext.Field,item)
            : EarlyRepelRules.Use(ShopCatalog, ShopInventoryView(state.Inventory!),
            checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable)), PotionUseContext.Field,
            item, celadonApproachRules: _content.CeladonApproachEnabled, celadonGymRules: _content.CeladonGymEnabled, rocketHideoutRules: _content.RocketHideoutEnabled, pokemonTowerRules: _content.PokemonTowerEnabled);
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
        if (!_content.RepelEnabled || state.ItemMenu is not { Origin: "bag", View: "bag" } menu)
            throw new InvalidDataException("repel_use_wait_context_invalid");
        var use = _content.SaffronGymEnabled ? SaffronGymRepelRules.Use(SaffronGymShopProof,RepelInventory(state.Inventory!),
            checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable)),PotionUseContext.Field,repel.Item)
            : EarlyRepelRules.Use(ShopCatalog, ShopInventoryView(state.Inventory!),
            checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable)), PotionUseContext.Field,
            repel.Item, celadonApproachRules: _content.CeladonApproachEnabled, celadonGymRules: _content.CeladonGymEnabled, rocketHideoutRules: _content.RocketHideoutEnabled, pokemonTowerRules: _content.PokemonTowerEnabled);
        if (!use.Succeeded) throw new InvalidDataException("repel_use_wait_changed: " + use.Error);
        return new(CommandStatus.Accepted, state with
        {
            Repel = null,
            Inventory = MergeFirstSeviiPredecessorInventory(state.Inventory!, use.Inventory),
            Script = state.Script with { Variables = state.Script.Variables.SetItem(RepelCounterVariable, use.Steps) },
            TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", ShopCatalog.Items.Single(i => i.Id == repel.Item).Name),
            ItemMenu = menu with { View = "message", MessageSymbol = "gText_PlayerUsedVar2", ReturnView = "bag" },
            PresentationEffects = state.PresentationEffects.RemoveAll(e => e.Opcode == "playse" && e.Args.SequenceEqual(new[] { "SE_REPEL" }))
        });
    }

    private CampaignState ApplyRepelWalkingStep(CampaignState state)
    {
        if (!_content.RepelEnabled) return state;
        ushort steps = checked((ushort)state.Script.Variables.GetValueOrDefault(RepelCounterVariable));
        var step = _content.SaffronGymEnabled ? SaffronGymRepelRules.AdvanceStep(SaffronGymShopProof,steps,EncounterCheckTrigger.WalkingStepCompleted)
            : EarlyRepelRules.AdvanceStep(steps, EncounterCheckTrigger.WalkingStepCompleted,
            celadonApproachRules: _content.CeladonApproachEnabled, celadonGymRules: _content.CeladonGymEnabled, rocketHideoutRules: _content.RocketHideoutEnabled, pokemonTowerRules: _content.PokemonTowerEnabled);
        if (step.Steps == steps) return state; // Keep absent zero absent, including upgraded saves.
        state = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem(RepelCounterVariable, step.Steps) } };
        return step.WoreOff ? state with
        {
            Repel = new(CampaignRepelPhase.WoreOff, "repel-expired:" + state.World.StepCount),
            Dialogue = new("Text_RepelWoreOff", _content.TextPages("Text_RepelWoreOff"), 0, false, true)
        } : state;
    }
}
