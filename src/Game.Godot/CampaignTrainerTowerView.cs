using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.World;
using Game.Presentation.BattleAnimations;
using Godot;

/// <summary>Source Tower art and facility identity; course rules and time remain authoritative commands.</summary>
public partial class CampaignView
{
    private JsonElement? _trainerTowerArt;
    private readonly Dictionary<string, Texture2D> _trainerTowerPortraits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> _trainerTowerBindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2> _trainerTowerPortraitPositions = new(StringComparer.Ordinal);
    private readonly TrainerTowerFrameAccumulator _trainerTowerFrames = new();
    private TrainerTowerMenuPresentation? _trainerTowerMenu;
    private string? _trainerTowerMenuWait;
    private CampaignDialogue? _trainerTowerSpeechDialogue;
    private string[] _trainerTowerSpeechPages = [];
    private int _trainerTowerSpeechPage;
    private bool TrainerTowerMenuActive => _session.State.TrainerTowerMenu is not null;
    private bool TrainerTowerMenuVisible => TrainerTowerMenuActive && FieldTextComplete &&
        (_session.State.Dialogue is null || _session.State.Dialogue.Page + 1 == _session.State.Dialogue.Pages.Length);
    private bool TrainerTowerMenuReady => TrainerTowerMenuVisible && _waitElapsed >= _waitDuration && _fade <= 0 && _tracks.All(t => t.Time >= t.Duration);
    private int TrainerTowerMenuWidth => (int)_trainerTowerMenu!.Options.Max(t => _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(t), fontSize: _sourceNormal.Size).X);
    private bool IsTrainerTowerPresentationMap(MapId map) => _catalog.TrainerTowerRegion.Maps.ContainsKey(map);
    private string PresentationMapKey(MapId map) => IsTrainerTowerPresentationMap(map)
        ? map.Value + ":" + _catalog.TrainerTowerRegion.LayoutId(map, (PretTrainerTowerLayoutFormat)_session.TrainerTowerLayout(map)) : SeafoamPresentationMapKey(map);
    private PretWorldMap PresentationSourceMap(MapId map) => IsTrainerTowerPresentationMap(map)
        ? (PretWorldMap)(_catalog.TrainerTowerMap(map, _session.TrainerTowerLayout(map)) ?? throw new InvalidDataException("Tower layout art missing.")) : SeafoamPresentationSourceMap(map);

    private void LoadTrainerTowerPresentation()
    {
        var catalog = PretTrainerTowerPresentation.Load(_root); _trainerTowerArt = catalog;
        Texture2D TextureFor(JsonElement row)
        {
            using var image = NativeImages.LoadPng(File.ReadAllBytes(Path.Combine(_root, row.GetProperty("texturePath").GetString()!)), row.GetProperty("width").GetInt32(), row.GetProperty("height").GetInt32());
            return SceneTextures.FromImage(image);
        }
        foreach (var row in catalog.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            string id = row.GetProperty("speciesId").GetString()!, side = row.GetProperty("side").GetString()!;
            if (_catalog.TrainerTowerOpponents.Catalog.Species[new(id)].SourceSpeciesId != species) throw new InvalidDataException("Tower sprite species differs.");
            var key = (species, side, row.GetProperty("paletteVariant").GetString() == "shiny");
            var position = new Vector2(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32());
            if (_battleArt.TryGetValue(key, out var inherited))
            {
                if (inherited.Position != position) throw new InvalidDataException("Inherited Tower battle pixels differ: " + id);
            }
            else _battleArt.Add(key, new(TextureFor(row), position));
        }
        foreach (var row in catalog.GetProperty("trainerPortraits").EnumerateArray())
        {
            string id = row.GetProperty("id").GetString()!;
            _trainerTowerPortraits.Add(id, TextureFor(row));
            _trainerTowerPortraitPositions.Add(id, new(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32()));
        }
        foreach (var row in catalog.GetProperty("trainerBindings").EnumerateArray())
            _trainerTowerBindings.Add(row.GetProperty("facilityClass").GetString()!, row);
        foreach (var row in catalog.GetProperty("itemIcons").EnumerateArray())
        {
            ItemId id = new(row.GetProperty("itemId").GetString()!);
            // Existing regions retain their original textures. New prizes include METAL COAT.
            if (!_earlyItemIcons.ContainsKey(id)) _earlyItemIcons.Add(id, (ImageTexture)TextureFor(row));
        }

    }

    private void AddTrainerTowerMoveGeometry()
    {
        if (_trainerTowerArt is not {} catalog) return;
        foreach (var row in catalog.GetProperty("sprites").EnumerateArray().Where(r => r.GetProperty("paletteVariant").GetString() == "normal"))
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32(); string side = row.GetProperty("side").GetString()!;
            var geometry = new AnimationBattler(new(side == "back" ? 0 : 1), "", row.GetProperty("sourceYOffset").GetInt32(),
                side == "front" ? row.GetProperty("sourceEnemyElevation").GetInt32() : 0,
                row.GetProperty("sourceWidth").GetInt32(), row.GetProperty("sourceHeight").GetInt32());
            if (_sourceBattlerGeometry.TryGetValue((species, side), out var old))
            {
                if (old.YOffset != geometry.YOffset || old.Elevation != geometry.Elevation || old.SourceWidth != geometry.SourceWidth || old.SourceHeight != geometry.SourceHeight)
                    throw new InvalidDataException("Inherited Tower move geometry differs.");
            }
            else _sourceBattlerGeometry.Add((species, side), geometry);
        }
    }

    private SourceBattleFrame? VisibleTrainerTowerFrame => _session.State.ForestTrainerBattle?.Presentation.Frame?.TrainerTowerEncounter is not null
        ? _session.State.ForestTrainerBattle.Frame : _session.State.DoubleBattle?.Frame.TrainerTowerEncounter is not null ? _session.State.DoubleBattle.Frame : null;
    private TrainerTowerTrainerTemplate? VisibleTrainerTowerTrainer
    {
        get
        {
            if (VisibleTrainerTowerFrame?.TrainerTowerEncounter is not {} tower) return null;
            var course = _catalog.TrainerTowerCatalog.Course(Enum.Parse<TrainerTowerChallengeKind>(tower.Roster.Course));
            return _catalog.TrainerTowerCatalog.Template(course.Floors[tower.Roster.FloorIndex].TemplateSymbol).Trainers[tower.Roster.TrainerIndex];
        }
    }
    private string? TrainerTowerTrainerText(string field) => VisibleTrainerTowerTrainer is {} trainer
        ? field == "name" ? trainer.Name : _trainerTowerBindings[trainer.FacilityClass].GetProperty(field).GetString() : null;
    private Texture2D? TrainerTowerTrainerPortrait => TrainerTowerTrainerText("portrait") is {} id ? _trainerTowerPortraits[id] : null;
    private Vector2 VisibleTrainerPortraitPosition => VisibleSharedTrainer?.Position ?? (TrainerTowerTrainerText("portrait") is {} id ? _trainerTowerPortraitPositions[id] : new(144, 8));
    private Texture2D VisibleTrainerPortrait => VisibleSharedTrainer?.Portrait ?? TrainerTowerTrainerPortrait ?? _forestTrainerPortraits[VisibleTrainerPortraitId];

    // trainer_tower.c TT_ConvertEasyChatMessageToString keeps Easy Chat words intact,
    // including words whose source spelling contains spaces; it is not ordinary word wrapping.
    private string FormatTrainerTowerSpeech(IEnumerable<TrainerTowerWord> source)
    {
        var words = source.Select(w => w.Text).ToArray();
        if (words.Length != 6) throw new InvalidDataException("Tower Easy Chat word count differs.");
        string[] Rows(int width) => words.Chunk(width).Select(row => string.Join(" ", row)).ToArray();
        var rows = Rows(3);
        if (rows.Any(row => _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(row), fontSize: _sourceNormal.Size).X > 196)) rows = Rows(2);
        return string.Join('\n', rows);
    }
    private string? TrainerTowerBattleSpeech(int opponentIndex)
    {
        if (VisibleTrainerTowerFrame is not {} frame || frame.TrainerTowerEncounter is not {} tower) return null;
        var course = _catalog.TrainerTowerCatalog.Course(Enum.Parse<TrainerTowerChallengeKind>(tower.Roster.Course));
        var template = _catalog.TrainerTowerCatalog.Template(course.Floors[tower.Roster.FloorIndex].TemplateSymbol);
        var trainer = template.Trainers[tower.Roster.TrainerIndex + (frame.Layout == SourceBattleLayout.PresetDoubles ? opponentIndex : 0)];
        return FormatTrainerTowerSpeech(frame.Outcome == Game.Domain.WildBattle.WildBattleOutcome.Won ? trainer.SpeechLose : trainer.SpeechWin);
    }
    private string? PresentedFieldText => _trainerTowerSpeechPages.Length > 0
        ? _trainerTowerSpeechPages[_trainerTowerSpeechPage] : _session.FormatVisibleText(CampaignSourceText.PrepareDisplayPage);
    private void SynchronizeTrainerTowerSpeech()
    {
        var dialogue = _session.State.Dialogue;
        if (dialogue == _trainerTowerSpeechDialogue) return;
        _trainerTowerSpeechDialogue = dialogue; _trainerTowerSpeechPages = []; _trainerTowerSpeechPage = 0;
        if (dialogue?.TextSymbol != "gStringVar4" || !IsTrainerTowerPresentationMap(_session.State.World.Player.MapId)) return;
        string map = _session.State.World.Player.MapId.Value;
        if (map.Length != "map.trainer-tower-1f".Length || map[^2] is < '1' or > '8') return;
        var course = _catalog.TrainerTowerCatalog.Course(_session.State.TrainerTower!.SelectedCourse);
        var template = _catalog.TrainerTowerCatalog.Template(course.Floors[map[^2] - '1'].TemplateSymbol);
        var trainer = template.Trainers[_session.State.Script.Variables.GetValueOrDefault("VAR_0x8006")];
        var words = _session.State.Script.Variables.GetValueOrDefault("VAR_0x8005") switch
        { 2 => trainer.SpeechBefore, 3 => trainer.SpeechWin, 4 => trainer.SpeechLose, 5 => trainer.SpeechAfter, _ => throw new InvalidDataException("Tower speech kind differs.") };
        _trainerTowerSpeechPages = SourceTrainerPresentationModel.ScrollResultPages(FormatTrainerTowerSpeech(words)).ToArray();
    }
    private bool TryTrainerTowerSpeechInput(InputEventKey key)
    {
        if (!key.Pressed || key.Keycode is not (Key.Enter or Key.Backspace) || !FieldTextComplete ||
            _trainerTowerSpeechPage + 1 >= _trainerTowerSpeechPages.Length) return false;
        _trainerTowerSpeechPage++; _held = null; SynchronizeFieldText(); return true;
    }

    private string? TrainerTowerGraphics(string graphics)
    {
        if (!IsTrainerTowerPresentationMap(_session.State.World.Player.MapId) || !graphics.StartsWith("OBJ_EVENT_GFX_VAR_", StringComparison.Ordinal)) return null;
        if (!int.TryParse(graphics[18..], out int slot) || slot is < 0 or > 3) throw new InvalidDataException("Tower object graphics slot differs.");
        int value = _session.State.Script.Variables.GetValueOrDefault("VAR_OBJ_GFX_ID_" + slot);
        return _art.Npcs.Keys.FirstOrDefault(s => _catalog.TrainerTowerRegion.Constants.GetValueOrDefault(s, -1) == value)
            ?? throw new InvalidDataException("Tower dynamic object graphics missing: " + value);
    }

    private void ObserveTrainerTowerTime(double delta)=>
        _trainerTowerFrames.Advance(delta, !PlayerEntryActive && _session.State.TrainerTower is { TimerRunning: true });

    private void ProcessTrainerTowerTime(double delta)
    {
        ObserveTrainerTowerTime(delta);
        CommitTrainerTowerTime(false);
    }
    private void CommitTrainerTowerTime(bool flush)
    {
        if (_busy) return;
        int frames;
        while ((frames = _trainerTowerFrames.Take(flush)) > 0)
        {
            long before = _session.Revision;
            var result = _session.Execute("ui:" + (_sequence++), before, new("trainer_tower_tick", Quantity: frames));
            if (result.Status != CommandStatus.Accepted) throw new InvalidDataException("Trainer Tower clock failed: " + result.Error);
            // Time is the only mutation: keep pending move animations and their continuation valid.
            if (_sourceMoveSession == _session && _sourceMoveRevision == before) _sourceMoveRevision = _session.Revision;
            Write("trainerTowerClock", new { frames, result.Revision, timer = _session.State.TrainerTower?.CurrentRecord.TimerFrames });
        }
    }
    private void SynchronizeTrainerTowerMenu()
    {
        if (!TrainerTowerMenuActive) { _trainerTowerMenu = null; _trainerTowerMenuWait = null; return; }
        string? wait = _session.State.Script.Wait?.CorrelationId;
        if (wait is null) throw new InvalidDataException("Tower menu source wait missing.");
        if (_trainerTowerMenuWait == wait) return;
        var menu = _session.State.TrainerTowerMenu!;
        int defaultCursor = menu.SourceSymbol == "MULTICHOICE_ROOFTOP_B1F"
            ? _session.State.TrainerTowerElevator?.MenuDefault ?? throw new InvalidDataException("Tower elevator menu default missing.") : 0;
        _trainerTowerMenu = new(menu.SourceSymbol, menu.Options, defaultCursor); _trainerTowerMenuWait = wait; _held = null; _repeat = 0;
    }
    private bool TryTrainerTowerInput(InputEventKey key)
    {
        if ((!TrainerTowerMenuActive && !_session.State.TrainerTowerRecordsVisible) || key.Keycode is Key.Escape or Key.F5 or Key.F9 or Key.F12) return false;
        _held = null; _repeat = 0;
        if (!key.Pressed) return true;
        if (_session.State.TrainerTowerRecordsVisible)
        {
            if (key.Keycode is Key.Enter or Key.Backspace) { SourceSound("SE_SELECT"); Apply(new("ack")); }
            return true;
        }
        if (!TrainerTowerMenuReady) return true;
        if (key.Keycode is Key.Up or Key.Down)
        { if (_trainerTowerMenu!.Move(key.Keycode == Key.Up ? -1 : 1)) SourceSound("SE_SELECT"); }
        else if (key.Keycode is Key.Enter or Key.Backspace)
        { SourceSound("SE_SELECT"); Apply(new("ack", Choice: key.Keycode == Key.Enter ? _trainerTowerMenu!.Cursor : TrainerTowerMenuPresentation.Cancel)); }
        return true;
    }
    private bool NavigateTrainerTowerPointer(Vector2 point, int direction)
    {
        if (!TrainerTowerMenuReady || _trainerTowerMenu is not {} menu) return false;
        int row = menu.RowAt(point.X, point.Y, TrainerTowerMenuWidth);
        return row >= 0 && (direction != 0 ? MouseNavigationKey(direction) : menu.Select(row));
    }
    private bool MouseTrainerTower(Vector2 point)
    {
        if (_session.State.TrainerTowerRecordsVisible) return MouseKey();
        if (!TrainerTowerMenuReady || _trainerTowerMenu is not {} menu) return false;
        int row = menu.RowAt(point.X, point.Y, TrainerTowerMenuWidth); if (row < 0) return false;
        menu.Select(row); return MouseKey();
    }
    private void DrawTrainerTowerMenu()
    {
        // Same native current-floor window as the Department Store: top-right 7x4
        // interior, source floor label aligned to its 56-pixel right edge.
        if (_session.State.World.Player.MapId.Value == "map.trainer-tower-elevator" &&
            _session.State.TrainerTowerElevator is { FloorWindowVisible: true } elevator)
        {
            _battleSkin.DrawUserFrame(this, new(168, 0, 72, 48));
            SourceText(_sourceNormal, new(176, 10), "Now on:");
            int floor = _session.State.Script.Variables.GetValueOrDefault("VAR_0x8005", elevator.Floor);
            string label = TrainerTowerMenuPresentation.ElevatorFloorLabel(floor);
            float width = _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(label), fontSize: _sourceNormal.Size).X;
            SourceText(_sourceNormal, new(232 - width, 24), label);
        }
        if (!TrainerTowerMenuVisible || _trainerTowerMenu is not {} menu) return;
        var f = menu.Frame(TrainerTowerMenuWidth); _battleSkin.DrawUserFrame(this, new(f.X, f.Y, f.Width, f.Height));
        for (int i = 0; i < menu.Options.Count; i++) SourceText(_sourceNormal, new(f.X + 16, f.Y + 10 + i * 14), menu.Options[i]);
        SourceText(_sourceNormal, new(f.X + 8, f.Y + 10 + menu.Cursor * 14), "▶");
    }
    private bool DrawTrainerTowerRecords()
    {
        if (!_session.State.TrainerTowerRecordsVisible) return false;
        DrawRect(new(0, 0, 240, 160), new Color("f8f8e8")); _battleSkin.DrawUserFrame(this, new(8, 8, 224, 144));
        SourceText(_sourceNormal, new(24, 19), "TRAINER TOWER RECORDS");
        var records = (_session.State.TrainerTower ?? TrainerTowerProgressRules.Create()).Records;
        string[] names = ["SINGLE", "DOUBLE", "KNOCKOUT", "MIXED"];
        for (int i = 0; i < names.Length; i++)
        {
            SourceText(_sourceNormal, new(24, 43 + i * 20), names[i]);
            SourceText(_sourceNormal, new(140, 43 + i * 20), TrainerTowerProgressRules.FormatTime(records[i].BestTimeFrames).ToString());
        }
        SourceText(_sourceSmall, new(24, 132), "Enter / Back: CLOSE"); return true;
    }
}
