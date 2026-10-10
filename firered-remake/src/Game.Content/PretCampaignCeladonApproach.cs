using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretCeladonApproachRegion? CeladonApproachRegion { get; }
    public PretCeladonApproachPokemon? CeladonApproachPokemon { get; }
    public PretCeladonApproachItemsDocument? CeladonApproachItems { get; }
    public PretCeladonApproachEncounters? CeladonApproachEncounters { get; }
    public CeladonApproachTrainerCatalog? CeladonApproachTrainers { get; }
    public bool CeladonApproachEnabled => CeladonApproachRegion is not null;

    private static PokemonCatalog CeladonApproachCampaignPokemon(PokemonCatalog catalog, PokemonCatalog previous)
    {
        PokemonFactory.ValidateCatalog(catalog);
        PokemonFactory.ValidateCatalog(previous);
        if (catalog.Species.Count != 94 || previous.Species.Count != 93)
            throw new InvalidDataException("celadon-approach_campaign_species_scope_differs");
        var moves = catalog.Moves;
        // The regional catalog is composed from the original Route11 Pokemon source.
        // Retain the separate Gym supplement, including Shock Wave, in the campaign.
        foreach (var (id, move) in previous.Moves)
        {
            if (moves.TryGetValue(id, out var current))
            {
                if (current with { Flags = move.Flags, Source = move.Source } != move ||
                    !current.Flags.SequenceEqual(move.Flags))
                    throw new InvalidDataException("Conflicting CeladonApproach predecessor move: " + id);
            }
            else moves = moves.Add(id, move);
        }
        var result = catalog with { Moves = moves };
        PokemonFactory.ValidateCatalog(result);
        return result;
    }

    private static IEnumerable<FieldPickupDefinition> CeladonApproachPickupDefinitions(
        PretCeladonApproachItemsDocument items, EarlyShopCatalog shop, PewterMachineCatalog machines, PretStoryCompilation scripts)
    {
        foreach (var pickup in items.Pickups)
        {
            var item = shop.Items.FirstOrDefault(i => i.Id == pickup.Item);
            var machine = PewterMachineRules.Definition(machines, pickup.Item)?.Machine.Item;
            int itemNumber = item?.SourceItemId ?? machine?.SourceItemId ??
                throw new InvalidDataException("celadon-approach_pickup_item_missing: " + pickup.Item);
            string symbol = item?.SourceItemSymbol ?? machine!.SourceItemSymbol;
            string name = item?.Name ?? machine!.Name;
            string pocket = item?.Pocket ?? machine!.Pocket;
            if (!scripts.TryConstant(pickup.Flag, out int flag))
                throw new InvalidDataException("celadon-approach_pickup_flag_missing: " + pickup.Flag);
            bool hidden = pickup.Kind == "hidden";
            string map = "MAP_" + Regex.Replace(pickup.Map, "([a-z])([A-Z])", "$1_$2").ToUpperInvariant();
            yield return new("celadon-approach:" + pickup.Flag, map, hidden, pickup.LocalObjectId,
                pickup.X, pickup.Y, pickup.Elevation, hidden ? "EventScript_HiddenItemScript" : pickup.Script!,
                symbol, itemNumber, pickup.Item, name, pocket, pickup.Quantity, pickup.Flag, flag);
        }
    }
}
