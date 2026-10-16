using Game.Domain.Ids;

namespace Game.Domain.Items;

public enum FieldMedicineEffect { Unsupported, HealHp, CureStatus, CureSleep, Revive, RestoreMovePp, RestoreAllPp }

/// <summary>Resolves only capabilities already admitted by an authenticated inventory definition.
/// New regional profiles inherit these capabilities without another list of profile names.</summary>
public static class AuthenticatedFieldMedicine
{
    public static (EarlyShopItemDefinition? Definition, FieldMedicineEffect Effect) Resolve(EarlyShopCatalog catalog, ItemId item)
    {
        EarlyShopInventoryRules.ValidateCatalog(catalog);
        var definition = EarlyShopInventoryRules.Find(catalog, item);
        var effect = definition switch
        {
            { UseSupport: EarlyShopUseSupport.Potion, HealHp: > 0 } => FieldMedicineEffect.HealHp,
            { UseSupport: EarlyShopUseSupport.StatusCure, CureStatus: not 0 } => FieldMedicineEffect.CureStatus,
            { UseSupport: EarlyShopUseSupport.DeferredSleep } => FieldMedicineEffect.CureSleep,
            { UseSupport: EarlyShopUseSupport.FieldRevive } => FieldMedicineEffect.Revive,
            { UseSupport: EarlyShopUseSupport.FieldEther } => FieldMedicineEffect.RestoreMovePp,
            { UseSupport: EarlyShopUseSupport.FieldRestoreAllPp } allPp
                when allPp.EffectBytes.SequenceEqual(new[] { 0, 0, 0, 0, 8, 0, 127 }) => FieldMedicineEffect.RestoreAllPp,
            _ => FieldMedicineEffect.Unsupported
        };
        return (definition, effect);
    }
}
