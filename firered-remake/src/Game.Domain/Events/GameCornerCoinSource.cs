using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Events;

/// <summary>Original finite coin services. Slot play, prizes and field coin pickups are separate owners.</summary>
public static class GameCornerCoinSource
{
    public const int Maximum = 9999;
    public const string Map = "map.celadon-city-game-corner";
    public const string Path = "data/maps/CeladonCity_GameCorner/scripts.inc";
    public const string Prefix = "CeladonCity_GameCorner_EventScript_";
    public const string Clerk = Prefix + "CoinsClerk";
    public const string RestaurantMap = "map.celadon-city-restaurant";
    public const string RestaurantPath = "data/maps/CeladonCity_Restaurant/scripts.inc";
    public const string CoinCaseMan = "CeladonCity_Restaurant_EventScript_CoinCaseMan";
    public const string CaseFlag = "FLAG_GOT_COIN_CASE";
    public static ItemId CoinCase { get; } = new("pret:ITEM_COIN_CASE");
    public static ImmutableArray<string> GiftFlags { get; } = ["FLAG_GOT_10_COINS_FROM_GAMBLER", "FLAG_GOT_20_COINS_FROM_GAMBLER", "FLAG_GOT_20_COINS_FROM_GAMBLER_2"];
    public static ImmutableArray<string> Rows { get; } = ["50 COINS", "500 COINS", "EXIT"];
    public static bool IsMenu(StoryInstruction? instruction) => instruction is { Op: StoryOp.Multichoice, Opcode: "multichoice" } &&
        instruction.Source.Path == Path && instruction.Source.Symbol == Clerk &&
        instruction.Args.SequenceEqual(new[] { "13", "0", "MULTICHOICE_GAME_CORNER_COIN_PURCHASE_COUNTER", "FALSE" });
    public static StoryInstruction? Instruction(StoryState state, IStoryCatalog catalog)
    {
        if (state.Stack.IsEmpty) return null;
        var frame = state.Stack[^1]; var program = catalog.FindProgram(frame.ProgramId); int index = frame.InstructionIndex - 1;
        return program is not null && index >= 0 && index < program.Instructions.Length ? program.Instructions[index] : null;
    }
    public static bool Pending(StoryState state, IStoryCatalog catalog) => state.Wait?.Kind == StoryWaitKind.MenuChoice && IsMenu(Instruction(state, catalog));

    // src/coins.c AddCoins saturates at MAX_COINS and fails only when already full.
    // Admitted gift/purchase scripts check capacity first; retain the original helper/result convention.
    public static (int Coins, int ScriptResult) Add(int current, int amount)
    {
        if (current is < 0 or > Maximum || amount is < 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException();
        return current == Maximum ? (current, 1) : (Math.Min(Maximum, current + amount), 0);
    }
}
