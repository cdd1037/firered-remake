using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Tutorials;

namespace Game.Application;

public sealed record CampaignTeachyTvField(TeachyTvFieldDemoState State,
    ImmutableArray<TeachyTvFieldDemoEffect> Effects, int AdvancedFrames = 0);
public sealed partial record CampaignTeachyTv
{
    public CampaignTeachyTvField? Field { get; init; }
}

public sealed partial class CampaignSession
{
    private Decision<CampaignState> BeginTeachyTvDemonstration(CampaignState state, TeachyTvDemonstrationRequest request)
    {
        if (request.Lesson.DemonstrationKind == TeachyTvDemonstrationKind.PokedudeBattle)
            return BeginTeachyTvBattle(state, request);
        var data = TeachyTvFieldDemoRules.CreateData(_content.QuestItems!, _content.EarlyShop!,
            _content.PewterMachines!, _content.PokemonContent!.Catalog);
        var field = TeachyTvFieldDemoRules.Begin(request.Ticket, request.Lesson.DemonstrationKind, data);
        return new(CommandStatus.Accepted, state with { TeachyTv = state.TeachyTv! with { Field = new(field, []) } });
    }

    private Decision<CampaignState> DecideTeachyTvDemonstration(CampaignState state, CampaignCommand command)
    {
        if (state.TeachyTv!.Field is not {} field) return DecideTeachyTvBattle(state, command);
        if (command.Kind != "teachy_tv_field_tick" || command.Text != field.State.WaitId ||
            command.Quantity is < 1 or > 60 || command.Choice is not (>= 0 and <= 31))
            return new(CommandStatus.Rejected, state, "teachy_tv_field_context");
        int input = command.Choice.Value;
        var current = field.State;
        var effects = ImmutableArray.CreateBuilder<TeachyTvFieldDemoEffect>();
        int frames = 0;
        while (frames < command.Quantity && !current.Completed)
        {
            var decision = TeachyTvFieldDemoRules.Step(current, current.WaitId, new(
                Confirm: frames == 0 && (input & 1) != 0, Cancel: frames == 0 && (input & 2) != 0,
                PaletteFadeActive: (input & 4) != 0, TextPrinterActive: (input & 8) != 0,
                MenuTransitionActive: (input & 16) != 0));
            if (!decision.Accepted) return new(CommandStatus.Rejected, state, decision.Error);
            current = decision.State; effects.AddRange(decision.Effects); frames++;
            // A new visual gate must reach the renderer before any subsequent source
            // controller frame can observe its fade/text/menu-transition status.
            if (!decision.Effects.IsEmpty) break;
        }
        state = state with { TeachyTv = state.TeachyTv with { Field = new(current, effects.ToImmutable(), frames) } };
        return current.Completed ? CompleteTeachyTvDemonstration(state, current.Ticket, current.Interrupted)
            : new(CommandStatus.Accepted, state);
    }

    private Decision<CampaignState> CompleteTeachyTvDemonstration(CampaignState state, string ticket,
        bool interrupted, int? battleOutcome = null)
    {
        var scene = state.TeachyTv!;
        var result = TeachyTvPlaybackRules.CompleteDemonstration(_content.TeachyTv!, scene.Playback,
            ticket, interrupted, battleOutcome);
        if (!result.Accepted) return new(CommandStatus.Rejected, state, result.Error);
        return new(CommandStatus.Accepted, state with { TeachyTv = scene with { Playback = result.State, Field = null, Battle = null },
            Music = result.State.Phase == TeachyTvPhase.Menu ? "MUS_TEACHY_TV_MENU" : "MUS_FOLLOW_ME" });
    }
}
