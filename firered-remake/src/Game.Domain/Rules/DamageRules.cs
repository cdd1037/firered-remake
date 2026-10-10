using System.Collections.Immutable;

namespace Game.Domain.Rules;

[Flags]
public enum UnsupportedBattleMechanics
{
    None = 0, Abilities = 1, Badges = 2, Weather = 4, HeldItems = 8,
    Status = 16, StatStages = 32, Screens = 64, MultiTarget = 128, SpecialMoveEffect = 256
}
public sealed record DamageInput(
    int Level, int Power, ElementType MoveType, int Attack, int Defense,
    ElementType AttackerType1, ElementType? AttackerType2,
    ElementType DefenderType1, ElementType? DefenderType2,
    bool Critical, int RandomPercent,
    UnsupportedBattleMechanics ActiveMechanics = UnsupportedBattleMechanics.None);
public sealed record DamageStep(string Operation, int Value);
public sealed record DamageResult(int Damage, DamageCategory Category, ImmutableArray<DamageStep> Steps);

/// <summary>Ordinary one-hit damage only. Attack/Defense inputs are the selected physical or special stats.</summary>
public static class DamageRules
{
    public static DamageResult Calculate(DamageInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ActiveMechanics != UnsupportedBattleMechanics.None)
            throw new NotSupportedException($"Unsupported battle mechanics: {input.ActiveMechanics}.");
        if (input.Level is < 1 or > 100 || input.Power is < 1 or > 255 ||
            input.Attack is < 1 or > 999 || input.Defense is < 1 or > 999 || input.RandomPercent is < 85 or > 100)
            throw new ArgumentOutOfRangeException(nameof(input), "Ordinary-damage inputs outside supported ranges.");
        if (!Enum.IsDefined(input.AttackerType1) || (input.AttackerType2.HasValue && !Enum.IsDefined(input.AttackerType2.Value)))
            throw new ArgumentOutOfRangeException(nameof(input), "Invalid attacker type.");
        var category = TypeRules.Category(input.MoveType);
        if (category == DamageCategory.Unsupported) throw new NotSupportedException("Mystery-type damage is not enabled in this profile.");
        var effects = TypeRules.OrderedEffects(input.MoveType, input.DefenderType1, input.DefenderType2);
        var steps = ImmutableArray.CreateBuilder<DamageStep>();
        int value = input.Attack * input.Power;
        steps.Add(new("attack-times-power", value));
        value *= 2 * input.Level / 5 + 2;
        steps.Add(new("times-level-factor", value));
        value /= input.Defense; steps.Add(new("divide-defense", value));
        value /= 50; steps.Add(new("divide-50", value));
        if (category == DamageCategory.Physical) value = Math.Max(1, value);
        steps.Add(new("physical-minimum-before-plus-two", value));
        value += 2; steps.Add(new("plus-two", value));
        if (input.Critical) value *= 2;
        steps.Add(new("critical", value));
        if (input.MoveType == input.AttackerType1 || input.MoveType == input.AttackerType2) value = value * 15 / 10;
        steps.Add(new("stab", value));
        bool immune = effects.Any(x => x.MultiplierTenths == 0);
        foreach (var effect in effects)
        {
            value = value * effect.MultiplierTenths / 10;
            if (value == 0 && effect.MultiplierTenths != 0) value = 1;
            steps.Add(new($"type-{effect.Defense}", value));
        }
        // An immunity flags the action ineffective even if a later source-table modifier raised the internal value.
        if (immune) value = 0;
        value = value == 0 ? 0 : Math.Max(1, value * input.RandomPercent / 100);
        steps.Add(new("random-percent", value));
        return new(value, category, steps.ToImmutable());
    }
}
