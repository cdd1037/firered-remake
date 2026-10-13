using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.World;
using Game.Domain.Pokemon;

namespace Game.Application;

public sealed record CampaignDojoGift(int SourceSpeciesId, long InstanceId, uint Personality, SixStats Ivs);

public sealed partial class CampaignSession
{
    private const string FightingDojoMap = "map.saffron-city-dojo";
    private const string FightingDojoSource = "data/maps/SaffronCity_Dojo/scripts.inc";
    private const string FightingDojoGiftFlag = "FLAG_GOT_HITMON_FROM_DOJO";
    private static string DojoBallScript(int species) => species switch
    {
        106 => "SaffronCity_Dojo_EventScript_HitmonleeBall",
        107 => "SaffronCity_Dojo_EventScript_HitmonchanBall",
        _ => throw new InvalidDataException("dojo_gift_species")
    };
    private static string DojoBallFlag(int species) => species == 106 ? "FLAG_HIDE_DOJO_HITMONLEE_BALL" : "FLAG_HIDE_DOJO_HITMONCHAN_BALL";
    private int? DojoGiftCallerSpecies(CampaignState state)
    {
        if (!_content.FightingDojoEnabled || state.World.Player.MapId.Value != FightingDojoMap ||
            state.LastTalked is not { } id || !state.Actors.TryGetValue(id, out var actor)) return null;
        int species = actor.SourceLocalId switch { 6 => 106, 7 => 107, _ => 0 };
        return species != 0 && actor.ScriptId == DojoBallScript(species) &&
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") == actor.SourceLocalId ? species : null;
    }
    private bool InDojoGiftScript(CampaignState state) => DojoGiftCallerSpecies(state) is not null;

    private static void ValidateFightingDojoCatalog(ICampaignCatalog content)
    {
        if (!content.FightingDojoEnabled || content.FightingDojoOwnership is not { } owned ||
            content.FightingDojoOpponents is not { } opponents || content.FightingDojoMachines is not { } machines ||
            content.FightingDojoTrainers is not { } trainers ||
            !ReferenceEquals(FightingDojoOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(owned.Predecessor, content.PowerPlantOwnership) ||
            !ReferenceEquals(owned, content.FossilLabOwnership?.Predecessor) ||
            !ReferenceEquals(FightingDojoOpponentPokemonCatalog.For(owned), opponents) ||
            !ReferenceEquals(opponents.Predecessor, content.PowerPlantOpponents) ||
            !ReferenceEquals(FightingDojoMachineCatalog.For(machines.Catalog), machines) ||
            !ReferenceEquals(machines.Predecessor, content.PowerPlantMachines) || !ReferenceEquals(machines.Pokemon, owned))
            throw new InvalidDataException("fighting_dojo_current_binding");
        FightingDojoTrainerPartyFactory.ValidateCatalog(trainers);
    }

    private string? ValidateFightingDojoGift(CampaignState state)
    {
        bool got = state.Script.Flags.GetValueOrDefault(FightingDojoGiftFlag);
        bool lee = state.Script.Flags.GetValueOrDefault(DojoBallFlag(106)), chan = state.Script.Flags.GetValueOrDefault(DojoBallFlag(107));
        if (!_content.FightingDojoEnabled) return state.DojoGift is not null || got || lee || chan ? "save_dojo_profile_missing" : null;
        if ((state.DojoGift is not null) != got || lee && chan || (lee || chan) != got) return "save_dojo_gift_flags";
        if (state.DojoGift is not { } gift) return null;
        if (gift.SourceSpeciesId is not (106 or 107) || gift.InstanceId < 2 || gift.InstanceId >= state.NextPokemonId ||
            gift.Ivs is null || lee != (gift.SourceSpeciesId == 106)) return "save_dojo_gift_origin";
        var matching = HistoricalPokemon(state).Where(p => p.InstanceId == gift.InstanceId).ToArray();
        if (matching.Length != 1) return "save_dojo_gift_instance";
        var mon = matching[0];
        if (mon.Pokemon.SourceSpeciesId != gift.SourceSpeciesId || mon.Pokemon.Personality != gift.Personality || mon.Pokemon.Ivs != gift.Ivs ||
            mon.Pokemon.OriginalTrainerId != state.TrainerId || mon.OriginalTrainerName != state.PlayerName ||
            mon.OriginalTrainerGender != state.PlayerGender || mon.Language != 2 || mon.MetGame != 4 ||
            mon.MetLocation != "MAPSEC_SAFFRON_CITY" || mon.MetLevel != 25 || mon.Ball != "ITEM_POKE_BALL" || mon.ModernFatefulEncounter ||
            !state.CaughtSpecies.Contains(gift.SourceSpeciesId) || !state.SeenSpecies.Contains(gift.SourceSpeciesId) ||
            state.Exploration?.Captured is not { } captures || captures.IsDefault || captures.Any(c => c is null || c.InstanceId == gift.InstanceId) ||
            state.MagikarpGift?.InstanceId == gift.InstanceId || state.EeveeGift?.InstanceId == gift.InstanceId || state.LaprasGift?.InstanceId == gift.InstanceId)
            return "save_dojo_gift_instance";
        if (state.World.Player.MapId.Value == FightingDojoMap &&
            state.Actors.Values.Any(a => a.SourceLocalId == (gift.SourceSpeciesId == 106 ? 6 : 7) && a.Active)) return "save_dojo_gift_actor";
        // The side coordinate events set scene=1 before battle. Neither scene nor
        // a victory receipt is a prerequisite in the original ball scripts.
        return null;
    }

    private bool TryApplyFightingDojoEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.FightingDojoEnabled || effect.Source.Path != FightingDojoSource || effect.Opcode is not ("showmonpic" or "givemon")) return false;
        int species = DojoGiftCallerSpecies(state) ?? throw new InvalidDataException("dojo_gift_caller");
        if (state.DojoGift is not null || state.Script.Flags.GetValueOrDefault(FightingDojoGiftFlag) ||
            state.Script.Flags.GetValueOrDefault(DojoBallFlag(106)) || state.Script.Flags.GetValueOrDefault(DojoBallFlag(107)))
            throw new InvalidDataException("dojo_gift_already_received");
        if (effect.Opcode == "showmonpic")
        {
            string symbol = species == 106 ? "SPECIES_HITMONLEE" : "SPECIES_HITMONCHAN";
            if (effect.Source.Symbol != DojoBallScript(species) || !effect.Args.SequenceEqual(new[] { symbol, "10", "3" }))
                throw new InvalidDataException("dojo_picture_source");
            RequireSpecies(species);
            next = state with { MonPicture = new(species, 10, 3) };
            return true;
        }
        if (effect.Source.Symbol != "SaffronCity_Dojo_EventScript_GiveHitmon" ||
            !effect.Args.SequenceEqual(new[] { species.ToString(System.Globalization.CultureInfo.InvariantCulture), "25" }) ||
            _content.FindProgram(effect.Source.Symbol)?.Instructions.SingleOrDefault(i => i.Opcode == "givemon") is not { } sourceGive ||
            !sourceGive.Args.SequenceEqual(new[] { "VAR_TEMP_1", "25" }) || state.Script.Variables.GetValueOrDefault("VAR_TEMP_1") != species)
            throw new InvalidDataException("dojo_gift_source");
        var received = GiveScriptGift(state, species, 25, "MAPSEC_SAFFRON_CITY");
        next = received.State;
        if (received.Gift is { } mon) next = next with { DojoGift = new(species, mon.InstanceId, mon.Pokemon.Personality, mon.Pokemon.Ivs) };
        return true;
    }
}
