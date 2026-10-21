using Game.Domain.Battle;
using Game.Domain.Growth;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Application;

/// <summary>One receipt can own multiple text pages without another domain event or ACK.</summary>
public sealed record CampaignBattleSwitchDisplay(WildBattleCombatant Previous, WildBattleCombatant Replacement,
    WildBattleCalledSwitchKind Kind, BattlerSlot Slot = default);

public sealed partial class CampaignSession
{
    private PokemonInstance DisplayRewardPage(CampaignWildBattle battle, int pageIndex)
    {
        var page=battle.Pages[pageIndex];
        if(page.Reward is not WildRewardLevelEvent level ||
            (page.PartyIndex??battle.ActivePartyIndex)!=battle.ActivePartyIndex)return battle.DisplayPlayer;
        var display=battle.DisplayPlayer;
        return display with {Level=level.After,Stats=level.StatsAfter,CurrentHp=level.HpAfter,
            Experience=GrowthRules.ExperienceAtLevel(CanonicalWildPokemon.Species[display.Species].GrowthRate,level.After)};
    }

    private static CampaignWildBattle AdvanceDisplayThrough(CampaignWildBattle battle,long sequence)
    {
        var batch=battle.DisplayBatch??throw new InvalidDataException("display_body_seed_missing");
        var current=battle.EventIndex<battle.Events.Length?battle.Events[battle.EventIndex]:null;
        if(current is not null && current.Sequence==sequence && batch.NextEventIndex<=battle.EventIndex)
        {
            // Preserve the outgoing event-time body before installing the replacement.
            var before=batch.AdvanceThrough(battle.Events,sequence-1);
            CampaignBattleSwitchDisplay? change=current switch {
                SourceCalledPartySwitchedEvent {Replacement:{} replacement} x when battle.Frame.TrainerEncounter is not null =>
                    new(before.Display.Bodies[x.Side.Value],replacement.Body,x.Kind,x.Side),
                SourcePlayerSwitchedEvent x => new(x.Previous.Body,x.Replacement.Body,WildBattleCalledSwitchKind.Voluntary),
                SourcePlayerReplacedEvent x => new(x.Previous.Body,x.Replacement.Body,WildBattleCalledSwitchKind.FaintedReplacement),
                _=>null };
            batch=before.AdvanceThrough(battle.Events,sequence);
            battle=battle with{DisplayBatch=batch,SwitchDisplay=change};
            // Reward pages retain their own visible-level boundary. Applying the receipt to
            // canonical display bodies must not reveal the reward before its text page.
            var deferredTrainerSwitch=current is SourceCalledPartySwitchedEvent called &&
                battle.Frame.TrainerEncounter is not null &&
                (called.Side.Value==1 || called.Kind==WildBattleCalledSwitchKind.Forced);
            if(!deferredTrainerSwitch && current is (SourceStartedEvent or SourceHeldItemHealEvent or SourceAbilityRecoveredEvent or SourceSelfDestructEvent or SourceFaintedEvent or
                SourcePpCommandEvent or SourceSnatchPressurePpEvent or SourceGrudgePpEvent or
                SourceWeatherDamageEvent or SourceDrainEvent or SourceMoveCopiedEvent or SourceTransformedEvent or
                SourcePlayerReplacedEvent or SourcePlayerSwitchedEvent or SourceCalledPartySwitchedEvent or
                SourceMoveUsedEvent or SourceTowerHpEvent or SourceSpiteEvent or SourceHpChangedEvent or
                SourceHeldItemConsumedEvent or SourceIngrainHealEvent or SourceRecoveredEvent or SourcePotionRecoveredEvent or
                SourceMedicineAppliedEvent or SourceRecoilEvent or SourceAbilityDamageEvent or SourceConfusionSelfHitEvent or
                SourceLeechSeedDrainEvent or SourceBindingDamageEvent or SourceStatusDamageEvent))
                battle=battle with{DisplayPlayer=BattleDisplayPokemon(batch.Display.Bodies[0]),
                    DisplayOpponent=BattleDisplayPokemon(batch.Display.Bodies[1]),
                    DisplayPlayerAppearance=BattleAppearance(batch.Display.Bodies[0]),
                    DisplayOpponentAppearance=BattleAppearance(batch.Display.Bodies[1])};
            return battle;
        }
        return battle with{DisplayBatch=batch.AdvanceThrough(battle.Events,sequence)};
    }

    private static CampaignBattleDisplayBatch BeginDisplayBatch(CampaignWildBattle previous)
    {
        if(previous.DisplayBatch is not {} old || old.NextEventIndex!=previous.Events.Length)
            throw new InvalidDataException("display_body_previous_batch_pending");
        return new(CampaignBattleDisplayBodies.FromSlots(previous.Frame.Slots));
    }
}
