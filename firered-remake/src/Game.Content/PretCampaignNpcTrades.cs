using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretNpcTrades NpcTradeContent { get; }
    public PretNpcTradeElyssa NpcTradeElyssa { get; }
    public PretNpcTradeHaden NpcTradeHaden { get; }
    public PretNpcTradeMail NpcTradeMail { get; }
    public SourceMailMessage? NpcTradeMailMessage => NpcTradeMail.Message;
    private ImmutableDictionary<int, MapId> NpcTradeMaps { get; }
    public MapId NpcTradeMap(int tradeId) => NpcTradeMaps.TryGetValue(tradeId, out var map)
        ? map : throw new ArgumentOutOfRangeException(nameof(tradeId));

    private static ImmutableDictionary<int, MapId> ResolveNpcTradeMaps(PretNpcTrades trades,ImmutableDictionary<MapId, PretWorldMap> maps)
    {
        var result = ImmutableDictionary.CreateBuilder<int, MapId>();
        foreach (var definition in trades.Trades.Definitions.Values)
        {
            if (!maps.TryGetValue(definition.Map, out var map))
                throw new InvalidDataException("npc_trade_map_binding");
            if (definition.LocalId < 1 || map.Events.GetProperty("object_events").GetArrayLength() < definition.LocalId ||
                map.Events.GetProperty("object_events")[definition.LocalId - 1].GetProperty("script").GetString() != definition.ScriptLabel)
                throw new InvalidDataException("npc_trade_actor_binding");
            result.Add(definition.TradeId, map.Id);
        }
        return result.ToImmutable();
    }
    public bool IsNpcTradeStablePosition(MapId map,int x,int y) => !NpcTradeHaden.NewMaps.ContainsKey(map) || NpcTradeHaden.AllowsFootDestination(map,x,y);
    public NpcTradePokemonCatalog NpcTrades => NpcTradeContent.Trades;

    private static void ValidateNpcTradeComposition(PretNpcTrades trades,ImmutableDictionary<MapId,PretWorldMap> maps,ImmutableDictionary<MapId,PretWorldMap> previous,PretStoryCompilation scripts)
    {
        if (trades.NewMaps.Count != 1 )
            throw new InvalidDataException("npc_trade_current_map_composition");
        var tradeMaps = ResolveNpcTradeMaps(trades, maps);
        foreach (var definition in trades.Trades.Definitions.Values.Where(d => trades.Trades.IsEnabled(d.TradeId)))
        {
            var map = maps[tradeMaps[definition.TradeId]];
            var actor=map.Events.GetProperty("object_events")[definition.LocalId-1];
            if(actor.GetProperty("script").GetString()!=definition.ScriptLabel)
                throw new InvalidDataException("npc_trade_actor_binding");
            var program=scripts.FindProgram(definition.ScriptLabel);
            if(program is null||program.Instructions.IsEmpty||program.Instructions.Any(i=>i.Op==StoryOp.Unsupported))
                throw new InvalidDataException("npc_trade_story_incomplete:"+definition.ScriptLabel);
        }
        foreach(var symbol in new[]{"EventScript_GetInGameTradeSpeciesInfo","EventScript_ChooseMonForInGameTrade","EventScript_GetInGameTradeSpecies","EventScript_DoInGameTrade"})
            if(scripts.FindProgram(symbol) is not {} program||program.Instructions.Any(i=>i.Op==StoryOp.Unsupported))
                throw new InvalidDataException("npc_trade_shared_story_incomplete:"+symbol);
    }
}
