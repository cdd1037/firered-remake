using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Pokemon;
using Godot;

// Thin MOVE POKEMON adapter. Only cursor/panel presentation is local: the held
// Pokemon and every grab/place/shift/refusal belong to CampaignSession.
public partial class CampaignView
{
    private bool _storageMovePartyOpen;
    private bool _storageMultiMode;
    private int? _storageRectangleAnchor;
    private bool StorageRectangleActive => _storageRectangleAnchor is not null || _session.State.Pc?.GroupCursor is not null;
    private int _storageMovePartyCursor, _storageMovePreviousPartyCursor = 1;
    private string StorageMoveBusyNotice => _storageText.Get("notice.busy");
    private static readonly Rect2 StorageMovePartyButton = new(80, 0, 88, 16);
    private static readonly Rect2 StorageMoveCloseButton = new(176, 0, 64, 16);
    private static readonly Rect2 StorageMovePartyCancel = new(128, 136, 48, 24);

    private void ResetStorageMovePresentation()
    {
        ResetStorageHandPresentation();
        _storageMovePartyOpen = false;
        _storageMultiMode = false; _storageRectangleAnchor = null;
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
        if (_storageHand.Active) return false;
        var before = _session.State;
        bool accepted = Apply(command);
        if (accepted) { BeginStorageHandPresentation(command, before); return true; }
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
            return _storageText.Get("notice.pokemon-changed");
        if (error.Contains("storage_move_slot_occupied", StringComparison.Ordinal))
            return _session.State.Pc?.GroupCursor is not null
                ? _storageText.Get("notice.group-occupied")
                : _storageText.Get("notice.slot-occupied");
        return StorageError(error);
    }

    private void ToggleStorageMoveParty()
    {
        _storageTitleSelected = false;
        if (StorageRectangleActive) { StorageMoveAction(new("storage_move_hide_party")); return; }
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
        if (_storageHand.Active) return true;
        if (StorageRectangleInput(key)) return true;
        if (!key.Pressed || key.Echo) return true;
        _held = null; if (_pcNotice is not null)
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
        if (_storageHand.Active) return true;
        if (_pcNotice is not null) return MouseConfirm(point, MouseMessage);
        if (StorageCreatureOverlayActive) return MouseStorageCreature(point);
        if (StorageRectangleActive)
        {
            if (new Rect2(88, 32, 144, 120).HasPoint(point))
            {
                int slot = (int)((point.X - 88) / 24) + 6 * (int)((point.Y - 32) / 24);
                if (_session.State.Pc?.GroupCursor is {} group)
                    _boxCursor = Math.Min(slot / 6, 5 - group.Origin.Height) * 6 + Math.Min(slot % 6, 6 - group.Origin.Width);
                else _boxCursor = slot;
                if (_storageRectangleAnchor is not null) { StorageMoveInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }); QueueRedraw(); return true; }
                return MouseKey();
            }
            if (new Rect2(84, 16, 24, 16).HasPoint(point)) return MouseKey(Key.Pageup);
            if (new Rect2(216, 16, 24, 16).HasPoint(point)) return MouseKey(Key.Pagedown);
            return true;
        }
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
        if (_storageHand.Active) return false;
        if (_pcNotice is not null) return false;
        if (StorageCreatureOverlayActive) return NavigateStorageCreaturePointer(point, direction);
        if (StorageRectangleActive) return false;
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
        DrawStorageWorkspace();
        DrawStorageRectangle();
        DrawStorageHand();
        var displayed = held ?? _session.State.Pc?.GroupCursor?.Cells.OfType<PartyPokemon>().FirstOrDefault() ?? target;
        // Replace the embedded PKMN DATA caption before drawing this thin view's
        // held-state label; never overlay two different words on the same pixels.
        DrawRect(new(4, 1, 72, 13), new Color("b8b8c0"));
        SourceText(_sourceSmall, new(6, 2), held is null ? PcText("gPCText_Move") : _storageText.Get("label.holding"));
        DrawStorageSelectedPokemon(displayed);
        var hint = StorageMoveHelpPresentation.SelectSlot(_storageMovePartyOpen, _storageMovePartyCursor,
            held is not null, target is not null);
        SourceText(_sourceSmall, new(6, 132), _storageRectangleAnchor is not null ? _storageText.Get("help.release-enter")
            : StorageRectangleActive ? _storageText.Get("help.enter-place") : _storageMultiMode ? _storageText.Get("help.hold-enter")
            : StorageMoveHelpPresentation.Text(hint, _storageText, PcText("gPCText_Cancel")));
        if (!_storageMovePartyOpen) SourceText(_sourceSmall, new(6, 145), _storageText.Get(_storageMultiMode ? "help.space-multi" : "help.space-single"));
        DrawStorageCreatureOverlay();
        DrawStorageCustomizationOverlay();
        if (_pcNotice is not null) ItemMessageBox(_pcNotice);
    }


    // Space is the desktop Select binding. Enter is A: hold while extending, release to grab.
    private bool StorageRectangleInput(InputEventKey key)
    {
        if (_pcNotice is not null || StorageCreatureOverlayActive || _busy) return false;
        if (_storageRectangleAnchor is {} anchor)
        {
            if (!key.Pressed && key.Keycode == Key.Enter)
            {
                _storageRectangleAnchor = null;
                if (anchor == _boxCursor)
                {
                    if (StorageMoveTarget is {} mon) StorageMoveAction(new("storage_move_grab") {
                        StorageLocation = PokemonStorageMoveLocation.Box(_session.State.Storage!.CurrentBox, anchor), TargetInstanceId = mon.InstanceId });
                    return true;
                }
                var rectangle = new PokemonStorageRectangle(_session.State.Storage!.CurrentBox, anchor, _boxCursor);
                var identities = Enumerable.Range(0, rectangle.Width * rectangle.Height)
                    .Select(i => _session.State.Storage.Boxes[rectangle.BoxIndex].Slots[rectangle.Slot(i)]?.InstanceId).ToImmutableArray();
                if (StorageMoveAction(new("storage_group_grab") { StorageRectangle = rectangle, StorageIdentities = identities }))
                    _boxCursor = rectangle.Top * 6 + rectangle.Left;
                return true;
            }
            if (key.Pressed) MoveRectangleCursor(key.Keycode, 1, 1, false);
            return true;
        }
        if (_session.State.Pc?.GroupCursor is {} group)
        {
            if (!key.Pressed || key.Echo) return true;
            if (key.Keycode == Key.Enter)
                StorageMoveAction(new("storage_group_place") {
                    StorageLocation = PokemonStorageMoveLocation.Box(_session.State.Storage!.CurrentBox, _boxCursor),
                    StorageIdentities = group.Cells.Select(p => p?.InstanceId).ToImmutableArray() });
            else if (key.Keycode is Key.B or Key.Backspace or Key.Tab) StorageMoveAction(new("pc_back"));
            else if (key.Keycode is Key.Pageup or Key.Pagedown)
                StorageMoveAction(new("storage_box", Choice: (_session.State.Storage!.CurrentBox + (key.Keycode == Key.Pagedown ? 1 : 13)) % 14));
            else MoveRectangleCursor(key.Keycode, group.Origin.Width, group.Origin.Height, true);
            return true;
        }
        if (_storageMovePartyOpen || !key.Pressed || key.Echo) return false;
        if (key.Keycode == Key.Space) { _storageMultiMode = !_storageMultiMode; _storageTitleSelected = false; return true; }
        if (_session.State.Pc?.MoveCursor is {} single)
        {
            if (!_storageMultiMode || key.Keycode != Key.Enter || _storageTitleSelected) return false;
            StorageMoveAction(new(StorageMoveTarget is null ? "storage_move_place" : "storage_move_shift") {
                StorageLocation = PokemonStorageMoveLocation.Box(_session.State.Storage!.CurrentBox, _boxCursor),
                HeldInstanceId = single.Pokemon.InstanceId, TargetInstanceId = StorageMoveTarget?.InstanceId });
            return true;
        }
        if (_storageMultiMode && key.Keycode == Key.Enter && !_storageTitleSelected && StorageMoveTarget is not null)
        { _storageRectangleAnchor = _boxCursor; return true; }
        return false;
    }

    private void MoveRectangleCursor(Key key, int width, int height, bool crossBox)
    {
        int x = _boxCursor % 6, y = _boxCursor / 6;
        int dx = key == Key.Left ? -1 : key == Key.Right ? 1 : 0;
        int dy = key == Key.Up ? -1 : key == Key.Down ? 1 : 0;
        if (crossBox && (x + dx < 0 || x + dx + width > 6))
        {
            StorageMoveAction(new("storage_box", Choice: (_session.State.Storage!.CurrentBox + (dx > 0 ? 1 : 13)) % 14));
            return;
        }
        _boxCursor = Math.Clamp(y + dy, 0, 5 - height) * 6 + Math.Clamp(x + dx, 0, 6 - width);
    }

    private void DrawStorageRectangle()
    {
        var group = _session.State.Pc?.GroupCursor;
        var rectangle = _storageRectangleAnchor is {} anchor
            ? new PokemonStorageRectangle(_session.State.Storage!.CurrentBox, anchor, _boxCursor) : group?.Origin;
        if (rectangle is null) return;
        int origin = group is null ? rectangle.Top * 6 + rectangle.Left : _boxCursor;
        var first = StorageMoveBoxRect(origin);
        DrawRect(new(first.Position, new(rectangle.Width * 24, rectangle.Height * 24)), new Color("fff4b8"), filled: false, width: 2);
        if (group is null || _storageHand.Active) return;
        for (int i = 0; i < group.Cells.Length; i++)
            if (group.Cells[i] is {} mon)
                DrawTexture(PresentedOwnedIcon(mon), first.Position + new Vector2(i % rectangle.Width * 24 - 4, i / rectangle.Width * 24 - 12));
    }

    // Shared source workspace pixels for creature MOVE and item MOVE.
    private Rect2 DrawStorageWorkspace()
    {
        var storage = _session.State.Storage!;
        var shownBox = _storageHand.Active ? _storageHandBox : storage.Boxes[storage.CurrentBox].Slots;
        var shownParty = _storageHand.Active ? _storageHandParty : _session.State.Party;
        DrawTexture(_storageAssets["box-background"], Vector2.Zero);
        DrawStorageWallpaperAndTitle();
        DrawStorageHandShadow();
        for (int i = 0; i < 30; i++)
            if (!StorageHandHidesSlot(false, i) && shownBox[i] is { } mon)
                DrawTexture(PresentedOwnedIcon(mon), new(84 + i % 6 * 24, 28 + i / 6 * 24));
        if (_storageMovePartyOpen)
        {
            DrawTexture(_storageAssets["party-panel"], new(80, 0));
            for (int i = 0; i < 6; i++)
            {
                var rect = StorageMovePartyRect(i);
                DrawTexture(_storageAssets[i < shownParty.Length ? "party-slot-filled" : "party-slot-empty"], rect.Position);
                if (i < shownParty.Length && !StorageHandHidesSlot(true, i)) DrawTexture(PresentedOwnedIcon(shownParty[i]), StorageMovePartyIconPosition(i));
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
            SourceScrollingText(_sourceSmall, new(6, 88), PresentedOwnedName(displayed), 72);
            if (!displayed.IsEgg)
            {
                SourceText(_sourceSmall, new(6, 102), (_catalog.SourceSpeciesName(displayed.Pokemon.SourceSpeciesId) ?? throw new InvalidDataException("Pokemon species display name missing.")));
                SourceText(_sourceSmall, new(10, 116), $"Lv{displayed.Pokemon.Level}");
            }
        }
    }

}
