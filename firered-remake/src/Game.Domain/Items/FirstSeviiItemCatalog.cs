using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.World;
namespace Game.Domain.Items;
public sealed record FirstSeviiSourceItem(QuestItemDefinition Item, string Description);
public sealed record FirstSeviiRenewableGroup(string MapSymbol, ImmutableArray<string?> Rare,
    ImmutableArray<string?> Uncommon, ImmutableArray<string?> Common, OpeningItemSourceRef Source);
/// <summary>Exact source declarations. Renewable hidden-item reset is an explicit field-service integration requirement.</summary>
public sealed record FirstSeviiItemCatalog(string SourceCommit, string Profile, ImmutableArray<FirstSeviiSourceItem> Items,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<EarlyShopStock> Shops,
    ImmutableArray<FieldPickupDefinition> Pickups, ImmutableArray<FirstSeviiRenewableGroup> RenewableGroups,
    int RenewableStepThreshold)
{
    public const string Version = "firered-first-sevii-items-v1";
    public void Validate()
    {
        if (SourceCommit != PokemonFactory.SourceCommit || Profile != Version || Items.IsDefault || Additions.IsDefault ||
            Shops.IsDefault || Pickups.IsDefault || RenewableGroups.IsDefault || Items.Length != 34 || Additions.Length != 10 ||
            Shops.Length != 3 || Pickups.Length != 22 || RenewableGroups.Length != 2 || RenewableStepThreshold != 1500 ||
            CampaignSourceFingerprint.Compute(this) != "5d61f3c713abb00e2f8a004d4a9e5f52e5e34683b37d7cd9b413e198b4ce138e")
            throw new NotSupportedException("first_sevii_item_source");
    }
}
