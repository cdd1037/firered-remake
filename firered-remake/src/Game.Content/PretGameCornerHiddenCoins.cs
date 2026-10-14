using System.Collections.Immutable;
using System.Security.Cryptography;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

/// <summary>The twelve original fixed coin backgrounds; reuses existing source bytes and map resources.</summary>
public static class PretGameCornerHiddenCoins
{
    public const string ScriptPath = "data/scripts/obtain_item.inc";
    public const string ScriptsSha256 = "11b0900307e6c83225abd205f0073528921c81c4e101eed14d4515a022b3c30d";
    private static readonly (string Path, string Hash)[] Inputs = [
        ("content/raw/rocket-hideout-region/data/maps/CeladonCity_GameCorner/map.json", "e0924467ae57d2fce051f3959bf1526bcb0befce6a6c39a51162aa2089f555ee"),
        ("content/raw/bill-region/data/scripts/obtain_item.inc", "11b0900307e6c83225abd205f0073528921c81c4e101eed14d4515a022b3c30d"),
        ("content/raw/bill-region/data/text/obtain_item.inc", "dad5d2666cd24ef854bb1ac7533dd3f53ffdcfee1528a48d436762fe00b2c813"),
        ("content/raw/early-pickups/src/field_control_avatar.c", "305daa295f88ce682b2b1868700d2f76012c659cb0df2905e6177a51ee4e9b8d"),
        ("content/raw/early-pickups/src/field_specials.c", "9c49d3702f0f4dcba61ae26bccefce1c26fbd1247475289dd821780bc537d1d9"),
        ("content/raw/pokemon-tower-items/src/itemfinder.c", "7f240eea2f57ac87d922e18fa960be8ec0a7d39c65c3c7cbb9c09222eaed9a30"),
        ("content/raw/game-corner-coins/src/coins.c", "ac223117abbde9e09abd13d75810e5f84d0fa7b03723b7a103655b55d7096b7f"),
        ("content/raw/bill-region/include/constants/flags.h", "45f31e4dc3d42c1054463974cd31b1173bdd4cb6d9f94b2cdbf2c88b0f745493"),
    ];
    public static ImmutableArray<string> AdditionalSourceHashes { get; } =
        ["game-corner-hidden-coins:firered-fixed-hidden-coins-v1", .. Inputs.Select(i => "hidden-coins:" + i.Path + ":" + i.Hash)];

    public static void ValidateSourceEvidence(string root)
    {
        foreach (var (path, hash) in Inputs)
        {
            var bytes = PretRuntimeMapValidation.ReadBounded(PretRuntimeMapValidation.SafeFile(root, path), 128 * 1024);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("hidden_coins_source_bytes:" + path);
        }
    }

    internal static ImmutableArray<FieldPickupDefinition> Pickups(PretWorldMap map, PretStoryCompilation scripts)
    {
        var backgrounds = map.Events.GetProperty("bg_events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "hidden_item" && e.GetProperty("item").GetString() == "ITEM_NONE").ToArray();
        if (map.Id.Value != GameCornerCoinSource.Map || map.SourceMapSymbol != "MAP_CELADON_CITY_GAME_CORNER" ||
            backgrounds.Length != 12 || backgrounds.Sum(e => e.GetProperty("quantity").GetInt32()) != 260 ||
            !scripts.TryConstant("ITEM_NONE", out int itemId) || itemId != 0)
            throw new InvalidDataException("hidden_coins_map_scope");
        var result = ImmutableArray.CreateBuilder<FieldPickupDefinition>();
        foreach (var bg in backgrounds)
        {
            string flag = bg.GetProperty("flag").GetString()!;
            int quantity = bg.GetProperty("quantity").GetInt32();
            if (bg.GetProperty("underfoot").GetBoolean() || quantity is < 1 or > 127 ||
                !scripts.TryConstant(flag, out int flagId))
                throw new InvalidDataException("hidden_coins_background:" + flag);
            // Position, amount and one-time flag come directly from authenticated map data.
            result.Add(new("hidden-coins:" + flag, map.SourceMapSymbol, true, null,
                bg.GetProperty("x").GetInt32(), bg.GetProperty("y").GetInt32(), bg.GetProperty("elevation").GetInt32(),
                "EventScript_HiddenItemScript", "ITEM_NONE", 0, new ItemId("pret:ITEM_NONE"), "COINS", "", quantity, flag, flagId));
        }
        return result.ToImmutable();
    }
}
