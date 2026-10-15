using System.Collections.Immutable;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // Called only after an authoritative release or NPC exchange, or explicit offline conversion.
    // One-time rewards and finite trade receipts retain business facts independently.
    /// <summary>Explicit offline conversion only. Normal load never repairs or compacts saves.</summary>
    public CampaignState ConvertRetiredPokemonCheckpoint(CampaignState state)
    {
        var converted = CompactCurrentPokemonSources(state);
        if (ValidatePlayerSave(converted) is {} error)
            throw new InvalidDataException("checkpoint_conversion_invalid: " + error);
        return converted;
    }

    private CampaignState CompactCurrentPokemonSources(CampaignState state)
    {
        var owned = OwnedPokemon(state).Select(p => p.InstanceId).ToHashSet();
        var next = state with
        {
            Exploration = state.Exploration is {} exploration ? exploration with
            {
                Captured = exploration.Captured is {} captures
                    ? captures.Where(c => owned.Contains(c.InstanceId)).ToImmutableArray() : null
            } : null,
            EvolutionHistory = state.EvolutionHistory is {} evolutions
                ? evolutions.Where(e => owned.Contains(e.InstanceId)).ToImmutableArray() : null,
            PrizePokemon = state.PrizePokemon is {} prizes
                ? prizes.Where(p => owned.Contains(p.InstanceId)).ToImmutableArray() : null,
            DayCareEggs = state.DayCareEggs is {} eggs
                ? eggs.Where(e => owned.Contains(e.InstanceId)).ToImmutableArray() : null
        };
        return CampaignWildHistoryRetention.Compact(next);
    }
}
