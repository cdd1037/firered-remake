using Game.Application;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Godot;

/// <summary>Original NPC party selection and a bounded before/after trade presentation.
/// Uses the two real instances, source text and existing sprites. The original cable,
/// ball travel, background animation and source trade timing are not reproduced here.</summary>
public partial class CampaignView
{
    private int _npcTradeCursor;
    private ulong _npcTradeClosedFrame = ulong.MaxValue;
    private bool NpcTradeChoosing => _session?.State.NpcTrade is { Phase: CampaignNpcTradePhase.ChoosingParty } &&
        _session.State.Script.Wait?.Kind == StoryWaitKind.PartySelection;
    private bool NpcTradePresenting => _session?.State.NpcTrade is { Phase: CampaignNpcTradePhase.Presenting } &&
        _session.State.Script.Wait?.Kind == StoryWaitKind.Presentation;

    private void SynchronizeNpcTradePresentation(CampaignState? before, CampaignState after)
    {
        bool wasChoosing = before?.NpcTrade is { Phase: CampaignNpcTradePhase.ChoosingParty };
        bool choosing = after.NpcTrade is { Phase: CampaignNpcTradePhase.ChoosingParty };
        bool wasPresenting = before?.NpcTrade is { Phase: CampaignNpcTradePhase.Presenting };
        bool presenting = after.NpcTrade is { Phase: CampaignNpcTradePhase.Presenting };
        if (!wasChoosing && choosing) _npcTradeCursor = 0;
        if (wasChoosing != choosing || wasPresenting != presenting)
        {
            _held = null; _repeat = 0; ReleaseFieldTextInput();
            _fade = 16.0 / 60;
            if (wasChoosing && !choosing || wasPresenting && !presenting)
                _npcTradeClosedFrame = Engine.GetProcessFrames();
        }
    }

    private bool ConsumeNpcTradeTransitionInput(InputEvent input) => ConsumeServiceTransitionInput(input, _npcTradeClosedFrame);

    private bool NpcTradeInput(InputEventKey key)
    {
        if ((!NpcTradeChoosing && !NpcTradePresenting) || key.Keycode is Key.F5 or Key.F9 or Key.F12) return false;
        _held = null; _repeat = 0;
        // Presentation completion is the ordinary timed ack owned by CampaignView.
        // No key can cancel, finish early, select a second row or reach the field.
        if (NpcTradePresenting || !key.Pressed || key.Echo || _fade > 0) return true;
        int count = _session.State.Party.Length + 1;
        if (key.Keycode is Key.Up or Key.Left) _npcTradeCursor = (_npcTradeCursor + count - 1) % count;
        else if (key.Keycode is Key.Down or Key.Right) _npcTradeCursor = (_npcTradeCursor + 1) % count;
        else if (key.Keycode is Key.Backspace or Key.Escape or Key.B) Apply(new("npc_trade_cancel"));
        else if (key.Keycode is Key.Enter or Key.KpEnter or Key.A)
        {
            if (_npcTradeCursor == _session.State.Party.Length) Apply(new("npc_trade_cancel"));
            else if (_npcTradeCursor >= 0 && _npcTradeCursor < _session.State.Party.Length)
                Apply(new("npc_trade_choose", PartyIndex: _npcTradeCursor)
                    { TargetInstanceId = _session.State.Party[_npcTradeCursor].InstanceId });
        }
        return true;
    }

    private bool NavigateNpcTradePointer(Vector2 point, int direction)
    {
        if (!NpcTradeChoosing) return false;
        int row = ServicePartyPointerRow(point);
        if (row < 0) return false;
        if (direction != 0) return MouseNavigationKey(direction);
        if (_npcTradeCursor == row) return false;
        _npcTradeCursor = row; return true;
    }

    private bool MouseNpcTrade(Vector2 point)
    {
        if (NpcTradePresenting) return true;
        if (!NpcTradeChoosing) return false;
        int row = ServicePartyPointerRow(point);
        if (row < 0) return false;
        _npcTradeCursor = row; MouseKey(Key.Enter); return true;
    }

    // These are visual phases only. They neither replace nor mutate the party.
    private int NpcTradePresentationStage => Math.Clamp((int)(_waitElapsed * 60 * 4 /
        Math.Max(1, _session.State.PresentationTicks)), 0, 3);

    private string NpcTradeSourceText(string symbol) =>
        _catalog.NpcTradeContent?.Texts.GetValueOrDefault(symbol)
        ?? throw new InvalidDataException("NPC trade presentation text missing: " + symbol);

    private string NpcTradePresentationText()
    {
        var trade = _session.State.NpcTrade!;
        var outgoing = trade.Outgoing ?? throw new InvalidDataException("NPC trade outgoing instance missing.");
        var incoming = trade.Incoming ?? throw new InvalidDataException("NPC trade incoming instance missing.");
        string symbol = NpcTradePresentationStage switch
        {
            0 => "gText_XWillBeSentToY", 1 => "gText_ByeByeVar1",
            2 => "gText_XSentOverY", _ => "gText_TakeGoodCareOfX"
        };
        // trade_scene.c TradeBufferOTnameAndNicknames uses these exact bindings.
        string text = NpcTradeSourceText(symbol)
            .Replace("{STR_VAR_1}", incoming.OriginalTrainerName, StringComparison.Ordinal)
            .Replace("{STR_VAR_2}", outgoing.Nickname, StringComparison.Ordinal)
            .Replace("{STR_VAR_3}", incoming.Nickname, StringComparison.Ordinal);
        if (text.Contains('{') || text.Contains('}')) throw new InvalidDataException("Unrendered NPC trade source control token.");
        return text;
    }

    private bool DrawNpcTrade()
    {
        if (NpcTradeChoosing)
        {
            DrawServicePartyChooser(_npcTradeCursor,
                NpcTradeSourceText(NameRaterRules.ChooserPromptSymbol(_session.State.Party)), NpcTradeSourceText("gFameCheckerText_Cancel"));
            return true;
        }
        if (!NpcTradePresenting) return false;
        var trade = _session.State.NpcTrade!;
        var owned = (NpcTradePresentationStage < 2 ? trade.Outgoing : trade.Incoming)
            ?? throw new InvalidDataException("NPC trade display instance missing.");
        var appearance = PresentedAppearance(owned.Pokemon);
        var portrait = _battleArt[(appearance.Species, "front", appearance.Shiny)].Texture;
        DrawRect(new(0, 0, 240, 160), new Color("e8f0d8"));
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 112));
        DrawTexture(portrait, new(88, 24));
        SourceText(_sourceSmall, new(12, 8), owned.Nickname);
        SourceText(_sourceSmall, new(184, 8), "Lv" + owned.Pokemon.Level);
        _battleSkin.DrawStandardFrame(this, new(0, 112, 240, 48));
        var lines = NpcTradePresentationText().Split('\n');
        for (int i = 0; i < lines.Length; i++) SourceText(_sourceNormal, new(10, 118 + i * 16), lines[i]);
        if (_fade > 0) DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, (float)Math.Clamp(_fade / (16.0 / 60), 0, 1)));
        return true;
    }
}
