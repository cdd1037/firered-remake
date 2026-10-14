using Game.Domain.Pokemon;
using Godot;

/// <summary>Normal-field start menu and a bounded, truthful party/summary view.
/// Party changes and field moves use authoritative commands; saving uses the existing handler.</summary>
public partial class CampaignView
{
    private readonly record struct FieldMenuEntry(string Id, string Label);
    private string _fieldMenuMode = "closed";
    private int _fieldMenuCursor, _fieldPartyCursor, _fieldPartyAction, _fieldSummaryPage, _fieldSaveChoice;
    private int _fieldSwapSource = -1;
    private string? _fieldMenuNotice;
    private bool _fieldNoticeCloses;
    private bool FieldMenuOpen => _fieldMenuMode != "closed";
    private bool CanOpenFieldMenu => _session is not null && _catalog.EarlyStarterEnabled && !_busy &&
        _fade <= 0 && _fanfareRemaining <= 0 && !_nicknameEdit.Visible &&
        _tracks.All(t => t.Time >= t.Duration) && _session.State.HealingEffect is null &&
        _session.State.PlayerVisible && _session.State.HelpContext == "map" &&
        (_session.ValidatePlayerSave(_session.State) is null || CanOpenSafariFieldMenu);

    // Fixed pret 037335f: start_menu.c:213-224, 279-290; strings.c:18,159-165.
    // Player is the real saved name, not the literal text "PLAYER".
    private FieldMenuEntry[] FieldMenuEntries()
    {
        if (_session.State.SafariVisit is { Active: true }) return
            [new("safari-retire", "RETIRE"), new("pokedex", "POKéDEX"), new("pokemon", "POKéMON"),
             new("bag", "BAG"), new("player", _session.State.PlayerName), new("option", "OPTION"), new("exit", "EXIT")];
        List<FieldMenuEntry> entries = [];
        if (_session.State.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET")) entries.Add(new("pokedex", "POKéDEX"));
        if (_session.State.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEMON_GET")) entries.Add(new("pokemon", "POKéMON"));
        entries.AddRange([new("bag", "BAG"), new("player", _session.State.PlayerName), new("save", "SAVE"), new("option", "OPTION"), new("exit", "EXIT")]);
        return entries.ToArray();
    }

    private void ResetFieldMenu()
    {
        _fieldMenuMode = "closed"; _fieldMenuNotice = null; _fieldNoticeCloses = false;
        _fieldSwapSource = -1; _held = null; _repeat = 0;
    }

    // Called before the key-only guard so an actual joypad Start works too.
    private bool TryFieldMenuToggle(InputEvent input)
    {
        if (_session?.State.MailComposer is not null) return false;
        bool start = input is InputEventKey { Keycode: Key.Tab, Echo: false } ||
            input is InputEventJoypadButton { ButtonIndex: JoyButton.Start };
        if (!start) return false;
        bool pressed = input is InputEventKey k ? k.Pressed : ((InputEventJoypadButton)input).Pressed;
        if(TryRegionalSignStart(pressed)){QueueRedraw();GetViewport().SetInputAsHandled();return true;}
        if(TownMapActive)
        {using var key=new InputEventKey{Keycode=Key.Tab,Pressed=pressed};TryTownMapInput(key);QueueRedraw();GetViewport().SetInputAsHandled();return true;}
        if (pressed)
        {
            // The source options screen handles A/B to commit; Start does not discard
            // its in-progress values. The ordinary field overlays retain their toggle.
            if (_fieldMenuMode == "options") { GetViewport().SetInputAsHandled(); return true; }
            if (FieldMenuOpen) ResetFieldMenu();
            else if (CanOpenFieldMenu) OpenFieldStartMenu();
            QueueRedraw();
        }
        GetViewport().SetInputAsHandled(); return true;
    }

    private void OpenFieldStartMenu()
    {
        _fieldMenuMode = "start"; _fieldMenuCursor = Math.Clamp(_fieldMenuCursor, 0, FieldMenuEntries().Length - 1);
        _held = null; _repeat = 0;
    }

    private bool TryFieldMenuInput(InputEventKey key)
    {
        if (!FieldMenuOpen) return false;
        if (key.Keycode is Key.Escape or Key.F12) return false;
        // Save/load use their established handler and validation after the overlay closes.
        if (key.Keycode is Key.F5 or Key.F9) { if (key.Pressed) ResetFieldMenu(); return false; }
        _held = null; _repeat = 0;
        if (!key.Pressed || key.Echo || _busy || _fade > 0) return true;
        if(key.Keycode is Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Backspace)SourceSound("SE_SELECT");
        if (_fieldMenuNotice is not null)
        {
            if (key.Keycode is Key.Enter or Key.Backspace)
            {
                if (_fieldNoticeCloses) ResetFieldMenu();
                else _fieldMenuNotice = null;
            }
            return true;
        }
        if (PokedexMenuInput(key)||TrainerCardInput(key)||OptionsInput(key)) return true;
        if (key.Keycode == Key.Backspace)
        {
            if (_fieldMenuMode == "start") ResetFieldMenu();
            else if (_fieldMenuMode is "party" or "save-confirm" or "save-overwrite") _fieldMenuMode = "start";
            else { _fieldMenuMode = "party"; _fieldSwapSource = -1; }
            return true;
        }
        if (_fieldMenuMode is "save-confirm" or "save-overwrite")
        {
            FieldMenuMoveCursor(key, ref _fieldSaveChoice, 2);
            if (key.Keycode == Key.Enter)
            {
                if (_fieldSaveChoice == 1) _fieldMenuMode = "start";
                else
                {
                    bool overwrite=_fieldMenuMode=="save-overwrite";
                    try{_entryOverwriteConfirmed=overwrite;SaveFromFieldMenu();}
                    finally{_entryOverwriteConfirmed=false;}
                }
            }
            return true;
        }
        if (_fieldMenuMode == "summary")
        {
            if (!_session.State.Party[_fieldPartyCursor].IsEgg)
            {
                if (key.Keycode == Key.Left) _fieldSummaryPage = (_fieldSummaryPage + 2) % 3;
                if (key.Keycode == Key.Right) _fieldSummaryPage = (_fieldSummaryPage + 1) % 3;
            }
            if (key.Keycode == Key.Up) _fieldPartyCursor = (_fieldPartyCursor + _session.State.Party.Length - 1) % _session.State.Party.Length;
            if (key.Keycode == Key.Down) _fieldPartyCursor = (_fieldPartyCursor + 1) % _session.State.Party.Length;
            if (_session.State.Party[_fieldPartyCursor].IsEgg) _fieldSummaryPage = 0;
            return true;
        }
        if (_fieldMenuMode == "start")
        {
            var entries = FieldMenuEntries();
            FieldMenuMoveCursor(key, ref _fieldMenuCursor, entries.Length);
            if (key.Keycode == Key.Enter) SelectFieldMenu(entries[_fieldMenuCursor].Id);
            return true;
        }
        if (_fieldMenuMode == "actions")
        {
            var actions=FieldPartyActions();
            FieldMenuMoveCursor(key, ref _fieldPartyAction, actions.Length);
            if (key.Keycode != Key.Enter) return true;
            if (actions[_fieldPartyAction] == "SUMMARY") { _fieldMenuMode = "summary"; _fieldSummaryPage = 0; }
            else if (actions[_fieldPartyAction] == "SWITCH")
            {
                if (!_session.CanReorderFieldParty) _fieldMenuNotice = "At least two POKéMON are\nneeded to SWITCH positions.";
                else { _fieldMenuMode = "switch"; _fieldSwapSource = _fieldPartyCursor; }
            }
            else if(actions[_fieldPartyAction] is "ITEM" or "MAIL") OpenPartyHeldItems();
            else if(actions[_fieldPartyAction] == "FLY") UseFieldPartyFly();
            else if(actions[_fieldPartyAction] == "CUT") UseFieldPartyCut();
            else if(actions[_fieldPartyAction] == "ROCK SMASH") UseFieldPartyRockSmash();
            else if(actions[_fieldPartyAction] == "FLASH") UseFieldPartyFlash();
            else if(actions[_fieldPartyAction] == "STRENGTH") UseFieldPartyStrength();
            else if(actions[_fieldPartyAction] == "SURF") UseFieldPartySurf();
            else if(actions[_fieldPartyAction] == "WATERFALL") UseFieldPartyWaterfall();
            else _fieldMenuMode = "party";
            return true;
        }
        FieldMenuMoveCursor(key, ref _fieldPartyCursor, _session.State.Party.Length + 1);
        if (key.Keycode != Key.Enter) return true;
        if (_fieldPartyCursor == _session.State.Party.Length)
        {
            _fieldMenuMode = _fieldMenuMode == "switch" ? "party" : "start"; _fieldSwapSource = -1;
            _fieldPartyCursor = Math.Max(0, _session.State.Party.Length - 1); return true;
        }
        if (_fieldMenuMode == "switch")
        {
            int source = _fieldSwapSource; _fieldSwapSource = -1; _fieldMenuMode = "party";
            // Source cancels when the same slot is chosen, without any game command.
            if (source != _fieldPartyCursor)
            {
                long revision = _session.Revision;
                Apply(new("party_swap", Choice: source, PartyIndex: _fieldPartyCursor));
                if (_session.Revision == revision) _fieldMenuNotice = "The party order could not\nbe changed here.";
            }
        }
        else { _fieldMenuMode = "actions"; _fieldPartyAction = 0; }
        return true;
    }

    // The source lists a known field move before checking its badge/context at selection.
    private string[] FieldPartyActions()
    {
        if (_fieldPartyCursor >= 0 && _fieldPartyCursor < _session.State.Party.Length && _session.State.Party[_fieldPartyCursor].IsEgg)
            return ["SUMMARY", "SWITCH", "CANCEL"];
        var actions=new List<string>{"SUMMARY"};
        if(_fieldPartyCursor>=0&&_fieldPartyCursor<_session.State.Party.Length&&!_session.State.Party[_fieldPartyCursor].IsEgg)
            foreach(var move in _session.State.Party[_fieldPartyCursor].Pokemon.Moves)
                if(_catalog.VermilionGymEnabled&&move.Move.Value=="pret:MOVE_CUT")actions.Add("CUT");
                else if(_catalog.FieldFly is not null&&move.Move.Value=="pret:MOVE_FLY")actions.Add("FLY");
                else if(_catalog.RockTunnelEnabled&&move.Move.Value=="pret:MOVE_FLASH")actions.Add("FLASH");
                else if(_catalog.SafariEnabled&&move.Move.Value=="pret:MOVE_STRENGTH")actions.Add("STRENGTH");
                else if(_catalog.SurfEnabled&&move.Move.Value=="pret:MOVE_SURF")actions.Add("SURF");
                else if(_catalog.FourIslandEnabled&&move.Move.Value=="pret:MOVE_WATERFALL")actions.Add("WATERFALL");
                else if(_catalog.FieldRockSmash is not null&&move.Move.Value=="pret:MOVE_ROCK_SMASH")actions.Add("ROCK SMASH");
        actions.Add("SWITCH");
        if(_catalog.EarlyShop is not null)actions.Add(_fieldPartyCursor>=0&&_fieldPartyCursor<_session.State.Party.Length&&
            SourceMailRules.IsMailItem(_session.State.Party[_fieldPartyCursor].HeldItem)?"MAIL":"ITEM");
        actions.Add("CANCEL");return actions.ToArray();
    }
    private int FieldPartyActionTop => 152-FieldPartyActions().Length*16;
    private void UseFieldPartyCut()
    {
        int selected=_fieldPartyCursor;
        // This query is advisory. The command repeats every gate and owns the effect.
        bool usable=_session.CanUseFieldCut(selected);
        long revision=_session.Revision;
        Apply(new("field_cut",PartyIndex:selected));
        if(_session.Revision>revision){ResetFieldMenu();return;}
        _fieldMenuMode="party";
        _fieldMenuNotice=_status=="field_cut_badge_required"
            ? "This can't be used until a new\nBADGE is obtained."
            : _status=="field_cut_nothing_to_cut" ? "There's nothing to CUT."
            : usable ? "CUT could not be used here." : "Can't use that here.";
    }

    private static void FieldMenuMoveCursor(InputEventKey key, ref int cursor, int count)
    {
        if (key.Keycode == Key.Up) cursor = (cursor + count - 1) % count;
        if (key.Keycode == Key.Down) cursor = (cursor + 1) % count;
    }

    private void SelectFieldMenu(string id)
    {
        switch (id)
        {
            case "safari-retire": ResetFieldMenu(); Apply(new("safari_retire")); break;
            case "pokemon":
                if (_session.State.Party.IsEmpty) _fieldMenuNotice = "You have no POKéMON\nin your party.";
                else { _fieldMenuMode = "party"; _fieldPartyCursor = 0; }
                break;
            case "bag":
                ResetFieldMenu(); Apply(new("bag_open"));
                if (_session.State.ItemMenu is null) { _fieldMenuMode = "start"; _fieldMenuNotice = "The BAG cannot be opened\nhere."; }
                break;
            case "save":
                _fieldMenuMode = "save-confirm"; _fieldSaveChoice = 0;
                break;
            case "exit": ResetFieldMenu(); break;
            case "pokedex": if(_catalog.PokemonStorageEnabled)OpenPokedexMenu();else _fieldMenuNotice = "The full POKéDEX screen is\nnot implemented yet."; break;
            case "player": if(_catalog.PokemonStorageEnabled)OpenTrainerCard();else _fieldMenuNotice = "The TRAINER CARD screen is\nnot implemented yet."; break;
            case "option": if(_catalog.PokemonStorageEnabled)OpenOptionsMenu();else _fieldMenuNotice = "The OPTION screen is\nnot implemented yet."; break;
        }
    }

    private void SaveFromFieldMenu()
    {
        ResetFieldMenu();
        using var save = new InputEventKey { Keycode = Key.F5, Pressed = true };
        _UnhandledInput(save);
        if(_fieldMenuMode=="save-overwrite")return;
        _fieldMenuMode = "start"; _fieldNoticeCloses = true;
        _fieldMenuNotice = _status == "已保存" ? _session.State.PlayerName + " saved the game." : "The game could not be saved\nhere. Return to a stable field.";
    }

    // Uses the existing mouse press/release/double-click/focus and coordinate gates.
    // Only visibly drawn row rectangles are actionable; the field remains inert.
    private bool MouseFieldMenu(Vector2 point)
    {
        if (_fieldMenuNotice is not null) return MouseConfirm(point, MouseMessage);
        if (_fieldMenuMode is "pokedex" or "pokedex-entry") return MousePokedexMenu(point);
        if (_fieldMenuMode == "options") return MouseOptionsMenu(point);
        if (_fieldMenuMode == "trainer-card") return new Rect2(176,140,64,20).HasPoint(point)&&MouseKey(Key.Backspace);
        if (_fieldMenuMode is "save-confirm" or "save-overwrite") return MouseSelect(point, ref _fieldSaveChoice, 2, 192, 73, 40, 16);
        if (_fieldMenuMode == "start")
            return MouseSelect(point, ref _fieldMenuCursor, FieldMenuEntries().Length, 176, 8, 56, 15);
        if (_fieldMenuMode == "actions") return MouseSelect(point, ref _fieldPartyAction, FieldPartyActions().Length, 160, FieldPartyActionTop, 72, 16);
        if (_fieldMenuMode == "summary")
        {
            if (new Rect2(8, 140, 60, 16).HasPoint(point)) return MouseKey(Key.Left);
            if (new Rect2(84, 140, 68, 16).HasPoint(point)) return MouseKey(Key.Right);
            if (new Rect2(172, 140, 60, 16).HasPoint(point)) return MouseKey(Key.Backspace);
            return false;
        }
        if (MouseSelect(point, ref _fieldPartyCursor, _session.State.Party.Length, 8, 8, 224, 20)) return true;
        if (new Rect2(8, 132, 76, 16).HasPoint(point)) { _fieldPartyCursor = _session.State.Party.Length; return MouseKey(); }
        return false;
    }

    private void DrawFieldMenu()
    {
        if (!FieldMenuOpen) return;
        if (_fieldMenuMode is "start" or "save-confirm" or "save-overwrite")
        {
            var entries = FieldMenuEntries();
            DrawSafariFieldStats();
            // Source new_menu_helpers.c:666-672, plus the surrounding 8px frame.
            _battleSkin.DrawUserFrame(this, new(168, 0, 72, entries.Length * 16 + 8));
            for (int i = 0; i < entries.Length; i++) SourceText(_sourceNormal, new(184, 8 + i * 15), entries[i].Label);
            SourceText(_sourceNormal, new(176, 8 + _fieldMenuCursor * 15), "▶");
            if (_fieldMenuMode is "save-confirm" or "save-overwrite")
            {
                ItemMessageBox(_fieldMenuMode=="save-overwrite"?"A different adventure is saved.\nReplace that save with this game?":"Would you like to\nsave the game?");
                _battleSkin.DrawUserFrame(this, new(176, 64, 64, 48));
                SourceText(_sourceNormal, new(192, 73), "YES"); SourceText(_sourceNormal, new(192, 89), "NO");
                SourceText(_sourceNormal, new(182, 73 + 16 * _fieldSaveChoice), "▶");
            }
        }
        else if (_fieldMenuMode is "pokedex" or "pokedex-entry") DrawPokedexMenu();
        else if (_fieldMenuMode == "trainer-card") DrawTrainerCard();
        else if (_fieldMenuMode == "options") DrawOptionsMenu();
        else if (_fieldMenuMode == "summary") DrawFieldSummary();
        else DrawFieldParty();
        if (_fieldMenuNotice is not null) ItemMessageBox(_fieldMenuNotice);
    }

    private void DrawFieldParty()
    {
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
        for (int i = 0; i < _session.State.Party.Length; i++)
        {
            var owned = _session.State.Party[i]; var mon = owned.Pokemon; int y = 8 + i * 20;
            if (_fieldMenuMode == "switch" && i == _fieldSwapSource) DrawRect(new(14, y, 218, 20), new Color("e4e4c8"));
            DrawTextureRectRegion(PresentedOwnedIcon(owned),new(18,y,16,16),new(0,0,32,32));
            SourceText(_sourceSmall, new(36, y), PresentedOwnedName(owned));
            if (!owned.IsEgg)
            {
            SourceText(_sourceSmall, new(98, y), "Lv" + mon.Level);
            SourceText(_sourceSmall, new(134, y), $"{mon.CurrentHp}/{mon.Stats.MaxHp}");
            DrawRect(new(134, y + 12, 54, 3), new Color("94948c"));
            int hpWidth = mon.CurrentHp == 0 ? 0 : Math.Max(1, 52 * mon.CurrentHp / mon.Stats.MaxHp);
            DrawRect(new(135, y + 12, hpWidth, 2), mon.CurrentHp * 2 > mon.Stats.MaxHp ? new Color("50b080") : mon.CurrentHp * 5 > mon.Stats.MaxHp ? new Color("e8c050") : new Color("e87058"));
            DrawForestPartyStatus(owned, new(198, y + 3));
            }
            if (i == _fieldPartyCursor) SourceText(_sourceSmall, new(8, y), "▶");
        }
        SourceText(_sourceNormal, new(18, 132), "CANCEL");
        if (_fieldPartyCursor == _session.State.Party.Length) SourceText(_sourceNormal, new(8, 132), "▶");
        SourceText(_sourceSmall, new(90, 134), _fieldMenuMode == "switch" ? "Move to which position?" : "Choose a POKéMON.");
        if (_fieldMenuMode == "actions")
        {
            var actions=FieldPartyActions();
            _battleSkin.DrawStandardFrame(this, new(152, FieldPartyActionTop-8, 88, actions.Length*16+16));
            for (int i = 0; i < actions.Length; i++) SourceText(_sourceNormal, new(170, FieldPartyActionTop + i * 16), actions[i]);
            SourceText(_sourceNormal, new(160, FieldPartyActionTop + _fieldPartyAction * 16), "▶");
        }
    }

    // These are live values from the owned instance/catalog, never reconstructed
    // from level or starter choice. Exact original summary/party artwork is pending.
    private string[] FieldSummaryLines() => PartySummaryLines(_fieldPartyCursor, _fieldSummaryPage);
    private string[] PartySummaryLines(int partyIndex, int page)
    {
        var owned = _session.State.Party[partyIndex]; var mon = owned.Pokemon;
        if (owned.IsEgg) return EggSummaryLines(owned);
        var species = _catalog.PokemonContent!.Catalog.Species[mon.Species];
        return page switch
        {
            0 => [_catalog.PokemonContent.SpeciesNames[mon.SourceSpeciesId],
                "TYPE " + FieldSymbol(species.Type1, "TYPE_") + (species.Type1 == species.Type2 ? "" : "/" + FieldSymbol(species.Type2, "TYPE_")),
                "OT/" + owned.OriginalTrainerName, "ID No." + (mon.OriginalTrainerId & 0xffff).ToString("D5"),
                "ABILITY/" + FieldSymbol(mon.AbilityId, "ABILITY_"), mon.Nature.ToString().ToUpperInvariant() + " nature"],
            1 => [$"HP {mon.CurrentHp}/{mon.Stats.MaxHp}", "ATTACK " + mon.Stats.Attack, "DEFENSE " + mon.Stats.Defense,
                "SP. ATK " + mon.Stats.SpecialAttack, "SP. DEF " + mon.Stats.SpecialDefense, "SPEED " + mon.Stats.Speed, "EXP. POINTS " + mon.Experience],
            _ => mon.Moves.Select(m => MoveName(m.Move) + $"  PP {m.CurrentPp}/{m.MaximumPp}").ToArray()
        };
    }
    private static string FieldSymbol(string value, string prefix) => value.StartsWith(prefix, StringComparison.Ordinal) ? value[prefix.Length..].Replace('_', ' ') : value;

    private void DrawFieldSummary() => DrawPartySummary(_fieldPartyCursor, _fieldSummaryPage);
    private void DrawPartySummary(int partyIndex, int page)
    {
        var owned = _session.State.Party[partyIndex]; var mon = owned.Pokemon;
        if (owned.IsEgg) { DrawEggSummary(owned); return; }
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
        SourceText(_sourceNormal, new(8, 8), new[] { "POKéMON INFO", "POKéMON SKILLS", "KNOWN MOVES" }[page]);
        SourceText(_sourceSmall, new(202, 10), $"{page + 1}/3");
        SourceText(_sourceNormal, new(8, 26), owned.Nickname);
        SourceText(_sourceSmall, new(105, 28), "Lv" + mon.Level);
        if (mon.Gender != PokemonGender.Genderless) SourceText(mon.Gender == PokemonGender.Female ? _healthFemale : _healthMale, new(139, 28), mon.Gender == PokemonGender.Female ? "♀" : "♂");
        DrawForestPartyStatus(owned, new(198, 28));
        var lines = PartySummaryLines(partyIndex, page);
        if (page == 0)
        {
            if (_battleArt.TryGetValue((PresentedRenderSpecies(mon), "front", mon.IsShiny), out var art)) DrawTexture(art.Texture, new(8, 44));
            SourceText(_sourceSmall, new(82, 46), lines[0]);
            SourceText(_sourceSmall, new(82, 60), lines[1]);
            SourceText(_sourceSmall, new(82, 77), lines[2]);
            SourceText(_sourceSmall, new(82, 91), lines[3]);
            SourceText(_sourceSmall, new(8, 110), lines[4]);
            SourceText(_sourceSmall, new(8, 124), lines[5]);
        }
        else for (int i = 0; i < lines.Length; i++) SourceText(_sourceSmall, new(12, 45 + i * (page == 1 ? 12 : 22)), lines[i]);
        SourceText(_sourceSmall, new(8, 143), "< PREV");
        SourceText(_sourceSmall, new(84, 143), "NEXT >");
        SourceText(_sourceSmall, new(172, 143), "BACK");
    }
}
