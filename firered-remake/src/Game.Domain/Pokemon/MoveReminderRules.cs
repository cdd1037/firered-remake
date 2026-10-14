using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Domain.Pokemon;

/// <summary>Original FireRed level-up move reminder, bound to the current authenticated owned catalog.</summary>
public sealed class MoveReminderCatalog
{
    public const string PokemonSourceSha256 = "116a470cd9ebfa226aa8d85c2eb63591e176d726837c67baaa1ac6260d50f2ec";
    public const string LearnMoveSourceSha256 = "8a3f0eb4a475633477bda6d65396205dfbb29db04dabb9f5f0d99df9f0c1ef40";
    public const string StringsSha256 = "08e3799f20dd90ae937808be25721d28c3465b4f2ebaba2e71321e52f72465c1";
    public PokemonCatalog Pokemon { get; }
    public ImmutableDictionary<string, string> Texts { get; }

    private MoveReminderCatalog(PokemonCatalog pokemon, ImmutableDictionary<string, string> texts)
    { Pokemon = pokemon; Texts = texts; }

    public static MoveReminderCatalog Authenticate(PokemonCatalog owned, ReadOnlySpan<byte> pokemonSource,
        ReadOnlySpan<byte> learnMoveSource, ReadOnlySpan<byte> strings)
    {
        if (OwnedBattlePokemonCatalog.For(owned) is null || owned.SourceCommit != PokemonFactory.SourceCommit ||
            Convert.ToHexStringLower(SHA256.HashData(pokemonSource)) != PokemonSourceSha256 ||
            Convert.ToHexStringLower(SHA256.HashData(learnMoveSource)) != LearnMoveSourceSha256 ||
            Convert.ToHexStringLower(SHA256.HashData(strings)) != StringsSha256)
            throw new ArgumentException("move_reminder_source_authentication");
        var source = Encoding.UTF8.GetString(strings);
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (string symbol in new[] { "gText_TeachWhichMoveToMon", "gText_TeachMoveQues", "gText_MonLearnedMove",
            "gText_MonIsTryingToLearnMove", "gText_StopLearningMove", "gText_1_2_and_Poof",
            "gText_MonForgotOldMoveAndMonLearnedNewMove", "gText_GiveUpTryingToTeachNewMove",
            "gText_WhichMoveShouldBeForgotten", "gText_PokeSum_HmMovesCantBeForgotten",
            "gText_2Tinymushrooms", "gText_1BigMushroom", "gText_TeachWhichPokemon" })
        {
            var match = Regex.Match(source, "const u8 " + symbol + "\\[\\] = _\\(\"([^\"]*)\"\\);", RegexOptions.CultureInvariant);
            if (!match.Success) throw new ArgumentException("move_reminder_text_missing: " + symbol);
            texts.Add(symbol, match.Groups[1].Value);
        }
        return new(owned, texts.ToImmutable());
    }
}

public sealed record MoveReminderCandidates(ImmutableArray<MoveId> Moves, string? Error = null);
public enum MoveReminderPayment { TinyMushrooms, BigMushroom }
public sealed record MoveReminderPayments(bool TinyMushrooms, bool BigMushroom, string? Error = null)
{
    public bool Any => TinyMushrooms || BigMushroom;
}

/// <summary>A successful lesson is produced only by Teach. The Application owns its one-time consumption.</summary>
public sealed class MoveReminderLesson
{
    public PartyPokemon Pokemon { get; }
    public MoveId Move { get; }
    public int LearnedSlot { get; }
    public string? Error { get; }
    public bool Succeeded => Error is null;
    internal MoveReminderLesson(PartyPokemon pokemon, MoveId move, int slot, string? error)
    { Pokemon = pokemon; Move = move; LearnedSlot = slot; Error = error; }
}

public static class MoveReminderRules
{
    public static ItemId TinyMushroomId { get; } = new("pret:ITEM_TINY_MUSHROOM");
    public static ItemId BigMushroomId { get; } = new("pret:ITEM_BIG_MUSHROOM");

    /// <summary>GetMoveRelearnerMoves: current species only, source order, at or below current level,
    /// excluding every already-known move and later duplicate. No TM/tutor/egg/pre-evolution backfill.</summary>
    public static MoveReminderCandidates Candidates(MoveReminderCatalog catalog, PartyPokemon mon)
    {
        if (OwnedBattlePokemonCatalog.For(catalog.Pokemon) is null) return new([], "move_reminder_pokemon_catalog");
        string? error = PokemonStateValidator.Validate(catalog.Pokemon, mon.Pokemon);
        if (error is not null) return new([], error);
        if (mon.IsEgg) return new([], "move_reminder_egg");
        var known = mon.Pokemon.Moves.Select(m => m.Move).ToHashSet();
        var result = ImmutableArray.CreateBuilder<MoveId>();
        foreach (var entry in catalog.Pokemon.Species[mon.Pokemon.Species].LevelUpMoves)
            if (entry.Level <= mon.Pokemon.Level && known.Add(entry.Move)) result.Add(entry.Move);
        return new(result.ToImmutable());
    }

    /// <summary>Teaching alone never consumes a mushroom. Native success precedes the field payment script.</summary>
    public static MoveReminderLesson Teach(MoveReminderCatalog catalog, PokemonCatalog owned, PartyPokemon mon,
        MoveId move, int? replacement = null)
    {
        MoveReminderLesson Reject(string error) => new(mon, move, -1, error);
        if (!ReferenceEquals(catalog.Pokemon, owned)) return Reject("move_reminder_pokemon_catalog");
        var candidates = Candidates(catalog, mon);
        if (candidates.Error is not null) return Reject(candidates.Error);
        if (!candidates.Moves.Contains(move)) return Reject("move_reminder_not_candidate");
        // Never silently hide a source candidate whose battle implementation is missing.
        if (!PokemonMoveRules.IsLearningSupported(owned, move)) return Reject("move_reminder_move_unsupported");
        var before = mon.Pokemon.Moves;
        if (before.Length < 4)
        {
            if (replacement is not null) return Reject("move_reminder_invalid_slot");
            var learned = PokemonMoveRules.TryLearn(owned, before, move);
            if (learned.Outcome != MoveLearningOutcome.Added) return Reject("move_reminder_move_unsupported");
            return new(mon with { Pokemon = mon.Pokemon with { Moves = learned.Moves } }, move, before.Length, null);
        }
        if (replacement is null) return Reject("move_reminder_replacement_required");
        if (replacement is < 0 or > 3) return Reject("move_reminder_invalid_slot");
        if (PokemonMoveRules.IsHiddenMachineMove(before[replacement.Value].Move)) return Reject("move_reminder_cannot_forget_hm");
        return new(mon with { Pokemon = mon.Pokemon with { Moves = PokemonMoveRules.Replace(owned, before, replacement.Value, move) } },
            move, replacement.Value, null);
    }

    /// <summary>Only mushrooms in the Bag pay; held items and PC storage are deliberately excluded.</summary>
    public static MoveReminderPayments PaymentOptions(EarlyShopCatalog catalog, InventoryState inventory)
    {
        string? error = EarlyShopInventoryRules.Validate(catalog, inventory);
        if (error is not null) return new(false, false, error);
        var tiny = EarlyShopInventoryRules.Find(catalog, TinyMushroomId);
        var big = EarlyShopInventoryRules.Find(catalog, BigMushroomId);
        if (tiny is not { SourceItemId: 103, Pocket: "POCKET_ITEMS" } || big is not { SourceItemId: 104, Pocket: "POCKET_ITEMS" })
            return new(false, false, "move_reminder_mushroom_catalog");
        return new(OpeningInventoryRules.Quantity(inventory.BagItems, TinyMushroomId) >= 2,
            OpeningInventoryRules.Quantity(inventory.BagItems, BigMushroomId) >= 1);
    }

    /// <summary>Called once at the source removeitem after successful learning. No free Bag slot is needed.
    /// Both options must be shown in source order, Tiny then Big, with cancellation disabled.</summary>
    public static InventoryTransactionResult PayAfterLearning(EarlyShopCatalog catalog, InventoryState inventory,
        MoveReminderLesson lesson, MoveReminderPayment payment)
    {
        if (!lesson.Succeeded) return new(inventory, false, "move_reminder_lesson_not_learned");
        var options = PaymentOptions(catalog, inventory);
        if (options.Error is not null) return new(inventory, false, options.Error);
        if (payment is not (MoveReminderPayment.TinyMushrooms or MoveReminderPayment.BigMushroom))
            return new(inventory, false, "move_reminder_payment_invalid");
        if (payment == MoveReminderPayment.TinyMushrooms ? !options.TinyMushrooms : !options.BigMushroom)
            return new(inventory, false, "inventory_insufficient_quantity");
        return EarlyInventoryAddRules.Remove(catalog, inventory,
            payment == MoveReminderPayment.TinyMushrooms ? TinyMushroomId : BigMushroomId,
            payment == MoveReminderPayment.TinyMushrooms ? 2 : 1);
    }
}
