using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Read-only views of the shared owner's actual position and party registers.</summary>
internal sealed class SourceTrainerAiFacts(SourceTrainerAiSlotContext slots)
{
    internal PokemonCatalog Catalog => slots.Catalog;
    internal WildBattleCombatant User => UserCell.Body!;
    internal WildBattleCombatant Target => TargetCell.Body!;
    internal bool Doubles => slots.Doubles;
    private SourceBattleSlot TargetCell => slots.Frame.Get(slots.Target);
    private SourceBattleSlot UserCell => slots.Frame.Get(slots.Actor);
    private SourceSideRegisters UserSide => slots.Frame.Sides[(int)slots.Actor.Side];
    internal bool UserHasReserves => slots.HasReserves(slots.Actor.Side);
    internal bool TargetHasReserves => slots.HasReserves(slots.Target.Side);
    internal bool FutureAttack => TargetCell.Position.FutureAttack is not null;
    internal bool LightScreen => UserSide.LightScreenTurnsRemaining > 0;
    internal bool Mist => UserSide.MistTurnsRemaining > 0;
    internal bool Safeguard => UserSide.SafeguardTurnsRemaining > 0;
    internal bool TargetTrapped => TargetCell.Registers.MeanLookSource is not null;
    internal bool TargetSeeded => TargetCell.Registers.LeechSeedSource is not null;
    internal bool TargetAttracted => TargetCell.Registers.InfatuationSource is not null;
    internal bool Spikes => slots.Frame.Sides[(int)slots.Target.Side].Spikes > 0;
    internal bool Rain => SourceWeatherRules.HasRain(slots.Frame.Field);
    internal bool Sandstorm => SourceWeatherRules.HasSandstorm(slots.Frame.Field);
    internal bool Sun => SourceWeatherRules.HasSun(slots.Frame.Field);
    internal bool Hail => slots.Frame.Field.HailTurnsRemaining > 0;
    internal bool UsedHeldItem => UserCell.Position.UsedHeldItem is not null;
    internal bool TargetLightScreen => slots.Frame.Sides[(int)slots.Target.Side].LightScreenTurnsRemaining > 0;
    internal bool TwoTargets => slots.Frame.Slots.Count(c => c.Slot.Side == slots.Target.Side && !slots.Frame.IsSourceAbsent(c.Slot)) == 2;
    internal bool HelpingHand => UserCell.Registers.HelpingHand;

    internal int Turn => Math.Min(255, slots.Frame.Turn);
    internal MoveId CurrentMove => slots.StaleCurrentMove.Value is null ? SourceTrainerEncounterPolicy.NoMove : slots.StaleCurrentMove;
    internal ushort RandomTurnNumber => slots.Frame.RandomTurnNumber;
    internal bool WeatherSuppressed => SourceWeatherRules.IsSuppressed(slots.Frame);
    internal IEnumerable<WildBattleCalledPartyMember> Party(bool user) =>
        slots.Frame.Party(user ? slots.Actor.Side : slots.Target.Side).Members;
    internal int ReserveCount(bool user)
    {
        var side = user ? slots.Actor.Side : slots.Target.Side;
        return slots.Frame.Party(side).Members.Where((m, i) => !m.IsEgg && m.Pokemon.CurrentHp > 0 &&
            !slots.Frame.Slots.Any(c => c.Slot.Side == side && c.Member?.Index == i)).Count();
    }
    internal uint HoldEffect(bool user)
    {
        if (!user)
        {
            string known = slots.KnownTargetHoldEffect ?? "HOLD_EFFECT_NONE";
            if (!SourceTrainerAiScript.SupportsKnownHoldEffect(known)) throw new SourceTrainerAiScriptFault("source_trainer_ai_hold_effect_history");
            return SourceTrainerAiScript.Constant(known);
        }
        return SourceTrainerAiProgram.Items.TryGetValue(User.HeldItem, out var item) ? item.Effect
            : throw new SourceTrainerAiScriptFault("source_trainer_ai_held_item_unknown");
    }
    internal uint UsedItemLowByte(bool user)
    {
        string? name = (user ? UserCell : TargetCell).Position.UsedHeldItem;
        if (name is null) return 0;
        return SourceTrainerAiProgram.Items.TryGetValue(name, out var item) ? (byte)item.Id
            : throw new SourceTrainerAiScriptFault("source_trainer_ai_used_item_unknown");
    }
    internal uint Status2(bool user)
    {
        var mon = user ? User : Target;
        var cell = user ? UserCell : TargetCell;
        uint bits = 0;
        void Add(bool set, string flag) { if(set) bits |= SourceTrainerAiScript.Constant(flag); }
        Add(mon.ConfusionTurnsRemaining > 0,"STATUS2_CONFUSION");
        Add(mon.FocusEnergy,"STATUS2_FOCUS_ENERGY");
        Add(mon.CalledVolatile?.SubstituteHp > 0,"STATUS2_SUBSTITUTE");
        Add(cell.Registers.MeanLookSource is not null,"STATUS2_ESCAPE_PREVENTION");
        Add(cell.Registers.InfatuationSource is not null,"STATUS2_INFATUATION");
        Add(mon.Nightmare,"STATUS2_NIGHTMARE"); Add(mon.Cursed,"STATUS2_CURSED");
        Add(mon.Identified,"STATUS2_FORESIGHT"); Add(mon.CalledVolatile?.Tormented == true,"STATUS2_TORMENT");
        return bits;
    }
    internal uint Status3(bool user)
    {
        var mon = user ? User : Target;
        var cell = user ? UserCell : TargetCell;
        uint bits = 0;
        void Add(bool set, string flag) { if(set) bits |= SourceTrainerAiScript.Constant(flag); }
        Add(cell.Registers.LeechSeedSource is not null,"STATUS3_LEECHSEED");
        Add(mon.CalledVolatile?.PerishSongTimer is not null,"STATUS3_PERISH_SONG");
        Add(mon.Rooted,"STATUS3_ROOTED"); Add(mon.YawnTurnsRemaining > 0,"STATUS3_YAWN");
        Add(mon.Imprison,"STATUS3_IMPRISONED_OTHERS"); Add(mon.MudSport,"STATUS3_MUDSPORT"); Add(mon.WaterSport,"STATUS3_WATERSPORT");
        return bits;
    }
    internal uint SideStatus(bool user)
    {
        uint bits = 0;
        void Add(bool set, string flag) { if(set) bits |= SourceTrainerAiScript.Constant(flag); }
        var side = user ? slots.Actor.Side : slots.Target.Side;
        var r = slots.Frame.Sides[(int)side];
        Add(r.ReflectTurnsRemaining > 0,"SIDE_STATUS_REFLECT"); Add(r.LightScreenTurnsRemaining > 0,"SIDE_STATUS_LIGHTSCREEN");
        Add(r.MistTurnsRemaining > 0,"SIDE_STATUS_MIST"); Add(r.SafeguardTurnsRemaining > 0,"SIDE_STATUS_SAFEGUARD");
        Add(r.Spikes > 0,"SIDE_STATUS_SPIKES");
        // Source gSideStatuses marks the side when either position has a delayed attack.
        Add(slots.Frame.Slots.Any(c => c.Slot.Side == side && c.Position.FutureAttack is not null),"SIDE_STATUS_FUTUREATTACK");
        return bits;
    }
}
