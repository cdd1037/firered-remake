using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Original FireRed table identity plus the original map actor and one-time script binding.</summary>
public sealed record NpcTradePokemonDefinition(int TradeId, SpeciesId RequestedSpecies, SpeciesId ReceivedSpecies,
    string Nickname, uint Personality, SixStats Ivs, uint OriginalTrainerId, string OriginalTrainerName,
    int OriginalTrainerGender, int AbilitySlot, string HeldItem, int Mail, PokemonContestConditions ContestConditions,
    string TradeSymbol, MapId Map, int LocalId, string ScriptLabel, string CompletionFlag,
    PokemonSourceRef Source, PokemonSourceRef ActorSource, PokemonSourceRef ScriptSource, bool UsesInlineSequence);

/// <summary>Immutable trade definitions bound to the source mechanics catalog.</summary>
public sealed class NpcTradePokemonCatalog
{
    public const string SemanticSha256 = "c259397a134f07268ae6d788ff7c7f2cb66d6d19755e07c3d525f4dec92c7414";
    private static readonly ConditionalWeakTable<PokemonCatalog, NpcTradePokemonCatalog> Proofs = new();
    public SourceMechanicsCatalog Mechanics { get; }
    public ImmutableDictionary<int, NpcTradePokemonDefinition> Definitions { get; }
    private NpcTradePokemonCatalog(SourceMechanicsCatalog mechanics, ImmutableArray<NpcTradePokemonDefinition> definitions)
    { Mechanics = mechanics; Definitions = definitions.ToImmutableDictionary(d => d.TradeId); }
    public bool IsEnabled(int tradeId) => tradeId is >= 0 and <= 8;

    public static NpcTradePokemonCatalog Authenticate(SourceMechanicsCatalog mechanics,
        ImmutableArray<NpcTradePokemonDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(mechanics);
        if (!ReferenceEquals(SourceMechanicsCatalog.For(mechanics.Catalog), mechanics) || definitions.IsDefault ||
            definitions.Length != 9 || definitions.Any(d => d is null || d.Ivs is null || d.ContestConditions is null ||
                d.Ivs.Values.Any(value => value is < 0 or > 31) || d.AbilitySlot is < 0 or > 1 ||
                d.OriginalTrainerGender is < 0 or > 1 || d.LocalId < 1 || string.IsNullOrWhiteSpace(d.ScriptLabel) ||
                string.IsNullOrWhiteSpace(d.CompletionFlag)) ||
            !definitions.Select(d => d.TradeId).SequenceEqual(Enumerable.Range(0, 9)))
            throw new ArgumentException("npc_trade_catalog_identity");
        foreach (var definition in definitions)
            if (!mechanics.Catalog.Species.ContainsKey(definition.RequestedSpecies) ||
                !mechanics.Catalog.Species.ContainsKey(definition.ReceivedSpecies))
                throw new ArgumentException("npc_trade_catalog_species");
        return Proofs.GetValue(mechanics.Catalog, _ => new(mechanics, definitions));
    }

    public static NpcTradePokemonCatalog? For(PokemonCatalog? catalog)
    {
        if (catalog is null) return null;
        var mechanics = SourceMechanicsCatalog.For(catalog) ?? OwnedBattlePokemonCatalog.For(catalog)?.Mechanics;
        return mechanics is not null && Proofs.TryGetValue(mechanics.Catalog, out var proof) ? proof : null;
    }

}
