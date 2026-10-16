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
    private OpeningPortraitTransition? _openingPortraitTransition;
    private Action? _openingPortraitCompleted;
    private SourceGameFont? _openingNamingFont;
    private string _openingNext = "gender";
    private readonly OpeningTitlePresentation _openingTitle = new();
    private readonly OpeningTitleRevealPresentation _openingTitleReveal = new();
    private readonly SourceTextProgress _openingPrinter = new();
    private double _openingFrameRemainder;
    private int _openingVisualFrame, _guidePage;
    private bool _openingSpeedUp;
    private OpeningExitPresentation? _openingExit;
    private OpeningNidoranPresentation? _openingNidoran;
    private int _titleExitFrame, _guideExitFrame;
    private string? OpeningAudioContext => PlayerEntryActive ? "opening:" + _entryMode : null;
    private string? OpeningSourceMusic => _entryMode switch
    {
        "intro-movie" => IntroMovieSourceMusic,
        "title" => "MUS_TITLE",
        "title-exit" => _titleExitFrame < 90 ? "MUS_TITLE" : "MUS_NONE",
        "controls-guide" => "MUS_NEW_GAME_INSTRUCT",
        "pikachu-guide" => "MUS_NEW_GAME_INTRO",
        "guide-exit" => "MUS_NEW_GAME_EXIT",
        "oak-speech" or "gender" or "player-name" or "player-confirm" or "rival-name" or "rival-choice" or "rival-confirm" or "oak-return" or "oak-exit" => "MUS_ROUTE24",
        "menu" or "notice" => "MUS_NONE",
        _ => null
    };
    private void BeginTitleExit()
    { _entryMode = "title-exit"; _titleExitFrame = 0; _openingTitle.BeginExit(); SourceCrySymbol("SPECIES_CHARIZARD"); }
    private SourceGameFont? _openingGuideFont;
    private void BeginAnimatedTitle()
    { _entryMode = "title"; _openingTitle.Reset(); _openingTitleReveal.Reset(); _openingFrameRemainder = 0; _openingVisualFrame = 0; }
    private void BeginNewGameGuide()
    { _entryMode = "controls-guide"; _guidePage = 0; _openingVisualFrame = 0; }
    private void ProcessOpeningPresentation(double delta)
    {
        if (_entryMode != "oak-speech") { _openingPortraitTransition = null; _openingPortraitCompleted = null; }
        if (OpeningSourceMusic is {} music) SourceMusic(music);
        _openingFrameRemainder += delta * 60;
        int frames = (int)_openingFrameRemainder; _openingFrameRemainder -= frames;
        _openingVisualFrame += frames;
        if (_entryMode is "player-name" or "rival-name") _namingGrid!.AdvanceFrames(frames);
        if (_openingPortraitTransition is { Complete: false } transition)
        {
            transition.AdvanceFrames(frames);
            if (transition.Complete)
            {
                _openingPortraitTransition = null;
                var completed = _openingPortraitCompleted; _openingPortraitCompleted = null; completed?.Invoke();
            }
            return;
        }
        if (_entryMode == "intro-movie") { ProcessIntroMoviePresentation(delta); return; }
        if (_entryMode == "title" && _openingVisualFrame >= 2700) { BeginIntroMovie(); return; }
        if (_entryMode == "title")
            for (int i = 0; i < frames; i++)
                if (!_openingTitleReveal.Complete) _openingTitleReveal.AdvanceFrame();
                else _openingTitle.AdvanceFrames(1);
        if (_entryMode == "title-exit")
        {
            _openingTitle.AdvanceFrames(frames);
            _titleExitFrame += frames;
            if (_titleExitFrame >= 106) ReadEntrySave();
        }
        if (_entryMode == "guide-exit")
        {
            _guideExitFrame += frames;
            if (_guideExitFrame >= 72) BeginOakIntroduction();
        }
        if (_entryMode == "oak-exit")
        {
            for (int i = 0; i < frames; i++)
            { _openingExit!.AdvanceFrame(); if (_openingExit.Frame == 40) SourceSound("SE_WARP_IN"); }
            if (_openingExit!.Complete) StartChosenGame();
        }
        if (_entryMode is "oak-speech" or "oak-return")
        {
            for (int i = 0; i < frames; i++)
            {
                if (_openingNidoran is { Complete: false } mon)
                {
                    mon.AdvanceFrame();
                    if (!mon.Returning && mon.Frame == 32) SourceCrySymbol("SPECIES_NIDORAN_F");
                    if (mon.Returning && mon.Frame == 44) SourceSound("SE_BALL_TRADE");
                    if (mon.Returning && mon.Complete) { _entryMode = "oak-speech"; _openingNidoran = null; ResetOpeningPrinter(); }
                }
                if (_entryMode == "oak-speech" && (_openingNidoran is null || _openingNidoran.Returning || _openingNidoran.Frame >= 32))
                    _openingPrinter.AdvanceFrames(1, _openingSpeedUp, _openingSpeedUp);
            }
        }
    }
    private void ResetOpeningPrinter()
    {
        _openingSpeedUp = false;
        int speed = (_entrySave?.State.Options ?? Game.Domain.Options.SourceGameOptions.Defaults).TextSpeed switch
        { Game.Domain.Options.SourceTextSpeed.Slow => 8, Game.Domain.Options.SourceTextSpeed.Mid => 4, _ => 1 };
        _openingPrinter.Reset(_openingPages[_openingPage].Text, speed);
    }
    private bool OpeningGuideKey(Key key)
    {
        if (_entryMode is not ("controls-guide" or "pikachu-guide")) return false;
        if (key == Key.Enter && _entryActionFrame == Engine.GetProcessFrames()) return true;
        if (key is Key.Backspace or Key.Left)
        { if (_guidePage > 0) _guidePage--; }
        else if (key is Key.Enter or Key.Right)
        {
            _entryActionFrame = Engine.GetProcessFrames(); SourceSound("SE_SELECT");
            if (++_guidePage == 3)
            {
                if (_entryMode == "controls-guide") { _entryMode = "pikachu-guide"; _guidePage = 0; _openingVisualFrame = 0; }
                else { _entryMode = "guide-exit"; _guidePage = 2; _guideExitFrame = 0; }
            }
        }
        return true;
    }
    private void DrawOpeningTitle()
    {
        if (!_openingTitleReveal.Complete) { DrawOpeningTitleReveal(); return; }
        DrawRect(new(0, 0, 240, 160), Colors.Black);
        DrawTexture(_openingTextures["title-border"], Vector2.Zero);
        DrawTexture(_openingTextures[_openingTitle.PressStartVisible ? "title-lettering" : "title-lettering-hidden"], Vector2.Zero);
        foreach (var flame in _openingTitle.Flames)
        {
            if (!flame.Visible) continue;
            int frame = OpeningTitlePresentation.AnimationFrame(_opening!.Animations["flames"], flame.Age, false);
            if (frame >= 0) DrawTextureRectRegion(_openingTextures["flames"], new((flame.X16 >> 4) - 8, (flame.Y16 >> 4) - 8, 16, 16), new(0, frame * 16, 16, 16));
        }
        DrawTexture(_openingTextures["title-mon"], Vector2.Zero);
        DrawTexture(_openingTextures["title-logo"], Vector2.Zero);
        if (_openingTitle.SlashVisible)
            DrawTexture(_openingTextures["slash"], new(_openingTitle.SlashX - 32, _openingTitle.SlashY - 32));
    }
    private void DrawOpeningTitleReveal()
    {
        var reveal = _openingTitleReveal;
        DrawRect(new(0, 0, 240, 160), Colors.Black);
        if (reveal.WindowFrame >= 0)
        {
            int borderWidth = Math.Clamp(reveal.WindowFrame * 24, 0, 240);
            if (borderWidth > 0) DrawTextureRectRegion(_openingTextures["title-border"], new(0, 0, borderWidth, 160), new(0, 0, borderWidth, 160));
            if (reveal.WindowFrame >= 20)
                DrawTexture(_openingTextures["title-lettering"], new(Math.Max(0, 240 - (reveal.WindowFrame - 20) * 24), 0));
        }
        if (reveal.UseColor) DrawTexture(_openingTextures["title-mon"], Vector2.Zero);
        else DrawTexture(_openingTextures["title-mon-gray"], Vector2.Zero, new Color(reveal.GrayBrightness, reveal.GrayBrightness, reveal.GrayBrightness));
        if (reveal.WhiteBlend > 0 && !reveal.ShowLogo)
            DrawTexture(_openingTextures["title-mon-white"], Vector2.Zero, new Color(1, 1, 1, reveal.WhiteBlend));
        if (reveal.Scanline >= -16)
            for (int y = Math.Max(0, reveal.Scanline - 15); y < Math.Min(160, reveal.Scanline + 16); y++)
                DrawTextureRectRegion(_openingTextures["title-mon-white"], new(0, y, 240, 1), new(0, y, 240, 1), new Color(1, 1, 1, (16 - Math.Abs(y - reveal.Scanline)) / 16f));
        if (reveal.ShowLogo)
        {
            DrawTexture(_openingTextures["title-logo"], Vector2.Zero);
            DrawRect(new(0, 0, 240, 160), new Color(247 / 255f, 247 / 255f, 1, reveal.WhiteBlend));
        }
    }
    private void BeginRivalNameChoices()
    { _entryMode = "rival-choice"; _entryCursor = 0; _openingVisualFrame = 0; }
    private bool OpeningRivalChoiceKey(Key key)
    {
        if (_entryMode != "rival-choice") return false;
        if (_openingVisualFrame < 30 || _entryActionFrame == Engine.GetProcessFrames()) return true;
        if (key is Key.Up or Key.Down) _entryCursor = (_entryCursor + (key == Key.Up ? 4 : 1)) % 5;
        else if (key == Key.Enter)
        {
            _entryActionFrame = Engine.GetProcessFrames(); SourceSound("SE_SELECT");
            if (_entryCursor == 0) ShowEntryName("rival-name");
            else { _entryRivalName = _opening!.RivalNames[_entryCursor - 1]; _entryMode = "rival-confirm"; _entryCursor = 0; }
        }
        return true;
    }
    private void OpeningGuideText(SourceGameFont font, Vector2 origin, string text, int lineStep)
    {
        // DrawString is deliberately single-line. Keep source LF and blank rows here.
        var lines = text.Split('\n');
        for (int line = 0; line < lines.Length; line++) SourceText(font, origin + new Vector2(0, line * lineStep), lines[line]);
    }
    private void DrawOpeningGuide()
    {
        bool controls = _entryMode == "controls-guide";
        DrawRect(new(0, 0, 240, 160), Color.Color8(16, 115, 231));
        DrawRect(new(0, 0, 240, 16), Color.Color8(74, 82, 107));
        SourceText(_openingGuideFont!, new(8, 0), controls ? "CONTROLS" : "");
        SourceText(_openingGuideFont!, new(108, 0), _guidePage == 0 ? "A: NEXT" : "A: NEXT  B: BACK");
        if (controls)
        {
            if (_guidePage == 0) OpeningGuideText(_openingGuideFont!, new(2, 56), _opening!.Texts["gControlsGuide_Text_Intro"], 15);
            else
            {
                DrawTexture(_openingTextures["controls-" + (_guidePage + 1)], new(8, 24));
                string[] names = _guidePage == 1 ? ["DPad", "AButton", "BButton"] : ["StartButton", "SelectButton", "LRButtons"];
                int[] ys = _guidePage == 1 ? [24, 80, 120] : [24, 64, 104];
                for (int i = 0; i < 3; i++) OpeningGuideText(_openingGuideFont!, new(54, ys[i]), _opening!.Texts["gControlsGuide_Text_" + names[i]], 15);
            }
        }
        else
        {
            DrawTexture(_openingTextures["pikachu-background"], new(0, 16));
            OpeningGuideText(_sourceNormal, new(11, 37), _opening!.Texts["gPikachuIntro_Text_Page" + (_guidePage + 1)], 14);
            DrawPikachuPart("ears", 32, 16, new(0, 1));
            DrawPikachuPart("body", 32, 32, new(0, 1));
            DrawPikachuPart("eyes", 16, 8, new(16, 9));
        }
    }
    private void DrawPikachuPart(string part, int width, int height, Vector2 position)
    {
        string name = "pikachu-" + part;
        int frame = OpeningTitlePresentation.AnimationFrame(_opening!.Animations[name], _openingVisualFrame);
        // Eyes use two horizontal frames; body and ears are stacked vertically.
        var source = part == "eyes" ? new Rect2(frame * width, 0, width, height) : new Rect2(0, frame * height, width, height);
        DrawTextureRectRegion(_openingTextures[name], new(position, new(width, height)), source);
    }

    private void InitializeOpeningPresentation()
    {
        InitializeIntroMoviePresentation();
        _opening = PretOpeningPresentation.Load(_root);
        _namingGrid = new(_opening);
        _openingNamingFont = SourceGameFont.Load(Path.Combine(_root, "content/generated/ui/font"), foreground: Colors.White, shadow: Color.Color8(98, 98, 98));
        _openingGuideFont = SourceGameFont.Load(Path.Combine(_root, "content/generated/ui/font"), foreground: Colors.White, shadow: Color.Color8(74, 82, 107));
        foreach (string name in PretOpeningPresentation.TextureNames)
        {
            using var image = new Image();
            if (image.LoadPngFromBuffer(_opening.ReadTexture(name)) != Error.Ok) throw new InvalidDataException("Opening texture invalid: " + name);
            _openingTextures.Add(name, ImageTexture.CreateFromImage(image));
        }
    }
    private string OpeningPlayerPortrait => _entryGender == 0 ? "red" : "leaf";
    private void BeginOpeningSpeech(string next, params (string Symbol, string Portrait)[] messages)
    {
        string? previous = _entryMode == "player-confirm" ? OpeningPlayerPortrait : null;
        bool showPlayer = _entryMode == "gender";
        _openingNidoran = null; _openingPortraitTransition = null; _openingPortraitCompleted = null;
        _openingPages.Clear(); _openingPage = 0; _openingNext = next; _entryMode = "oak-speech"; _entryCursor = 0;
        foreach (var (symbol, portrait) in messages)
            foreach (string text in _opening!.Pages(symbol, _entryPlayerName, _entryRivalName)) _openingPages.Add((text, portrait));
        if (_openingPages.Count == 0) throw new InvalidDataException("Opening speech has no pages.");
        ResetOpeningPrinter();
        if (previous is not null || showPlayer)
            _openingPortraitTransition = new(previous, _openingPages[0].Portrait, previous is null ? 0 : 40);
        QueueRedraw();
    }
    private void BeginOakIntroduction() => BeginOpeningSpeech("gender",
        ("WelcomeToTheWorld", "oak"), ("ThisWorld", "oak"), ("IsInhabitedFarAndWide", "nidoran-f"),
        ("IStudyPokemon", "nidoran-f"), ("TellMeALittleAboutYourself", "oak"));
    private void AdvanceOpeningSpeech()
    {
        if (_openingPortraitTransition is { Complete: false }) return;
        if (!_openingPrinter.Complete) { _openingSpeedUp = true; return; }
        if (_openingNidoran is { Returning: false, Complete: false }) return;
        if (_openingNidoran is { Returning: false } && _sourceAudio?.Snapshot().Cry is { Playing: true }) return;
        string previousPortrait = _openingPages[_openingPage].Portrait;
        if (++_openingPage < _openingPages.Count)
        {
            ResetOpeningPrinter();
            string nextPortrait = _openingPages[_openingPage].Portrait;
            if (previousPortrait != "nidoran-f" && nextPortrait == "nidoran-f")
                _openingNidoran = new(false, _captureBallAssets.Sine);
            else if (previousPortrait == "nidoran-f" && nextPortrait != "nidoran-f")
            { _openingNidoran = new(true, _captureBallAssets.Sine); _entryMode = "oak-return"; }
            else if (previousPortrait != nextPortrait)
                _openingPortraitTransition = new(previousPortrait, nextPortrait, 40);
            return;
        }
        if (_openingNext == "start") { _openingExit = new(); _entryMode = "oak-exit"; }
        else if (_openingNext == "rival-name") BeginRivalNameChoices();
        else if (_openingNext == "player-name") ShowEntryName(_openingNext);
        else if (_openingNext == "gender")
        {
            _openingPage = _openingPages.Count - 1;
            _openingPortraitTransition = new("oak", null, 48);
            _openingPortraitCompleted = () => { _entryMode = "gender"; _entryCursor = 0; };
        }
        else { _entryMode = _openingNext; _entryCursor = 0; }
    }
    private void DrawOpeningPortrait(string portrait, float offset = 0)
    {
        DrawTexture(_openingTextures["background"], Vector2.Zero);
        DrawTexture(_openingTextures["platform"], new(72 + offset, 96));
        DrawTexture(_openingTextures[portrait], portrait == "nidoran-f" ? new(88 + offset, 48) : new(88 + offset, 16));
    }
    private bool DrawOpeningEntry()
    {
        if (DrawIntroMoviePresentation()) return true;
        if (_openingPortraitTransition is { Complete: false } transition)
        {
            DrawTexture(_openingTextures["background"], Vector2.Zero);
            if (transition.PlatformVisible) DrawTexture(_openingTextures["platform"], new(72, 96));
            if (transition.Portrait is { } portrait && transition.Alpha > 0)
                DrawTexture(_openingTextures[portrait], new(88, 16), new Color(1, 1, 1, transition.Alpha));
            return true;
        }
        if (_entryMode == "title-exit")
        {
            DrawOpeningTitle();
            if (_titleExitFrame >= 90) DrawRect(new(0, 0, 240, 160), new Color(1, 1, 1, Math.Clamp((_titleExitFrame - 90) / 16f, 0, 1)));
            return true;
        }
        if (_entryMode == "oak-exit")
        {
            var exit = _openingExit!;
            DrawTexture(_openingTextures["background"], Vector2.Zero);
            DrawTexture(_openingTextures["platform"], new(72, 96));
            DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, exit.BackgroundFade));
            var rect = new Rect2(120 - 32 * exit.ScaleX, 84 - 68 * exit.ScaleY, 64 * exit.ScaleX, 96 * exit.ScaleY);
            DrawTextureRect(_openingTextures[OpeningPlayerPortrait], rect, false);
            DrawTextureRect(_openingTextures[OpeningPlayerPortrait + "-white"], rect, false, new Color(1, 1, 1, exit.WhiteBlend));
            DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, exit.PlayerBlackFade));
            return true;
        }
        if (_entryMode == "title") { DrawOpeningTitle(); return true; }
        if (_entryMode is "controls-guide" or "pikachu-guide" or "guide-exit")
        {
            DrawOpeningGuide();
            if (_entryMode == "guide-exit" && _guideExitFrame >= 24)
                DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, Math.Clamp((_guideExitFrame - 24) / 48f, 0, 1)));
            return true;
        }
        if (_entryMode == "rival-choice")
        {
            DrawOpeningPortrait("rival", Math.Min(60, _openingVisualFrame * 2));
            ItemMessageBox(_opening!.Pages("YourRivalsNameWhatWasIt", _entryPlayerName, _entryRivalName)[0]);
            if (_openingVisualFrame >= 30)
            {
                _battleSkin.DrawStandardFrame(this, new(8, 8, 112, 88));
                SourceText(_sourceNormal, new(24, 16), _opening.Texts["gOtherText_NewName"]);
                for (int i = 0; i < 4; i++) SourceText(_sourceNormal, new(24, 32 + i * 16), _opening.RivalNames[i]);
                SourceText(_sourceNormal, new(10, 16 + _entryCursor * 16), "▶");
            }
            return true;
        }
        if (_entryMode == "gender")
        {
            DrawTexture(_openingTextures["background"], Vector2.Zero);
            _battleSkin.DrawStandardFrame(this, new(136, 64, 88, 48));
            SourceText(_sourceNormal, new(152, 72), "BOY"); SourceText(_sourceNormal, new(152, 88), "GIRL");
            SourceText(_sourceNormal, new(138, 72 + _entryCursor * 16), "▶");
            ItemMessageBox(_opening!.Pages("AskPlayerGender", _entryPlayerName, _entryRivalName)[0]); return true;
        }
        if (_entryMode is "oak-speech" or "oak-return")
        {
            var page = _openingPages[_openingPage];
            DrawOpeningPortrait(page.Portrait == "nidoran-f" ? "oak" : page.Portrait);
            if (_openingNidoran is not null)
            {
                if (_openingNidoran.BackgroundFlash > 0) DrawRect(new(0, 0, 240, 160), new Color(1, 1, 1, _openingNidoran.BackgroundFlash));
                DrawOpeningNidoran();
            }
            if (_entryMode == "oak-speech") ItemMessageBox(_openingNidoran is { Returning: false, Frame: < 32 } ? _opening!.Pages("ThisWorld", _entryPlayerName, _entryRivalName)[0] : _openingPrinter.VisibleText);
            return true;
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
            DrawOpeningNaming();
            if (_entryNotice is not null) ItemMessageBox(_entryNotice);
            return true;
        }
        return false;
    }
    private void DrawOpeningNaming()
    {
        var grid = _namingGrid!;
        DrawRect(new(0, 0, 240, 160), Colors.White);
        DrawTexture(_openingTextures["naming-background"], Vector2.Zero);
        SourceText(_sourceNormal, new(73, 33), _entryMode == "player-name" ? "YOUR NAME?" : "RIVAL's NAME?");
        string icon = _entryMode == "rival-name" ? "rival" : OpeningPlayerPortrait;
        int iconFrame = OpeningTitlePresentation.AnimationFrame(_opening!.Animations[icon == "rival" ? "naming-rival" : "naming-player"], grid.VisualFrame);
        DrawTextureRectRegion(_openingTextures["naming-" + icon], new(48, 21, 16, 32), icon == "rival" ? new(0, iconFrame * 32, 16, 32) : new(iconFrame * 16, 0, 16, 32));
        DrawTexture(_openingTextures["naming-input-arrow"], new(89 + grid.ArrowOffsetX, 52));
        for (int i = 0; i < 7; i++)
            DrawTexture(_openingTextures["naming-underscore"], new(97 + i * 8, 56 + (i == _entryNameEdit!.CaretColumn ? grid.UnderscoreOffsetY : 0)));
        foreach (var layer in grid.Layers(_captureBallAssets.Sine)) DrawNamingKeyboardLayer(layer.Page, layer.OffsetY);
        string button = new[] { "upper", "lower", "others" }[grid.PageButtonPage];
        DrawTexture(_openingTextures["naming-page-button-" + button], new(188, 75));
        if (grid.PageButtonTextVisible)
            DrawTexture(_openingTextures["naming-page-swap-" + button], new(192, 80 + grid.PageButtonOffsetY));
        DrawTexture(_openingTextures["naming-page-swap-frame"], new(184, 72));
        DrawTexture(_openingTextures["naming-back-button"], new(184, 104));
        DrawTexture(_openingTextures["naming-ok-button"], new(184, 128));
        if (_namingGridActive && !grid.Swapping)
        {
            if (grid.OnAction) DrawRect(new(184, grid.Row == 0 ? 72 : grid.Row == 1 ? 104 : 128, 40, grid.Row == 0 ? 32 : 24), Color.Color8(255, 66, 82), false, 1);
            else DrawTexture(_openingTextures["naming-cursor"], new(grid.CursorX - 8, 80 + grid.Row * 16));
        }
        DrawRect(new(0, 0, 240, 16), Color.Color8(74, 82, 107));
        SourceText(_openingGuideFont!, new(4, 1), "Tab: type/keys  Enter: OK  Esc: back");
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
                SourceText(_openingNamingFont!, new(35 + _opening.NamingColumnOffsets[page][col], y), _opening.Keyboard[page][row][col].ToString());
        }
    }
    private void DrawOpeningNidoran()
    {
        var mon = _openingNidoran!;
        if (mon.TargetVisible)
        {
            float size = 64 * mon.Scale;
            DrawTextureRect(CaptureTargetTint(_openingTextures["nidoran-f"], Game.Domain.Capture.CaptureBall.Poke, mon.TintBlend), new(mon.X - size / 2, mon.Y - size / 2, size, size), false);
        }
        if (mon.BallVisible)
            DrawTextureRectRegion(_captureBallAtlas, new(92, 58, 16, 16), new(_captureBallAssets.Column(Game.Domain.Capture.CaptureBall.Poke), mon.BallFrame * 16, 16, 16));
        foreach (var particle in CaptureBallPresentation.PokeBallBurst(mon.ParticleAge, 100, 61, _captureBallAssets.Sine))
        {
            DrawSetTransform(new(particle.X, particle.Y), 0, new(particle.FlipH ? -1 : 1, 1));
            DrawTextureRectRegion(_captureParticleAtlas, new(-4, -4, 8, 8), new(0, particle.TileFrame * 8, 8, 8));
            DrawSetTransform(Vector2.Zero);
        }
    }
    private void ApplyOpeningNameKey(OpeningNameKey key)
    {
        if (key.Action == OpeningNameAction.None) return;
        if (key.Action == OpeningNameAction.ChangePage) SourceSound("SE_WIN_OPEN");
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
            if (_namingGrid!.Swapping)
            {
                if (key.Unicode > 0 && key.Keycode is not (Key.Enter or Key.KpEnter or Key.Backspace))
                { _namingGridActive = false; return false; }
                return key.Keycode != Key.Escape;
            }
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
            if (_namingGrid!.Swapping) return true;
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
        if (_namingGrid!.Swapping) return true;
        if (new Rect2(184, 72, 40, 80).HasPoint(point))
            _namingGrid.Select(point.Y < 104 ? 0 : point.Y < 128 ? 1 : 2, _namingGrid.Columns);
        else if (new Rect2(24, 80, 152, 64).HasPoint(point))
        {
            int row = (int)((point.Y - 80) / 16);
            int col = Enumerable.Range(0, _namingGrid.Columns).MinBy(i => Math.Abs(point.X - 38 - _opening!.NamingColumnOffsets[_namingGrid.Page][i]));
            _namingGrid.Select(row, col);
        }
        else return false;
        _namingGridActive = false; ApplyOpeningNameKey(_namingGrid!.Activate());
        if (_entryMode is "player-name" or "rival-name") { _entryNameEdit!.GrabFocus(); _entryNameEdit.Edit(); }
        QueueRedraw(); return true;
    }
    private void DisposeOpeningPresentation()
    { DisposeIntroMoviePresentation(); foreach (var texture in _openingTextures.Values) texture.Dispose(); _openingTextures.Clear(); _openingGuideFont?.Dispose(); _openingNamingFont?.Dispose(); }
}
