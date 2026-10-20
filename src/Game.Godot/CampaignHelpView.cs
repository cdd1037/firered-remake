using Game.Application;
using Game.Content;
using Game.Domain.Items;
using Godot;

public partial class CampaignView
{
    private HelpCatalog? _helpCatalog;
    private readonly SourceHelpPresentation _help = new();
    private CampaignButton _helpHeld;
    private double _helpRepeat;
    private Control? _helpReturnFocus;
    private ulong _helpClosedFrame = ulong.MaxValue;
    private void LoadHelpPresentation() => _helpCatalog = HelpCatalog.Load(_root, battleText: _battleText);

    private HelpProgress HelpProgressSnapshot()
    {
        var state = _session.State;
        var flags = state.Script.Flags.Where(p => p.Value).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        var inventory = state.Inventory;
        IEnumerable<InventoryStack> bag = inventory is null ? [] : inventory.BagItems
            .Concat(inventory.KeyItems ?? []).Concat(inventory.Balls ?? [])
            .Concat(inventory.Machines ?? []).Concat(inventory.Berries ?? []);
        var items = bag.Where(s => s.Quantity > 0).Select(s => _catalog.Inventory.Items[s.Item].SourceItemSymbol).ToHashSet(StringComparer.Ordinal);
        int caught = state.CaughtSpecies.Count(id => (_pokedexMenu?.Entries[id].NationalNumber ?? id) <= 151);
        return new(flags, items, caught, state.PlayerName);
    }
    private string CurrentHelpContext(HelpProgress progress)
    {
        var state = _session.State;
        if (_nicknameEdit.Visible) return "HELPCONTEXT_NAMING_SCREEN";
        if (PlayerEntryActive) return _entryMode == "menu" ? "HELPCONTEXT_TITLE_SCREEN" : "HELPCONTEXT_NEW_GAME";
        // Native SetHelpContext deliberately retains battle context inside Bag/Party/Summary.
        if (state.SafariBattle is not null) return "HELPCONTEXT_SAFARI_BATTLE";
        if (DoubleBattleVisible) return "HELPCONTEXT_TRAINER_BATTLE_DOUBLE";
        if (ForestTrainerCombatVisible || state.Battle is not null) return "HELPCONTEXT_TRAINER_BATTLE_SINGLE";
        if (state.WildBattle is not null) return "HELPCONTEXT_WILD_BATTLE";
        if (_fieldMenuMode.StartsWith("pokedex", StringComparison.Ordinal)) return "HELPCONTEXT_POKEDEX";
        if (_fieldMenuMode == "trainer-card") return _trainerCardBack ? "HELPCONTEXT_TRAINER_CARD_BACK" : "HELPCONTEXT_TRAINER_CARD_FRONT";
        if (_fieldMenuMode == "options") return "HELPCONTEXT_OPTIONS";
        if (_fieldMenuMode.StartsWith("save", StringComparison.Ordinal)) return "HELPCONTEXT_SAVE";
        if (_fieldMenuMode == "summary" || state.Pc?.Summary is not null)
        {
            int page = state.Pc?.Summary is {} summary ? (int)summary.Page : _fieldSummaryPage;
            return page switch { 0 => "HELPCONTEXT_POKEMON_INFO", 1 => "HELPCONTEXT_POKEMON_SKILLS", _ => "HELPCONTEXT_POKEMON_MOVES" };
        }
        if (_fieldMenuMode is "party" or "party-action") return "HELPCONTEXT_PARTY_MENU";
        if (state.ItemMenu is {} menu)
        {
            if (menu.Origin == "bag") return "HELPCONTEXT_BAG";
            bool bedroom = state.HelpContext.StartsWith("HELPCONTEXT_BEDROOM_PC", StringComparison.Ordinal);
            if (menu.View == "top") return bedroom ? "HELPCONTEXT_BEDROOM_PC" : "HELPCONTEXT_PC";
            if (menu.View.Contains("mail", StringComparison.Ordinal)) return bedroom ? "HELPCONTEXT_BEDROOM_PC_MAILBOX" : "HELPCONTEXT_PLAYERS_PC_MAILBOX";
            return bedroom ? "HELPCONTEXT_BEDROOM_PC_ITEMS" : "HELPCONTEXT_PLAYERS_PC_ITEMS";
        }
        if (state.Pc is {} pc) return pc.View switch
        {
            CampaignPcView.Main => "HELPCONTEXT_PC", CampaignPcView.HallOfFame => "HELPCONTEXT_PC_MISC",
            CampaignPcView.PlayerItems => "HELPCONTEXT_PLAYERS_PC_ITEMS", _ => "HELPCONTEXT_BILLS_PC"
        };
        if (state.HelpContext != "map") return state.HelpContext;
        return _helpCatalog!.ResolveMapContext(_catalog.MapDefinition(state.World.Player.MapId).SourceMapSymbol, state.IsSurfing, progress.Flags);
    }
    private bool TryHelpInput(InputEvent input)
    {
        if (!_help.Active && _helpClosedFrame == Engine.GetProcessFrames())
        { GetViewport().SetInputAsHandled(); return true; }
        if (!_help.Active)
        {
            if (input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q or Key.W }) return false;
            if (_buttons.Mode != Game.Domain.Options.SourceButtonMode.Help) return false;
            if (_helpCatalog is null || !_session.CanOpenHelp || _busy || _fade > 0 ||
                _session.State.EggHatch is not null || _session.State.FlyEffect is not null)
            { SourceSound("SE_HELP_ERROR"); GetViewport().SetInputAsHandled(); return true; }
            var progress = HelpProgressSnapshot(); string context = CurrentHelpContext(progress);
            bool seen = _session.SawHelpIntroduction;
            if (!_helpCatalog.SupportsContext(context) || !_help.Open(_helpCatalog.Topics(context, progress), seen))
            { SourceSound("SE_HELP_ERROR"); GetViewport().SetInputAsHandled(); return true; }
            if (!seen && !Apply(new("help_intro_seen"))) { _help.Close(); GetViewport().SetInputAsHandled(); return true; }
            _helpReturnFocus = GetViewport().GuiGetFocusOwner(); _helpReturnFocus?.ReleaseFocus();
            if (NamingScreenActive) _nicknameEdit.Visible = false;
            _held = null; ReleaseFieldTextInput(); ReleaseModalHeldInput();
            _helpHeld = CampaignButton.None; _helpRepeat = 0;
            _sourceAudio?.StopSoundEffects();
            _sourceAudio?.SetHelpDucking(!context.Equals("HELPCONTEXT_POKEDEX", StringComparison.Ordinal));
            SourceSound("SE_HELP_OPEN"); QueueRedraw(); GetViewport().SetInputAsHandled(); return true;
        }
        if (input is InputEventKey { Echo: false } key)
        {
            CampaignButton button = key.Keycode switch
            {
                Key.Enter or Key.KpEnter => CampaignButton.A, Key.Backspace => CampaignButton.B,
                Key.Up => CampaignButton.Up, Key.Down => CampaignButton.Down,
                Key.Left => CampaignButton.Left, Key.Right => CampaignButton.Right,
                Key.Q => CampaignButton.L, Key.W => CampaignButton.R, _ => CampaignButton.None
            };
            if (key.Pressed)
            {
                PressHelp(button);
                if (button is CampaignButton.Up or CampaignButton.Down or CampaignButton.Left or CampaignButton.Right)
                { _helpHeld = button; _helpRepeat = 20; }
            }
            else if (_helpHeld == button) _helpHeld = CampaignButton.None;
        }
        GetViewport().SetInputAsHandled(); QueueRedraw(); return true;
    }
    private void PressHelp(CampaignButton button)
    {
        if (!_help.Press(button)) return;
        if (!_help.Active) CloseHelpPresentation(); else SourceSound("SE_SELECT");
    }
    private bool ProcessHelpPresentation(double delta)
    {
        if (!_help.Active) return false;
        _help.Advance(delta);
        if (_helpHeld != CampaignButton.None)
        {
            _helpRepeat -= delta * SourceAudioTiming.SourceFramesPerSecond;
            if (_helpRepeat <= 0) { PressHelp(_helpHeld); _helpRepeat = 5; }
        }
        QueueRedraw(); return true;
    }
    private void CloseHelpPresentation()
    {
        _help.Close(); _helpHeld = CampaignButton.None; _sourceAudio?.SetHelpDucking(false);
        SourceSound("SE_HELP_CLOSE"); _helpClosedFrame = Engine.GetProcessFrames();
        if (NamingScreenActive) _nicknameEdit.Visible = true;
        if (GodotObject.IsInstanceValid(_helpReturnFocus) && _helpReturnFocus!.IsVisibleInTree())
        {
            _helpReturnFocus.GrabFocus();
            if (_helpReturnFocus == _nicknameEdit) _nicknameEdit.Edit();
        }
        _helpReturnFocus = null;
    }
    private void DisposeHelpPresentation()
    { _help.Close(); _sourceAudio?.SetHelpDucking(false); _helpReturnFocus = null; }
    private bool DrawHelp()
    {
        if (!_help.Active || _helpCatalog is null) return false;
        DrawRect(new(0, 0, 240, 160), Color.Color8(232, 232, 216));
        DrawRect(new(0, 0, 240, 20), Color.Color8(120, 160, 152));
        DrawRect(new(0, 142, 240, 18), Color.Color8(120, 160, 152));
        SourceText(_sourceNormal, new(8, 2), SourceMenuLabelPresentation.Plain(_battleText, "gString_Help"));
        string controls = _help.Level == SourceHelpLevel.Greeting
            ? SourceMenuLabelPresentation.Plain(_battleText, "gText_HelpSystemControls_A_Next", new Dictionary<string, string> { ["A_BUTTON"] = "A: " })
            : FieldMenuText.Get(_help.Level == SourceHelpLevel.Answer ? "help.control.back" : "help.control.select");
        SourceText(_sourceSmall, new(112, 146), controls);
        if (_help.Level == SourceHelpLevel.Greeting) DrawHelpText(_helpCatalog.Greeting, 8, 28, 224);
        else if (_help.Level == SourceHelpLevel.Answer)
        {
            int y = DrawHelpText(_help.Entry.Question, 8, 24, 224);
            DrawHelpText(_help.Entry.Answer, 8, y + 5, 224);
        }
        else
        {
            bool main = _help.Level == SourceHelpLevel.Topics;
            // Topic names may occupy a full line; the selected questions identify the list.
            int visible = Math.Min(7, _help.Count - _help.Scroll);
            for (int row = 0; row < visible; row++)
            {
                int index = _help.Scroll + row;
                string label = main ? _help.Topics[index].Label : index == _help.Topic.Entries.Length ? _helpCatalog.CancelLabel : _help.Topic.Entries[index].Question;
                SourceText(_sourceNormal, new(18, 22 + row * 15), label);
                if (index == _help.Selected) SourceText(_sourceNormal, new(8, 22 + row * 15), "▶");
            }
            if (main) DrawHelpText(_help.Topic.Description, 8, 112, 224);
            if (_help.Scroll > 0) DrawColoredPolygon([new(228, 26), new(236, 26), new(232, 22)], Colors.Black);
            if (_help.Scroll + visible < _help.Count) DrawColoredPolygon([new(228, 130), new(236, 130), new(232, 134)], Colors.Black);
        }
        return true;
    }
    private int DrawHelpText(string text, int x, int y, int width)
    {
        var lines = SourceHelpTextLayout.Wrap(text, width,
            line => _sourceSmall.Font.GetStringSize(_sourceSmall.PrepareText(line), fontSize: _sourceSmall.Size).X);
        foreach (string line in lines)
        {
            if (y + _sourceSmall.Size > 142) throw new InvalidDataException("Help text exceeds its panel.");
            SourceText(_sourceSmall, new(x, y), line); y += 14;
        }
        return y;
    }
}
