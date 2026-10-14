using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Exact Victory Road Ray/Tyra admission over the existing shared four-slot owner.</summary>
public static class VictoryRoadDoubleTrainerEncounter
{
    public const string Version = "firered-victory-road-double-encounter-v1";
    public static TrainerId TrainerId { get; } = new("pret:TRAINER_COOL_COUPLE_RAY_TYRA");
    public const int SharedTrainerFlag = 0x500 + 485;
    public const int VictoryMoney = 2160;
    public static SourceTwinsEncounterResult Begin(VictoryRoadTrainerCatalog trainers, CinnabarOpponentPokemonCatalog opponents, string battleScript,
        ImmutableArray<WildBattleCalledPartyMember> playerParty, RngState rng,
        bool attackBadgeBoost = false, bool defenseBadgeBoost = false, bool speedBadgeBoost = false, bool specialBadgeBoost = false, WildBattleTerrain terrain = WildBattleTerrain.Cave, OwnedBattlePokemonCatalog? successorOwned = null)
    {
        SourceTwinsEncounterResult Fail(string code) => new(null, rng, [], [], code);
        if (opponents is null || !ReferenceEquals(CinnabarOpponentPokemonCatalog.For(opponents.Catalog), opponents))
            return Fail("victory_road_double_catalog");
        try { VictoryRoadTrainerPartyFactory.ValidateCatalog(trainers); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException) { return Fail("victory_road_double_source"); }
        if (!trainers.BattlesByScript.TryGetValue(battleScript, out var battle) || battle.Trainer != TrainerId || battle.Mode != 4)
            return Fail("victory_road_double_script");
        OwnedBattlePokemonCatalog predecessorOwned = opponents.Owned;
        if (successorOwned is not null && !OwnedBattlePokemonCatalog.IsCampaignSuccessorOf(predecessorOwned, successorOwned))
            return Fail("source_double_successor_owned");
        var owned = successorOwned ?? predecessorOwned;
        var proof = opponents.Mechanics; var catalog = proof.Catalog;
        if (!Enum.IsDefined(terrain)) return Fail("victory_road_double_terrain");
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) return Fail("victory_road_double_rng");
        if (playerParty.IsDefault || playerParty.Length is < 2 or > 6) return Fail("victory_road_double_player_party");
        foreach (var member in playerParty)
            if (member?.Pokemon is null || !owned.Catalog.Species.ContainsKey(member.Pokemon.Species) || SourcePokemonValidator.ValidatePartyMember(catalog, member, owned) is not null ||
                member.KnockedOffItem is not null || member.PersistentHeldItemOverride is not null ||
                !SourceFrameBerryItems.SupportsHeldItem(member.HeldItem, catalog) ||
                member.Status is not (WildBattleStatus.None or WildBattleStatus.Poison or WildBattleStatus.Burn or WildBattleStatus.Freeze or WildBattleStatus.Paralysis) &&
                !WildBattleStatus.IsSleeping(member.Status) && !WildBattleSourceFieldEffects.IsValidToxic(member.Status))
                return Fail("victory_road_double_player_member");
        var active = playerParty.Select((m, i) => (m, i)).Where(x => !x.m.IsEgg && x.m.Pokemon.CurrentHp > 0).Take(2).Select(x => x.i).ToArray();
        if (active.Length != 2) return Fail("victory_road_double_two_usable_pokemon_required");
        try
        {
            // SetUpBattleVars runs while flags are TRAINER only: four setup draws.
            // CreateNPCTrainerParty adds DOUBLE after generation, so there is no initial flank draw.
            var setupTrace = ImmutableArray.CreateBuilder<RandomTrace>(); var generationRng = rng;
            for (int slot = 0; slot < 4; slot++) {
                var draw = Gen3Rng.Next(generationRng, $"victory-road-double.entry.ai-setup.slot-{slot}", 16);
                generationRng = draw.State; setupTrace.Add(draw.Trace);
            }
            var generated = VictoryRoadTrainerPartyFactory.Create(trainers, opponents, TrainerId, generationRng);
            var enemy = generated.Party.Select(m => new WildBattleCalledPartyMember(m.Pokemon, m.HeldItem)).ToImmutableArray();
            var frame = new SourceBattleFrame {
                Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
                SourceMechanicsRulesVersion = SourceBattleKernel.Version, SourceCampaignRulesVersion = successorOwned is null ? CinnabarOpponentPokemonCatalog.Profile : successorOwned.TrainerOpponentProfile, BattleKind = WildBattleCalledBattleKind.Trainer,
                Rng = generated.Rng, Field = new() { Terrain = terrain }, Parties = [new(BattleSide.Player, playerParty), new(BattleSide.Opponent, enemy)],
                Slots = Enumerable.Range(0, 4).Select(i => {
                    var side = (BattleSide)(i & 1); int index = side == BattleSide.Player ? active[i / 2] : i / 2;
                    var p = side == BattleSide.Player ? playerParty[index] : enemy[index];
                    return new SourceBattleSlot(new(i), new(side, index), 1, new WildBattleCombatant(p.Pokemon,
                        HeldItem: p.HeldItem, Status: p.Status, AttackBadgeBoost: side == BattleSide.Player && attackBadgeBoost) {
                        FirstTurnCounter = 2, DefenseBadgeBoost = side == BattleSide.Player && defenseBadgeBoost,
                        SpeedBadgeBoost = side == BattleSide.Player && speedBadgeBoost,
                        SpecialBadgeBoost = side == BattleSide.Player && specialBadgeBoost });
                }).ToImmutableArray()
            };
            var entry = SourceFourEntryAbilities.InitializeEntry(catalog, frame, frame.Revision);
            if (!entry.Succeeded) return Fail(entry.Fault!);
            var turnNumber = Gen3Rng.Next(entry.State.Rng, "victory-road-double.entry.random-turn-number");
            var ready = SourceBattleEventHooks.Publish(entry.State with { Rng = turnNumber.State, RandomTurnNumber = turnNumber.Value },
                (n, t) => new SourceTurnReadyEvent(n, t, turnNumber.Value));
            return new(ready.State, ready.State.Rng, [.. entry.Events, ready.Event],
                [.. setupTrace, .. generated.Trace, .. entry.Trace, turnNumber.Trace], null);
        }
        catch (OverflowException) { return Fail("victory_road_double_rng_or_counter_exhausted"); }
        catch (ArgumentException) { return Fail("victory_road_double_invalid_generation"); }
        catch (NotSupportedException) { return Fail("victory_road_double_generation_not_supported"); }
    }

    public static SourceTwinsAiResult ChooseEnemyActions(VictoryRoadTrainerCatalog trainers,
        CinnabarOpponentPokemonCatalog opponents, SourceBattleFrame frame, long expectedRevision,
        string? knownPlayerAbility = null)
    {
        if (ValidateFrame(trainers, opponents, frame) is { } error) return new(frame, [], [], error);
        var result = SourceFourTrainerAi.ChooseMoves(opponents.Mechanics.Catalog, trainers.Trainers[TrainerId], frame,
            expectedRevision, knownPlayerAbility);
        return new(result.State,result.Choices.Select(c => new SourceTwinsEnemyChoice(c.Actor,c.ScoredTarget,
            c.Selection,c.Scores,c.SimulatedDamagePercent,c.AbilityReads)).ToImmutableArray(),result.Trace,result.Fault);
    }

    public static string? ValidateFrame(VictoryRoadTrainerCatalog trainers, CinnabarOpponentPokemonCatalog opponents, SourceBattleFrame frame)
    {
        if (opponents is null || !ReferenceEquals(CinnabarOpponentPokemonCatalog.For(opponents.Catalog), opponents))
            return "victory_road_double_catalog";
        try { VictoryRoadTrainerPartyFactory.ValidateCatalog(trainers); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException) { return "victory_road_double_source"; }
        if (frame is null ||
            SourceFourActionTurns.ValidateCapability(opponents.Mechanics.Catalog, frame) is { }) return "victory_road_double_frame";
        if (frame.SourceCampaignRulesVersion != CinnabarOpponentPokemonCatalog.Profile &&
            (OwnedBattlePokemonCatalog.ForMechanics(opponents.Mechanics.Catalog, frame.SourceCampaignRulesVersion) is not { } current ||
             frame.SourceCampaignRulesVersion != current.TrainerOpponentProfile ||
             !OwnedBattlePokemonCatalog.IsCampaignSuccessorOf(opponents.Owned, current))) return "source_double_successor_owned";
        if (SourcePokemonValidator.ValidateFrameParties(opponents.Mechanics.Catalog, frame) is not null) return "victory_road_double_pokemon";
        var members = frame.Party(BattleSide.Opponent).Members;
        var expected = VictoryRoadTrainerPartyFactory.Expected[TrainerId];
        if (members.Length != expected.Party.Length) return "victory_road_double_roster";
        uint hash = 0;
        for (int i = 0; i < members.Length; i++)
        {
            var mon = members[i].Pokemon; var source = expected.Party[i];
            foreach (int value in expected.Encoded.Concat(source.Encoded)) hash = unchecked(hash + (uint)value);
            uint personality = unchecked(0x80u + (hash << 8));
            int iv = source.Iv * 31 / 255;
            if (members[i].IsEgg || mon.Species.Value != source.Species || mon.SourceSpeciesId != source.SourceSpecies ||
                mon.Level != source.Level || mon.Personality != personality || mon.IsShiny || mon.Evs != SixStats.Zero ||
                mon.Ivs != new SixStats(iv,iv,iv,iv,iv,iv) || mon.Moves.Any(m => m.PpUps != 0) ||
                !mon.Moves.Select(m => m.Move.Value).SequenceEqual(source.Moves)) return "victory_road_double_roster";
        }
        return null;
    }
}
