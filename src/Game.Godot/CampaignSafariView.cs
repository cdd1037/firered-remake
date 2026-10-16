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
    private static readonly string[] SafariLabels = ["BALL", "BAIT", "ROCK", "RUN"];

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
    private void DrawSafariFieldStats()
    {
        if (_session.State.SafariVisit is not { Active: true } visit) return;
        _battleSkin.DrawUserFrame(this, new(0, 0, 96, 48));
        SourceText(_sourceNormal, new(12, 11), visit.Steps.ToString().PadLeft(3) + "/600");
        SourceText(_sourceNormal, new(12, 27), "BALLS  " + visit.Balls.ToString().PadLeft(2));
    }

    private void SynchronizeSafariBattle()
    {
        if (_session.State.SafariBattle is not { } battle)
        {
            _safariPresentationKey = null; _safariBattleId = null; _safariNotice = null;
            _safariPages = []; _safariCursor = 0; _safariTextPage = 0; return;
        }
        _held = null; _repeat = 0;
        string key = $"{battle.Id}:{battle.State.BattleTurnCounter}:{battle.Phase}:{battle.PageIndex}:{battle.Capture?.Stage}:{battle.Capture?.MessagePage}";
        if (_safariPresentationKey == key) return;
        bool entering = _safariBattleId != battle.Id;
        _safariPresentationKey = key; _safariBattleId = battle.Id;
        _safariElapsed = 0; _safariTextPage = 0; _safariNotice = null; _safariPages = [];
        if (entering || battle.Capture is not null) _safariCursor = 0;
        if (battle.Capture is { Stage: CampaignCaptureStage.DexNotice or CampaignCaptureStage.NicknameChoice } capture)
        {
            string symbol = capture.Stage == CampaignCaptureStage.DexNotice ? "sText_PkmnDataAddedToDex" : "sText_GiveNicknameCaptured";
            _safariPages = PretBattleText.Pages(_battleText, symbol, new Dictionary<string, string>(StringComparer.Ordinal)
                { ["B_OPPONENT_MON1_NAME"] = capture.Origin.DefaultName });
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

    private void ProcessSafariBattle(double delta) { _held = null; _safariElapsed += delta; }

    private bool SafariBattleInput(InputEventKey key)
    {
        if (key.Keycode is Key.Escape or Key.F12) return false;
        _held = null; _repeat = 0;
        if (!key.Pressed || key.Echo || _busy) return true;
        if (key.Keycode is Key.F5 or Key.F9)
        { _safariNotice = "You can't save or load\nduring a SAFARI GAME."; return true; }
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
            if (key.Keycode == Key.Enter && _safariElapsed > .1)
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
            if (!accepted) _safariNotice = "That action is not available\nright now.";
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
        if (battle.Capture?.Stage == CampaignCaptureStage.Naming) { DrawCaptureNamingPanel(); if (_safariNotice is not null) ItemMessageBox(_safariNotice); return; }
        DrawRect(new(0, 0, 240, 160), Colors.Black);
        _battleSkin.DrawBackground(this); DrawTexture(_session.State.IsSurfing ? _cityBattleScenes["water"] : _wildTerrain, Vector2.Zero);
        DrawTexture(_forestTrainerPortraits[_session.State.PlayerGender == 0 ? "red-back" : "leaf-back"], new(48, 48));
        if (battle.Capture is null)
        {
            var target = battle.State.Target;
            var sprite = _battleArt[(PresentedRenderSpecies(target), "front", target.IsShiny)];
            DrawTexture(sprite.Texture, sprite.Position);
            _battleSkin.DrawHealthbox(this, false, target.CurrentHp, target.Stats.MaxHp);
            SourceText(_healthFont, LabBattleUiSkin.OpponentName, battle.Origin.DefaultName);
            if (target.Gender != PokemonGender.Genderless)
            {
                float width = _healthFont.Font.GetStringSize(_healthFont.PrepareText(battle.Origin.DefaultName), fontSize: _healthFont.Size).X;
                SourceText(target.Gender == PokemonGender.Female ? _healthFemale : _healthMale,
                    LabBattleUiSkin.OpponentName + new Vector2(width, 0), target.Gender == PokemonGender.Female ? "♀" : "♂");
            }
            SourceText(_healthFont, LabBattleUiSkin.OpponentLevelWindow + new Vector2(5 * (3 - target.Level.ToString().Length), 0), _healthFont.LevelPrefix + target.Level);
        }
        // Source wording/counter, using the shared source frame until the dedicated Safari healthbox is imported.
        _battleSkin.DrawStandardFrame(this, new(128, 72, 112, 40));
        SourceText(_sourceSmall, new(140, 79), "SAFARI BALLS");
        SourceText(_sourceSmall, new(157, 94), "Left: " + battle.State.BallsRemaining.ToString().PadLeft(2));
        _battleSkin.DrawBattleMessage(this);
        if (battle.Capture is null && battle.Phase == CampaignBattlePhase.ChooseMove)
        {
            _battleSkin.DrawActionMenu(this); WildLines("What will\n" + _session.State.PlayerName + " do?");
            for (int i = 0; i < SafariLabels.Length; i++) SourceText(_battleMenuFont, LabBattleUiSkin.ActionText(i), SafariLabels[i]);
            _uiCorrections.DrawSelectionCursor(this, false, _safariCursor);
        }
        else if (!_safariPages.IsEmpty)
        {
            string text = _safariPages[_safariTextPage]; WildLines(text);
            if (battle.Capture?.Stage == CampaignCaptureStage.NicknameChoice && _safariTextPage + 1 >= _safariPages.Length)
            {
                _battleSkin.DrawStandardFrame(this, new(184, 64, 56, 48));
                SourceText(_sourceNormal, new(200, 72), "YES"); SourceText(_sourceNormal, new(200, 88), "NO");
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
