using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Pure pinned scoring helpers for the shared actual-position trainer AI.</summary>
internal static class SourceTrainerAi
{
    internal const string SourceCommit = PokemonFactory.SourceCommit;

    internal static bool DiscouragedPower(string effect) => effect is "EFFECT_EXPLOSION" or "EFFECT_DREAM_EATER" or
        "EFFECT_RAZOR_WIND" or "EFFECT_SKY_ATTACK" or "EFFECT_RECHARGE" or "EFFECT_SKULL_BASH" or "EFFECT_SOLAR_BEAM" or
        "EFFECT_SPIT_UP" or "EFFECT_FOCUS_PUNCH" or "EFFECT_SUPERPOWER" or "EFFECT_ERUPTION" or "EFFECT_OVERHEAT";

    // Additional CHECK_BAD_MOVE dispatches reachable after Metronome/Transform, plus source
    // corrections where the old bounded profiles never admitted the relevant registers.
    internal static bool AdditionalBadMove(SourceTrainerAiFacts source, PokemonMoveDefinition move,
        Func<string,string> ability, Action<int> score, Func<int> effectiveness)
    {
        var u = source.User; var t = source.Target;
        int Hp(WildBattleCombatant m) => 100 * m.Pokemon.CurrentHp / m.Pokemon.Stats.MaxHp;
        bool HasType(string type) => WildBattleEffective.HasType(source.Catalog, t, type);
        bool HasReserves() => source.UserHasReserves;
        void MaxStage(int stage) { if (stage == 12) score(-10); }
        void Down(int stage, string? blocked = null)
        { if (stage == 0 || blocked is not null && ability("stat-specific") == blocked || ability("stat-change") is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") score(-10); }
        switch(move.Effect)
        {
            case "EFFECT_TOXIC": if (HasType("TYPE_POISON") || HasType("TYPE_STEEL") || ability("poison-immunity") == "ABILITY_IMMUNITY" || t.Status != 0) score(-10); break;
            case "EFFECT_FUTURE_SIGHT": if (source.FutureAttack) score(-10); break;
            case "EFFECT_DEFENSE_UP_2": MaxStage(u.DefenseStage); break;
            case "EFFECT_SPEED_UP": MaxStage(u.SpeedStage); break;
            case "EFFECT_SPECIAL_ATTACK_UP_2": MaxStage(u.SpecialAttackStageDelta + 6); break;
            case "EFFECT_SPECIAL_DEFENSE_UP" or "EFFECT_SPECIAL_DEFENSE_UP_2": MaxStage(u.SpecialDefenseStageDelta + 6); break;
            case "EFFECT_ACCURACY_UP" or "EFFECT_ACCURACY_UP_2": MaxStage(u.AccuracyStage); break;
            case "EFFECT_EVASION_UP_2": MaxStage(u.EvasionStage); break;
            case "EFFECT_SPECIAL_ATTACK_DOWN" or "EFFECT_SPECIAL_ATTACK_DOWN_2": Down(t.SpecialAttackStageDelta + 6); break;
            case "EFFECT_SPECIAL_DEFENSE_DOWN": Down(t.SpecialDefenseStageDelta + 6); break;
            case "EFFECT_ACCURACY_DOWN_2": Down(t.AccuracyStage, "ABILITY_KEEN_EYE"); break;
            case "EFFECT_EVASION_DOWN_2": Down(t.EvasionStage); break;
            case "EFFECT_BELLY_DRUM": if (Hp(u) < 51 || u.AttackStage == 12) score(-10); break;
            case "EFFECT_NIGHTMARE": if(t.Nightmare) score(-10); else if(!WildBattleStatus.IsSleeping(t.Status)) score(-8); break;
            case "EFFECT_DREAM_EATER": if(!WildBattleStatus.IsSleeping(t.Status)) score(-8); else if(effectiveness()==0) score(-10); break;
            case "EFFECT_HAZE" or "EFFECT_PSYCH_UP":
                if(!Stages(u).Any(s=>s<6) && !Stages(t).Any(s=>s>6)) score(-10); break;
            case "EFFECT_ROAR":
                if (!source.TargetHasReserves || ability("roar-immunity")=="ABILITY_SUCTION_CUPS") score(-10); break;
            case "EFFECT_MAGNITUDE":
                if (ability("magnitude-immunity") == "ABILITY_LEVITATE") {score(-10);break;}
                goto case "EFFECT_BIDE";
            case "EFFECT_BIDE":
            case "EFFECT_RAZOR_WIND" or "EFFECT_SUPER_FANG" or "EFFECT_RECHARGE" or "EFFECT_PSYWAVE" or "EFFECT_COUNTER" or
                "EFFECT_MIRROR_COAT" or "EFFECT_SKULL_BASH" or "EFFECT_FOCUS_PUNCH" or "EFFECT_SUPERPOWER" or "EFFECT_ENDEAVOR" or
                "EFFECT_RETURN" or "EFFECT_PRESENT" or "EFFECT_FRUSTRATION":
                if(effectiveness()==0 || ability("high-risk-damage")=="ABILITY_WONDER_GUARD" && effectiveness()!=20) score(-10);break;
            case "EFFECT_OHKO": if(effectiveness()==0 || ability("ohko-immunity")=="ABILITY_STURDY" || u.Pokemon.Level<t.Pokemon.Level) score(-10);break;
            case "EFFECT_LIGHT_SCREEN": if(source.LightScreen) score(-8);break;
            case "EFFECT_MIST": if(source.Mist) score(-8);break;
            case "EFFECT_SAFEGUARD": if(source.Safeguard) score(-8);break;
            case "EFFECT_SUBSTITUTE": if(u.CalledVolatile?.SubstituteHp>0) score(-8); else if(Hp(u)<26) score(-10);break;
            case "EFFECT_SNORE" or "EFFECT_SLEEP_TALK": if(!WildBattleStatus.IsSleeping(u.Status)) score(-8);break;
            case "EFFECT_MEAN_LOOK": if(source.TargetTrapped) score(-10);break;
            case "EFFECT_SPIKES": if(source.Spikes) score(-10);break;
            case "EFFECT_PERISH_SONG": if(t.CalledVolatile?.PerishSongTimer is not null) score(-10);break;
            case "EFFECT_SANDSTORM": if(source.Sandstorm) score(-8);break;
            case "EFFECT_SUNNY_DAY": if(source.Sun) score(-8);break;
            case "EFFECT_HAIL": if(source.Hail) score(-8);break;
            case "EFFECT_SWAGGER" or "EFFECT_FLATTER": if(t.ConfusionTurnsRemaining>0) score(-5); else if(ability("confusion-immunity")=="ABILITY_OWN_TEMPO") score(-10);break;
            case "EFFECT_ATTRACT":
                if(source.TargetAttracted || ability("attract-immunity")=="ABILITY_OBLIVIOUS" || u.Pokemon.Gender==PokemonGender.Genderless || t.Pokemon.Gender==PokemonGender.Genderless || u.Pokemon.Gender==t.Pokemon.Gender) score(-10);break;
            case "EFFECT_MEMENTO": if(t.AttackStage==0) score(-10); else if(t.SpecialAttackStageDelta == -6) score(-8); else if(!HasReserves()) score(-10);break;
            case "EFFECT_BATON_PASS": if(!HasReserves()) score(-10);break;
            case "EFFECT_STOCKPILE": if(u.CalledVolatile?.StockpileCount==3) score(-10);break;
            case "EFFECT_SPIT_UP" or "EFFECT_SWALLOW": if(effectiveness()==0 || (u.CalledVolatile?.StockpileCount??0)==0) score(-10);break;
            case "EFFECT_TORMENT": if(t.CalledVolatile?.Tormented==true) score(-10);break;
            case "EFFECT_TRICK": if(ability("sticky-hold")=="ABILITY_STICKY_HOLD") score(-10);break;
            case "EFFECT_RECYCLE": if(!source.UsedHeldItem) score(-10);break;
            case "EFFECT_IMPRISON": if(u.Imprison) score(-10);break;
            case "EFFECT_REFRESH": if((u.Status & (WildBattleStatus.Poison|WildBattleStatus.Burn|WildBattleStatus.Paralysis|WildBattleSourceFieldEffects.ToxicPoison))==0) score(-10);break;
            case "EFFECT_TICKLE": if(t.AttackStage==0) score(-10); else if(t.DefenseStage==0) score(-8);break;
            case "EFFECT_COSMIC_POWER": if(u.DefenseStage==12) score(-10); else if(u.SpecialDefenseStageDelta==6) score(-8);break;
            case "EFFECT_BULK_UP": if(u.AttackStage==12) score(-10); else if(u.DefenseStage==12) score(-8);break;
            case "EFFECT_CALM_MIND": if(u.SpecialAttackStageDelta==6) score(-10); else if(u.SpecialDefenseStageDelta==6) score(-8);break;
            case "EFFECT_DRAGON_DANCE": if(u.AttackStage==12) score(-10); else if(u.SpeedStage==12) score(-8);break;
            default: return false;
        }
        return true;
    }
    internal static bool HasBadMoveDispatch(string effect) => effect is
        "EFFECT_SLEEP" or
        "EFFECT_EXPLOSION" or
        "EFFECT_DREAM_EATER" or
        "EFFECT_ATTACK_UP" or
        "EFFECT_DEFENSE_UP" or
        "EFFECT_SPEED_UP" or
        "EFFECT_SPECIAL_ATTACK_UP" or
        "EFFECT_SPECIAL_DEFENSE_UP" or
        "EFFECT_ACCURACY_UP" or
        "EFFECT_EVASION_UP" or
        "EFFECT_ATTACK_DOWN" or
        "EFFECT_DEFENSE_DOWN" or
        "EFFECT_SPEED_DOWN" or
        "EFFECT_SPECIAL_ATTACK_DOWN" or
        "EFFECT_SPECIAL_DEFENSE_DOWN" or
        "EFFECT_ACCURACY_DOWN" or
        "EFFECT_EVASION_DOWN" or
        "EFFECT_HAZE" or
        "EFFECT_BIDE" or
        "EFFECT_ROAR" or
        "EFFECT_TOXIC" or
        "EFFECT_LIGHT_SCREEN" or
        "EFFECT_OHKO" or
        "EFFECT_RAZOR_WIND" or
        "EFFECT_SUPER_FANG" or
        "EFFECT_MIST" or
        "EFFECT_FOCUS_ENERGY" or
        "EFFECT_CONFUSE" or
        "EFFECT_ATTACK_UP_2" or
        "EFFECT_DEFENSE_UP_2" or
        "EFFECT_SPEED_UP_2" or
        "EFFECT_SPECIAL_ATTACK_UP_2" or
        "EFFECT_SPECIAL_DEFENSE_UP_2" or
        "EFFECT_ACCURACY_UP_2" or
        "EFFECT_EVASION_UP_2" or
        "EFFECT_ATTACK_DOWN_2" or
        "EFFECT_DEFENSE_DOWN_2" or
        "EFFECT_SPEED_DOWN_2" or
        "EFFECT_SPECIAL_ATTACK_DOWN_2" or
        "EFFECT_SPECIAL_DEFENSE_DOWN_2" or
        "EFFECT_ACCURACY_DOWN_2" or
        "EFFECT_EVASION_DOWN_2" or
        "EFFECT_REFLECT" or
        "EFFECT_POISON" or
        "EFFECT_PARALYZE" or
        "EFFECT_SUBSTITUTE" or
        "EFFECT_RECHARGE" or
        "EFFECT_LEECH_SEED" or
        "EFFECT_DISABLE" or
        "EFFECT_LEVEL_DAMAGE" or
        "EFFECT_PSYWAVE" or
        "EFFECT_COUNTER" or
        "EFFECT_ENCORE" or
        "EFFECT_SNORE" or
        "EFFECT_SLEEP_TALK" or
        "EFFECT_FLAIL" or
        "EFFECT_MEAN_LOOK" or
        "EFFECT_NIGHTMARE" or
        "EFFECT_MINIMIZE" or
        "EFFECT_CURSE" or
        "EFFECT_SPIKES" or
        "EFFECT_FORESIGHT" or
        "EFFECT_PERISH_SONG" or
        "EFFECT_SANDSTORM" or
        "EFFECT_SWAGGER" or
        "EFFECT_ATTRACT" or
        "EFFECT_RETURN" or
        "EFFECT_PRESENT" or
        "EFFECT_FRUSTRATION" or
        "EFFECT_SAFEGUARD" or
        "EFFECT_MAGNITUDE" or
        "EFFECT_BATON_PASS" or
        "EFFECT_SONICBOOM" or
        "EFFECT_RAIN_DANCE" or
        "EFFECT_SUNNY_DAY" or
        "EFFECT_BELLY_DRUM" or
        "EFFECT_PSYCH_UP" or
        "EFFECT_MIRROR_COAT" or
        "EFFECT_SKULL_BASH" or
        "EFFECT_FUTURE_SIGHT" or
        "EFFECT_TELEPORT" or
        "EFFECT_DEFENSE_CURL" or
        "EFFECT_FAKE_OUT" or
        "EFFECT_STOCKPILE" or
        "EFFECT_SPIT_UP" or
        "EFFECT_SWALLOW" or
        "EFFECT_HAIL" or
        "EFFECT_TORMENT" or
        "EFFECT_FLATTER" or
        "EFFECT_WILL_O_WISP" or
        "EFFECT_MEMENTO" or
        "EFFECT_FOCUS_PUNCH" or
        "EFFECT_HELPING_HAND" or
        "EFFECT_TRICK" or
        "EFFECT_INGRAIN" or
        "EFFECT_SUPERPOWER" or
        "EFFECT_RECYCLE" or
        "EFFECT_KNOCK_OFF" or
        "EFFECT_ENDEAVOR" or
        "EFFECT_IMPRISON" or
        "EFFECT_REFRESH" or
        "EFFECT_LOW_KICK" or
        "EFFECT_MUD_SPORT" or
        "EFFECT_TICKLE" or
        "EFFECT_COSMIC_POWER" or
        "EFFECT_BULK_UP" or
        "EFFECT_WATER_SPORT" or
        "EFFECT_CALM_MIND" or
        "EFFECT_DRAGON_DANCE";

    private static IEnumerable<int> Stages(WildBattleCombatant m) => [m.AttackStage,m.DefenseStage,m.SpeedStage,m.SpecialAttackStageDelta+6,m.SpecialDefenseStageDelta+6,m.AccuracyStage,m.EvasionStage];
}
