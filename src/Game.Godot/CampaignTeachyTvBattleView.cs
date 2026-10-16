using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Items;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.Tutorials;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Read-only shared battle view with source-controlled menus. A only advances
/// the narration printer; every battle choice comes from the application controller.</summary>
public partial class CampaignView
{
    private CampaignTeachyTvBattle? TeachyBattle => _session.State.TeachyTv?.Battle;
    private ImmutableArray<PartyPokemon> PresentedBattlePlayerParty => TeachyBattle is null ? _session.State.Party : _session.TeachyTvBattleParty;
    private string? _teachyBattleVisualKey, _teachyBattleVoiceSymbol;
    private readonly TeachyTvTextPresentation _teachyBattleVoiceText = new();
    private TeachyTvNormalPaletteClock? _teachyBattlePalette;
    private double _teachyBattleRemainder;
    private int _teachyBattleSoundSequence, _teachyBattleVoicePhase;
    private bool _teachyBattleAHeld, _teachyBattleVoiceHealthbox;
    private ulong _teachyBattleExitFrame = ulong.MaxValue;

    private void SynchronizeTeachyTvBattle()
    {
        if (TeachyBattle is not {} b)
        {
            if (_teachyBattleVisualKey is not null) _teachyBattleExitFrame = Engine.GetProcessFrames();
            _teachyBattleVisualKey = _teachyBattleVoiceSymbol = null; _teachyBattlePalette = null;
            _teachyBattleVoiceText.Clear(); _teachyBattleRemainder = 0; _teachyBattleSoundSequence = 0; _teachyBattleAHeld = false;
            return;
        }
        _held = null; _repeat = 0;
        if (b.SoundSequence != _teachyBattleSoundSequence)
        { _teachyBattleSoundSequence = b.SoundSequence; if (b.Sound is {} sound) SourceSound(sound); }
        string key = $"{b.Ticket}:{b.Stage}:{b.StepIndex}:{b.CurrentStep?.Event.Sequence}:{b.Voiceover.Next}:{b.Voiceover.Pending?.TextSymbol}:{b.SelectingSide}";
        if (_teachyBattleVisualKey != key)
        {
            _teachyBattleVisualKey = key; _wildElapsed = 0;
            if (b.Voiceover.Pending is {} voice && _teachyBattleVoiceSymbol != voice.TextSymbol)
            {
                _teachyBattleVoiceSymbol = voice.TextSymbol;
                _teachyBattleVoiceHealthbox = voice.Callback == "PokedudeAction_PrintMessageWithHealthboxPals";
                _teachyBattleVoicePhase = 0;
                _teachyBattlePalette = TeachyTvFieldPresentationClock.Begin(0xFFFFFF7F, 4, 0, 8);
                _teachyBattleVoiceText.Begin(voice.TextSymbol, _session.TeachyTvCatalog!.Texts[voice.TextSymbol].EncodedText,
                    _session.EffectiveOptions.TextSpeed switch { SourceTextSpeed.Slow => 8, SourceTextSpeed.Mid => 4, _ => 1 });
            }
            else if (b.Voiceover.Pending is null)
            { _teachyBattleVoiceSymbol = null; _teachyBattlePalette = null; _teachyBattleVoiceText.Clear(); }
            if (b.CurrentStep?.Event is SourcePokedudeCaughtEvent)
            {
                var raw = _catalog.OldManTutorial!.Texts["sText_GotchaPkmnCaught2"].Text;
                var parsed = ParseOldManText(raw, PresentedBattleSpeciesName(b.DisplayOpponent.SourceSpeciesId), "POKé BALL");
                _wildPages = [parsed.Text]; _wildPage = 0;
            }
        }
        _wildCursor = b.VisibleCursor;
        _wildMenu = b.Stage == CampaignTeachyTvBattleStage.ChooseMove && b.SelectingSide == 0 ? "moves" : "actions";
    }

    private bool ProcessTeachyTvBattle(double delta)
    {
        if (TeachyBattle is null) return false;
        if (ProcessCommittedMoveAnimation(delta)) { QueueRedraw(); return true; }
        SynchronizeTeachyTvBattle();
        _wildElapsed += delta; _teachyBattleRemainder = Math.Min(_teachyBattleRemainder + delta, .25);
        while (_teachyBattleRemainder >= 1d / 60 && TeachyBattle is {} b)
        {
            _teachyBattleRemainder -= 1d / 60;
            if (b.Voiceover.Pending is {} voice)
            {
                if (_teachyBattlePalette is { Active: true } palette)
                    _teachyBattlePalette = TeachyTvFieldPresentationClock.Update(palette);
                else if (_teachyBattleVoicePhase == 0)
                {
                    _teachyBattleVoicePhase = _teachyBattleVoiceHealthbox ? 1 : 2;
                    if (_teachyBattleVoiceHealthbox) _teachyBattlePalette = TeachyTvFieldPresentationClock.Begin(0, 4, 8, 0);
                }
                else if (_teachyBattleVoicePhase == 1) _teachyBattleVoicePhase = 2;
                else if (_teachyBattleVoicePhase == 2)
                {
                    _teachyBattleVoiceText.AdvanceFrame();
                    if (_teachyBattleVoiceText.Complete && !b.VoiceoverPrinted)
                        Apply(new("teachy_tv_battle_presented", Text: b.WaitId));
                }
                else if (_teachyBattleVoicePhase == 3 && _teachyBattleVoiceHealthbox)
                {
                    _teachyBattleVoicePhase = 4;
                    _teachyBattlePalette = TeachyTvFieldPresentationClock.Begin(0xFFFFFF7F, 4, 8, 0);
                }
                else if (_teachyBattleVoicePhase is 3 or 4)
                {
                    Apply(new("teachy_tv_battle_confirm", Text: b.WaitId));
                }
                continue;
            }
            if (b.Stage == CampaignTeachyTvBattleStage.Events)
            {
                double pause = b.CurrentStep?.Event is SourcePokedudeCaughtEvent ? 127d / 60 : _wildPages.IsEmpty ? .45 : 64d / 60;
                if (_wildElapsed < pause) break;
                if (_wildPage + 1 < _wildPages.Length) { _wildPage++; _wildElapsed = 0; }
                else AcknowledgeWildAttackText();
                break;
            }
            if (b.Stage is CampaignTeachyTvBattleStage.Boundary or CampaignTeachyTvBattleStage.Finished) break;
            Apply(new("teachy_tv_battle_tick", Text: b.WaitId));
        }
        QueueRedraw(); return true;
    }

    private bool TryTeachyTvBattleInput(InputEvent input)
    {
        if (TeachyBattle is not {} b)
        {
            if (_teachyBattleExitFrame != Engine.GetProcessFrames()) return false;
            GetViewport().SetInputAsHandled(); return true;
        }
        bool? a = null, cancel = null;
        if (input is InputEventKey key && !key.Echo)
        {
            if (key.Keycode is Key.Enter or Key.Z) a = key.Pressed;
            if (key.Keycode is Key.Backspace or Key.X) cancel = key.Pressed;
            if (key.Pressed && key.Keycode == Key.Escape) RequestQuit();
        }
        else if (input is InputEventJoypadButton joy)
        { if (joy.ButtonIndex == JoyButton.A) a = joy.Pressed; if (joy.ButtonIndex == JoyButton.B) cancel = joy.Pressed; }
        else if (input is InputEventMouseButton mouse)
        { if (mouse.ButtonIndex == MouseButton.Left) a = mouse.Pressed; if (mouse.ButtonIndex == MouseButton.Right) cancel = mouse.Pressed; }
        if (cancel == true) Apply(new("teachy_tv_battle_cancel", Text: b.WaitId));
        else if (a is {} pressed)
        {
            bool fresh = pressed && !_teachyBattleAHeld; _teachyBattleAHeld = pressed;
            if (_teachyBattleVoicePhase == 2 && b.Voiceover.Pending is not null)
            {
                if (fresh && b.VoiceoverPrinted && _teachyBattleVoiceText.Complete)
                {
                    SourceSound("SE_SELECT"); _teachyBattleVoicePhase = 3;
                    _teachyBattlePalette = _teachyBattleVoiceHealthbox
                        ? TeachyTvFieldPresentationClock.Begin(0, 4, 0, 8)
                        : TeachyTvFieldPresentationClock.Begin(0xFFFFFF7F, 4, 8, 0);
                }
                else _teachyBattleVoiceText.Confirm(pressed);
            }
        }
        _held = null; _repeat = 0; QueueRedraw(); GetViewport().SetInputAsHandled(); return true;
    }

    // Shared move animation completion uses the same current correlated event as the
    // non-animated path. It cannot route a TV event to ordinary owned-battle commands.
    private void AcknowledgePresentedSingleBattle()
    {
        if (TeachyBattle is {} b) Apply(new("teachy_tv_battle_presented", Text: b.WaitId));
        else Apply(new("ack"));
    }

    private bool TeachyTvHidesCaughtOpponent => TeachyBattle is {} b &&
        (b.CurrentStep?.Event is SourcePokedudeCaughtEvent || b.Stage == CampaignTeachyTvBattleStage.Ending && b.Frame.Outcome == WildBattleOutcome.Caught);

    private bool DrawTeachyTvBattle()
    {
        if (TeachyBattle is not {} b) return false;
        if (b.Stage == CampaignTeachyTvBattleStage.Bag && b.Voiceover.Pending is null) DrawTeachyTvBattleBag(b);
        else if (b.Stage is CampaignTeachyTvBattleStage.Party or CampaignTeachyTvBattleStage.MedicineTarget && b.Voiceover.Pending is null)
            DrawTeachyTvBattleParty(b);
        else
        {
            DrawWildBattle();
            if (TeachyTvHidesCaughtOpponent && _oldManTextures.TryGetValue("poke-ball", out var ball))
                DrawTextureRectRegion(ball, new(168, 58, 16, 16), new(0, 0, 16, 16));
            if (b.Stage == CampaignTeachyTvBattleStage.Boundary)
            { _battleSkin.DrawBattleMessage(this); WildLines("This source demo cannot continue.\nPress B to return to TEACHY TV."); }
        }
        if (b.Voiceover.Pending is not null) DrawTeachyTvBattleVoiceover(b);
        return true;
    }

    private void DrawTeachyTvBattleVoiceover(CampaignTeachyTvBattle b)
    {
        int fade = _teachyBattlePalette?.BackgroundCoefficient ?? 8;
        if (_teachyBattleVoiceHealthbox && _teachyBattleVoicePhase is 1 or 2 or 3) fade = 8;
        if (fade > 0) DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, fade / 16f));
        if (_teachyBattleVoiceHealthbox && _teachyBattleVoicePhase is 1 or 2 or 3)
        {
            DrawWildHealth(b.DisplayPlayer, WildBattleSide.Player, b.DisplayPlayer.CurrentHp);
            int hpFade = _teachyBattlePalette?.BackgroundCoefficient ?? 0;
            if (_teachyBattleVoicePhase == 2) hpFade = 0;
            if (hpFade > 0) DrawRect(new(126, 72, 114, 40), new Color(0, 0, 0, hpFade / 16f));
        }
        if (_teachyBattleVoicePhase < 2) return;
        // Authenticated menu_message tiles are shared by the original Oak/Pokedude window.
        DrawTexture(_battleSkin.Texture("dialogue"), new(0, 112));
        if (_teachyBattleVoiceText.Page is not {} page) return;
        foreach (var line in page.Lines)
        {
            int count = Math.Clamp(_teachyBattleVoiceText.VisibleLength - line.CharacterStart, 0, line.Text.Length);
            int y = 121 + line.LineIndex * 15 - _teachyBattleVoiceText.ScrollPixels;
            if (count > 0 && y >= 120 && y < 152) SourceText(_sourceNormal, new(16, y), line.Text[..count]);
        }
        if (_teachyBattleVoiceText.AtPrompt)
        {
            var last = page.Lines[^1];
            float width = _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(last.Text), fontSize: _sourceNormal.Size).X;
            _textCursor.Draw(this, new(16 + width, 121 + last.LineIndex * 15), _teachyBattleVoiceText.PromptFrames);
        }
    }

    private static InventoryState TeachyTvTemporaryBag => new([new(new("pret:ITEM_POTION"), 1), new(new("pret:ITEM_ANTIDOTE"), 1)], [], new(42, 30, 999))
    { KeyItems = [new(new("pret:ITEM_TEACHY_TV"), 1), new(new("pret:ITEM_TM_CASE"), 1)],
        Balls = [new(new("pret:ITEM_POKE_BALL"), 5), new(new("pret:ITEM_GREAT_BALL"), 1), new(new("pret:ITEM_NEST_BALL"), 1)] };
    private void DrawTeachyTvBattleBag(CampaignTeachyTvBattle b)
    {
        var bag = TeachyTvTemporaryBag;
        if (b.BagPocket == "items") DrawItemList(bag, "bag", b.VisibleCursor);
        else
        {
            bool balls = b.BagPocket == "balls";
            DrawQuestPocketAsset(balls ? "bag-poke-balls-background" : "bag-key-items-background");
            var title = _questPocketUi!.Texts[balls ? "gText_PokeBalls2" : "gText_KeyItems2"];
            float width = _sourceWhite.Font.GetStringSize(_sourceWhite.PrepareText(title), fontSize: _sourceWhite.Size).X;
            SourceText(_sourceWhite, OpeningItemUiSkin.BagTitleCenter - new Vector2((int)Math.Ceiling(width / 2), 0), title);
            DrawQuestPocketAsset("pocket-left-arrow"); if (!balls) _itemSkin.DrawPocketRightArrow(this);
            var stacks = (balls ? bag.Balls : bag.KeyItems)!.Value;
            for (int i = 0; i < stacks.Length; i++)
            {
                SourceText(_sourceNormal, OpeningItemUiSkin.ListText(false, i), QuestPocketItem(stacks[i]).Name);
                if (balls) SourceText(_sourceSmall, OpeningItemUiSkin.QuantityText(false, i), _itemSkin.FormatText("gText_TimesStrVar1", stacks[i].Quantity.ToString().PadLeft(3)));
            }
            SourceText(_sourceNormal, OpeningItemUiSkin.ListText(false, stacks.Length), _itemSkin.Text("gFameCheckerText_Cancel"));
            SourceText(_sourceNormal, OpeningItemUiSkin.ListCursor(false, b.VisibleCursor), _itemSkin.Text("gText_SelectorArrow2"));
            var selected = QuestPocketItem(stacks[b.VisibleCursor]); DrawQuestPocketAsset(selected.Icon);
            ItemLines(_sourceWhite, OpeningItemUiSkin.DescriptionText, selected.Description);
        }
        if (b.ContextMenu) DrawItemActionPopup(false, [_itemSkin.Text("gOtherText_Use"), _itemSkin.Text("gFameCheckerText_Cancel")], 0,
            b.Lesson == 1 ? InventoryName(new("pret:ITEM_ANTIDOTE")) : QuestPocketItem(TeachyTvTemporaryBag.Balls!.Value[0]).Name);
    }

    private void DrawTeachyTvBattleParty(CampaignTeachyTvBattle b)
    {
        DrawRect(new(0, 0, 240, 160), Colors.Black);
        DrawCaptureParty(_session.TeachyTvBattlePresentation!, true, true);
        if(b.Stage==CampaignTeachyTvBattleStage.MedicineTarget)
            DrawNativeParty(PresentedBattlePlayerParty,_wildCursor,"Use on which POKéMON?");
        if (b.ContextMenu)
        {
            _battleSkin.DrawStandardFrame(this, new(152, 40, 88, 64));
            SourceText(_sourceNormal, new(168, 48), "SHIFT"); SourceText(_sourceNormal, new(168, 64), "SUMMARY");
            SourceText(_sourceNormal, new(168, 80), "CANCEL"); SourceText(_sourceNormal, new(156, 48), "▶");
        }
    }
}
