using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.Tutorials;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed partial record CampaignTeachyTv
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CampaignTeachyTvBattle? Battle { get; init; }
}

public sealed partial class CampaignSession
{
    /// <summary>Temporary battle members exclusively for display. Never installed in State.Party.</summary>
    public ImmutableArray<PartyPokemon> TeachyTvBattleParty => State.TeachyTv?.Battle is {} b
        ? b.Frame.Party(BattleSide.Player).Members.Select((m, i) => TeachyTvTemporaryMember(State, m.Pokemon, m.Status, i)).ToImmutableArray() : [];
    public CampaignWildBattle? TeachyTvBattlePresentation => State.TeachyTv?.Battle is {} b ? ProjectTeachyTvBattle(State, b) : null;

    private Decision<CampaignState> BeginTeachyTvBattle(CampaignState state, TeachyTvDemonstrationRequest request)
    {
        if (state.TeachyTv is not {} tv || tv.Playback.Phase != TeachyTvPhase.AwaitingDemonstration ||
            tv.Playback.WaitId != request.Ticket || request.Lesson.DemonstrationKind != TeachyTvDemonstrationKind.PokedudeBattle)
            return new(CommandStatus.Rejected, state, "teachy_tv_battle_context");
        if (_content.PresetDoubleItems is null) return new(CommandStatus.Rejected, state, "teachy_tv_battle_items_missing");
        bool Badge(string name) => state.Script.Flags.GetValueOrDefault(name);
        var begun = CampaignTeachyTvBattleRules.Start(SafariOrdinaryOwnership.Mechanics, request, state.TrainerId,
            state.PlayerName, _content.MapDefinition(state.World.Player.MapId).RegionMapSection, state.World.Rng,
            new(Badge("FLAG_BADGE01_GET"), Badge("FLAG_BADGE05_GET"), Badge("FLAG_BADGE03_GET"), Badge("FLAG_BADGE07_GET")));
        return new(CommandStatus.Accepted, state with { TeachyTv = tv with { Battle = begun.State },
            World = state.World with { Rng = begun.State.Frame.Rng }, Music = "MUS_VS_WILD" });
    }

    private Decision<CampaignState> DecideTeachyTvBattle(CampaignState state, CampaignCommand command)
    {
        var tv = state.TeachyTv!;
        if (tv.Battle is not {} battle || tv.Playback.Phase != TeachyTvPhase.AwaitingDemonstration ||
            tv.Playback.WaitId != battle.Ticket || _content.PresetDoubleItems is null)
            return new(CommandStatus.Rejected, state, "teachy_tv_battle_context");
        var result = CampaignTeachyTvBattleRules.Decide(SafariOrdinaryOwnership.Mechanics, _content.PresetDoubleItems,
            _content.TeachyTv!.Lessons[battle.Lesson].Battle!, battle, command.Kind, command.Text);
        if (!result.Accepted) return new(CommandStatus.Rejected, state, result.Error);
        string music = command.Kind == "teachy_tv_battle_confirm" && battle.Voiceover.Pending?.StringId == "STRINGID_PKMNGAINEDEXP"
            ? "MUS_VICTORY_WILD" : result.State.CurrentStep?.Event is SourcePokedudeCaughtEvent ? "MUS_CAUGHT" : state.Music;
        var next = state with { TeachyTv = tv with { Battle = result.State }, World = state.World with { Rng = result.State.Frame.Rng }, Music = music };
        return result.State.Stage == CampaignTeachyTvBattleStage.Finished ? CompleteTeachyTvBattle(next) : new(CommandStatus.Accepted, next);
    }

    /// <summary>Only the real session owner can return its issued ticket. No public
    /// command supplies an outcome, success bit, capture, party, inventory or reward.</summary>
    private Decision<CampaignState> CompleteTeachyTvBattle(CampaignState state)
    {
        var tv = state.TeachyTv!; var b = tv.Battle!;
        if (b.Stage != CampaignTeachyTvBattleStage.Finished) return new(CommandStatus.Rejected, state, "teachy_tv_battle_not_finished");
        return CompleteTeachyTvDemonstration(state with { World = state.World with { Rng = b.Frame.Rng } },
            b.Ticket, b.Interrupted, b.SourceOutcome);
    }

    private PartyPokemon TeachyTvTemporaryMember(CampaignState state, PokemonInstance pokemon, uint status, int index) =>
        new(index, pokemon, _content.PokemonContent!.SpeciesNames[pokemon.SourceSpeciesId], state.PlayerName, state.PlayerGender,
            2, _content.MapDefinition(state.World.Player.MapId).RegionMapSection, pokemon.Level, 4, "ITEM_POKE_BALL", "ITEM_NONE") { Status = status };

    private CampaignWildBattle ProjectTeachyTvBattle(CampaignState state, CampaignTeachyTvBattle b)
    {
        int active = b.Frame.Get(new(0)).Member!.Value.Index;
        var body = b.Frame.Party(BattleSide.Player).Members[active];
        var owned = TeachyTvTemporaryMember(state, body.Pokemon, body.Status, active);
        bool Exportable(CampaignTeachyTvBattleStep step) => step.Reward is null && step.Event is not SourcePokedudeCaughtEvent;
        var events = b.Steps.Where(Exportable).Select(s => SourceBattleEventAdapter.Export(s.Event)).ToImmutableArray();
        int index = b.Steps.Take(b.StepIndex).Count(Exportable);
        var pages = b.CurrentStep is { Reward: {} reward, Recipient: {} recipient }
            ? ImmutableArray.Create(new CampaignWildPage("source-reward", reward, recipient)) : [];
        var phase = !pages.IsEmpty ? CampaignBattlePhase.Results : b.CurrentStep is {} step && Exportable(step)
            ? CampaignBattlePhase.Events : b.Stage is CampaignTeachyTvBattleStage.ChooseAction or CampaignTeachyTvBattleStage.ChooseMove
                ? CampaignBattlePhase.ChooseMove : CampaignBattlePhase.Results;
        var displayPlayer = b.CurrentStep?.Event is SourcePlayerSwitchedEvent switched ? switched.Replacement.Body.Pokemon : b.DisplayPlayer;
        var projection = new CampaignWildBattle(b.Ticket, owned, b.Frame, events, index, displayPlayer, b.DisplayOpponent,
            phase, pages, 0, b.RandomTrace) { ActivePartyIndex = active,
            SourceEvents = b.Steps.Select(s => s.Event).DistinctBy(e => e.Sequence).ToImmutableArray() };
        if (b.CurrentStep?.Event is SourcePlayerSwitchedEvent change)
            projection = projection with { LastSwitch = new(TeachyTvTemporaryMember(state, change.Previous.Body.Pokemon,
                change.Previous.Body.Status, 0), "sText_PkmnThatsEnough", "sText_GoPkmn2") };
        return projection;
    }
}
