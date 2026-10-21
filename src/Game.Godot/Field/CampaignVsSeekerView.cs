using Game.Application;
using Game.Content;
using Game.Domain.Options;
using Game.Domain.Trainers;
using Godot;
using Direction = Game.Domain.World.Direction;

/// <summary>Original scanner, independent reaction sprites, and persistent rematch idle poses.
/// Commands alone advance charge, RNG, response selection and rematch ownership.</summary>
public partial class CampaignView
{
    private sealed class VsIcon
    {
        public required string Actor;
        public required VsSeekerIcon Kind;
        public double Tick;
    }
    private sealed class VsIdle
    {
        public required string Movement;
        public double Tick;
    }
    private PretVsSeekerPresentation? _vsSeekerArt;
    private readonly Dictionary<string, ImageTexture> _vsSeekerTextures = new(StringComparer.Ordinal);
    private readonly List<VsIcon> _vsSeekerIcons = [];
    private readonly Dictionary<string, VsIdle> _vsSeekerIdle = new(StringComparer.Ordinal);
    private readonly SourceTextProgress _vsSeekerText = new();
    private string[] _vsSeekerMessagePages = [];
    private int _vsSeekerMessagePage;
    private double _vsSeekerFrameRemainder, _vsSeekerTextRemainder, _vsSeekerMessageElapsed;
    private bool _vsSeekerTextHeld, _vsSeekerTextPressed;
    private ulong _vsSeekerClosedFrame = ulong.MaxValue;

    private PretVsSeekerPresentation VsSeekerArt => _vsSeekerArt ??= PretVsSeekerPresentation.Load(_root);
    private static bool IsVsReady(string movement) => movement is "MOVEMENT_TYPE_RAISE_HAND_AND_STOP"
        or "MOVEMENT_TYPE_RAISE_HAND_AND_JUMP" or "MOVEMENT_TYPE_RAISE_HAND_AND_SWIM";

    private Texture2D VsSeekerTexture(string id)
    {
        // These two textures are already owned by the shared field-move presentation.
        if (id == "red-field-move" && _fieldCutRed is not null) return _fieldCutRed;
        if (id == "green-field-move" && _fieldCutGreen is not null) return _fieldCutGreen;
        if (_vsSeekerTextures.TryGetValue(id, out var old)) return old;
        var asset = VsSeekerArt.Asset(id);
        using var image = new Image();
        if (image.LoadPngFromBuffer(VsSeekerArt.ReadPng(id)) != Error.Ok || image.GetWidth() != asset.Width ||
            image.GetHeight() != asset.Height || image.GetFormat() != Image.Format.Rgba8)
            throw new InvalidDataException("Vs Seeker source pixels differ: " + id);
        var texture = ImageTexture.CreateFromImage(image); _vsSeekerTextures.Add(id, texture); return texture;
    }

    private void ExtendVsSeekerQuestPocket(ref QuestItemUiDocument document)
    {
        if (!_session.VsSeekerEnabled) return;
        var art = VsSeekerArt; var item = art.ItemDefinition; var icon = art.Asset("item-vs-seeker");
        string positionIcon = document.Items[0].Icon;
        var position = document.Assets.Single(a => a.Id == positionIcon);
        using var image = NativeImages.LoadPng(art.ReadItemIconPng(), 24, 24);
        _questPocketTextures.Add(icon.Id, SceneTextures.FromImage(image));
        document = document with
        {
            Items = document.Items.Add(new("pret:ITEM_VS_SEEKER", "ITEM_VS_SEEKER", SharedMachineCatalog.LoadProject(_root).Items.Definition(new("pret:ITEM_VS_SEEKER")).Name,
                item.GetProperty("description").GetString()!, icon.Id, "FieldUseFunc_VsSeeker")),
            Assets = document.Assets.Add(new(icon.Id,icon.Path,24,24,position.X,position.Y))
        };
    }

    private void SynchronizeVsSeekerPresentation(CampaignState? before, CampaignState after)
    {
        if (!_session.VsSeekerEnabled) return;
        if (before is null || before.World.Player.MapId != after.World.Player.MapId)
        {
            _vsSeekerIcons.Clear(); _vsSeekerIdle.Clear(); _vsSeekerFrameRemainder = 0;
            _vsSeekerMessagePages = []; _vsSeekerClosedFrame = ulong.MaxValue; ResetVsSeekerInput();
        }
        foreach (string id in _vsSeekerIdle.Keys.ToArray())
            if (!after.Actors.TryGetValue(id, out var actor) || !IsVsReady(actor.MovementType)) _vsSeekerIdle.Remove(id);
        foreach (var actor in after.Actors.Values.Where(a => IsVsReady(a.MovementType)))
            if (!_vsSeekerIdle.TryGetValue(actor.LocalId, out var idle) || idle.Movement != actor.MovementType)
                _vsSeekerIdle[actor.LocalId] = new() { Movement = actor.MovementType };
        var previous = before?.VsSeekerEffect; var current = after.VsSeekerEffect;
        if (current is not null) { _held = null; ReleaseFieldTextInput(); }
        if (previous is null && current is not null) _vsSeekerFrameRemainder = 0;
        if (previous?.Phase == "scan")
        {
            int end = current?.Phase == "scan" ? current.Tick : previous.Cycling ? VsSeekerTiming.CyclingScanTicks : VsSeekerTiming.NormalScanTicks;
            foreach (int tick in new[] { VsSeekerSourcePresentation.FirstSignalTaskTick, VsSeekerSourcePresentation.SecondSignalTaskTick })
                if (previous.Tick < tick && end >= tick) SourceSound("SE_CONTEST_MONS_TURN");
        }
        if (current is { Phase: "wait", Response: { } response } && previous?.Phase == "scan")
        {
            _vsSeekerIcons.Clear();
            foreach (var reaction in response.Reactions)
            {
                var actor = after.Actors.Values.FirstOrDefault(a => a.SourceLocalId == reaction.LocalId);
                if (actor is null || !actor.Visible) continue;
                var kind = reaction.Response switch { VsSeekerTrainerResponse.Unfought => VsSeekerIcon.Unfought,
                    VsSeekerTrainerResponse.Rematch => VsSeekerIcon.Accepted, _ => VsSeekerIcon.Declined };
                _vsSeekerIcons.Add(new() { Actor = actor.LocalId, Kind = kind,
                    Tick = current.Tick - (kind == VsSeekerIcon.Accepted ? VsSeekerSourcePresentation.AcceptedStepTicks : 0) });
            }
            if (response.Response == VsSeekerResponse.FoundRematches) SourceSound("SE_PIN");
            Write("vsSeekerResponse", new { response.Response, response.Reactions, sourceIconAnimationTicks = 60, sourceIconRetirementTick = 61,
                responseMovementTicks = VsSeekerTiming.ResponseMovementTicks, responseWaitTicks = VsSeekerTiming.WaitTicks });
        }
        if (current is { Phase: "message" } && (previous?.Phase != "message" || previous.Message != current.Message))
        {
            _vsSeekerMessagePages = (current.Message ?? "").Split("\n\n", StringSplitOptions.None);
            _vsSeekerMessagePage = 0; ResetVsSeekerMessagePage();
        }
        if (previous is not null && current is null)
        { _vsSeekerClosedFrame = Engine.GetProcessFrames(); _vsSeekerMessagePages = []; ResetVsSeekerInput(); }
    }

    private void ResetVsSeekerMessagePage()
    {
        _vsSeekerTextRemainder = _vsSeekerMessageElapsed = 0; ResetVsSeekerInput();
        _vsSeekerText.Reset(_vsSeekerMessagePages[_vsSeekerMessagePage], _session.EffectiveOptions.TextSpeed switch
            { SourceTextSpeed.Slow => 8, SourceTextSpeed.Mid => 4, _ => 1 });
    }
    private void ResetVsSeekerInput() { _vsSeekerTextHeld = _vsSeekerTextPressed = false; }

    private void AdvanceVsSeekerActors(double ticks, bool freezeReady)
    {
        foreach (var icon in _vsSeekerIcons) icon.Tick += ticks;
        _vsSeekerIcons.RemoveAll(i => i.Tick >= VsSeekerSourcePresentation.IconTicks || !_session.State.Actors.ContainsKey(i.Actor));
        if (freezeReady) return;
        foreach (var (id, idle) in _vsSeekerIdle)
            if (!_tracks.Any(t => t.Actor == id && t.Time < t.Duration)) idle.Tick += ticks;
    }

    private bool ProcessVsSeekerPresentation(double delta)
    {
        if (!_session.VsSeekerEnabled) return false;
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        var state = _session.State;
        if (state.VsSeekerEffect is null)
        {
            bool frozen = PlayerEntryActive || FieldMenuOpen || state.Script.Locked || state.ItemMenu is not null ||
                state.Battle is not null || state.WildBattle is not null || state.ForestTrainerBattle is not null ||
                state.DoubleBattle is not null || state.Shop is not null || state.Pc is not null || state.FameChecker is not null || state.SlotMachine is not null;
            AdvanceVsSeekerActors(delta * 60, frozen); return false;
        }
        _held = null; if (state.VsSeekerEffect.Phase == "message")
        {
            AdvanceVsSeekerActors(delta * 60, true);
            _vsSeekerMessageElapsed += delta; _vsSeekerTextRemainder += delta * 60;
            int frames = (int)Math.Min(60, Math.Floor(_vsSeekerTextRemainder + 1e-9));
            _vsSeekerTextRemainder -= frames;
            if (frames > 0) { _vsSeekerText.AdvanceFrames(frames, _vsSeekerTextHeld, _vsSeekerTextPressed); _vsSeekerTextPressed = false; }
            QueueRedraw(); return true;
        }
        _vsSeekerFrameRemainder += delta * 60;
        int pending = (int)Math.Min(60, Math.Floor(_vsSeekerFrameRemainder + 1e-9));
        if (_busy || _lastCommandFault) { QueueRedraw(); return true; }
        _vsSeekerFrameRemainder -= pending;
        while (pending > 0 && _session.State.VsSeekerEffect is { Phase: "scan" or "wait" } owner)
        {
            int duration = owner.Phase == "wait" ? VsSeekerTiming.WaitTicks : owner.Cycling ? VsSeekerTiming.CyclingScanTicks : VsSeekerTiming.NormalScanTicks;
            int count = Math.Min(pending, duration - owner.Tick);
            if (count <= 0) throw new InvalidDataException("Vs Seeker presentation owner exceeded its phase.");
            AdvanceVsSeekerActors(count, true);
            Apply(new("vs_seeker_tick", Quantity: count)); pending -= count;
            if (_lastCommandFault) break;
        }
        if (pending > 0) AdvanceVsSeekerActors(pending, _session.State.VsSeekerEffect is not null || _session.State.Script.Locked);
        QueueRedraw(); return true;
    }

    private bool TryVsSeekerInput(InputEvent input)
    {
        if (_session is null || !_session.VsSeekerEnabled) return false;
        if (input is InputEventKey { Keycode: Key.Escape or Key.F12 }) return false;
        if (_vsSeekerClosedFrame == Engine.GetProcessFrames()) { GetViewport().SetInputAsHandled(); return true; }
        if (_session.State.VsSeekerEffect is not { } owner)
        {
            bool use = input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.R } ||
                input is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Back };
            if (!use || _session.State.RegisteredItem != "pret:ITEM_VS_SEEKER" || FieldMenuOpen || PlayerEntryActive ||
                _session.State.Script.Locked || _session.State.ItemMenu is not null || _session.State.Pc is not null ||
                _session.State.Shop is not null || _session.State.SlotMachine is not null || _session.State.FameChecker is not null ||
                _session.State.Battle is not null || _session.State.WildBattle is not null || _session.State.ForestTrainerBattle is not null ||
                _session.State.DoubleBattle is not null) return false;
            _held = null; Apply(new("use_registered")); QueueRedraw(); GetViewport().SetInputAsHandled(); return true;
        }
        _held = null; bool confirm = input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.Backspace } ||
            input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right } ||
            input is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.A or JoyButton.B };
        bool release = input is InputEventKey { Pressed: false, Keycode: Key.Enter or Key.Backspace } ||
            input is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left or MouseButton.Right } ||
            input is InputEventJoypadButton { Pressed: false, ButtonIndex: JoyButton.A or JoyButton.B };
        if (release) _vsSeekerTextHeld = false;
        if (owner.Phase == "message" && confirm)
        {
            if (!_vsSeekerText.Complete) { _vsSeekerTextHeld = true; _vsSeekerTextPressed = true; }
            else if (_vsSeekerMessagePage + 1 < _vsSeekerMessagePages.Length) { _vsSeekerMessagePage++; ResetVsSeekerMessagePage(); }
            else Apply(new("vs_seeker_close"));
        }
        QueueRedraw(); GetViewport().SetInputAsHandled(); return true;
    }

    private bool DrawVsSeekerActor(string id, SpriteAsset asset, Pose pose, Vector2 camera)
    {
        if (!_session.VsSeekerEnabled) return false;
        if (id == "LOCALID_PLAYER" && _session.State.VsSeekerEffect is { Phase: "scan", Tick: > 0 } scan &&
            scan.Tick < VsSeekerSourcePresentation.ScannerAnimationTicks(scan.Cycling) + 3 &&
            VsSeekerSourcePresentation.ScannerSlice(_session.State.PlayerGender != 0, scan.Cycling,
                Math.Min(scan.Tick - 1, VsSeekerSourcePresentation.ScannerAnimationTicks(scan.Cycling) - 1)) is { } slice)
        {
            var position = pose.Position.Floor() + new Vector2(8 - slice.Width / 2, -16) - camera;
            DrawTextureRectRegion(VsSeekerTexture(slice.AssetId), new(position, new(slice.Width, slice.Height)),
                new(slice.Frame * slice.Width, 0, slice.Width, slice.Height)); return true;
        }
        var state = _session.State;
        if (!state.Actors.TryGetValue(id, out var actor)) return false;
        if (state.VsSeekerEffect is { Phase: "wait", Response: { } response } wait &&
            response.Reactions.Any(r => r.LocalId == actor.SourceLocalId && r.Response == VsSeekerTrainerResponse.Rematch))
        {
            // This host admits settled source NPCs. The source faster-in-place south action is four callbacks:
            // GoFasterSouth's first two-tick step frame, then its two-tick south idle frame.
            int frame = wait.Tick < 2 ? 3 : 0;
            DrawVsSeekerNpcFrame(asset, pose, camera, new(frame, 0, 0)); return true;
        }
        if (!_vsSeekerIdle.TryGetValue(id, out var idle) || _tracks.Any(t => t.Actor == id && t.Time < t.Duration) ||
            state.Script.Locked && state.LastTalked == id) return false;
        var framePose = VsSeekerSourcePresentation.ReadyIdlePose(idle.Movement, (long)idle.Tick);
        if (framePose.Frame < 0) return false;
        DrawVsSeekerNpcFrame(asset, pose, camera, framePose); return true;
    }

    private void DrawVsSeekerNpcFrame(SpriteAsset asset, Pose pose, Vector2 camera, VsSeekerIdlePose frame)
    {
        if (frame.Frame >= asset.Count) throw new InvalidDataException("Vs Seeker trainer source frame is missing.");
        var position = pose.Position.Floor() + new Vector2(8 + frame.XOffset, 8 + frame.YOffset) - asset.Anchor - camera;
        DrawTextureRectRegion(SixIslandPaletteTexture(asset.Texture, false), new(position, new(asset.Width, asset.Height)),
            new(frame.Frame * asset.Width, 0, asset.Width, asset.Height));
    }

    private void DrawVsSeekerIcons(Vector2 camera)
    {
        foreach (var icon in _vsSeekerIcons)
        {
            if (icon.Tick < 0 || !_poses.TryGetValue(icon.Actor, out var pose) || !pose.Visible ||
                !_session.State.Actors.TryGetValue(icon.Actor, out var actor) || !_art.Npcs.TryGetValue(actor.GraphicsId, out var asset)) continue;
            int tick = (int)icon.Tick, frame = VsSeekerSourcePresentation.IconFrame(icon.Kind, tick);
            if (frame < 0) continue;
            // Source icon center follows the actor sprite's center, not the tile origin or screen center.
            var center = pose.Position.Floor() + new Vector2(8, 8 + pose.DrawYOffset) - asset.Anchor + new Vector2(asset.Width / 2, asset.Height / 2);
            if (_session.State.VsSeekerEffect?.Phase != "wait" && _vsSeekerIdle.TryGetValue(icon.Actor, out var idle) &&
                !(_session.State.Script.Locked && _session.State.LastTalked == icon.Actor) &&
                !_tracks.Any(t => t.Actor == icon.Actor && t.Time < t.Duration))
            {
                var idlePose = VsSeekerSourcePresentation.ReadyIdlePose(idle.Movement, (long)idle.Tick);
                center += new Vector2(idlePose.XOffset, idlePose.YOffset);
            }
            var position = center - camera + new Vector2(-8, -24 + VsSeekerSourcePresentation.IconYOffset(tick));
            DrawTextureRectRegion(VsSeekerTexture("reaction-icons"), new(position, new(16, 16)), new(frame * 16, 0, 16, 16));
        }
    }

    private void DrawVsSeekerMessage()
    {
        if (_session.State.VsSeekerEffect?.Phase != "message" || _vsSeekerMessagePages.Length == 0) return;
        _battleSkin.DrawDialogue(this);
        var lines = _vsSeekerText.VisibleText.Split('\n');
        for (int i = 0; i < lines.Length; i++) SourceText(_sourceNormal, LabBattleUiSkin.DialogueText + new Vector2(0, i * 15), lines[i]);
        if (_vsSeekerText.Complete) DrawTextCursor(_sourceNormal, LabBattleUiSkin.DialogueText,
            _vsSeekerMessagePages[_vsSeekerMessagePage].Split('\n'), 15, _vsSeekerMessageElapsed);
    }

    private void DisposeVsSeekerPresentation()
    {
        foreach (var texture in _vsSeekerTextures.Values) texture.Dispose();
        _vsSeekerTextures.Clear(); _vsSeekerIcons.Clear(); _vsSeekerIdle.Clear(); _vsSeekerArt = null;
        _vsSeekerMessagePages = []; ResetVsSeekerInput();
    }
}
