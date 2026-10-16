using System.Buffers.Binary;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

internal sealed class SourceTrainerAiScriptFault(string code) : Exception(code);

/// <summary>The original flags 1/7 bytecode, over read-only source registers. Setup, damage
/// prediction and final selection remain in the shared scorer. funcResult survives all slots
/// and script passes, including the original clear-weather and target move-history bugs.</summary>
internal static class SourceTrainerAiScript
{
    internal static bool SupportsKnownHoldEffect(string value) => value.StartsWith("HOLD_EFFECT_", StringComparison.Ordinal) &&
        SourceTrainerAiProgram.Constants.ContainsKey(value);
    internal static uint Constant(string name) => SourceTrainerAiProgram.Constants.TryGetValue(name, out uint value)
        ? value : throw new SourceTrainerAiScriptFault("source_trainer_ai_unknown_constant");

    internal static MoveId Score(SourceTrainerAiFacts facts, uint flags, int[] scores, int[] predictions,
        Func<int, bool> process, Func<string, int, int> draw, Func<int, string, string> targetAbility)
    {
        var code = SourceTrainerAiProgram.Code;
        var catalog = facts.Catalog;
        var user = facts.User; var target = facts.Target;
        _ = facts.HoldEffect(false); // Validate observed history even if this moveset never reads it.
        uint result = 0;
        MoveId currentMove = facts.CurrentMove;
        // Approved BUG-05/06 corrections are limited to three operands in the
        // SHA-256-authenticated original pack. Keep its source bytes unchanged.
        int U8(int at) => at switch {
            6845 => 1, // AI_CV_Facade: read AI_USER status instead of AI_TARGET.
            6691 => 2, // AI_CV_SemiInvulnerable2: sandstorm -> Ground/Rock/Steel.
            6697 => 3, // AI_CV_SemiInvulnerable2: hail -> Ice.
            _ => code[at]
        };
        uint U32(int at) => BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(at, 4));
        int Pointer(int at) => checked((int)U32(at));
        int U16(int at) => BinaryPrimitives.ReadUInt16LittleEndian(code.AsSpan(at, 2));
        WildBattleCombatant Mon(int who) => who == 1 ? user : target;
        int Hp(int who) => 100 * Mon(who).Pokemon.CurrentHp / Mon(who).Pokemon.Stats.MaxHp;
        uint Type(int who, bool second)
        {
            var t = WildBattleEffective.Types(catalog, Mon(who));
            return Constant(second ? t.Type2 : t.Type1);
        }
        uint Stage(int who, int stat) => (uint)(stat switch {
            1 => Mon(who).AttackStage, 2 => Mon(who).DefenseStage, 3 => Mon(who).SpeedStage,
            4 => Mon(who).SpecialAttackStageDelta + 6, 5 => Mon(who).SpecialDefenseStageDelta + 6,
            6 => Mon(who).AccuracyStage, 7 => Mon(who).EvasionStage,
            _ => throw new SourceTrainerAiScriptFault("source_trainer_ai_stat_operand") });
        uint LastMove(int who)
        {
            var id = Mon(who).LastUsedMove;
            if (id is null || id.Value.Value == "pret:MOVE_NONE") return 0;
            if (id.Value.Value == "pret:MOVE_UNAVAILABLE") return 65535;
            return catalog.Moves.TryGetValue(id.Value, out var move) ? (uint)move.SourceMoveId
                : throw new SourceTrainerAiScriptFault("source_trainer_ai_last_move_unknown");
        }
        uint MoveField(uint id, int field)
        {
            // Cancellation legitimately records MOVE_UNAVAILABLE. The original AI indexes
            // gBattleMoves[0xffff]; define that metadata read as MOVE_NONE while preserving
            // the sentinel in battle history for actual move rules. No RNG is consumed.
            if (id is 0 or 65535) return 0;
            var m = catalog.Moves.Values.FirstOrDefault(x => x.SourceMoveId == id);
            if (m is null) throw new SourceTrainerAiScriptFault("source_trainer_ai_last_move_metadata_unavailable");
            return field switch { 0 => Constant(m.Type), 1 => (uint)m.Power, 2 => Constant(m.Effect),
                _ => throw new SourceTrainerAiScriptFault("source_trainer_ai_move_field") };
        }
        bool InTable(int table, int width)
        {
            for (int at = table; at < code.Length; at += width)
            {
                uint v = width == 1 ? (uint)U8(at) : (uint)U16(at);
                if (v == (width == 1 ? 255u : 65535u)) return false;
                if (v == result) return true;
            }
            throw new SourceTrainerAiScriptFault("source_trainer_ai_table_terminator");
        }
        int StrikesFirst(int slot, int pc)
        {
            uint Speed(WildBattleCombatant mon, bool player) => SourceBattleTurnOrder.EffectiveSpeed(mon,
                !facts.WeatherSuppressed && (WildBattleEffective.Ability(mon) == "ABILITY_SWIFT_SWIM" && facts.Rain ||
                    WildBattleEffective.Ability(mon) == "ABILITY_CHLOROPHYLL" && facts.Sun), player, facts.RandomTurnNumber);
            uint a = Speed(user, false), b = Speed(target, true);
            return a == b ? draw($"source-trainer.ai.slot.{slot}.command.{pc}.speed-tie", 2) != 0 ? 2 : 0 : a < b ? 1 : 0;
        }
        foreach (int entry in flags == 1 ? new[] { SourceTrainerAiProgram.AI_CheckBadMove } :
            new[] { SourceTrainerAiProgram.AI_CheckBadMove, SourceTrainerAiProgram.AI_CheckViability, SourceTrainerAiProgram.AI_TryToFaint })
        for (int slot = 0; slot < 4; slot++)
        {
            if (!process(slot)) { scores[slot] = 0; continue; }
            var move = catalog.Moves[user.Pokemon.Moves[slot].Move];
            int pc = entry, count = 0;
            uint PowerRank()
            {
                if (move.Power < 2 || SourceTrainerAi.DiscouragedPower(move.Effect)) return 0;
                foreach (var m in user.Pokemon.Moves)
                    if (catalog.Moves[m.Move].Power > 1 && !SourceTrainerAi.DiscouragedPower(catalog.Moves[m.Move].Effect)) currentMove = m.Move;
                return user.Pokemon.Moves.Select((m,i) => (m,i)).Any(x =>
                    catalog.Moves[x.m.Move].Power > 1 && !SourceTrainerAi.DiscouragedPower(catalog.Moves[x.m.Move].Effect) &&
                    predictions[x.i] > predictions[slot]) ? 1u : 2u;
            }
            int Effectiveness()
            {
                currentMove = move.Id;
                // TypeCalc's returned immunity flags are discarded by this original command;
                // Levitate/Wonder Guard therefore retain the source nonzero damage quirk.
                int value = CeruleanTrainerAi.TypePrediction(catalog, user, target, move, 40);
                return (byte)(value switch { 120 => 80, 240 => 160, 30 => 20, 15 => 10, _ => value });
            }
            void Branch(bool condition, int pointerOffset, int width) => pc = condition ? Pointer(pc + pointerOffset) : pc + width;
            bool done = false;
            while (!done)
            {
                if (pc < 0 || pc >= code.Length || ++count > 2048)
                    throw new SourceTrainerAiScriptFault("source_trainer_ai_program_boundary");
                int op = U8(pc);
                switch (op)
                {
                    case 0x00: Branch(draw($"source-trainer.ai.slot.{slot}.command.{pc}.random",256) < U8(pc+1),2,6); break;
                    case 0x04: scores[slot] = Math.Max(0, (int)unchecked((sbyte)(scores[slot] + U8(pc+1)))); pc += 2; break;
                    case 0x05: Branch(Hp(U8(pc+1)) < U8(pc+2),3,7); break;
                    case 0x06: Branch(Hp(U8(pc+1)) > U8(pc+2),3,7); break;
                    case 0x07: Branch(Hp(U8(pc+1)) == U8(pc+2),3,7); break;
                    case 0x08: Branch(Hp(U8(pc+1)) != U8(pc+2),3,7); break;
                    case 0x09: Branch((Mon(U8(pc+1)).Status & U32(pc+2)) != 0,6,10); break;
                    case 0x0a: Branch((Mon(U8(pc+1)).Status & U32(pc+2)) == 0,6,10); break;
                    case 0x0b: Branch((facts.Status2(U8(pc+1)==1) & U32(pc+2)) != 0,6,10); break;
                    case 0x0c: Branch((facts.Status2(U8(pc+1)==1) & U32(pc+2)) == 0,6,10); break;
                    case 0x0d: Branch((facts.Status3(U8(pc+1)==1) & U32(pc+2)) != 0,6,10); break;
                    case 0x0e: Branch((facts.Status3(U8(pc+1)==1) & U32(pc+2)) == 0,6,10); break;
                    case 0x0f: Branch((facts.SideStatus(U8(pc+1)==1) & U32(pc+2)) != 0,6,10); break;
                    case 0x11: Branch(result < U8(pc+1),2,6); break;
                    case 0x12: Branch(result > U8(pc+1),2,6); break;
                    case 0x13: case 0x26: Branch(result == U8(pc+1),2,6); break;
                    case 0x14: Branch(result != U8(pc+1),2,6); break;
                    case 0x19: Branch(move.SourceMoveId == U16(pc+1),3,7); break;
                    case 0x1b: Branch(InTable(Pointer(pc+1),1),5,9); break;
                    case 0x1c: Branch(!InTable(Pointer(pc+1),1),5,9); break;
                    case 0x1d: Branch(InTable(Pointer(pc+1),2),5,9); break;
                    case 0x1e: Branch(!InTable(Pointer(pc+1),2),5,9); break;
                    case 0x20: Branch(!user.Pokemon.Moves.Any(m => catalog.Moves[m.Move].Power != 0),1,5); break;
                    case 0x21: result = (uint)facts.Turn; pc++; break;
                    case 0x22: result = U8(pc+1) switch { 0 => Type(0,false),1 => Type(1,false),2 => Type(0,true),3 => Type(1,true),4 => Constant(move.Type),
                        _ => throw new SourceTrainerAiScriptFault("source_trainer_ai_type_operand") }; pc+=2; break;
                    case 0x24: result = PowerRank(); pc++; break;
                    case 0x25: result = LastMove(U8(pc+1)); pc+=2; break;
                    case 0x28: Branch(StrikesFirst(slot,pc) == U8(pc+1),2,6); break;
                    case 0x2c: result = (uint)facts.ReserveCount(U8(pc+1)==1); pc+=2; break;
                    case 0x2f: result = Constant(U8(pc+1)==1 ? WildBattleEffective.Ability(user) : targetAbility(slot,$"command.{pc}")); pc+=2; break;
                    case 0x31: Branch(Effectiveness() == U8(pc+1),2,6); break;
                    // Original compares equality with STATUS1_ANY and chooses raw player/enemy
                    // party by operand, including active entries. It is not a masked status test.
                    case 0x34: Branch(facts.Party(U8(pc+1)==1).Any(m => !m.IsEgg && m.Pokemon.CurrentHp > 0 && m.Status == U32(pc+2)),6,10); break;
                    case 0x36:
                        if(facts.Rain) result=1; if(facts.Sandstorm) result=2; if(facts.Sun) result=0; if(facts.Hail) result=3;
                        pc++; break;
                    case 0x37: Branch(Constant(move.Effect) == U8(pc+1),2,6); break;
                    case 0x38: Branch(Constant(move.Effect) != U8(pc+1),2,6); break;
                    case 0x39: Branch(Stage(U8(pc+1),U8(pc+2)) < U8(pc+3),4,8); break;
                    case 0x3a: Branch(Stage(U8(pc+1),U8(pc+2)) > U8(pc+3),4,8); break;
                    case 0x3b: Branch(Stage(U8(pc+1),U8(pc+2)) == U8(pc+3),4,8); break;
                    case 0x3d:
                        if (move.Power >= 2) currentMove = move.Id;
                        Branch(move.Power >= 2 && target.Pokemon.CurrentHp <= predictions[slot],1,5); break;
                    case 0x41: case 0x42:
                        bool has = U8(pc+1) is 0 or 2 || user.Pokemon.Moves.Any(m => Constant(catalog.Moves[m.Move].Effect)==U8(pc+2));
                        Branch(op==0x41 ? has : !has,3,7); break;
                    case 0x43:
                        var mon = Mon(U8(pc+1));
                        Branch(U8(pc+2)==0 ? mon.DisabledMove is not null : U8(pc+2)==1 && mon.EncoredMove is not null,3,7); break;
                    case 0x48: result = facts.HoldEffect(U8(pc+1)==1); pc+=2; break;
                    case 0x49: result = Mon(U8(pc+1)).Pokemon.Gender switch { PokemonGender.Male => 0u, PokemonGender.Female => 254u, _ => 255u }; pc+=2; break;
                    case 0x4a: result = (uint)Mon(U8(pc+1)).FirstTurnCounter; pc+=2; break;
                    case 0x4b: result = (uint)(Mon(U8(pc+1)).CalledVolatile?.StockpileCount??0); pc+=2; break;
                    case 0x4c: result = facts.Doubles ? 1u : 0u; pc++; break;
                    case 0x4d: result = facts.UsedItemLowByte(U8(pc+1)==1); pc+=2; break;
                    case 0x4e: result = MoveField(result,0); pc++; break;
                    case 0x4f: result = MoveField(result,1); pc++; break;
                    case 0x50: result = MoveField(result,2); pc++; break;
                    case 0x51: result = (uint)Mon(U8(pc+1)).ProtectConsecutiveUses; pc+=2; break;
                    case 0x59: pc = Pointer(pc+1); break;
                    case 0x5a: done = true; break;
                    case 0x5b: Branch(U8(pc+1) switch { 0 => user.Pokemon.Level > target.Pokemon.Level, 1 => user.Pokemon.Level < target.Pokemon.Level,
                        2 => user.Pokemon.Level == target.Pokemon.Level, _ => throw new SourceTrainerAiScriptFault("source_trainer_ai_level_operand") },2,6); break;
                    case 0x5d: Branch(target.CalledVolatile?.TauntTurnsRemaining is null or 0,1,5); break;
                    default: throw new SourceTrainerAiScriptFault("source_trainer_ai_unknown_command");
                }
            }
        }
        return currentMove;
    }
}
