using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.World;

public sealed record FieldRockSmashSourceRef(string Path);
public sealed record FieldRockSmashSourceActor(MapId Map, string SourceMapSymbol, int SourceLocalId,
    int X, int Y, int Elevation, string GraphicsId, string ScriptId, string MovementType,
    int MovementRangeX, int MovementRangeY, string HideFlag, FieldRockSmashSourceRef Source);
public sealed record FieldRockSmashSourceDocument(string SourceCommit,
    ImmutableArray<FieldRockSmashSourceActor> Rocks, ImmutableArray<RockSmashEncounterTable> EncounterTables,
    RockSmashWildSourceDocument Wild);
public sealed record RockSmashWildSpeciesSource(SpeciesId Species, int SourceSpeciesId, string Ability1, string Ability2,
    string ItemCommon, string ItemRare, string LearnsetSymbol, FieldRockSmashSourceRef Source, FieldRockSmashSourceRef LearnsetSource);
public sealed record RockSmashInitialMoves(SpeciesId Species, byte Level, ImmutableArray<MoveId> Moves);
public sealed record RockSmashWildSourceDocument(ImmutableArray<RockSmashWildSpeciesSource> Species,
    ImmutableArray<RockSmashInitialMoves> InitialMoves);

/// <summary>The live actor projected into Domain. Actor holds live coordinates; templates stay source-bound.</summary>
public sealed record FieldRockSmashActorState(FieldActorDefinition Actor, int TemplateX, int TemplateY,
    bool Active, bool Invisible);

/// <summary>Original field predicates. HP, status, move PP and HM possession do not gate field use.</summary>
public static class FieldRockSmashSourceRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string Move = "pret:MOVE_ROCK_SMASH", Badge = "FLAG_BADGE06_GET";
    public const string Graphics = "OBJ_EVENT_GFX_ROCK_SMASH_ROCK", InteractionScript = "EventScript_RockSmash";
    public const string PartyScript = "EventScript_FldEffRockSmash", RemovalScript = "EventScript_UseRockSmash";
    public const string UsageStat = "GAME_STAT_USED_ROCK_SMASH", EffectSound = "SE_M_ROCK_THROW";
    public const int NoPartyMember = 6, FieldUsePpCost = 0;
    public const int RockAnimationFrames = 4, RockFrameDuration = 8, RockBlinkFrames = 32;
    public const int RockBreakCueFrames = RockAnimationFrames * RockFrameDuration + RockBlinkFrames;

    public static bool Knows(PartyPokemon? member) => member is { IsEgg: false, Pokemon: not null } &&
        !member.Pokemon.Moves.IsDefault && member.Pokemon.Moves.Any(m => m is not null && m.Move.Value == Move);
    public static int FirstPartyMember(ImmutableArray<PartyPokemon> party)
    {
        if (party.IsDefault || party.Length > NoPartyMember) throw new ArgumentException("Invalid party.", nameof(party));
        for (int i = 0; i < party.Length; i++) if (Knows(party[i])) return i;
        return NoPartyMember;
    }
    public static bool CanUse(PartyPokemon? member, bool hasMarshBadge, bool hasFacingSourceRock) =>
        Knows(member) && hasMarshBadge && hasFacingSourceRock;
    public static bool ElevationMatches(int player, int actor)
    {
        if (player is < 0 or > 15 || actor is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(player));
        return player == 0 || actor == 0 || player == actor;
    }
}

/// <summary>All 97 original source rocks. Loaded maps still decide reachability; inclusion never enables a region.</summary>
public sealed class FieldRockSmashCatalog
{
    private static FieldRockSmashCatalog? _loadedSource;
    // Immutable pinned content, not game state. Shared battle validation requires the normal Content load first.
    internal static FieldRockSmashCatalog? LoadedSource => Volatile.Read(ref _loadedSource);
    public ImmutableArray<FieldRockSmashSourceActor> Rocks { get; }
    public ImmutableArray<RockSmashEncounterTable> EncounterTables { get; }
    public RockSmashWildSourceDocument Wild { get; }
    private FieldRockSmashCatalog(FieldRockSmashSourceDocument source)
    { Rocks = source.Rocks; EncounterTables = source.EncounterTables; Wild = source.Wild; }
    public static FieldRockSmashCatalog Authenticate(FieldRockSmashSourceDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SourceCommit != FieldRockSmashSourceRules.SourceCommit || source.Rocks.IsDefault ||
            source.Rocks.Length != 97 || source.EncounterTables.IsDefault || source.EncounterTables.Length != 14 ||
            source.Wild is null || source.Wild.Species.IsDefault || source.Wild.Species.Length != 4 ||
            source.Wild.InitialMoves.IsDefault || source.Wild.InitialMoves.Length != 139)
            throw new ArgumentException("field_rock_smash_source");
        var catalog = new FieldRockSmashCatalog(source);
        return Interlocked.CompareExchange(ref _loadedSource, catalog, null) ?? catalog;
    }
    public bool HasRockMap(MapId map) => Rocks.Any(rock => rock.Map == map);
    public RockSmashEncounterTable? EncountersFor(MapId map) => EncounterTables.SingleOrDefault(table => table.Map == map);

    public FieldRockSmashSourceActor? SourceFor(FieldMapDefinition map, FieldActorDefinition actor)
    {
        ArgumentNullException.ThrowIfNull(map); ArgumentNullException.ThrowIfNull(actor);
        if (actor.IsClone || actor.TrainerType != "TRAINER_TYPE_NONE" || actor.TrainerSight != 0 ||
            !map.Actors.Contains(actor)) return null;
        return Rocks.SingleOrDefault(source => source.Map == map.Id && source.SourceMapSymbol == map.SourceMapSymbol &&
            source.SourceLocalId == actor.SourceLocalId && source.GraphicsId == actor.GraphicsId && source.ScriptId == actor.ScriptId &&
            source.X == actor.X && source.Y == actor.Y && source.Elevation == actor.Elevation &&
            source.MovementType == actor.MovementType && source.HideFlag == actor.HideFlag);
    }

    /// <summary>Authenticate category and original row, then test live activity, source spot and source elevation matching.</summary>
    public FieldRockSmashSourceActor? FindFacingRock(FieldMapDefinition map, TilePosition player,
        ImmutableArray<FieldRockSmashActorState> actors, IReadOnlyDictionary<string, bool> flags)
    {
        ArgumentNullException.ThrowIfNull(map); ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(flags);
        if (actors.IsDefault || player.MapId != map.Id || !Enum.IsDefined(player.Facing)) throw new ArgumentException("field_rock_smash_context");
        var (dx, dy) = player.Facing switch { Direction.North => (0, -1), Direction.East => (1, 0),
            Direction.South => (0, 1), Direction.West => (-1, 0), _ => throw new ArgumentOutOfRangeException(nameof(player)) };
        FieldRockSmashSourceActor? found = null;
        foreach (var live in actors)
        {
            if (live is null || live.Actor is null) throw new ArgumentException("field_rock_smash_actor");
            var actor = live.Actor;
            if (!live.Active || live.Invisible || actor.X != player.X + dx || actor.Y != player.Y + dy ||
                live.TemplateX != actor.X || live.TemplateY != actor.Y ||
                !FieldRockSmashSourceRules.ElevationMatches(player.Elevation, actor.Elevation) ||
                flags.GetValueOrDefault(actor.HideFlag) || SourceFor(map, actor) is not { } source) continue;
            if (found is not null) throw new ArgumentException("field_rock_smash_ambiguous_actor");
            found = source;
        }
        return found;
    }
}
