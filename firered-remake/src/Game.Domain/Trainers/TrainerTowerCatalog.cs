using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>Only the four source local courses. e-Reader data cannot create this proof.</summary>
public sealed class TrainerTowerCatalog
{
    public const string Profile = "firered-trainer-tower-local-v1";
    public const int CreationRandomDrawsPerMember = 0;
    public TrainerTowerLocalData Data { get; }
    public ImmutableArray<TrainerTowerCourse> Courses => Data.Courses;
    public ImmutableArray<TrainerTowerFloorTemplate> Templates => Data.Templates;
    private readonly ImmutableDictionary<string, TrainerTowerFloorTemplate> _templates;
    private TrainerTowerCatalog(TrainerTowerLocalData data)
    { Data = data; _templates = data.Templates.ToImmutableDictionary(t => t.Symbol, StringComparer.Ordinal); }
    public static TrainerTowerCatalog Authenticate(TrainerTowerLocalData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.LocalSetId != 1 || data.ExternalEReaderEnabled || data.Courses.IsDefault || data.Courses.Length != 4 ||
            data.Templates.IsDefault || data.Templates.Length != 28)
            throw new ArgumentException("trainer_tower_local_source");
        return new(data);
    }
    public TrainerTowerCourse Course(TrainerTowerChallengeKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        return Courses[(int)kind];
    }
    public TrainerTowerFloorTemplate Template(string symbol) =>
        _templates.TryGetValue(symbol, out var template) ? template : throw new ArgumentException("trainer_tower_template");

    /// <summary>floorIndex is zero-based. Only Knockout accepts trainerIndex 1 or 2. Caller recomputes party maximum for each battle.</summary>
    public TrainerTowerBattleDescriptor DescribeBattle(TrainerTowerChallengeKind course, int floorIndex,
        int trainerIndex, int maximumPartyLevel)
    {
        var definition = Course(course);
        if (floorIndex is < 0 or >= 8) throw new ArgumentOutOfRangeException(nameof(floorIndex));
        if (maximumPartyLevel is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(maximumPartyLevel));
        var floor = definition.Floors[floorIndex]; var template = Template(floor.TemplateSymbol);
        if (trainerIndex < 0 || trainerIndex >= (floor.Format == TrainerTowerBattleFormat.Knockout ? 3 : 1))
            throw new ArgumentOutOfRangeException(nameof(trainerIndex));
        var selection = floor.Format == TrainerTowerBattleFormat.Knockout ? [floor.Selection[trainerIndex]] : floor.Selection;
        var members = selection.Select(s => new TrainerTowerSelectedMember(s.TrainerIndex, s.PoolIndex,
            template.Trainers[s.TrainerIndex], template.Trainers[s.TrainerIndex].Mons[s.PoolIndex])).ToImmutableArray();
        return new(course, floorIndex, trainerIndex, floor.Format, maximumPartyLevel, template.Symbol, members,
            floor.Format == TrainerTowerBattleFormat.Double ? 524297u : 524296u, 7u);
    }

    /// <summary>Original CreateBattleTowerMon: preset PID/OT, fixed IV initialization, then explicit source IV/EV/ability/move overwrite. Zero RNG draws.</summary>
    public TrainerPartyCreationResult CreateBattle(GameOpponentPokemonCatalog opponents,
        TrainerTowerChallengeKind course, int floorIndex, int trainerIndex, int maximumPartyLevel, RngState rng)
    {
        ArgumentNullException.ThrowIfNull(opponents);
        var descriptor = DescribeBattle(course, floorIndex, trainerIndex, maximumPartyLevel);
        var party = descriptor.Members.Select(member => {
            var mon = member.Template; var species = opponents.Catalog.Species[mon.Species];
            var made = PokemonFactory.CreateCore(opponents.Catalog,
                new(mon.Species, maximumPartyLevel, mon.OriginalTrainerId, IvPolicy.Fixed(0), PersonalityPolicy.Fixed(mon.Personality)),
                rng, opponents.Mechanics.Data);
            if (made.Rng != rng || !made.Trace.IsEmpty) throw new InvalidOperationException("trainer_tower_creation_rng");
            var stats = PokemonStatCalculation.Calculate(species, maximumPartyLevel, made.Pokemon.Nature, mon.Ivs, mon.Evs, opponents.Mechanics.Data);
            var moves = mon.Moves.Select((move, index) => {
                var ppUps = mon.PpBonuses >> (index * 2) & 3; var pp = opponents.Catalog.Moves[move].Pp;
                var maximumPp = pp + pp * 20 * ppUps / 100;
                return new PokemonMoveSlot(move, maximumPp, maximumPp, ppUps);
            }).ToImmutableArray();
            var pokemon = made.Pokemon with { Ivs = mon.Ivs, Evs = mon.Evs, Stats = stats, CurrentHp = stats.MaxHp,
                AbilitySlot = mon.AbilitySlot, AbilityId = mon.AbilityId, Friendship = mon.Friendship, Moves = moves };
            return new TrainerPartyPokemon(pokemon, mon.HeldItem);
        }).ToImmutableArray();
        var trainer = Adapter(descriptor);
        var result = new TrainerPartyCreationResult(trainer, party, rng, []);
        return result with { TowerRoster = TrainerTowerEncounterRoster.Create(opponents.Mechanics.Catalog,
            result, course.ToString(), floorIndex, trainerIndex, descriptor.Format == TrainerTowerBattleFormat.Double) };
    }
    private static TrainerDefinition Adapter(TrainerTowerBattleDescriptor descriptor)
    {
        var lead = descriptor.Members[0].Trainer;
        static TrainerSourceRef Source(PokemonSourceRef r) => new(r.Path, r.Symbol, r.StartLine, r.EndLine);
        var members = descriptor.Members.Select(m => new TrainerPartyMemberDefinition(m.Template.Species,
            m.Template.SourceSpeciesId, m.Template.Nickname, [], descriptor.Level, 0, m.Template.HeldItem,
            m.Template.Moves, Source(m.Template.Source), Source(m.Template.Source))).ToImmutableArray();
        // This is a facility AI adapter, never a normal gTrainers record or a money/EXP admission.
        return new(new TrainerId($"tower:{descriptor.Course}:{descriptor.FloorIndex}:{descriptor.TrainerIndex}"), 0,
            lead.TrainerClass, lead.SourceTrainerClass, lead.TrainerPic, lead.SourceTrainerPic, lead.Name, [], 0,
            ["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"], descriptor.Format == TrainerTowerBattleFormat.Double,
            3, 7, ["AI_SCRIPT_CHECK_BAD_MOVE", "AI_SCRIPT_TRY_TO_FAINT", "AI_SCRIPT_CHECK_VIABILITY"], 0,
            members, Source(lead.Source), Source(lead.Source));
    }
}
