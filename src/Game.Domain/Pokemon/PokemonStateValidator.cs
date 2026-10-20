using System.Collections.Immutable;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Pokemon;

/// <summary>
/// Pure validation of persisted owned state. Ordinary instances stay strict; party eggs require
/// authenticated breeding metadata and their explicit initial-stat basis. Returns the first stable error code,
/// or null. This validates represented state, not whether a battle, TM, trade or item action exists.
/// Catalog authenticity and loading remain the composition/content layer's responsibility.
/// </summary>
public static class PokemonStateValidator
{
    // Each character is one byte in the pinned English source charmap. This is not a UTF-8 byte limit.
    // naming_screen.c:378-397; global.h:63-64; charmap.txt:67-145 (037335f4c725d7c9aecdac87066f2002b4bd7e14).
    // Authenticated original on-screen keyboard, not the live literal-name validator.
    public const string NicknameAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,!?/-♂♀…“”‘'";

    /// <summary>
    /// General instance invariants: injury/fainting, partial XP, EVs, changed friendship, depleted PP,
    /// PP Ups and any distinct imported moves are valid. Initial learnsets are NOT a legality test:
    /// a later move choice, TM or evolution can retain moves absent from this level's initial set.
    /// </summary>
    public static string? Validate(PokemonCatalog? catalog, PokemonInstance? pokemon)
    {
        var catalogError = ValidateCatalog(catalog);
        if (catalogError is not null) return catalogError;
        return ValidateInstance(catalog!, pokemon);
    }

    /// <summary>
    /// General ownership invariants in the current English-keyboard profile. Source language
    /// and game IDs, and the twelve Gen III balls, are recognized. Poison, burn and paralysis are
    /// represented; explicit content admits sleep counters1–5, freeze and canonical Toxic0x80.
    /// Toxic battle counters remain unsupported; injury/zero HP and authenticated source eggs are supported.
    /// Geographic met-location/held-item symbols get syntax validation; the real in-game-trade met
    /// sentinel requires an authenticated fixed origin and its exact immutable ownership metadata.
    /// Call ValidateLocalStarterGift for the exact locally implemented gift checkpoint instead.
    /// </summary>
    public static string? ValidateParty(CampaignPokemonContent? content, PartyPokemon? owned)
    {
        var contentError = ValidateContent(content);
        if (contentError is not null) return contentError;
        return ValidateOwned(content!.Catalog, owned, content.AllowSleepStatus, content.AllowFreezeStatus, content.AllowToxicStatus);
    }

    internal static string? ValidateOwnedForBattle(GamePokemonCatalog ownership, PartyPokemon? owned)
    {
        return ValidateOwned(ownership.Catalog, owned, true, true, true);
    }

    internal static string? ValidateOwned(PokemonCatalog catalog, PartyPokemon? owned,
        bool allowSleep, bool allowFreeze, bool allowToxic)
    {
        if (owned is null) return "party_pokemon_missing";
        if (owned.InstanceId <= 0) return "party_instance_id";
        if (owned.Markings is < 0 or > 15) return "party_markings";
        var instanceError = owned.IsEgg
            ? ValidateEggInstance(catalog, owned.Pokemon)
            : ValidateInstance(catalog, owned.Pokemon);
        if (instanceError is not null) return instanceError;
        if (owned.IsEgg ? owned.CustomNickname is not null : !PokemonNicknameRules.IsValidCustomNickname(owned.CustomNickname)) return "party_nickname";
        if (!ValidName(owned.OriginalTrainerName, 7)) return "party_original_trainer_name";
        if (owned.OriginalTrainerGender is not (0 or 1)) return "party_original_trainer_gender";
        // 6 is explicitly unused in the pinned source language list.
        if (owned.Language is not (1 or 2 or 3 or 4 or 5 or 7)) return "party_language";
        if (ValidateOriginMetadata(catalog, owned) is { } originError) return originError;
        // Zero describes both source eggs and previously hatched/traded creatures.
        if (owned.MetLevel < 0 || owned.MetLevel > owned.Pokemon.Level) return "party_met_level";
        // These are the named values in pinned include/constants/global.h. No unknown 4-bit IDs.
        if (owned.MetGame is not (1 or 2 or 3 or 4 or 5 or 7 or 8 or 10 or 11 or 12 or 15)) return "party_met_game";
        if (owned.Ball is not ("ITEM_MASTER_BALL" or "ITEM_ULTRA_BALL" or "ITEM_GREAT_BALL" or
            "ITEM_POKE_BALL" or "ITEM_SAFARI_BALL" or "ITEM_NET_BALL" or "ITEM_DIVE_BALL" or
            "ITEM_NEST_BALL" or "ITEM_REPEAT_BALL" or "ITEM_TIMER_BALL" or "ITEM_LUXURY_BALL" or "ITEM_PREMIER_BALL"))
            return "party_ball";
        if (!ValidSymbol(owned.HeldItem, "ITEM_")) return "party_held_item";
        if (SourceMailRules.ValidateAttachmentReference(owned) is {} mailError) return mailError;
        if (!SourcePartyStatusRules.IsSupported(owned.Status,allowSleep,allowFreeze,allowToxic)) return "party_status_unsupported";
        if (owned.IsEgg && (owned.Language != 1 || owned.MetLevel != 0 || owned.Ball != "ITEM_POKE_BALL" ||
            owned.HeldItem != "ITEM_NONE" || owned.Status != 0 || owned.Mail != 255 || owned.ChampionRibbon))
            return "party_egg_metadata";
        return null;
    }

    /// <summary>Party container invariants, including allocated entity IDs. An empty party is valid.</summary>
    public static string? ValidatePartyCollection(CampaignPokemonContent? content, ImmutableArray<PartyPokemon> party,
        long nextInstanceId)
    {
        var contentError = ValidateContent(content);
        if (contentError is not null) return contentError;
        if (party.IsDefault || party.Length > 6) return "party_count";
        if (nextInstanceId <= 0) return "party_next_instance_id";
        var ids = new HashSet<long>();
        foreach (var owned in party)
        {
            var error = ValidateParty(content, owned);
            if (error is not null) return error;
            if (!ids.Add(owned.InstanceId)) return "party_duplicate_instance_id";
            if (owned.InstanceId >= nextInstanceId) return "party_next_instance_id";
        }
        return null;
    }

    /// <summary>
    /// Exact current first-starter checkpoint, before battles/progression exist. This deliberately
    /// requires a healthy newborn and local OT/met data. Never substitute it for general validation
    /// once the campaign can alter HP, experience, EVs, friendship, moves, items or ownership.
    /// Nickname may already have been changed through the source keyboard.
    /// </summary>
    public static string? ValidateLocalStarterGift(CampaignPokemonContent? content, PartyPokemon? owned,
        uint expectedTrainerId, string expectedTrainerName, int expectedTrainerGender)
    {
        var error = ValidateParty(content, owned);
        if (error is not null) return error;
        if (!ValidName(expectedTrainerName, 7)) return "gift_expected_trainer_name";
        if (expectedTrainerGender is not (0 or 1)) return "gift_expected_trainer_gender";
        var p = owned!.Pokemon;
        if (p.SourceSpeciesId is not (1 or 4 or 7)) return "gift_species";
        if (p.Level != 5 || p.Experience != 135) return "gift_level_experience";
        if (p.OriginalTrainerId != expectedTrainerId || owned.OriginalTrainerName != expectedTrainerName ||
            owned.OriginalTrainerGender != expectedTrainerGender) return "gift_original_trainer";
        if (owned.Language != 2) return "gift_language";
        if (owned.MetLocation != "MAPSEC_PALLET_TOWN" || owned.MetLevel != 5 || owned.MetGame != 4)
            return "gift_met_data";
        if (owned.Ball != "ITEM_POKE_BALL" || owned.HeldItem != "ITEM_NONE") return "gift_item_data";
        if (p.Evs != SixStats.Zero) return "gift_evs";
        if (p.CurrentHp != p.Stats.MaxHp) return "gift_hp";
        if (owned.Status != 0) return "gift_status";
        var species = content!.Catalog.Species[p.Species];
        if (p.Friendship != species.Friendship) return "gift_friendship";
        var initialMoves = PokemonMoveRules.InitialMoves(content.Catalog, species, 5);
        if (!p.Moves.SequenceEqual(initialMoves)) return "gift_moves";
        return null;
    }

    internal static string? ValidateInstance(PokemonCatalog catalog, PokemonInstance? p, PokemonMechanicsData? mechanics = null) =>
        ValidateInstanceCore(catalog, p, mechanics ?? catalog.MechanicsData, null);

    // No raw override flag is accepted: this path requires the exact authenticated source enemy.
    internal static string? ValidateTrainerTowerInstance(PokemonCatalog catalog, PokemonInstance? p,
        Game.Domain.Battle.TrainerTowerEncounterRoster proof) =>
        catalog.MechanicsData is null || !proof.MatchesIdentity(p)
            ? "pokemon_trainer_tower_identity" : ValidateInstanceCore(catalog, p, catalog.MechanicsData, p!.AbilitySlot);

    internal static string? ValidateEggInstance(PokemonCatalog catalog, PokemonInstance? p,
        PokemonMechanicsData? mechanics = null)
    {
        if (p is null) return "pokemon_missing";
        if (catalog.BreedingData is not { } metadata ||
            !metadata.Species.TryGetValue(p.Species, out var row)) return "pokemon_egg_catalog";
        // Bound this path to offspring of supported breedable parents (and source incense
        // fallback), rather than admitting arbitrary evolved or legendary egg instances.
        if (!metadata.Species.Values.Any(parent => parent.Breeding.EggGroup1 is not
                (FourIslandDayCareRules.DittoEggGroup or FourIslandDayCareRules.UndiscoveredEggGroup) &&
                parent.BaseEggSpecies == p.Species) && p.Species.Value is not
                ("pret:SPECIES_WOBBUFFET" or "pret:SPECIES_MARILL")) return "pokemon_egg_species";
        if (p.EggStatCalculationIvs is null) return "pokemon_egg_stat_basis";
        if (p.Level != SourceEggRules.HatchLevel || !catalog.Species.TryGetValue(p.Species, out var species) ||
            p.Experience != GrowthRules.ExperienceAtLevel(species.GrowthRate, SourceEggRules.HatchLevel) ||
            p.Friendship > row.Breeding.EggCycles || p.Evs != SixStats.Zero || p.StatCalculationEvs is not null ||
            p.NpcTradeOrigin is not null || p.ContestConditions is not null ||
            p.Stats is null || p.CurrentHp != p.Stats.MaxHp ||
            p.Moves.IsDefaultOrEmpty || p.Moves.Any(m => m is null || m.PpUps != 0 || m.CurrentPp != m.MaximumPp))
            return "pokemon_egg_state";
        return ValidateInstanceCore(catalog, p, mechanics ?? catalog.MechanicsData, null, isEgg: true);
    }

    private static string? ValidateInstanceCore(PokemonCatalog catalog, PokemonInstance? p, PokemonMechanicsData? mechanics,
        int? sourceAbilitySlot, bool isEgg = false)
    {
        if (p is null) return "pokemon_missing";
        if (!ValidSymbol(p.Species.Value, "pret:SPECIES_") || !catalog.Species.TryGetValue(p.Species, out var species))
            return "pokemon_species";
        if (p.SourceSpeciesId != species.SourceSpeciesId) return "pokemon_source_species_id";
        if (p.Level is < 1 or > 100) return "pokemon_level";
        uint minimum = GrowthRules.ExperienceAtLevel(species.GrowthRate, p.Level);
        uint maximum = GrowthRules.ExperienceAtLevel(species.GrowthRate, 100);
        if (p.Experience < minimum || p.Experience > maximum ||
            (p.Level < 100 && p.Experience >= GrowthRules.ExperienceAtLevel(species.GrowthRate, p.Level + 1)))
            return "pokemon_experience";
        if (!Enum.IsDefined(p.Nature) || p.Nature != (Nature)(p.Personality % 25)) return "pokemon_nature";
        if (p.Ivs is null || OutsideRange(p.Ivs, 31)) return "pokemon_ivs";
        if (!isEgg && p.EggStatCalculationIvs is not null) return "pokemon_egg_instance";
        if (isEgg && (p.EggStatCalculationIvs is null || OutsideRange(p.EggStatCalculationIvs, 31)))
            return "pokemon_egg_stat_basis";
        if (p.ContestConditions is { } conditions &&
            new[] { conditions.Cool, conditions.Beauty, conditions.Cute, conditions.Smart, conditions.Tough, conditions.Sheen }
                .Any(value => value is < 0 or > 255)) return "pokemon_contest_conditions";
        if (NpcTradePokemonOriginRules.ValidateInstance(catalog, p, out int? tradeAbilitySlot) is { } originError)
            return originError;
        if (sourceAbilitySlot is not null && tradeAbilitySlot is not null) return "pokemon_npc_trade_origin";
        if (p.Evs is null || OutsideRange(p.Evs, 255) || Total(p.Evs) > 510) return "pokemon_evs";
        var statEvs = p.StatCalculationEvs ?? p.Evs;
        if (OutsideRange(statEvs, 255) || Total(statEvs) > 510 ||
            Exceeds(statEvs, p.Evs))
            return "pokemon_stat_calculation_evs";
        var expected = PokemonStatCalculation.Calculate(species, p.Level, p.Nature,
            isEgg ? p.EggStatCalculationIvs! : p.Ivs, statEvs, mechanics);
        if (p.Stats != expected) return "pokemon_stats";
        if (p.CurrentHp < 0 || p.CurrentHp > expected.MaxHp) return "pokemon_hp";
        if (!Enum.IsDefined(p.Gender) || p.Gender != PokemonFactory.GenderFromPersonality(species.GenderRatio, p.Personality))
            return "pokemon_gender";
        // Source trades and Tower enemies must independently authenticate the immutable source
        // identity before their stored ability bit can differ from the ordinary PID-derived bit.
        int abilitySlot = tradeAbilitySlot ?? sourceAbilitySlot ?? (species.Ability2 == "ABILITY_NONE" ? 0 : (int)(p.Personality & 1));
        if (abilitySlot is < 0 or > 1 || abilitySlot == 1 && species.Ability2 == "ABILITY_NONE") return "pokemon_ability";
        if (p.AbilitySlot != abilitySlot || p.AbilityId != (abilitySlot == 0 ? species.Ability1 : species.Ability2))
            return "pokemon_ability";
        if (p.Friendship is < 0 or > 255) return "pokemon_friendship";
        if (p.Moves.IsDefaultOrEmpty || p.Moves.Length > 4) return "pokemon_move_count";
        var seen = new HashSet<MoveId>();
        foreach (var slot in p.Moves)
        {
            if (slot is null) return "pokemon_move_missing";
            if (!ValidSymbol(slot.Move.Value, "pret:MOVE_") || !catalog.Moves.TryGetValue(slot.Move, out var move))
                return "pokemon_move_id";
            if (!seen.Add(slot.Move)) return "pokemon_move_duplicate";
            if (slot.PpUps is < 0 or > 3) return "pokemon_move_pp_ups";
            // src/pokemon.c:3898-3902: basePP + basePP * 20 * bonus / 100, integer floor.
            int maximumPp = PokemonMoveRules.MaximumPp(move, slot.PpUps);
            if (slot.MaximumPp != maximumPp) return "pokemon_move_maximum_pp";
            if (slot.CurrentPp < 0 || slot.CurrentPp > maximumPp) return "pokemon_move_current_pp";
        }
        return null;
    }

    // Validate six scalar fields without allocating an ordered Values array per check.
    private static bool OutsideRange(SixStats stats, int maximum) =>
        stats.Hp < 0 || stats.Hp > maximum || stats.Attack < 0 || stats.Attack > maximum ||
        stats.Defense < 0 || stats.Defense > maximum || stats.Speed < 0 || stats.Speed > maximum ||
        stats.SpecialAttack < 0 || stats.SpecialAttack > maximum ||
        stats.SpecialDefense < 0 || stats.SpecialDefense > maximum;

    private static int Total(SixStats stats) => stats.Hp + stats.Attack + stats.Defense +
        stats.Speed + stats.SpecialAttack + stats.SpecialDefense;

    private static bool Exceeds(SixStats value, SixStats limit) =>
        value.Hp > limit.Hp || value.Attack > limit.Attack || value.Defense > limit.Defense ||
        value.Speed > limit.Speed || value.SpecialAttack > limit.SpecialAttack ||
        value.SpecialDefense > limit.SpecialDefense;

    private static string? ValidateCatalog(PokemonCatalog? catalog)
    {
        if (catalog is null) return "pokemon_catalog_missing";
        try { PokemonFactory.ValidateCatalog(catalog); }
        catch (ArgumentException) { return "pokemon_catalog_invalid"; }
        catch (NotSupportedException) { return "pokemon_catalog_unsupported"; }
        return null;
    }

    private static string? ValidateContent(CampaignPokemonContent? content)
    {
        if (content is null) return "pokemon_content_missing";
        var error = ValidateCatalog(content.Catalog);
        if (error is not null) return error;
        return null;
    }

    internal static string? ValidateOriginMetadata(PokemonCatalog catalog, PartyPokemon owned)
    {
        if (owned.MetLocation == SourceGiftEggRules.MetLocation)
            return SourceGiftEggRules.AdmitsSpecialOrigin(catalog, owned) ? null : "party_gift_egg_origin";
        if (!ValidSymbol(owned.MetLocation, "MAPSEC_") && owned.MetLocation != NpcTradePokemonOriginRules.MetLocation)
            return "party_met_location";
        return NpcTradePokemonOriginRules.ValidateParty(catalog, owned);
    }

    private static bool ValidName(string? value, int maximumLength) => NameTextRules.IsValidName(value, maximumLength);

    private static bool ValidSymbol(string? value, string prefix) => value is not null && value.Length <= 128 &&
        value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length &&
        value[prefix.Length..].All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_');
}
