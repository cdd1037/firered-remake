using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace Game.Domain.Encounters;

/// <summary>Immutable fixed encounter gameplay definitions; battle mechanics remain shared.</summary>
public sealed record SourceScriptedEncounterDefinition(string Id, MapId Map, SpeciesId Species, byte Level,
    string ScriptId, int LocalId, string FoughtFlag, string HideFlag, string ResumeScript,
    string OutcomeScript, bool Legendary);

public static class SourceScriptedEncounterRegistry
{
    public static ImmutableArray<SourceScriptedEncounterDefinition> Definitions { get; } = [
        new("static.moltres", new("map.mt-ember-summit"), new("pret:SPECIES_MOLTRES"), 50,
            "MtEmber_Summit_EventScript_Moltres", 1, "FLAG_FOUGHT_MOLTRES", "FLAG_HIDE_MOLTRES",
            "MtEmber_Summit_OnResume", "MtEmber_Summit_EventScript_TryRemoveMoltres", true)
    ];

    public static SourceScriptedEncounterDefinition? Find(SourceTableWildOrigin? origin) =>
        origin is { Kind: SourceTableEncounterKind.Scripted, RulesVersion: SourceTableWildBattleContract.Version, Slot: >= 0 }
            ? Definitions.Where(d => d.Map == origin.Map).Skip(origin.Slot).FirstOrDefault() : null;

    public static SourceTableWildOrigin Origin(SourceScriptedEncounterDefinition definition)
    {
        int slot = Definitions.Where(d => d.Map == definition.Map).ToList().IndexOf(definition);
        if (slot < 0) throw new ArgumentException("scripted_encounter_definition");
        return new(definition.Map, SourceTableEncounterKind.Scripted, slot);
    }
}
