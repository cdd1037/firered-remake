using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

internal static class ControlPowerAnimationKernelFixture
{
    internal static (string Name, string Species, int Level, string Effect) Spec(int id) => id switch {
        175 => ("FLAIL", "MAGIKARP", 50, "EFFECT_FLAIL"), 179 => ("REVERSAL", "HITMONLEE", 60, "EFFECT_FLAIL"),
        182 => ("PROTECT", "SQUIRTLE", 50, "EFFECT_PROTECT"), 247 => ("SHADOW_BALL", "GASTLY", 50, "EFFECT_SPECIAL_DEFENSE_DOWN_HIT"),
        269 => ("TAUNT", "MURKROW", 50, "EFFECT_TAUNT"), 266 => ("FOLLOW_ME", "CLEFAIRY", 50, "EFFECT_FOLLOW_ME"),
        213 => ("ATTRACT", "SKITTY", 50, "EFFECT_ATTRACT"), 259 => ("TORMENT", "NUZLEAF", 50, "EFFECT_TORMENT"),
        _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    internal static MoveId Move(int id) => Kernel.Id(Spec(id).Name);
    internal static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        // Gender is source-generated from personality. Every possible selected target
        // is male, while Attract's natural Skitty user is female, including allied targets.
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100, personality: 254) with { SpeedStage = 0 }).ToArray();
        var spec = Spec(id); bodies[actor] = Kernel.Mon(spec.Species, spec.Name, spec.Level) with { AccuracyStage = 12, SpeedStage = 12 }; return bodies;
    }
    internal static SourceBattleEvent Marker(ImmutableArray<SourceBattleEvent> events, int id) => id switch {
        182 => events.OfType<SourceProtectAttemptEvent>().Single(),
        213 => events.OfType<SourceInfatuationChangedEvent>().Single(e => e.Reason == "attract"),
        266 or 269 or 259 => events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == (id == 266 ? "follow-me" : id == 269 ? "taunt" : "torment") && e.Reason != "countdown"),
        _ => Envelope.Hits(events).Single() };
    internal static SourceKernelResult Resolve(int id, bool single, int actor = 0, int target = 1) =>
        Envelope.Resolve(Kernel.Frame(Bodies(id, actor), single), actor, target);
    internal static void Deny(ImmutableArray<SourceBattleEvent> events, bool single) =>
        Envelope.Deny(events, single, "Unowned selected-volatile or prepared-hit invocation.");
    internal static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events, int index, bool single) => Kernel.Project(events, index, single);
}
