using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>One authenticated teaching successor: 45 inherited machines and five original prizes over 174 owned species.</summary>
public sealed class GameCornerMachineCatalog
{
    public const string Profile = "firered-game-corner-teaching-v1";
    private static readonly ConditionalWeakTable<PewterMachineCatalog, GameCornerMachineCatalog> Proofs = new();
    public PewterMachineCatalog Catalog { get; }
    public DepartmentStoreMachineStock Predecessor { get; }
    public GameCornerOwnedPokemonCatalog Pokemon { get; }
    private GameCornerMachineCatalog(PewterMachineCatalog catalog, DepartmentStoreMachineStock predecessor, GameCornerOwnedPokemonCatalog pokemon)
    { Catalog = catalog; Predecessor = predecessor; Pokemon = pokemon; }
    public static GameCornerMachineCatalog? For(PewterMachineCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static GameCornerMachineCatalog Authenticate(PewterMachineCatalog original, GameCornerOwnedPokemonCatalog pokemon, PewterMachineCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(original); ArgumentNullException.ThrowIfNull(pokemon); ArgumentNullException.ThrowIfNull(candidate);
        if (DepartmentStoreMachineStock.For(original) is not { } predecessor || !DepartmentStoreMachineStock.HasRooftopRewards(original) ||
            !ReferenceEquals(OwnedMachineCatalogBinding.For(original), pokemon.Predecessor.SharedBattle) ||
            !ReferenceEquals(GameCornerOwnedPokemonCatalog.For(pokemon.Catalog), pokemon) || candidate.Profile != Profile ||
            original.AdditionalMachines is not { IsDefault: false, Length: 44 } inherited ||
            candidate.AdditionalMachines is not { IsDefault: false, Length: 49 } machines ||
            !ReferenceEquals(candidate.Tm39, original.Tm39) || !ReferenceEquals(candidate.TmCase, original.TmCase) ||
            candidate with { Profile = original.Profile, Compatibility = original.Compatibility, AdditionalMachines = original.AdditionalMachines } != original)
            throw new ArgumentException("game_corner_machine_predecessor");
        predecessor.Validate();
        void Rows(ImmutableArray<PewterMachineCompatibility> rows, ImmutableArray<PewterMachineCompatibility> before)
        {
            if (before.Length != 170 || rows.IsDefault || rows.Length != 174 || rows.Any(c => c is null) ||
                before.Where((cell, i) => !ReferenceEquals(cell, rows[i])).Any() ||
                !rows.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys) ||
                !rows.Skip(170).Select(c => c.Species).SequenceEqual(GameCornerOwnedPokemonCatalog.AdditionalSpecies) ||
                rows.Any(c => pokemon.Catalog.Species[c.Species].SourceSpeciesId != c.SourceSpeciesId))
                throw new ArgumentException("game_corner_machine_rows");
        }
        Rows(candidate.Compatibility, original.Compatibility);
        for (int i = 0; i < inherited.Length; i++)
        {
            var next = machines[i]; var before = inherited[i];
            if (next is null || !ReferenceEquals(next.Machine, before.Machine) || next with { Compatibility = before.Compatibility } != before)
                throw new ArgumentException("game_corner_machine_inherited_metadata");
            Rows(next.Compatibility, before.Compatibility);
        }
        var added = machines.Skip(inherited.Length).ToArray();
        if (!added.Select(m => m.Machine.Item.Id.Value).SequenceEqual(new[] { "pret:ITEM_TM13", "pret:ITEM_TM23", "pret:ITEM_TM24", "pret:ITEM_TM30", "pret:ITEM_TM35" }))
            throw new ArgumentException("game_corner_machine_prize_source");
        foreach (var machine in added)
            if (PewterMachineRules.Definition(original, machine.Machine.Item.Id) is not null ||
                machine.Compatibility.Length != 174 || !machine.Compatibility.Select(c => c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys) ||
                machine.Compatibility.Any(c => pokemon.Catalog.Species[c.Species].SourceSpeciesId != c.SourceSpeciesId) ||
                !PokemonMoveRules.IsLearningSupported(pokemon.Catalog, machine.Move))
                throw new ArgumentException("game_corner_machine_prize_owner");
        var proof = new GameCornerMachineCatalog(candidate with { }, predecessor, pokemon);
        Proofs.Add(proof.Catalog, proof);
        OwnedMachineCatalogBinding.Register(proof.Catalog, pokemon.SharedBattle);
        DepartmentStoreMachineStock.BindGameCornerSuccessor(proof);
        return proof;
    }
}
