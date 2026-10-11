namespace Game.Domain.Battle;

internal enum SourceImmunityCure { None, PrimaryStatus, Confusion, Infatuation }
internal readonly record struct SourceImmunityCommand(SourceImmunityCure Cure, string Reason);

/// <summary>ABILITYEFFECT_IMMUNITY's pure source selection. The caller scans numeric positions,
/// without filtering HP or source absence. Storage adapters retain their event/projection contract.</summary>
internal static class SourceImmunityRules
{
    internal static SourceImmunityCommand Resolve(string ability, uint status, int confusion,
        bool infatuated, bool expanded = true, bool poison = true) => ability switch
    {
        "ABILITY_IMMUNITY" when poison && (status & 0xF88u) != 0 => new(SourceImmunityCure.PrimaryStatus, "immunity"),
        "ABILITY_LIMBER" when (status & 0x40u) != 0 => new(SourceImmunityCure.PrimaryStatus, "limber"),
        "ABILITY_OWN_TEMPO" when expanded && confusion > 0 => new(SourceImmunityCure.Confusion, "own-tempo"),
        "ABILITY_OBLIVIOUS" when expanded && infatuated => new(SourceImmunityCure.Infatuation, "oblivious"),
        "ABILITY_INSOMNIA" or "ABILITY_VITAL_SPIRIT" when expanded && (status & 7u) != 0 => new(SourceImmunityCure.PrimaryStatus, "transform-immunity"),
        "ABILITY_WATER_VEIL" when expanded && (status & 0x10u) != 0 => new(SourceImmunityCure.PrimaryStatus, "transform-immunity"),
        "ABILITY_MAGMA_ARMOR" when expanded && (status & 0x20u) != 0 => new(SourceImmunityCure.PrimaryStatus, "transform-immunity"),
        _ => default
    };
}
