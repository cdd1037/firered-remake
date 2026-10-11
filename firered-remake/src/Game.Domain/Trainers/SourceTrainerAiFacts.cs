using Game.Domain.Battle;
using Game.Domain.Ids;
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
    internal bool Rain => slots is not null ? SourceWeatherRules.HasRain(slots.Frame.Field) : SourceWeatherRules.HasRain(source!.Battle);
    internal bool Sandstorm => slots is not null ? SourceWeatherRules.HasSandstorm(slots.Frame.Field) : SourceWeatherRules.HasSandstorm(source!.Battle);
    internal bool Sun => slots is not null ? SourceWeatherRules.HasSun(slots.Frame.Field) : SourceWeatherRules.HasSun(source!.Battle);
    internal bool Hail => slots is not null ? slots.Frame.Field.HailTurnsRemaining > 0 : source!.Battle.CalledField?.HailTurnsRemaining > 0;
    internal bool UsedHeldItem => slots is not null ? UserCell.Position.UsedHeldItem is not null : source!.Battle.CalledParty?.OpponentUsedHeldItem is not null;
    internal bool TargetLightScreen => slots is not null ? slots.Frame.Sides[(int)slots.Target.Side].LightScreenTurnsRemaining > 0 : source!.Battle.CalledField?.PlayerLightScreenTurnsRemaining > 0;
    internal bool TwoTargets => slots is not null && slots.Frame.Slots.Count(c => c.Slot.Side == slots.Target.Side && !slots.Frame.IsSourceAbsent(c.Slot)) == 2;
    internal bool HelpingHand => slots is not null && UserCell.Registers.HelpingHand;

    internal int Turn => Math.Min(255, slots?.Frame.Turn ?? source!.Battle.Turn);
    internal MoveId CurrentMove => slots is null ? source!.State.StaleCurrentMove :
        slots.StaleCurrentMove.Value is null ? ForestTrainerCombatRules.NoMove : slots.StaleCurrentMove;
    internal ushort RandomTurnNumber => slots?.Frame.RandomTurnNumber ?? source!.Battle.RandomTurnNumber;
    internal bool WeatherSuppressed => slots is not null ? SourceWeatherRules.IsSuppressed(slots.Frame)
        : SourceWeatherRules.IsSuppressed(new[] { User, Target });
    internal IEnumerable<WildBattleCalledPartyMember> Party(bool user) => slots is not null
        ? slots.Frame.Party(user ? slots.Actor.Side : slots.Target.Side).Members
        : user ? source!.Battle.CalledParty!.Opponent : source!.Battle.CalledParty!.Player;
    internal int ReserveCount(bool user)
    {
        if (slots is not null)
        {
            var side = user ? slots.Actor.Side : slots.Target.Side;
            return slots.Frame.Party(side).Members.Where((m,i) => !m.IsEgg && m.Pokemon.CurrentHp > 0 &&
                !slots.Frame.Slots.Any(c => c.Slot.Side == side && c.Member?.Index == i)).Count();
        }
        int active = user ? source!.Battle.CalledParty!.OpponentActiveIndex : source!.Battle.CalledParty!.PlayerActiveIndex;
        return Party(user).Where((m,i) => i != active && !m.IsEgg && m.Pokemon.CurrentHp > 0).Count();
    }
    internal uint HoldEffect(bool user)
    {
        if (!user)
        {
            string known = slots?.KnownTargetHoldEffect ?? "HOLD_EFFECT_NONE";
            if (!SourceTrainerAiScript.SupportsKnownHoldEffect(known)) throw new SourceTrainerAiScriptFault("source_trainer_ai_hold_effect_history");
            return SourceTrainerAiScript.Constant(known);
        }
        return SourceTrainerAiProgram.Items.TryGetValue(User.HeldItem, out var item) ? item.Effect
            : throw new SourceTrainerAiScriptFault("source_trainer_ai_held_item_unknown");
    }
    internal uint UsedItemLowByte(bool user)
    {
        string? name = slots is not null ? (user ? UserCell : TargetCell).Position.UsedHeldItem
            : user ? source!.Battle.CalledParty?.OpponentUsedHeldItem : source!.Battle.CalledParty?.PlayerUsedHeldItem;
        if (name is null) return 0;
        return SourceTrainerAiProgram.Items.TryGetValue(name, out var item) ? (byte)item.Id
            : throw new SourceTrainerAiScriptFault("source_trainer_ai_used_item_unknown");
    }
    internal uint Status2(bool user)
    {
        var mon = user ? User : Target;
        var cell = slots is null ? null : user ? UserCell : TargetCell;
        uint bits = 0;
        void Add(bool set, string flag) { if(set) bits |= SourceTrainerAiScript.Constant(flag); }
        Add(mon.ConfusionTurnsRemaining > 0,"STATUS2_CONFUSION");
        Add(mon.FocusEnergy,"STATUS2_FOCUS_ENERGY");
        Add(mon.CalledVolatile?.SubstituteHp > 0,"STATUS2_SUBSTITUTE");
        Add(cell is not null ? cell.Registers.MeanLookSource is not null : mon.MeanLookSource is not null,"STATUS2_ESCAPE_PREVENTION");
        Add(cell is not null ? cell.Registers.InfatuationSource is not null : mon.InfatuationSource is not null,"STATUS2_INFATUATION");
        Add(mon.Nightmare,"STATUS2_NIGHTMARE"); Add(mon.Cursed,"STATUS2_CURSED");
        Add(mon.Identified,"STATUS2_FORESIGHT"); Add(mon.CalledVolatile?.Tormented == true,"STATUS2_TORMENT");
        return bits;
    }
    internal uint Status3(bool user)
    {
        var mon = user ? User : Target;
        var cell = slots is null ? null : user ? UserCell : TargetCell;
        uint bits = 0;
        void Add(bool set, string flag) { if(set) bits |= SourceTrainerAiScript.Constant(flag); }
        Add(cell is not null ? cell.Registers.LeechSeedSource is not null : mon.LeechSeedSource is not null,"STATUS3_LEECHSEED");
        Add(mon.CalledVolatile?.PerishSongTimer is not null,"STATUS3_PERISH_SONG");
        Add(mon.Rooted,"STATUS3_ROOTED"); Add(mon.YawnTurnsRemaining > 0,"STATUS3_YAWN");
        Add(mon.Imprison,"STATUS3_IMPRISONED_OTHERS"); Add(mon.MudSport,"STATUS3_MUDSPORT"); Add(mon.WaterSport,"STATUS3_WATERSPORT");
        return bits;
    }
    internal uint SideStatus(bool user)
    {
        uint bits = 0;
        void Add(bool set, string flag) { if(set) bits |= SourceTrainerAiScript.Constant(flag); }
        if (slots is not null)
        {
            var side = user ? slots.Actor.Side : slots.Target.Side;
            var r = slots.Frame.Sides[(int)side];
            Add(r.ReflectTurnsRemaining > 0,"SIDE_STATUS_REFLECT"); Add(r.LightScreenTurnsRemaining > 0,"SIDE_STATUS_LIGHTSCREEN");
            Add(r.MistTurnsRemaining > 0,"SIDE_STATUS_MIST"); Add(r.SafeguardTurnsRemaining > 0,"SIDE_STATUS_SAFEGUARD");
            Add(r.Spikes > 0,"SIDE_STATUS_SPIKES");
            // Source gSideStatuses marks the side when either position has a delayed attack.
            Add(slots.Frame.Slots.Any(c => c.Slot.Side == side && c.Position.FutureAttack is not null),"SIDE_STATUS_FUTUREATTACK");
        }
        else
        {
            var b = source!.Battle; var f = b.CalledField; var p = b.CalledParty;
            Add((user ? b.OpponentReflectTurnsRemaining : b.PlayerReflectTurnsRemaining) > 0,"SIDE_STATUS_REFLECT");
            Add((user ? f?.OpponentLightScreenTurnsRemaining : f?.PlayerLightScreenTurnsRemaining) > 0,"SIDE_STATUS_LIGHTSCREEN");
            Add((user ? f?.OpponentMistTurnsRemaining : f?.PlayerMistTurnsRemaining) > 0,"SIDE_STATUS_MIST");
            Add((user ? f?.OpponentSafeguardTurnsRemaining : f?.PlayerSafeguardTurnsRemaining) > 0,"SIDE_STATUS_SAFEGUARD");
            Add((user ? p?.OpponentSpikes : p?.PlayerSpikes) > 0,"SIDE_STATUS_SPIKES");
            Add((user ? b.CalledDelayed?.OpponentFutureAttack : b.CalledDelayed?.PlayerFutureAttack) is not null,"SIDE_STATUS_FUTUREATTACK");
        }
        return bits;
    }
}
