using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Validated source state passed to shared AI; callers cannot fabricate a validation exemption.</summary>
public sealed class SourceTrainerAiContext
{
    internal SourceTrainerAiContext(PokemonCatalog catalog, ForestTrainerCombatState state, ImmutableArray<WildBattleCombatant> party)
    { Catalog = catalog; State = state; Party = party; }
    internal PokemonCatalog Catalog { get; }
    internal ForestTrainerCombatState State { get; }
    internal ImmutableArray<WildBattleCombatant> Party { get; }
    internal WildBattleState Battle => State.Battle;
    internal bool Matches(PokemonCatalog catalog, WildBattleCombatant user, WildBattleCombatant target) =>
        ReferenceEquals(catalog, Catalog) && user == Battle.Opponent && target == Battle.Player;
}

public sealed record SourceTrainerItemDecision(TransitionStatus Status, int? ItemSlot, string? Item,
    int HpAfter, uint StatusAfter, int ConfusionTurnsAfter, Diagnostic? Diagnostic)
{
    public bool Succeeded => Status == TransitionStatus.Accepted;
}

/// <summary>Pinned ordinary trainer AI over the actual source battle registers. The original
/// scoring, switching and replacement algorithms remain shared with historical profiles.
/// Persistent identity is used only to authenticate the original trainer roster.</summary>
public static class SourceTrainerAi
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-source-trainer-ai-v1";

    public static CeruleanTrainerAiDecision Choose(PokemonCatalog catalog, ForestTrainerCombatState state)
    {
        string? error = Validate(catalog, state, out var source);
        var rng = state?.Battle?.Rng ?? new RngState(0);
        if (error is not null) return new(TransitionStatus.Fault, null, [], [], rng, [], new(error, "Invalid source trainer move context."));
        var battle = state!.Battle;
        if (battle.Opponent.Pokemon.CurrentHp == 0 || battle.Player.Pokemon.CurrentHp == 0 || battle.CalledParty!.PendingSwitch is not null)
            return new(TransitionStatus.Fault, null, [], [], rng, [], new("source_trainer_ai_phase", "Both active battlers must be ready."));
        return CeruleanTrainerAi.Choose(catalog, new(battle.Opponent, battle.Player, state.KnownPlayerAbility, Flags(state.Trainer)) {
            UserHasReserves = source!.Party.Where((_,i) => i != state.OpponentIndex).Any(m => m.Pokemon.CurrentHp > 0),
            TargetHasReserves = WildBattleSourcePartyEffects.LegalReserveIndices(battle.CalledParty, WildBattleSide.Player).Length > 0
        }, battle.Rng, fuchsiaRules: true, rain: battle.RainTurnsRemaining > 0,
            reflect: battle.PlayerReflectTurnsRemaining > 0, userReflect: battle.OpponentReflectTurnsRemaining > 0, source: source);
    }

    public static CeruleanTrainerSwitchDecision DecideSwitch(PokemonCatalog catalog, ForestTrainerCombatState state)
    {
        string? error = Validate(catalog, state, out var source);
        var rng = state?.Battle?.Rng ?? new RngState(0);
        if (error is not null) return new(TransitionStatus.Fault, null, rng, [], new(error, "Invalid source trainer switch context."));
        var battle = state!.Battle;
        if (battle.Opponent.Pokemon.CurrentHp == 0 || battle.Player.Pokemon.CurrentHp == 0 || battle.CalledParty!.PendingSwitch is not null)
            return new(TransitionStatus.Fault, null, rng, [], new("source_trainer_switch_phase", "Both active battlers must be ready."));
        return CeruleanTrainerActionAi.DecideSwitch(catalog, new(state.Trainer, source!.Party, state.OpponentIndex,
            battle.Opponent, battle.Player, battle.Opponent.LastLandedMove, battle.Opponent.LastHitBy, battle.CalledParty.PlayerActiveIndex),
            battle.Rng, fuchsiaRules: true, source: source);
    }

    public static ForestTrainerReplacementDecision ChooseReplacement(PokemonCatalog catalog, ForestTrainerCombatState state)
    {
        string? error = Validate(catalog, state, out var source, replacement: true);
        if (error is not null) return new(TransitionStatus.Fault, null, [], new(error, "Invalid source trainer replacement context."));
        if (state.Battle.Player.Pokemon.CurrentHp == 0 || state.Battle.Opponent.Pokemon.CurrentHp > 0 &&
            state.Battle.CalledParty!.PendingSwitch is not { Side: WildBattleSide.Opponent, Kind: WildBattleCalledSwitchKind.BatonPass })
            return new(TransitionStatus.Fault, null, [], new("source_trainer_replacement_phase", "Trainer replacement requires a faint or Baton Pass boundary."));
        if (!source!.Party.Where((_,i) => i != state.OpponentIndex).Any(m => m.Pokemon.CurrentHp > 0))
            return new(TransitionStatus.Fault, null, [], new("source_trainer_replacement_no_reserve", "No living trainer reserve."));
        return Replacement(source);
    }

    internal static ForestTrainerReplacementDecision Replacement(SourceTrainerAiContext source) =>
        CeruleanTrainerAiReplacement.Choose(source.Catalog, new(source.Party, source.State.OpponentIndex,
            source.Battle.Opponent, source.Battle.Player, source.State.StaleCurrentMove), fuchsiaRules: true,
            rain: source.Battle.RainTurnsRemaining > 0, reflect: source.Battle.PlayerReflectTurnsRemaining > 0, source: source);

    /// <summary>ShouldUseItem is pure. The caller consumes the returned slot only when its transaction succeeds.</summary>
    public static SourceTrainerItemDecision DecideItem(PokemonCatalog catalog, ForestTrainerCombatState state)
    {
        string? error = Validate(catalog, state, out var source);
        if (error is not null) return new(TransitionStatus.Fault, null, null, 0, 0, 0, new(error, "Invalid source trainer item context."));
        var active = state.Battle.Opponent; var mon = active.Pokemon;
        SourceTrainerItemDecision Done(int? slot = null, string? item = null, int? hp = null, uint? status = null, int? confusion = null) =>
            new(TransitionStatus.Accepted, slot, item, hp ?? mon.CurrentHp, status ?? active.Status,
                confusion ?? active.ConfusionTurnsRemaining, null);
        if (mon.CurrentHp == 0 || state.Battle.Player.Pokemon.CurrentHp == 0 || state.Battle.CalledParty!.PendingSwitch is not null)
            return new(TransitionStatus.Fault, null, null, 0, 0, 0, new("source_trainer_item_phase", "Both active battlers must be ready."));
        var items = Items(state.Trainer); int validMons = source!.Party.Count(m => m.Pokemon.CurrentHp > 0);
        for (int i = 0; i < items.Length; i++)
        {
            // itemsNo is the initial compacted inventory size, not the remaining item count.
            if ((state.CeruleanItemsUsedMask & (1 << i)) != 0 || i > 0 && validMons > items.Length - i + 1) continue;
            string item = items[i]; int heal = item switch { "ITEM_POTION" => 20, "ITEM_SUPER_POTION" => 50, "ITEM_HYPER_POTION" => 200, _ => 0 };
            if (heal > 0 && (mon.CurrentHp < mon.Stats.MaxHp / 4 || mon.Stats.MaxHp - mon.CurrentHp > heal))
                return Done(i, item, Math.Min(mon.Stats.MaxHp, mon.CurrentHp + heal));
            if (item == "ITEM_FULL_HEAL" && (active.Status != 0 || active.ConfusionTurnsRemaining > 0))
                return Done(i, item, status: 0, confusion: 0);
        }
        return Done();
    }

    internal static ImmutableArray<string> Items(TrainerId id)
    {
        if (FuchsiaGymTrainerPartyFactory.Expected.TryGetValue(id, out var gym)) return gym.Items.Where(x => x != "ITEM_NONE").ToImmutableArray();
        if (CeladonGymTrainerPartyFactory.Expected.TryGetValue(id, out var celadon)) return celadon.Items.Where(x => x != "ITEM_NONE").ToImmutableArray();
        if (id == VermilionGymTrainerPartyFactory.LtSurge) return ["ITEM_SUPER_POTION", "ITEM_FULL_HEAL"];
        return id == CeruleanTrainerPartyFactory.Misty ? ["ITEM_SUPER_POTION"] : [];
    }

    internal static uint Flags(TrainerId id)
    {
        if (FuchsiaGymTrainerPartyFactory.Expected.TryGetValue(id, out var gym)) return gym.AiFlags;
        if (PokemonTowerTrainerPartyFactory.Expected.TryGetValue(id, out var tower)) return tower.AiFlags;
        if (RocketHideoutTrainerPartyFactory.Expected.TryGetValue(id, out var rocket)) return rocket.AiFlags;
        if (CeladonGymTrainerPartyFactory.Expected.TryGetValue(id, out var celadon)) return celadon.AiFlags;
        if (CeladonApproachTrainerPartyFactory.Expected.TryGetValue(id, out var approach)) return approach.AiFlags;
        if (RockTunnelTrainerPartyFactory.Expected.TryGetValue(id, out var rock)) return rock.AiFlags;
        if (VermilionGymTrainerPartyFactory.Expected.TryGetValue(id, out var surge)) return surge.AiFlags;
        if (SsAnneTrainerPartyFactory.Expected.TryGetValue(id, out var ship)) return ship.AiFlags;
        if (VermilionTrainerPartyFactory.Expected.TryGetValue(id, out var vermilion)) return vermilion.Flags == 1 ? 7u : 1u;
        if (BillTrainerPartyFactory.Expected.TryGetValue(id, out var bill)) return bill.Flags == 1 ? 7u : 1u;
        if (CeruleanTrainerPartyFactory.SupportedTrainers.Contains(id)) return CeruleanTrainerPartyFactory.Expected(id).Flags == 1 ? 7u : 1u;
        return id == PewterTrainerPartyFactory.Brock ? 7u : 1u;
    }

    private static string? Validate(PokemonCatalog? catalog, ForestTrainerCombatState? state, out SourceTrainerAiContext? context, bool replacement = false)
    {
        context = null;
        if (catalog is null || state?.Battle is null || state.SourceMoveRulesVersion != ForestTrainerCombatRules.SourceTrainerContractVersion ||
            catalog.SourceCommit != SourceCommit || catalog.Species.Count != 107) return "source_trainer_ai_contract";
        if ((replacement ? ForestTrainerCombatRules.ValidateSourceTrainerAiState(catalog, state) : ForestTrainerCombatRules.Validate(catalog, state)) is {} error) return error;
        var actual = state.Battle.CalledParty!;
        var party = actual.Opponent.Select((m,i) => i == actual.OpponentActiveIndex ? state.Battle.Opponent :
            new WildBattleCombatant(m.Pokemon, HeldItem: m.HeldItem, Status: m.Status) { KnockedOffItem = m.KnockedOffItem }).ToImmutableArray();
        int mask = (1 << Items(state.Trainer).Length) - 1;
        if (state.CeruleanItemsUsedMask < 0 || (state.CeruleanItemsUsedMask & ~mask) != 0) return "source_trainer_ai_item_mask";
        context = new(catalog, state, party); return null;
    }

    internal static bool DiscouragedPower(string effect) => effect is "EFFECT_EXPLOSION" or "EFFECT_DREAM_EATER" or
        "EFFECT_RAZOR_WIND" or "EFFECT_SKY_ATTACK" or "EFFECT_RECHARGE" or "EFFECT_SKULL_BASH" or "EFFECT_SOLAR_BEAM" or
        "EFFECT_SPIT_UP" or "EFFECT_FOCUS_PUNCH" or "EFFECT_SUPERPOWER" or "EFFECT_ERUPTION" or "EFFECT_OVERHEAT";

    // Additional CHECK_BAD_MOVE dispatches reachable after Metronome/Transform, plus source
    // corrections where the old bounded profiles never admitted the relevant registers.
    internal static bool AdditionalBadMove(SourceTrainerAiContext source, PokemonMoveDefinition move,
        Func<string,string> ability, Action<int> score, Func<int> effectiveness)
    {
        var b = source.Battle; var u = b.Opponent; var t = b.Player;
        int Hp(WildBattleCombatant m) => 100 * m.Pokemon.CurrentHp / m.Pokemon.Stats.MaxHp;
        bool HasType(string type) => WildBattleEffective.HasType(source.Catalog, t, type);
        bool HasReserves() => source.Party.Where((_,i) => i != source.State.OpponentIndex).Any(m => m.Pokemon.CurrentHp > 0);
        void MaxStage(int stage) { if (stage == 12) score(-10); }
        void Down(int stage, string? blocked = null)
        { if (stage == 0 || blocked is not null && ability("stat-specific") == blocked || ability("stat-change") is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") score(-10); }
        switch(move.Effect)
        {
            case "EFFECT_TOXIC": if (HasType("TYPE_POISON") || HasType("TYPE_STEEL") || ability("poison-immunity") == "ABILITY_IMMUNITY" || t.Status != 0) score(-10); break;
            case "EFFECT_FUTURE_SIGHT": if (b.CalledDelayed?.PlayerFutureAttack is not null) score(-10); break;
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
                if (WildBattleSourcePartyEffects.LegalReserveIndices(b.CalledParty!, WildBattleSide.Player).IsEmpty || ability("roar-immunity")=="ABILITY_SUCTION_CUPS") score(-10); break;
            case "EFFECT_MAGNITUDE":
                if (ability("magnitude-immunity") == "ABILITY_LEVITATE") {score(-10);break;}
                goto case "EFFECT_BIDE";
            case "EFFECT_BIDE":
            case "EFFECT_RAZOR_WIND" or "EFFECT_SUPER_FANG" or "EFFECT_RECHARGE" or "EFFECT_PSYWAVE" or "EFFECT_COUNTER" or
                "EFFECT_MIRROR_COAT" or "EFFECT_SKULL_BASH" or "EFFECT_FOCUS_PUNCH" or "EFFECT_SUPERPOWER" or "EFFECT_ENDEAVOR" or
                "EFFECT_RETURN" or "EFFECT_PRESENT" or "EFFECT_FRUSTRATION":
                if(effectiveness()==0 || ability("high-risk-damage")=="ABILITY_WONDER_GUARD" && effectiveness()!=20) score(-10);break;
            case "EFFECT_OHKO": if(effectiveness()==0 || ability("ohko-immunity")=="ABILITY_STURDY" || u.Pokemon.Level<t.Pokemon.Level) score(-10);break;
            case "EFFECT_LIGHT_SCREEN": if(b.CalledField?.OpponentLightScreenTurnsRemaining>0) score(-8);break;
            case "EFFECT_MIST": if(b.CalledField?.OpponentMistTurnsRemaining>0) score(-8);break;
            case "EFFECT_SAFEGUARD": if(b.CalledField?.OpponentSafeguardTurnsRemaining>0) score(-8);break;
            case "EFFECT_SUBSTITUTE": if(u.CalledVolatile?.SubstituteHp>0) score(-8); else if(Hp(u)<26) score(-10);break;
            case "EFFECT_SNORE" or "EFFECT_SLEEP_TALK": if(!WildBattleStatus.IsSleeping(u.Status)) score(-8);break;
            case "EFFECT_MEAN_LOOK": if(t.MeanLookSource is not null) score(-10);break;
            case "EFFECT_SPIKES": if(b.CalledParty?.PlayerSpikes>0) score(-10);break;
            case "EFFECT_PERISH_SONG": if(t.CalledVolatile?.PerishSongTimer is not null) score(-10);break;
            case "EFFECT_SANDSTORM": if(b.SandstormTurnsRemaining>0) score(-8);break;
            case "EFFECT_SUNNY_DAY": if(b.CalledField?.SunTurnsRemaining>0) score(-8);break;
            case "EFFECT_HAIL": if(b.CalledField?.HailTurnsRemaining>0) score(-8);break;
            case "EFFECT_SWAGGER" or "EFFECT_FLATTER": if(t.ConfusionTurnsRemaining>0) score(-5); else if(ability("confusion-immunity")=="ABILITY_OWN_TEMPO") score(-10);break;
            case "EFFECT_ATTRACT":
                if(t.InfatuationSource is not null || ability("attract-immunity")=="ABILITY_OBLIVIOUS" || u.Pokemon.Gender==PokemonGender.Genderless || t.Pokemon.Gender==PokemonGender.Genderless || u.Pokemon.Gender==t.Pokemon.Gender) score(-10);break;
            case "EFFECT_MEMENTO": if(t.AttackStage==0) score(-10); else if(t.SpecialAttackStageDelta == -6) score(-8); else if(!HasReserves()) score(-10);break;
            case "EFFECT_BATON_PASS": if(!HasReserves()) score(-10);break;
            case "EFFECT_STOCKPILE": if(u.CalledVolatile?.StockpileCount==3) score(-10);break;
            case "EFFECT_SPIT_UP" or "EFFECT_SWALLOW": if(effectiveness()==0 || (u.CalledVolatile?.StockpileCount??0)==0) score(-10);break;
            case "EFFECT_TORMENT": if(t.CalledVolatile?.Tormented==true) score(-10);break;
            case "EFFECT_TRICK": if(ability("sticky-hold")=="ABILITY_STICKY_HOLD") score(-10);break;
            case "EFFECT_RECYCLE": if(b.CalledParty?.OpponentUsedHeldItem is null) score(-10);break;
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
