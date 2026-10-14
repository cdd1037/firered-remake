using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public PretNpcTrades NpcTradeContent { get; }
    public PretNpcTradeElyssa NpcTradeElyssa { get; }
    public NpcTradePokemonCatalog NpcTrades => NpcTradeContent.Trades;

    private static void AppendNpcTradeSources(PretNpcTrades content, List<PretSourceScriptFile> sources,
        List<string> texts, Dictionary<string,int> constants, List<string> hashes) =>
        AppendAuthenticatedRegionalSources(content.Scripts, content.TextSources.Values, content.Constants,
            content.AdditionalSourceHashes, sources, texts, constants, hashes);

    private static void ValidateNpcTradeComposition(PretNpcTrades trades,
        ImmutableDictionary<MapId,PretWorldMap> maps, ImmutableDictionary<MapId,PretWorldMap> previous,
        PretStoryCompilation scripts)
    {
        if (previous.Count != 315 || maps.Count != 316 || trades.NewMaps.Count != 1 ||
            previous.Any(p => !ReferenceEquals(p.Value,maps[p.Key])) ||
            !ReferenceEquals(maps[PretNpcTrades.LoungeMapId],trades.NewMaps[PretNpcTrades.LoungeMapId]))
            throw new InvalidDataException("npc_trade_current_map_composition");
        foreach (var definition in trades.Trades.Definitions.Values.Where(d => trades.Trades.IsEnabled(d.TradeId)))
        {
            var map = maps[definition.Map];
            var actor=map.Events.GetProperty("object_events")[definition.LocalId-1];
            if(actor.GetProperty("script").GetString()!=definition.ScriptLabel)
                throw new InvalidDataException("npc_trade_actor_binding");
            var program=scripts.FindProgram(definition.ScriptLabel);
            if(program is null||program.Instructions.IsEmpty||program.Instructions.Any(i=>i.Op==StoryOp.Unsupported))
                throw new InvalidDataException("npc_trade_story_incomplete:"+definition.ScriptLabel);
        }
        foreach(var symbol in new[]{"EventScript_GetInGameTradeSpeciesInfo","EventScript_ChooseMonForInGameTrade",
            "EventScript_GetInGameTradeSpecies","EventScript_DoInGameTrade"})
            if(scripts.FindProgram(symbol) is not {} program||program.Instructions.Any(i=>i.Op==StoryOp.Unsupported))
                throw new InvalidDataException("npc_trade_shared_story_incomplete:"+symbol);
    }
}
