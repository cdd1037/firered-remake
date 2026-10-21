using Game.Domain;
using Game.Domain.Items;
using Game.Domain.Tutorials;

namespace Game.Application;

/// <summary>The outer source TV callback. Temporary demonstration data belongs to
/// the demo owner; the campaign party, inventory, dex and rewards remain untouched.</summary>
public sealed partial record CampaignTeachyTv(TeachyTvPlaybackState Playback, string OriginalMusic);

public sealed partial class CampaignSession
{
    public bool TeachyTvEnabled => _content.TeachyTv is not null;
    public TeachyTvCatalog? TeachyTvCatalog => _content.TeachyTv;
    public TeachyTvInstruction? CurrentTeachyTvInstruction => State.TeachyTv is {} active
        ? TeachyTvPlaybackRules.Current(_content.TeachyTv!, active.Playback) : null;

    private Decision<CampaignState> OpenTeachyTv(CampaignState state, CampaignCommand command, bool registered = false)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (!TeachyTvEnabled) return No("teachy_tv_not_enabled");
        if ((registered ? state.RegisteredItem : command.Text) != QuestItemRules.TeachyTvId.Value)
            return No("teachy_tv_item_context");
        if (registered ? state.ItemMenu is not null : state.ItemMenu is not { Origin: "bag", View: "bag", TargetInstanceId: null })
            return No("teachy_tv_callback_context");
        if (!IsStableFieldForParty(state with { ItemMenu = null })) return No("teachy_tv_field_context");
        if (ValidateCampaignInventory(state.Inventory) is {} error) return No(error);
        if (OpeningInventoryRules.Quantity(state.Inventory!.KeyItems ?? [], QuestItemRules.TeachyTvId) != 1)
            return No("teachy_tv_not_owned");
        bool hasTmCase = OpeningInventoryRules.Quantity(state.Inventory.KeyItems ?? [], new("pret:ITEM_TM_CASE")) == 1;
        var playback = TeachyTvPlaybackRules.Open("teachy-tv:" + (Revision + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            registered ? TeachyTvOrigin.RegisteredItem : TeachyTvOrigin.Bag, true, hasTmCase);
        return new(CommandStatus.Accepted, state with { TeachyTv = new(playback, state.Music), Music = "MUS_TEACHY_TV_MENU" });
    }

    private Decision<CampaignState> DecideTeachyTv(CampaignState state, CampaignCommand command)
    {
        var scene = state.TeachyTv!;
        if (!TeachyTvEnabled) return new(CommandStatus.Rejected, state, "teachy_tv_not_enabled");
        if (scene.Playback.Phase == TeachyTvPhase.AwaitingDemonstration)
            return DecideTeachyTvDemonstration(state, command);
        // Every acknowledgement is tied to one outer command revision; stale input
        // cannot advance a subsequent lesson or a re-opened TV session.
        if (command.Text != scene.Playback.WaitId)
            return new(CommandStatus.Rejected, state, "teachy_tv_stale_input");
        if (command.Kind == "teachy_tv_begin_demo")
        {
            var begun = TeachyTvPlaybackRules.BeginDemonstration(_content.TeachyTv!, scene.Playback, command.Text);
            if (!begun.Decision.Accepted) return new(CommandStatus.Rejected, state, begun.Decision.Error);
            return BeginTeachyTvDemonstration(state with { TeachyTv = scene with { Playback = begun.Decision.State } }, begun.Request!);
        }
        TeachyTvDecision result = command.Kind switch
        {
            "teachy_tv_navigate" when command.Choice is {} index => TeachyTvPlaybackRules.Navigate(_content.TeachyTv!, scene.Playback, index),
            "teachy_tv_choose" => TeachyTvPlaybackRules.ConfirmMenu(_content.TeachyTv!, scene.Playback),
            "teachy_tv_presented" => TeachyTvPlaybackRules.Acknowledge(_content.TeachyTv!, scene.Playback, command.Text),
            "teachy_tv_confirm" => TeachyTvPlaybackRules.ConfirmText(_content.TeachyTv!, scene.Playback, command.Text),
            "teachy_tv_cancel" => TeachyTvPlaybackRules.Cancel(scene.Playback),
            "teachy_tv_select_button" => TeachyTvPlaybackRules.SelectButton(scene.Playback),
            _ => new(false, scene.Playback, "teachy_tv_active")
        };
        if (!result.Accepted) return new(CommandStatus.Rejected, state, result.Error);
        string music = result.State.Phase is TeachyTvPhase.Menu or TeachyTvPhase.ReturningToMenu ||
            TeachyTvPlaybackRules.Current(_content.TeachyTv!, result.State)?.SourceCommand == "TTVcmd_End" ? "MUS_TEACHY_TV_MENU"
            : command.Kind == "teachy_tv_presented" && TeachyTvPlaybackRules.Current(_content.TeachyTv!, scene.Playback)?.SourceCommand == "TTVcmd_TransitionRenderBg2TeachyTvGraphicInitNpcPos"
                ? "MUS_FOLLOW_ME" : state.Music;
        return new(CommandStatus.Accepted, result.State.Phase == TeachyTvPhase.Closed
            ? state with { TeachyTv = null, Music = scene.OriginalMusic }
            : state with { TeachyTv = scene with { Playback = result.State }, Music = music });
    }
}
