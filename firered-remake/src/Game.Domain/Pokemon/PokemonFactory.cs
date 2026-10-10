using System.Collections.Immutable;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Rules;

namespace Game.Domain.Pokemon;

/// <summary>Bounded CreateMon/CreateBoxMon semantics. No IO, shared RNG, battle effects or original-save encoding.</summary>
public static class PokemonFactory
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    // Serialized legacy starter-content profile; preserve it when adding separately locked catalogs.
    public const string RulesVersion = "firered-starter-instances-v1";
    public static ImmutableArray<SpeciesId> SupportedSpecies { get; } =
        new[] { "BULBASAUR", "IVYSAUR", "VENUSAUR", "CHARMANDER", "CHARMELEON", "CHARIZARD", "SQUIRTLE", "WARTORTLE", "BLASTOISE" }
            .Select(x => new SpeciesId("pret:SPECIES_" + x)).ToImmutableArray();

    // Keep the reviewed starter profile intact; the separate Route 1 content profile adds two complete families.
    public static ImmutableArray<SpeciesId> Route1SupportedSpecies { get; } = SupportedSpecies.AddRange(
        new[] { "PIDGEY", "PIDGEOTTO", "PIDGEOT", "RATTATA", "RATICATE" }.Select(x => new SpeciesId("pret:SPECIES_" + x)));
    // The tutorial catalog keeps Weedle's evolution references intact; it does not enable
    // those species in ordinary encounters or claim their battle effects are implemented.
    public static ImmutableArray<SpeciesId> OldManTutorialSupportedSpecies { get; } = Route1SupportedSpecies.AddRange(
        new[] { "WEEDLE", "KAKUNA", "BEEDRILL" }.Select(x => new SpeciesId("pret:SPECIES_" + x)));
    // Full forward evolution families reachable in Route 2 / Viridian Forest.
    public static ImmutableArray<SpeciesId> ForestSupportedSpecies { get; } = OldManTutorialSupportedSpecies.AddRange(
        new[] { "CATERPIE", "METAPOD", "BUTTERFREE", "PIKACHU", "RAICHU" }.Select(x => new SpeciesId("pret:SPECIES_" + x)));
    // Gym families retain the original non-Kanto Steelix target as data only. No trade execution or encounters are enabled.
    public static ImmutableArray<SpeciesId> PewterSupportedSpecies { get; } = ForestSupportedSpecies.AddRange(
        new[] { "GEODUDE", "GRAVELER", "GOLEM", "SANDSHREW", "SANDSLASH", "ONIX", "STEELIX" }
            .Select(x => new SpeciesId("pret:SPECIES_" + x)));
    // Route22's bounded level20 catalog adds these three exact identities. Primeape is deferred.
    public static ImmutableArray<SpeciesId> Route22SupportedSpecies { get; } = PewterSupportedSpecies.AddRange(
        new[] { "SPEAROW", "FEAROW", "MANKEY" }.Select(x => new SpeciesId("pret:SPECIES_" + x)));
    // Reconstructed Route3 bounded catalog contract; no new item/trade executor.
    public static ImmutableArray<SpeciesId> Route3SupportedSpecies { get; } = Route22SupportedSpecies.AddRange(
        new[] { "EKANS", "NIDORAN_F", "NIDORINA", "NIDORAN_M", "NIDORINO", "JIGGLYPUFF", "MAGIKARP", "GYARADOS" }
            .Select(x => new SpeciesId("pret:SPECIES_" + x)));
    // Source-only Mt. Moon composition; admission does not enable battle effects or item use.
    public static ImmutableArray<SpeciesId> MtMoonSupportedSpecies { get; } = Route3SupportedSpecies.AddRange(
        new[] { "ARBOK", "CLEFAIRY", "ZUBAT", "GOLBAT", "ODDISH", "PARAS", "PARASECT", "BELLSPROUT", "MAGNEMITE", "GRIMER", "VOLTORB", "KOFFING", "NIDOQUEEN", "NIDOKING", "CLEFABLE", "WIGGLYTUFF" }
            .Select(x => new SpeciesId("pret:SPECIES_" + x)));
    public static ImmutableArray<SpeciesId> CeruleanSupportedSpecies { get; } = MtMoonSupportedSpecies.AddRange(
        new[] { "ABRA", "KADABRA", "MACHOP", "DROWZEE", "SHELLDER", "HORSEA", "GOLDEEN", "STARYU", "STARMIE" }
            .Select(x => new SpeciesId("pret:SPECIES_" + x)));
    public static ImmutableArray<SpeciesId> BillSupportedSpecies { get; } = CeruleanSupportedSpecies.AddRange(
        new[] { "SLOWPOKE", "GLOOM", "WEEPINBELL" }.Select(x => new SpeciesId("pret:SPECIES_" + x)));
    public static ImmutableArray<SpeciesId> VermilionSupportedSpecies { get; } = BillSupportedSpecies.AddRange(
        new[] { "MEOWTH", "PERSIAN" }.Select(x => new SpeciesId("pret:SPECIES_" + x)));
    public static ImmutableArray<SpeciesId> SsAnneSupportedSpecies { get; } = VermilionSupportedSpecies.AddRange(
        new[] { "GROWLITHE", "PONYTA", "TENTACOOL" }.Select(x => new SpeciesId("pret:SPECIES_" + x)));
    public static ImmutableArray<SpeciesId> Route11SupportedSpecies { get; } = SsAnneSupportedSpecies.AddRange(
        new[] { "DIGLETT", "DUGTRIO", "MAGNETON", "POLIWAG", "POLIWHIRL", "VULPIX" }.Select(x => new SpeciesId("pret:SPECIES_" + x)));
    private static readonly ImmutableDictionary<SpeciesId,int> SourceIds = Route11SupportedSpecies
        .Zip(new[]{1,2,3,4,5,6,7,8,9,16,17,18,19,20,13,14,15,10,11,12,25,26,74,75,76,27,28,95,208,21,22,56,23,29,30,32,33,39,129,130,24,35,41,42,43,46,47,69,81,88,100,109,31,34,36,40,63,64,66,96,90,116,118,120,121,79,44,70,52,53,58,77,72,50,51,82,60,61,37},(id,source)=>(Id:id,Source:source)).ToImmutableDictionary(x=>x.Id,x=>x.Source);

    public static PokemonCreationResult Create(PokemonCatalog catalog, CreatePokemonRequest request, RngState rng)
    {
        // Preflight every fallible content/policy operation before consuming even a tentative draw.
        ValidateCatalog(catalog);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Ivs);
        ArgumentNullException.ThrowIfNull(request.Personality);
        if (!catalog.Species.TryGetValue(request.Species, out var species))
            throw new NotSupportedException("Requested species is absent from the verified bounded catalog.");
        if (request.Level is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(request.Level));
        if (!Enum.IsDefined(request.Ivs.Mode) || request.Ivs.FixedValue is < 0 or > 31 ||
            (request.Ivs.Mode == IvMode.Random && request.Ivs.FixedValue != 0))
            throw new ArgumentException("Use random IVs, or one fixed IV from 0 through 31 for all six stats.", nameof(request));
        if (!Enum.IsDefined(request.Personality.Mode) ||
            (request.Personality.Mode == PersonalityMode.Random && request.Personality.FixedValue != 0))
            throw new ArgumentException("Only a random or explicitly fixed personality is supported.", nameof(request));
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unsupported RNG algorithm.");
        int draws = (request.Personality.Mode == PersonalityMode.Random ? 2 : 0) + (request.Ivs.Mode == IvMode.Random ? 2 : 0);
        if (rng.Cursor > ulong.MaxValue - (ulong)draws) throw new OverflowException("RNG cursor cannot accommodate creation.");
        uint experience = GrowthRules.ExperienceAtLevel(species.GrowthRate, request.Level);
        var moves = PokemonMoveRules.InitialMoves(catalog, species, request.Level);
        var trace = ImmutableArray.CreateBuilder<RandomTrace>(draws);
        ushort Draw(string purpose)
        {
            var draw = Gen3Rng.Next(rng, purpose);
            rng = draw.State; trace.Add(draw.Trace); return draw.Value;
        }
        uint personality = request.Personality.Mode == PersonalityMode.Fixed ? request.Personality.FixedValue :
            (uint)Draw("pokemon.personality.low16") | ((uint)Draw("pokemon.personality.high16") << 16);
        SixStats ivs;
        if (request.Ivs.Mode == IvMode.Fixed)
        {
            int iv = request.Ivs.FixedValue;
            ivs = new(iv, iv, iv, iv, iv, iv);
        }
        else
        {
            uint first = Draw("pokemon.ivs.hp-attack-defense"), second = Draw("pokemon.ivs.speed-specialAttack-specialDefense");
            ivs = new((int)(first & 31), (int)((first >> 5) & 31), (int)((first >> 10) & 31),
                (int)(second & 31), (int)((second >> 5) & 31), (int)((second >> 10) & 31));
        }
        var nature = (Nature)(personality % 25);
        int Other(int b, int iv, Stat stat) => StatRules.CalculateOther(b, iv, 0, request.Level, nature, stat);
        var b = species.BaseStats;
        var stats = new BattleStats(StatRules.CalculateHp(b.Hp, ivs.Hp, 0, request.Level),
            Other(b.Attack, ivs.Attack, Stat.Attack), Other(b.Defense, ivs.Defense, Stat.Defense),
            Other(b.SpecialAttack, ivs.SpecialAttack, Stat.SpecialAttack), Other(b.SpecialDefense, ivs.SpecialDefense, Stat.SpecialDefense),
            Other(b.Speed, ivs.Speed, Stat.Speed));
        int slot = species.Ability2 == "ABILITY_NONE" ? 0 : (int)(personality & 1);
        var pokemon = new PokemonInstance(species.Id, species.SourceSpeciesId, request.Level, experience,
            request.OriginalTrainerId, personality, nature, ivs, SixStats.Zero, stats, stats.MaxHp,
            GenderFromPersonality(species.GenderRatio, personality), slot, slot == 0 ? species.Ability1 : species.Ability2,
            species.Friendship, moves);
        return new(pokemon, rng, trace.ToImmutable());
    }

    public static PokemonGender GenderFromPersonality(int genderRatio, uint personality)
    {
        if (genderRatio is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(genderRatio));
        return genderRatio switch
        {
            0 => PokemonGender.Male, 254 => PokemonGender.Female, 255 => PokemonGender.Genderless,
            _ => genderRatio > (personality & 255) ? PokemonGender.Female : PokemonGender.Male
        };
    }

    public static void ValidateCatalog(PokemonCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit) throw new NotSupportedException("Unsupported Pokemon source commit.");
        if (catalog.Species is null || catalog.Moves is null || catalog.Species.Count is not (9 or 14 or 17 or 22 or 29 or 32 or 40 or 56 or 65 or 68 or 70 or 73 or 79) || catalog.Moves.Count == 0)
            throw new ArgumentException("A complete bounded starter, Route 1, old-man tutorial, forest, Pewter, Route22, Route3, Mt. Moon or Cerulean catalog is required.");
        var supported=catalog.Species.Count switch{9=>SupportedSpecies,14=>Route1SupportedSpecies,17=>OldManTutorialSupportedSpecies,22=>ForestSupportedSpecies,29=>PewterSupportedSpecies,32=>Route22SupportedSpecies,40=>Route3SupportedSpecies,56=>MtMoonSupportedSpecies,65=>CeruleanSupportedSpecies,68=>BillSupportedSpecies,70=>VermilionSupportedSpecies,79=>Route11SupportedSpecies,_=>SsAnneSupportedSpecies};
        foreach (var (id, species) in catalog.Species)
        {
            if (species is null || species.Id != id || !supported.Contains(id) || species.SourceSpeciesId != SourceIds[id])
                throw new ArgumentException("Invalid or unsupported species identity.");
            if (species.BaseStats is null || species.BaseStats.Values.Any(x => x is < 1 or > 255) ||
                species.EvYield is null || species.EvYield.Values.Any(x => x is < 0 or > 3) ||
                species.CatchRate is < 1 or > 255 || species.ExperienceYield is < 1 or > 255 ||
                species.Friendship is < 0 or > 255 || species.GenderRatio is < 0 or > 255 ||
                !ValidSymbol(species.Type1, "TYPE_") || !ValidSymbol(species.Type2, "TYPE_") ||
                !ValidSymbol(species.Ability1, "ABILITY_") || !ValidSymbol(species.Ability2, "ABILITY_"))
                throw new ArgumentException("Invalid species data.");
            _ = GrowthRules.ExperienceAtLevel(species.GrowthRate, 1);
            if (species.LevelUpMoves.IsDefaultOrEmpty || species.Evolutions.IsDefault) throw new ArgumentException("Missing learnset/evolution array.");
            int previous = 0;
            foreach (var move in species.LevelUpMoves)
            {
                if (move is null || move.Level is < 1 or > 100 || move.Level < previous || !catalog.Moves.ContainsKey(move.Move))
                    throw new ArgumentException("Unordered, missing, or invalid learnset entry.");
                previous = move.Level;
            }
            foreach (var evolution in species.Evolutions)
            {
                if (evolution is null || !catalog.Species.ContainsKey(evolution.Target) || !Enum.IsDefined(evolution.Method))
                    throw new ArgumentException("Invalid evolution reference.");
                if (evolution.Method == PokemonEvolutionMethod.Level)
                {
                    if (evolution.Level is < 1 or > 100 || evolution.Parameter is not null)
                        throw new ArgumentException("Invalid level-evolution parameter.");
                }
                else if (evolution.Method == PokemonEvolutionMethod.Item)
                {
                    bool thunderStone = catalog.Species.Count is 22 or 29 or 32 or 40 or 56 or 65 or 68 or 70 or 73 or 79 &&
                        id.Value == "pret:SPECIES_PIKACHU" && evolution.Target.Value == "pret:SPECIES_RAICHU" &&
                        evolution.Parameter == "ITEM_THUNDER_STONE";
                    bool moonStone = catalog.Species.Count is 56 or 65 or 68 or 70 or 73 or 79 && evolution.Parameter == "ITEM_MOON_STONE" &&
                        (id.Value, evolution.Target.Value) is
                            ("pret:SPECIES_NIDORINA", "pret:SPECIES_NIDOQUEEN") or
                            ("pret:SPECIES_NIDORINO", "pret:SPECIES_NIDOKING") or
                            ("pret:SPECIES_CLEFAIRY", "pret:SPECIES_CLEFABLE") or
                            ("pret:SPECIES_JIGGLYPUFF", "pret:SPECIES_WIGGLYTUFF");
                    bool waterStone = catalog.Species.Count is 65 or 68 or 70 or 73 or 79 && id.Value == "pret:SPECIES_STARYU" &&
                        evolution.Target.Value == "pret:SPECIES_STARMIE" && evolution.Parameter == "ITEM_WATER_STONE";
                    if (evolution.Level != 0 || !(thunderStone || moonStone || waterStone))
                        throw new ArgumentException("Unsupported item-evolution metadata. Item use/evolution execution is not implemented.");
                }
                else if (evolution.Method == PokemonEvolutionMethod.Trade)
                {
                    if (catalog.Species.Count is not (29 or 32 or 40 or 56 or 65 or 68 or 70 or 73 or 79) || id.Value != "pret:SPECIES_GRAVELER" ||
                        evolution.Level != 0 || evolution.Target.Value != "pret:SPECIES_GOLEM" || evolution.Parameter is not null)
                        throw new ArgumentException("Unsupported trade-evolution metadata. Trade execution is not implemented.");
                }
                else if (catalog.Species.Count is not (29 or 32 or 40 or 56 or 65 or 68 or 70 or 73 or 79) || id.Value != "pret:SPECIES_ONIX" ||
                    evolution.Level != 0 || evolution.Target.Value != "pret:SPECIES_STEELIX" || evolution.Parameter != "ITEM_METAL_COAT")
                    throw new ArgumentException("Unsupported trade-item evolution metadata. Trade execution is not implemented.");
            }
        }
        foreach (var (id, move) in catalog.Moves)
        {
            if (move is null || id != move.Id || !ValidSymbol(id.Value, "pret:MOVE_") || move.SourceMoveId is < 1 or > 354 ||
                move.Pp is < 1 or > 40 || move.Power is < 0 or > 255 || move.Accuracy is < 0 or > 100 ||
                move.SecondaryEffectChance is < 0 or > 100 || move.Priority is < -7 or > 7 ||
                !ValidSymbol(move.Effect, "EFFECT_") || !ValidSymbol(move.Type, "TYPE_") || !ValidSymbol(move.Target, "MOVE_TARGET_") ||
                move.Flags.IsDefault || move.BattleEffectImplemented)
                throw new ArgumentException("Invalid move data or unsupported battle-effect implementation claim.");
        }
    }
    private static bool ValidSymbol(string value, string prefix) => value is not null && value.StartsWith(prefix, StringComparison.Ordinal)
        && value.Length > prefix.Length && value[prefix.Length..].All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_');
}
