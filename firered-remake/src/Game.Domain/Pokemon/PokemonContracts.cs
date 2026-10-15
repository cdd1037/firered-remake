using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Rules;

namespace Game.Domain.Pokemon;

/// <summary>Source order: HP, Attack, Defense, Speed, Special Attack, Special Defense.</summary>
public sealed record SixStats(int Hp, int Attack, int Defense, int Speed, int SpecialAttack, int SpecialDefense)
{
    public static SixStats Zero { get; } = new(0, 0, 0, 0, 0, 0);
    public ImmutableArray<int> Values => [Hp, Attack, Defense, Speed, SpecialAttack, SpecialDefense];
}
public sealed record PokemonSourceRef(string Path, string Symbol, int StartLine, int EndLine);
public sealed record PokemonLevelUpMove(int Level, MoveId Move);
public enum PokemonEvolutionMethod { Level = 0, Item = 1, Trade = 2, TradeItem = 3, Friendship = 4, LevelAttackGreaterDefense = 5, LevelAttackEqualDefense = 6, LevelAttackLessDefense = 7 }
public sealed record PokemonEvolution(int Level, SpeciesId Target, PokemonSourceRef Source)
{
    // Missing fields retain the exact legacy level-evolution serialization.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public PokemonEvolutionMethod Method { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Parameter { get; init; }
}
public sealed record PokemonSpeciesDefinition(SpeciesId Id, int SourceSpeciesId, SixStats BaseStats,
    string Type1, string Type2, int CatchRate, int ExperienceYield, SixStats EvYield,
    GrowthRate GrowthRate, int GenderRatio, string Ability1, string Ability2, int Friendship,
    ImmutableArray<PokemonLevelUpMove> LevelUpMoves, ImmutableArray<PokemonEvolution> Evolutions,
    PokemonSourceRef Source, PokemonSourceRef LearnsetSource);

/// <summary>Reference move data only. BattleEffectImplemented is deliberately false for this profile.</summary>
public sealed record PokemonMoveDefinition(MoveId Id, int SourceMoveId, int Pp, int Power, string Type,
    int Accuracy, string Effect, int SecondaryEffectChance, string Target, int Priority,
    ImmutableArray<string> Flags, bool BattleEffectImplemented, PokemonSourceRef Source);
public sealed record PokemonCatalog(string SourceCommit,
    ImmutableDictionary<SpeciesId, PokemonSpeciesDefinition> Species,
    ImmutableDictionary<MoveId, PokemonMoveDefinition> Moves);

public enum IvMode { Random, Fixed }
public sealed record IvPolicy(IvMode Mode, int FixedValue = 0)
{
    public static IvPolicy Random { get; } = new(IvMode.Random);
    public static IvPolicy Fixed(int value) => new(IvMode.Fixed, value);
}
public enum PersonalityMode { Random, Fixed }
public sealed record PersonalityPolicy(PersonalityMode Mode, uint FixedValue = 0)
{
    public static PersonalityPolicy Random { get; } = new(PersonalityMode.Random);
    public static PersonalityPolicy Fixed(uint value) => new(PersonalityMode.Fixed, value);
}
public sealed record CreatePokemonRequest(SpeciesId Species, int Level, uint OriginalTrainerId,
    IvPolicy Ivs, PersonalityPolicy Personality);
public enum PokemonGender { Male = 0, Female = 254, Genderless = 255 }
public sealed record PokemonMoveSlot(MoveId Move, int CurrentPp, int MaximumPp, int PpUps = 0);

/// <summary>Permanent Pokemon data. Egg stat caches are explicitly marked. Ownership uses the full source 32-bit OT ID.</summary>
public sealed record PokemonInstance(SpeciesId Species, int SourceSpeciesId, int Level, uint Experience,
    uint OriginalTrainerId, uint Personality, Nature Nature, SixStats Ivs, SixStats Evs,
    BattleStats Stats, int CurrentHp, PokemonGender Gender, int AbilitySlot, string AbilityId,
    int Friendship, ImmutableArray<PokemonMoveSlot> Moves)
{
    // EV awards do not recalculate source party stats until a later CalculateMonStats call.
    // Null preserves the existing representation: Stats were calculated from current Evs.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SixStats? StatCalculationEvs { get; init; }

    // CreateMon calculates party stats before InheritIVs writes the egg's final IVs.
    // Non-null is an egg-only marker; ordinary instance/battle validation rejects it.
    // BoxMonToMon recalculates stats and records the current IVs as the new basis.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SixStats? EggStatCalculationIvs { get; init; }

    // Serialized source identity, not an ownership receipt. The actual authenticated catalog
    // must resolve this descriptor before it can authorize a fixed in-game-trade ability bit.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NpcTradePokemonOrigin? NpcTradeOrigin { get; init; }

    // The source NPC-trade constructor sets these hidden fields even without contest gameplay.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PokemonContestConditions? ContestConditions { get; init; }

    // Source shiny test; does not imply shiny presentation has been implemented.
    public bool IsShiny => ((OriginalTrainerId >> 16) ^ (OriginalTrainerId & 0xffff) ^
        (Personality >> 16) ^ (Personality & 0xffff)) < 8;
}
public sealed record PokemonCreationResult(PokemonInstance Pokemon, RngState Rng, ImmutableArray<RandomTrace> Trace);
public enum MoveLearningOutcome { Added, AlreadyKnown, NeedsReplacement }
public sealed record MoveLearningResult(MoveLearningOutcome Outcome, ImmutableArray<PokemonMoveSlot> Moves);
