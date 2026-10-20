using Game.Application;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private string? _noncombatWait;
    private double _noncombatTicks;
    private int _noncombatFrame;
    private bool _noncombatCry;
    private FieldReturnTimeline? _fieldReturn;
    private SweetScentTimeline? _sweetScent;
    private int _fieldHealingSource = -1, _fieldHealingTarget = -1, _fieldHealingBeforeSource, _fieldHealingBeforeTarget;
    private int _fieldMoveConfirmChoice, _fieldHealingDonated, _fieldHealingRestored;
    private long _fieldHealingSourceIdentity;
    private double _fieldHealingFrames;
    private string? _fieldHealingMove;
    private bool _fieldHealingSecondSound;
    private Vector2 _noncombatPlayerCamera;
    private Pose? _noncombatPlayerPose;
    private bool NoncombatOutdoors => _catalog.MapDefinition(_session.State.World.Player.MapId).MapType is
        "MAP_TYPE_ROUTE" or "MAP_TYPE_TOWN" or "MAP_TYPE_UNDERWATER" or "MAP_TYPE_CITY" or "MAP_TYPE_OCEAN_ROUTE";
    private int? NoncombatShowMonFrame => _session.State.NoncombatFieldEffect?.Phase == "show-mon" ? _noncombatFrame : null;
    private int NoncombatOutdoorParty => NoncombatShowMonFrame is not null && NoncombatOutdoors ? _session.State.NoncombatFieldEffect!.PartyIndex : -1;

    private void ResetFieldHealingSelection()
    {
        _fieldHealingSource = _fieldHealingTarget = -1; _fieldHealingMove = null;
        _fieldHealingFrames = 0; _fieldHealingSecondSound = false;
    }
    private bool TryNoncombatFieldAction(string move)
    {
        if (!NoncombatFieldMoveRules.IsSupported(move)) return false;
        var mon = _session.State.Party[_fieldPartyCursor];
        if (!NoncombatFieldMoveRules.Knows(mon, move)) return false;
        if (NoncombatFieldMoveRules.IsHealing(move))
        {
            if (!_session.CanUseNoncombatFieldMove(_fieldPartyCursor, move))
            { _fieldMenuMode = "party"; _fieldMenuNotice = SourceMenuLabelPresentation.Plain(_battleText, "gText_NotEnoughHp"); return true; }
            _fieldHealingSource = _fieldPartyCursor; _fieldHealingSourceIdentity = mon.InstanceId;
            _fieldHealingMove = move; _fieldMenuMode = "field-heal"; return true;
        }
        if (!Apply(new("field_move", Text: move, PartyIndex: _fieldPartyCursor) { TargetInstanceId = mon.InstanceId }))
        { _fieldMenuMode = "party"; _fieldMenuNotice = FieldMenuText.Get("notice.cannot-use-here"); return true; }
        if (_session.State.NoncombatFieldEffect!.Phase == "confirm")
        { _fieldMoveConfirmChoice = 0; _fieldMenuMode = "field-move-confirm"; }
        else ResetFieldMenu();
        return true;
    }
    private bool TryFieldHealingInput(InputEventKey key)
    {
        if (_fieldMenuMode == "field-move-confirm")
        {
            FieldMenuMoveCursor(key, ref _fieldMoveConfirmChoice, 2);
            if (key.Keycode is Key.Enter or Key.Backspace)
            {
                int choice = key.Keycode == Key.Backspace ? 1 : _fieldMoveConfirmChoice;
                if (Apply(new("field_move_confirm", Choice: choice)))
                { if (choice == 0) ResetFieldMenu(); else _fieldMenuMode = "party"; }
            }
            return true;
        }
        if (_fieldMenuMode == "field-heal-animation") return true;
        if (_fieldMenuMode != "field-heal") return false;
        if (key.Keycode == Key.Backspace)
        { _fieldMenuMode = "party"; ResetFieldHealingSelection(); return true; }
        NativePartyMove(key.Keycode, ref _fieldPartyCursor, _session.State.Party.Length);
        if (key.Keycode != Key.Enter) return true;
        if (_fieldPartyCursor == _session.State.Party.Length)
        { _fieldPartyCursor = _fieldHealingSource; _fieldMenuMode = "party"; ResetFieldHealingSelection(); return true; }
        if (_fieldHealingSource < 0 || _fieldHealingSource >= _session.State.Party.Length ||
            _session.State.Party[_fieldHealingSource].InstanceId != _fieldHealingSourceIdentity)
        { _fieldMenuMode = "party"; ResetFieldHealingSelection(); return true; }
        var donor = _session.State.Party[_fieldHealingSource]; var target = _session.State.Party[_fieldPartyCursor];
        _fieldHealingBeforeSource = donor.Pokemon.CurrentHp; _fieldHealingBeforeTarget = target.Pokemon.CurrentHp;
        if (!Apply(new("field_heal", Text: _fieldHealingMove, PartyIndex: _fieldHealingSource, Choice: _fieldPartyCursor)
            { TargetInstanceId = target.InstanceId }))
        { _fieldMenuNotice = string.Join('\n', SourceBattleText.Pages(_battleText, "gText_CantBeUsedOnPkmn", new Dictionary<string, string>(StringComparer.Ordinal))); return true; }
        _fieldHealingTarget = _fieldPartyCursor;
        _fieldHealingDonated = _fieldHealingBeforeSource - _session.State.Party[_fieldHealingSource].Pokemon.CurrentHp;
        _fieldHealingRestored = _session.State.Party[_fieldHealingTarget].Pokemon.CurrentHp - _fieldHealingBeforeTarget;
        _fieldHealingFrames = 0; _fieldHealingSecondSound = false; _fieldMenuMode = "field-heal-animation";
        SourceSound("SE_USE_ITEM"); return true;
    }
    private void ProcessFieldHealingPresentation(double delta)
    {
        if (_fieldMenuMode != "field-heal-animation") return;
        _fieldHealingFrames += delta * 60;
        if (!_fieldHealingSecondSound && _fieldHealingFrames >= _fieldHealingDonated)
        { _fieldHealingSecondSound = true; SourceSound("SE_USE_ITEM"); }
        if (_fieldHealingFrames < _fieldHealingDonated + _fieldHealingRestored) return;
        string name = _catalog.PokemonDisplayName(_session.State.Party[_fieldHealingTarget]);
        _fieldMenuNotice = string.Join('\n', SourceBattleText.Pages(_battleText, "gText_PkmnHPRestoredByVar2",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["STR_VAR_1"] = name, ["STR_VAR_2"] = _fieldHealingRestored.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        _fieldMenuMode = "party"; ResetFieldHealingSelection();
    }
    private PartyPokemon DisplayedFieldHealingMember(int index)
    {
        var mon = _session.State.Party[index];
        if (_fieldMenuMode != "field-heal-animation") return mon;
        int frame = (int)_fieldHealingFrames;
        int hp = index == _fieldHealingSource ? _fieldHealingBeforeSource - Math.Min(frame, _fieldHealingDonated) :
            index == _fieldHealingTarget ? _fieldHealingBeforeTarget + Math.Clamp(frame - _fieldHealingDonated, 0, _fieldHealingRestored) : mon.Pokemon.CurrentHp;
        return mon with { Pokemon = mon.Pokemon with { CurrentHp = hp } };
    }
    private void DrawNoncombatPartyPrompt()
    {
        if (_fieldMenuMode != "field-move-confirm" || _session.State.NoncombatFieldEffect is not { Destination: { } destination } effect) return;
        string section = _catalog.MapDefinition(destination.MapId).RegionMapSection;
        string name = _catalog.FlyMapPresentation.Regions.SelectMany(region => region.Cells).FirstOrDefault(cell => cell.SectionId == section)?.Name ??
            throw new InvalidDataException("field_move_destination_name_missing");
        DrawNativePartyMessage(string.Join('\n', SourceBattleText.Pages(_battleText,
            effect.Move == NoncombatFieldMoveRules.Dig ? "gText_EscapeFromHereAndReturnTo" : "gText_ReturnToHealingSpot",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["STR_VAR_1"] = name })));
        _battleSkin.DrawStandardFrame(this, new(176, 64, 64, 48));
        SourceText(_sourceNormal, new(192, 72), InventorySourceText("gText_Yes")); SourceText(_sourceNormal, new(192, 88), InventorySourceText("gText_No"));
        SourceText(_sourceNormal, new(182, 72 + _fieldMoveConfirmChoice * 16), "▶");
    }
    private bool TryNoncombatFieldInput(InputEvent input)
    {
        if (_session is null) return false;
        if (input is InputEventKey { Keycode: Key.Escape or Key.F12 }) return false;
        if (_fieldMenuMode == "field-heal-animation")
        { GetViewport().SetInputAsHandled(); return true; }
        if (_session.State.NoncombatFieldEffect is not { } effect) return false;
        if (effect.Phase == "confirm")
        {
            // Keep the authoritative confirmation open across Start/save shortcuts.
            if (input is not InputEventKey { Keycode: Key.Tab or Key.F5 or Key.F9 }) return false;
        }
        _held = null; GetViewport().SetInputAsHandled(); return true;
    }
    private void SynchronizeNoncombatFieldPresentation(CampaignState state)
    {
        if (state.NoncombatFieldEffect is not { Phase: not "confirm" } effect) return;
        if (_noncombatWait == effect.WaitId) return;
        _noncombatWait = effect.WaitId; _noncombatTicks = 0; _noncombatFrame = 0; _noncombatCry = false; _fade = 0;
        _fieldReturn = effect.Phase is "out" or "in" ? new(effect.Move, effect.Phase == "out", state.World.Player.Facing) : null;
        _sweetScent = effect.Phase is "scent" or "fail" ? new(effect.Phase == "fail") : null;
        if (effect.Phase == "scent") SourceSound("SE_M_SWEET_SCENT");
    }
    private bool ProcessNoncombatFieldPresentation(double delta)
    {
        if (_session.State.NoncombatFieldEffect is not { Phase: not "confirm" } effect) return false;
        SynchronizeNoncombatFieldPresentation(_session.State);
        _held = null; _noncombatTicks += delta * 60;
        int duration = NoncombatOutdoors ? FieldCutSourcePresentation.ShowMonCueFrames : FieldFlashSourcePresentation.ShowMonCueFrames;
        while (_noncombatTicks + 1e-7 >= 1)
        {
            _noncombatTicks -= 1; _noncombatFrame++;
            _fieldReturn?.Tick(); _sweetScent?.Tick();
            if (_fieldReturn?.Sound is { } sound) SourceSound(sound);
            if (effect.Phase == "show-mon" && !_noncombatCry && _noncombatFrame >= 24 + (NoncombatOutdoors ?
                FieldCutSourcePresentation.OutdoorBannerOpenFrames : FieldFlashSourcePresentation.IndoorBannerOpenFrames) + 10)
            { _noncombatCry = true; SourceCry(_session.State.Party[effect.PartyIndex].Pokemon); }
            if (effect.Phase == "show-mon" ? _noncombatFrame >= duration : (_fieldReturn?.Complete ?? _sweetScent?.Complete ?? false))
            { if (!_busy) Apply(new("field_move_ack", Text: effect.WaitId)); break; }
        }
        return true;
    }
    private bool DrawNoncombatFieldActor(string id, SpriteAsset asset, Pose pose, Vector2 camera)
    {
        if (id != "LOCALID_PLAYER" || _session.State.NoncombatFieldEffect is not { Phase: not "confirm" } effect) return false;
        _noncombatPlayerCamera = camera; _noncombatPlayerPose = pose;
        if (effect.Phase == "show-mon")
        {
            int frame = Math.Min(4, _noncombatFrame / 4);
            DrawTextureRectRegion(_session.State.PlayerGender == 0 ? _fieldCutRed! : _fieldCutGreen!,
                new(pose.Position.Floor() + new Vector2(8,8) - asset.Anchor - camera, new(16,32)), new(frame * 16,0,16,32));
            return true;
        }
        if (_fieldReturn is { } travel && effect.Phase is "out" or "in")
        {
            DrawActor(asset, new Pose { Position = pose.Position + new Vector2(0, travel.OffsetY), Facing = travel.Facing, Visible = true }, camera);
            return true;
        }
        return false;
    }
    private void DrawNoncombatFieldOverlay()
    {
        if (_session.State.NoncombatFieldEffect is not { Phase: not "confirm" } effect) return;
        if (effect.Phase == "show-mon" && !NoncombatOutdoors) DrawFieldIndoorShowMonBanner(_noncombatFrame, effect.PartyIndex);
        if (_sweetScent is { } scent && effect.Phase is "scent" or "fail")
        {
            DrawRect(new(0,0,240,160), new Color(1,0,0,scent.BackgroundCoefficient / 16f));
            // The source excludes the player's OBJ palette from the red fade.
            if (_noncombatPlayerPose is { } pose)
            {
                if (_session.State.IsSurfing) DrawFieldSurfActor("LOCALID_PLAYER", pose, _noncombatPlayerCamera);
                else DrawActor(CurrentPlayerSprite, pose, _noncombatPlayerCamera);
            }
        }
        if (_fieldReturn is { Fade: > 0 } travel && effect.Phase is "out" or "in")
            DrawRect(new(0,0,240,160), new Color(0,0,0,travel.Fade / 16f));
    }
}
