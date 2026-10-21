using Game.Application;
using Game.Domain.Pokemon;
using Godot;

// One context menu for the three creature workspaces. Only menu cursors and
// snapshots live here; Release confirmation and ownership belong to Application.
public partial class CampaignView
{
    private string[] _storageCreatureActions = [], _storageReleasePages = [];
    private int _storageCreatureChoice, _storageReleaseChoice, _storageReleasePage;
    private long _storageCreatureRevision, _storageReleaseRevision;
    private PokemonStorageMoveLocation? _storageCreatureLocation;
    private long? _storageCreatureTargetId, _storageCreatureHeldId;
    private string _storageCreatureName = "", _storageReleaseName = "";
    private CampaignStorageReleaseTarget? _storageReleaseTarget;
    private bool StorageCreatureOverlayActive => _storageCreatureActions.Length != 0 ||
        _session.State.Pc?.ReleaseTarget is not null || _storageReleasePages.Length != 0;
    private PokemonStorageMoveLocation StorageCreatureLocation => _session.State.Pc!.View switch
    {
        CampaignPcView.Deposit => PokemonStorageMoveLocation.Party(_pcCursor),
        CampaignPcView.Withdraw => PokemonStorageMoveLocation.Box(_session.State.Storage!.CurrentBox, _boxCursor),
        _ => _storageMovePartyOpen ? PokemonStorageMoveLocation.Party(_storageMovePartyCursor)
            : PokemonStorageMoveLocation.Box(_session.State.Storage!.CurrentBox, _boxCursor)
    };
    private PartyPokemon? StorageCreatureAt(PokemonStorageMoveLocation? location) => location is null
        ? _session.State.Pc?.MoveCursor?.Pokemon
        : location.IsParty ? location.SlotIndex < _session.State.Party.Length ? _session.State.Party[location.SlotIndex] : null
        : _session.State.Storage!.Boxes[location.BoxIndex].Slots[location.SlotIndex];

    private void ResetStorageCreaturePresentation()
    {
        _storageCreatureActions = []; _storageReleasePages = []; _storageCreatureChoice = _storageReleasePage = 0;
        _storageReleaseChoice = 1; _storageReleaseTarget = null; _storageCreatureLocation = null;
        _storageCreatureTargetId = _storageCreatureHeldId = null; _storageCreatureName = _storageReleaseName = "";
    }

    private void SynchronizeStorageReleasePrompt()
    {
        var target = _session.State.Pc?.ReleaseTarget;
        if (_storageReleaseTarget == target) return;
        _storageReleaseTarget = target;
        if (target is null) return;
        _storageCreatureActions = []; _storageReleasePages = [];
        _storageReleaseChoice = target.DefaultChoice; _storageReleaseRevision = _session.Revision;
        var pokemon = StorageCreatureAt(target.Location);
        _storageReleaseName = pokemon is null ? "" : PresentedOwnedName(pokemon);
    }

    private void OpenStorageCreatureActions()
    {
        var pc = _session.State.Pc!;
        if (pc.View is not (CampaignPcView.Deposit or CampaignPcView.Withdraw or CampaignPcView.Move)) return;
        var location = StorageCreatureLocation;
        var target = StorageCreatureAt(location);
        var held = pc.MoveCursor?.Pokemon;
        if (held is null && target is null) return;
        // Pinned SetMenuTextsForMon ordering, including original unsupported rows.
        string first = pc.View == CampaignPcView.Deposit ? "Store" : pc.View == CampaignPcView.Withdraw ? "Withdraw"
            : held is null ? "Move" : target is null ? "Place" : "Shift";
        _storageCreatureActions = pc.View == CampaignPcView.Move
            ? [first, "Summary", location.IsParty ? "Store" : "Withdraw", "Mark", "Release", "Cancel"]
            : [first, "Summary", "Mark", "Release", "Cancel"];
        _storageCreatureChoice = 0; _storageCreatureRevision = _session.Revision;
        _storageCreatureLocation = location; _storageCreatureTargetId = target?.InstanceId; _storageCreatureHeldId = held?.InstanceId;
        _storageCreatureName = PresentedOwnedName(held ?? target!);
    }

    private bool StorageCreatureSelectionIsCurrent()
    {
        if (_session.Revision == _storageCreatureRevision && StorageCreatureLocation == _storageCreatureLocation &&
            StorageCreatureAt(_storageCreatureLocation)?.InstanceId == _storageCreatureTargetId &&
            _session.State.Pc?.MoveCursor?.Pokemon.InstanceId == _storageCreatureHeldId) return true;
        _pcNotice = _storageText.Get("notice.pokemon-changed"); return false;
    }

    private void ConfirmStorageCreatureAction()
    {
        string action = _storageCreatureActions[_storageCreatureChoice]; _storageCreatureActions = [];
        if (action == "Cancel" || !StorageCreatureSelectionIsCurrent()) return;
        var location = _storageCreatureLocation!;
        if (action == "Mark")
        {
            StorageCustomizationAction(new("storage_mark_open")
            {
                StorageLocation = _storageCreatureHeldId is null ? location : null,
                TargetInstanceId = _storageCreatureHeldId is null ? _storageCreatureTargetId : null,
                HeldInstanceId = _storageCreatureHeldId
            });
            return;
        }
        if (action == "Summary")
        {
            StorageSummaryAction(new("storage_summary_open")
            {
                StorageLocation = _storageCreatureHeldId is null ? location : null,
                TargetInstanceId = _storageCreatureHeldId is null ? _storageCreatureTargetId : null,
                HeldInstanceId = _storageCreatureHeldId
            });
            return;
        }
        if (_session.State.Pc!.View == CampaignPcView.Move && action is "Store" or "Withdraw")
        {
            _pcNotice = _storageText.Get("notice.deposit-withdraw-menu");
            return;
        }
        if (action == "Release")
        {
            StorageReleaseAction(new("storage_release")
            {
                StorageLocation = _storageCreatureHeldId is null ? location : null,
                TargetInstanceId = _storageCreatureHeldId is null ? _storageCreatureTargetId : null,
                HeldInstanceId = _storageCreatureHeldId
            });
            return;
        }
        if (action == "Store") { StorageAction(new("storage_deposit", PartyIndex: location.SlotIndex), "gText_PkmnWasDeposited", _storageCreatureName); return; }
        if (action == "Withdraw") { StorageAction(new("storage_withdraw", MoveSlot: location.SlotIndex), "gText_PkmnCameBack", _storageCreatureName); return; }
        StorageMoveAction(new(action switch { "Move" => "storage_move_grab", "Place" => "storage_move_place", _ => "storage_move_shift" })
            { StorageLocation = location, TargetInstanceId = _storageCreatureTargetId, HeldInstanceId = _storageCreatureHeldId });
    }

    private bool StorageReleaseAction(CampaignCommand command)
    {
        if (_busy) { _pcNotice = StorageMoveBusyNotice; return false; }
        bool accepted = Apply(command);
        if (accepted) return true;
        _pcNotice = _status.Contains("storage_release_egg", StringComparison.Ordinal) ? PcText("gText_YouCantReleaseAnEgg")
            : _status.Contains("identity", StringComparison.Ordinal) || _status.Contains("stale", StringComparison.Ordinal)
                ? _storageText.Get("notice.pokemon-changed") : StorageError(_status);
        return false;
    }

    private void ConfirmStorageRelease(bool cancel)
    {
        var target = _session.State.Pc!.ReleaseTarget!;
        if (cancel || _storageReleaseChoice == 1) { StorageReleaseAction(new("storage_release_prompt", Choice: cancel ? 127 : 1)); return; }
        if (target != _storageReleaseTarget || _session.Revision != _storageReleaseRevision)
        {
            StorageReleaseAction(new("pc_back"));
            _pcNotice = _storageText.Get("notice.pokemon-changed"); return;
        }
        string name = _storageReleaseName;
        if (StorageReleaseAction(new("storage_release_prompt", Choice: 0)
            { StorageLocation = target.Location, TargetInstanceId = target.Location is null ? null : target.InstanceId,
                HeldInstanceId = target.Location is null ? target.InstanceId : null }))
        {
            _storageReleasePages = [PcText("gText_PkmnWasReleased", name), PcText("gText_ByeByePkmn", name)];
            _storageReleasePage = 0;
        }
        else if (_status.Contains("storage_release_hm_protected", StringComparison.Ordinal))
        {
            // No removal happened. Without the original departure/return animation,
            // never show its misleading "was released" line on this refused path.
            if (StorageReleaseAction(new("pc_back")))
            {
                _pcNotice = null;
                _storageReleasePages = [PcText("gText_PkmnCameBack", name), PcText("gText_WasItWorriedAboutYou")];
                _storageReleasePage = 0;
            }
        }
    }

    private bool StorageCreatureInput(InputEventKey key)
    {
        if (!StorageCreatureOverlayActive) return false;
        if (_busy) { _pcNotice = StorageMoveBusyNotice; return true; }
        bool back = key.Keycode is Key.Backspace or Key.B;
        if (_storageReleasePages.Length != 0)
        {
            if (back || key.Keycode is Key.Enter or Key.Up or Key.Down or Key.Left or Key.Right)
                if (++_storageReleasePage >= _storageReleasePages.Length) { _storageReleasePages = []; _storageReleasePage = 0; }
            return true;
        }
        if (_session.State.Pc!.ReleaseTarget is not null)
        {
            if (key.Keycode == Key.Up) _storageReleaseChoice = 0;
            if (key.Keycode == Key.Down) _storageReleaseChoice = 1;
            if (back || key.Keycode == Key.Enter) ConfirmStorageRelease(back);
            return true;
        }
        if (back) _storageCreatureActions = [];
        else { FieldMenuMoveCursor(key, ref _storageCreatureChoice, _storageCreatureActions.Length); if (key.Keycode == Key.Enter) ConfirmStorageCreatureAction(); }
        return true;
    }

    private bool MouseStorageCreature(Vector2 point) => _storageReleasePages.Length != 0 ? MouseConfirm(point, MouseMessage)
        : _session.State.Pc!.ReleaseTarget is not null ? MouseSelect(point, ref _storageReleaseChoice, 2, 176, 72, 64)
        : MouseSelect(point, ref _storageCreatureChoice, _storageCreatureActions.Length, 144, 8, 96);
    private bool NavigateStorageCreaturePointer(Vector2 point, int direction) => _storageReleasePages.Length != 0 ? false
        : _session.State.Pc!.ReleaseTarget is not null ? PointerRows(point, direction, ref _storageReleaseChoice, 2, 176, 72, 64)
        : PointerRows(point, direction, ref _storageCreatureChoice, _storageCreatureActions.Length, 144, 8, 96);

    private void DrawStorageCreatureOverlay()
    {
        if (_storageCreatureActions.Length != 0)
        {
            ItemMessageBox(PcText("gText_PkmnIsSelected", _storageCreatureName));
            _battleSkin.DrawUserFrame(this, new(144, 0, 96, _storageCreatureActions.Length * 16 + 16));
            for (int i = 0; i < _storageCreatureActions.Length; i++) SourceText(_sourceNormal, new(160, 8 + i * 16), PcText("gPCText_" + _storageCreatureActions[i]));
            SourceText(_sourceNormal, new(148, 8 + _storageCreatureChoice * 16), "▶");
        }
        if (_session.State.Pc!.ReleaseTarget is not null)
        {
            ItemMessageBox(PcText("gText_ReleaseThisPokemon"));
            _battleSkin.DrawUserFrame(this, new(176, 64, 64, 48));
            SourceText(_sourceNormal, new(192, 72), PcText("gText_Yes")); SourceText(_sourceNormal, new(192, 88), PcText("gText_No"));
            SourceText(_sourceNormal, new(180, 72 + _storageReleaseChoice * 16), "▶");
        }
        if (_storageReleasePages.Length != 0) ItemMessageBox(_storageReleasePages[_storageReleasePage]);
    }
}
