using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Items;

/// <summary>Source-frozen eleven-machine ship and twelve-machine Gym profiles over the same 73 species.</summary>
internal static class SsAnneMachineRules
{
    private sealed record Expected(string Item, int Id, int Price, string Move, int MoveId, int Pp, string Compatibility);
    private static readonly Expected[] Rows = [
        new("TM39", 327, 3000, "ROCK_TOMB", 317, 10, "0000000000000000000000111111100100000000000000000100110000100000000000000"),
        new("TM05", 293, 1000, "ROAR", 46, 20, "0010010010000000000100000011100000000001000000000000110000000000000001100"),
        new("TM09", 297, 3000, "BULLET_SEED", 331, 30, "1110000000000000000000000000000000000000000011110000000000000000001100000"),
        new("TM46", 334, 3000, "THIEF", 168, 10, "0000000000010011111101110000011111111000101101110111110011110000000111101"),
        new("TM03", 291, 3000, "WATER_PULSE", 352, 20, "0000001110000000000000000000000001111101010000000000111100001111110011001"),
        new("TM45", 333, 3000, "ATTRACT", 213, 15, "1111111110010011111111111111111111111101111111110101111111111110011111111"),
        new("TM43", 331, 3000, "SECRET_POWER", 290, 20, "1111111110010011111111111111111111111101111111111111111111111111111111111"),
        new("TM28", 316, 2000, "DIG", 91, 10, "0001111110000000001111111111100111111100110001100100111100100000010011100"),
        new("TM31", 319, 3000, "BRICK_BREAK", 280, 15, "0001111110000010000011111110000100000100010000000000111100110000000000000"),
        new("TM44", 332, 3000, "REST", 156, 10, "1111111110010011111111111111111111111101111111111111111111111111111111111"),
        new("HM01", 339, 0, "CUT", 15, 30, "1111110000000010001100110000100001111000000011110000110000000000001111001"),
    ];
    public static void ValidateCatalog(PewterMachineCatalog catalog)
    {
        bool gym = catalog.Profile == PewterMachineRules.VermilionGymProfile;
        if (catalog.Compatibility.IsDefault || catalog.Compatibility.Length != 73 ||
            catalog.AdditionalMachines is not { IsDefault: false } additions || additions.Length != (gym ? 11 : 10))
            throw new NotSupportedException("Incomplete S.S. Anne machine scope.");
        PewterMachineRules.ValidateCatalog(catalog with
        {
            Profile = PewterMachineRules.VermilionProfile,
            Compatibility = catalog.Compatibility.Take(70).ToImmutableArray(),
            AdditionalMachines = additions.Take(7).Select(m => m with { Compatibility = m.Compatibility.Take(70).ToImmutableArray() }).ToImmutableArray()
        });
        string[] symbols = ["GROWLITHE", "PONYTA", "TENTACOOL"]; int[] ids = [58, 77, 72];
        for (int n = 0; n < 3; n++)
        {
            var c = catalog.Compatibility[n + 70];
            if (c.Species != new SpeciesId("pret:SPECIES_" + symbols[n]) || c.SourceSpeciesId != ids[n])
                throw new NotSupportedException("S.S. Anne machine species identity differs.");
        }
        var values = new[] { PewterMachineRules.Definition(catalog, PewterMachineRules.Tm39Id)! }.Concat(additions).ToArray();
        Expected[] rows = gym ? [.. Rows, new("TM34", 322, 3000, "SHOCK_WAVE", 351, 20,
            "0000000000000000001111000000000001111100010000001111111111000000000011000")] : Rows;
        for (int n = 0; n < rows.Length; n++)
        {
            var e = rows[n]; var v = values[n]; var i = v.Machine.Item; bool hm = e.Item == "HM01";
            if (v.Move != new MoveId("pret:MOVE_" + e.Move) || v.SourceMoveId != e.MoveId || v.MovePp != e.Pp ||
                v.Compatibility.IsDefault || v.Compatibility.Length != 73 || i.Id != new ItemId("pret:ITEM_" + e.Item) ||
                i.SourceItemSymbol != "ITEM_" + e.Item || i.SourceItemId != e.Id || i.Name != e.Item || i.Price != e.Price ||
                i.Importance != (hm ? 1 : 0) || i.Registrability != 0 || i.Pocket != "POCKET_TM_CASE" ||
                i.Type != "ITEM_TYPE_PARTY_MENU" || i.FieldUseFunction != "NULL" || i.HoldEffect != "HOLD_EFFECT_NONE" ||
                i.HoldEffectParam != 0 || i.BattleUsage != 0 || i.BattleUseFunction != "NULL" || i.SecondaryId != 0 ||
                i.CanToss == hm || i.CanSell == hm || i.CanGive == hm || i.Source is null || v.Machine.DescriptionSource is null ||
                string.IsNullOrWhiteSpace(v.Machine.Description) || string.IsNullOrWhiteSpace(v.Machine.DeclarationDescription))
                throw new NotSupportedException("S.S. Anne machine metadata differs.");
            for (int x = 0; x < 73; x++)
            {
                var c = v.Compatibility[x]; var original = catalog.Compatibility[x];
                if (c.Species != original.Species || c.SourceSpeciesId != original.SourceSpeciesId || c.Source != original.Source ||
                    c.CanLearn != (e.Compatibility[x] == '1') || c.Source is not { Path: "src/data/pokemon/tmhm_learnsets.h" } ||
                    c.Source.StartLine < 1 || c.Source.EndLine < c.Source.StartLine)
                    throw new NotSupportedException("S.S. Anne machine compatibility differs.");
            }
        }
    }
}
