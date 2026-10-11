using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasChargeRecipe(string effect) => effect is "EFFECT_RAZOR_WIND" or "EFFECT_SEMI_INVULNERABLE" or "EFFECT_SOLAR_BEAM" or "EFFECT_SKULL_BASH" or "EFFECT_SKY_ATTACK";
    internal virtual bool ChargeLocked(BattlerSlot actor) => Get(actor).CalledTurnState?.Lock?.Kind == WildBattleCalledLockKind.Charge;
    internal virtual WildBattleSemiInvulnerable SemiInvulnerable(BattlerSlot target) => WildBattleRules.CalledSemiInvulnerable(Get(target));
    internal abstract void ChangeCharge(BattlerSlot actor, PokemonMoveDefinition move, bool locked, string reason);
    internal abstract void ChargePp(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, bool attackString);
    internal abstract void ChargeNoPpDeduct();
    internal virtual bool ChargeAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose) => SourceGenericAccuracy(actor,target,move,purpose);
    internal virtual int ChargeDamage(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, bool critical, int percent, string purpose) =>
        AdjustHeldDamage(target,move.Id,SourceGenericDamage(catalog,Get(actor),Get(target),target,move,critical,percent),purpose);
    internal virtual bool ChargeImmune(PokemonCatalog catalog, BattlerSlot target, PokemonMoveDefinition move) => FixedDamageType(catalog,target,move).Immune;
    internal virtual void ChargeReportType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) => SourceGenericEffectiveness(catalog,actor,target,move);
    internal virtual void ChargeFinish(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move,string purpose,int lost) => RolloutFinish(catalog,actor,target,move,purpose,lost);
    private bool chargeRelease;
    private bool sunnyChargePrepared;
    // SolarBeam's sun branch is before attackcanceler, including on an existing charge.
    // Store the script branch before Protect/other cancellers can clear the lock.
    internal void PrepareCharge(BattlerSlot actor, PokemonMoveDefinition move)
    {
        chargeRelease=ChargeLocked(actor); sunnyChargePrepared=false;
        if(move.Effect!="EFFECT_SOLAR_BEAM" || !SourceCalledSun || SourceCalledWeatherSuppressed)return;
        ChangeCharge(actor,move,true,"move");
        ChargePp(actor,Invocation!.Target,move,false);
        sunnyChargePrepared=true;
    }
    internal bool ChargeFirstTurn(PokemonMoveDefinition move) => HasChargeRecipe(move.Effect) && !chargeRelease && !sunnyChargePrepared;
    internal void ReleaseChargeWithoutTarget(BattlerSlot actor, PokemonMoveDefinition move)
    {
        if(!HasChargeRecipe(move.Effect)||!chargeRelease&&!sunnyChargePrepared)return;
        ChangeCharge(actor,move,false,"released");ChargeNoPpDeduct();
    }
    internal void ExecuteCharge(PokemonCatalog catalog,BattlerSlot actor,PokemonMoveDefinition move,string purpose,ref bool handled)
    {
        if(!HasChargeRecipe(move.Effect))return;
        handled=true;var target=Invocation!.Target;
        if(ChargeFirstTurn(move))
        {
            ChargePp(actor,target,move,false);ChangeCharge(actor,move,true,"move");
            if(move.Effect=="EFFECT_SKULL_BASH")ChangeStage(actor,actor,WildBattleStat.Defense,1,move.Id);
            return;
        }
        ChangeCharge(actor,move,false,"released");ChargeNoPpDeduct();
        bool hit=ChargeAccuracy(actor,target,move,purpose);
        ChargePp(actor,target,move,true);
        if(!hit){SourceGenericMiss(actor,target,move);return;}
        if(Route11WaterAbsorb(actor,target,move)||SsAnneFlashFire(actor,target,move))return;
        bool critical=SourceGenericCritical(Get(actor),Get(target),move,purpose);
        int percent=100-Draw($"{purpose}.damage",16)%16;
        int damage=ChargeDamage(catalog,actor,target,move,critical,percent,purpose);
        int lost=SourceGenericApplyDamage(actor,target,move,damage,critical,percent);
        ChargeReportType(catalog,actor,target,move);
        bool effective=!ChargeImmune(catalog,target,move);
        int roll=Draw($"{purpose}.secondary-effect",100)%100;
        int chance=move.SecondaryEffectChance*(WildBattleEffective.Ability(Get(actor))=="ABILITY_SERENE_GRACE"?2:1);
        if(effective&&roll<=chance)
        {
            if(move.Effect=="EFFECT_SKY_ATTACK")ApplyFlinch(target);
            else if(move.Id.Value=="pret:MOVE_BOUNCE")InflictStatus(catalog,target,WildBattleStatus.Paralysis,false);
        }
        ChargeFinish(catalog,actor,target,move,purpose,lost);
    }
}
