using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Godot;

// Both battle party layouts share the native party action window and read-only
// Summary. Inspection changes only these view fields, never the battle frame.
public partial class CampaignView
{
    private SourceSummaryText _summaryPartyText = null!;
    private SummarySelectionStamp? _battlePartySelection;
    private int _battlePartyIndex, _battlePartyAction, _battlePartySummaryPage = -1;
    private bool _battlePartyDouble;
    private const int BattlePartyActionX = 144, BattlePartyActionY = 64;
    private string? BattlePartyContext => _battlePartyDouble
        ? _session.State.DoubleBattle is { } doubles && _doubleMenu == "party"
            ? $"double:{doubles.Id}:{doubles.SelectingSlot}:{doubles.ReplacementSlot}" : null
        : PresentedWildBattle is { Phase: CampaignBattlePhase.ChooseMove } single &&
            (_wildMenu == "party" || single.Replacement == CampaignReplacementStage.Select)
            ? $"single:{single.Id}:{single.Replacement}" : null;
    private PartyPokemon BattleSummaryPokemon(int index)
    {
        var owned = _session.State.Party[index];
        var member = (_battlePartyDouble ? _session.State.DoubleBattle!.Frame : PresentedWildBattle!.Frame)
            .Party(BattleSide.Player).Members[index];
        return PartyPresentationAssets.Displayed(owned, member.Pokemon, member.Status);
    }
    private SummaryPartyMenuEntry[] BattlePartyActions => SummaryInteractionPresentationAssets.BattlePartyActions(
        _session.State.Party.Length, _session.State.Party[_battlePartyIndex].IsEgg,
        _battlePartyDouble ? _session.State.DoubleBattle!.ReplacementSlot is not null
            : PresentedWildBattle!.Replacement == CampaignReplacementStage.Select && !ForestTrainerShiftPending,
        _summaryPartyText.Label);
    private void OpenBattlePartyActions(int index, bool doubles = false)
    {
        _battlePartyDouble = doubles; _battlePartyIndex = index; _battlePartyAction = 0; _battlePartySummaryPage = -1;
        _battlePartySelection = new(BattlePartyContext!, _session.Revision, _session.State.Party[index].InstanceId);
        ResetOwnedSummarySelection();
    }
    private void CloseBattlePartySelection()
    {
        _battlePartySelection = null; _battlePartySummaryPage = -1; ResetOwnedSummarySelection();
    }
    private void CloseStaleBattlePartySelection()
    {
        if (_battlePartyDouble) _doubleNotice = SummaryChangedNotice;
        else _wildNotice = SummaryChangedNotice;
        CloseBattlePartySelection();
    }
    private bool BattlePartySelectionCurrent()
    {
        if (_battlePartySelection is null) return false;
        if (_battlePartyIndex < _session.State.Party.Length && BattlePartyContext is { } context &&
            _battlePartySelection.Matches(context, _session.Revision, _session.State.Party[_battlePartyIndex].InstanceId)) return true;
        CloseStaleBattlePartySelection(); return false;
    }
    private bool BattlePartyInspectionInput(InputEventKey key)
    {
        if (_battlePartySelection is null) return false;
        if (!BattlePartySelectionCurrent() || SummaryExitActive) return true;
        bool back = key.Keycode is Key.Backspace or Key.B;
        bool confirm = key.Keycode is Key.Enter or Key.A or Key.Space;
        if (_battlePartySummaryPage >= 0)
        {
            var owned = BattleSummaryPokemon(_battlePartyIndex);
            if (OwnedMoveDetailsInput(key, owned)) return true;
            if (back || confirm && _battlePartySummaryPage == 0)
            {
                BeginSummaryExit(() => { _battlePartySummaryPage = -1; _battlePartyAction = 0; ResetOwnedSummarySelection(); });
                return true;
            }
            if (!owned.IsEgg)
            {
                if (key.Keycode == Key.Left) _battlePartySummaryPage = Math.Max(0, _battlePartySummaryPage - 1);
                if (key.Keycode == Key.Right) _battlePartySummaryPage = Math.Min(2, _battlePartySummaryPage + 1);
                if (confirm && _battlePartySummaryPage == 2) OpenOwnedMoveDetails(owned, SummaryInteractionMode.ReadOnly);
            }
            if (key.Keycode is Key.Up or Key.Down && PokemonSummaryRules.SeekPartyIndex(_session.State.Party,
                _battlePartyIndex, (PokemonSummaryPage)_battlePartySummaryPage, key.Keycode == Key.Up ? -1 : 1) is { } next)
            {
                _battlePartyIndex = next;
                if (_battlePartyDouble) _doubleCursor = next; else _wildCursor = next;
                _battlePartySelection = new(BattlePartyContext!, _session.Revision, _session.State.Party[next].InstanceId);
            }
            return true;
        }
        var actions = BattlePartyActions;
        if (key.Keycode == Key.Up) _battlePartyAction = Math.Max(0, _battlePartyAction - 1);
        if (key.Keycode == Key.Down) _battlePartyAction = Math.Min(actions.Length - 1, _battlePartyAction + 1);
        var action = SummaryInteractionPresentationAssets.SelectedPartyAction(actions, _battlePartyAction, confirm, back);
        if (action == SummaryPartyAction.Cancel) CloseBattlePartySelection();
        else if (action == SummaryPartyAction.Summary)
        { _battlePartySummaryPage = 0; ResetOwnedSummarySelection(); }
        else if (action is SummaryPartyAction.Shift or SummaryPartyAction.SendOut)
        {
            int index = _battlePartyIndex;
            bool doubles = _battlePartyDouble;
            int actor = doubles ? _session.State.DoubleBattle!.ReplacementSlot ?? _session.State.DoubleBattle.SelectingSlot ?? -1 : -1;
            CloseBattlePartySelection();
            if (doubles) DoubleSubmit(new("double_switch", PartyIndex: actor, Choice: index));
            else ApplyWildChoice(new("battle_switch", PartyIndex: index));
        }
        return true;
    }
    private bool DrawBattlePartyInspection()
    {
        if (!BattlePartySelectionCurrent()) return false;
        if (_battlePartySummaryPage >= 0)
        { DrawOwnedSummary(BattleSummaryPokemon(_battlePartyIndex), _battlePartySummaryPage, true); return true; }
        bool cancellable = _battlePartyDouble ? _session.State.DoubleBattle!.ReplacementSlot is null
            : PresentedWildBattle!.Replacement is null || ForestTrainerShiftPending;
        DrawNativeParty(_session.State.Party, _battlePartyIndex, _summaryPartyText.ActionPrompt, cancel: cancellable,
            layout: _battlePartyDouble ? NativePartyLayout.Double : NativePartyLayout.Single, displayed: BattleSummaryPokemon);
        var actions = BattlePartyActions;
        _battleSkin.DrawStandardFrame(this, new(BattlePartyActionX, BattlePartyActionY, 96, actions.Length * 16 + 16));
        for (int i = 0; i < actions.Length; i++) SourceText(_sourceNormal, new(BattlePartyActionX + 16, BattlePartyActionY + 8 + i * 16), actions[i].Label);
        SourceText(_sourceNormal, new(BattlePartyActionX + 4, BattlePartyActionY + 8 + _battlePartyAction * 16), "▶");
        return true;
    }
    private bool MouseBattlePartyInspection(Vector2 point)
    {
        if (!BattlePartySelectionCurrent()) return true;
        return _battlePartySummaryPage >= 0 ? MouseOwnedSummary(point, BattleSummaryPokemon(_battlePartyIndex), _battlePartySummaryPage)
            : MouseSelect(point, ref _battlePartyAction, BattlePartyActions.Length, BattlePartyActionX, BattlePartyActionY + 8, 96, 16);
    }
    private bool NavigateBattlePartyInspection(Vector2 point, int direction)
    {
        if (!BattlePartySelectionCurrent()) return true;
        return _battlePartySummaryPage >= 0 ? NavigateOwnedSummaryPointer(point, direction, BattleSummaryPokemon(_battlePartyIndex))
            : PointerRows(point, direction, ref _battlePartyAction, BattlePartyActions.Length, BattlePartyActionX, BattlePartyActionY + 8, 96, 16);
    }
}
