using System.Collections.Immutable;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Application;

public enum CampaignRareCandyStage { LevelMessage, StatsGain, StatsTotal, MoveMessage, LearnMove }
public sealed record CampaignRareCandy(RareCandyResult Result, CampaignItemMenu ReturnMenu,
    CampaignRareCandyStage Stage, ImmutableArray<string> Pages, int PageIndex,
    ImmutableArray<LevelEvolutionPendingMove> PendingMoves);
public sealed record CampaignCandyEvolution(CampaignItemMenu ReturnMenu);

public sealed partial class CampaignSession
{
    private const string RareCandyItem = "pret:ITEM_RARE_CANDY";

    private Decision<CampaignState> UseFieldRareCandy(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (!_content.MtMoonEnabled || state.ItemMenu is not { Origin: "bag", View: "bag" } menu ||
            command.Text != RareCandyItem || command.Quantity != 1 || command.MoveSlot != -1)
            return No("field_rare_candy_context_invalid");
        if (command.PartyIndex < 0 || command.PartyIndex >= state.Party.Length)
            return No("item_party_index_invalid");
        if (ValidateCampaignInventory(state.Inventory) is { } error) return No(error);
        var consumed = EarlyInventoryAddRules.Remove(ShopCatalog, ShopInventoryView(state.Inventory!), new(RareCandyItem), 1);
        if (!consumed.Succeeded) return No(consumed.Error!);
        var result = RareCandyRules.Apply(_content.PokemonContent!, state.Party[command.PartyIndex],
            _content.MapDefinition(state.World.Player.MapId).RegionMapSection,snorlaxRules:_content.SnorlaxEnabled);
        if (result.Outcome == RareCandyOutcome.UnsupportedEvolutionTarget)
            return No("rare_candy_evolution_target_not_supported");
        if (result.Outcome == RareCandyOutcome.NoEffect)
            return new(CommandStatus.Accepted, state with { ItemMenu = menu with
                { View = "message", MessageSymbol = "gText_WontHaveEffect", ReturnView = "bag" } });
        return new(CommandStatus.Accepted, BeginRareCandyFanfare(state with
        {
            Party = state.Party.SetItem(command.PartyIndex, result.Pokemon),
            Inventory = MergeShopInventory(state.Inventory!, consumed.State), ItemMenu = null,
            RareCandy = new(result, menu, CampaignRareCandyStage.LevelMessage,
                [$"{result.Pokemon.Nickname} grew to\nLV. {result.Pokemon.Pokemon.Level}!"], 0, result.PendingMoves)
        }));
    }

    private Decision<CampaignState> DecideRareCandy(CampaignState state, CampaignCommand command)
    {
        var scene = state.RareCandy!;
        if (!_content.MtMoonEnabled || state.ItemMenu is not null || state.Evolution is not null)
            throw new InvalidDataException("rare_candy_context_invalid");
        int index = RareCandyPartyIndex(state, scene);
        if (command.Kind == "ack" && scene.Stage != CampaignRareCandyStage.LearnMove)
        {
            if (scene.Stage == CampaignRareCandyStage.LevelMessage)
                return new(CommandStatus.Accepted, state with { RareCandy = scene with { Stage = CampaignRareCandyStage.StatsGain } });
            if (scene.Stage == CampaignRareCandyStage.StatsGain)
                return new(CommandStatus.Accepted, state with { RareCandy = scene with { Stage = CampaignRareCandyStage.StatsTotal } });
            if (scene.Stage == CampaignRareCandyStage.StatsTotal && !scene.Result.LearnedMoves.IsEmpty)
                return new(CommandStatus.Accepted, BeginRareCandyFanfare(state with { RareCandy = scene with
                {
                    Stage = CampaignRareCandyStage.MoveMessage, PageIndex = 0,
                    Pages = scene.Result.LearnedMoves.Select(m => $"{state.Party[index].Nickname} learned\n{BattleContent.MoveNames[m.Move.Value]}!").ToImmutableArray()
                } }));
            if (scene.Stage == CampaignRareCandyStage.MoveMessage && scene.PageIndex + 1 < scene.Pages.Length)
                return new(CommandStatus.Accepted, BeginRareCandyFanfare(state with { RareCandy = scene with { PageIndex = scene.PageIndex + 1 } }));
            return new(CommandStatus.Accepted, AdvanceRareCandyMoves(state));
        }
        if (command.Kind == "rare_candy_learn" && scene.Stage == CampaignRareCandyStage.LearnMove)
        {
            var owned = state.Party[index];
            if (command.MoveSlot < -1 || command.MoveSlot >= owned.Pokemon.Moves.Length)
                return new(CommandStatus.Rejected, state, "rare_candy_learning_slot_invalid");
            if (_content.SsAnneEnabled && command.MoveSlot >= 0 && Game.Domain.Pokemon.PokemonMoveRules.IsHiddenMachineMove(owned.Pokemon.Moves[command.MoveSlot].Move))
                return new(CommandStatus.Rejected,state,"hm_move_cannot_be_forgotten");
            var move = scene.PendingMoves[0];
            if (command.MoveSlot >= 0)
                owned = ReplaceOwnedLevelMove(owned, command.MoveSlot, move.Move);
            string text = command.MoveSlot < 0 ? $"{owned.Nickname} did not learn\n{BattleContent.MoveNames[move.Move.Value]}."
                : $"{owned.Nickname} learned\n{BattleContent.MoveNames[move.Move.Value]}!";
            var next = state with
            {
                Party = state.Party.SetItem(index, owned),
                RareCandy = scene with { Stage = CampaignRareCandyStage.MoveMessage, Pages = [text], PageIndex = 0,
                    PendingMoves = scene.PendingMoves.RemoveAt(0) }
            };
            return new(CommandStatus.Accepted, command.MoveSlot < 0 ? next : BeginRareCandyFanfare(next));
        }
        return new(CommandStatus.Rejected, state, "rare_candy_decision_required");
    }

    private CampaignState AdvanceRareCandyMoves(CampaignState state)
    {
        var scene = state.RareCandy!;
        int index = RareCandyPartyIndex(state, scene);
        while (!scene.PendingMoves.IsEmpty)
        {
            var owned = state.Party[index]; var move = scene.PendingMoves[0];
            var learning = LearnOwnedLevelMove(owned, move.Move);
            if (learning.Outcome == MoveLearningOutcome.NeedsReplacement)
                return state with { RareCandy = scene with { Stage = CampaignRareCandyStage.LearnMove, Pages = [], PageIndex = 0 } };
            scene = scene with { PendingMoves = scene.PendingMoves.RemoveAt(0) };
            if (learning.Outcome == MoveLearningOutcome.Added)
                return BeginRareCandyFanfare(state with
                {
                    Party = state.Party.SetItem(index, owned with { Pokemon = owned.Pokemon with { Moves = learning.Moves } }),
                    RareCandy = scene with { Stage = CampaignRareCandyStage.MoveMessage,
                        Pages = [$"{owned.Nickname} learned\n{BattleContent.MoveNames[move.Move.Value]}!"], PageIndex = 0 }
                });
        }
        return scene.Result.Evolution is { } proposal
            ? state with { RareCandy = null, Evolution = new(null, [proposal], 0,
                CampaignEvolutionStage.Proposal, [], 0, []) { CandyUse = new(scene.ReturnMenu) } }
            : state with { RareCandy = null, ItemMenu = scene.ReturnMenu };
    }

    private static int RareCandyPartyIndex(CampaignState state, CampaignRareCandy scene)
    {
        for (int i = 0; i < state.Party.Length; i++)
            if (state.Party[i].InstanceId == scene.Result.Pokemon.InstanceId) return i;
        throw new InvalidDataException("rare_candy_member_missing");
    }

    private static CampaignState BeginRareCandyFanfare(CampaignState state)
    {
        long sequence = checked(state.Script.EffectSequence + 1);
        return state with
        {
            Script = state.Script with { EffectSequence = sequence },
            PresentationEffects = state.PresentationEffects.Add(new(sequence, "playfanfare", state.World.Player.MapId, ["MUS_LEVEL_UP"]))
        };
    }
}
