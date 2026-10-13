using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // This private composition table binds the same source catalog to definition,
    // script and Domain entry. It accepts no caller-provided catalog or executor.
    // The Domain overloads retain catalog identity, owned/opponent and party checks.
    private enum TrainerRegion
    {
        Forest, Pewter, Route22, Route3, MtMoon, Cerulean, Bill, Vermilion, SsAnne, VermilionGym, Route11, RockTunnel, CeladonApproach, CeladonGym, RocketHideout, PokemonTower, FuchsiaRoute, FuchsiaGym, Silph, SaffronGym, Cinnabar, FirstSevii, ViridianGym, LeagueRival, VictoryRoad, PokemonLeague, SeviiRuby, FourIsland, FiveIsland, SixIsland
    }

    private sealed record TrainerEntry(ImmutableArray<WildBattleCombatant> Party, int Active, RngState Rng,
        ForestTrainerBattleStyle Style, WildBattleTerrain Terrain, PokemonCatalog Catalog,
        OwnedBattlePokemonCatalog? Owned, OwnedBattlePokemonCatalog? Successor);
    private sealed record TrainerScriptBinding(TrainerRegion Region, ImmutableArray<ForestTrainerBattleDefinition> Sources,
        ImmutableDictionary<string, CeruleanTrainerBattleDefinition> AuthenticatedSources);
    private sealed record OrdinaryTrainerBinding(TrainerDefinition Definition, TrainerScriptBinding Scripts,
        Func<TrainerEntry, TrainerId, ForestTrainerCombatResult> Start);
    private Dictionary<TrainerId, OrdinaryTrainerBinding>? _ordinaryTrainerBindings;
    private Dictionary<TrainerId, OrdinaryTrainerBinding> OrdinaryTrainerBindings =>
        _ordinaryTrainerBindings ??= BindOrdinaryTrainers();
    private OrdinaryTrainerBinding OrdinaryBinding(TrainerId trainer) => OrdinaryTrainerBindings[trainer];

    private Dictionary<TrainerId, OrdinaryTrainerBinding> BindOrdinaryTrainers()
    {
        var bindings = new Dictionary<TrainerId, OrdinaryTrainerBinding>();
        void Add(TrainerRegion region, IEnumerable<TrainerDefinition> trainers,
            IEnumerable<ForestTrainerBattleDefinition> scripts,
            Func<TrainerEntry, TrainerId, ForestTrainerCombatResult> start,
            IEnumerable<CeruleanTrainerBattleDefinition>? authenticated = null)
        {
            var source = new TrainerScriptBinding(region, scripts.ToImmutableArray(),
                (authenticated ?? []).ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
            // Some authenticated catalogs intentionally include an earlier roster
            // (Bill includes Cerulean). Later registrations own those trainers,
            // matching the current Domain entry catalog as well as its script.
            foreach (var trainer in trainers.Where(t => !t.DoubleBattle))
                bindings[trainer.Id] = new(trainer, source, start);
        }
        if (_content.ForestTrainers is { } forest)
        {
            Add(TrainerRegion.Forest, forest.Trainers.Values, forest.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(forest,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.PewterTrainers is { } pewter)
        {
            Add(TrainerRegion.Pewter, pewter.Trainers.Values, pewter.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(pewter,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.Route22Trainers is { } route22)
        {
            Add(TrainerRegion.Route22, route22.Trainers.Values, route22.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(route22,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.Route3Trainers is { } route3)
        {
            Add(TrainerRegion.Route3, route3.Trainers.Values, route3.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(route3,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.MtMoonTrainers is { } mtMoon)
        {
            Add(TrainerRegion.MtMoon, mtMoon.Trainers.Values, mtMoon.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(mtMoon,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.CeruleanTrainers is { } cerulean)
        {
            Add(TrainerRegion.Cerulean, cerulean.Trainers.Values, cerulean.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(cerulean,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.BillTrainers is { } bill)
        {
            Add(TrainerRegion.Bill, bill.Trainers.Values, bill.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(bill,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.VermilionTrainers is { } vermilion)
        {
            Add(TrainerRegion.Vermilion, vermilion.Trainers.Values, vermilion.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(vermilion,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.SsAnneTrainers is { } ssAnne)
        {
            Add(TrainerRegion.SsAnne, ssAnne.Trainers.Values, ssAnne.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(ssAnne,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.VermilionGymTrainers is { } vermilionGym)
        {
            Add(TrainerRegion.VermilionGym, vermilionGym.Trainers.Values, vermilionGym.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(vermilionGym,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.Route11Trainers is { } route11)
        {
            Add(TrainerRegion.Route11, route11.Trainers.Values, route11.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(route11,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.RockTunnelTrainers is { } rockTunnel)
        {
            Add(TrainerRegion.RockTunnel, rockTunnel.Trainers.Values, rockTunnel.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(rockTunnel,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.CeladonApproachTrainers is { } celadonApproach)
        {
            Add(TrainerRegion.CeladonApproach, celadonApproach.Trainers.Values, celadonApproach.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(celadonApproach,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.CeladonGymTrainers is { } celadonGym)
        {
            Add(TrainerRegion.CeladonGym, celadonGym.Trainers.Values, celadonGym.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(celadonGym,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.RocketHideoutTrainers is { } rocketHideout)
        {
            Add(TrainerRegion.RocketHideout, rocketHideout.Trainers.Values, rocketHideout.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(rocketHideout,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.PokemonTowerTrainers is { } pokemonTower)
        {
            Add(TrainerRegion.PokemonTower, pokemonTower.Trainers.Values, pokemonTower.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(pokemonTower,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.FuchsiaRouteEnabled && _content.FuchsiaRouteTrainers is { } fuchsiaRoute)
        {
            Add(TrainerRegion.FuchsiaRoute, fuchsiaRoute.Trainers.Values, fuchsiaRoute.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(fuchsiaRoute,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,entry.Owned));
        }
        if (_content.FuchsiaGymTrainerParties is { } fuchsiaGym)
        {
            Add(TrainerRegion.FuchsiaGym, fuchsiaGym.Trainers.Values, fuchsiaGym.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.Start(fuchsiaGym,entry.Catalog,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Owned));
        }
        if (_content.SilphTrainerParties is { } silph)
        {
            Add(TrainerRegion.Silph, silph.Trainers.Values, silph.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => (_content.CinnabarEnabled ? ForestTrainerCombatRules.StartSource(silph,_content.CinnabarOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,successorOwned:entry.Successor) : ForestTrainerCombatRules.StartSource(silph,_content.SilphOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain)));
        }
        if (_content.SaffronGymTrainerParties is { } saffronGym)
        {
            Add(TrainerRegion.SaffronGym, saffronGym.Trainers.Values, saffronGym.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => (_content.CinnabarEnabled ? ForestTrainerCombatRules.StartSource(saffronGym,_content.CinnabarOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,successorOwned:entry.Successor) : ForestTrainerCombatRules.StartSource(saffronGym,_content.SaffronGymOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain)));
        }
        if (_content.CinnabarTrainerParties is { } cinnabar)
        {
            Add(TrainerRegion.Cinnabar, cinnabar.Trainers.Values, cinnabar.BattlesByScript.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(cinnabar,_content.CinnabarOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,successorOwned:entry.Successor));
        }
        if (_content.FirstSeviiEnabled && _content.FirstSeviiTrainerParties is { } firstSevii)
        {
            Add(TrainerRegion.FirstSevii, firstSevii.Trainers.Values, firstSevii.BattlesBySource.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(firstSevii,_content.CinnabarOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,successorOwned:entry.Successor));
        }
        if (_content.ViridianGymEnabled && _content.ViridianGymTrainerParties is { } viridianGym)
        {
            Add(TrainerRegion.ViridianGym, viridianGym.Trainers.Values, viridianGym.BattlesByScript.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(viridianGym,_content.CinnabarOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,successorOwned:entry.Successor));
        }
        if (_content.LeagueRivalEnabled && _content.LeagueRivalTrainerParties is { } leagueRival)
        {
            Add(TrainerRegion.LeagueRival, leagueRival.Trainers.Values, leagueRival.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(leagueRival,_content.CinnabarOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,successorOwned:entry.Successor));
        }
        if (_content.VictoryRoadEnabled && _content.VictoryRoadTrainerParties is { } victoryRoad)
        {
            Add(TrainerRegion.VictoryRoad, victoryRoad.Trainers.Values, victoryRoad.BattlesByScript.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(victoryRoad,_content.VictoryRoadOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,successorOwned:entry.Successor));
        }
        if (_content.PokemonLeagueEnabled && _content.PokemonLeagueTrainerParties is { } pokemonLeague)
        {
            Add(TrainerRegion.PokemonLeague, pokemonLeague.Trainers.Values, pokemonLeague.Battles.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(pokemonLeague,_content.PokemonLeagueOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,successorOwned:entry.Successor));
        }
        if (_content.SeviiRubyEnabled && _content.SeviiRubyTrainers is { } seviiRuby)
        {
            Add(TrainerRegion.SeviiRuby, seviiRuby.Trainers.Values, seviiRuby.BattlesByScript.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(seviiRuby,_content.SeviiRubyOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,successorOwned:_content.FourIslandEnabled?entry.Owned:null));
        }
        if (_content.FourIslandEnabled && _content.FourIslandTrainers is { } fourIsland)
        {
            Add(TrainerRegion.FourIsland, fourIsland.Trainers.Values, fourIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(fourIsland,_content.FourIslandOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,successorOwned:_content.SixIslandEnabled?entry.Owned:null), fourIsland.BattlesByScript.Values);
        }
        if (_content.FiveIslandEnabled && _content.FiveIslandTrainers is { } fiveIsland)
        {
            Add(TrainerRegion.FiveIsland, fiveIsland.Trainers.Values, fiveIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(fiveIsland,_content.FiveIslandOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,successorOwned:_content.CelioEnabled?entry.Owned:null), fiveIsland.BattlesByScript.Values);
        }
        if (_content.SixIslandEnabled && _content.SixIslandTrainers is { } sixIsland)
        {
            Add(TrainerRegion.SixIsland, sixIsland.Trainers.Values, sixIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (entry, trainer) => ForestTrainerCombatRules.StartSource(sixIsland,_content.SixIslandOpponents!,trainer,entry.Party,entry.Active,entry.Rng,entry.Style,entry.Terrain,successorOwned:_content.FiveIslandEnabled?entry.Owned:null), sixIsland.BattlesByScript.Values);
        }
        return bindings;
    }

    private static ForestTrainerBattleDefinition ProjectTrainerScript(ForestTrainerBattleDefinition source) => source;
    private static ForestTrainerBattleDefinition ProjectTrainerScript(CeruleanTrainerBattleDefinition source) =>
        new(source.Trainer, source.Mode, source.BattleTypeFlags, source.ScriptEntry, source.IntroText,
            source.DefeatText, source.PostBattleText, source.EntryScript, source.Callback, source.Source);
    private static ForestTrainerBattleDefinition ProjectTrainerScript(PewterTrainerBattleDefinition source) =>
        new(source.Trainer, source.Mode, source.BattleTypeFlags, source.ScriptEntry, source.IntroText,
            source.DefeatText, source.PostBattleText, source.EntryScript, source.Callback, source.Source);
    private static ForestTrainerBattleDefinition ProjectTrainerScript(Route22TrainerBattleDefinition source) =>
        new(source.Trainer, source.Mode, source.BattleTypeFlags, source.ScriptEntry, "Route22_Text_EarlyRivalIntro",
            source.DefeatText, "Route22_Text_EarlyRivalPostBattle", source.EntryScript, source.Callback, source.Source);

    private ForestTrainerBattleDefinition BoundTrainerScript(OrdinaryTrainerBinding binding, string? script, int? sourceLine)
    {
        var trainer = binding.Definition.Id;
        var region = binding.Scripts.Region;
        var sources = binding.Scripts.Sources;
        if (region == TrainerRegion.FirstSevii)
        {
            var source = sourceLine is { } line && script is not null
                ? ProjectTrainerScript(_content.FirstSeviiTrainerParties!.Battle(script, line))
                : sources.Single(b => b.Trainer == trainer && (script is null || b.ScriptEntry == script));
            if (source.Trainer != trainer || source.Mode == 4)
                throw new InvalidDataException("first_sevii_single_script_mismatch");
            return source;
        }
        string? prefix = region switch
        {
            TrainerRegion.FiveIsland => "five_island", TrainerRegion.SixIsland => "six_island",
            TrainerRegion.FourIsland => "four_island", TrainerRegion.SeviiRuby => "sevii_ruby",
            TrainerRegion.VictoryRoad => "victory_road", TrainerRegion.ViridianGym => "viridian_gym",
            TrainerRegion.Cinnabar => "cinnabar", _ => null
        };
        if (prefix is null)
        {
            var source = sources.Single(b => b.Trainer == trainer);
            if (region == TrainerRegion.LeagueRival && (source.Mode != 3 || script is not null && script != source.ScriptEntry))
                throw new InvalidDataException("league_rival_script_mismatch");
            if (region == TrainerRegion.PokemonLeague && (source.Mode != 3 || source.BattleTypeFlags != 8 ||
                script is not null && script != source.ScriptEntry))
                throw new InvalidDataException("pokemon_league_trainer_script");
            return source;
        }
        if (region == TrainerRegion.FourIsland && script is not null && script != FourIslandTrainerPartyFactory.BattleScript)
            throw new InvalidDataException("four_island_trainer_script_missing");
        var selected = script is null
            ? sources.Single(b => b.Trainer == trainer && (region switch
            {
                TrainerRegion.SixIsland or TrainerRegion.VictoryRoad => b.Mode == 0,
                TrainerRegion.Cinnabar => b.Mode is 0 or 1 or 2,
                TrainerRegion.SeviiRuby => b.Mode != 4,
                _ => true
            }))
            : sources.SingleOrDefault(b => b.ScriptEntry == script) ?? throw new InvalidDataException(prefix + "_trainer_script_missing");
        bool accepted = region switch
        {
            TrainerRegion.FiveIsland or TrainerRegion.SixIsland => selected.Mode == 0 && selected.BattleTypeFlags == 8,
            TrainerRegion.FourIsland => selected.Mode == 3 && selected.BattleTypeFlags == 8 && selected.Source.Path == FourIslandBackSource,
            TrainerRegion.SeviiRuby => selected.Mode is 0 or 3,
            TrainerRegion.VictoryRoad => selected.Mode == 0,
            TrainerRegion.ViridianGym => selected.Mode is 0 or 1,
            TrainerRegion.Cinnabar => selected.Mode != 4,
            _ => false
        };
        // Preserve each region's fingerprint validation on the original complete
        // battle record, including Five's callback and Six's double parameters.
        if (region == TrainerRegion.FiveIsland) FiveIslandTrainerPartyFactory.ValidateBattle(binding.Scripts.AuthenticatedSources[selected.ScriptEntry]);
        if (region == TrainerRegion.SixIsland) SixIslandTrainerPartyFactory.ValidateBattle(binding.Scripts.AuthenticatedSources[selected.ScriptEntry]);
        if (region == TrainerRegion.FourIsland) FourIslandTrainerPartyFactory.ValidateBattle(binding.Scripts.AuthenticatedSources[selected.ScriptEntry]);
        if (selected.Trainer != trainer || !accepted)
            throw new InvalidDataException(prefix + "_single_script_mismatch");
        return selected;
    }

    private void RequireOrdinaryTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro, TrainerRegion region, string fault)
    {
        if (!OrdinaryTrainerBindings.TryGetValue(trainer, out var binding) || binding.Scripts.Region != region ||
            noIntro || source.Mode != 0 || state.World.Player.MapId != OrdinaryTrainerMap(trainer) ||
            state.Script.Wait?.Kind != StoryWaitKind.Battle || state.LastTalked is not { } id ||
            !state.Actors.TryGetValue(id, out var actor) || !actor.Active || actor.ScriptId != source.ScriptEntry ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId)
            throw new InvalidDataException(fault);
    }

    private bool OrdinaryTrainerHistoryOriginMatches(CampaignForestTrainerHistory history, TrainerRegion region)
    {
        if (!OrdinaryTrainerBindings.TryGetValue(new(history.TrainerId), out var binding) || binding.Scripts.Region != region ||
            history.SourceScriptEntry is not { } script || !binding.Scripts.AuthenticatedSources.TryGetValue(script, out var source) ||
            source.Trainer.Value != history.TrainerId || source.Mode != 0) return false;
        int line = region switch
        {
            TrainerRegion.FiveIsland => FiveIslandTrainerSourceLine(script),
            TrainerRegion.SixIsland => SixIslandTrainerSourceLine(script),
            _ => throw new InvalidDataException("ordinary_trainer_history_policy")
        };
        return history.SourceScriptLine == line;
    }

    private int RequireTrainerInstruction(CeruleanTrainerBattleDefinition source, string script,
        string opcode, string[] args, int line, string fault)
    {
        var instruction = _content.FindProgram(script)?.Instructions.SingleOrDefault(i => i.Op == StoryOp.TrainerBattle);
        if (instruction is null || instruction.Opcode != opcode || instruction.Source.Path != source.Source.Path ||
            instruction.Source.Symbol != script || instruction.Source.Line != line || !instruction.Args.SequenceEqual(args))
            throw new InvalidDataException(fault);
        return instruction.Source.Line;
    }
}
