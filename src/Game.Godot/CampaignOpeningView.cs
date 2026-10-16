using Game.Content;
using Godot;

public partial class CampaignView
{
    private PretOpeningPresentation? _opening;
    private readonly Dictionary<string, ImageTexture> _openingTextures = [];
    private readonly List<(string Text, string Portrait)> _openingPages = [];
    private int _openingPage;
    private OpeningNamingPresentation? _namingGrid;
    private bool _namingGridActive;
    private int _namingPage => _namingGrid!.Page;
    private string _openingNext = "gender";

    private void InitializeOpeningPresentation()
    {
        _opening = PretOpeningPresentation.Load(_root);
        _namingGrid = new(_opening);
        foreach (string name in new[] { "title", "background", "oak", "red", "leaf", "rival", "platform", "nidoran-f" })
        {
            using var image = new Image();
            if (image.LoadPngFromBuffer(_opening.ReadTexture(name)) != Error.Ok) throw new InvalidDataException("Opening texture invalid: " + name);
            _openingTextures.Add(name, ImageTexture.CreateFromImage(image));
        }
    }
    private string OpeningPlayerPortrait => _entryGender == 0 ? "red" : "leaf";
    private void BeginOpeningSpeech(string next, params (string Symbol, string Portrait)[] messages)
    {
        _openingPages.Clear(); _openingPage = 0; _openingNext = next; _entryMode = "oak-speech"; _entryCursor = 0;
        foreach (var (symbol, portrait) in messages)
            foreach (string text in _opening!.Pages(symbol, _entryPlayerName, _entryRivalName)) _openingPages.Add((text, portrait));
        if (_openingPages.Count == 0) throw new InvalidDataException("Opening speech has no pages.");
        QueueRedraw();
    }
    private void BeginOakIntroduction() => BeginOpeningSpeech("gender",
        ("WelcomeToTheWorld", "oak"), ("ThisWorld", "oak"), ("IsInhabitedFarAndWide", "nidoran-f"),
        ("IStudyPokemon", "nidoran-f"), ("TellMeALittleAboutYourself", "oak"));
    private void AdvanceOpeningSpeech()
    {
        if (++_openingPage < _openingPages.Count) return;
        if (_openingNext == "start") StartChosenGame();
        else if (_openingNext is "player-name" or "rival-name") ShowEntryName(_openingNext);
        else { _entryMode = _openingNext; _entryCursor = 0; }
    }
    private void DrawOpeningPortrait(string portrait)
    {
        DrawTexture(_openingTextures["background"], Vector2.Zero);
        DrawTexture(_openingTextures["platform"], new(72, 96));
        DrawTexture(_openingTextures[portrait], portrait == "nidoran-f" ? new(88, 48) : new(88, 16));
    }
    private bool DrawOpeningEntry()
    {
        if (_entryMode == "title") { DrawTexture(_openingTextures["title"], Vector2.Zero); return true; }
        if (_entryMode == "gender")
        {
            DrawOpeningPortrait(_entryCursor == 0 ? "red" : "leaf");
            _battleSkin.DrawStandardFrame(this, new(136, 64, 88, 48));
            SourceText(_sourceNormal, new(152, 72), "BOY"); SourceText(_sourceNormal, new(152, 88), "GIRL");
            SourceText(_sourceNormal, new(138, 72 + _entryCursor * 16), "▶");
            ItemMessageBox(_opening!.Pages("AskPlayerGender", _entryPlayerName, _entryRivalName)[0]); return true;
        }
        if (_entryMode == "oak-speech")
        {
            var page = _openingPages[_openingPage]; DrawOpeningPortrait(page.Portrait); ItemMessageBox(page.Text); return true;
        }
        if (_entryMode is "player-confirm" or "rival-confirm")
        {
            bool player = _entryMode == "player-confirm";
            DrawOpeningPortrait(player ? OpeningPlayerPortrait : "rival");
            ItemMessageBox(_opening!.Pages(player ? "SoYourNameIsPlayer" : "ConfirmRivalName", _entryPlayerName, _entryRivalName)[0]);
            _battleSkin.DrawStandardFrame(this, new(8, 8, 64, 48));
            SourceText(_sourceNormal, new(26, 16), "YES"); SourceText(_sourceNormal, new(26, 32), "NO");
            SourceText(_sourceNormal, new(12, 16 + _entryCursor * 16), "▶"); return true;
        }
        if (_entryMode is "player-name" or "rival-name")
        {
            DrawRect(new(0, 0, 240, 160), Color.Color8(232, 240, 248));
            SourceText(_sourceNormal, new(16, 8), _entryMode == "player-name" ? "YOUR NAME?" : "RIVAL's NAME?");
            _battleSkin.DrawStandardFrame(this, new(8, 64, 224, 88));
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < _opening!.Keyboard[_namingPage][row].Length; col++)
                    SourceText(_sourceNormal, new(18 + col * 20, 72 + row * 18), _opening.Keyboard[_namingPage][row][col].ToString());
            SourceText(_sourceSmall, new(184, 74), _namingPage == 0 ? "abc" : _namingPage == 1 ? "123" : "ABC");
            SourceText(_sourceSmall, new(184, 98), "BACK"); SourceText(_sourceSmall, new(184, 122), "OK");
            if (_namingGridActive)
            {
                var cursor = _namingGrid!;
                DrawRect(cursor.OnAction ? new Rect2(178, 68 + cursor.Row * 24, 44, 21) : new Rect2(14 + cursor.Column * 20, 69 + cursor.Row * 18, 18, 17), Color.Color8(72, 112, 192), false, 1);
            }
            SourceText(_sourceSmall, new(12, 151), "Tab: type/keys  Enter: OK  Esc: back");
            if (_entryNotice is not null) ItemMessageBox(_entryNotice);
            return true;
        }
        return false;
    }
    private void ApplyOpeningNameKey(OpeningNameKey key)
    {
        var edit = _entryNameEdit!;
        if (key.Action is OpeningNameAction.Insert or OpeningNameAction.Delete)
        {
            bool selected = edit.HasSelection();
            if (selected) { int from = edit.GetSelectionFromColumn(); edit.DeleteText(from, edit.GetSelectionToColumn()); edit.CaretColumn = from; }
            if (key.Action == OpeningNameAction.Insert) edit.InsertTextAtCaret(key.Character.ToString());
            else if (!selected && edit.CaretColumn > 0) { int caret = edit.CaretColumn; edit.DeleteText(caret - 1, caret); edit.CaretColumn = caret - 1; }
        }
        else if (key.Action == OpeningNameAction.Submit) SubmitPlayerEntryName(edit.Text);
        QueueRedraw();
    }
    private bool TryOpeningNameGuiInput(InputEvent input)
    {
        if (_entryMode is not ("player-name" or "rival-name")) return false;
        if (input is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Tab) { _namingGridActive = !_namingGridActive; QueueRedraw(); return true; }
            if (!_namingGridActive) return false;
            switch (key.Keycode)
            {
                case Key.Left: _namingGrid!.Move(-1, 0); break;
                case Key.Right: _namingGrid!.Move(1, 0); break;
                case Key.Up: _namingGrid!.Move(0, -1); break;
                case Key.Down: _namingGrid!.Move(0, 1); break;
                case Key.Enter: case Key.KpEnter: ApplyOpeningNameKey(_namingGrid!.Activate()); break;
                case Key.Backspace: ApplyOpeningNameKey(new(OpeningNameAction.Delete)); break;
                default: if (key.Unicode > 0) _namingGridActive = false; return false;
            }
            QueueRedraw(); return true;
        }
        if (input is InputEventJoypadButton joy)
        {
            if (!joy.Pressed) return true;
            _namingGridActive = true;
            switch (joy.ButtonIndex)
            {
                case JoyButton.DpadLeft: _namingGrid!.Move(-1, 0); break;
                case JoyButton.DpadRight: _namingGrid!.Move(1, 0); break;
                case JoyButton.DpadUp: _namingGrid!.Move(0, -1); break;
                case JoyButton.DpadDown: _namingGrid!.Move(0, 1); break;
                case JoyButton.A: ApplyOpeningNameKey(_namingGrid!.Activate()); break;
                case JoyButton.B: ApplyOpeningNameKey(new(OpeningNameAction.Delete)); break;
                case JoyButton.Start: _namingGrid!.SelectSubmit(); break;
                default: return false;
            }
            QueueRedraw(); return true;
        }
        return false;
    }
    private bool OpeningNamePointer(Vector2 point)
    {
        if (_entryMode is not ("player-name" or "rival-name")) return false;
        if (new Rect2(176, 68, 48, 72).HasPoint(point))
            _namingGrid!.Select((int)((point.Y - 68) / 24), _namingGrid.Columns);
        else if (new Rect2(12, 68, 160, 72).HasPoint(point))
        {
            int col = (int)((point.X - 12) / 20), row = (int)((point.Y - 68) / 18);
            if (col >= _namingGrid!.Columns) return true;
            _namingGrid.Select(row, col);
        }
        else return false;
        _namingGridActive = false; ApplyOpeningNameKey(_namingGrid!.Activate());
        if (_entryMode is "player-name" or "rival-name") { _entryNameEdit!.GrabFocus(); _entryNameEdit.Edit(); }
        QueueRedraw(); return true;
    }
    private void DisposeOpeningPresentation()
    { foreach (var texture in _openingTextures.Values) texture.Dispose(); _openingTextures.Clear(); }
}
