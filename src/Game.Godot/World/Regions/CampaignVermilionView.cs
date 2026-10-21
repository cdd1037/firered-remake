using Game.Domain.Battle;
using Game.Application;
using Game.Domain.WildBattle;
using Godot;

// Presentation consumes authenticated source pixels and committed events only.
// Item consumption, Pickup rolls and Pay Day settlement remain application rules.
public partial class CampaignView
{
    private void LoadVermilionPresentation()
    {
        if (_catalog.VermilionPokemon is not { } source) return;

        RegisterPokemonSprites(source);

        RegisterPokemonIcons(source);
    }

    // Source battle_scripts_1.s:3883 and 4212–4281 select these battle_message.c strings.
    // Status/confusion changes precede their single held-item message in the event stream.
    private ForestMessage? VermilionWildMessage(SourceBattleEvent current) => current switch
    {
        SourcePayDayEvent => new("sText_CoinsScattered"),
        SourceAbilityPreventedEvent { Ability: "ABILITY_LIMBER" } ability => new("sText_PkmnPreventsParalysisWith",
            ("B_EFF_NAME_WITH_PREFIX", WildPrefixedName(ability.Target)), ("B_DEF_ABILITY", _catalog.DisplayNames.AbilityName(ability.Ability))),
        SourceStatusChangedEvent { Reason: "limber", After: 0 } change => new("sText_PkmnsXCuredItsYProblem",
            ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(change.Side)), ("B_SCR_ACTIVE_ABILITY", _catalog.DisplayNames.AbilityName("ABILITY_LIMBER")), ("B_BUFF1", ForestStatusName(change.Before))),
        SourceStatusChangedEvent { Reason: "held-berry" } or SourceConfusionChangedEvent { Reason: "persim-berry" } => new(""),
        SourceHeldItemConsumedEvent { Item: "ITEM_CHERI_BERRY" or "ITEM_CHESTO_BERRY" or "ITEM_PECHA_BERRY" or "ITEM_RAWST_BERRY" or "ITEM_PERSIM_BERRY" } item =>
            new(item.Item switch
            {
                "ITEM_CHERI_BERRY" => "sText_PkmnsItemCuredParalysis",
                "ITEM_CHESTO_BERRY" => "sText_PkmnsItemWokeIt",
                "ITEM_PECHA_BERRY" => "sText_PkmnsItemCuredPoison",
                "ITEM_RAWST_BERRY" => "sText_PkmnsItemHealedBurn",
                _ => "sText_PkmnsItemSnappedOut"
            }, ("B_SCR_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(item.Side)), ("B_LAST_ITEM", _catalog.DisplayNames.ItemName(item.Item))),
        SourceHeldItemStolenEvent stolen => new("sText_PkmnStoleItem",
            ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(stolen.Actor)), ("B_DEF_NAME_WITH_PREFIX", WildPrefixedName(stolen.Target)), ("B_LAST_ITEM", _catalog.DisplayNames.ItemName(stolen.Item))),
        _ => null
    };

    private bool VermilionSilentEvent(CampaignWildBattle battle) => WildCurrentEvent(battle) is { } current &&
        VermilionWildMessage(current) is { Symbol.Length: 0 };

    private int VermilionTrainerPayDayPageCount => _session.State.ForestTrainerBattle is { Combat.Battle: { Outcome: WildBattleOutcome.Won, PayDayMoney: > 0 } } ? 1 : 0;

    private bool VermilionPayDayResultPage(CampaignWildBattle battle, Action<string, (string Key, string Value)[]> source)
    {
        if (battle.Frame?.TrainerTowerEncounter is not null || battle.Phase != CampaignBattlePhase.Results || battle.Pages.IsEmpty ||
            battle.PageIndex != battle.Pages.Length - 1) return false;
        var trainer = _session.State.ForestTrainerBattle;
        bool settled = trainer is null ? battle.PostBattleItemsSettled : trainer.Stage == CampaignForestTrainerStage.Finished;
        var result = trainer?.Combat.Battle ?? battle.State;
        if (!settled || result.Outcome != WildBattleOutcome.Won || result.PayDayMoney == 0) return false;
        // Source Cmd_givepaydaymoney prints the collected amount even at the wallet cap.
        source("sText_PkmnPickedUpItem", [("B_BUFF1", result.PayDayMoney.ToString())]);
        return true;
    }
}
