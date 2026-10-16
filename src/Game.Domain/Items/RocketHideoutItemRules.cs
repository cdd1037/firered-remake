using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Exact additive Rocket Hideout inventory metadata. Executable uses are independently admitted.</summary>
public static class RocketHideoutItemRules
{
    public const string Profile = "firered-rocket-hideout-items-v1";
    public static ItemId LiftKeyId { get; } = new("pret:ITEM_LIFT_KEY");
    public static ItemId SilphScopeId { get; } = new("pret:ITEM_SILPH_SCOPE");
    public static ItemId BlackGlassesId { get; } = new("pret:ITEM_BLACK_GLASSES");
    public static ItemId XSpeedId { get; } = new("pret:ITEM_X_SPEED");
    public static ItemId CalciumId { get; } = new("pret:ITEM_CALCIUM");
    public static ItemId NetBallId { get; } = new("pret:ITEM_NET_BALL");
    public static ItemId NestBallId { get; } = new("pret:ITEM_NEST_BALL");
    public static ItemId Tm12Id { get; } = new("pret:ITEM_TM12");
    public static ItemId Tm21Id { get; } = new("pret:ITEM_TM21");
    public static ItemId Tm49Id { get; } = new("pret:ITEM_TM49");
    private sealed record Expected(ItemId Item, ImmutableArray<int> Effects);
    private static readonly Expected[] Additions = [
        new(new("pret:ITEM_LIFT_KEY"), []),
        new(new("pret:ITEM_SILPH_SCOPE"), []),
        new(new("pret:ITEM_BLACK_GLASSES"), []),
        new(new("pret:ITEM_X_SPEED"), [0,1,0,0,0,96,1,1]),
        new(new("pret:ITEM_CALCIUM"), [0,0,0,0,0,232,10,5,3,2]),
        new(new("pret:ITEM_NET_BALL"), []),
        new(new("pret:ITEM_NEST_BALL"), []),
        new(new("pret:ITEM_TM12"), []),
        new(new("pret:ITEM_TM21"), []),
        new(new("pret:ITEM_TM49"), []),
    ];
    public static void ValidateSourceItem(MachineItem metadata, ImmutableArray<int> effects)
    {
        var expected = Additions.FirstOrDefault(e => e.Item == metadata?.Item.Id);
        if (expected is null || string.IsNullOrWhiteSpace(metadata!.Description) || effects.IsDefault ||
            !effects.SequenceEqual(expected.Effects))
            throw new NotSupportedException("regional_item_definition");
    }
    public static void ValidateMachine(MachineItem machine)
    {
        if (machine?.Item.Id != Tm12Id && machine?.Item.Id != Tm21Id && machine?.Item.Id != Tm49Id)
            throw new NotSupportedException("Rocket Hideout machine identity differs.");
        ValidateSourceItem(machine!, []);
    }

    public static bool IsBerry(ItemId item) => CeladonGymItemRules.IsBerry(item);
    public static string? ValidateBerries(InventoryState state) => CeladonGymItemRules.ValidateBerries(state);
}
