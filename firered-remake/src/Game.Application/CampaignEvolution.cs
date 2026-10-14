using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

public enum CampaignEvolutionStage { Proposal, Results, LearnMove }
public sealed record CampaignItemEvolution(ItemEvolutionEligibility Eligibility, CampaignItemMenu ReturnMenu);
public sealed record CampaignEvolution(string? BattleId, ImmutableArray<LevelEvolutionEligibility> Proposals,
    int ProposalIndex, CampaignEvolutionStage Stage, ImmutableArray<string> Pages, int PageIndex,
    ImmutableArray<LevelEvolutionPendingMove> PendingMoves)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CampaignItemEvolution? ItemUse { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CampaignCandyEvolution? CandyUse { get; init; }
    // Preserve the existing serialized/UI projection for older level scenes. An item scene has
    // no level proposals or battle; this same-level projection is display data only and is never
    // passed to LevelEvolutionRules.Apply/Cancel.
    public LevelEvolutionEligibility Proposal => ItemUse is { Eligibility: var item }
        ? new(item.InstanceId, item.PreviousSpecies, item.TargetSpecies, item.Level, item.Level, item.Source)
        : Proposals[ProposalIndex];
}
// The capture ledger always retains the originally caught species. This separate source-ordered
// ledger proves every later species change, including intermediate evolutions no longer in party.
public sealed record CampaignNormalEvolutionTrigger(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Friendship = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Attack = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Defense = null);
public sealed record CampaignEvolutionHistory(long InstanceId, string? BattleId, int PreviousSpeciesId,
    int TargetSpeciesId, int LevelBeforeBattle, int Level)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Item { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CampaignNormalEvolutionTrigger? NormalTrigger { get; init; }
}

public sealed partial class CampaignSession
{
    public const string EvolutionCountStat = "GAME_STAT_EVOLVED_POKEMON";

    // Shared capability and menu label: presentation never infers execution from a historical
    // item's pickup-only UseSupport value or from an opponent-only source species definition.
    public bool FieldEvolutionSupported(ItemId item) => _content.MtMoonEnabled &&
        _content.EarlyShop is { } shop && SourceFieldEvolutionRules.Supports(shop, item);

    public SpeciesId? FieldEvolutionPartyTarget(PartyPokemon owned, ItemId item)
    {
        if (!FieldEvolutionSupported(item) || owned.IsEgg) return null;
        var target = ItemEvolutionRules.PartyMenuTarget(_content.PokemonContent!, _binding.Ownership,
            owned, item.Value["pret:".Length..]);
        return target is { } species && (NationalDexEnabledIn(State) ||
            BattlePokemon.Species[species].SourceSpeciesId <= 151) ? target : null;
    }

    private Decision<CampaignState> UseFieldEvolution(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (!_content.MtMoonEnabled || _content.EarlyShop is not { } shop ||
            state.ItemMenu is not { Origin: "bag", View: "bag" } menu || state.Inventory is null ||
            state.Evolution is not null || state.WildBattle is not null || state.ForestTrainerBattle is not null || state.DoubleBattle is not null)
            return No("field_evolution_context_invalid");
        if (string.IsNullOrEmpty(command.Text) || !FieldEvolutionSupported(new(command.Text)))
            return No("evolution_item_unsupported");
        var evolutionItem = new ItemId(command.Text);
        if (command.PartyIndex < 0 || command.PartyIndex >= state.Party.Length)
            return No("item_party_index_invalid");
        if (command.MoveSlot != -1 || command.Quantity != 1) return No("evolution_item_command_invalid");
        var inventoryError = ValidateCampaignInventory(state.Inventory);
        if (inventoryError is not null) return No(inventoryError);
        // Preflight consumption against the narrowed inventory, but publish no partial result.
        var consumed = EarlyInventoryAddRules.Remove(shop, ShopInventoryView(state.Inventory), evolutionItem, 1);
        if (!consumed.Succeeded) return No(consumed.Error!);
        var owned = state.Party[command.PartyIndex];
        if (command.TargetInstanceId is { } targetInstance && targetInstance != owned.InstanceId)
            return No("evolution_target_changed");
        var stone = evolutionItem.Value["pret:".Length..];
        var proposal = owned.IsEgg ? null : ItemEvolutionRules.Eligibility(_content.PokemonContent!, _binding.Ownership, owned, stone);
        if (proposal is null || !NationalDexEnabledIn(state) && BattlePokemon.Species[proposal.TargetSpecies].SourceSpeciesId > 151)
            return new(CommandStatus.Accepted, state with { ItemMenu = menu with
                { View = "message", MessageSymbol = "gText_WontHaveEffect", ReturnView = "bag" } });
        var evolved = ItemEvolutionRules.Apply(_content.PokemonContent!, _binding.Ownership, owned, proposal);
        int target = evolved.Pokemon.Pokemon.SourceSpeciesId;
        var pages = ImmutableArray.CreateBuilder<string>();
        pages.Add($"Congratulations! Your {owned.Nickname}\nevolved into {_content.PokemonContent!.SpeciesNames[target]}!");
        foreach (var learned in evolved.LearnedMoves)
            pages.Add($"{evolved.Pokemon.Nickname} learned\n{BattleContent.MoveNames[learned.Move.Value]}!");
        var stats = state.GameStats ?? ImmutableSortedDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal);
        return new(CommandStatus.Accepted, state with
        {
            Inventory = MergeShopInventory(state.Inventory, consumed.State),
            Party = state.Party.SetItem(command.PartyIndex, evolved.Pokemon),
            SeenSpecies = state.SeenSpecies.Add(target), CaughtSpecies = state.CaughtSpecies.Add(target),
            EvolutionHistory = (state.EvolutionHistory ?? []).Add(new(owned.InstanceId, null,
                owned.Pokemon.SourceSpeciesId, target, owned.Pokemon.Level, owned.Pokemon.Level)
                { Item = evolutionItem.Value }),
            GameStats = stats.SetItem(EvolutionCountStat, Math.Min(0xffffff, stats.GetValueOrDefault(EvolutionCountStat) + 1)),
            ItemMenu = null,
            Evolution = new(null, [], 0, CampaignEvolutionStage.Results, pages.ToImmutable(), 0,
                evolved.PendingMoves.Select(m => new LevelEvolutionPendingMove(m.Level, m.Move)).ToImmutableArray())
                { ItemUse = new(proposal, menu) }
        });
    }

    private ImmutableArray<LevelEvolutionEligibility> EligibleLevelEvolutions(CampaignState state, ImmutableArray<int> levels) =>
        LevelEvolutionRules.ForParty(_content.PokemonContent!, state.Party, levels)
            .Where(p => NationalDexEnabledIn(state) || BattlePokemon.Species[p.TargetSpecies].SourceSpeciesId <= 151).ToImmutableArray();

    private CampaignState BeginWildEvolution(CampaignState state)
    {
        if (!_content.ForestEnabled) return ReturnFromWildBattle(state);
        var battle = state.WildBattle!;
        if (battle.Frame is not {Outcome:WildBattleOutcome.Won,Rewards.Pending:null} ||
            battle.EntryLevels.IsDefault||battle.EntryLevels.Length!=state.Party.Length||!battle.PendingMoves.IsEmpty||state.Evolution is not null)
            throw new InvalidDataException("evolution_reward_context_invalid");
        var levels=battle.EntryLevels;
        var proposals = EligibleLevelEvolutions(state, levels);
        return proposals.IsEmpty ? ReturnFromWildBattle(state) : state with
        {
            Evolution = new(battle.Id, proposals, 0, CampaignEvolutionStage.Proposal, [], 0, [])
        };
    }

    private Decision<CampaignState> DecideEvolution(CampaignState state, CampaignCommand command)
    {
        var scene = state.Evolution!;
        bool itemScene = scene.ItemUse is not null;
        bool candyScene = scene.CandyUse is not null;
        if (!_content.ForestEnabled || (candyScene
            ? !_content.MtMoonEnabled || itemScene || scene.BattleId is not null || state.WildBattle is not null || state.ForestTrainerBattle is not null
            : itemScene
            ? !_content.MtMoonEnabled || scene.BattleId is not null || state.WildBattle is not null ||
                state.ForestTrainerBattle is not null || scene.Stage == CampaignEvolutionStage.Proposal
            : scene.BattleId is null || (state.WildBattle?.Id != scene.BattleId && state.ForestTrainerBattle?.Id != scene.BattleId && state.DoubleBattle?.Id != scene.BattleId)))
            return new(CommandStatus.Fault, state, "evolution_context_invalid");
        int index = EvolutionPartyIndex(state, scene);
        var owned = state.Party[index];
        if (!itemScene && command.Kind == "evolution_choice" && scene.Stage == CampaignEvolutionStage.Proposal)
        {
            if (command.Choice is not (0 or 1)) return new(CommandStatus.Rejected, state, "evolution_choice_invalid");
            var result = command.Choice == 1
                ? LevelEvolutionRules.Apply(_content.PokemonContent!, owned, scene.Proposal)
                : LevelEvolutionRules.Cancel(_content.PokemonContent!, owned, scene.Proposal);
            var pages = ImmutableArray.CreateBuilder<string>();
            var next = state;
            if (result.Outcome == LevelEvolutionOutcome.Evolved)
            {
                int target = result.Pokemon.Pokemon.SourceSpeciesId;
                pages.Add($"Congratulations! Your {owned.Nickname}\nevolved into {_content.PokemonContent!.SpeciesNames[target]}!");
                foreach (var learned in result.LearnedMoves)
                    pages.Add($"{result.Pokemon.Nickname} learned\n{BattleContent.MoveNames[learned.Move.Value]}!");
                var history = new CampaignEvolutionHistory(owned.InstanceId, scene.BattleId,
                    owned.Pokemon.SourceSpeciesId, target, scene.Proposal.LevelBeforeBattle, scene.Proposal.Level) { Item = candyScene ? RareCandyItem : null,
                    NormalTrigger = NormalEvolutionTrigger(BattlePokemon.Species[owned.Pokemon.Species].Evolutions.Single(e => e.Target == result.TargetSpecies && e.Source == scene.Proposal.Source).Method, owned.Pokemon) };
                var stats = state.GameStats ?? ImmutableSortedDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal);
                next = state with
                {
                    Party = state.Party.SetItem(index, result.Pokemon),
                    SeenSpecies = state.SeenSpecies.Add(target), CaughtSpecies = state.CaughtSpecies.Add(target),
                    EvolutionHistory = (state.EvolutionHistory ?? []).Add(history),
                    GameStats = stats.SetItem(EvolutionCountStat, Math.Min(0xffffff, stats.GetValueOrDefault(EvolutionCountStat) + 1))
                };
            }
            else pages.Add($"Huh? {owned.Nickname}\nstopped evolving!");
            return new(CommandStatus.Accepted, next with
            {
                Evolution = scene with { Stage = CampaignEvolutionStage.Results, Pages = pages.ToImmutable(), PageIndex = 0, PendingMoves = result.PendingMoves }
            });
        }
        if (command.Kind == "ack" && scene.Stage == CampaignEvolutionStage.Results)
            return new(CommandStatus.Accepted, scene.PageIndex + 1 < scene.Pages.Length
                ? state with { Evolution = scene with { PageIndex = scene.PageIndex + 1 } }
                : AdvanceEvolutionMoves(state));
        if (command.Kind == "evolution_learn" && scene.Stage == CampaignEvolutionStage.LearnMove)
        {
            if (command.MoveSlot < -1 || command.MoveSlot >= owned.Pokemon.Moves.Length)
                return new(CommandStatus.Rejected, state, "evolution_learning_slot_invalid");
            if (_content.SsAnneEnabled && command.MoveSlot >= 0 && command.MoveSlot < owned.Pokemon.Moves.Length && PokemonMoveRules.IsHiddenMachineMove(owned.Pokemon.Moves[command.MoveSlot].Move))
                return new(CommandStatus.Rejected, state, "hm_move_cannot_be_forgotten");
            var move = scene.PendingMoves[0];
            if (command.MoveSlot >= 0 && !PokemonMoveRules.IsLearningSupported(BattlePokemon, move.Move))
                return new(CommandStatus.Rejected, state, "pokemon_learning_effect_unsupported");
            if (command.MoveSlot >= 0)
                owned = ReplaceOwnedLevelMove(owned, command.MoveSlot, move.Move);
            string message = command.MoveSlot < 0
                ? $"{owned.Nickname} did not learn\n{BattleContent.MoveNames[move.Move.Value]}."
                : $"{owned.Nickname} learned\n{BattleContent.MoveNames[move.Move.Value]}!";
            return new(CommandStatus.Accepted, state with
            {
                Party = state.Party.SetItem(index, owned),
                Evolution = scene with { Stage = CampaignEvolutionStage.Results, Pages = [message], PageIndex = 0, PendingMoves = scene.PendingMoves.RemoveAt(0) }
            });
        }
        return new(CommandStatus.Rejected, state, "evolution_decision_required");
    }

    private CampaignState AdvanceEvolutionMoves(CampaignState state)
    {
        var scene = state.Evolution!;
        int index = EvolutionPartyIndex(state, scene);
        while (!scene.PendingMoves.IsEmpty)
        {
            var owned = state.Party[index]; var move = scene.PendingMoves[0];
            // Recheck now: earlier explicit replacement decisions may have changed knowledge.
            var learning = LearnOwnedLevelMove(owned, move.Move);
            if (learning.Outcome == MoveLearningOutcome.NeedsReplacement)
                return state with { Evolution = scene with { Stage = CampaignEvolutionStage.LearnMove, Pages = [], PageIndex = 0 } };
            scene = scene with { PendingMoves = scene.PendingMoves.RemoveAt(0) };
            if (learning.Outcome == MoveLearningOutcome.Added)
                return state with
                {
                    Party = state.Party.SetItem(index, owned with { Pokemon = owned.Pokemon with { Moves = learning.Moves } }),
                    Evolution = scene with { Stage = CampaignEvolutionStage.Results, Pages = [$"{owned.Nickname} learned\n{BattleContent.MoveNames[move.Move.Value]}!"], PageIndex = 0 }
                };
        }
        if (scene.CandyUse is { } candyUse)
            return state with { Evolution = null, ItemMenu = candyUse.ReturnMenu };
        if (scene.ItemUse is { } itemUse)
            return state with { Evolution = null, ItemMenu = itemUse.ReturnMenu };
        if (scene.ProposalIndex + 1 < scene.Proposals.Length)
            return state with { Evolution = scene with { ProposalIndex = scene.ProposalIndex + 1, Stage = CampaignEvolutionStage.Proposal, Pages = [], PageIndex = 0, PendingMoves = [] } };
        // Never rebuild the proposal list here: one member gets one attempt per won battle.
        return state.DoubleBattle is not null ? ReturnFromDoubleBattle(state with { Evolution = null })
            : state.ForestTrainerBattle is not null ? ReturnFromForestTrainerBattle(state with { Evolution = null })
            : ReturnFromWildBattle(state with { Evolution = null });
    }

    private static int EvolutionPartyIndex(CampaignState state, CampaignEvolution scene)
    {
        for (int i = 0; i < state.Party.Length; i++) if (state.Party[i].InstanceId == scene.Proposal.InstanceId) return i;
        throw new InvalidDataException("evolution_member_missing");
    }

    // Level/evolution decisions act on owned members, whose unaffected moves can have PP Ups.
    // The creation-only PokemonMoveRules helpers intentionally reject that wider state.
    private MoveLearningResult LearnOwnedLevelMove(PartyPokemon owned, MoveId move)
    {
        if (PokemonStateValidator.ValidateParty(_content.PokemonContent!, owned) is { } error)
            throw new ArgumentException(error, nameof(owned));
        var moves = owned.Pokemon.Moves;
        var definition = BattlePokemon.Moves[move];
        return moves.Any(m => m.Move == move) ? new(MoveLearningOutcome.AlreadyKnown, moves)
            : !PokemonMoveRules.IsLearningSupported(BattlePokemon, move) || moves.Length == 4 ? new(MoveLearningOutcome.NeedsReplacement, moves)
            : new(MoveLearningOutcome.Added, moves.Add(new(move, definition.Pp, definition.Pp)));
    }

    private PartyPokemon ReplaceOwnedLevelMove(PartyPokemon owned, int slot, MoveId move)
    {
        if (PokemonStateValidator.ValidateParty(_content.PokemonContent!, owned) is { } error)
            throw new ArgumentException(error, nameof(owned));
        if (slot < 0 || slot >= owned.Pokemon.Moves.Length || owned.Pokemon.Moves.Any(m => m.Move == move))
            throw new ArgumentException("Invalid owned level-move replacement.");
        if (_content.SsAnneEnabled && PokemonMoveRules.IsHiddenMachineMove(owned.Pokemon.Moves[slot].Move))
            throw new ArgumentException("An HM move cannot be forgotten here.");
        if (!PokemonMoveRules.IsLearningSupported(BattlePokemon, move))
            throw new NotSupportedException("pokemon_learning_effect_unsupported");
        var definition = BattlePokemon.Moves[move];
        return owned with { Pokemon = owned.Pokemon with
            { Moves = owned.Pokemon.Moves.SetItem(slot, new(move, definition.Pp, definition.Pp)) } };
    }

    private bool EvolutionSpeciesMatchesOrigin(CampaignState state, PartyPokemon owned, int originalSpecies)
    {
        int species = originalSpecies;
        if (_content.ForestEnabled && state.EvolutionHistory is { } history)
            foreach (var entry in history.Where(e => e is not null && e.InstanceId == owned.InstanceId))
            {
                if (entry.PreviousSpeciesId != species) return false;
                species = entry.TargetSpeciesId;
            }
        return species == owned.Pokemon.SourceSpeciesId;
    }

    private IEnumerable<int> EvolvedSpecies(CampaignState state) => _content.ForestEnabled && state.EvolutionHistory is { } history
        ? history.Where(e => e is not null).Select(e => e.TargetSpeciesId) : [];

    private static CampaignNormalEvolutionTrigger? NormalEvolutionTrigger(PokemonEvolutionMethod method, PokemonInstance pokemon) =>
        method == PokemonEvolutionMethod.Level ? null : method == PokemonEvolutionMethod.Friendship
            ? new(Friendship: pokemon.Friendship) : new(Attack: pokemon.Stats.Attack, Defense: pokemon.Stats.Defense);

    private static bool ValidNormalEvolutionTrigger(PokemonEvolution evolution, CampaignEvolutionHistory receipt)
    {
        if (evolution.Method == PokemonEvolutionMethod.Level)
            return receipt.NormalTrigger is null && SourceLevelEvolutionProjection.Qualifies(evolution, receipt.Level, 0, 0, 0);
        if (receipt.NormalTrigger is not {} trigger) return false;
        if (evolution.Method == PokemonEvolutionMethod.Friendship
            ? trigger.Friendship is not (>= 0 and <= 255) || trigger.Attack is not null || trigger.Defense is not null
            : trigger.Friendship is not null || trigger.Attack is not (> 0 and <= 999) || trigger.Defense is not (> 0 and <= 999)) return false;
        return SourceLevelEvolutionProjection.Qualifies(evolution, receipt.Level, trigger.Friendship ?? 0, trigger.Attack ?? 0, trigger.Defense ?? 0);
    }

    private string? ValidateEvolutionHistory(CampaignState state)
    {
        if (!_content.ForestEnabled)
            return state.Evolution is not null || state.EvolutionHistory is not null ? "save_evolution_profile_missing" : null;
        if (state.Evolution is not null) return "save_event_active";
        var history = state.EvolutionHistory ?? [];
        var allOwned = HistoricalPokemon(state);
        if (history.IsDefault || history.Length > allOwned.Length * 2) return "save_evolution_history_invalid";
        var used = new HashSet<(string?, long)>();
        var latestLevel = new Dictionary<long, int>();
        var byNumber = _content.PokemonContent!.Catalog.Species.Values.ToDictionary(s => s.SourceSpeciesId);
        int lastWildBattle = -1, lastTrainerBattle = -1, lastDoubleBattle = -1, lastParty = -1;
        string? lastBattleId = null;
        var closedBattles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in history)
        {
            if (entry is null || !used.Add((entry.Item == RareCandyItem ? "candy:" + entry.Level : entry.BattleId, entry.InstanceId)) ||
                !byNumber.TryGetValue(entry.PreviousSpeciesId, out var previous) || !byNumber.TryGetValue(entry.TargetSpeciesId, out var target))
                return "save_evolution_history_invalid";
            bool item = entry.Item is not null;
            if (item && !_content.MtMoonEnabled) return "save_evolution_item_profile_missing";
            int partyIndex = -1;
            for (int i = 0; i < allOwned.Length; i++) if (allOwned[i].InstanceId == entry.InstanceId) partyIndex = i;
            if (partyIndex < 0 || entry.LevelBeforeBattle < allOwned[partyIndex].MetLevel ||
                latestLevel.TryGetValue(entry.InstanceId, out int previousLevel) && entry.LevelBeforeBattle < previousLevel ||
                entry.Level > allOwned[partyIndex].Pokemon.Level || (item
                    ? entry.BattleId is not null || (entry.Item == RareCandyItem
                        ? entry.Level != entry.LevelBeforeBattle + 1 || !previous.Evolutions.Any(e =>
                            e.Target == target.Id && ValidNormalEvolutionTrigger(e, entry))
                        : entry.NormalTrigger is not null || entry.Level != entry.LevelBeforeBattle || !(entry.Item is { } stoneItem && stoneItem.StartsWith("pret:", StringComparison.Ordinal) && ItemEvolutionRules.SourceItemId(stoneItem[5..]) is not null &&
                            ItemEvolutionRules.SourceEvolution(_content.PokemonContent!, _binding.Ownership, previous.Id,
                                stoneItem[5..])?.Target == target.Id))
                    : entry.Level <= entry.LevelBeforeBattle || !previous.Evolutions.Any(e =>
                        e.Target == target.Id && ValidNormalEvolutionTrigger(e, entry))))
                return "save_evolution_lineage_invalid";
            if (item)
            {
                // Item use has neither a won-battle requirement nor an invented battle receipt.
                // It closes the previous level-evolution group so that group cannot be reopened.
                if (lastBattleId is not null) closedBattles.Add(lastBattleId);
                lastBattleId = null; lastParty = -1;
            }
            else
            {
                if (entry.BattleId is null) return "save_evolution_battle_invalid";
                var battles = state.Exploration?.Battles ?? [];
                int battleIndex = -1;
                for (int i = 0; i < battles.Length; i++) if (battles[i].Id == entry.BattleId && battles[i].Outcome == WildBattleOutcome.Won && battles[i].ExperienceGained > 0) battleIndex = i;
                if (battleIndex >= 0)
                {
                    if (battleIndex < lastWildBattle) return "save_evolution_battle_invalid";
                    lastWildBattle = battleIndex;
                }
                else
                {
                    var trainers = state.ForestTrainerHistory ?? [];
                    for (int i = 0; i < trainers.Length; i++)
                        if (trainers[i].Id == entry.BattleId && trainers[i].Outcome == WildBattleOutcome.Won && trainers[i].ExperienceGained > 0) battleIndex = i;
                    if(battleIndex>=0)
                    {
                        if (!_content.ForestTrainersEnabled || battleIndex < lastTrainerBattle) return "save_evolution_battle_invalid";
                        lastTrainerBattle = battleIndex;
                    }
                    else
                    {
                        var doubles=state.DoubleBattleHistory??[];
                        for(int i=0;i<doubles.Length;i++)
                            if(doubles[i].Id==entry.BattleId&&doubles[i].Outcome==WildBattleOutcome.Won&&doubles[i].ExperienceGained>0)battleIndex=i;
                        if(!_content.PresetDoublesEnabled||battleIndex<0||battleIndex<lastDoubleBattle)return "save_evolution_battle_invalid";
                        lastDoubleBattle=battleIndex;
                    }
                }
                // Wild/trainer serials are independent. Storage can later change party order.
                if (!_content.PokemonStorageEnabled && lastBattleId == entry.BattleId && partyIndex <= lastParty || closedBattles.Contains(entry.BattleId))
                    return "save_evolution_battle_invalid";
                if (lastBattleId is not null && lastBattleId != entry.BattleId) closedBattles.Add(lastBattleId);
                lastBattleId = entry.BattleId; lastParty = partyIndex;
            }
            if (!state.CaughtSpecies.Contains(entry.PreviousSpeciesId) || !state.CaughtSpecies.Contains(entry.TargetSpeciesId) || !state.SeenSpecies.Contains(entry.TargetSpeciesId))
                return "save_evolution_dex_invalid";
            latestLevel[entry.InstanceId] = entry.Level;
        }
        if ((state.GameStats?.GetValueOrDefault(EvolutionCountStat) ?? 0) != history.Length) return "save_evolution_stat_invalid";
        return null;
    }
}
