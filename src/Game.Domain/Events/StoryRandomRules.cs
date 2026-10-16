using System.Globalization;
using Game.Domain.Randomness;

namespace Game.Domain.Events;

public sealed record StoryRandomResult(StoryState Story, RngState Rng, RandomTrace Trace);

/// <summary>ScrCmd_random: one shared Gen III draw, reduced modulo VarGet(max).</summary>
public static class StoryRandomRules
{
    public static StoryRandomResult Apply(StoryState story, RngState rng, StoryEffect effect)
    {
        if (effect.Opcode != "random" || effect.Args.Length != 1 ||
            story.Wait is not { Kind: StoryWaitKind.NativeEffect } wait ||
            wait.CorrelationId != "event:" + effect.Sequence || story.EffectSequence != effect.Sequence)
            throw new InvalidDataException("story_random_effect_context");
        if (!int.TryParse(effect.Args[0], NumberStyles.None, CultureInfo.InvariantCulture, out int maximum) ||
            maximum is < 1 or > ushort.MaxValue)
            throw new InvalidDataException("story_random_maximum");
        var draw = Gen3Rng.Next(rng, "story.random", maximum);
        return new(story with { Variables = story.Variables.SetItem("VAR_RESULT", draw.Trace.Result) },
            draw.State, draw.Trace);
    }
}
