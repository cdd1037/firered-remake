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
    private sealed record TrainerScriptBinding(ImmutableArray<RegisteredTrainerSource> Sources);
    private sealed record OrdinaryTrainerBinding(TrainerDefinition Definition, TrainerScriptBinding Scripts,
        Func<TrainerId, RngState, TrainerPartyCreationResult> Factory, string TracePrefix);
    private Dictionary<TrainerId, OrdinaryTrainerBinding>? _ordinaryTrainerBindings;
    private Dictionary<TrainerId, OrdinaryTrainerBinding> OrdinaryTrainerBindings =>
        _ordinaryTrainerBindings ??= BindOrdinaryTrainers();
    private OrdinaryTrainerBinding OrdinaryBinding(TrainerId trainer) => OrdinaryTrainerBindings[trainer];

    /// <summary>Read-only source definition from the same authenticated registry used for battle entry.</summary>
    public TrainerDefinition GetOrdinaryTrainerDefinition(TrainerId trainer) => OrdinaryBinding(trainer).Definition;

    private Dictionary<TrainerId, OrdinaryTrainerBinding> BindOrdinaryTrainers()
    {
        var bindings = new Dictionary<TrainerId, OrdinaryTrainerBinding>();
        void Add(IEnumerable<TrainerDefinition> trainers,
            IEnumerable<RegisteredTrainerSource> scripts,
            Func<TrainerId, RngState, TrainerPartyCreationResult> factory, string tracePrefix)
        {
            var source = new TrainerScriptBinding(scripts.ToImmutableArray());
            foreach (var trainer in trainers.Where(t => !t.DoubleBattle))
                bindings[trainer.Id] = new(trainer, source, factory, tracePrefix);
        }
        if (_content.Route22Trainers is { } route22)
        {
            Add(route22.Trainers.Values, route22.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => Route22TrainerPartyFactory.Create(route22, _binding.Ownership.Opponents.Catalog, trainer, rng), "route22-trainer");
        }
        if (_content.FuchsiaRouteTrainers is { } fuchsiaRoute)
        {
            Add(fuchsiaRoute.Trainers.Values, fuchsiaRoute.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FuchsiaRouteTrainerPartyFactory.Create(fuchsiaRoute, _binding.Ownership.Opponents.Catalog, trainer, rng), "fuchsia-route-trainer");
        }
        if (_content.CinnabarTrainerParties is { } cinnabar)
        {
            Add(cinnabar.Trainers.Values, cinnabar.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => CinnabarTrainerPartyFactory.Create(cinnabar, _content.OpponentPokemon!, trainer, rng), "cinnabar-trainer");
        }
        if (_content.FirstSeviiTrainerParties is { } firstSevii)
        {
            Add(firstSevii.Trainers.Values, firstSevii.BattlesBySource.Values.Select(ProjectTrainerScript),
                (trainer, rng) => FirstSeviiTrainerPartyFactory.Create(firstSevii, _content.OpponentPokemon!, trainer, rng), "first-sevii-trainer");
        }
        if (_content.LeagueRivalTrainerParties is { } leagueRival)
        {
            Add(leagueRival.Trainers.Values, leagueRival.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => LeagueRivalTrainerPartyFactory.Create(leagueRival, _content.OpponentPokemon!, trainer, rng), "league-rival-trainer");
        }
        if (_content.VictoryRoadTrainerParties is { } victoryRoad)
        {
            Add(victoryRoad.Trainers.Values, victoryRoad.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => VictoryRoadTrainerPartyFactory.Create(victoryRoad, _content.OpponentPokemon!, trainer, rng), "victory-road-trainer");
        }
        if (_content.PokemonLeagueTrainerParties is { } pokemonLeague)
        {
            Add(pokemonLeague.Trainers.Values, pokemonLeague.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => PokemonLeagueTrainerPartyFactory.Create(pokemonLeague, _content.OpponentPokemon!, trainer, rng), "pokemon-league-trainer");
        }
        if (_content.LeagueRematchTrainerParties is { } rematch)
        {
            Add(rematch.Trainers.Values, rematch.Battles.Values.Select(ProjectTrainerScript),
                (trainer, rng) => LeagueRematchTrainerPartyFactory.Create(rematch, _content.OpponentPokemon!, trainer, rng), "league-rematch-trainer");
        }
        if (_content.SeviiRubyTrainers is { } seviiRuby)
        {
            Add(seviiRuby.Trainers.Values, seviiRuby.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SeviiRubyTrainerPartyFactory.Create(seviiRuby, _content.OpponentPokemon!, trainer, rng), "sevii-ruby-trainer");
        }
        if (_content.SixIslandTrainers is { } sixIsland)
        {
            Add(sixIsland.Trainers.Values, sixIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SixIslandTrainerPartyFactory.Create(sixIsland, _content.OpponentPokemon!, trainer, rng), "six-island-trainer");
        }
        if (_content.SevenIslandTrainers is { } sevenIsland)
        {
            Add(sevenIsland.Trainers.Values, sevenIsland.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SevenIslandTrainerPartyFactory.Create(sevenIsland, _content.OpponentPokemon!, trainer, rng), "seven-island-trainer");
        }
        if (_content.SeafoamTrainers is { } seafoam)
        {
            Add(seafoam.Trainers.Values, seafoam.BattlesByScript.Values.Select(ProjectTrainerScript),
                (trainer, rng) => SeafoamTrainerPartyFactory.Create(seafoam, _content.OpponentPokemon!, trainer, rng), "seafoam-trainer");
        }
        foreach (var catalog in _content.OrdinaryTrainerCatalogs)
        {
            if (catalog.Trainers.Keys.Any(bindings.ContainsKey))
                throw new InvalidDataException("ordinary_trainer_duplicate_registration");
            Add(catalog.Trainers.Values, catalog.Battles.Select(ProjectTrainerScript),
                (trainer, rng) => TrainerPartyFactory.Create(catalog.Trainers, _binding.Ownership.Opponents, trainer, rng), catalog.TracePrefix);
        }
        if (VsSeekerEnabled && _content.VsSeekerTrainers is { } vsSeeker)
        {
            // Existing first-battle bindings keep their declared ordinary occurrences.
            // Additional parties are reachable only through the typed rematch adapter.
            var source = new TrainerScriptBinding([]);
            foreach (var trainer in vsSeeker.Trainers.Values.Where(t => !t.DoubleBattle && !bindings.ContainsKey(t.Id)))
                bindings.Add(trainer.Id, new(trainer, source,
                    (id, rng) => VsSeekerTrainerPartyFactory.Create(vsSeeker, _binding.Ownership.Mechanics, id, rng), "vs-seeker-trainer"));
        }
        return bindings;
    }

    private static RegisteredTrainerSource ProjectTrainerScript(CeruleanTrainerBattleDefinition source)
    {
        var definition = new ForestTrainerBattleDefinition(source.Trainer, source.Mode, source.BattleTypeFlags,
            source.ScriptEntry, source.IntroText, source.DefeatText, source.PostBattleText,
            source.EntryScript, source.Callback, source.Source);
        var arguments = source.Mode == 3
            ? ImmutableArray.Create(source.Trainer.Value[5..], source.DefeatText)
            : ImmutableArray.Create(source.Trainer.Value[5..], source.IntroText, source.DefeatText);
        if (source.Mode != 3 && source.PostBattleScript is { } callback) arguments = arguments.Add(callback);
        if (source.Mode != 3 && source.MusicOption is { } music) arguments = arguments.Add(music);
        return new(definition, source.Mode == 3 ? "trainerbattle_no_intro" : "trainerbattle_single",
            arguments, source.PostBattleScript, source.MusicOption);
    }

    private static RegisteredTrainerSource ProjectTrainerScript(Route22TrainerBattleDefinition source) =>
        new(new(source.Trainer, source.Mode, source.BattleTypeFlags, source.ScriptEntry, "Route22_Text_EarlyRivalIntro",
                source.DefeatText, "Route22_Text_EarlyRivalPostBattle", source.EntryScript, source.Callback, source.Source),
            "trainerbattle_earlyrival", [source.Trainer.Value[5..], source.RivalFlags.ToString(System.Globalization.CultureInfo.InvariantCulture),
                source.DefeatText, source.VictoryText]);

    private ForestTrainerBattleDefinition BoundTrainerScript(OrdinaryTrainerBinding binding, string? script, int? sourceLine) =>
        ResolveTrainerOccurrence(binding.Definition.Id, script, sourceLine).Source.Definition;

    private void RequireOrdinaryTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro, string fault)
    {
        if (noIntro || source.Mode != 0) throw new InvalidDataException(fault);
        RequireTrainerActorOccurrence(state, ResolveTrainerOccurrence(trainer, source.ScriptEntry));
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
