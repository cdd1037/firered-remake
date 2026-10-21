using Game.Domain.WildBattle;

/// <summary>Source presentation selectors only. These do not advance battle, script, RNG or healing state.</summary>
internal static class PokemonTowerPresentationModel
{
    internal enum RevealPhase { NotApplicable, BeforeReveal, AfterReveal }
    internal sealed record OpponentAppearance(string Name, int? SpeciesSprite, bool GhostSprite,
        bool ShowGender, bool ShowCaughtMarker, bool ShowLevel, bool ShowHp, int? FixedX, int? FixedY);
    internal const string GhostTexture = "content/generated/maps/regions/pokemon-tower/presentation/ghost-front.png";
    internal const string TerrainTexture = "content/generated/ui/pokemon-tower-presentation/indoor1-scene.png";
    internal const string FieldMusic = "MUS_POKE_TOWER";
    internal const string FluteFanfare = "MUS_POKE_FLUTE";
    internal const int FluteFanfareSourceId = 338;
    internal const int FluteFanfareCounter = 450;

    internal static OpponentAppearance Opponent(PokemonTowerBattleKind kind, RevealPhase phase, int speciesId, string speciesName, string ghostName)
    {
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (speciesId is < 1 or > 411 || string.IsNullOrWhiteSpace(speciesName)) throw new ArgumentException("Missing real opponent identity.");
        bool scripted = kind is PokemonTowerBattleKind.DisguisedMarowak or PokemonTowerBattleKind.RevealedMarowak;
        if (scripted && speciesId != 105) throw new ArgumentException("Tower scripted opponent must remain MAROWAK.");
        if (kind == PokemonTowerBattleKind.RevealedMarowak ? phase == RevealPhase.NotApplicable : phase != RevealPhase.NotApplicable)
            throw new ArgumentException("An explicit reveal phase is required only for scoped scripted Marowak.");
        bool ghost = kind is PokemonTowerBattleKind.DisguisedWild or PokemonTowerBattleKind.DisguisedMarowak ||
            kind == PokemonTowerBattleKind.RevealedMarowak && phase == RevealPhase.BeforeReveal;
        return ghost ? new(ghostName, null, true, false, false, true, true, 144, 8)
            : new(speciesName, speciesId, false, true, true, true, true, scripted ? 144 : null, scripted ? 21 : null);
    }

    internal static string? IntroTextSymbol(PokemonTowerBattleKind kind) => kind switch
    {
        PokemonTowerBattleKind.OrdinaryWild => null,
        PokemonTowerBattleKind.DisguisedWild or PokemonTowerBattleKind.DisguisedMarowak => "sText_GhostAppearedCantId",
        PokemonTowerBattleKind.RevealedMarowak => "sText_TheGhostAppeared",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    internal static bool UsesTowerTerrain(string mapId) => mapId is
        "map.pokemon-tower-1f" or "map.pokemon-tower-2f" or "map.pokemon-tower-3f" or "map.pokemon-tower-4f" or
        "map.pokemon-tower-5f" or "map.pokemon-tower-6f" or "map.pokemon-tower-7f";
    internal static byte BlendWhiteChannel(byte value, int coefficient)
    {
        if (coefficient is < 0 or > 16) throw new ArgumentOutOfRangeException(nameof(coefficient));
        int channel = value >> 3; channel += ((31 - channel) * coefficient) >> 4;
        return (byte)((channel << 3) | (channel >> 2));
    }
}
