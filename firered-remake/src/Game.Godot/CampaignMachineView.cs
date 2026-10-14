using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Items;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Godot;

/// <summary>Functional source-authenticated TM presentation using existing source fonts/frames. The original TM CASE background is not imported.</summary>
public partial class CampaignView
{
    private const string MachineIcon = "pewter-tm39", MachineCaseIcon = "pewter-tm-case";
    private bool _machineWasActive;
    private string _machineMode = "list", _machineNoticeReturn = "party";
    private int _machineCursor, _machineListCursor, _machineParty, _machineSlot = -1, _machinePage;
    private ImmutableArray<string> _machinePages = [];
    private bool MachineActive => _catalog.PewterMachines is not null && _session.State.ItemMenu is { Origin: "bag" } menu
        && (menu.View == "machines" || menu.View == "message" && menu.ReturnView == "machines");
    private ItemId _machineSelected = PewterMachineRules.Tm39Id;
    private InventoryStack[] MachineStacks => (_session.State.Inventory!.Machines ?? []).OrderBy(s => s.Item.Value, StringComparer.Ordinal).ToArray();
    private PewterAdditionalMachine MachineDefinition => PewterMachineRules.Definition(_catalog.PewterMachines!, _machineSelected)
        ?? throw new InvalidDataException("Selected machine is not in this profile.");
    private string MachineMoveName => MoveName(MachineDefinition.Move);
    private int MachineQuantity => OpeningInventoryRules.Quantity(_session.State.Inventory!.Machines ?? [], _machineSelected);
    private int MachineListX => _catalog.MtMoonEnabled ? 106 : 36;
    private int MachineListWidth => _catalog.MtMoonEnabled ? 134 : 196;
    private int MachineListRowHeight => _catalog.MtMoonEnabled ? 16 : 32;
    private int MachineListStart => _catalog.SsAnneEnabled ? Math.Max(0, _machineCursor - 6) : 0;
    private int MachineListVisibleCount => _catalog.SsAnneEnabled ? Math.Min(7, MachineStacks.Length + 1 - MachineListStart) : MachineStacks.Length + 1;

    partial void ExtendMachineQuestPocket(ref QuestItemUiDocument document)
    {
        if (_catalog.PewterMachines is not { } catalog) return;
        var machine = PretPewterMachines.LoadDocument(System.IO.File.ReadAllText(System.IO.Path.Combine(_root,
            "content/generated/pewter-machines/pewter-machines.json")));
        // Catalog admission belongs to the current rules; these shared pixels depend only on
        // the exact inherited TM39/CASE source definitions, not the regional profile name.
        PewterMachineRules.ValidateCatalog(catalog);
        if (machine.Catalog.Profile != PewterMachineRules.Profile || machine.Catalog.SourceCommit != catalog.SourceCommit ||
            Game.Application.CanonicalJson.Hash(machine.Catalog.TmCase) != Game.Application.CanonicalJson.Hash(catalog.TmCase) ||
            Game.Application.CanonicalJson.Hash(machine.Catalog.Tm39) != Game.Application.CanonicalJson.Hash(catalog.Tm39))
            throw new InvalidDataException("Machine presentation source definitions differ.");
        string firstIcon = document.Items[0].Icon;
        var position = document.Assets.Single(x => x.Id == firstIcon);
        foreach (var icon in machine.Icons)
        {
            string id = icon.Item == PewterMachineRules.TmCaseId ? MachineCaseIcon : MachineIcon;
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, PretPewterMachines.IconRgba(icon));
            _questPocketTextures.Add(id, ImageTexture.CreateFromImage(image));
            document = document with { Assets = document.Assets.Add(new(id, icon.PngPath, icon.Width, icon.Height,
                position.X, position.Y, icon.PngSha256, icon.RgbaSha256)) };
        }
        var item = machine.Catalog.TmCase;
        document = document with { Items = document.Items.Add(new(item.Item.Id.Value, item.Item.SourceItemSymbol,
            item.Item.Name, item.Description, MachineCaseIcon, item.Item.FieldUseFunction)) };
    }

    private void LoadDepartmentMachineIcon()
    {
        if (DepartmentStoreMachineStock.For(_catalog.PewterMachines) is null) return;
        if (!_earlyItemIcons.TryGetValue(PretDepartmentStoreMachines.ReusedTm15Icon, out var normalIcon))
            throw new InvalidDataException("department_machine_normal_icon_missing");
        _earlyItemIcons.Add(DepartmentStoreMachineStock.Tm15Id, normalIcon);
    }

    private void SynchronizeMachines()
    {
        bool active = MachineActive;
        if (active && !_machineWasActive)
        { _machineMode = "list"; _machineSelected = MachineStacks.FirstOrDefault()?.Item ?? PewterMachineRules.Tm39Id; _machineCursor = 0; _machineParty = 0; _machineSlot = -1; _machinePages = []; _machinePage = 0; }
        if (!active && _machineWasActive && _session.State.ItemMenu is { Origin: "bag", View: "bag" })
        {
            _questPocket = 1;
            _questPocketCursor = Math.Max(0, Enumerable.Range(0, QuestPocketStacks.Length).FirstOrDefault(i => QuestPocketStacks[i].Item == PewterMachineRules.TmCaseId, 0));
        }
        _machineWasActive = active;
    }

    private ImmutableArray<string> MachineTextPages(string symbol) => _catalog.TextPages(symbol)
        .Select(text => text.Replace("{STR_VAR_1}", _session.State.Party.Length > _machineParty ? _session.State.Party[_machineParty].Nickname : "", StringComparison.Ordinal)
            .Replace("{STR_VAR_2}", MachineMoveName, StringComparison.Ordinal)).ToImmutableArray();
    private string MachineText(string symbol) => MachineTextPages(symbol)[0];
    private string MachineCompatibility(PartyPokemon pokemon) => pokemon.IsEgg ? "gText_NotAble"
        : pokemon.Pokemon.Moves.Any(x => x.Move == MachineDefinition.Move) ? "gText_Learned"
        : MachineDefinition.Compatibility.Any(x => x.Species == pokemon.Pokemon.Species && x.CanLearn) ? "gText_Able2" : "gText_NotAble";
    private void MachineNotice(string symbol, string after = "party")
    { _machinePages = MachineTextPages(symbol); _machinePage = 0; _machineMode = "notice"; _machineNoticeReturn = after; }
    private void SelectMachineParty()
    {
        _machineParty = _machineCursor; _machineSlot = -1;
        string compatibility = MachineCompatibility(_session.State.Party[_machineParty]);
        if (compatibility != "gText_Able2")
        { MachineNotice(compatibility == "gText_Learned" ? "gText_PkmnAlreadyKnows" : "gText_PkmnCantLearnMove"); return; }
        _machineMode = _session.State.Party[_machineParty].Pokemon.Moves.Length == 4 ? "replace-confirm" : "teach-confirm";
        _machinePages = MachineTextPages(_machineMode == "replace-confirm" ? "gText_PkmnNeedsToReplaceMove" : "gText_TeachMoveQues");
        _machinePage = 0; _machineCursor = 0;
    }

    private void ReturnToMachineList()
    { _machineMode = "list"; _machineCursor = Math.Clamp(_machineListCursor, 0, MachineStacks.Length); }

    private bool MachineInput(InputEventKey key)
    {
        if (!MachineActive || HeldItemScreen || key.Keycode is Key.F5 or Key.F9 or Key.F12 or Key.Escape) return false;
        if (!key.Pressed || key.Echo) return true;
        _held = null;
        if (_itemNotice is not null) { if (key.Keycode is Key.Enter or Key.Backspace) _itemNotice = null; return true; }
        if (_session.State.ItemMenu!.View == "message")
        {
            if (key.Keycode is Key.Enter or Key.Backspace)
            { Apply(new("menu_ack")); _machineMode = "list"; _machineCursor = 0; }
            return true;
        }
        if (_machineMode == "actions")
        {
            if (key.Keycode == Key.Backspace) { ReturnToMachineList(); return true; }
            FieldMenuMoveCursor(key, ref _machineCursor, 3);
            if (key.Keycode == Key.Enter)
            {
                int action = _machineCursor;
                if (action == 0)
                {
                    if (_session.State.Party.IsEmpty) MachineNotice("gText_ThereIsNoPokemon", "list");
                    else { _machineMode = "party"; _machineCursor = 0; }
                }
                else
                {
                    ReturnToMachineList();
                    if (action == 1) StartGiveItem(_machineSelected);
                }
            }
            return true;
        }
        if (_machineMode == "notice")
        {
            if (key.Keycode is Key.Enter or Key.Backspace)
            { if (++_machinePage >= _machinePages.Length) { _machineMode = _machineNoticeReturn; _machineCursor = _machineMode == "party" ? _machineParty : 0; } }
            return true;
        }
        if (_machineMode is "replace-confirm" or "teach-confirm")
        {
            if (key.Keycode == Key.Backspace) { _machineMode = "party"; _machineCursor = _machineParty; return true; }
            if (_machinePage + 1 < _machinePages.Length)
            { if (key.Keycode == Key.Enter) _machinePage++; return true; }
            if (key.Keycode == Key.Up) _machineCursor = 0;
            if (key.Keycode == Key.Down) _machineCursor = 1;
            if (key.Keycode == Key.Enter)
            {
                if (_machineCursor == 1) { _machineMode = "party"; _machineCursor = _machineParty; }
                else if (_machineMode == "replace-confirm") { _machineMode = "replacement"; _machineCursor = 0; }
                else
                {
                    long revision = _session.Revision;
                    Apply(new("machine_teach", Text: _machineSelected.Value, PartyIndex: _machineParty, MoveSlot: _machineSlot));
                    if (_session.Revision == revision)
                    {
                        if (_status is "machine_cannot_forget_hm" or "hm_move_cannot_be_forgotten")
                        { _machineMode = "replacement"; _machineCursor = Math.Max(0, _machineSlot); _itemNotice = HmCannotForgetText; }
                        else _itemNotice = _status;
                    }
                }
            }
            return true;
        }
        if (key.Keycode == Key.Backspace)
        {
            if (_machineMode == "list") Apply(new("menu_back"));
            else if (_machineMode == "party") ReturnToMachineList();
            else { _machineMode = "party"; _machineCursor = 0; }
            return true;
        }
        int count = _machineMode == "list" ? MachineStacks.Length
            : _machineMode == "party" ? _session.State.Party.Length : 4;
        _machineCursor = Math.Clamp(_machineCursor, 0, count);
        if (key.Keycode == Key.Up) _machineCursor = Math.Max(0, _machineCursor - 1);
        if (key.Keycode == Key.Down) _machineCursor = Math.Min(count, _machineCursor + 1);
        if (key.Keycode == Key.Enter)
        {
            if (_machineCursor == count)
            {
                if (_machineMode == "list") Apply(new("menu_back"));
                else if (_machineMode == "party") ReturnToMachineList();
                else { _machineMode = "party"; _machineCursor = 0; }
            }
            else if (_machineMode == "list")
            {
                _machineListCursor = _machineCursor; _machineSelected = MachineStacks[_machineCursor].Item;
                if (_session.State.ItemMenu.TargetInstanceId is not null) StartGiveItem(_machineSelected);
                else { _machineMode = "actions"; _machineCursor = 0; }
            }
            else if (_machineMode == "party") SelectMachineParty();
            else
            {
                _machineSlot = _machineCursor; _machineMode = "teach-confirm"; _machineCursor = 0;
                _machinePages = MachineTextPages("gText_TeachMoveQues"); _machinePage = 0;
            }
        }
        return true;
    }


    private void DrawMtMoonMachineList()
    {
        DrawTexture(_questPocketTextures[MachineCaseIcon], new(8, 8));
        SourceText(_sourceNormal, new(40, 12), MachineText("gText_TMCase"));
        var stacks = MachineStacks;
        int start = MachineListStart;
        for (int i = start; i < Math.Min(stacks.Length, start + MachineListVisibleCount); i++)
        {
            var definition = PewterMachineRules.Definition(_catalog.PewterMachines!, stacks[i].Item)!;
            SourceText(_sourceNormal, new(118, 40 + (i - start) * 16), definition.Machine.Item.Name + " " + MoveName(definition.Move));
        }
        if (stacks.Length < start + MachineListVisibleCount) SourceText(_sourceNormal, new(118, 40 + (stacks.Length - start) * 16), MachineText("gText_Close"));
        int selected = Math.Min(_machineCursor, stacks.Length);
        SourceText(_sourceNormal, new(106, 40 + (selected - start) * 16), _itemSkin.Text("gText_SelectorArrow2"));
        if (selected < stacks.Length)
        {
            var stack = stacks[selected];
            if (_earlyItemIcons.TryGetValue(stack.Item, out var icon)) DrawTexture(icon, new(8, 40));
            else DrawTexture(_questPocketTextures[MachineIcon], new(8, 40));
            SourceText(_sourceSmall, new(44, 50), _itemSkin.FormatText("gText_TimesStrVar1", stack.Quantity.ToString()));
            ItemLines(_sourceSmall, new(8, 80), PewterMachineRules.Definition(_catalog.PewterMachines!, stack.Item)!.Machine.Description, 13);
        }
        else ItemLines(_sourceSmall, new(8, 128), MachineText("gText_TMCaseWillBePutAway"), 13);
    }

    private bool DrawMachines()
    {
        if (!MachineActive || HeldItemScreen) return false;
        // Reuse authenticated source menu frames/fonts; this is deliberately not the original case skin.
        _battleSkin.DrawStandardFrame(this, new(0, 0, 240, 160));
        string mode = _session.State.ItemMenu!.View == "message" ? "list" : _machineMode;
        if (mode == "list" && _catalog.MtMoonEnabled) DrawMtMoonMachineList();
        else if (mode == "list")
        {
            DrawTexture(_questPocketTextures[MachineCaseIcon], new(8, 8));
            SourceText(_sourceNormal, new(40, 12), MachineText("gText_TMCase"));
            int count = MachineQuantity > 0 ? 1 : 0;
            if (count > 0)
            {
                DrawTexture(_questPocketTextures[MachineIcon], new(8, 40));
                SourceText(_sourceNormal, new(48, 40), _catalog.PewterMachines!.Tm39.Item.Name + " " + MachineMoveName);
                SourceText(_sourceSmall, new(184, 57), _itemSkin.FormatText("gText_TimesStrVar1", MachineQuantity.ToString()));
            }
            SourceText(_sourceNormal, new(48, 40 + count * 32), MachineText("gText_Close"));
            SourceText(_sourceNormal, new(36, 40 + Math.Min(_machineCursor, count) * 32), _itemSkin.Text("gText_SelectorArrow2"));
            if (_machineCursor < count) ItemLines(_sourceSmall, new(16, 92), _catalog.PewterMachines!.Tm39.Description, 13);
            else ItemLines(_sourceNormal, new(16, 112), MachineText("gText_TMCaseWillBePutAway"));
        }
        else if (mode == "actions")
        {
            SourceText(_sourceNormal, new(16, 12), MachineDefinition.Machine.Item.Name + " " + MachineMoveName);
            ItemLines(_sourceSmall, new(16, 38), MachineDefinition.Machine.Description, 13);
            string[] options = ["USE", "GIVE", "CANCEL"];
            for (int i = 0; i < options.Length; i++) SourceText(_sourceNormal, new(176, 98 + i * 17), options[i]);
            SourceText(_sourceNormal, new(164, 98 + _machineCursor * 17), _itemSkin.Text("gText_SelectorArrow2"));
        }
        else if (mode == "replacement")
        {
            SourceText(_sourceNormal, new(16, 8), _session.State.Party[_machineParty].Nickname);
            var moves = _session.State.Party[_machineParty].Pokemon.Moves;
            for (int i = 0; i < moves.Length; i++)
            {
                SourceText(_sourceNormal, new(24, 26 + i * 16), MoveName(moves[i].Move));
                SourceText(_sourceSmall, new(174, 26 + i * 16), $"{moves[i].CurrentPp}/{moves[i].MaximumPp}");
            }
            SourceText(_sourceNormal, new(24, 90), _itemSkin.Text("gFameCheckerText_Cancel"));
            SourceText(_sourceNormal, new(12, 26 + _machineCursor * 16), _itemSkin.Text("gText_SelectorArrow2"));
            ItemMessageBox(MachineText("gText_WhichMoveToForget"));
        }
        else
        {
            for (int i = 0; i < _session.State.Party.Length; i++)
            {
                SourceText(_sourceNormal, new(24, 8 + i * 14), PresentedOwnedName(_session.State.Party[i]));
                SourceText(_sourceSmall, new(148, 8 + i * 14), MachineText(MachineCompatibility(_session.State.Party[i])));
            }
            SourceText(_sourceNormal, new(24, 8 + _session.State.Party.Length * 14), _itemSkin.Text("gFameCheckerText_Cancel"));
            if (mode == "party")
            {
                SourceText(_sourceNormal, new(12, 8 + _machineCursor * 14), _itemSkin.Text("gText_SelectorArrow2"));
                ItemMessageBox(MachineText("gText_TeachWhichPokemon"));
            }
            else
            {
                if (!_session.State.Party.IsEmpty)
                    SourceText(_sourceNormal, new(12, 8 + _machineParty * 14), _itemSkin.Text("gText_SelectorArrow2"));
                ItemMessageBox(_machinePages[_machinePage]);
                if (mode is "replace-confirm" or "teach-confirm" && _machinePage == _machinePages.Length - 1)
                {
                    _battleSkin.DrawStandardFrame(this, new(184, 64, 56, 48));
                    SourceText(_sourceNormal, new(200, 72), MachineText("gText_Yes"));
                    SourceText(_sourceNormal, new(200, 88), MachineText("gText_No"));
                    SourceText(_sourceNormal, new(188, 72 + _machineCursor * 16), _itemSkin.Text("gText_SelectorArrow2"));
                    if (mode == "teach-confirm" && _machineSlot >= 0)
                    {
                        _battleSkin.DrawStandardFrame(this, new(0, 64, 184, 48));
                        var old = _session.State.Party[_machineParty].Pokemon.Moves[_machineSlot];
                        SourceText(_sourceSmall, new(8, 72), MoveName(old.Move));
                        SourceText(_sourceSmall, new(8, 88), _itemSkin.Text("gText_SelectorArrow2") + " " + MachineMoveName);
                    }
                }
            }
        }
        if (_session.State.ItemMenu.View == "message") ItemMessageBox(_session.VisibleItemMessage!);
        if (_itemNotice is not null) ItemMessageBox(_itemNotice);
        return true;
    }
}
