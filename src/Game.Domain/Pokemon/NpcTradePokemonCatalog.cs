using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Original FireRed table identity plus the original map actor and one-time script binding.</summary>
public sealed record NpcTradePokemonDefinition(int TradeId, SpeciesId RequestedSpecies, SpeciesId ReceivedSpecies,
    string Nickname, uint Personality, SixStats Ivs, uint OriginalTrainerId, string OriginalTrainerName,
    int OriginalTrainerGender, int AbilitySlot, string HeldItem, int Mail, PokemonContestConditions ContestConditions,
    string TradeSymbol, MapId Map, int LocalId, string ScriptLabel, string CompletionFlag,
    bool UsesInlineSequence);

/// <summary>Immutable trade definitions bound to the source mechanics catalog.</summary>
public sealed class NpcTradePokemonCatalog
{
    public ImmutableDictionary<int, NpcTradePokemonDefinition> Definitions { get; }
    private NpcTradePokemonCatalog( ImmutableArray<NpcTradePokemonDefinition> definitions)
    { Definitions = definitions.ToImmutableDictionary(d => d.TradeId); }
    public bool IsEnabled(int tradeId) => tradeId is >= 0 and <= 8;

    public static NpcTradePokemonCatalog Create(PokemonCatalog catalog,
        ImmutableArray<NpcTradePokemonDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (definitions.IsDefault ||
            definitions.Length != 9 || definitions.Any(d => d is null || d.Ivs is null || d.ContestConditions is null ||
                d.Ivs.Values.Any(value => value is < 0 or > 31) || d.AbilitySlot is < 0 or > 1 ||
                d.OriginalTrainerGender is < 0 or > 1 || d.LocalId < 1 || string.IsNullOrWhiteSpace(d.ScriptLabel) ||
                string.IsNullOrWhiteSpace(d.CompletionFlag)) ||
            !definitions.Select(d => d.TradeId).SequenceEqual(Enumerable.Range(0, 9)))
            throw new ArgumentException("npc_trade_catalog_identity");
        foreach (var definition in definitions)
            if (!catalog.Species.ContainsKey(definition.RequestedSpecies) ||
                !catalog.Species.ContainsKey(definition.ReceivedSpecies))
                throw new ArgumentException("npc_trade_catalog_species");
        return new(definitions);
    }
}
