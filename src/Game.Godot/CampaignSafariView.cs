using Game.Domain.Events;
using System.Collections.Immutable;
using Game.Application;
using Game.Content;
using Game.Domain.Pokemon;
using Godot;

/// <summary>Safari presentation has a trainer and one wild target, never a party combatant.
/// The session owns balls, turns, capture identity, delivery and the field return.</summary>
public partial class CampaignView
{
    private string? _safariPresentationKey, _safariBattleId, _safariNotice;
    private int _safariCursor, _safariTextPage;
    private double _safariElapsed;
    private ulong _safariActionFrame = ulong.MaxValue;
    private ImmutableArray<string> _safariPages = [];
    private static readonly string[] SafariActions = ["ball", "bait", "rock", "run"];

    private void LoadSafariPresentation()
    {
        var art = _catalog.SafariArt ?? throw new InvalidDataException("Safari art catalog missing.");
        RegisterPokemonArt(art.Sprites, art.Icons, art.Footprints, art.Files);

        LoadSafariItemPresentation();

    }

    private void LoadSafariItemPresentation()
    {
        var items = _catalog.SafariItems ?? throw new InvalidDataException("Safari item metadata missing.");
        foreach (var icon in items.Icons)
        {
            
            if(!(_earlyItemIcons.TryGetValue(icon.Item, out var existing)))_earlyItemIcons.Add(icon.Item, ItemTexture("data/items/safari", icon));
        }
        var document = _questPocketUi ?? throw new InvalidDataException("Safari key-item pocket missing.");
        var gold = items.Items.Single(i => i.Metadata.Item.Id == PretSafariItems.GoldTeethId).Metadata;
        var goldIcon = items.Icons.Single(i => i.Item == PretSafariItems.GoldTeethId);
        var position = document.Assets.Single(a => a.Id == document.Items[0].Icon);
        const string id = "safari-gold-teeth";
        
        _questPocketTextures.Add(id, ItemTexture("data/items/safari", goldIcon));
        _questPocketUi = document with
        {
            Items = document.Items.Add(new(gold.Item.Id.Value, gold.Item.SourceItemSymbol, gold.Item.Name,
                gold.Description, id, gold.Item.FieldUseFunction)),
            Assets = document.Assets.Add(new(id,goldIcon.PngPath,goldIcon.Width,goldIcon.Height,                position.X,position.Y))
        };
    }

    private bool CanOpenSafariFieldMenu
    {
        get
        {
            var s = _session.State;
            return s.SafariVisit is { Active: true, Balls: > 0, Steps: > 0 } &&
                s.Script.Flags.GetValueOrDefault("FLAG_SYS_SAFARI_MODE") && !s.SafariExitPending &&
                s.Script.Completed && !s.Script.Locked && s.Script.Wait is null && s.Script.Stack.IsEmpty &&
                s.Script.QuestLogState == 0 && s.EntryPrograms.IsEmpty && s.Dialogue is null && s.SignMessage is null &&
                s.NameRater is null && s.Nickname is null && s.MonPicture is null && !s.ScreenIsBlack && s.PlayerVisible &&
                s.PresentationTicks == 0 && s.Motions.IsEmpty && s.ArrivalFacing is null && s.Battle is null &&
                s.SafariBattle is null && s.WildBattle is null && s.ForestTrainerBattle is null && s.DoubleBattle is null &&
                s.OldManTutorial is null && s.FieldPoison is null && s.Evolution is null && s.RareCandy is null &&
                s.EscapeRope is null && s.HealingEffect is null && s.Shop is null && s.ItemMenu is null &&
                s.Pc is null && !s.PcPoweredOn && s.Fishing is null && s.MoneyBox is null && s.GiftDelivery is null &&
                s.TownMap is null && s.Repel is null && s.PokeFlute is null && s.CutEffect is null && s.RockSmashEffect is null && s.FlashEffect is null && s.StrengthEffect is null && s.SurfEffect is null &&
                s.HelpContext == "map";
        }
    }

    // start_menu.c:128-136, 255-264: source 10x4 stats window and remaining/600.
    private void DrawSafariFieldStats(SourceGameFont? font=null,Func<Texture2D,Texture2D>? palette=null)
    {
        if (_session.State.SafariVisit is not { Active: true } visit) return;
        _battleSkin.DrawUserFrame(this, new(0, 0, 96, 48),palette);
        var lines = SourceBattleText.Pages(_battleText, "gText_MenuSafariStats", new Dictionary<string, string>(StringComparer.Ordinal)
        { ["STR_VAR_1"] = visit.Steps.ToString().PadLeft(3), ["STR_VAR_2"] = "600", ["STR_VAR_3"] = visit.Balls.ToString().PadLeft(2) })[0].Split('\n');
        for (int i = 0; i < lines.Length; i++) SourceText(font??_sourceNormal, new(12, 11 + i * 16), lines[i]);
    }

    private void SynchronizeSafariBattle()
    {
        if (_session.State.SafariBattle is not { } battle)
        {
            _safariPresentationKey = null; _safariBattleId = null; _safariNotice = null;
            _safariPages = []; _safariCursor = 0; _safariTextPage = 0; return;
        }
        _held = null; string key = $"{battle.Id}:{battle.State.BattleTurnCounter}:{battle.Phase}:{battle.PageIndex}:{battle.Capture?.Stage}:{battle.Capture?.MessagePage}";
        if (_safariPresentationKey == key) return;
        bool entering = _safariBattleId != battle.Id;
        _safariPresentationKey = key; _safariBattleId = battle.Id;
        _safariElapsed = 0; _safariTextPage = 0; _safariNotice = null; _safariPages = [];
        if (entering || battle.Capture is not null) _safariCursor = 0;
        if (battle.Capture is { Stage: CampaignCaptureStage.DexNotice or CampaignCaptureStage.NicknameChoice } capture)
        {
            string symbol = capture.Stage == CampaignCaptureStage.DexNotice ? "sText_PkmnDataAddedToDex" : "sText_GiveNicknameCaptured";
            _safariPages = SourceBattleText.Pages(_battleText, symbol, new Dictionary<string, string>(StringComparer.Ordinal)
                { ["B_OPPONENT_MON1_NAME"] = (_catalog.SourceSpeciesName(capture.Pokemon.SourceSpeciesId) ?? throw new InvalidDataException("Pokemon species display name missing.")) });
        }
        else if (_session.VisibleSafariMessage is { } text)
        {
            // Source PC delivery can scroll beyond two lines. Keep every local page
            // visible before acknowledging the application's source-message boundary.
            var lines = text.Split('\n');
            _safariPages = lines.Length <= 2 ? [text] :
                Enumerable.Range(0, lines.Length - 1).Select(i => string.Join('\n', lines.Skip(i).Take(2))).ToImmutableArray();
        }
    }

    private void ProcessSafariBattle(double delta) { _held = null; _safariElapsed += delta; ProcessSafariCaptureBall(); }

    private bool SafariBattleInput(InputEventKey key)
    {
        if (key.Keycode is Key.Escape or Key.F12) return false;
        _held = null; if (!key.Pressed || key.Echo || _busy) return true;
        if (key.Keycode is Key.F5 or Key.F9)
        { _safariNotice = FieldMenuText.Get("safari.notice.save-load"); return true; }
        var battle = _session.State.SafariBattle!;
        if (battle.Capture?.Stage == CampaignCaptureStage.Naming) return true;
        if (_safariNotice is not null)
        { if (key.Keycode is Key.Enter or Key.Backspace) _safariNotice = null; return true; }
        if (_safariActionFrame == Engine.GetProcessFrames()) return true;
        if (battle.Capture?.Stage == CampaignCaptureStage.NicknameChoice && _safariTextPage + 1 >= _safariPages.Length)
        {
            if (key.Keycode is Key.Up or Key.Down) { _safariCursor ^= 1; SourceSound("SE_SELECT"); }
            if (key.Keycode is Key.Enter or Key.Backspace)
            { _safariActionFrame = Engine.GetProcessFrames(); Apply(new("capture_choice", Choice: key.Keycode == Key.Enter && _safariCursor == 0 ? 1 : 0)); }
            return true;
        }
        if (battle.Capture is not null || battle.Phase == CampaignBattlePhase.Events)
        {
            if (key.Keycode == Key.Enter && !SafariCaptureBallBlocking && _safariElapsed > .1)
            {
                _safariActionFrame = Engine.GetProcessFrames();
                if (_safariTextPage + 1 < _safariPages.Length) { _safariTextPage++; _safariElapsed = 0; }
                else Apply(new("ack"));
            }
            return true;
        }
        if (battle.Phase != CampaignBattlePhase.ChooseMove) return true;
        if (key.Keycode is Key.Left or Key.Right) { _safariCursor ^= 1; SourceSound("SE_SELECT"); }
        if (key.Keycode is Key.Up or Key.Down) { _safariCursor ^= 2; SourceSound("SE_SELECT"); }
        if (key.Keycode == Key.Enter)
        {
            _safariActionFrame = Engine.GetProcessFrames();
               bool accepted = Apply(new("safari_action", Text: SafariActions[_safariCursor]));
            if (!accepted) _safariNotice = FieldMenuText.Get("safari.notice.action-unavailable");
        }
        return true;
    }

    private bool NavigateSafariPointer(Vector2 point, int direction)
    {
        var battle = _session.State.SafariBattle!;
        if (_safariNotice is not null) return false;
        if (battle.Capture is { } capture)
            return capture.Stage == CampaignCaptureStage.NicknameChoice && _safariTextPage + 1 >= _safariPages.Length &&
                PointerYesNo(point, direction, ref _safariCursor);
        return battle.Phase == CampaignBattlePhase.ChooseMove && PointerGrid(point, direction, ref _safariCursor, false, 4);
    }

    private bool MouseSafari(Vector2 point)
    {
        var battle = _session.State.SafariBattle!;
        if (_safariNotice is not null) return MouseConfirm(point, MouseMessage);
        if (battle.Capture is { } capture)
        {
            if (capture.Stage == CampaignCaptureStage.NicknameChoice && _safariTextPage + 1 >= _safariPages.Length)
                return MouseYesNo(point, ref _safariCursor);
            if (capture.Stage == CampaignCaptureStage.DexEntry && _captureDex is { } dex)
                return MouseConfirm(point, new Rect2(dex.Layout.FooterRightX - 40, dex.Layout.FooterY, 40, 16));
            return capture.Stage != CampaignCaptureStage.Naming && MouseConfirm(point, MouseMessage);
        }
        return battle.Phase == CampaignBattlePhase.ChooseMove ? MouseGrid(point, ref _safariCursor, false, 4) : MouseConfirm(point, MouseMessage);
    }

    private void DrawSafariBattle()
    {
        var battle = _session.State.SafariBattle!;
        if (battle.Capture?.Stage == CampaignCaptureStage.DexEntry) { DrawCaptureDexEntry(battle.State.Target.SourceSpeciesId); if (_safariNotice is not null) ItemMessageBox(_safariNotice); return; }
        if (battle.Capture?.Stage == CampaignCaptureStage.Naming) { DrawNamingScreen(); if (_safariNotice is not null) ItemMessageBox(_safariNotice); return; }
        DrawRect(new(0, 0, 240, 160), Colors.Black);
        _battleSkin.DrawBackground(this); DrawTexture(_session.State.IsSurfing ? _cityBattleScenes["water"] : _wildTerrain, Vector2.Zero);
        DrawSafariCaptureTrainer();
        if (battle.Capture is null)
        {
            var target = battle.State.Target;
            var sprite = _battleArt[(PresentedRenderSpecies(target), "front", target.IsShiny)];
            if(SafariCaptureBallPlayback() is {} playback)DrawCaptureTargetFrame(sprite.Texture,sprite.Position,playback.Sample(SafariCaptureBallTick),playback.Ball,playback.TargetBlend(SafariCaptureBallTick));
            else DrawTexture(sprite.Texture, sprite.Position);
            _battleSkin.DrawHealthbox(this, false, target.CurrentHp, target.Stats.MaxHp);
            var levelOrigin = LabBattleUiSkin.OpponentLevelWindow + new Vector2(5 * (3 - target.Level.ToString().Length), 0);
            DrawHealthName(_healthFont, target.Gender == PokemonGender.Genderless ? null : target.Gender == PokemonGender.Female ? _healthFemale : _healthMale,
                LabBattleUiSkin.OpponentName, _catalog.SourceSpeciesName(target.SourceSpeciesId) ?? throw new InvalidDataException("Pokemon species display name missing."), target.Gender, levelOrigin.X);
            SourceText(_healthFont, levelOrigin, _healthFont.LevelPrefix + target.Level);
        }
        if(SafariCaptureBallPlayback() is {} ballPlayback)DrawCapturePlayback(ballPlayback,SafariCaptureBallTick);
        // Source wording/counter, using the shared source frame until the dedicated Safari healthbox is imported.
        _battleSkin.DrawStandardFrame(this, new(128, 72, 112, 40));
        SourceText(_sourceSmall, new(140, 79), SourceMenuLabelPresentation.Plain(_battleText, "gText_SafariBalls", new Dictionary<string, string> { ["HIGHLIGHT 2"] = "" }));
        SourceText(_sourceSmall, new(157, 94), SourceMenuLabelPresentation.Plain(_battleText, "gText_HighlightRed_Left", new Dictionary<string, string> { ["HIGHLIGHT 2"] = "" }) + battle.State.BallsRemaining.ToString().PadLeft(2));
        _battleSkin.DrawBattleMessage(this);
        if (battle.Capture is null && battle.Phase == CampaignBattlePhase.ChooseMove)
        {
            _battleSkin.DrawActionMenu(this); DrawCompactBattlePrompt(_battleSelectionText.SafariPrompt(_session.State.PlayerName));
            var labels = _battleSelectionText.SafariActionLabels;
            for (int i = 0; i < labels.Length; i++) SourceText(_battleMenuFont, LabBattleUiSkin.ActionText(i), labels[i]);
            _uiCorrections.DrawSelectionCursor(this, false, _safariCursor);
        }
        else if (!_safariPages.IsEmpty && !SafariCaptureBallBlocking)
        {
            string text = _safariPages[_safariTextPage]; WildLines(text);
            if (battle.Capture?.Stage == CampaignCaptureStage.NicknameChoice && _safariTextPage + 1 >= _safariPages.Length)
            {
                _battleSkin.DrawStandardFrame(this, new(184, 64, 56, 48));
                SourceText(_sourceNormal, new(200, 72), InventorySourceText("gText_Yes")); SourceText(_sourceNormal, new(200, 88), InventorySourceText("gText_No"));
                SourceText(_sourceNormal, new(188, 72 + _safariCursor * 16), "▶");
            }
            else DrawTextCursor(_battleWhite, LabBattleUiSkin.BattleMessageText, text.Split('\n'), 16, _safariElapsed, true);
        }
        if (_safariNotice is not null) ItemMessageBox(_safariNotice);
    }

    private void ProcessSafariBattleAudio()
    {
        if (_session.State.SafariBattle is not { Phase: CampaignBattlePhase.Events, PageIndex: 0 } battle ||
            !battle.Events.IsEmpty || !_audioTrainerOneShots.Add(battle.Id + ":safari-appearance")) return;
        SourceCry(battle.State.Target);
    }
}
