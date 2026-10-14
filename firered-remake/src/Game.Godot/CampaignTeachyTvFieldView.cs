using Game.Application;
using Game.Content;
using Game.Domain.Items;
using Game.Domain.Options;
using Game.Domain.Tutorials;
using Godot;

public partial class CampaignView
{
    private string? _teachyFieldTicket, _teachyFieldWait;
    private double _teachyFieldRemainder;
    private bool _teachyFieldConfirm, _teachyFieldCancel;
    private int _teachyFieldTransition;
    private TeachyTvNormalPaletteClock? _teachyFieldPalette;
    private readonly TeachyTvTextPresentation _teachyFieldText = new();
    private TeachyTvFieldDemoState? TeachyField => _session.State.TeachyTv?.Field?.State;

    private void SynchronizeTeachyTvFieldPresentation()
    {
        if (_session.State.TeachyTv?.Field is not {} owner)
        {
            _teachyFieldTicket = _teachyFieldWait = null; _teachyFieldRemainder = 0;
            _teachyFieldText.Clear(); _teachyFieldPalette = null;
            _teachyFieldConfirm = _teachyFieldCancel = false; return;
        }
        var field = owner.State;
        if (_teachyFieldTicket != field.Ticket)
        {
            _teachyFieldTicket = field.Ticket; _teachyFieldRemainder = 0;
            _teachyFieldTransition = TeachyTvFieldPresentationClock.BagOpenWindowFrames;
            _teachyFieldPalette = TeachyTvFieldPresentationClock.Begin(uint.MaxValue, 0, 16, 0);
        }
        if (_teachyFieldWait == field.WaitId) return;
        _teachyFieldWait = field.WaitId;
        if (field.MessageSymbol is null) _teachyFieldText.Clear();
        foreach (var effect in owner.Effects)
        {
            switch (effect.Kind)
            {
                case "sound": SourceSound(effect.Value!); break;
                case "pocket_switch": _teachyFieldTransition = TeachyTvFieldPresentationClock.BagPocketSwitchFrames; break;
                case "bag_close_window": _teachyFieldTransition = TeachyTvFieldPresentationClock.BagCloseWindowFrames; break;
                case "palette_fade":
                    _teachyFieldPalette = TeachyTvFieldPresentationClock.Begin(effect.PaletteMask, effect.Delay, effect.From, effect.To); break;
                case "open_tm_case":
                    _teachyFieldPalette = TeachyTvFieldPresentationClock.Begin(uint.MaxValue, 0, 16, 0); break;
                case "message":
                    _teachyFieldText.Begin(effect.Value!, _session.TeachyTvCatalog!.Texts[effect.Value!].EncodedText,
                        _session.EffectiveOptions.TextSpeed switch { SourceTextSpeed.Slow => 8, SourceTextSpeed.Mid => 4, _ => 1 }); break;
            }
        }
    }

    private bool ProcessTeachyTvFieldPresentation(double delta)
    {
        if (TeachyField is null) return false;
        _held = null; _repeat = 0;
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        _teachyFieldRemainder += Math.Min(delta, .25) * 60;
        int frames = (int)Math.Min(60, Math.Floor(_teachyFieldRemainder + 1e-9));
        _teachyFieldRemainder -= frames;
        for (int i = 0; i < frames && TeachyField is {} field; i++)
        {
            int input = (_teachyFieldConfirm ? 1 : 0) | (_teachyFieldCancel ? 2 : 0)
                | (_teachyFieldPalette?.Active == true ? 4 : 0) | (!_teachyFieldText.Complete ? 8 : 0)
                | (_teachyFieldTransition > 0 ? 16 : 0);
            Apply(new("teachy_tv_field_tick", Text: field.WaitId, Choice: input));
            _teachyFieldConfirm = _teachyFieldCancel = false;
            if (_lastCommandFault) break;
            // The original palette/text tasks run after the current menu controller.
            if (_teachyFieldTransition > 0) _teachyFieldTransition--;
            if (_teachyFieldPalette is {} palette) _teachyFieldPalette = TeachyTvFieldPresentationClock.Update(palette);
            _teachyFieldText.AdvanceFrame();
        }
        QueueRedraw(); return true;
    }

    private bool TryTeachyTvFieldInput(InputEvent input)
    {
        if (TeachyField is not {} field) return false;
        _held = null; _repeat = 0;
        if (input is InputEventKey key && !key.Echo)
        {
            if (key.Pressed && key.Keycode is Key.Escape or Key.F12) { RequestQuit(); return true; }
            if (key.Keycode is Key.Enter or Key.Z) TeachyTvFieldButton(true, key.Pressed, field);
            if (key.Keycode is Key.Backspace or Key.X) TeachyTvFieldButton(false, key.Pressed, field);
        }
        else if (input is InputEventJoypadButton joy && joy.ButtonIndex is JoyButton.A or JoyButton.B)
            TeachyTvFieldButton(joy.ButtonIndex == JoyButton.A, joy.Pressed, field);
        else if (input is InputEventMouseButton mouse && mouse.ButtonIndex is MouseButton.Left or MouseButton.Right)
            TeachyTvFieldButton(mouse.ButtonIndex == MouseButton.Left, mouse.Pressed, field);
        GetViewport().SetInputAsHandled(); return true;
    }

    private void TeachyTvFieldButton(bool confirm, bool pressed, TeachyTvFieldDemoState field)
    {
        if (!confirm) { if (pressed) _teachyFieldCancel = true; return; }
        if (pressed && field.AwaitingConfirm && _teachyFieldText.Complete) _teachyFieldConfirm = true;
        else _teachyFieldText.Confirm(pressed);
    }

    private bool DrawTeachyTvField()
    {
        if (TeachyField is not {} field) return false;
        var inventory = field.Inventory ?? (field.Screen == TeachyTvFieldScreen.TmCase ? field.Data.TmInventory : field.Data.BagInventory);
        if (field.Screen == TeachyTvFieldScreen.TmCase)
        {
            _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
            DrawMachineList((inventory.Machines ?? []).OrderBy(s => s.Item.Value, StringComparer.Ordinal).ToArray(), field.Cursor, 0, 5);
            if (field.SelectedMachine is {} machine)
                SourceText(_sourceSmall, new(8, 67), "TYPE/" + machine.Move.Type.Replace("TYPE_", "", StringComparison.Ordinal));
            if (field.DescriptionShaded) DrawRect(new(0, 78, 108, 82), new Color(0, 0, 0, 6f / 16));
        }
        else if (field.Pocket == TeachyTvBagPocket.Items) DrawItemList(inventory, "bag", field.Cursor);
        else DrawTeachyTvKeyPocket(field, inventory);
        if (field.ContextOpen)
        {
            string[] actions = field.Kind == TeachyTvDemonstrationKind.RegisterItem
                ? [_itemSkin.Text("gOtherText_Use"), "REGISTER", _itemSkin.Text("gFameCheckerText_Cancel")]
                : [_itemSkin.Text("gOtherText_Use"), _itemSkin.Text("gFameCheckerText_Cancel")];
            DrawItemActionPopup(actions.Length == 3, actions, field.ContextCursor,
                field.SelectedItem is {} id ? _questPocketUi!.Items.Single(item => item.Id == id).Name : null);
        }
        if (field.MessageSymbol is not null && _teachyFieldText.Page is {} page)
        {
            // TM WIN_MESSAGE is the original 26x4 window at tile(2,15),
            // not the narrower Bag selected-item/result box.
            _battleSkin.DrawDialogue(this);
            foreach (var line in page.Lines)
            {
                int count = Math.Clamp(_teachyFieldText.VisibleLength - line.CharacterStart, 0, line.Text.Length);
                float y = LabBattleUiSkin.DialogueText.Y + line.LineIndex * 15 - _teachyFieldText.ScrollPixels;
                if (count > 0 && y >= LabBattleUiSkin.DialogueText.Y && y < 151)
                    SourceText(_sourceNormal, new(LabBattleUiSkin.DialogueText.X, y), line.Text[..count]);
            }
            if (_teachyFieldText.AtPrompt)
            {
                var last = page.Lines[^1];
                float width = _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(last.Text), fontSize: _sourceNormal.Size).X;
                _textCursor.Draw(this, new(16 + (int)width, 121 + last.LineIndex * 15), _teachyFieldText.PromptFrames);
            }
        }
        if (_teachyFieldPalette is { PaletteMask: uint.MaxValue } fade && fade.BackgroundCoefficient > 0)
            DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, fade.BackgroundCoefficient / 16f));
        return true;
    }

    private void DrawTeachyTvKeyPocket(TeachyTvFieldDemoState field, InventoryState inventory)
    {
        // Shared authenticated pocket assets/fonts, with an explicit temporary inventory.
        DrawQuestPocketAsset("bag-key-items-background");
        string title = _questPocketUi!.Texts["gText_KeyItems2"];
        float width = _sourceWhite.Font.GetStringSize(_sourceWhite.PrepareText(title), fontSize: _sourceWhite.Size).X;
        SourceText(_sourceWhite, OpeningItemUiSkin.BagTitleCenter - new Vector2((int)Math.Ceiling(width / 2), 0), title);
        if (!field.ContextOpen)
        { DrawQuestPocketAsset("pocket-left-arrow"); _itemSkin.DrawPocketRightArrow(this); }
        var stacks = inventory.KeyItems ?? [];
        for (int i = 0; i < stacks.Length; i++)
            SourceText(_sourceNormal, OpeningItemUiSkin.ListText(false, i), QuestPocketItem(stacks[i]).Name);
        SourceText(_sourceNormal, OpeningItemUiSkin.ListText(false, stacks.Length), _itemSkin.Text("gFameCheckerText_Cancel"));
        int cursor = Math.Min(field.Cursor, stacks.Length);
        SourceText(_sourceNormal, OpeningItemUiSkin.ListCursor(false, cursor), _itemSkin.Text("gText_SelectorArrow2"));
        if (cursor < stacks.Length)
        {
            var item = QuestPocketItem(stacks[cursor]); DrawQuestPocketAsset(item.Icon);
            ItemLines(_sourceWhite, OpeningItemUiSkin.DescriptionText, item.Description);
        }
        else ItemLines(_sourceWhite, OpeningItemUiSkin.DescriptionText, _itemSkin.Text("gText_CloseBag"));
    }
}
