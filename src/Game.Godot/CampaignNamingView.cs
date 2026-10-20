using System.Text;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.World;
using Godot;

/// <summary>One source naming screen and one native editor. Adapters select the
/// current target; the existing Application commands remain the only writers.</summary>
public partial class CampaignView
{
    private sealed record NamingScreenBinding(NamingScreenContext Context, PokemonInstance? Pokemon = null);
    private NamingScreenPresentation? _namingScreen;
    private NamingScreenBinding? _namingBinding;
    private bool _namingGridActive;
    private bool _namingGlyphUnavailable;
    private double _namingFrameRemainder;
    private ulong _namingSubmitFrame = ulong.MaxValue, _namingClosedFrame = ulong.MaxValue;
    private SourceGameFont? _namingKeyboardFont, _namingGuideFont;
    private SourceNameEntryLayout _namingEntryLayout;
    private bool NamingScreenActive => _namingBinding is not null;

    private void EnsureNamingPresentation()
    {
        if (_namingScreen is not null) return;
        _opening ??= PretOpeningPresentation.Load(_root, battleText: _battleText);
        _namingScreen = new(_opening);
        string fonts = Path.Combine(_root, "content/generated/ui/font");
        _namingKeyboardFont = SourceGameFont.Load(fonts, foreground: Colors.White, shadow: Color.Color8(98, 98, 98));
        _namingGuideFont = SourceGameFont.Load(fonts, foreground: Colors.White, shadow: Color.Color8(74, 82, 107));
        foreach (string name in PretOpeningPresentation.TextureNames.Where(name => name.StartsWith("naming-", StringComparison.Ordinal)))
        {
            using var image = new Image();
            if (image.LoadPngFromBuffer(_opening.ReadTexture(name)) != Error.Ok) throw new InvalidDataException("Naming texture invalid: " + name);
            _openingTextures.Add(name, ImageTexture.CreateFromImage(image));
        }
    }

    private void InitializeNamingEditor()
    {
        _nicknameEdit = new LineEdit { Visible = false, PlaceholderText = "", CaretBlink = true, KeepEditingOnTextSubmit = true,
            CaretMidGrapheme = false, BackspaceDeletesCompositeCharacterEnabled = true };
        _nicknameEdit.AddThemeFontOverride("font", _sourceNormal.Font);
        _nicknameEdit.AddThemeFontSizeOverride("font_size", _sourceNormal.Size);
        var style = new StyleBoxFlat { BgColor = Colors.Transparent, ContentMarginLeft = 4, ContentMarginRight = 0, ContentMarginTop = 0, ContentMarginBottom = 0 };
        // Both styles contribute to LineEdit's minimum size. Keep its full native
        // selection/IME surface inside the source name row, away from the keys.
        _nicknameEdit.AddThemeStyleboxOverride("normal", style);
        _nicknameEdit.AddThemeStyleboxOverride("read_only", style);
        _nicknameEdit.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        _nicknameEdit.AddThemeColorOverride("font_color", Colors.White);
        _nicknameEdit.AddThemeColorOverride("font_selected_color", Colors.White);
        _nicknameEdit.AddThemeColorOverride("caret_color", Color.Color8(66, 66, 66));
        _nicknameEdit.AddThemeColorOverride("selection_color", Color.Color8(173, 206, 247));
        _nicknameEdit.AddThemeConstantOverride("outline_size", 0);
        _nicknameEdit.TextChanged += text => { NamingGlyphPreflight(text); UpdateNamingEditorLayout(); };
        _nicknameEdit.TextSubmitted += SubmitNamingScreen;
        _nicknameEdit.GuiInput += NamingEditorGuiInput;
        AddChild(_nicknameEdit);
    }

    private NamingScreenBinding? CurrentNamingBinding()
    {
        if (PlayerEntryActive)
            return _entryMode switch
            {
                "player-name" => new(new(NamingScreenFlow.Player, "player", _entryPlayerName, PlayerGender: _entryGender)),
                "rival-name" => new(new(NamingScreenFlow.Rival, "rival", _entryRivalName)),
                _ => null
            };
        var state = _session.State;
        if (StorageBoxNaming)
        {
            int box = state.Pc!.Customization!.BoxIndex;
            return new(new(NamingScreenFlow.Box, "box:" + box, StorageBoxName(box)));
        }
        if (CaptureNamingActive)
        {
            var capture = state.SafariBattle?.Capture ?? state.WildBattle!.Capture!;
            return MonNamingBinding(NamingScreenFlow.Capture, "capture:" + (state.SafariBattle?.Id ?? state.WildBattle!.Id), (_catalog.SourceSpeciesName(capture.Pokemon.SourceSpeciesId) ?? throw new InvalidDataException("Pokemon species display name missing.")), capture.Pokemon);
        }
        if (state.Nickname is not { Submitted: false } naming || state.Script.Wait?.Kind != StoryWaitKind.Nickname) return null;
        PartyPokemon mon;
        NamingScreenFlow flow;
        if (GiftNamingActive) { mon = GiftNicknameTarget(); flow = NamingScreenFlow.Gift; }
        else
        {
            if (naming.PartyIndex < 0 || naming.PartyIndex >= state.Party.Length) throw new InvalidDataException("Naming party target differs.");
            mon = state.Party[naming.PartyIndex];
            flow = NameRaterNaming ? NamingScreenFlow.NameRater : EggHatchNaming ? NamingScreenFlow.Hatch : NamingScreenFlow.PartyNickname;
            if (NameRaterNaming)
            {
                var target = state.NameRater!.Target!;
                string? error = NameRaterRules.ValidateSelection(state.Party, target.PartyIndex, target.InstanceId);
                if (error is not null || target.PartyIndex != naming.PartyIndex) throw new InvalidDataException(error ?? "Naming target differs.");
            }
            if (EggHatchNaming && (state.EggHatch!.InstanceId != mon.InstanceId || mon.IsEgg || state.EggHatch.SourceSpeciesId != mon.Pokemon.SourceSpeciesId))
                throw new InvalidDataException("Hatch naming identity differs.");
        }
        return MonNamingBinding(flow, "owned:" + mon.InstanceId, _catalog.PokemonDisplayName(mon), mon.Pokemon);
    }

    private NamingScreenBinding MonNamingBinding(NamingScreenFlow flow, string identity, string originalName, PokemonInstance mon)
        => new(new(flow, identity, originalName, (_catalog.SourceSpeciesName(mon.SourceSpeciesId) ?? throw new InvalidDataException("Pokemon species display name missing.")), mon.Gender), mon);

    private void OpenNamingScreen(NamingScreenBinding binding)
    {
        EnsureNamingPresentation();
        _namingBinding = binding;
        if (!_namingScreen!.Open(binding.Context)) return;
        _namingGridActive = false; _namingFrameRemainder = 0;
        _held = null; _repeat = 0; ReleaseFieldTextInput();
        // Native IME/editor columns count scalars. Domain rules own the 7/8/10 text-element limits.
        _nicknameEdit.MaxLength = NameTextRules.MaximumScalarCount;
        _sourceNormal.EnsureGlyphs(binding.Context.InitialText);
        _nicknameEdit.Text = binding.Context.InitialText;
        UpdateNamingEditorLayout();
        NamingGlyphPreflight(_nicknameEdit.Text);
        _nicknameEdit.Visible = !_help.Active;
        if (!_help.Active) { _nicknameEdit.GrabFocus(); _nicknameEdit.Edit(); }
        if (binding.Context.IsOpening) _nicknameEdit.SelectAll();
    }

    private void CloseNamingScreen()
    {
        if (_namingBinding is not null) _namingClosedFrame = Engine.GetProcessFrames();
        _namingBinding = null; _namingScreen?.Close(); _namingGridActive = false; _namingFrameRemainder = 0;
        _namingGlyphUnavailable = false;
        _nicknameEdit.Visible = false; _nicknameEdit.ReleaseFocus();
        _held = null; _repeat = 0; ReleaseFieldTextInput();
    }

    private void SynchronizeNamingScreen(CampaignState? before, CampaignState after)
    {
        var binding = CurrentNamingBinding();
        if (binding is not null) OpenNamingScreen(binding);
        else if (_namingBinding is not null || _nicknameEdit.Visible)
        {
            CloseNamingScreen();
            if (after.Nickname?.Submitted == true || before?.WildBattle?.Capture?.Stage == CampaignCaptureStage.Naming || before?.SafariBattle?.Capture?.Stage == CampaignCaptureStage.Naming)
                _fade = 16.0 / 60;
        }
    }

    private void ProcessNamingPresentation(double delta)
    {
        if (!NamingScreenActive) return;
        UpdateNamingEditorLayout();
        _namingFrameRemainder += delta * 60;
        int frames = (int)_namingFrameRemainder; _namingFrameRemainder -= frames;
        _namingScreen!.AdvanceFrames(frames);
    }

    private bool NamingGlyphPreflight(string text)
    {
        _namingGlyphUnavailable = !SourceNameGlyphs.CanRender(text,
            codepoint => _sourceNormal.Supports(codepoint) && _sourceSmall.Supports(codepoint));
        QueueRedraw();
        return !_namingGlyphUnavailable;
    }

    private void UpdateNamingEditorLayout()
    {
        if (!NamingScreenActive) return;
        var grid = _namingScreen!; var layout = _opening!.NamingLayout;
        // The native editor owns selection, caret, composition and overflow scrolling.
        // Preflight its direct FontFile use, including the platform's exposed IME preedit.
        string composition = _nicknameEdit.HasFocus() && OS.GetName() == "macOS" ? DisplayServer.ImeGetText() : "";
        _sourceNormal.EnsureGlyphs(_nicknameEdit.Text + composition);
        float measured = _sourceNormal.MeasureText(_nicknameEdit.Text).X + _sourceNormal.MeasureText(composition).X;
        bool showGender = grid.Template.AddGenderIcon && _namingBinding!.Context.Gender != PokemonGender.Genderless;
        bool female = _namingBinding!.Context.Gender == PokemonGender.Female;
        float genderWidth = showGender ? (female ? _healthFemale : _healthMale).MeasureText(female ? "♀" : "♂").X : 0;
        _namingEntryLayout = SourceCompactTextLayout.NameEntry(measured, grid.MaxChars * layout.EntryCellWidth,
            grid.EntryX, layout.GenderX, genderWidth);
        _nicknameEdit.Scale = new(_namingEntryLayout.Scale, 1);
        _nicknameEdit.Position = new(grid.EntryX - 4 * _namingEntryLayout.Scale, layout.EntryY - 2);
        _nicknameEdit.Size = new(_namingEntryLayout.SourceWidth + 8, 18);
    }

    private void SubmitNamingScreen(string text)
    {
        if (!NamingScreenActive || _busy || _namingSubmitFrame == Engine.GetProcessFrames()) return;
        _namingSubmitFrame = Engine.GetProcessFrames();
        if (_namingBinding!.Context.IsOpening) SubmitPlayerEntryName(text);
        else SubmitCampaignNickname(text);
    }

    private void NamingEscape()
    {
        switch (_namingBinding!.Context.EscapeBehavior)
        {
            case NamingEscapeBehavior.ReturnToEntry: CancelEntryName(); break;
            case NamingEscapeBehavior.KeepOriginal: SubmitNamingScreen(""); break;
            case NamingEscapeBehavior.HostQuit: RequestQuit(); break;
        }
    }

    private void ApplyNamingKey(NamingScreenKey key)
    {
        if (key.Action == NamingScreenAction.None || !NamingScreenActive || _busy) return;
        if (key.Action == NamingScreenAction.ChangePage) SourceSound("SE_WIN_OPEN");
        if (key.Action is NamingScreenAction.Insert or NamingScreenAction.Delete)
        {
            bool selected = _nicknameEdit.HasSelection();
            if (selected)
            {
                int from = _nicknameEdit.GetSelectionFromColumn();
                _nicknameEdit.DeleteText(from, _nicknameEdit.GetSelectionToColumn()); _nicknameEdit.CaretColumn = from;
            }
            if (key.Action == NamingScreenAction.Insert)
            {
                _sourceNormal.EnsureGlyphs(key.Character.ToString());
                _nicknameEdit.InsertTextAtCaret(key.Character.ToString());
            }
            else if (!selected && _nicknameEdit.CaretColumn > 0)
            {
                int caret = _nicknameEdit.CaretColumn;
                int previous = _nicknameEdit.GetPreviousCompositeCharacterColumn(caret);
                _nicknameEdit.DeleteText(previous, caret); _nicknameEdit.CaretColumn = previous;
            }
        }
        else if (key.Action == NamingScreenAction.Submit) SubmitNamingScreen(_nicknameEdit.Text);
        QueueRedraw();
    }

    private bool NamingGridKey(InputEventKey key)
    {
        if (!key.Pressed) return false;
        if (key.Keycode == Key.Tab)
        { if (!key.Echo) _namingGridActive = !_namingGridActive; QueueRedraw(); return true; }
        if (key.Keycode == Key.Escape)
        { if (!key.Echo) NamingEscape(); return true; }
        if (!_namingGridActive) return false;
        if (key.Keycode is Key.F5 or Key.F9 or Key.F12) return false;
        if (key.Unicode > 0 && key.Keycode is not (Key.Enter or Key.KpEnter or Key.Backspace))
        { _namingGridActive = false; QueueRedraw(); return false; }
        if (key.Echo || _namingScreen!.Swapping) return true;
        switch (key.Keycode)
        {
            case Key.Left: _namingScreen.Move(-1, 0); break;
            case Key.Right: _namingScreen.Move(1, 0); break;
            case Key.Up: _namingScreen.Move(0, -1); break;
            case Key.Down: _namingScreen.Move(0, 1); break;
            case Key.Enter: case Key.KpEnter: ApplyNamingKey(_namingScreen.Activate()); break;
            case Key.Backspace: ApplyNamingKey(new(NamingScreenAction.Delete)); break;
            default: return false;
        }
        QueueRedraw(); return true;
    }

    private void NamingEditorGuiInput(InputEvent input)
    {
        if (!NamingScreenActive || _help.Active) return;
        if (input is InputEventKey { Unicode: > 0 } typed)
            _sourceNormal.EnsureGlyphs(char.ConvertFromUtf32((int)typed.Unicode));
        if (input is InputEventKey key && NamingGridKey(key)) { _nicknameEdit.AcceptEvent(); return; }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right } mouse)
        {
            // GUI pointers are editor-local. Use the same global-transform and
            // press/release guards as a click outside the editor.
            using var pointer = (InputEventMouseButton)mouse.Duplicate();
            pointer.Position = _nicknameEdit.GetGlobalTransformWithCanvas() * mouse.Position;
            TryMenuMouseInput(pointer); _nicknameEdit.AcceptEvent();
        }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        { _namingGridActive = false; QueueRedraw(); }
    }

    // Joypad keys must reach naming before the normal field-button conversion.
    private bool TryNamingJoypadInput(InputEvent input)
    {
        if (!NamingScreenActive || _help.Active || input is not InputEventJoypadButton joy ||
            joy.ButtonIndex is JoyButton.LeftShoulder or JoyButton.RightShoulder) return false;
        if (_helpClosedFrame == Engine.GetProcessFrames()) { GetViewport().SetInputAsHandled(); return true; }
        if (joy.Pressed && !_busy)
        {
            _namingGridActive = true;
            if (!_namingScreen!.Swapping)
                switch (joy.ButtonIndex)
                {
                    case JoyButton.DpadLeft: _namingScreen.Move(-1, 0); break;
                    case JoyButton.DpadRight: _namingScreen.Move(1, 0); break;
                    case JoyButton.DpadUp: _namingScreen.Move(0, -1); break;
                    case JoyButton.DpadDown: _namingScreen.Move(0, 1); break;
                    case JoyButton.A: ApplyNamingKey(_namingScreen.Activate()); break;
                    case JoyButton.B: ApplyNamingKey(new(NamingScreenAction.Delete)); break;
                    case JoyButton.Start: _namingScreen.SelectSubmit(); break;
                }
        }
        QueueRedraw(); GetViewport().SetInputAsHandled(); return true;
    }

    private bool TryNamingUnhandledInput(InputEvent input)
    {
        if (_help.Active) return false;
        if (_namingClosedFrame == Engine.GetProcessFrames() && input is not InputEventKey { Keycode: Key.F5 or Key.F9 or Key.F12 })
        { GetViewport().SetInputAsHandled(); return true; }
        if (!NamingScreenActive) return false;
        if (input is InputEventMouseButton or InputEventMouseMotion) return TryMenuMouseInput(input);
        if (input is InputEventKey key)
        {
            if (key.Keycode is Key.F5 or Key.F9 or Key.F12) return false;
            if (!_busy && key.Pressed)
            {
                if (!NamingGridKey(key) && !key.Echo && key.Keycode is Key.Enter or Key.KpEnter) SubmitNamingScreen(_nicknameEdit.Text);
                if (NamingScreenActive && !_nicknameEdit.HasFocus()) { _nicknameEdit.GrabFocus(); _nicknameEdit.Edit(); }
            }
        }
        _held = null; _repeat = 0; GetViewport().SetInputAsHandled(); return true;
    }

    private bool NamingPointer(Vector2 point)
    {
        if (!NamingScreenActive || !_namingScreen!.SelectPoint(point.X, point.Y)) return false;
        _namingGridActive = false; ApplyNamingKey(_namingScreen.Activate());
        if (NamingScreenActive) { _nicknameEdit.GrabFocus(); _nicknameEdit.Edit(); }
        QueueRedraw(); return true;
    }

    private bool DrawNamingScreen()
    {
        if (!NamingScreenActive) return false;
        var grid = _namingScreen!; var template = grid.Template; var layout = _opening!.NamingLayout;
        DrawRect(new(0, 0, 240, 160), Colors.White);
        DrawTexture(_openingTextures["naming-background"], Vector2.Zero);
        SourceFittedText(_sourceNormal, new(layout.TitleX, layout.TitleY), grid.Title, 200 - layout.TitleX);
        DrawNamingTargetIcon(template, grid.VisualFrame);
        if (template.AddGenderIcon && _namingBinding!.Context.Gender != PokemonGender.Genderless)
        {
            bool female = _namingBinding.Context.Gender == PokemonGender.Female;
            SourceText(female ? _healthFemale : _healthMale, new(_namingEntryLayout.GenderX, layout.GenderY), female ? "♀" : "♂");
        }
        DrawTexture(_openingTextures["naming-input-arrow"], new(grid.EntryX - 9 + grid.ArrowOffsetX, 52));
        // Convert the native scalar column only for the decorative underscore. The editor
        // continues to own cursor movement, selection, deletion and IME composition.
        int prefixLength = 0, scalarColumn = 0;
        foreach (var rune in _nicknameEdit.Text.EnumerateRunes())
        {
            if (scalarColumn++ >= _nicknameEdit.CaretColumn) break;
            prefixLength += rune.Utf16SequenceLength;
        }
        int textElementColumn = NameTextRules.TextElementCount(_nicknameEdit.Text[..prefixLength]);
        for (int i = 0; i < grid.MaxChars; i++)
            DrawTexture(_openingTextures["naming-underscore"], new(grid.EntryX - 1 + i * layout.EntryCellWidth, 56 + (i == textElementColumn ? grid.UnderscoreOffsetY : 0)));
        foreach (var layer in grid.Layers(_captureBallAssets.Sine)) DrawNamingKeyboardLayer(layer.Page, layer.OffsetY);
        string button = new[] { "upper", "lower", "others" }[grid.PageButtonPage];
        DrawTexture(_openingTextures["naming-page-button-" + button], new(188, 75));
        if (grid.PageButtonTextVisible) DrawTexture(_openingTextures["naming-page-swap-" + button], new(192, 80 + grid.PageButtonOffsetY));
        DrawTexture(_openingTextures["naming-page-swap-frame"], new(184, 72));
        DrawTexture(_openingTextures["naming-back-button"], new(184, 104));
        DrawTexture(_openingTextures["naming-ok-button"], new(184, 128));
        if (_namingGridActive && !grid.Swapping)
        {
            if (grid.OnAction) DrawRect(new(184, grid.Row == 0 ? 72 : grid.Row == 1 ? 104 : 128, 40, grid.Row == 0 ? 32 : 24), Color.Color8(255, 66, 82), false, 1);
            else DrawTexture(_openingTextures["naming-cursor"], new(grid.CursorX - 8, 80 + grid.Row * 16));
        }
        DrawRect(new(0, 0, 240, 16), Color.Color8(74, 82, 107));
        string hint = _namingBinding!.Context.EscapeBehavior switch
        {
            NamingEscapeBehavior.ReturnToEntry => FieldMenuText.Get("naming.hint.back"),
            NamingEscapeBehavior.KeepOriginal => FieldMenuText.Get("naming.hint.keep"),
            _ => FieldMenuText.Get("naming.hint.delete")
        };
        SourceFittedText(_namingGuideFont!, new(4, 1), hint, 232);
        if (_namingGlyphUnavailable) ItemMessageBox(FieldMenuText.Get("naming.notice.character-unavailable"));
        else if (_namingBinding.Context.IsOpening && _entryNotice is not null) ItemMessageBox(_entryNotice);
        else if (StorageBoxNaming && _pcNotice is not null) ItemMessageBox(_pcNotice);
        else if (CaptureNamingActive && (_session.State.SafariBattle is not null ? _safariNotice : _wildNotice) is {} notice) ItemMessageBox(notice);
        else if (_status.StartsWith("nickname_", StringComparison.Ordinal) || _status.StartsWith("name_rater_", StringComparison.Ordinal))
            ItemMessageBox(_status == "nickname_length" ? FieldMenuText.Get("naming.notice.length", ("limit", grid.MaxChars.ToString())) : FieldMenuText.Get("naming.notice.failed"));
        return true;
    }

    private void DrawNamingTargetIcon(NamingScreenTemplate template, int visualFrame)
    {
        if (_namingBinding!.Pokemon is {} mon)
        {
            DrawTextureRectRegion(PresentedPokemonIcon(mon), new(template.IconX - 16, template.IconY - 16, 32, 32), new(0, 0, 32, 32));
            return;
        }
        if (template.IconFunction == 2)
        {
            int frame = OpeningTitlePresentation.AnimationFrame(_opening!.Animations["naming-pc"], visualFrame);
            DrawTextureRectRegion(_openingTextures["naming-pc-icon"], new(template.IconX - 8, template.IconY - 12, 16, 24), new(0, frame * 24, 16, 24));
            return;
        }
        string icon = template.IconFunction == 4 ? "rival" : _namingBinding.Context.PlayerGender == 0 ? "red" : "leaf";
        int iconFrame = OpeningTitlePresentation.AnimationFrame(_opening!.Animations[icon == "rival" ? "naming-rival" : "naming-player"], visualFrame);
        DrawTextureRectRegion(_openingTextures["naming-" + icon], new(template.IconX - 8, template.IconY - 16, 16, 32),
            icon == "rival" ? new(0, iconFrame * 32, 16, 32) : new(iconFrame * 16, 0, 16, 32));
    }

    private void DrawNamingKeyboardLayer(int page, int offsetY)
    {
        string name = new[] { "upper", "lower", "symbols" }[page];
        int top = Math.Max(16, offsetY), bottom = Math.Min(160, 160 + offsetY);
        DrawTextureRectRegion(_openingTextures["naming-keyboard-" + name], new(0, top, 240, bottom - top), new(0, top - offsetY, 240, bottom - top));
        for (int row = 0; row < 4; row++)
        {
            int y = 81 + row * 16 + offsetY;
            if (y < 16 || y > 145) continue;
            for (int col = 0; col < _opening!.Keyboard[page][row].Length; col++)
                SourceText(_namingKeyboardFont!, new(35 + _opening.NamingColumnOffsets[page][col], y), _opening.Keyboard[page][row][col].ToString());
        }
    }

    private void DisposeNamingPresentation()
    {
        _namingKeyboardFont?.Dispose(); _namingGuideFont?.Dispose();
        _namingKeyboardFont = _namingGuideFont = null; _namingScreen = null; _namingBinding = null;
    }
}
