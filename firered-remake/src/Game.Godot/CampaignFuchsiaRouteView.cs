using Game.Application;
using Game.Content;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Source presentation for the admitted southern route. No authoritative movement or battle mutation.</summary>
public partial class CampaignView
{

    private void LoadFuchsiaRoutePresentation()
    {
        if (!_catalog.FuchsiaRouteEnabled) return;
        var art = _catalog.FuchsiaRouteArt ?? throw new InvalidDataException("Fuchsia route art catalog missing.");
        if (_battleArt.Count != 412 || _storageIcons.Count != 103 || _route22Footprints.Count != 74)
            throw new InvalidDataException("Fuchsia route must extend the complete 103-species presentation.");
        foreach (var sprite in art.Sprites)
        {
            using var image = Image.CreateFromData(sprite.Width, sprite.Height, false, Image.Format.Rgba8, art.Files[sprite.RgbaPath]);
            _battleArt.Add((sprite.SourceSpeciesId, sprite.Side, sprite.PaletteVariant == "shiny"),
                new(SceneTextures.FromImage(image), new(sprite.SingleBattleTopLeft.X, sprite.SingleBattleTopLeft.Y)));
        }
        foreach (var icon in art.Icons)
        {
            var rgba = art.Files[icon.RgbaPath].ToArray();
            for (int i = 0; i < rgba.Length; i += 4)
                if (rgba[i + 3] != 0)
                    for (int c = 0; c < 3; c++) { int v = rgba[i + c] >> 3; rgba[i + c] = (byte)((v << 3) | (v >> 2)); }
            using var image = Image.CreateFromData(icon.Width, icon.Height, false, Image.Format.Rgba8, rgba);
            _storageIcons.Add(new(icon.SpeciesId), SceneTextures.FromImage(image));
        }
        foreach (var footprint in art.Footprints)
        {
            using var image = Image.CreateFromData(footprint.Width, footprint.Height, false, Image.Format.Rgba8, art.Files[footprint.RgbaPath]);
            _route22Footprints.Add(footprint.SourceSpeciesId, SceneTextures.FromImage(image));
        }
        foreach (var sprite in art.Sprites.Where(s => s.Side == "front" && s.PaletteVariant == "normal"))
            _portraits.Add(sprite.SourceSpeciesId, _battleArt[(sprite.SourceSpeciesId, "front", false)].Texture);
        if (_battleArt.Count != 428 || _storageIcons.Count != 107 || _route22Footprints.Count != 78)
            throw new InvalidDataException("Fuchsia route 107-species presentation inventory differs.");
        Write("fuchsiaRoutePresentation", new { species = 107, battleSprites = 428, icons = 107, footprints = 78,
            maps = _catalog.FuchsiaRouteRegion!.NewMaps.Count, PretFuchsiaRouteArt.ManifestSha256 });
    }

    private bool FuchsiaTransformCuePending(CampaignWildBattle battle) => _catalog.FuchsiaRouteEnabled && WildCurrentEvent(battle) is WildBattleTransformedEvent && _wildElapsed < 49.0 / 60;

    private void ProcessFuchsiaTransformAudio()
    {
        if (!_catalog.FuchsiaRouteEnabled || PresentedWildBattle is not { Phase: CampaignBattlePhase.Events } battle || WildCurrentEvent(battle) is not WildBattleTransformedEvent transformed) return;
        string key = $"{battle.Id}:transform:{transformed.Sequence}";
        if (_audioTrainerOneShots.Add(key + ":start")) SourceSound("SE_M_TELEPORT");
        if (_wildElapsed >= 48.0 / 60 && _audioTrainerOneShots.Add(key + ":48")) SourceSound("SE_M_MINIMIZE");
    }

    private ForestMessage? FuchsiaRouteWildMessage(WildBattleEvent current) => !_catalog.FuchsiaRouteEnabled ? null : current switch
    {
        WildBattleTransformedEvent transformed => new("sText_PkmnTransformedInto",
            ("B_ATK_NAME_WITH_PREFIX", WildPrefixedName(transformed.Actor)),
            ("B_BUFF1", PresentedBattleSpeciesName(PresentedSpeciesId(transformed.AppearanceSpecies)))),
        WildBattleMoveFailedEvent { Reason: "target-transformed" or "semi-invulnerable" } => new("sText_ButItFailed"),
        _ => null
    };

    private (int Species, bool Shiny) FuchsiaBattleAppearance(CampaignWildBattle battle, WildBattleSide side, PokemonInstance identity)
    {
        // The display projection advances at event boundaries; the final turn state can be ahead of the screen.
        // A withdrawing player still uses the previous combatant's source appearance.
        if (side == WildBattleSide.Player && WildWithdrawing(battle) && WildCurrentEvent(battle) is WildBattlePlayerSwitchedEvent switched)
        {
            var old = switched.Previous.Transform?.CopiedPokemon ?? switched.Previous.Pokemon;
            return PresentedAppearance(old);
        }
        var appearance = side == WildBattleSide.Player ? battle.DisplayPlayerAppearance : battle.DisplayOpponentAppearance;
        if (appearance is null) return PresentedAppearance(identity);
        var species = PresentedSpeciesId(appearance.Species);
        return SevenIslandRenderKeys.Appearance(species, appearance.Personality, appearance.OriginalTrainerId);
    }

    private bool IsFuchsiaLedgeStep(CampaignState? before, CampaignState after)
    {
        if (!_catalog.FuchsiaRouteEnabled || before is null || before.World.Player.MapId != after.World.Player.MapId || !after.Motions.IsEmpty) return false;
        int dx = after.World.Player.X - before.World.Player.X, dy = after.World.Player.Y - before.World.Player.Y;
        if (!(Math.Abs(dx) == 2 && dy == 0 || Math.Abs(dy) == 2 && dx == 0)) return false;
        int? behavior = _catalog.SourceMaps[before.World.Player.MapId].SourceBehaviorAt(before.World.Player.X + dx / 2, before.World.Player.Y + dy / 2);
        return (dx, dy, behavior) is (2, 0, 0x38) or (-2, 0, 0x39) or (0, -2, 0x3a) or (0, 2, 0x3b);
    }

    // pret event_object_movement.c: JUMP_DISTANCE_FAR uses 32 one-pixel frames and sJumpY_High[timer >> 1].
    // Keep y2 separate from the world/camera anchor, just as the original sprite does.
    private static int FuchsiaLedgeDrawYOffset(double elapsed)
    {
        ReadOnlySpan<int> high = [-4, -6, -8, -10, -11, -12, -12, -12, -11, -10, -9, -8, -6, -4, 0, 0];
        int frame = (int)Math.Floor(elapsed * 60);
        return frame >= 32 ? 0 : high[Math.Clamp(frame >> 1, 0, 15)];
    }
}
