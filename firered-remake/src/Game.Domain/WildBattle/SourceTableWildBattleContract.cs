using Game.Domain.Encounters;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.WildBattle;

public enum SourceTableEncounterKind { Land, Surf, OldRod }
public sealed record SourceTableWildOrigin(MapId Map, SourceTableEncounterKind Kind, int Slot)
{
    public string RulesVersion { get; init; } = SourceTableWildBattleContract.Version;
}
public sealed record SourceTableWildSlot(SpeciesId Species, byte MinimumLevel, byte MaximumLevel);

/// <summary>Ordinary exact-table provenance without regional ownership inheritance. Admits only the exact registered source table sets.</summary>
public static class SourceTableWildBattleContract
{
    public const string Version = "firered-source-table-wild-entry-v1";

    public static SourceTableWildSlot? SourceSlot(SourceTableWildOrigin? origin)
    {
        if (origin is null || origin.RulesVersion != Version || origin.Slot < 0)
            return null;
        if (origin.Kind == SourceTableEncounterKind.Land)
        {
            var table = SourceTableEncounterRegistry.Land
                .SingleOrDefault(t => t.Map == origin.Map);
            var slot = table?.Slots.SingleOrDefault(s => s.Index == origin.Slot);
            return slot is null ? null : new(slot.Species, slot.MinimumLevel, slot.MaximumLevel);
        }
        if (origin.Kind == SourceTableEncounterKind.Surf)
        {
            var table = SourceTableEncounterRegistry.Water.SingleOrDefault(t => t.Map == origin.Map);
            var slot = table?.Slots.SingleOrDefault(s => s.Index == origin.Slot);
            return slot is null ? null : new(slot.Species, slot.MinimumLevel, slot.MaximumLevel);
        }
        if (origin.Kind == SourceTableEncounterKind.OldRod)
        {
            var table = SourceTableEncounterRegistry.OldRod.SingleOrDefault(t => t.Map == origin.Map);
            var slot = table?.Slots.SingleOrDefault(s => s.Index == origin.Slot);
            return slot is null ? null : new(slot.Species, slot.MinimumLevel, slot.MaximumLevel);
        }
        return null;
    }

    public static bool IsAdmittedProfile(string? profile) =>
        profile == GamePokemonCatalog.Profile;

    public static bool Matches(SourceTableWildOrigin? origin, PokemonInstance? pokemon) => pokemon is not null &&
        SourceSlot(origin) is { } slot && pokemon.Species == slot.Species &&
        pokemon.Level >= slot.MinimumLevel && pokemon.Level <= slot.MaximumLevel;

    public static Route1WildPokemonResult Generate(GamePokemonCatalog owned, SourceTableWildOrigin origin,
        byte level, RngState rng, uint trainerId)
    {
        if (!ValidOwned(owned) || SourceSlot(origin) is not { } slot || !owned.Catalog.Species.ContainsKey(slot.Species) ||
            level < slot.MinimumLevel || level > slot.MaximumLevel || rng.AlgorithmId != Gen3Rng.AlgorithmId)
            throw new ArgumentException("source_table_wild_generation");
        var made = Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, slot.Species, level, rng, trainerId);
        if (!SourceInitialInstance(owned, origin, made.Pokemon)) throw new InvalidOperationException("source_table_wild_identity");
        return made;
    }

    public static bool SourceInitialInstance(GamePokemonCatalog? owned, SourceTableWildOrigin? origin,
        PokemonInstance? pokemon)
    {
        if (!ValidOwned(owned) || pokemon is null || !owned!.Catalog.Species.ContainsKey(pokemon.Species) ||
            !Matches(origin, pokemon) || SourcePokemonValidator.ValidatePermanent(owned.Mechanics, pokemon) is not null)
            return false;
        var source = owned.Mechanics.Catalog.Species[pokemon.Species];
        return pokemon.NpcTradeOrigin is null && pokemon.ContestConditions is null && pokemon.StatCalculationEvs is null &&
            pokemon.Evs == SixStats.Zero && pokemon.CurrentHp == pokemon.Stats.MaxHp && pokemon.Friendship == source.Friendship &&
            pokemon.Experience == GrowthRules.ExperienceAtLevel(source.GrowthRate, pokemon.Level) &&
            pokemon.Moves.SequenceEqual(PokemonMoveRules.InitialMovesCore(owned.Mechanics.Catalog, source, pokemon.Level));
    }

    /// <summary>Cape's rare items are Fearow's SHARP_BEAK and Meowth's NUGGET. Caller consumes the separate draw.</summary>
    public static string HeldItem(SourceTableWildOrigin origin, int roll)
    {
        if (SourceSlot(origin) is not { } slot || roll is < 0 or >= 100) throw new ArgumentException("source_table_held_origin");
        return roll < 95 ? "ITEM_NONE" : slot.Species.Value switch
        {
            "pret:SPECIES_FEAROW" => "ITEM_SHARP_BEAK",
            "pret:SPECIES_MEOWTH" => "ITEM_NUGGET",
            _ => "ITEM_NONE"
        };
    }

    private static bool ValidOwned(GamePokemonCatalog? owned) => owned is not null &&
        owned is not null && IsAdmittedProfile(owned.RulesVersion);
}
