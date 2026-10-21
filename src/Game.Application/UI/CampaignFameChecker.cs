using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Read-only source viewer callback. Selection and text timing never alter collected facts.</summary>
public sealed record CampaignFameChecker(string Origin);

public sealed partial class CampaignSession
{
    public bool FameCheckerEnabled => _content.FameChecker is not null;
    public ImmutableArray<FameCheckerPersonView> FameCheckerPeople => _content.FameChecker is {} catalog
        ? FameCheckerRules.CreateView(catalog, State.Script.Fame, State.BattledTrainers.Contains("pret:" + FameCheckerRules.GiovanniGymTrainerSymbol)) : [];

    private Decision<CampaignState> OpenFameChecker(CampaignState state, CampaignCommand command, bool registered = false)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (!FameCheckerEnabled) return No("fame_checker_not_enabled");
        if (registered ? state.RegisteredItem != BillItemRules.FameCheckerId.Value : command.Text != BillItemRules.FameCheckerId.Value)
            return No("fame_checker_item_invalid");
        if (registered ? state.ItemMenu is not null : state.ItemMenu is not { Origin: "bag", View: "bag", TargetInstanceId: null })
            return No("fame_checker_callback_context");
        if (!IsStableFieldForParty(state with { ItemMenu = null })) return No("fame_checker_field_context");
        if (ValidateCampaignInventory(state.Inventory) is {} error) return No(error);
        if (OpeningInventoryRules.Quantity(state.Inventory!.KeyItems ?? [], BillItemRules.FameCheckerId) != 1)
            return No("fame_checker_not_owned");
        if (FameCheckerRules.Validate(state.Script.Fame) is {} invalid) return No(invalid);
        return new(CommandStatus.Accepted, state with { FameChecker = new(registered ? "registered" : "bag") });
    }

    private Decision<CampaignState> DecideFameChecker(CampaignState state, CampaignCommand command)
    {
        if (command.Kind != "fame_checker_close") return new(CommandStatus.Rejected, state, "fame_checker_active");
        if (!FameCheckerEnabled || state.FameChecker is not { Origin: "bag" or "registered" } scene ||
            (scene.Origin == "bag" ? state.ItemMenu is not { Origin: "bag", View: "bag", TargetInstanceId: null } : state.ItemMenu is not null))
            return new(CommandStatus.Rejected, state, "fame_checker_callback_context");
        return new(CommandStatus.Accepted, state with { FameChecker = null });
    }

    public bool SupportsRegisteredKeyItem(string id)
    {
        var item = _content.EarlyShop?.Items.SingleOrDefault(i => i.Id.Value == id);
        var questItem = _content.QuestItems?.Items.SingleOrDefault(i => i.Id.Value == id);
        if (item?.Item is not { Registrability: 1, Pocket: "POCKET_KEY_ITEMS" } &&
            (questItem is null || !KeyItemRegistrationRules.CanRegister(questItem))) return false;
        if (id == PowderJarId.Value) return true;
        if (id == QuestItemRules.TeachyTvId.Value) return TeachyTvEnabled;
        return id == ItemfinderRules.Item.Value ? ItemfinderEnabled : id == VsSeekerItem.Value ? VsSeekerEnabled : id == BillItemRules.FameCheckerId.Value && FameCheckerEnabled;
    }

    public string RegisteredKeyItemUseCommand(string id) => id switch {
        "pret:ITEM_POWDER_JAR" => "field_powder_jar",
        "pret:ITEM_VS_SEEKER" when VsSeekerEnabled => "field_vs_seeker",
        "pret:ITEM_ITEMFINDER" when ItemfinderEnabled => "field_itemfinder",
        "pret:ITEM_FAME_CHECKER" when FameCheckerEnabled => "fame_checker_open",
        "pret:ITEM_TEACHY_TV" when TeachyTvEnabled => "teachy_tv_open",
        _ => throw new ArgumentException("registered_item_unavailable", nameof(id)) };

    private string? ValidateRegisteredKeyItem(CampaignState state) => state.RegisteredItem is not {} id ? null :
        !SupportsRegisteredKeyItem(id) || OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], new(id)) != 1
            ? "save_registered_item_invalid" : null;

    private Decision<CampaignState> RegisterKeyItem(CampaignState state, CampaignCommand command)
    {
        if (command.Text is not {} id || !SupportsRegisteredKeyItem(id) ||
            state.ItemMenu is not { Origin: "bag", View: "bag", TargetInstanceId: null } ||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], new(id)) != 1)
            return new(CommandStatus.Rejected, state, "key_item_registration_context_invalid");
        return new(CommandStatus.Accepted, state with { RegisteredItem = state.RegisteredItem == id ? null : id });
    }

    private Decision<CampaignState> UseRegisteredKeyItem(CampaignState state, CampaignCommand command) =>
        state.RegisteredItem == PowderJarId.Value ? UsePowderJar(state,command,registered:true) :
        state.RegisteredItem == QuestItemRules.TeachyTvId.Value ? OpenTeachyTv(state, command, registered: true) :
        state.RegisteredItem == VsSeekerItem.Value ? UseVsSeeker(state, command, registered: true) :
        state.RegisteredItem == ItemfinderRules.Item.Value ? UseItemfinder(state, command, registered: true) :
        state.RegisteredItem == BillItemRules.FameCheckerId.Value ? OpenFameChecker(state, command, registered: true) :
        new(CommandStatus.Rejected, state, "registered_item_unavailable");
}
