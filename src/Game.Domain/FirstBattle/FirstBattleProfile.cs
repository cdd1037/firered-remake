using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.FirstBattle;

/// <summary>
/// The tutorial executor and its specialized AI share this bounded capability contract.
/// Numeric move data belongs to the supplied Pokemon catalog, not a second source table here.
/// </summary>
internal static class FirstBattleProfile
{
    internal static ImmutableArray<MoveId> OwnedMoves { get; } =
        [new("pret:MOVE_TACKLE"), new("pret:MOVE_SCRATCH"), new("pret:MOVE_GROWL"), new("pret:MOVE_TAIL_WHIP")];

    internal static int StarterSpecies(SpeciesId species) => species.Value switch
    {
        "pret:SPECIES_BULBASAUR" => 1,
        "pret:SPECIES_CHARMANDER" => 4,
        "pret:SPECIES_SQUIRTLE" => 7,
        _ => 0
    };

    // Fixed two-slot ownership is tutorial policy; PP, power and accuracy come from the catalog.
    internal static string AttackMove(int species) => species == 4 ? "pret:MOVE_SCRATCH" : "pret:MOVE_TACKLE";
    internal static string StatusMove(int species) => species == 7 ? "pret:MOVE_TAIL_WHIP" : "pret:MOVE_GROWL";

    internal static bool SupportsMove(PokemonMoveDefinition? move)
    {
        if (!SourceMoveDefinitionRules.IsValid(move) || move!.Type != "TYPE_NORMAL" ||
            move.Priority != 0 || move.SecondaryEffectChance != 0 || move.Accuracy == 0)
            return false;
        // Accuracy zero has a special always-hit meaning not implemented by this tutorial.
        // Power one also has a special AI interpretation; status moves cannot enter damage prediction.
        return move.Id.Value switch
        {
            "pret:MOVE_TACKLE" or "pret:MOVE_SCRATCH" => move.Power >= 2 &&
                move.Effect == "EFFECT_HIT" && move.Target == "MOVE_TARGET_SELECTED",
            "pret:MOVE_GROWL" => move.Power == 0 && move.Effect == "EFFECT_ATTACK_DOWN" && move.Target == "MOVE_TARGET_BOTH",
            "pret:MOVE_TAIL_WHIP" => move.Power == 0 && move.Effect == "EFFECT_DEFENSE_DOWN" && move.Target == "MOVE_TARGET_BOTH",
            _ => false
        };
    }

    // The specialized AI omits type-immunity, ability-guess and physical-type branches only
    // for these exact type/ability combinations. They remain execution limits, not data copies.
    internal static bool SupportsSpecies(PokemonSpeciesDefinition species) =>
        (species.SourceSpeciesId, species.Type1, species.Type2, species.Ability1, species.Ability2) is
            (1, "TYPE_GRASS", "TYPE_POISON", "ABILITY_OVERGROW", "ABILITY_NONE") or
            (4, "TYPE_FIRE", "TYPE_FIRE", "ABILITY_BLAZE", "ABILITY_NONE") or
            (7, "TYPE_WATER", "TYPE_WATER", "ABILITY_TORRENT", "ABILITY_NONE");
}
