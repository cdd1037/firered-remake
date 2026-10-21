using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PokemonPresentationPackage? Route11Pokemon { get; }
    public PretRoute11ItemsDocument? Route11Items { get; }
    public PretRoute11Encounters? Route11Encounters { get; }
    public OrdinaryTrainerCatalog? Route11Trainers { get; }

    private static IEnumerable<FieldPickupDefinition> Route11PickupDefinitions(
        PretRoute11ItemsDocument items,PretStoryCompilation scripts)
    {
        foreach (var pickup in items.Pickups.Where(p => p.Kind != "hidden"))
        {
            var item = items.Items.Single(i => i.Metadata.Item.Id == pickup.Item).Metadata.Item;
            if (!scripts.TryConstant(pickup.Flag, out int flag))
                throw new InvalidDataException("route11_pickup_flag_missing");

            yield return new("route11:" + pickup.Flag, "MAP_" + pickup.Map.ToUpperInvariant(), false,
                pickup.LocalObjectId, pickup.X, pickup.Y, pickup.Elevation,
                pickup.Script!, item.SourceItemSymbol,
                item.SourceItemId, item.Id, item.Name, item.Pocket, pickup.Quantity, pickup.Flag, flag);
        }
    }
}
