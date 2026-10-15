using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record GamePokemonAdmission(SpeciesId Id, ImmutableArray<PokemonEvolution> Evolutions,
    string? FormScope = null);
public sealed record GamePokemonAdmissions(int SchemaVersion, ImmutableArray<GamePokemonAdmission> Owned,
    ImmutableArray<GamePokemonAdmission> Opponents, ImmutableArray<GamePokemonAdmission> TrainerTowerOpponents);
public sealed record GameBreedingSpecies(SpeciesId Species, SpeciesId BaseEggSpecies, ImmutableArray<MoveId> EggMoves);
public sealed record GameBreedingDocument(ImmutableArray<GameBreedingSpecies> Species, ImmutableArray<MoveId> AllMachineMoves);

/// <summary>Current gameplay admission policy. Definitions and form mechanics are resolved from the shared catalog.</summary>
public static class PretGamePokemonCatalog
{
    public const string RelativePath = "content/generated/data/pokemon/game/catalog.json";

    public static GamePokemonCatalog Load(string root, PretCanonicalPokemonCatalog canonical,
        SourceMechanicsCatalog mechanics, NpcTradePokemonCatalog trades)
        => Load(root, canonical, mechanics, trades, SharedMachineCatalog.LoadProject(root));

    public static GamePokemonCatalog Load(string root, PretCanonicalPokemonCatalog canonical,
        SourceMechanicsCatalog mechanics, NpcTradePokemonCatalog trades, SharedMachineCatalog machines)
    {
        var (owned, opponents, tower) = Resolve(File.ReadAllText(Path.Combine(root, RelativePath)), canonical, mechanics);
        var breeding = LoadBreeding(root, canonical, owned, machines);
        PokemonCatalog WithRules(PokemonCatalog catalog) => catalog with { BreedingData = breeding, NpcTrades = trades.Definitions };
        return GamePokemonCatalog.Create(SourceMechanicsCatalog.FromDefinitions(WithRules(mechanics.Catalog)),
            WithRules(owned), WithRules(opponents), WithRules(tower));
    }

    public static GamePokemonCatalog LoadContent(string json, PretCanonicalPokemonCatalog canonical,
        SourceMechanicsCatalog mechanics)
    {
        var (owned, opponents, tower) = Resolve(json, canonical, mechanics);
        return GamePokemonCatalog.Create(mechanics, owned, opponents, tower);
    }

    private static (PokemonCatalog Owned, PokemonCatalog Opponents, PokemonCatalog Tower) Resolve(
        string json, PretCanonicalPokemonCatalog canonical, SourceMechanicsCatalog mechanics)
    {
        ArgumentNullException.ThrowIfNull(canonical);
        ArgumentNullException.ThrowIfNull(mechanics);
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var document = ContentJson.ParseStrict<GamePokemonAdmissions>(json, options);
        if (document.SchemaVersion != 1) throw new InvalidDataException("game_pokemon_schema");
        return (Roster(document.Owned), Roster(document.Opponents), Roster(document.TrainerTowerOpponents));

        PokemonCatalog Roster(ImmutableArray<GamePokemonAdmission> admissions)
        {
            if (admissions.IsDefaultOrEmpty || admissions.Any(row => row is null) ||
                admissions.Select(row => row.Id).Distinct().Count() != admissions.Length)
                throw new InvalidDataException("game_pokemon_admissions");
            var species = admissions.Select(row =>
            {
                var definition = canonical.Project(row.Id, row.Evolutions);
                if (row.FormScope is not null)
                {
                    var form = canonical.GenerationContract.FormStatOverrides.SingleOrDefault(
                        form => form.Species == row.Id && form.Scope == row.FormScope)
                        ?? throw new InvalidDataException("game_pokemon_form");
                    definition = definition with { BaseStats = form.BaseStats };
                }
                return definition;
            }).ToImmutableDictionary(species => species.Id);
            return mechanics.Catalog with { Species = species };
        }
    }

    private static PokemonBreedingData LoadBreeding(string root, PretCanonicalPokemonCatalog canonical, PokemonCatalog owned, SharedMachineCatalog machines)
    {
        var document = ContentJson.ParseStrict<GameBreedingDocument>(File.ReadAllText(Path.Combine(root,
            "content/generated/data/pokemon/game/breeding.json")), ContentJson.CreateOptions());
        if (document.Species.IsDefaultOrEmpty || document.Species.Any(row => row is null || row.EggMoves.IsDefault) ||
            document.Species.Select(row => row.Species).Distinct().Count() != document.Species.Length ||
            !document.Species.Select(row => row.Species).ToHashSet().SetEquals(owned.Species.Keys) ||
            document.AllMachineMoves.IsDefault || document.AllMachineMoves.Length != 58 ||
            document.Species.Any(row => !owned.Species.ContainsKey(row.BaseEggSpecies) || row.EggMoves.Any(move => !owned.Moves.ContainsKey(move))))
            throw new InvalidDataException("game_pokemon_breeding");
        for (var index = 0; index < document.AllMachineMoves.Length; index++)
        {
            var expected = index == 57 ? new MoveId("pret:MOVE_DIVE") : machines.Definition(
                new ItemId(index < 50 ? $"pret:ITEM_TM{index + 1:00}" : $"pret:ITEM_HM{index - 49:00}")).Move;
            if (document.AllMachineMoves[index] != expected)
                throw new InvalidDataException("game_pokemon_breeding_machine_order");
        }
        var rows = document.Species.Select(row =>
        {
            var metadata = canonical.Metadata[row.Species];
            var breeding = new SourceBreedingSpecies(row.Species, EggGroup(metadata.EggGroups[0]),
                EggGroup(metadata.EggGroups[1]), checked((byte)metadata.EggCycles));
            var inherited = Enumerable.Range(0, document.AllMachineMoves.Length).Where(index => machines.Compatibility(
                new ItemId(index < 50 ? $"pret:ITEM_TM{index + 1:00}" : $"pret:ITEM_HM{index - 49:00}"), row.Species).CanLearn)
                .Select(index => document.AllMachineMoves[index]).ToImmutableArray();
            return new SourceEggSpeciesRow(breeding, row.BaseEggSpecies, row.EggMoves, inherited);
        }).ToImmutableArray();
        return PokemonBreedingData.Create(owned, rows, document.AllMachineMoves);
    }

    private static int EggGroup(string symbol) => symbol switch
    {
        "EGG_GROUP_NONE" => 0, "EGG_GROUP_MONSTER" => 1, "EGG_GROUP_WATER_1" => 2,
        "EGG_GROUP_BUG" => 3, "EGG_GROUP_FLYING" => 4, "EGG_GROUP_FIELD" => 5,
        "EGG_GROUP_FAIRY" => 6, "EGG_GROUP_GRASS" => 7, "EGG_GROUP_HUMAN_LIKE" => 8,
        "EGG_GROUP_WATER_3" => 9, "EGG_GROUP_MINERAL" => 10, "EGG_GROUP_AMORPHOUS" => 11,
        "EGG_GROUP_WATER_2" => 12, "EGG_GROUP_DITTO" => 13, "EGG_GROUP_DRAGON" => 14,
        "EGG_GROUP_UNDISCOVERED" => 15, _ => throw new InvalidDataException("game_pokemon_egg_group")
    };
}
