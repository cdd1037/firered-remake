using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Items;

public sealed record SourceBattleItemSelection(ItemId Item, long TargetInstanceId, int? MoveSlot = null);
public sealed record SourceItemFriendshipContext(string Ball, string MetLocation, string CurrentMapSection);
public sealed record SourceBattleItemEffectResult(SourceBattleFrame State, bool Changed, string? Fault)
{
    public bool Succeeded => Fault is null;
}

/// <summary>The fixed source table effects over permanent parties and live positions. No inventory,
/// choice, RNG or turn owner. Player menu and trainer action owners decide when to commit.</summary>
public static class SourceBattleItemEffects
{
    public static SourceBattleItemEffectResult Apply(SourceBattleItemCatalog items, SourceBattleFrame frame,
        OccupantToken actor, PartyMemberKey target, ItemId item, int? moveSlot = null,
        SourceItemFriendshipContext? friendship = null)
    {
        var definition = items.Find(item);
        if (definition is null) return new(frame, false, "source_item_effect_unsupported");
        return ApplyDefinition(frame, actor, target, definition, moveSlot, friendship);
    }

    internal static SourceBattleItemEffectResult ApplyDefinition(SourceBattleFrame frame, OccupantToken actor,
        PartyMemberKey target, SourceBattleItemDefinition definition, int? moveSlot = null, SourceItemFriendshipContext? friendship = null)
    {
        SourceBattleItemEffectResult Fail(string error) => new(frame, false, error);
        if (!SourceBattleItemDefinitionProof.IsAuthentic(definition)) return Fail("source_item_definition_fingerprint");
        if (SourceBattleIdentityRules.Validate(frame) is { } invalid) return Fail(invalid);
        if (!frame.IsCurrent(actor) || !frame.CanAct(actor.Slot) || target.Side != actor.Slot.Side ||
            target.Index >= frame.Party(target.Side).Members.Length) return Fail("source_item_target");
        if (definition.IsStatBooster && target != actor.Member) return Fail("source_item_stat_target");
        var member = frame.Party(target.Side).Members[target.Index];
        if (member.IsEgg) return Fail("source_item_egg");
        var effect = definition.EffectBytes;
        bool onePp = (effect[4] & 0x10) != 0;
        if (onePp ? moveSlot is null || moveSlot < 0 || moveSlot >= member.Pokemon.Moves.Length : moveSlot is not null)
            return Fail("source_item_move_slot");
        // Player stat items change friendship after a successful effect. These bonuses need actual met data.
        if (definition.IsStatBooster && actor.Slot.Side == BattleSide.Player && friendship is null)
            return Fail("source_item_friendship_context");
        var cell = frame.Slots.FirstOrDefault(s => s.Member == target);
        var body = cell?.Body;
        var actorCell = frame.Get(actor.Slot); var actorBody = actorCell.Body!;
        var pokemon = member.Pokemon; uint status = member.Status;
        var next = frame; bool changed = false; int arg = 6;
        if ((effect[0] & 0x80) != 0 && cell?.Registers.InfatuationSource is not null)
        {
            cell = cell with { Registers = cell.Registers with { InfatuationSource = null } }; changed = true;
        }
        if ((effect[0] & 0x30) != 0 && !actorBody.FocusEnergy) { actorBody = actorBody with { FocusEnergy = true }; changed = true; }
        int Boost(int stage, int amount) { if (amount == 0 || stage == 12) return stage; changed = true; return Math.Min(12, stage + amount); }
        actorBody = actorBody with { AttackStage = Boost(actorBody.AttackStage, effect[0] & 15),
            DefenseStage = Boost(actorBody.DefenseStage, effect[1] >> 4), SpeedStage = Boost(actorBody.SpeedStage, effect[1] & 15),
            AccuracyStage = Boost(actorBody.AccuracyStage, effect[2] >> 4),
            SpecialAttackStageDelta = Boost(actorBody.SpecialAttackStageDelta + 6, effect[2] & 15) - 6 };
        if ((effect[3] & 0x80) != 0 && frame.Sides[(int)actor.Slot.Side].MistTurnsRemaining == 0)
        {
            next = next with { Sides = next.Sides.SetItem((int)actor.Slot.Side, next.Sides[(int)actor.Slot.Side] with { MistTurnsRemaining = 5 }),
                SinglesEncoding = next.SinglesEncoding is { } encoding ? encoding with { FieldRulesVersion = WildBattleRules.CalledMoveKernelRulesVersion } : null };
            changed = true;
        }
        if (cell?.Slot == actor.Slot) body = actorBody;
        uint mask = 0;
        if ((effect[3] & 0x20) != 0) mask |= 7;
        if ((effect[3] & 0x10) != 0) mask |= 0xf88;
        if ((effect[3] & 8) != 0) mask |= WildBattleStatus.Burn;
        if ((effect[3] & 4) != 0) mask |= WildBattleStatus.Freeze;
        if ((effect[3] & 2) != 0) mask |= WildBattleStatus.Paralysis;
        if ((status & mask) != 0)
        {
            bool woke = (status & mask & 7) != 0; status &= ~mask; changed = true;
            if (body is not null) body = body with { Status = body.Status & ~mask,
                Nightmare = woke ? false : body.Nightmare,
                SourcePartyStatusOverride = null, Transform = body.Transform is { } form ? form with { PartyStatusOverride = null } : null };
        }
        if ((effect[3] & 1) != 0 && body?.ConfusionTurnsRemaining > 0)
        { body = body with { ConfusionTurnsRemaining = 0 }; changed = true; }
        bool revive = (effect[4] & 0x40) != 0;
        if ((effect[4] & 4) != 0)
        {
            int amount = effect[arg++];
            if ((revive ? pokemon.CurrentHp == 0 : pokemon.CurrentHp != 0) && pokemon.CurrentHp != pokemon.Stats.MaxHp)
            {
                int restored = amount switch { 255 => pokemon.Stats.MaxHp - pokemon.CurrentHp, 254 => Math.Max(1, pokemon.Stats.MaxHp / 2), _ => amount };
                pokemon = pokemon with { CurrentHp = Math.Min(pokemon.Stats.MaxHp, pokemon.CurrentHp + restored) }; changed = true;
                if (revive)
                {
                    if (cell is not null)
                    {
                        // CopyPlayerPartyMonToBattleData restores permanent species/moves/types, stat stages and status2.
                        body = new(pokemon, HeldItem: member.HeldItem, Status: status) { KnockedOffItem = member.KnockedOffItem,
                            AttackBadgeBoost = body!.AttackBadgeBoost, DefenseBadgeBoost = body.DefenseBadgeBoost, SpecialBadgeBoost = body.SpecialBadgeBoost, SpeedBadgeBoost = body.SpeedBadgeBoost };
                        cell = cell with { Registers = new() };
                        next = next with { SourceAbsentMask = (byte)(next.SourceAbsentMask & ~cell.Slot.Mask) };
                    }
                    else if (frame.Layout == SourceBattleLayout.PresetDoubles)
                        next = next with { SourceAbsentMask = (byte)(next.SourceAbsentMask & ~(1 << (actor.Slot.Value ^ 2))) };
                }
            }
        }
        if ((effect[4] & 8) != 0)
        {
            int amount = effect[arg++]; var moves = pokemon.Moves;
            for (int i = 0; i < moves.Length; i++)
            {
                if (onePp && i != moveSlot) continue;
                var move = moves[i]; int pp = Math.Min(move.MaximumPp, move.CurrentPp + amount);
                if (pp != move.CurrentPp) { moves = moves.SetItem(i, move with { CurrentPp = pp }); changed = true; }
            }
            pokemon = pokemon with { Moves = moves };
        }
        if (!changed) return Fail("source_item_no_effect");
        // Trainer scripts do not execute the player's friendship tail. Bitter medicine never needs location bonuses.
        if (actor.Slot.Side == BattleSide.Player)
        {
            int beforeFriendship = pokemon.Friendship; int delta = 0;
            for (int band = 0; band < 3; band++) if ((effect[5] & (0x20 << band)) != 0)
            {
                int amount = unchecked((sbyte)effect[arg++]);
                if (band == (beforeFriendship < 100 ? 0 : beforeFriendship < 200 ? 1 : 2)) delta = amount;
            }
            int adjustment = delta;
            if (delta > 0)
            {
                if (WildBattleSourcePartyEffects.PersistentHeldItem(member) == "ITEM_SOOTHE_BELL") adjustment = 150 * delta / 100;
                if (friendship!.Ball == "ITEM_LUXURY_BALL") adjustment++;
                if (friendship.MetLocation == friendship.CurrentMapSection) adjustment++;
            }
            pokemon = pokemon with { Friendship = Math.Clamp(beforeFriendship + adjustment, 0, 255) };
        }
        next = next with { Parties = next.Parties.SetItem((int)target.Side, next.Party(target.Side) with {
            Members = next.Party(target.Side).Members.SetItem(target.Index, member with { Pokemon = pokemon, Status = status }) }) };
        if (cell is not null)
        {
            body = WildBattleTransformationRules.ApplyPersistentPokemon(body!, pokemon);
            // Preserve the rare Poké Flute live/party status divergence after unrelated medicine.
            uint canonicalLive = SourcePartyStatusRules.FromBattle(body.Status);
            body = body with { SourcePartyStatusOverride = status == canonicalLive ? null : status,
                Transform = body.Transform is { } form ? form with { PartyStatusOverride = null } : null };
            next = next with { Slots = next.Slots.SetItem(cell.Slot.Value, cell with { Body = body }) };
        }
        if (cell?.Slot != actor.Slot) next = next with { Slots = next.Slots.SetItem(actor.Slot.Value, actorCell with { Body = actorBody }) };
        return new(next, true, null);
    }

    internal static SourceFourActionResult PublishChanges(SourceBattleFrame before, SourceBattleFrame after, OccupantToken actor,
        PartyMemberKey target, ItemId item, long instanceId) => SourceBattleItemEventRules.Publish(before,after,actor,target,item,instanceId);

    internal static ImmutableArray<int> PokeFluteWakeIndices(ImmutableArray<WildBattleCalledPartyMember> members) =>
        Enumerable.Range(0,members.Length).Where(i=>!members[i].IsEgg && WildBattleStatus.IsSleeping(members[i].Status) &&
            members[i].Pokemon.AbilityId != "ABILITY_SOUNDPROOF").ToImmutableArray();
    internal static WildBattleCombatant ClearPokeFluteBody(WildBattleCombatant body, uint permanentStatus)
    {
        bool soundproof = WildBattleEffective.Ability(body) == "ABILITY_SOUNDPROOF";
        uint status = soundproof ? body.Status : body.Status & ~7u;
        return body with { Status = status, Nightmare = soundproof && body.Nightmare,
            SourcePartyStatusOverride = permanentStatus == SourcePartyStatusRules.FromBattle(status) ? null : permanentStatus,
            Transform = body.Transform is { } form ? form with { PartyStatusOverride = null } : null };
    }

    /// <summary>BattleScript_PokeFlute tests permanent party abilities and live abilities independently.</summary>
    public static SourceFourActionResult ApplyPokeFlute(SourceBattleFrame frame)
    {
        var next = frame; var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        ImmutableArray<int>[] awakened = [[], []];
        for (int s = 0; s < 2; s++)
        {
            var party = next.Parties[s]; var members = party.Members;
            awakened[s] = PokeFluteWakeIndices(members);
            foreach (int i in awakened[s]) members = members.SetItem(i, members[i] with { Status = members[i].Status & ~7u });
            next = next with { Parties = next.Parties.SetItem(s, party with { Members = members }) };
        }
        foreach (var cell in next.Slots.Where(s => s.Body is not null))
        {
            uint permanent = next.Party(cell.Member!.Value.Side).Members[cell.Member.Value.Index].Status;
            var body = ClearPokeFluteBody(cell.Body!, permanent);
            next = next with { Slots = next.Slots.SetItem(cell.Slot.Value, cell with { Body = body }) };
        }
        var phases = awakened.All(x => x.IsEmpty) ? new[] { PokeFluteBattlePhase.NoEffect } :
            new[] { PokeFluteBattlePhase.Played, PokeFluteBattlePhase.Fanfare, PokeFluteBattlePhase.Awakened };
        foreach (var phase in phases)
        {
            if (phase is PokeFluteBattlePhase.Awakened or PokeFluteBattlePhase.NoEffect)
                foreach (var cell in next.Slots.Where(s => s.Body is not null))
                {
                    uint beforeStatus = frame.Get(cell.Slot).Body!.Status, afterStatus = cell.Body!.Status;
                    if (beforeStatus == afterStatus) continue;
                    var statusChanged = SourceBattleEventHooks.Publish(next, (n,t) =>
                        new SourceStatusChangedEvent(n,t,cell.Slot,beforeStatus,afterStatus,"poke-flute"));
                    next = statusChanged.State; events.Add(statusChanged.Event);
                }
            var published = SourceBattleEventHooks.Publish(next, (n,t) => new SourcePokeFluteEvent(n,t,phase,awakened[0],awakened[1]));
            next = published.State; events.Add(published.Event);
        }
        return new(next, events.ToImmutable(), [], null);
    }
}
