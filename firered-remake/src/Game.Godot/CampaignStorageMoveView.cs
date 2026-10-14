using Game.Application;
using Game.Domain.Pokemon;
using Godot;

// Thin MOVE POKEMON adapter. Only cursor/panel presentation is local: the held
// Pokemon and every grab/place/shift/refusal belong to CampaignSession.
public partial class CampaignView
{
    private bool _storageMovePartyOpen;
    private int _storageMovePartyCursor, _storageMovePreviousPartyCursor = 1;
    private const string StorageMoveBusyNotice = "The PC is busy.\nPlease try again.";
    private static readonly Rect2 StorageMovePartyButton = new(80, 0, 88, 16);
    private static readonly Rect2 StorageMoveCloseButton = new(176, 0, 64, 16);
    private static readonly Rect2 StorageMovePartyCancel = new(128, 136, 48, 24);

    private void ResetStorageMovePresentation()
    {
        _storageMovePartyOpen = false;
        _storageMovePartyCursor = 0;
        _storageMovePreviousPartyCursor = 1;
    }

    private static Rect2 StorageMoveBoxRect(int index) => new(88 + index % 6 * 24, 32 + index / 6 * 24, 24, 24);
    private static Vector2 StorageMovePartyIconPosition(int index) => index == 0 ? new(88, 48) : new(136, (index - 1) * 24);
    private static Rect2 StorageMovePartyRect(int index) => index == 0 ? new(88, 56, 32, 24) : new(136, 8 + (index - 1) * 24, 32, 24);
    private PartyPokemon? StorageMoveTarget => _storageMovePartyOpen
        ? _storageMovePartyCursor < _session.State.Party.Length ? _session.State.Party[_storageMovePartyCursor] : null
        : _session.State.Storage!.Boxes[_session.State.Storage.CurrentBox].Slots[_boxCursor];

    private bool StorageMoveAction(CampaignCommand command)
    {
        if (_busy) { _pcNotice = StorageMoveBusyNotice; return false; }
        long before = _session.Revision;
        Apply(command);
        if (_session.Revision != before) return true;
        _pcNotice = StorageMoveError(_status);
        return false;
    }

    private string StorageMoveError(string error)
    {
        if (error.Contains("storage_move_holding", StringComparison.Ordinal)) return PcText("gText_YoureHoldingAPkmn");
        if (error.Contains("storage_move_active", StringComparison.Ordinal)) return StorageMoveBusyNotice;
        if (error.Contains("storage_mail_unsupported", StringComparison.Ordinal)) return PcText("gText_MailCantBeStored");
        if (error.Contains("storage_move_held_identity", StringComparison.Ordinal) ||
            error.Contains("storage_move_target_identity", StringComparison.Ordinal) ||
            error.Contains("storage_move_box_not_selected", StringComparison.Ordinal))
            return "The selected POKéMON has changed.\nPlease choose its current slot.";
        if (error.Contains("storage_move_slot_occupied", StringComparison.Ordinal))
            return "That slot is occupied.\nChoose the POKéMON to shift.";
        return StorageError(error);
    }

    private void ToggleStorageMoveParty()
    {
        _storageTitleSelected = false;
        if (_storageMovePartyOpen)
        {
            // Source Task_PokeStorageMain refuses hiding the party with held Mail.
            if (StorageMoveAction(new("storage_move_hide_party"))) _storageMovePartyOpen = false;
        }
        else
        {
            _storageMovePartyOpen = true;
            _storageMovePartyCursor = 0;
            _storageMovePreviousPartyCursor = 1;
        }
    }

    private void ConfirmStorageMoveSlot()
    {
        if (_storageMovePartyOpen && _storageMovePartyCursor == 6) { ToggleStorageMoveParty(); return; }
        OpenStorageCreatureActions();
    }

    private bool StorageMoveInput(InputEventKey key)
    {
        if (!key.Pressed || key.Echo) return true;
        _held = null; _repeat = 0;
        if (_pcNotice is not null)
        {
            if (key.Keycode is Key.Enter or Key.Backspace or Key.B) _pcNotice = null;
            return true;
        }
        if (StorageCreatureInput(key)) return true;
        if (_busy) { _pcNotice = StorageMoveBusyNotice; return true; }
        if (key.Keycode is Key.Backspace or Key.B)
        {
            // B inside the source party panel means hide party; B in the box
            // means try to exit. Neither path returns a held Pokemon automatically.
            if (_storageMovePartyOpen) ToggleStorageMoveParty();
            else StorageMoveAction(new("pc_back"));
            return true;
        }
        if (key.Keycode == Key.Tab) { ToggleStorageMoveParty(); return true; }
        if (key.Keycode is Key.Pageup or Key.Pagedown)
        {
            int box = (_session.State.Storage!.CurrentBox + (key.Keycode == Key.Pagedown ? 1 : 13)) % 14;
            StorageMoveAction(new("storage_box", Choice: box)); return true;
        }
        NavigateStorageWorkspace(key, ToggleStorageMoveParty);
        if (key.Keycode == Key.Enter) ConfirmStorageMoveSlot();
        return true;
    }

    private void NavigateStorageWorkspace(InputEventKey key, Action toggleParty)
    {
        if (_storageMovePartyOpen)
        {
            if (key.Keycode == Key.Up) _storageMovePartyCursor = (_storageMovePartyCursor + 6) % 7;
            if (key.Keycode == Key.Down) _storageMovePartyCursor = (_storageMovePartyCursor + 1) % 7;
            if (key.Keycode == Key.Left && _storageMovePartyCursor != 0)
            { _storageMovePreviousPartyCursor = _storageMovePartyCursor; _storageMovePartyCursor = 0; }
            if (key.Keycode == Key.Right)
            {
                if (_storageMovePartyCursor == 0) _storageMovePartyCursor = _storageMovePreviousPartyCursor;
                else toggleParty();
            }
        }
        else
        {
            if (key.Keycode == Key.Left) _boxCursor = _boxCursor % 6 == 0 ? _boxCursor + 5 : _boxCursor - 1;
            if (key.Keycode == Key.Right) _boxCursor = _boxCursor % 6 == 5 ? _boxCursor - 5 : _boxCursor + 1;
            if (key.Keycode == Key.Up) _boxCursor = (_boxCursor + 24) % 30;
            if (key.Keycode == Key.Down) _boxCursor = (_boxCursor + 6) % 30;
        }
    }

    private bool MouseStorageMove(Vector2 point)
    {
        if (_pcNotice is not null) return MouseConfirm(point, MouseMessage);
        if (StorageCreatureOverlayActive) return MouseStorageCreature(point);
        if (!_storageMovePartyOpen && MouseStorageBoxTitle(point)) return true;
        if (StorageMoveCloseButton.HasPoint(point))
        {
            _menuMouseActionFrame = Engine.GetProcessFrames();
            StorageMoveAction(new("storage_move_close")); return true;
        }
        if (_storageMovePartyOpen)
        {
            if (StorageMovePartyCancel.HasPoint(point)) { _storageMovePartyCursor = 6; return MouseKey(); }
            for (int i = 0; i < 6; i++)
                if (StorageMovePartyRect(i).HasPoint(point)) { _storageMovePartyCursor = i; return MouseKey(); }
            return false;
        }
        if (StorageMovePartyButton.HasPoint(point)) return MouseKey(Key.Tab);
        if (new Rect2(84, 16, 24, 16).HasPoint(point)) return MouseKey(Key.Pageup);
        if (new Rect2(216, 16, 24, 16).HasPoint(point)) return MouseKey(Key.Pagedown);
        if (new Rect2(0, 140, 80, 20).HasPoint(point)) return MouseKey(Key.Tab);
        if (!new Rect2(88, 32, 144, 120).HasPoint(point)) return false;
        _storageTitleSelected = false; _boxCursor = (int)((point.X - 88) / 24) + 6 * (int)((point.Y - 32) / 24);
        return MouseKey();
    }

    private bool NavigateStorageMovePointer(Vector2 point, int direction)
    {
        if (_pcNotice is not null) return false;
        if (StorageCreatureOverlayActive) return NavigateStorageCreaturePointer(point, direction);
        if (_storageMovePartyOpen)
        {
            if (StorageMovePartyCancel.HasPoint(point))
            {
                if (direction != 0) return MouseNavigationKey(direction);
                if (_storageMovePartyCursor == 6) return false;
                _storageMovePartyCursor = 6; return true;
            }
            for (int i = 0; i < 6; i++)
                if (StorageMovePartyRect(i).HasPoint(point))
                {
                    if (direction != 0) return MouseNavigationKey(direction);
                    if (_storageMovePartyCursor == i) return false;
                    _storageMovePartyCursor = i; return true;
                }
            return false;
        }
        if (!new Rect2(88, 32, 144, 120).HasPoint(point)) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        int index = (int)((point.X - 88) / 24) + 6 * (int)((point.Y - 32) / 24);
        if (_boxCursor == index && !_storageTitleSelected) return false;
        _storageTitleSelected = false; _boxCursor = index; return true;
    }

    private void DrawStorageMove()
    {
        var held = _session.State.Pc!.MoveCursor?.Pokemon;
        var target = StorageMoveTarget;
        Rect2 cursor = DrawStorageWorkspace();
        if (!_storageTitleSelected) DrawRect(cursor, new Color(held is null ? "f4db88" : "fff4b8"), filled: false, width: 1);
        if (held is not null)
        {
            // A deliberately simple held marker, using the same authenticated icon
            // as its party/box slot. The original animated hand is not yet restored.
            var heldRect = new Rect2(cursor.Position + new Vector2(cursor.Size.X - 12, -7), new(16, 16));
            DrawRect(heldRect, new Color("f4db88"));
            DrawTextureRectRegion(PresentedOwnedIcon(held), heldRect, new(0, 0, 32, 32));
        }
        var displayed = held ?? target;
        // Replace the embedded PKMN DATA caption before drawing this thin view's
        // held-state label; never overlay two different words on the same pixels.
        DrawRect(new(4, 1, 72, 13), new Color("b8b8c0"));
        SourceText(_sourceSmall, new(6, 2), held is null ? "MOVE" : "HOLDING");
        DrawStorageSelectedPokemon(displayed);
        string action = _storageMovePartyOpen && _storageMovePartyCursor == 6 ? "CANCEL"
            : held is null ? target is null ? "EMPTY" : PcText("gPCText_Move")
            : target is null ? PcText("gPCText_Place") : PcText("gPCText_Shift");
        SourceText(_sourceSmall, new(6, 132), "ENTER: " + (action is "EMPTY" or "CANCEL" ? action : "MENU"));
        DrawStorageCreatureOverlay();
        DrawStorageCustomizationOverlay();
        if (_pcNotice is not null) ItemMessageBox(_pcNotice);
    }

    // Shared source workspace pixels for creature MOVE and item MOVE.
    private Rect2 DrawStorageWorkspace()
    {
        var storage = _session.State.Storage!;
        DrawTexture(_storageAssets["box-background"], Vector2.Zero);
        DrawStorageWallpaperAndTitle();
        for (int i = 0; i < 30; i++)
            if (storage.Boxes[storage.CurrentBox].Slots[i] is { } mon)
                DrawTexture(PresentedOwnedIcon(mon), new(84 + i % 6 * 24, 28 + i / 6 * 24));
        if (_storageMovePartyOpen)
        {
            DrawTexture(_storageAssets["party-panel"], new(80, 0));
            for (int i = 0; i < 6; i++)
            {
                var rect = StorageMovePartyRect(i);
                DrawTexture(_storageAssets[i < _session.State.Party.Length ? "party-slot-filled" : "party-slot-empty"], rect.Position);
                if (i < _session.State.Party.Length) DrawTexture(PresentedOwnedIcon(_session.State.Party[i]), StorageMovePartyIconPosition(i));
            }
        }
        return _storageMovePartyOpen
            ? _storageMovePartyCursor == 6 ? StorageMovePartyCancel : StorageMovePartyRect(_storageMovePartyCursor)
            : StorageMoveBoxRect(_boxCursor);
    }

    private void DrawStorageSelectedPokemon(PartyPokemon? displayed)
    {
        DrawStorageMarkings(displayed);
        if (displayed is not null)
        {
            if (displayed.IsEgg) DrawTexture(BreedingEggShell(), new(8, 16));
            else if (_battleArt.TryGetValue((PresentedRenderSpecies(displayed.Pokemon), "front", displayed.Pokemon.IsShiny), out var art))
                DrawTexture(art.Texture, new(8, 16));
            SourceText(_sourceSmall, new(6, 88), PresentedOwnedName(displayed));
            if (!displayed.IsEgg)
            {
                SourceText(_sourceSmall, new(6, 102), _catalog.PokemonContent!.SpeciesNames[displayed.Pokemon.SourceSpeciesId]);
                SourceText(_sourceSmall, new(10, 116), $"Lv{displayed.Pokemon.Level}");
            }
        }
    }

}
