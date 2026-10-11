using System.Collections.Immutable;
using Game.Domain.Pokemon;

namespace Game.Domain.World;

/// <summary>The first five values of CollisionType in include/global.fieldmap.h.</summary>
public enum FieldHmCollision { None = 0, OutsideRange = 1, Impassable = 2, ElevationMismatch = 3, ObjectEvent = 4 }
public enum FieldStrengthInteraction { CannotUse, AlreadyActive, OfferMove }
public enum FieldStrengthPush { Blocked, OrdinaryPush, FallWarpRequiresHandler }

/// <summary>
/// Source predicates only: does not enable a map, move the player, start a battle,
/// change PP, or replace source collision/terrain resolution.
/// </summary>
public static class FieldHmSourceRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SurfMove = "pret:MOVE_SURF";
    public const string StrengthMove = "pret:MOVE_STRENGTH";
    public const string SurfBadge = "FLAG_BADGE05_GET";
    public const string StrengthBadge = "FLAG_BADGE04_GET";
    public const string StrengthActiveFlag = "FLAG_SYS_USE_STRENGTH";
    public const string BoulderGraphics = "OBJ_EVENT_GFX_PUSHABLE_BOULDER";
    public const int NoPartyMember = 6;
    public const int FastWaterBehavior = 0x11;
    public const int FallWarpBehavior = 0x66;
    public const int NonAnimatedDoorBehavior = 0x60;
    public const int ShoreElevation = 3;
    public const int FieldUsePpCost = 0;

    // ScrCmd_checkpartymove excludes Eggs, checks move IDs, and never checks HP or PP.
    // Party menu field-move selections also exclude Eggs before reaching the callback.
    public static bool Knows(PartyPokemon member, string move)
    {
        ArgumentNullException.ThrowIfNull(member);
        RequireMove(move);
        return !member.IsEgg && member.Pokemon.Moves.Any(slot => slot.Move.Value == move);
    }

    public static int FirstPartyMember(ImmutableArray<PartyPokemon> party, string move)
    {
        RequireMove(move);
        if (party.IsDefault || party.Length > NoPartyMember) throw new ArgumentException("Invalid party.", nameof(party));
        for (int i = 0; i < party.Length; i++) if (Knows(party[i], move)) return i;
        return NoPartyMember;
    }

    // field_player_avatar.c:IsPlayerFacingSurfableFishableWater. Collision must be
    // resolved using the original priority: terrain/camera -> elevation -> objects.
    public static bool FacesSurfableWater(int playerElevation, FieldHmCollision facingCollision, bool facingWaterTerrain)
    {
        RequireElevation(playerElevation); RequireCollision(facingCollision);
        return facingCollision == FieldHmCollision.ElevationMismatch && playerElevation == ShoreElevation && facingWaterTerrain;
    }

    // party_menu.c:SetUpFieldMove_Surf plus the party-menu badge gate.
    // The source interaction path offers Yes/No before it begins the same effect.
    public static bool CanStartSurf(PartyPokemon member, bool hasSoulBadge, bool alreadySurfing,
        int playerElevation, int facingBehavior, FieldHmCollision facingCollision, bool facingWaterTerrain)
    {
        RequireBehavior(facingBehavior);
        return Knows(member, SurfMove) && hasSoulBadge && !alreadySurfing && facingBehavior != FastWaterBehavior &&
            FacesSurfableWater(playerElevation, facingCollision, facingWaterTerrain);
    }

    // fldeff_strength.c:SetUpFieldMove_Strength plus the party-menu badge gate.
    // Already-active Strength is allowed through the menu callback in the source.
    public static bool CanStartStrength(PartyPokemon member, bool hasRainbowBadge, bool surfing, string? facingGraphics) =>
        Knows(member, StrengthMove) && hasRainbowBadge && !surfing && facingGraphics == BoulderGraphics;

    // data/scripts/field_moves.inc:EventScript_StrengthBoulder checks active before
    // checking the party. Once activated it does not need a continuing move owner.
    public static FieldStrengthInteraction StrengthInteraction(bool hasRainbowBadge, bool strengthActive, bool partyKnowsStrength) =>
        !hasRainbowBadge ? FieldStrengthInteraction.CannotUse : strengthActive ? FieldStrengthInteraction.AlreadyActive :
        partyKnowsStrength ? FieldStrengthInteraction.OfferMove : FieldStrengthInteraction.CannotUse;

    // field_player_avatar.c:CanStopSurfing runs ONLY following elevation mismatch.
    // The actor test is at elevation 3; do not reuse an elevation-1 surf occupancy test.
    public static bool CanStopSurfing(bool surfing, FieldHmCollision facingCollision, int facingElevation,
        bool actorOccupiesShoreElevation)
    {
        RequireCollision(facingCollision); RequireElevation(facingElevation);
        return surfing && facingCollision == FieldHmCollision.ElevationMismatch && facingElevation == ShoreElevation &&
            !actorOccupiesShoreElevation;
    }

    // TryPushBoulder has a fall-warp OR clause before ordinary collision/door tests.
    // A caller without the hole/switch handler must reject the explicit third outcome.
    // Successful ordinary pushes move only the boulder: the player walks in place.
    public static FieldStrengthPush ClassifyStrengthPush(bool strengthActive, string? facingGraphics,
        int targetBehavior, FieldHmCollision boulderTargetCollision)
    {
        RequireBehavior(targetBehavior); RequireCollision(boulderTargetCollision);
        if (!strengthActive || facingGraphics != BoulderGraphics) return FieldStrengthPush.Blocked;
        if (targetBehavior == FallWarpBehavior) return FieldStrengthPush.FallWarpRequiresHandler;
        return boulderTargetCollision == FieldHmCollision.None && targetBehavior != NonAnimatedDoorBehavior
            ? FieldStrengthPush.OrdinaryPush : FieldStrengthPush.Blocked;
    }

    /// <summary>GetCollisionAtCoords priority for an admitted stationary, untracked boulder.
    /// Zero movement ranges mean unlimited, not zero permitted travel. The application
    /// authenticates that range before this pure terrain/elevation/object predicate.</summary>
    public static FieldHmCollision StrengthTargetCollision(ConnectedFieldCell here, ConnectedFieldCell? target,
        int boulderElevation, Direction direction, bool objectAtTarget, bool outsideMovementRange = false)
    {
        RequireElevation(boulderElevation);
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (outsideMovementRange) return FieldHmCollision.OutsideRange;
        if (target is null || target.Collision != 0 || ConnectedFieldRules.BlocksEdge(here.SourceBehavior, direction) ||
            ConnectedFieldRules.BlocksEdge(target.SourceBehavior, ConnectedFieldRules.Opposite(direction)))
            return FieldHmCollision.Impassable;
        RequireElevation(target.Elevation);
        if (boulderElevation != 0 && target.Elevation != 0 && target.Elevation != 15 && boulderElevation != target.Elevation)
            return FieldHmCollision.ElevationMismatch;
        return objectAtTarget ? FieldHmCollision.ObjectEvent : FieldHmCollision.None;
    }

    private static void RequireMove(string move)
    { if (move is not (SurfMove or StrengthMove)) throw new NotSupportedException("Only Surf and Strength are covered."); }
    private static void RequireBehavior(int behavior)
    { if (behavior is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(behavior)); }
    private static void RequireElevation(int elevation)
    { if (elevation is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(elevation)); }
    private static void RequireCollision(FieldHmCollision collision)
    { if (!Enum.IsDefined(collision)) throw new ArgumentOutOfRangeException(nameof(collision)); }
}
