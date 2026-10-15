using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

public partial class CampaignView
{
    private static bool SourceTrainerPresentationActive(CampaignWildBattle battle) => battle.Frame is not null;

    private bool SourceBatonSwitchBefore(CampaignWildBattle battle, WildBattleSide side, long sequence) =>
        SourceTrainerPresentationActive(battle) && battle.Events.LastOrDefault(e => e.Sequence <= sequence && e is WildBattleCalledPartySwitchedEvent) is
            WildBattleCalledPartySwitchedEvent { Kind: WildBattleCalledSwitchKind.BatonPass } switched &&
        switched.Side == side && (switched.Sequence == sequence || switched.Sequence + 1 == sequence);

    private bool SourcePlayerBatonSwitch(CampaignWildBattle battle) =>
        WildCurrentEvent(battle) is WildBattlePlayerSwitchedEvent switched &&
        SourceBatonSwitchBefore(battle, WildBattleSide.Player, switched.Sequence);

    // Cmd_hpthresholds -> STRINGID_SWITCHINMON uses the opponent HP at this display boundary.
    private static string SourceBatonSendOutText(CampaignWildBattle battle)
    {
        int hp = battle.DisplayOpponent.CurrentHp;
        int percentage = Math.Max(1, hp * 100 / battle.DisplayOpponent.Stats.MaxHp);
        return hp == 0 || percentage >= 70 ? "sText_GoPkmn2" : percentage >= 40 ? "sText_DoItPkmn" :
            percentage >= 10 ? "sText_GoForItPkmn" : "sText_YourFoesWeakGetEmPkmn";
    }

    private static SourceBattleFrame PresentedCanonicalFrame(CampaignWildBattle battle) =>
        battle.Frame ?? throw new InvalidDataException("canonical_battle_presentation_frame_missing");

    private static int? SourceBattleForcedMoveSlot(CampaignWildBattle battle) =>
        PresentedCanonicalFrame(battle).Get(new(0)).Registers.Turn?.Lock?.PpSlot;

    private PokemonCatalog SourceBattlePresentationCatalog(CampaignWildBattle battle)
    {
        var frame = PresentedCanonicalFrame(battle);
        var owned = _catalog.PokemonContent!.Pokemon;
        return owned is not null && (frame.Pokedude is not null
            ? SourceFourActionTurns.ValidateCapability(owned.Mechanics.Catalog, frame) is null : frame.TrainerEncounter is null
            ? frame.SourceCampaignRulesVersion == owned.RulesVersion
            : SourceTrainerEncounterPolicy.Validate(owned.Mechanics.Catalog, frame) is null)
            ? owned.Mechanics.Catalog
            : throw new InvalidDataException("canonical_battle_presentation_catalog");
    }

    private bool SourceBattleRequiresStruggle(CampaignWildBattle battle) =>
        SourceFourActionTurns.RequiresStruggle(SourceBattlePresentationCatalog(battle), PresentedCanonicalFrame(battle), new(0));

    private bool SourceBattleMoveSelectable(CampaignWildBattle battle, int slot) =>
        SourceWildBattlePolicy.ValidateMoveSelection(SourceBattlePresentationCatalog(battle), PresentedCanonicalFrame(battle), slot) is null;

    private SourceTrainerText? SourceBattleSelectionNotice(CampaignWildBattle battle, int slot)
    {
        if (!SourceTrainerPresentationActive(battle)) return null;
        var reason = SourceWildBattlePolicy.ValidateMoveSelection(SourceBattlePresentationCatalog(battle), PresentedCanonicalFrame(battle), slot);
        if (reason is null) return null;
        string symbol = reason switch {
            "called_move_tormented" => "sText_PkmnCantUseMoveTorment",
            "called_move_taunted" => "sText_PkmnCantUseMoveTaunt",
            "called_move_imprisoned" => "sText_PkmnCantUseMoveSealed",
            "called_move_disabled" => "sText_PkmnMoveIsDisabled",
            "called_move_no_pp" => "sText_NoPPLeft",
            _ => "sText_ButItFailed"
        };
        return new(symbol, ("B_ACTIVE_NAME_WITH_PREFIX", WildPrefixedName(WildBattleSide.Player)),
            ("B_CURRENT_MOVE", slot >= 0 && slot < battle.State.Player.Pokemon.Moves.Length
                ? MoveName(battle.State.Player.Pokemon.Moves[slot].Move) : ""));
    }

    private SourceTrainerEventDisplay? SourceTrainerEventDisplay(CampaignWildBattle battle)
    {
        if (!SourceTrainerPresentationActive(battle) || battle.Phase != CampaignBattlePhase.Events || WildCurrentEvent(battle) is null) return null;
        string PartyName(WildBattleSide side, int index)
        {
            if (side == WildBattleSide.Player)
                return index >= 0 && index < PresentedBattlePlayerParty.Length ? PresentedBattlePlayerParty[index].Nickname
                    : throw new InvalidDataException("Source player party display index differs.");
            if (_session.State.ForestTrainerBattle is { } trainer)
            {
                var party = trainer.Combat.OpponentParty;
                return index >= 0 && index < party.Length ? PresentedBattleSpeciesName(party[index].Pokemon.SourceSpeciesId)
                    : throw new InvalidDataException("Source opponent party display index differs.");
            }
            // A wild battle has one real opponent, even if Transform changes its live appearance.
            return index == 0 ? PresentedBattleSpeciesName(WildBattleTransformationRules.PersistentPokemon(battle.State.Opponent).SourceSpeciesId)
                : throw new InvalidDataException("Source wild opponent party display index differs.");
        }
        return SourceTrainerPresentationModel.Describe(battle.Events, battle.EventIndex, WildPrefixedName, MoveName, PartyName, RockTunnelHeldItemName);
    }

    private bool SourceTrainerEventPages(CampaignWildBattle battle, Action<string, (string Key, string Value)[]> source,
        ImmutableArray<string>.Builder pages)
    {
        if (SourceTrainerEventDisplay(battle) is not { } display) return false;
        if (display.Unsupported)
        {
            pages.Add(SourceTrainerPresentationModel.UnsupportedNotice);
            var current = WildCurrentEvent(battle)!;
            Write("unsupportedBattlePresentation", new { type = current.GetType().Name, current.Sequence });
        }
        else foreach (var text in display.Texts) source(text.Symbol, text.Values);
        return true;
    }

    private bool SourceTrainerSilentEvent(CampaignWildBattle battle) => SourceTrainerEventDisplay(battle)?.Silent == true;
    // Only the new profile admits Toxic in the persisted party. Normalization is a view operation.
    private uint ForestPresentationStatus(uint status) => _catalog.PokemonContent?.AllowToxicStatus == true
        ? SourceTrainerPresentationModel.DisplayStatus(status) : status;
}
