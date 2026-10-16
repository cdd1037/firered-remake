using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
namespace Game.Domain.FirstBattle;
/// <summary>Source stat stages composed with the already verified ordinary physical damage formula.</summary>
public static class FirstBattleDamage
{
    public static int Stat(int basis,int stage)
    {
        if(basis is <1 or >999||stage is <0 or >12)throw new ArgumentOutOfRangeException(nameof(stage));
        return stage<6?basis*10/(10+(6-stage)*5):basis*(10+(stage-6)*5)/10;
    }
    public static DamageResult Calculate(PokemonCatalog catalog,PokemonInstance user,PokemonInstance target,
        MoveId moveId,int attackStage,int defenseStage,bool critical,int randomPercent)
    {
        int attack=Stat(user.Stats.Attack,critical&&attackStage<=6?6:attackStage),defense=Stat(target.Stats.Defense,critical&&defenseStage>=6?6:defenseStage);
        if(moveId == FirstBattleRules.StruggleMove)
        {
            // Source still calculates Normal physical base damage, then Cmd_typecalc skips BOTH STAB and the chart.
            // Mystery here is only a neutral modifier sentinel, never the move category or an altered Pokemon type.
            return DamageRules.Calculate(new(user.Level,50,ElementType.Normal,attack,defense,
                ElementType.Mystery,null,ElementType.Mystery,null,critical,randomPercent));
        }
        var move=catalog.Moves[moveId];
        if(move.Id.Value is not("pret:MOVE_TACKLE" or "pret:MOVE_SCRATCH")||move.Type!="TYPE_NORMAL"||move.Effect!="EFFECT_HIT")throw new NotSupportedException("first-battle-damage-move");
        var a=catalog.Species[user.Species];var d=catalog.Species[target.Species];
        ElementType Type(string symbol)=>Enum.Parse<ElementType>(symbol[5..],true);
        return DamageRules.Calculate(new(user.Level,move.Power,ElementType.Normal,attack,defense,Type(a.Type1),Type(a.Type2),Type(d.Type1),Type(d.Type2),critical,randomPercent));
    }
}
