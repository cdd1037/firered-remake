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
    private bool TrainerTowerMenuActive => _session.State.TrainerTowerMenu is not null;
    private bool TrainerTowerMenuVisible => TrainerTowerMenuActive && FieldTextComplete &&
        (_session.State.Dialogue is null || _session.State.Dialogue.Page + 1 == _session.State.Dialogue.Pages.Length);
    private bool TrainerTowerMenuReady => TrainerTowerMenuVisible && _waitElapsed >= _waitDuration && _fade <= 0 && _tracks.All(t => t.Time >= t.Duration);
    private int TrainerTowerMenuWidth => (int)_trainerTowerMenu!.Options.Max(t => _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(t), fontSize: _sourceNormal.Size).X);
    private bool IsTrainerTowerPresentationMap(MapId map) => _catalog.TrainerTowerEnabled && _catalog.TrainerTowerRegion.Maps.ContainsKey(map);
    private string PresentationMapKey(MapId map) => IsTrainerTowerPresentationMap(map)
        ? map.Value + ":" + _catalog.TrainerTowerRegion.LayoutId(map, (PretTrainerTowerLayoutFormat)_session.TrainerTowerLayout(map)) : map.Value;
    private PretWorldMap PresentationSourceMap(MapId map) => IsTrainerTowerPresentationMap(map)
        ? (PretWorldMap)(_catalog.TrainerTowerMap(map, _session.TrainerTowerLayout(map)) ?? throw new InvalidDataException("Tower layout art missing.")) : _catalog.SourceMaps[map];

    private void LoadTrainerTowerPresentation()
    {
        if (!_catalog.TrainerTowerEnabled) return;
        if (!_session.IsCurrentGame || !_catalog.PowerPlantEnabled || _battleArt.Count != 852)
            throw new InvalidDataException("Trainer Tower requires the current Power Plant presentation predecessor.");
        var catalog = PretTrainerTowerPresentation.Load(_root); _trainerTowerArt = catalog;
        byte[] Pixels(JsonElement row) => File.ReadAllBytes(Path.Combine(_root, row.GetProperty("rgbaPath").GetString()!));
        Texture2D TextureFor(JsonElement row)
        {
            using var image = Image.CreateFromData(row.GetProperty("width").GetInt32(), row.GetProperty("height").GetInt32(), false, Image.Format.Rgba8, Pixels(row));
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
                using var old = inherited.Texture.GetImage(); old.Convert(Image.Format.Rgba8);
                if (inherited.Position != position || !old.GetData().SequenceEqual(Pixels(row))) throw new InvalidDataException("Inherited Tower battle pixels differ: " + id);
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
        Write("trainerTowerPresentation", new { PretTrainerTowerPresentation.ManifestSha256, sourceSprites = 80,
            newOpponentSpecies = 18, trainerPortraits = _trainerTowerPortraits.Count, battleSprites = _battleArt.Count,
            layouts = 27, nativeWindowVerified = false, physicalAudioVerified = false });
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
            var course = _catalog.TrainerTowerCatalog.Course(Enum.Parse<TrainerTowerChallengeKind>(tower.Course));
            return _catalog.TrainerTowerCatalog.Template(course.Floors[tower.FloorIndex].TemplateSymbol).Trainers[tower.TrainerIndex];
        }
    }
    private string? TrainerTowerTrainerText(string field) => VisibleTrainerTowerTrainer is {} trainer
        ? field == "name" ? trainer.Name : _trainerTowerBindings[trainer.FacilityClass].GetProperty(field).GetString() : null;
    private Texture2D? TrainerTowerTrainerPortrait => TrainerTowerTrainerText("portrait") is {} id ? _trainerTowerPortraits[id] : null;
    private Vector2 VisibleTrainerPortraitPosition => TrainerTowerTrainerText("portrait") is {} id ? _trainerTowerPortraitPositions[id] : new(144, 8);
    private Texture2D VisibleTrainerPortrait => TrainerTowerTrainerPortrait ?? _forestTrainerPortraits[VisibleTrainerPortraitId];
    private string? TrainerTowerSpeciesName(int species) => _catalog.TrainerTowerEnabled
        ? _catalog.TrainerTowerOpponents.Catalog.Species.Values.Any(s => s.SourceSpeciesId == species)
            ? (_pokedexMenu ?? throw new InvalidDataException("Tower species names missing.")).Entries[species].Name : null : null;

    private string? TrainerTowerGraphics(string graphics)
    {
        if (!IsTrainerTowerPresentationMap(_session.State.World.Player.MapId) || !graphics.StartsWith("OBJ_EVENT_GFX_VAR_", StringComparison.Ordinal)) return null;
        if (!int.TryParse(graphics[18..], out int slot) || slot is < 0 or > 3) throw new InvalidDataException("Tower object graphics slot differs.");
        int value = _session.State.Script.Variables.GetValueOrDefault("VAR_OBJ_GFX_ID_" + slot);
        return _art.Npcs.Keys.FirstOrDefault(s => _catalog.TrainerTowerRegion.Constants.GetValueOrDefault(s, -1) == value)
            ?? throw new InvalidDataException("Tower dynamic object graphics missing: " + value);
    }
    private bool TryTrainerTowerPatch(Image canvas, FieldMetatileEdit edit, int layer, MapId map)
    {
        if (!IsTrainerTowerPresentationMap(map)) return false;
        var source = PresentationSourceMap(map);
        var cell = source.Cells.FirstOrDefault(c => c.MetatileId == edit.SourceMetatileId)
            ?? throw new InvalidDataException("Tower source patch tile missing: " + edit.SourceMetatileId);
        var original = GetMapArt(map);
        using var pixels = (Image)(layer == 0 ? original.Bottom : layer == 1 ? original.Middle : original.Top).GetImage().Duplicate();
        canvas.BlitRect(pixels, new(cell.X * 16, cell.Y * 16, 16, 16), new(edit.X * 16, edit.Y * 16)); return true;
    }

    private void ProcessTrainerTowerTime(double delta)
    {
        _trainerTowerFrames.Advance(delta, !PlayerEntryActive && _session.State.TrainerTower is { TimerRunning: true });
        CommitTrainerTowerTime(false);
    }
    private void CommitTrainerTowerTime(bool flush)
    {
        if (_busy || !_session.TrainerTowerEnabled) return;
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
    private void SaveCampaignWithTrainerTowerClock(string path)
    {
        CommitTrainerTowerTime(true);
        ulong started = Time.GetTicksUsec();
        try { new SaveService(EntrySaveStorage()).Save(path, _session.State, _session.Revision, _session.RulesVersion, _catalog.ContentVersion, _session.ValidatePlayerSave); }
        finally { _trainerTowerFrames.ExcludeSaveWrite((Time.GetTicksUsec() - started) / 1_000_000d); }
    }

    private void SynchronizeTrainerTowerMenu()
    {
        if (!TrainerTowerMenuActive) { _trainerTowerMenu = null; _trainerTowerMenuWait = null; return; }
        string? wait = _session.State.Script.Wait?.CorrelationId;
        if (wait is null) throw new InvalidDataException("Tower menu source wait missing.");
        if (_trainerTowerMenuWait == wait) return;
        var menu = _session.State.TrainerTowerMenu!;
        _trainerTowerMenu = new(menu.SourceSymbol, menu.Options); _trainerTowerMenuWait = wait; _held = null; _repeat = 0;
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
