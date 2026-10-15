using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;
public static class RockTunnelMachineRules
{
    public const string Profile = "firered-rocktunnel-machines-v1";
    public static ItemId Tm40Id { get; } = new("pret:ITEM_TM40");
    private static readonly string[] TailCompatibility = ["10011111000000", "00000001000000", "00000000000000", "11111111001111", "00010000111000", "10111111111111", "11111111111111", "10011111000000", "10111110000000", "11111111111111", "00010000001000", "01010001000001", "01100000000111", "10000110000100"];
    private const string AerialCompatibility = "000111000001001111000011000001110110000000110110000010000000000000001110011000010000110000100";
    public static void ValidateCatalog(PewterMachineCatalog catalog)
    {
        if(catalog.Profile!=Profile||catalog.Compatibility.IsDefault||catalog.Compatibility.Length!=93||catalog.AdditionalMachines is not {IsDefault:false,Length:13} extra)
            throw new NotSupportedException("Incomplete RockTunnel machine scope.");
        PewterMachineRules.ValidateCatalog(catalog with{Profile=PewterMachineRules.Route11Profile,
            Compatibility=catalog.Compatibility.Take(79).ToImmutableArray(),
            AdditionalMachines=extra.Take(12).Select(m=>m with{Compatibility=m.Compatibility.Take(79).ToImmutableArray()}).ToImmutableArray()});
        var values=new[]{PewterMachineRules.Definition(catalog,PewterMachineRules.Tm39Id)!}.Concat(extra).ToArray();
        string[] species=["CUBONE","ELECTRODE","HYPNO","LICKITUNG","MACHOKE","MAROWAK","PRIMEAPE","RHYHORN","SEADRA","SEAKING","TENTACRUEL","VENOMOTH","VENONAT","WEEZING"];int[] ids=[104,101,97,108,67,105,57,111,117,119,73,49,48,110];
        for(int row=0;row<values.Length;row++)
        {
            var value=values[row];if(value.Compatibility.IsDefault||value.Compatibility.Length!=93)throw new NotSupportedException("RockTunnel compatibility shape.");
            for(int n=0;n<93;n++)
            {
                var c=value.Compatibility[n];var basis=catalog.Compatibility[n];
                if(c is null||basis is null||c.Species!=basis.Species||c.SourceSpeciesId!=basis.SourceSpeciesId)throw new NotSupportedException("RockTunnel compatibility source differs.");
                if(n>=79&&(c.Species!=new SpeciesId("pret:SPECIES_"+species[n-79])||c.SourceSpeciesId!=ids[n-79]||c.CanLearn!=(TailCompatibility[row][n-79]=='1'))||row==13&&c.CanLearn!=(AerialCompatibility[n]=='1'))throw new NotSupportedException("RockTunnel source compatibility differs.");
            }
        }
        var tm=values[13];var i=tm.Machine.Item;
        if(tm.Move!=new MoveId("pret:MOVE_AERIAL_ACE")||tm.SourceMoveId!=332||tm.MovePp!=20||i.Id!=Tm40Id||i.SourceItemSymbol!="ITEM_TM40"||i.SourceItemId!=328||i.Name!="TM40"||i.Price!=3000||i.Importance!=0||i.Registrability!=0||i.Pocket!="POCKET_TM_CASE"||i.Type!="ITEM_TYPE_PARTY_MENU"||i.FieldUseFunction!="NULL"||i.HoldEffect!="HOLD_EFFECT_NONE"||i.HoldEffectParam!=0||i.BattleUsage!=0||i.BattleUseFunction!="NULL"||i.SecondaryId!=0||!i.CanToss||!i.CanSell||!i.CanGive||
            i.Source!=new OpeningItemSourceRef("src/data/items.json", "ITEM_TM40", 5290, 5306)||tm.Machine.DescriptionSource!=new OpeningItemSourceRef("src/move_descriptions.c", "gMoveDescription_AerialAce", 335, 335)||tm.Machine.Description!="An extremely fast\nattack against one\ntarget. It can't be\nevaded."||tm.Machine.DeclarationDescription!="An extremely fast attack against\none target. It is impossible to\nevade.")throw new NotSupportedException("TM40 source metadata differs.");
    }
}
