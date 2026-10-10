using System.Collections.Immutable;

namespace Game.Domain.Rules;

// Numeric values retain the pinned source's type identifiers. This is Gen III: no Fairy, Dark is special.
public enum ElementType
{
    Normal, Fighting, Flying, Poison, Ground, Rock, Bug, Ghost, Steel, Mystery,
    Fire, Water, Grass, Electric, Psychic, Ice, Dragon, Dark
}
public enum DamageCategory { Physical, Special, Unsupported }
public sealed record TypeEffect(ElementType Attack, ElementType Defense, int MultiplierTenths);

public static class TypeRules
{
    public static DamageCategory Category(ElementType type) => type switch
    {
        >= ElementType.Normal and <= ElementType.Steel => DamageCategory.Physical,
        >= ElementType.Fire and <= ElementType.Dark => DamageCategory.Special,
        ElementType.Mystery => DamageCategory.Unsupported,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static int EffectivenessTenths(ElementType attack, ElementType defense)
    {
        Validate(attack); Validate(defense);
        return Chart.FirstOrDefault(x => x.Attack == attack && x.Defense == defense)?.MultiplierTenths ?? 10;
    }

    // Source-table order matters for integer rounding; never multiply a combined dual-type fraction once.
    public static ImmutableArray<TypeEffect> OrderedEffects(ElementType attack, ElementType defense1, ElementType? defense2 = null)
    {
        Validate(attack); Validate(defense1);
        if (defense2 is { } second) Validate(second);
        return Chart.Where(x => x.Attack == attack && (x.Defense == defense1 || x.Defense == defense2)).ToImmutableArray();
    }

    private static void Validate(ElementType type)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
    }

    // Facts transcribed from pinned src/battle_main.c:gTypeEffectiveness, retaining entry order.
    private static readonly ImmutableArray<TypeEffect> Chart =
    [
        new(ElementType.Normal, ElementType.Rock, 5),
        new(ElementType.Normal, ElementType.Steel, 5),
        new(ElementType.Fire, ElementType.Fire, 5),
        new(ElementType.Fire, ElementType.Water, 5),
        new(ElementType.Fire, ElementType.Grass, 20),
        new(ElementType.Fire, ElementType.Ice, 20),
        new(ElementType.Fire, ElementType.Bug, 20),
        new(ElementType.Fire, ElementType.Rock, 5),
        new(ElementType.Fire, ElementType.Dragon, 5),
        new(ElementType.Fire, ElementType.Steel, 20),
        new(ElementType.Water, ElementType.Fire, 20),
        new(ElementType.Water, ElementType.Water, 5),
        new(ElementType.Water, ElementType.Grass, 5),
        new(ElementType.Water, ElementType.Ground, 20),
        new(ElementType.Water, ElementType.Rock, 20),
        new(ElementType.Water, ElementType.Dragon, 5),
        new(ElementType.Electric, ElementType.Water, 20),
        new(ElementType.Electric, ElementType.Electric, 5),
        new(ElementType.Electric, ElementType.Grass, 5),
        new(ElementType.Electric, ElementType.Ground, 0),
        new(ElementType.Electric, ElementType.Flying, 20),
        new(ElementType.Electric, ElementType.Dragon, 5),
        new(ElementType.Grass, ElementType.Fire, 5),
        new(ElementType.Grass, ElementType.Water, 20),
        new(ElementType.Grass, ElementType.Grass, 5),
        new(ElementType.Grass, ElementType.Poison, 5),
        new(ElementType.Grass, ElementType.Ground, 20),
        new(ElementType.Grass, ElementType.Flying, 5),
        new(ElementType.Grass, ElementType.Bug, 5),
        new(ElementType.Grass, ElementType.Rock, 20),
        new(ElementType.Grass, ElementType.Dragon, 5),
        new(ElementType.Grass, ElementType.Steel, 5),
        new(ElementType.Ice, ElementType.Water, 5),
        new(ElementType.Ice, ElementType.Grass, 20),
        new(ElementType.Ice, ElementType.Ice, 5),
        new(ElementType.Ice, ElementType.Ground, 20),
        new(ElementType.Ice, ElementType.Flying, 20),
        new(ElementType.Ice, ElementType.Dragon, 20),
        new(ElementType.Ice, ElementType.Steel, 5),
        new(ElementType.Ice, ElementType.Fire, 5),
        new(ElementType.Fighting, ElementType.Normal, 20),
        new(ElementType.Fighting, ElementType.Ice, 20),
        new(ElementType.Fighting, ElementType.Poison, 5),
        new(ElementType.Fighting, ElementType.Flying, 5),
        new(ElementType.Fighting, ElementType.Psychic, 5),
        new(ElementType.Fighting, ElementType.Bug, 5),
        new(ElementType.Fighting, ElementType.Rock, 20),
        new(ElementType.Fighting, ElementType.Dark, 20),
        new(ElementType.Fighting, ElementType.Steel, 20),
        new(ElementType.Poison, ElementType.Grass, 20),
        new(ElementType.Poison, ElementType.Poison, 5),
        new(ElementType.Poison, ElementType.Ground, 5),
        new(ElementType.Poison, ElementType.Rock, 5),
        new(ElementType.Poison, ElementType.Ghost, 5),
        new(ElementType.Poison, ElementType.Steel, 0),
        new(ElementType.Ground, ElementType.Fire, 20),
        new(ElementType.Ground, ElementType.Electric, 20),
        new(ElementType.Ground, ElementType.Grass, 5),
        new(ElementType.Ground, ElementType.Poison, 20),
        new(ElementType.Ground, ElementType.Flying, 0),
        new(ElementType.Ground, ElementType.Bug, 5),
        new(ElementType.Ground, ElementType.Rock, 20),
        new(ElementType.Ground, ElementType.Steel, 20),
        new(ElementType.Flying, ElementType.Electric, 5),
        new(ElementType.Flying, ElementType.Grass, 20),
        new(ElementType.Flying, ElementType.Fighting, 20),
        new(ElementType.Flying, ElementType.Bug, 20),
        new(ElementType.Flying, ElementType.Rock, 5),
        new(ElementType.Flying, ElementType.Steel, 5),
        new(ElementType.Psychic, ElementType.Fighting, 20),
        new(ElementType.Psychic, ElementType.Poison, 20),
        new(ElementType.Psychic, ElementType.Psychic, 5),
        new(ElementType.Psychic, ElementType.Dark, 0),
        new(ElementType.Psychic, ElementType.Steel, 5),
        new(ElementType.Bug, ElementType.Fire, 5),
        new(ElementType.Bug, ElementType.Grass, 20),
        new(ElementType.Bug, ElementType.Fighting, 5),
        new(ElementType.Bug, ElementType.Poison, 5),
        new(ElementType.Bug, ElementType.Flying, 5),
        new(ElementType.Bug, ElementType.Psychic, 20),
        new(ElementType.Bug, ElementType.Ghost, 5),
        new(ElementType.Bug, ElementType.Dark, 20),
        new(ElementType.Bug, ElementType.Steel, 5),
        new(ElementType.Rock, ElementType.Fire, 20),
        new(ElementType.Rock, ElementType.Ice, 20),
        new(ElementType.Rock, ElementType.Fighting, 5),
        new(ElementType.Rock, ElementType.Ground, 5),
        new(ElementType.Rock, ElementType.Flying, 20),
        new(ElementType.Rock, ElementType.Bug, 20),
        new(ElementType.Rock, ElementType.Steel, 5),
        new(ElementType.Ghost, ElementType.Normal, 0),
        new(ElementType.Ghost, ElementType.Psychic, 20),
        new(ElementType.Ghost, ElementType.Dark, 5),
        new(ElementType.Ghost, ElementType.Steel, 5),
        new(ElementType.Ghost, ElementType.Ghost, 20),
        new(ElementType.Dragon, ElementType.Dragon, 20),
        new(ElementType.Dragon, ElementType.Steel, 5),
        new(ElementType.Dark, ElementType.Fighting, 5),
        new(ElementType.Dark, ElementType.Psychic, 20),
        new(ElementType.Dark, ElementType.Ghost, 20),
        new(ElementType.Dark, ElementType.Dark, 5),
        new(ElementType.Dark, ElementType.Steel, 5),
        new(ElementType.Steel, ElementType.Fire, 5),
        new(ElementType.Steel, ElementType.Water, 5),
        new(ElementType.Steel, ElementType.Electric, 5),
        new(ElementType.Steel, ElementType.Ice, 20),
        new(ElementType.Steel, ElementType.Rock, 20),
        new(ElementType.Steel, ElementType.Steel, 5),
        new(ElementType.Normal, ElementType.Ghost, 0),
        new(ElementType.Fighting, ElementType.Ghost, 0),
    ];
}
