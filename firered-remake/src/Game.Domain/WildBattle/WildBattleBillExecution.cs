using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

/// <summary>Source battle terrain values used by Secret Power; independent of rendered backgrounds.</summary>
public enum WildBattleTerrain { Grass=0,LongGrass=1,Sand=2,Underwater=3,Water=4,Pond=5,Mountain=6,Cave=7,
    Building=8,Plain=9,Link=10,Gym=11,Leader=12,Indoor2=13,Indoor1=14,Lorelei=15,Bruno=16,Agatha=17,Lance=18,Champion=19 }

public static partial class WildBattleRules
{
    internal static string? ValidateBillTerrain(WildBattleState state) => UsesBillRules(state.RulesVersion)
        ? state.Terrain is null || !Enum.IsDefined(state.Terrain.Value) ? "bill_battle_terrain" : null
        : state.Terrain is not null ? "legacy_battle_terrain_unsupported" : null;
    internal sealed partial class Execution
    {
        private readonly HashSet<WildBattleSide> billDigRelease=[];
        private void ChangeDig(WildBattleSide side,int? slot,string reason)
        {
            var mon=Get(side);if(mon.DigMoveSlot==slot)return;
            Set(side,mon with{DigMoveSlot=slot});
            Add(n=>new WildBattleMtMoonVolatileEvent(n,State.Turn,side,"dig-underground",mon.DigMoveSlot is null?0:1,slot is null?0:1,reason));
        }
        private bool BillUnderground(WildBattleSide side,WildBattleSide target,PokemonMoveDefinition move) =>
            Bill && side!=target && Get(target).DigMoveSlot is not null && move.Effect!="EFFECT_MAGNITUDE";
        private bool BillScript(PokemonCatalog catalog,WildBattleSide side,int slot,PokemonMoveDefinition move,string purpose)
        {
            var targetSide=Other(side);
            if(move.Id.Value=="pret:MOVE_DIG")
            {
                if(Get(side).DigMoveSlot is null)
                {
                    SpendEarlyPartyPp(side,targetSide,slot,move);
                    ChangeDig(side,slot,"charging");return true;
                }
                // Source NO_PPDEDUCT on release, including an accuracy failure.
                billDigRelease.Add(side);ChangeDig(side,null,"release");return false;
            }
            if(move.Effect!="EFFECT_ATTRACT")return false;
            SpendEarlyPartyPp(side,targetSide,slot,move);
            if(!EarlyPartyAccuracy(side,targetSide,move,purpose))return true;
            var actor=Get(side);var target=Get(targetSide);
            if(target.Pokemon.AbilityId=="ABILITY_OBLIVIOUS")
            {
                RecordAbility?.Invoke(targetSide,"ABILITY_OBLIVIOUS");
                Add(n=>new WildBattleAbilityPreventedEvent(n,State.Turn,side,targetSide,move.Id,"ABILITY_OBLIVIOUS"));return true;
            }
            string? reason=actor.Pokemon.Gender==PokemonGender.Genderless || target.Pokemon.Gender==PokemonGender.Genderless ? "genderless" :
                actor.Pokemon.Gender==target.Pokemon.Gender ? "same-gender" : target.InfatuationSource is not null ? "already-infatuated" : null;
            if(reason is not null)Add(n=>new WildBattleMoveFailedEvent(n,State.Turn,side,targetSide,move.Id,reason));
            else ChangeInfatuation(targetSide,side,"attract");
            return true;
        }
        private void BillSecretPower(PokemonCatalog catalog,WildBattleSide side,WildBattleSide target,string purpose,MoveId move)
        {
            var mon=Get(target);if(mon.Pokemon.CurrentHp==0 || mon.Pokemon.AbilityId=="ABILITY_SHIELD_DUST")return;
            switch(State.Terrain)
            {
                case WildBattleTerrain.Grass:InflictStatus(catalog,target,WildBattleStatus.Poison,false);break;
                case WildBattleTerrain.LongGrass:
                    if(mon.Status==0 && mon.Pokemon.AbilityId is not ("ABILITY_INSOMNIA" or "ABILITY_VITAL_SPIRIT"))
                        ChangeStatus(target,(uint)((Draw($"{purpose}.sleep-duration",4)&3)+2),"move");
                    break;
                case WildBattleTerrain.Sand:ChangeStage(side,target,WildBattleStat.Accuracy,-1,move,true);break;
                case WildBattleTerrain.Underwater:ChangeStage(side,target,WildBattleStat.Defense,-1,move,true);break;
                case WildBattleTerrain.Water:ChangeStage(side,target,WildBattleStat.Attack,-1,move,true);break;
                case WildBattleTerrain.Pond:ChangeStage(side,target,WildBattleStat.Speed,-1,move,true);break;
                case WildBattleTerrain.Mountain:ApplyConfusion(target,purpose,true);break;
                case WildBattleTerrain.Cave:ApplyFlinch(target);break;
                default:InflictStatus(catalog,target,WildBattleStatus.Paralysis,false);break;
            }
        }
    }
}
