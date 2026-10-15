using Game.Application;
using Game.Content;
using Godot;

/// <summary>Source-derived healing-machine presentation. Never issues commands or modifies party state.</summary>
public partial class CampaignView
{
    private CenterHealingArtDocument? _centerHealingArt;
    private readonly Dictionary<string, ImageTexture> _centerHealingTextures = new(StringComparer.Ordinal);
    private CampaignHealingEffect? _centerHealingEffect;
    private string? _centerHealingWaitId;
    private double _centerHealingElapsed;
    private int _centerHealingTick = -1;
    private const string CenterHealingMap = "map.viridian-pokemon-center-1f";

    private void LoadCenterHealingPresentation()
    {
        DisposeCenterHealingPresentation();
        if (!_catalog.ViridianCenterEnabled) return;
        var directory = System.IO.Path.Combine(_root, "content/generated/ui/center-healing-art");
        var document = PretCenterHealingArt.Load(System.IO.File.ReadAllText(System.IO.Path.Combine(directory, "center-healing-art.json")));
        try
        {
            foreach (var asset in document.Assets)
            {
                using var image = new Image();
                if (image.LoadPngFromBuffer(PretCenterHealingArt.ReadTexture(directory, asset)) != Error.Ok
                    || image.GetWidth() != asset.Width || image.GetHeight() != asset.Height || image.GetFormat() != Image.Format.Rgba8)
                    throw new InvalidDataException("Healing texture pixels differ: " + asset.Id);
                _centerHealingTextures.Add(asset.Id, ImageTexture.CreateFromImage(image));
            }
            _centerHealingArt = document;
        }
        catch
        {
            DisposeCenterHealingPresentation(); throw;
        }
    }

    private void DisposeCenterHealingPresentation()
    {
        foreach(var texture in _centerHealingTextures.Values)texture.Dispose();
        _centerHealingTextures.Clear(); _centerHealingArt=null; ResetCenterHealing();
    }

    private void ResetCenterHealing()
    { _centerHealingEffect = null; _centerHealingWaitId = null; _centerHealingElapsed = 0; _centerHealingTick = -1; }

    private int CenterHealingTotalTicks => _centerHealingEffect is {} effect && _centerHealingArt is {} art
        ? art.Timing.BallSpacingTicks * (effect.PartyCount - 1) + art.Timing.ActiveTicksBase : 0;
    // t=0 is the first visible ball, held for the first 1/60 second. Includes the final removal tick.
    private double CenterHealingDuration => CenterHealingTotalTicks / 60.0;
    private int CenterHealingFanfareTick => _centerHealingArt!.Timing.BallSpacingTicks * (_centerHealingEffect!.PartyCount - 1)
        + _centerHealingArt.Timing.LastBallPauseTicks;

    private void SynchronizeCenterHealing(CampaignState? before, CampaignState after)
    {
        var effect = after.HealingEffect;
        if (effect is null || !_catalog.ViridianCenterEnabled || !(after.World.Player.MapId.Value==CenterHealingMap||_catalog.PewterEnabled&&after.World.Player.MapId.Value=="map.pewter-pokemon-center-1f"||_catalog.Route3Enabled&&after.World.Player.MapId.Value=="map.route-4-pokemon-center-1f"||_catalog.CeruleanEnabled&&after.World.Player.MapId.Value=="map.cerulean-city-pokemon-center-1f"||_catalog.VermilionEnabled&&after.World.Player.MapId.Value=="map.vermilion-city-pokemon-center-1f"||_catalog.PokemonLeagueEnabled&&after.World.Player.MapId.Value=="map.indigo-plateau-pokemon-center-1f"))
        { ResetCenterHealing(); return; }
        if (_centerHealingArt is null) throw new InvalidDataException("Healing presentation assets are not loaded.");
        if (effect.Symbol != "FLDEFF_POKECENTER_HEAL" || effect.PartyCount is < 1 or > 6)
            throw new InvalidDataException("Unsupported healing presentation effect.");
        var waitId = after.Script.Wait?.CorrelationId;
        if (_centerHealingEffect == effect && _centerHealingWaitId == waitId) return;
        _centerHealingEffect = effect; _centerHealingWaitId = waitId; _centerHealingElapsed = 0; _centerHealingTick = -1;
        AdvanceCenterHealingTo(0);
    }

    private void ProcessCenterHealing(double delta)
    {
        if (_centerHealingEffect is null) return;
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        _centerHealingElapsed = Math.Min(CenterHealingDuration, _centerHealingElapsed + delta);
        AdvanceCenterHealingTo(Math.Min(CenterHealingTotalTicks, (int)Math.Floor(_centerHealingElapsed * 60 + 1e-9)));
    }

    private void AdvanceCenterHealingTo(int next)
    {
        int fanfare = CenterHealingFanfareTick;
        for (int tick = _centerHealingTick + 1; tick <= next; tick++)
        {
            if (tick % _centerHealingArt!.Timing.BallSpacingTicks == 0 && tick / _centerHealingArt.Timing.BallSpacingTicks < _centerHealingEffect!.PartyCount)
                CenterHealingCue("SE_BALL", tick, tick / _centerHealingArt.Timing.BallSpacingTicks + 1);
            if (tick == fanfare) CenterHealingCue("MUS_HEAL", tick);
            if (tick == fanfare + _centerHealingArt.Timing.FanfareCounterTicks + 1) CenterHealingCue("fanfare_task_inactive", tick);
            if (tick == CenterHealingTotalTicks - 1) CenterHealingCue("field_effect_removed", tick);
        }
        _centerHealingTick = next;
    }

    private void CenterHealingCue(string symbol, int tick, int? ball = null)
    {
        if(symbol=="MUS_HEAL")SourceSound(symbol,fanfare:true);else if(symbol.StartsWith("SE_",StringComparison.Ordinal))SourceSound(symbol);
        Write("centerHealingCue", new { symbol, sourceTick = tick, ball, partyCount = _centerHealingEffect!.PartyCount,
            fanfareCounterTicks = symbol == "MUS_HEAL" ? _centerHealingArt!.Timing.FanfareCounterTicks : (int?)null,
            source = "src/field_effect.c;src/sound.c", sourceCommit = PretCenterHealingArt.SourceCommit, audibleVerified = false });
    }

    private int CenterHealingBallCount => _centerHealingEffect is null || _centerHealingTick >= CenterHealingFanfareTick + 150 ? 0
        : Math.Min(_centerHealingEffect.PartyCount, _centerHealingTick / _centerHealingArt!.Timing.BallSpacingTicks + 1);

    private int CenterHealingPalette
    {
        get
        {
            if (_centerHealingEffect is null) return 0;
            int t = _centerHealingTick - CenterHealingFanfareTick;
            if (t <= 0) return 0;
            if (t <= 96) return 1 + (t / 8 & 3);
            return 5 + Math.Min(3, (t - 96) / 8);
        }
    }

    private int CenterHealingMonitorFrame
    {
        get
        {
            if (_centerHealingEffect is null) return -1;
            int tick = _centerHealingTick - CenterHealingFanfareTick - 1;
            // Callback sees animEnded one tick after the terminal animation command.
            int cycle = _centerHealingArt!.MonitorFrames.Sum(f => f.Ticks), length = cycle * _centerHealingArt.MonitorLoops;
            if (tick < 0 || tick > length) return -1;
            if (tick == length) return 0;
            tick %= cycle;
            foreach (var frame in _centerHealingArt.MonitorFrames)
            { if (tick < frame.Ticks) return frame.Frame; tick -= frame.Ticks; }
            throw new InvalidDataException("Healing monitor frame is unavailable.");
        }
    }

    private void DrawCenterHealing()
    {
        if (_centerHealingEffect is null || _centerHealingArt is null) return;
        // Source effect uses screen coordinates. OAM coordinates are centers, not top-left corners.
        int palette = CenterHealingPalette;
        for (int i = 0; i < CenterHealingBallCount; i++)
        {
            var offset = _centerHealingArt.BallOffsets[i];
            var position = new Vector2(_centerHealingArt.FirstBall.X + offset.X - 4, _centerHealingArt.FirstBall.Y + offset.Y - 4);
            DrawTextureRectRegion(_centerHealingTextures["pokeball-glow"], new Rect2(position, new Vector2(8, 8)), new Rect2(0, palette * 8, 8, 8));
        }
        int frame = CenterHealingMonitorFrame;
        if (frame >= 0)
            DrawTextureRectRegion(_centerHealingTextures["pokecenter-monitor"], new Rect2(_centerHealingArt.Monitor.X - 16, _centerHealingArt.Monitor.Y - 8, 32, 16), new Rect2(frame * 32, palette * 16, 32, 16));
    }
}
