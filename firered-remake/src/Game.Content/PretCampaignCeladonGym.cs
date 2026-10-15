using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretCeladonGymRegion? CeladonGymRegion { get; }
    public PretCeladonGymPokemon? CeladonGymPokemon { get; }
    public PretCeladonGymItemsDocument? CeladonGymItems { get; }
    public CeladonGymTrainerCatalog? CeladonGymTrainers { get; }
    public bool CeladonGymEnabled => CeladonGymRegion is not null;

    private static PokemonCatalog CeladonGymCampaignPokemon(PokemonCatalog catalog,PokemonCatalog previous)
    {
        PokemonFactory.ValidateCatalog(catalog);
        PokemonFactory.ValidateCatalog(previous);
        if (catalog.Species.Count != 98 || previous.Species.Count != 94)
            throw new InvalidDataException("celadon-gym_campaign_species_scope_differs");
        var moves = catalog.Moves;
        // The regional catalog is composed from the authenticated Celadon approach source.
        // Retain the separate Gym supplement, including Shock Wave, in the campaign.
        foreach (var (id, move) in previous.Moves)
        {
            if (moves.TryGetValue(id, out var current))
            {
                if (current with { Flags = move.Flags,Source = move.Source } != move ||
                    !current.Flags.SequenceEqual(move.Flags))
                    throw new InvalidDataException("Conflicting CeladonGym predecessor move: " + id);
            }
            else moves = moves.Add(id, move);
        }
        var result = catalog with { Moves = moves };
        PokemonFactory.ValidateCatalog(result);
        return result;
    }

}
