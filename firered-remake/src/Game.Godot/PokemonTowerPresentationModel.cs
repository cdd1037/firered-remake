using Game.Domain.WildBattle;

/// <summary>Source presentation selectors only. These do not advance battle, script, RNG or healing state.</summary>
internal static class PokemonTowerPresentationModel
{
    internal enum RevealPhase { NotApplicable, BeforeReveal, AfterReveal }
    internal sealed record OpponentAppearance(string Name, int? SpeciesSprite, bool GhostSprite,
        bool ShowGender, bool ShowCaughtMarker, bool ShowLevel, bool ShowHp, int? FixedX, int? FixedY);
    internal readonly record struct FogSprite(int X, int Y);
    internal readonly record struct FogBlend(int Eva, int Evb);
    internal const string GhostName = "GHOST";
    internal const string GhostTexture = "content/generated/pokemon-tower-region/presentation/ghost-front.png";
    internal const string TerrainTexture = "content/generated/pokemon-tower-presentation/indoor1-scene.png";
    internal const string FogTexture = "content/generated/pokemon-tower-presentation/fog-horizontal.png";
    internal const string FieldMusic = "MUS_POKE_TOWER";
    internal const string FluteFanfare = "MUS_POKE_FLUTE";
    internal const int FluteFanfareSourceId = 338;
    internal const int FluteFanfareCounter = 450;

    internal static OpponentAppearance Opponent(PokemonTowerBattleKind kind, RevealPhase phase, int speciesId, string speciesName)
    {
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (speciesId is < 1 or > 411 || string.IsNullOrWhiteSpace(speciesName)) throw new ArgumentException("Missing real opponent identity.");
        bool scripted = kind is PokemonTowerBattleKind.DisguisedMarowak or PokemonTowerBattleKind.RevealedMarowak;
        if (scripted && (speciesId != 105 || speciesName != "MAROWAK")) throw new ArgumentException("Tower scripted opponent must remain MAROWAK.");
        if (kind == PokemonTowerBattleKind.RevealedMarowak ? phase == RevealPhase.NotApplicable : phase != RevealPhase.NotApplicable)
            throw new ArgumentException("An explicit reveal phase is required only for scoped scripted Marowak.");
        bool ghost = kind is PokemonTowerBattleKind.DisguisedWild or PokemonTowerBattleKind.DisguisedMarowak ||
            kind == PokemonTowerBattleKind.RevealedMarowak && phase == RevealPhase.BeforeReveal;
        return ghost ? new(GhostName, null, true, false, false, true, true, 144, 8)
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
    // Scope is deliberately not an input: the source weather belongs to the floor.
    internal static bool HasFog(string mapId) => mapId is "map.pokemon-tower-3f" or "map.pokemon-tower-4f" or
        "map.pokemon-tower-5f" or "map.pokemon-tower-6f" or "map.pokemon-tower-7f";

    /// <summary>Number of FogHorizontal_Main calls since fresh creation; call 1 creates sprites.
    /// The source calculates X before incrementing its four-call scroll counter.</summary>
    internal static FogSprite[] FogSprites(long sourceCalls, int spriteCoordOffsetX, int spriteCoordOffsetY)
    {
        if (sourceCalls < 0) throw new ArgumentOutOfRangeException(nameof(sourceCalls));
        if (sourceCalls == 0) return [];
        long scroll = (sourceCalls - 1) / 4;
        int position = (int)((spriteCoordOffsetX - scroll) & 255);
        var sprites = new FogSprite[20];
        for (int i = 0; i < sprites.Length; i++)
        {
            int column = i % 5, centerX = position + 32 + column * 64;
            if (centerX >= 272) centerX = (480 + position - (4 - column) * 64) & 511;
            int topX = (centerX - 32) & 511;
            if (topX >= 240) topX -= 512;
            // Source OBJ Y is eight-bit wrapped; 160..255 wraps into the visible top when necessary.
            int topY = (i / 5 * 64 + spriteCoordOffsetY) & 255;
            if (topY >= 160) topY -= 256;
            sprites[i] = new(topX, topY);
        }
        return sprites;
    }

    internal static FogBlend FogEntryBlend(long sourceCalls)
    {
        if (sourceCalls < 0) throw new ArgumentOutOfRangeException(nameof(sourceCalls));
        // Creation is call 1. Subsequent Weather_UpdateBlend calls alternate EVA/EVB each fourth call.
        long changes = Math.Max(0, sourceCalls - 1) / 4;
        return new((int)Math.Min(12, (changes + 1) / 2), 16 - (int)Math.Min(8, changes / 2));
    }
    internal static FogBlend FogExitBlend(long finishCalls, FogBlend start)
    {
        if (finishCalls < 0 || start.Eva is < 0 or > 16 || start.Evb is < 0 or > 16) throw new ArgumentOutOfRangeException(nameof(finishCalls));
        long changes = Math.Max(0, finishCalls - 1) / 4;
        return new(Math.Max(0, start.Eva - (int)Math.Min(16, (changes + 1) / 2)), Math.Min(16, start.Evb + (int)Math.Min(16, changes / 2)));
    }

    /// <summary>GBA's two independent coefficients, not ordinary alpha compositing. This is a pixel oracle;
    /// live blend targets, palette timing, sprite priorities and original hardware comparison are separate gates.</summary>
    internal static byte BlendFogChannel(byte fog, byte background, FogBlend coefficients)
    {
        if (coefficients.Eva is < 0 or > 16 || coefficients.Evb is < 0 or > 16) throw new ArgumentOutOfRangeException(nameof(coefficients));
        int value = Math.Min(31, ((fog >> 3) * coefficients.Eva + (background >> 3) * coefficients.Evb) >> 4);
        return (byte)((value << 3) | (value >> 2));
    }
    internal static byte BlendWhiteChannel(byte value, int coefficient)
    {
        if (coefficient is < 0 or > 16) throw new ArgumentOutOfRangeException(nameof(coefficient));
        int channel = value >> 3; channel += ((31 - channel) * coefficient) >> 4;
        return (byte)((channel << 3) | (channel >> 2));
    }
}
