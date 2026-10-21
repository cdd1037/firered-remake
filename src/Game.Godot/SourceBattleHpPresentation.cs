using Game.Domain.Battle;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

/// <summary>Battle HUD values; receipt HP spans use event-time identity and callers own animation timing.</summary>
internal static class SourceBattleHpPresentation
{
    internal static int ExperienceBar(PokemonCatalog catalog, SpeciesId species, int level, long experience)
    {
        var growth = catalog.Species[species].GrowthRate;
        uint minimum = GrowthRules.ExperienceAtLevel(growth, level), maximum = GrowthRules.ExperienceAtLevel(growth, Math.Min(100, level + 1));
        // Clamp before arithmetic so malformed long inputs cannot overflow the settled HUD width.
        if (maximum == minimum || experience <= minimum) return 0;
        if (experience >= maximum) return 64;
        // pret/pokefirered community decompilation 037335f4c725d7c9aecdac87066f2002b4bd7e14,
        // src/battle_interface.c:2057-2060: positive settled progress retains one visible pixel.
        return Math.Max(1, (int)((experience - minimum) * 64 / (maximum - minimum)));
    }

    internal static bool ChangesHp(SourceBattleEvent? current)=>current is SourceHpChangedEvent or SourceRecoilEvent or SourceConfusionSelfHitEvent or
        SourceStatusDamageEvent or SourceWeatherDamageEvent or SourceAbilityDamageEvent or SourceRecoveredEvent or SourceDrainEvent or SourceMedicineAppliedEvent or
        SourcePotionRecoveredEvent or SourceHeldItemHealEvent or SourceHeldItemConsumedEvent or SourceAbilityRecoveredEvent or SourceBindingDamageEvent or
        SourceLeechSeedDrainEvent or SourceIngrainHealEvent or SourceTowerHpEvent or SourceSelfDestructEvent;
    internal static (int Before,int After)? Span(SourceBattleEvent? current,BattlerSlot slot,PartyMemberKey member) => current switch {
            SourceHpChangedEvent x when x.Target==slot=>(x.HpBefore,x.HpAfter),
            SourceRecoilEvent x when x.Actor==slot=>(x.HpBefore,x.HpAfter),
            SourceConfusionSelfHitEvent x when x.Side==slot=>(x.HpBefore,x.HpAfter),
            SourceStatusDamageEvent x when x.Side==slot=>(x.HpBefore,x.HpAfter),
            SourceWeatherDamageEvent x when x.Side==slot=>(x.HpBefore,x.HpAfter),
            SourceAbilityDamageEvent x when x.Target==slot=>(x.HpBefore,x.HpAfter),
            SourceRecoveredEvent x when x.Side==slot=>(x.HpBefore,x.HpAfter),
            SourceDrainEvent x when x.Actor==slot=>(x.HpBefore,x.HpAfter),
            SourcePotionRecoveredEvent x when x.Side==slot=>(x.HpBefore,x.HpAfter),
            SourceHeldItemHealEvent x when x.Actor==slot=>(x.HpBefore,x.HpAfter),
            SourceHeldItemConsumedEvent x when x.Side==slot=>(x.HpBefore,x.HpAfter),
            SourceAbilityRecoveredEvent x when x.Side==slot=>(x.HpBefore,x.HpAfter),
            SourceBindingDamageEvent x when x.Side==slot=>(x.HpBefore,x.HpAfter),
            SourceLeechSeedDrainEvent x when x.DrainedSide==slot=>(x.HpBefore,x.HpAfter),
            SourceLeechSeedDrainEvent x when x.ReceivingSide==slot=>(x.ReceiverHpBefore,x.ReceiverHpAfter),
            SourceIngrainHealEvent x when x.Side==slot=>(x.HpBefore,x.HpAfter),
            SourceTowerHpEvent x when x.Side==slot=>(x.HpBefore,x.HpAfter),
            SourceSelfDestructEvent x when x.Actor==slot=>(x.HpBefore,0),
            SourceMedicineAppliedEvent x when member==new PartyMemberKey(x.TargetSide??BattleSide.Player,x.PartyIndex)=>(x.HpBefore,x.HpAfter),
            _=>null};
}
