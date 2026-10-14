using Game.Application;
using Game.Domain.Pokemon;
using Godot;

// PC Summary borrows the existing renderer; Application retains its source,
// page, current identity and any held Pokemon throughout the modal visit.
public partial class CampaignView
{
    private CampaignStorageSummary? _storageSummarySnapshot;
    private void ResetStorageSummaryPresentation()
    {
        _storageSummarySnapshot = null; ResetOwnedSummarySelection();
    }
    private void SynchronizeStorageSummary()
    {
        var summary = _session.State.Pc?.Summary;
        if (_storageSummarySnapshot == summary) return;
        if (summary is null && _storageSummarySnapshot?.Location is { } last && _session.State.Pc is { } pc)
        {
            // Returning from Summary keeps its last viewed physical slot. Held
            // Summary has no location and leaves the underlying cursor untouched.
            if (last.IsParty)
            {
                if (pc.View == CampaignPcView.Deposit) _pcCursor = last.SlotIndex;
                else _storageMovePartyCursor = last.SlotIndex;
            }
            else _boxCursor = last.SlotIndex;
        }
        _storageSummarySnapshot = summary; ResetOwnedSummarySelection();
    }
    private static CampaignCommand BindStorageSummary(CampaignCommand command, CampaignStorageSummary summary) => command with
    {
        StorageLocation = summary.Location,
        TargetInstanceId = summary.Location is null ? null : summary.InstanceId,
        HeldInstanceId = summary.Location is null ? summary.InstanceId : null
    };
    private bool StorageSummaryAction(CampaignCommand command)
    {
        if (_busy) { _pcNotice = StorageMoveBusyNotice; return false; }
        long before = _session.Revision; Apply(command);
        if (_session.Revision != before) return true;
        // Source navigation stops at the first/last visible creature without a popup.
        if (_status.Contains("summary_boundary", StringComparison.Ordinal)) return false;
        ResetOwnedSummarySelection();
        _pcNotice = _status.Contains("identity", StringComparison.Ordinal) || _status.Contains("stale", StringComparison.Ordinal)
            ? SummaryChangedNotice : StorageError(_status);
        return false;
    }
    private bool StorageSummaryInput(InputEventKey key)
    {
        _held = null; _repeat = 0;
        if (!key.Pressed || key.Echo) return true;
        if (_busy) { _pcNotice = StorageMoveBusyNotice; return true; }
        if (_pcNotice is not null)
        { if (key.Keycode is Key.Enter or Key.Backspace or Key.B) _pcNotice = null; return true; }
        var summary = _session.State.Pc!.Summary!;
        var owned = _session.StorageSummaryPokemon;
        if (owned is null) { _pcNotice = SummaryChangedNotice; return true; }
        if (OwnedMoveDetailsInput(key, owned)) return true;
        if (key.Keycode is Key.Backspace or Key.B || key.Keycode == Key.Enter && summary.Page == PokemonSummaryPage.Info)
        { StorageSummaryAction(new("storage_summary_close")); return true; }
        if (key.Keycode is Key.Left or Key.Right && !owned.IsEgg)
        {
            int page = Math.Clamp((int)summary.Page + (key.Keycode == Key.Left ? -1 : 1), 0, 2);
            if (page != (int)summary.Page) StorageSummaryAction(BindStorageSummary(new("storage_summary_page", Choice: page), summary));
        }
        else if (key.Keycode is Key.Up or Key.Down)
            StorageSummaryAction(BindStorageSummary(new("storage_summary_browse", Choice: key.Keycode == Key.Up ? -1 : 1), summary));
        else if (key.Keycode == Key.Enter && summary.Page == PokemonSummaryPage.Moves) OpenOwnedMoveDetails(owned);
        return true;
    }
    private bool MouseStorageSummary(Vector2 point) => _pcNotice is not null ? MouseConfirm(point, MouseMessage)
        : _session.StorageSummaryPokemon is { } owned && MouseOwnedSummary(point, owned, (int)_session.State.Pc!.Summary!.Page);
    private void DrawStorageSummary()
    {
        if (_session.StorageSummaryPokemon is { } owned)
            DrawOwnedSummary(owned, (int)_session.State.Pc!.Summary!.Page, true);
        if (_pcNotice is not null) ItemMessageBox(_pcNotice);
    }
}
