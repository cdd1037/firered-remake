using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Read-only views of each owner's actual registers, never a synthetic singles state.</summary>
internal sealed class SourceTrainerAiFacts(SourceTrainerAiContext? source, SourceTrainerAiSlotContext? slots)
{
    internal PokemonCatalog Catalog => slots?.Catalog ?? source!.Catalog;
    internal WildBattleCombatant User => slots is not null ? slots.Frame.Get(slots.Actor).Body! : source!.Battle.Opponent;
    internal WildBattleCombatant Target => slots is not null ? slots.Frame.Get(slots.Target).Body! : source!.Battle.Player;
    internal bool Doubles => slots is not null;
    private SourceBattleSlot TargetCell => slots!.Frame.Get(slots.Target);
    private SourceBattleSlot UserCell => slots!.Frame.Get(slots.Actor);
    private SourceSideRegisters UserSide => slots!.Frame.Sides[(int)slots.Actor.Side];
    internal bool UserHasReserves => slots is not null ? slots.HasReserves(slots.Actor.Side)
        : source!.Party.Where((_, i) => i != source.State.OpponentIndex).Any(m => m.Pokemon.CurrentHp > 0);
    internal bool TargetHasReserves => slots is not null ? slots.HasReserves(slots.Target.Side)
        : !WildBattleSourcePartyEffects.LegalReserveIndices(source!.Battle.CalledParty!, WildBattleSide.Player).IsEmpty;
    internal bool FutureAttack => slots is not null ? TargetCell.Position.FutureAttack is not null : source!.Battle.CalledDelayed?.PlayerFutureAttack is not null;
    internal bool LightScreen => slots is not null ? UserSide.LightScreenTurnsRemaining > 0 : source!.Battle.CalledField?.OpponentLightScreenTurnsRemaining > 0;
    internal bool Mist => slots is not null ? UserSide.MistTurnsRemaining > 0 : source!.Battle.CalledField?.OpponentMistTurnsRemaining > 0;
    internal bool Safeguard => slots is not null ? UserSide.SafeguardTurnsRemaining > 0 : source!.Battle.CalledField?.OpponentSafeguardTurnsRemaining > 0;
    internal bool TargetTrapped => slots is not null ? TargetCell.Registers.MeanLookSource is not null : Target.MeanLookSource is not null;
    internal bool TargetSeeded => slots is not null ? TargetCell.Registers.LeechSeedSource is not null : Target.LeechSeedSource is not null;
    internal bool TargetAttracted => slots is not null ? TargetCell.Registers.InfatuationSource is not null : Target.InfatuationSource is not null;
    internal bool Spikes => slots is not null ? slots.Frame.Sides[(int)slots.Target.Side].Spikes > 0 : source!.Battle.CalledParty?.PlayerSpikes > 0;
    internal bool Sandstorm => slots is not null ? slots.Frame.Field.SandstormTurnsRemaining > 0 : source!.Battle.SandstormTurnsRemaining > 0;
    internal bool Sun => slots is not null ? slots.Frame.Field.SunTurnsRemaining > 0 : source!.Battle.CalledField?.SunTurnsRemaining > 0;
    internal bool Hail => slots is not null ? slots.Frame.Field.HailTurnsRemaining > 0 : source!.Battle.CalledField?.HailTurnsRemaining > 0;
    internal bool UsedHeldItem => slots is not null ? UserCell.Position.UsedHeldItem is not null : source!.Battle.CalledParty?.OpponentUsedHeldItem is not null;
    internal bool TargetLightScreen => slots is not null ? slots.Frame.Sides[(int)slots.Target.Side].LightScreenTurnsRemaining > 0 : source!.Battle.CalledField?.PlayerLightScreenTurnsRemaining > 0;
    internal bool TwoTargets => slots is not null && slots.Frame.Slots.Count(c => c.Slot.Side == slots.Target.Side && !slots.Frame.IsSourceAbsent(c.Slot)) == 2;
    internal bool HelpingHand => slots is not null && UserCell.Registers.HelpingHand;
}
