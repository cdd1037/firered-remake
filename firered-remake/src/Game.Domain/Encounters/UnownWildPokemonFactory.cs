using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>GenerateWildMon's Unown-only source-slot projection. The encounter owner must already
/// have resolved the unlock/header gate, slot, level and Repel. This helper grants none of them.</summary>
public static class UnownWildPokemonFactory
{
    public const string RulesVersion = "firered-unown-source-generation-v1";

    public static Route1WildPokemonResult GenerateSourceSlot(SourceMechanicsCatalog mechanics,
        MapId chamber, int sourceSlotIndex, byte level, RngState globalRng, uint playerTrainerId)
    {
        if (mechanics is null)
            throw new ArgumentException("unown_source_mechanics", nameof(mechanics));
        int form = UnownFormRules.SourceSlotForm(chamber, sourceSlotIndex);
        if (level != 25) throw new ArgumentOutOfRangeException(nameof(level), "unown_source_level");
        if (globalRng.AlgorithmId != Gen3Rng.AlgorithmId)
            throw new NotSupportedException("Unknown global RNG algorithm.");

        // All state is local until the entire PID/IV creation succeeds. An exhausted cursor
        // throws without returning a partial instance, RNG state or trace.
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        uint Draw(string purpose)
        {
            var draw = Gen3Rng.Next(globalRng, purpose);
            globalRng = draw.State; trace.Add(draw.Trace); return draw.Value;
        }
        uint personality;
        ulong attempt = 0;
        do
        {
            attempt = checked(attempt + 1);
            string prefix = "wild.unown.personality." + attempt.ToString(CultureInfo.InvariantCulture);
            // Explicit high-then-low matches the retained host source probe. Original target
            // compiler ordering is an open verification gate; see tests/Game.UnownRules.Tests/README.md.
            uint high = Draw(prefix + ".high16");
            uint low = Draw(prefix + ".low16");
            personality = (high << 16) | low;
        } while (UnownFormRules.Form(personality) != form);

        // Fixed personality skips both ordinary nature selection and CreateMon PID draws.
        // Authenticated complete mechanics allows the same shared creation/IV/stat/move owners.
        var created = PokemonFactory.CreateCore(mechanics.Catalog,
            new(UnownFormRules.Species, level, playerTrainerId, IvPolicy.Random,
                PersonalityPolicy.Fixed(personality)), globalRng, mechanics.Data);
        if (created.Trace.Length != 2) throw new InvalidOperationException("unown_fixed_pid_iv_contract");
        trace.AddRange(created.Trace.Select(draw => draw with { Purpose = "wild." + draw.Purpose["pokemon.".Length..] }));
        return new(created.Pokemon, created.Rng, trace.ToImmutable());
    }

    /// <summary>Separate later SetWildMonHeldItem call. Equal ITEM_NONE slots still draw once.</summary>
    public static Route1WildHeldItemResult AssignHeldItem(RngState globalRng)
    {
        var draw = Gen3Rng.Next(globalRng, "wild-battle.held-item", 100);
        return new("ITEM_NONE", draw.State, [draw.Trace]);
    }
}
