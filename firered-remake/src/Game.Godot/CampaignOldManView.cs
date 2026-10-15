using System.Text;
using Game.Application;
using Game.Content;
using Game.Domain.Tutorials;
using Godot;

/// <summary>Non-interactive presentation of the already committed Old Man result. No battle rules or player inventory writes.</summary>
public partial class CampaignView
{
    private OldManArtDocument? _oldManArt;
    private readonly Dictionary<string, ImageTexture> _oldManTextures = new(StringComparer.Ordinal);
    private string? _oldManWaitId;
    private OldManTutorialEvent? _oldManEvent;
    private string _oldManText = "";
    private double _oldManElapsed;
    private int _oldManDurationTicks;
    private bool _oldManCompletionSent, _oldManShakeCueSent;
    // These timings bound the visual adapter, not GBA callback/VBlank equivalence.
    private const int OldManThrowAdapterTicks = 132, OldManShakeAdapterTicks = 41, OldManReturnAdapterTicks = 16;
    private int OldManTick => (int)Math.Floor(_oldManElapsed * 60 + 1e-9);
    private string OldManWeedleTexture => _session.State.OldManTutorial!.Result.Weedle.IsShiny ? "weedle-front-shiny" : "weedle-front-normal";

    private void LoadOldManPresentation()
    {
        foreach (var texture in _oldManTextures.Values) texture.Dispose();
        _oldManTextures.Clear(); _oldManArt = null; ResetOldManPresentation();
        if (!_catalog.OldManTutorialEnabled) return;
        var directory = System.IO.Path.Combine(_root, "content/generated/sprites/pokemon/old-man");
        var document = PretOldManArt.Load(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "old-man-art.json")));
        try
        {
            foreach (var asset in document.Assets)
            {
                using var image = new Image();
                if (image.LoadPngFromBuffer(PretOldManArt.ReadTexture(directory, asset)) != Error.Ok
                    || image.GetWidth() != asset.Width || image.GetHeight() != asset.Height || image.GetFormat() != Image.Format.Rgba8)
                    throw new InvalidDataException("Old Man texture pixels differ: " + asset.Id);
                _oldManTextures.Add(asset.Id, ImageTexture.CreateFromImage(image));
            }
            _oldManArt = document;
        }
        catch
        {
            foreach (var texture in _oldManTextures.Values) texture.Dispose();
            _oldManTextures.Clear(); throw;
        }
    }

    private void ResetOldManPresentation()
    {
        _oldManWaitId = null; _oldManEvent = null; _oldManText = ""; _oldManElapsed = 0;
        _oldManDurationTicks = 0; _oldManCompletionSent = false; _oldManShakeCueSent = false;
    }

    // The only supported text controls are those present in the four authenticated tutorial strings.
    // Do not strip arbitrary braces: an added engine operation must fail until deliberately implemented.
    private static (string Text, int Pause, bool WaitSe, string? Music) ParseOldManText(string raw, string weedle, string ball)
    {
        var text = new StringBuilder(); int pause = 0; bool waitSe = false; string? music = null;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '{')
            {
                int end = raw.IndexOf('}', i + 1);
                if (end < 0) throw new InvalidDataException("Unterminated Old Man text control.");
                var token = raw[(i + 1)..end];
                switch (token)
                {
                    case "B_OPPONENT_MON1_NAME": text.Append(weedle); break;
                    case "B_LAST_ITEM": text.Append(ball); break;
                    case "PAUSE 127": pause += 127; break;
                    case "WAIT_SE": waitSe = true; break;
                    case "PLAY_BGM MUS_CAUGHT": music = "MUS_CAUGHT"; break;
                    default: throw new InvalidDataException("Unsupported Old Man text control: " + token);
                }
                i = end;
            }
            else if (c is '}' or '\\') throw new InvalidDataException("Unsupported Old Man text syntax.");
            else text.Append(c);
        }
        return (text.ToString(), pause, waitSe, music);
    }

    private void SynchronizeOldManPresentation()
    {
        if (_session.State.OldManTutorial is not { } tutorial)
        { ResetOldManPresentation(); return; }
        if (!_catalog.OldManTutorialEnabled || _oldManArt is null || _catalog.OldManTutorial is null)
            throw new InvalidDataException("Old Man presentation profile is not loaded.");
        if (_oldManWaitId == tutorial.WaitId) return; // Save rejection, repeated sync and repeated input cannot restart a timer.
        _oldManWaitId = tutorial.WaitId; _oldManEvent = tutorial.CurrentEvent; _oldManElapsed = 0; _oldManCompletionSent = false; _oldManShakeCueSent = false;
        _oldManText = "";
        var kind = tutorial.CurrentEvent.Kind;
        _oldManDurationTicks = kind switch
        {
            OldManTutorialEventKind.WildAppeared or OldManTutorialEventKind.Caught => 127,
            // Source SHORT pause (32) + LONG message wait (64); stat-animation rendering remains an adapter.
            OldManTutorialEventKind.IntimidateActivated => 96,
            OldManTutorialEventKind.ActionPrompt or OldManTutorialEventKind.MoveCursorToBag => 64,
            OldManTutorialEventKind.OpenDemoBag or OldManTutorialEventKind.SwitchToKeyItems
                or OldManTutorialEventKind.SwitchToPokeBalls or OldManTutorialEventKind.SelectPokeBall => 102,
            OldManTutorialEventKind.ThrowPokeBall => OldManThrowAdapterTicks,
            OldManTutorialEventKind.BallShake => OldManShakeAdapterTicks,
            OldManTutorialEventKind.ReturnToField => OldManReturnAdapterTicks,
            _ => throw new InvalidDataException("Hidden or unsupported Old Man presentation event: " + kind)
        };
        string? symbol = kind switch
        {
            OldManTutorialEventKind.WildAppeared or OldManTutorialEventKind.ThrowPokeBall or OldManTutorialEventKind.Caught => tutorial.CurrentEvent.SourceSymbol,
            OldManTutorialEventKind.ActionPrompt or OldManTutorialEventKind.MoveCursorToBag => "gText_WhatWillOldManDo",
            OldManTutorialEventKind.BallShake => "sText_OldManUsedItem",
            _ => null
        };
        if (symbol is not null)
        {
            var source = _catalog.OldManTutorial.Texts[symbol];
            var parsed = ParseOldManText(source.Text, "WEEDLE", "POKé BALL");
            _oldManText = parsed.Text;
            if (parsed.Pause > 0) _oldManDurationTicks = parsed.Pause;
            // Dummy audio has no active voice to wait on. Keep both source controls observable; no claim of audible output.
            if (parsed.WaitSe) OldManCue("WAIT_SE", "source-text-control");
            if (parsed.Music is not null) OldManCue(parsed.Music, "source-text-control");
        }
        if (kind == OldManTutorialEventKind.IntimidateActivated)
        {
            var values = new Dictionary<string,string>(StringComparer.Ordinal)
            {
                ["B_SCR_ACTIVE_NAME_WITH_PREFIX"] = tutorial.PartyBefore[tutorial.HiddenPlayerIndex].Nickname,
                ["B_SCR_ACTIVE_ABILITY"] = "INTIMIDATE", ["B_DEF_NAME_WITH_PREFIX"] = "Wild WEEDLE"
            };
            _oldManText = string.Join('\n', PretBattleText.Pages(PretBattleText.Load(_root), "sText_PkmnCutsAttackWith", values));
        }
        if (kind is OldManTutorialEventKind.MoveCursorToBag or OldManTutorialEventKind.OpenDemoBag
            or OldManTutorialEventKind.SelectPokeBall)
            OldManCue("SE_SELECT", "source-controller");
        if (kind is OldManTutorialEventKind.SwitchToKeyItems or OldManTutorialEventKind.SwitchToPokeBalls) OldManCue("SE_BAG_POCKET", "source-controller");
        if (kind == OldManTutorialEventKind.ThrowPokeBall) OldManCue("SE_SELECT", "source-bag-close");
        Write("oldManStage", new { waitId = _oldManWaitId, kind = kind.ToString(), tutorial.CurrentEvent.SourceTick,
            tutorial.CurrentEvent.Clock, tutorial.CurrentEvent.Value, durationTicks = _oldManDurationTicks,
            localAnimationAdapter = kind is OldManTutorialEventKind.ThrowPokeBall or OldManTutorialEventKind.BallShake or OldManTutorialEventKind.ReturnToField,
            shiny = tutorial.Result.Weedle.IsShiny, hiddenPartyIndex = tutorial.HiddenPlayerIndex });
    }

    private void OldManCue(string symbol, string origin, int? sourceTick = null)
    {
        if(symbol.StartsWith("MUS_",StringComparison.Ordinal))SourceMusic(symbol);
        else if(symbol.StartsWith("SE_",StringComparison.Ordinal))SourceSound(symbol);
        Write("oldManCue",new{symbol,origin,waitId=_oldManWaitId,tick=sourceTick??OldManTick,sourceCommit=PretOldManArt.SourceCommit,audibleVerified=false});
    }

    private void ProcessOldManPresentation(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        var tutorial = _session.State.OldManTutorial;
        if (tutorial is null) { ResetOldManPresentation(); return; }
        // A stale callback may only synchronize the new stage, never spend its old timer on that stage.
        if (_oldManWaitId != tutorial.WaitId) { SynchronizeOldManPresentation(); return; }
        if (_oldManCompletionSent) return;
        _oldManElapsed = Math.Min(_oldManDurationTicks / 60.0, _oldManElapsed + delta);
        if (_oldManEvent?.Kind == OldManTutorialEventKind.BallShake && !_oldManShakeCueSent && OldManTick >= 31)
        { _oldManShakeCueSent = true; OldManCue("SE_BALL", "source-shake-cue", 31); }
        if (OldManTick < _oldManDurationTicks) return;
        string completedWaitId = _oldManWaitId!;
        _oldManCompletionSent = true;
        Apply(new CampaignCommand("old_man_presented", Text: completedWaitId));
        // Deliberately no loop: excess wall time never completes a newly entered wait.
    }

    private bool TryOldManInput(InputEventKey key)
    {
        if (_session.State.OldManTutorial is null || key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape) return false;
        _held = null;
        return true; // A/B/directions never select actions, skip text, consume inventory or cancel the demonstration.
    }

    private int OldManBackFrame
    {
        get
        {
            if (_oldManEvent?.Kind != OldManTutorialEventKind.ThrowPokeBall) return 0;
            int tick = OldManTick;
            foreach (var frame in _catalog.OldManTutorial!.OldManThrowFrames)
            { if (tick < frame.Ticks) return frame.Frame; tick -= frame.Ticks; }
            return 0;
        }
    }

    private void DrawOldManPresentation()
    {
        if (_session.State.OldManTutorial is not { } tutorial || _oldManEvent is null) return;
        TextureFilter = TextureFilterEnum.Nearest;
        var kind = _oldManEvent.Kind;
        if (kind is OldManTutorialEventKind.OpenDemoBag or OldManTutorialEventKind.SwitchToKeyItems
            or OldManTutorialEventKind.SwitchToPokeBalls or OldManTutorialEventKind.SelectPokeBall)
        { DrawOldManDemoBag(kind); return; }
        DrawRect(new(0, 0, 240, 160), Colors.Black); _battleSkin.DrawBackground(this);
        DrawTexture(_wildTerrain, Vector2.Zero);
        // Source trainer CreateSprite center (80,80), 64px OAM; its unused table y_offset must not be added here.
        var layout = _oldManArt!.Layout;
        DrawTextureRectRegion(_oldManTextures["old-man-back"], new(layout.TrainerCenterX - 32, layout.TrainerCenterY - 32, 64, 64), new(0, OldManBackFrame * 64, 64, 64));
        var weedle = tutorial.Result.Weedle;
        bool showingWeedle = kind is not (OldManTutorialEventKind.BallShake or OldManTutorialEventKind.Caught or OldManTutorialEventKind.ReturnToField);
        if (showingWeedle)
        {
            double shrink = kind == OldManTutorialEventKind.ThrowPokeBall ? Math.Clamp((OldManTick - 68) / 28.0, 0, 1) : 0;
            if (shrink < 1)
            {
                var size = new Vector2(64, 64) * (float)(1 - shrink);
                DrawTextureRect(_oldManTextures[OldManWeedleTexture], new(new Vector2(layout.WeedleCenterX, layout.WeedleCenterY) - size / 2, size), false);
            }
            _battleSkin.DrawHealthbox(this, false, weedle.CurrentHp, weedle.Stats.MaxHp);
            SourceText(_healthFont, LabBattleUiSkin.OpponentName, "WEEDLE");
            float width = _healthFont.Font.GetStringSize(_healthFont.PrepareText("WEEDLE"), fontSize: _healthFont.Size).X;
            SourceText(_healthMale, LabBattleUiSkin.OpponentName + new Vector2(width, 0), "♂");
            SourceText(_healthFont, LabBattleUiSkin.OpponentLevelWindow + new Vector2(10, 0), _healthFont.LevelPrefix + "5");
        }
        if (kind is OldManTutorialEventKind.ThrowPokeBall or OldManTutorialEventKind.BallShake or OldManTutorialEventKind.Caught)
            DrawOldManBall(kind);
        bool actions = kind is OldManTutorialEventKind.ActionPrompt or OldManTutorialEventKind.MoveCursorToBag;
        if (actions)
        {
            _uiCorrections.ClearBattleMenu(this); _battleSkin.DrawActionMenu(this);
            string[] labels = ["FIGHT", "BAG", "POKéMON", "RUN"];
            for (int i = 0; i < labels.Length; i++) SourceText(_battleMenuFont, LabBattleUiSkin.ActionText(i), labels[i]);
            _uiCorrections.DrawSelectionCursor(this, false, kind == OldManTutorialEventKind.MoveCursorToBag ? 1 : 0);
        }
        else _battleSkin.DrawBattleMessage(this);
        var lines = _oldManText.Split('\n');
        if (lines.Length > 2) throw new InvalidDataException("Old Man battle text exceeds source two-line window.");
        for (int i = 0; i < lines.Length; i++) SourceText(_battleWhite, LabBattleUiSkin.BattleMessageText + new Vector2(0, i * 16), lines[i]);
        if (kind == OldManTutorialEventKind.ReturnToField)
            DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, (float)Math.Clamp(_oldManElapsed * 60 / OldManReturnAdapterTicks, 0, 1)));
    }

    private void DrawOldManBall(OldManTutorialEventKind kind)
    {
        int tick = OldManTick, frame = 0;
        var layout = _oldManArt!.Layout;
        var center = new Vector2(layout.BallTargetX, layout.BallTargetY + _oldManArt.BallTiming.FallAmplitude);
        if (kind == OldManTutorialEventKind.ThrowPokeBall)
        {
            if (tick < 24) return;
            if (tick < 58)
            {
                float p = (tick - 24) / 34f;
                center = new Vector2(layout.BallStartX, layout.BallStartY).Lerp(new(layout.BallTargetX, layout.BallTargetY), p) + new Vector2(0, -40 * Mathf.Sin(Mathf.Pi * p));
            }
            else if (tick < 96) { center = new(layout.BallTargetX, layout.BallTargetY); frame = tick < 63 ? 1 : 2; }
            else { float p = Math.Clamp((tick - 96) / 36f, 0, 1); center.Y = layout.BallTargetY + _oldManArt.BallTiming.FallAmplitude * p - 12 * Mathf.Abs(Mathf.Sin(p * Mathf.Pi * 4)) * (1 - p); }
        }
        else if (kind == OldManTutorialEventKind.BallShake)
        {
            // Source rests 31 ticks; movement/rotation here is a clearly bounded render adapter.
            int t = tick - 31;
            if (t >= 0 && t < 10) center.X += 4 * Mathf.Sin(t / 10f * Mathf.Tau);
        }
        DrawTextureRectRegion(_oldManTextures["poke-ball"], new(center - new Vector2(8, 8), new Vector2(16, 16)), new(0, frame * 16, 16, 16));
    }

    private void DrawOldManDemoBag(OldManTutorialEventKind kind)
    {
        // Never read State.Inventory: the tutorial bag is its own source-defined Potion1/empty keys/Ball1 display.
        int pocket = kind == OldManTutorialEventKind.OpenDemoBag ? 0 : kind == OldManTutorialEventKind.SwitchToKeyItems ? 1 : 2;
        if (pocket == 0) _itemSkin.DrawBag(this);
        else DrawQuestPocketAsset(pocket == 1 ? "bag-key-items-background" : "bag-poke-balls-background");
        string title = pocket == 0 ? _itemSkin.Text("gText_Items2") : _questPocketUi!.Texts[pocket == 1 ? "gText_KeyItems2" : "gText_PokeBalls2"];
        float width = _sourceWhite.Font.GetStringSize(_sourceWhite.PrepareText(title), fontSize: _sourceWhite.Size).X;
        SourceText(_sourceWhite, OpeningItemUiSkin.BagTitleCenter - new Vector2((int)Math.Ceiling(width / 2), 0), title);
        if (pocket > 0) DrawQuestPocketAsset("pocket-left-arrow");
        if (pocket < 2) _itemSkin.DrawPocketRightArrow(this);
        var ball = _questPocketUi!.Items.Single(x => x.SourceSymbol == "ITEM_POKE_BALL");
        int count = pocket == 1 ? 0 : 1;
        if (count == 1)
        {
            SourceText(_sourceNormal, OpeningItemUiSkin.ListText(false, 0), pocket == 0 ? _itemSkin.PotionName : ball.Name);
            SourceText(_sourceSmall, OpeningItemUiSkin.QuantityText(false, 0), _itemSkin.FormatText("gText_TimesStrVar1", "  1"));
            if (pocket == 0) _itemSkin.DrawPotion(this); else DrawQuestPocketAsset(ball.Icon);
        }
        SourceText(_sourceNormal, OpeningItemUiSkin.ListText(false, count), _itemSkin.Text("gFameCheckerText_Cancel"));
        SourceText(_sourceNormal, OpeningItemUiSkin.ListCursor(false, 0), _itemSkin.Text("gText_SelectorArrow2"));
        ItemLines(_sourceWhite, OpeningItemUiSkin.DescriptionText, pocket == 0 ? _itemSkin.PotionDescription : pocket == 1 ? _itemSkin.Text("gText_CloseBag") : ball.Description);
        if (kind == OldManTutorialEventKind.SelectPokeBall)
        {
            _battleSkin.DrawStandardFrame(this, OpeningItemUiSkin.BattleBagActionFrame);
            SourceText(_sourceNormal, OpeningItemUiSkin.BattleBagActionText(0), _itemSkin.Text("gOtherText_Use"));
            SourceText(_sourceNormal, OpeningItemUiSkin.BattleBagActionText(1), _itemSkin.Text("gFameCheckerText_Cancel"));
            SourceText(_sourceNormal, OpeningItemUiSkin.BattleBagActionCursor(0), _itemSkin.Text("gText_SelectorArrow2"));
            _itemSkin.DrawStandardFrame(this, OpeningItemUiSkin.BagSelectedFrame);
            ItemLines(_sourceNormal, OpeningItemUiSkin.BagSelectedText, _itemSkin.FormatText("gText_Var1IsSelected", ball.Name));
        }
    }
}
