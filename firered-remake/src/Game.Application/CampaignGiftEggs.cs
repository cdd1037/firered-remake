using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Application;

// Gift eggs have no breeding parents. Replay only the authenticated source factory
// and immutable creation identity; normal hatching uses the existing shared receipt.
public sealed record CampaignGiftEggReceipt(long InstanceId, StorySource Source,
    int SourceSpeciesId, uint Personality, SixStats Ivs, RngState RngBefore, RngState RngAfter)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CampaignDayCareHatchReceipt? Hatch { get; init; }
}

public sealed partial class CampaignSession
{
    private const string GiftEggMap = "map.five-island-water-labyrinth";
    private const string GiftEggPath = "data/maps/FiveIsland_WaterLabyrinth/scripts.inc";
    private const string GiftEggPrefix = "FiveIsland_WaterLabyrinth_EventScript_";
    private const string GiftEggReceivedFlag = "FLAG_GOT_TOGEPI_EGG";
    private const string GiftEggNoRoomFlag = "FLAG_NO_ROOM_FOR_TOGEPI_EGG";

    private static IEnumerable<long> EggAcquisitionIds(CampaignState state) =>
        (state.DayCareEggs ?? []).Select(e => e.InstanceId).Concat((state.GiftEggs ?? []).Select(e => e.InstanceId));

    private static CampaignDayCareHatchReceipt? EggHatchReceipt(CampaignState state, long instanceId)
    {
        var matches = (state.DayCareEggs ?? []).Where(e => e.InstanceId == instanceId).Select(e => e.Hatch)
            .Concat((state.GiftEggs ?? []).Where(e => e.InstanceId == instanceId).Select(e => e.Hatch)).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("hatch_receipt_missing_or_duplicate");
        return matches[0];
    }

    private static CampaignState WriteEggHatchReceipt(CampaignState state, long instanceId, CampaignDayCareHatchReceipt hatch)
    {
        _ = EggHatchReceipt(state, instanceId);
        int bred = Array.FindIndex((state.DayCareEggs ?? []).ToArray(), e => e.InstanceId == instanceId);
        if (bred >= 0)
            return state with { DayCareEggs = state.DayCareEggs!.Value.SetItem(bred, state.DayCareEggs.Value[bred] with { Hatch = hatch }) };
        int gift = Array.FindIndex((state.GiftEggs ?? []).ToArray(), e => e.InstanceId == instanceId);
        return state with { GiftEggs = state.GiftEggs!.Value.SetItem(gift, state.GiftEggs.Value[gift] with { Hatch = hatch }) };
    }

    private StoryInstruction? GiftEggCreationInstruction() => _content.FindProgram(GiftEggPrefix + "TryGiveEgg")?.Instructions
        .SingleOrDefault(i => i.Opcode == "giveegg" && i.Args.SequenceEqual(new[] { "SPECIES_TOGEPI" }) &&
            i.Source.Path == GiftEggPath && i.Source.Symbol == GiftEggPrefix + "TryGiveEgg");

    private string? ValidateGiftEggReceipts(CampaignState state)
    {
        var receipts = state.GiftEggs ?? [];
        if (!_content.LostCaveEnabled)
            return state.GiftEggs is not null || state.Script.Flags.GetValueOrDefault(GiftEggReceivedFlag) ||
                state.Script.Flags.GetValueOrDefault(GiftEggNoRoomFlag) ? "save_gift_egg_content_missing" : null;
        if (receipts.IsDefault || receipts.Length > 1 || receipts.Any(e => e is null)) return "save_gift_egg_receipts";
        if (state.Script.Flags.GetValueOrDefault(GiftEggReceivedFlag) != (receipts.Length == 1)) return "save_gift_egg_flag";
        foreach (var receipt in receipts)
        {
            if (receipt.InstanceId < 2 || receipt.InstanceId >= state.NextPokemonId || receipt.SourceSpeciesId != 175 ||
                receipt.Source is null || GiftEggCreationInstruction()?.Source != receipt.Source || receipt.Ivs is null ||
                receipt.RngAfter.Cursor > state.World.Rng.Cursor) return "save_gift_egg_origin";
            var matches = OwnedPokemon(state).Where(p => p.InstanceId == receipt.InstanceId).ToArray();
            if (matches.Length > 1) return "save_gift_egg_identity";
            var owned = matches.SingleOrDefault();
            if (owned is not null && (owned.Pokemon.Personality != receipt.Personality || owned.Pokemon.Ivs != receipt.Ivs || owned.MetLevel != 0 ||
                owned.Pokemon.OriginalTrainerId != state.TrainerId || owned.OriginalTrainerName != state.PlayerName ||
                owned.OriginalTrainerGender != state.PlayerGender || owned.Ball != "ITEM_POKE_BALL" || owned.MetGame != 4 ||
                owned.ModernFatefulEncounter || !EvolutionSpeciesMatchesOrigin(state, owned, receipt.SourceSpeciesId)))
                return "save_gift_egg_identity";
            try
            {
                var made = SourceGiftEggRules.GiveEgg(_content.PokemonContent!, BreedingMetadata, SourceGiftEggRules.Togepi,
                    receipt.InstanceId, EggTrainer(state), 4, receipt.RngBefore);
                if (made.Rng != receipt.RngAfter || made.Egg.Pokemon.Personality != receipt.Personality || made.Egg.Pokemon.Ivs != receipt.Ivs)
                    return "save_gift_egg_creation";
                if (receipt.Hatch is not {} hatch)
                {
                    if (owned is null || !owned.IsEgg || owned.MetLocation != SourceGiftEggRules.MetLocation ||
                        owned.Pokemon.EggStatCalculationIvs != made.Egg.Pokemon.Ivs ||
                        !owned.Pokemon.Moves.SequenceEqual(made.Egg.Pokemon.Moves)) return "save_gift_egg_unhatched_identity";
                }
                else
                {
                    if (owned is not null && (owned.IsEgg || owned.Language != 2 || owned.MetLocation != hatch.Location) ||
                        hatch.RngBefore.Cursor < receipt.RngAfter.Cursor || hatch.RngAfter.Cursor > state.World.Rng.Cursor ||
                        !_content.Maps.Keys.Any(m => _content.MapDefinition(m).RegionMapSection == hatch.Location))
                        return "save_gift_egg_hatch_identity";
                    var hatched = SourceEggRules.Hatch(_content.PokemonContent!, made.Egg, EggTrainer(state), hatch.Location, hatch.RngBefore);
                    var shards = SourceEggRules.CreateNormalHatchShards(hatched.Rng);
                    if (shards.Rng != hatch.RngAfter || !state.SeenSpecies.Contains(receipt.SourceSpeciesId) ||
                        !state.CaughtSpecies.Contains(receipt.SourceSpeciesId)) return "save_gift_egg_hatch_rng_or_dex";
                }
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or InvalidDataException or KeyNotFoundException or NotSupportedException or OverflowException)
            { return "save_gift_egg_creation"; }
        }
        return null;
    }

    private bool InGiftEggScript(CampaignState state, bool requireLock) => _content.LostCaveEnabled &&
        (!requireLock || state.Script.Locked) && !state.Script.Completed && state.Script.QuestLogState == 0 &&
        state.Script.Wait?.Kind == StoryWaitKind.NativeEffect && state.World.Player.MapId.Value == GiftEggMap &&
        state.LastTalked is {} id && state.Actors.TryGetValue(id, out var actor) && actor.Active && !actor.Invisible &&
        actor.SourceLocalId == 1 && actor.GraphicsId == "OBJ_EVENT_GFX_GENTLEMAN" &&
        actor.ScriptId == GiftEggPrefix + "EggGentleman" && actor.TemplateX == 14 && actor.TemplateY == 11 && actor.Elevation == 3 &&
        state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") == 1 &&
        _content.MapDefinition(state.World.Player.MapId).Actors.FirstOrDefault(a => a.LocalId == id) is
            { SourceLocalId: 1, IsClone: false, GraphicsId: "OBJ_EVENT_GFX_GENTLEMAN", ScriptId: GiftEggPrefix + "EggGentleman", X: 14, Y: 11, Elevation: 3 };

    private bool TryApplyGiftEggEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.LostCaveEnabled || effect.Source.Path != GiftEggPath ||
            effect.Opcode is not ("special" or "specialvar" or "getpartysize" or "giveegg")) return false;
        bool cut = effect.Opcode == "special" && effect.Source.Symbol == GiftEggPrefix + "EggGentleman" &&
            effect.Args.SequenceEqual(new[] { "QuestLog_CutRecording" });
        var instruction = DayCareInstruction(state);
        if (!InGiftEggScript(state, !cut) || instruction is null || instruction.Source != effect.Source ||
            instruction.Opcode != effect.Opcode || !instruction.Args.SequenceEqual(effect.Args))
            throw new InvalidDataException("gift_egg_source_context");
        CampaignState SetResult(int value) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", value) } };
        if (cut) return true;
        if (effect.Opcode == "specialvar" && effect.Source.Symbol == GiftEggPrefix + "EggGentleman" &&
            effect.Args.SequenceEqual(new[] { "VAR_RESULT", "GetLeadMonFriendship" }))
        {
            next = SetResult(SourceGiftEggRules.GetLeadMonFriendship(_content.PokemonContent!, state.Party)); return true;
        }
        if (effect.Opcode == "specialvar" && effect.Source.Symbol == GiftEggPrefix + "PostEggComment" &&
            effect.Args.SequenceEqual(new[] { "VAR_RESULT", "PlayerPartyContainsSpeciesWithPlayerID" }))
        {
            int species = state.Script.Variables.GetValueOrDefault("VAR_0x8004");
            if (species is not (175 or 176)) throw new InvalidDataException("gift_egg_comment_species");
            next = SetResult(SourceGiftEggRules.PlayerPartyContainsSpeciesWithPlayerId(_content.PokemonContent!, state.Party,
                new SpeciesId(species == 175 ? "pret:SPECIES_TOGEPI" : "pret:SPECIES_TOGETIC"), state.TrainerId) ? 1 : 0); return true;
        }
        if (effect.Source.Symbol != GiftEggPrefix + "TryGiveEgg") throw new InvalidDataException("gift_egg_source_label");
        if (effect.Opcode == "getpartysize" && effect.Args.IsEmpty)
        {
            next = SetResult(state.Party.Length); return true;
        }
        if (effect.Opcode != "giveegg" || !effect.Args.SequenceEqual(new[] { "SPECIES_TOGEPI" }) ||
            !state.Script.Flags.GetValueOrDefault(GiftEggReceivedFlag) || !(state.GiftEggs ?? []).IsEmpty ||
            state.Party.Length >= 6 || state.NextPokemonId is < 2 or long.MaxValue)
            throw new InvalidDataException("gift_egg_receipt_context");
        var made = SourceGiftEggRules.GiveEgg(_content.PokemonContent!, BreedingMetadata, SourceGiftEggRules.Togepi,
            state.NextPokemonId, EggTrainer(state), 4, state.World.Rng);
        var receipt = new CampaignGiftEggReceipt(made.Egg.InstanceId, effect.Source, made.Egg.Pokemon.SourceSpeciesId,
            made.Egg.Pokemon.Personality, made.Egg.Pokemon.Ivs, state.World.Rng, made.Rng);
        next = SetResult(0) with { Party = state.Party.Add(made.Egg), GiftEggs = (state.GiftEggs ?? []).Add(receipt),
            NextPokemonId = checked(state.NextPokemonId + 1), World = state.World with { Rng = made.Rng } };
        return true;
    }
}
