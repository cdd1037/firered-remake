using System.Collections.Immutable;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

public enum PokeFluteBattlePhase { NoEffect, Played, Fanfare, Awakened }
/// <summary>Source party-wide message/fanfare sequence. No individual wake messages are printed.</summary>
public sealed record WildBattlePokeFluteEvent(long Sequence, int Turn, PokeFluteBattlePhase Phase,
    ImmutableArray<int> PlayerPartyIndices, ImmutableArray<int> OpponentPartyIndices) : WildBattleEvent(Sequence, Turn);

public sealed record WildBattlePokeFluteResult(WildBattleResult Battle, ImmutableArray<PartyPokemon> Party)
{
    public bool Succeeded => Battle.Succeeded;
    public string? Fault => Battle.Fault;
}

public static partial class WildBattleRules
{
    public static WildBattlePokeFluteResult ResolvePokeFlute(EarlyShopCatalog shop,
        CampaignPokemonContent content, WildBattleState state, InventoryState inventory,
        ImmutableArray<PartyPokemon> party, int activeIndex)
    {
        if (IsSourceWild(state)) return ResolveSourceWildPokeFlute(shop, content, state, inventory, party, activeIndex);
        WildBattlePokeFluteResult Reject(string error) => new(Fail(state, error), party);
        if (state.RulesVersion is not (TowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion)) return Reject("poke_flute_battle_profile_not_enabled");
        var error = ValidateAction(content.Catalog, state) ?? ValidateRoute3NonMoveAction(state) ??
            PokeFluteRules.ValidateUse(shop, content, inventory, party, state.RulesVersion == SnorlaxRulesVersion, state.RulesVersion == FuchsiaRulesVersion) ??
            PokeFluteRules.ValidateActive(party, activeIndex, state.Player);
        if (error is not null) return Reject(error);
        var playerIndices = Enumerable.Range(0, party.Length).Where(i => PokeFluteRules.BattleWakes(party[i])).ToImmutableArray();
        ImmutableArray<int> opponentIndices = PokeFluteRules.PartyWakes(state.Opponent) ? [0] : [];
        var awakened = party;
        foreach (int i in playerIndices) awakened = awakened.SetItem(i, awakened[i] with { Status = 0 });
        var battle = Execute(state, state, context =>
        {
            // Select the opponent action against the pre-item state, then give the item priority.
            int slot = context.ChooseOpponent(content.Catalog);
            context.PrepareMtMoonTurn(content.Catalog, StruggleSlot, slot);
            context.PlayerFirst();
            context.ClearTowerDestinyForItem();
            context.ApplyPokeFlute(playerIndices, opponentIndices);
            context.Move(content.Catalog, WildBattleSide.Opponent, slot);
            context.FinishTurn(content.Catalog);
        }, party.Where((m, i) => i != activeIndex).Any(m => !m.IsEgg && m.Pokemon.CurrentHp > 0));
        return battle.Succeeded ? new(battle, BattleMedicineRules.Synchronize(awakened, activeIndex, battle.State!.Player))
            : new(battle, party);
    }

    internal sealed partial class Execution
    {
        internal void ApplyPokeFlute(ImmutableArray<int> playerIndices, ImmutableArray<int> opponentIndices)
        {
            State = State with { Player = PokeFluteRules.ClearActive(State.Player), Opponent = PokeFluteRules.ClearActive(State.Opponent) };
            if (playerIndices.IsEmpty && opponentIndices.IsEmpty)
                Add(n => new WildBattlePokeFluteEvent(n, State.Turn, PokeFluteBattlePhase.NoEffect, playerIndices, opponentIndices));
            else
                foreach (var phase in new[] { PokeFluteBattlePhase.Played, PokeFluteBattlePhase.Fanfare, PokeFluteBattlePhase.Awakened })
                    Add(n => new WildBattlePokeFluteEvent(n, State.Turn, phase, playerIndices, opponentIndices));
        }
    }
}
