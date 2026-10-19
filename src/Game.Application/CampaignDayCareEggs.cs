using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;

namespace Game.Application;

// Only immutable factory inputs and the acquired identity are retained. No world, party,
// capture history, battle history or redundant full parent snapshots enter an egg receipt.
public sealed record CampaignEggParent(long InstanceId, int SourceSpeciesId, uint Personality,
    uint TrainerId, SixStats Ivs, ImmutableArray<MoveId> Moves, string HeldItem);
public sealed record CampaignDayCareEggReceipt(long InstanceId, CampaignEggParent First,
    CampaignEggParent Second, ushort PendingPersonality, int SourceSpeciesId, uint Personality,
    SixStats Ivs, string ReceivedLocation, RngState RngBefore, RngState RngAfter)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CampaignDayCareHatchReceipt? Hatch { get; init; }
}
public sealed record CampaignDayCareHatchReceipt(string Location, RngState RngBefore, RngState RngAfter);
public enum CampaignEggHatchStage { Animation, Hatched, NicknameChoice, Naming }
public sealed record CampaignEggHatch(int PartyIndex, long InstanceId, int SourceSpeciesId,
    CampaignEggHatchStage Stage, ImmutableArray<int> ShardAnimationIndexes);

public sealed partial class CampaignSession
{
    private const string HatchedEggsStat = "GAME_STAT_HATCHED_EGGS";

    private SourceEggTrainer EggTrainer(CampaignState state) => new(state.TrainerId, state.PlayerName, state.PlayerGender);

    private CampaignState ApplyDayCareWalkingStep(CampaignState state)
    {
        state = ApplyRoute5DayCareWalkingStep(state);
        if (state.Script.QuestLogState != 0 || state.FieldPoison is not null || !state.Script.Completed || state.Script.Locked ||
            state.Script.Wait is not null || state.DayCareInteraction is not null || state.EggHatch is not null)
            throw new InvalidDataException("daycare_step_context");
        // The shared u8 counter runs even with no residents and no eggs, as ShouldEggHatch does.
        var step = FourIslandDayCareRules.AdvanceEligibleStep(FourDayCare(state),
            _binding.BreedingSpecies, state.World.Rng);
        state = state with { FourIslandDayCare = step.State, World = state.World with { Rng = step.Rng },
            Script = step.EggProduced ? state.Script with { Flags = state.Script.Flags.SetItem(PendingDayCareEggFlag, true) } : state.Script };
        if (!step.HatchCycleDue) return state;
        var tick = SourceEggRules.AdvanceHatchCycle(_content.PokemonContent!, state.Party);
        state = state with { Party = tick.Party };
        if (tick.HatchPartyIndex is not {} index) return state;
        state = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", index) } };
        return Pump(StartStory(state, "EventScript_EggHatch"));
    }

    private static CampaignEggParent EggParent(PartyPokemon parent) => new(parent.InstanceId, parent.Pokemon.SourceSpeciesId,
        parent.Pokemon.Personality, parent.Pokemon.OriginalTrainerId, parent.Pokemon.Ivs,
        parent.Pokemon.Moves.Select(m => m.Move).ToImmutableArray(), parent.HeldItem);

    private CampaignState ReceiveDayCareEgg(CampaignState state)
    {
        var daycare = FourDayCare(state);
        if (!daycare.HasPendingEgg || !state.Script.Flags.GetValueOrDefault(PendingDayCareEggFlag) || state.Party.Length >= 6 ||
            state.NextPokemonId is < 2 or long.MaxValue) throw new InvalidDataException("daycare_receipt_context");
        string location = _content.MapDefinition(state.World.Player.MapId).RegionMapSection;
        var made = SourceEggRules.Receive(_content.PokemonContent!, BreedingMetadata, daycare, state.NextPokemonId, EggTrainer(state), location, 4, state.World.Rng);
        var receipt = new CampaignDayCareEggReceipt(made.Egg.InstanceId, EggParent(daycare.First!.Pokemon), EggParent(daycare.Second!.Pokemon),
            daycare.OffspringPersonality, made.Egg.Pokemon.SourceSpeciesId, made.Egg.Pokemon.Personality, made.Egg.Pokemon.Ivs, location, state.World.Rng, made.Rng);
        return state with { Party = state.Party.Add(made.Egg), FourIslandDayCare = FourIslandDayCareRules.ClearPendingEgg(daycare),
            DayCareEggs = (state.DayCareEggs ?? []).Add(receipt), NextPokemonId = checked(state.NextPokemonId + 1), World = state.World with { Rng = made.Rng } };
    }

    private PartyPokemon RebuildEggParent(CampaignEggParent parent, CampaignState state)
    {
        if (parent is null || parent.InstanceId < 1 || parent.Ivs is null || parent.Moves.IsDefaultOrEmpty || parent.Moves.Length > 4 || parent.Moves.Distinct().Count() != parent.Moves.Length)
            throw new InvalidDataException("save_daycare_parent_receipt");
        var content = _content.PokemonContent!;
        var species = content.Catalog.Species.Values.Single(p => p.SourceSpeciesId == parent.SourceSpeciesId);
        var made = PokemonFactory.Create(content.Catalog, new(species.Id, 5, parent.TrainerId, IvPolicy.Fixed(0), PersonalityPolicy.Fixed(parent.Personality)), new RngState(0)).Pokemon;
        var b = species.BaseStats; var iv = parent.Ivs;
        int Other(int value, int gene, Stat stat) => StatRules.CalculateOther(value, gene, 0, 5, made.Nature, stat);
        // Receipt-only reconstruction uses the same shared stat primitives; this never mutates an owned parent.
        var stats = new BattleStats(StatRules.CalculateHp(b.Hp, iv.Hp, 0, 5), Other(b.Attack, iv.Attack, Stat.Attack),
            Other(b.Defense, iv.Defense, Stat.Defense), Other(b.SpecialAttack, iv.SpecialAttack, Stat.SpecialAttack),
            Other(b.SpecialDefense, iv.SpecialDefense, Stat.SpecialDefense), Other(b.Speed, iv.Speed, Stat.Speed));
        made = made with { Ivs = parent.Ivs, Stats = stats, CurrentHp = stats.MaxHp,
            Moves = parent.Moves.Select(m => new PokemonMoveSlot(m, content.Catalog.Moves[m].Pp, content.Catalog.Moves[m].Pp)).ToImmutableArray() };
        return new(parent.InstanceId, made, null, state.PlayerName, state.PlayerGender,
            2, "MAPSEC_FOUR_ISLAND", 0, 4, "ITEM_POKE_BALL", parent.HeldItem);
    }

    private string? ValidateDayCareEggReceipts(CampaignState state)
    {
        var receipts = state.DayCareEggs ?? [];
        if (receipts.IsDefault || receipts.Any(e => e is null)) return "save_daycare_egg_receipts";
        var ownedPokemon = OwnedPokemon(state);
        if (ownedPokemon.Any(p => p.IsEgg && !EggAcquisitionIds(state).Contains(p.InstanceId))) return "save_daycare_egg_acquisition_missing";
        var ids = new HashSet<long>(); long lastId = 1; ulong lastCursor = 0;
        foreach (var receipt in receipts)
        {
            if (receipt.InstanceId <= lastId || receipt.InstanceId >= state.NextPokemonId || !ids.Add(receipt.InstanceId) ||
                receipt.First is null || receipt.Second is null || receipt.First.InstanceId == receipt.Second.InstanceId ||
                receipt.First.InstanceId >= receipt.InstanceId || receipt.Second.InstanceId >= receipt.InstanceId || receipt.PendingPersonality == 0 ||
                receipt.ReceivedLocation != "MAPSEC_FOUR_ISLAND" || receipt.RngBefore.Cursor < lastCursor || receipt.RngAfter.Cursor > state.World.Rng.Cursor)
                return "save_daycare_egg_origin";
            var owned = ownedPokemon.SingleOrDefault(p => p.InstanceId == receipt.InstanceId);
            if (owned is null || owned.Pokemon.Personality != receipt.Personality || owned.Pokemon.Ivs != receipt.Ivs || owned.MetLevel != 0 ||
                owned.Pokemon.OriginalTrainerId != state.TrainerId || owned.Ball != "ITEM_POKE_BALL" || owned.MetGame != 4 ||
                owned.ModernFatefulEncounter || !EvolutionSpeciesMatchesOrigin(state, owned, receipt.SourceSpeciesId)) return "save_daycare_egg_identity";
            try
            {
                var parents = new FourIslandDayCareState(new(RebuildEggParent(receipt.First, state), 0), new(RebuildEggParent(receipt.Second, state), 0), receipt.PendingPersonality, 0);
                var made = SourceEggRules.Receive(_content.PokemonContent!, BreedingMetadata, parents, receipt.InstanceId, EggTrainer(state), receipt.ReceivedLocation, 4, receipt.RngBefore);
                if (made.Rng != receipt.RngAfter || made.Egg.Pokemon.SourceSpeciesId != receipt.SourceSpeciesId || made.Egg.Pokemon.Personality != receipt.Personality || made.Egg.Pokemon.Ivs != receipt.Ivs)
                    return "save_daycare_egg_creation";
                if (receipt.Hatch is null)
                {
                    if (!owned.IsEgg || owned.MetLocation != receipt.ReceivedLocation || (owned.Pokemon.EggStatCalculationIvs != made.Egg.Pokemon.EggStatCalculationIvs && owned.Pokemon.EggStatCalculationIvs != owned.Pokemon.Ivs) ||
                        !owned.Pokemon.Moves.SequenceEqual(made.Egg.Pokemon.Moves)) return "save_daycare_unhatched_identity";
                }
                else
                {
                    var hatch = receipt.Hatch;
                    if (owned.IsEgg || owned.Language != 2 || owned.MetLocation != hatch.Location || hatch.RngBefore.Cursor < receipt.RngAfter.Cursor ||
                        hatch.RngAfter.Cursor > state.World.Rng.Cursor || !_content.Maps.Keys.Any(m => _content.MapDefinition(m).RegionMapSection == hatch.Location)) return "save_daycare_hatch_identity";
                    var hatched = SourceEggRules.Hatch(_content.PokemonContent!, made.Egg, EggTrainer(state), hatch.Location, hatch.RngBefore);
                    var shards = SourceEggRules.CreateNormalHatchShards(hatched.Rng);
                    if (shards.Rng != hatch.RngAfter || !state.SeenSpecies.Contains(receipt.SourceSpeciesId) || !state.CaughtSpecies.Contains(receipt.SourceSpeciesId)) return "save_daycare_hatch_rng_or_dex";
                }
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or InvalidDataException or KeyNotFoundException or NotSupportedException or OverflowException)
            { return "save_daycare_egg_creation"; }
            lastId = receipt.InstanceId; lastCursor = receipt.RngAfter.Cursor;
        }
        return null;
    }

    private bool TryApplyEggHatchEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Source.Path != DayCarePath || effect.Source.Symbol != "EventScript_EggHatch" ||
            effect.Opcode is not ("special" or "waitstate")) return false;
        var instruction = DayCareInstruction(state);
        if (instruction is null || instruction.Source != effect.Source || instruction.Opcode != effect.Opcode || !instruction.Args.SequenceEqual(effect.Args) ||
            !state.Script.Locked || state.Script.QuestLogState != 0) throw new InvalidDataException("hatch_source_context");
        if (effect.Opcode == "waitstate")
        {
            if (!effect.Args.IsEmpty || state.EggHatch?.Stage != CampaignEggHatchStage.Animation || state.Script.Wait?.Kind != StoryWaitKind.Presentation)
                throw new InvalidDataException("hatch_wait_context");
            next = state with { Dialogue = null, PresentationTicks = 0 }; return true;
        }
        if (!effect.Args.SequenceEqual(new[] { "EggHatch" }) || state.EggHatch is not null) throw new InvalidDataException("hatch_special_context");
        int index = state.Script.Variables.GetValueOrDefault("VAR_0x8004", -1);
        if (index < 0 || index >= state.Party.Length || !state.Party[index].IsEgg || state.Party[index].Pokemon.Friendship != 0) throw new InvalidDataException("hatch_selected_egg");
        var egg = state.Party[index];
        if (EggHatchReceipt(state, egg.InstanceId) is not null) throw new InvalidDataException("hatch_receipt_already_hatched");
        string location = _content.MapDefinition(state.World.Player.MapId).RegionMapSection;
        var hatched = SourceEggRules.Hatch(_content.PokemonContent!, egg, EggTrainer(state), location, state.World.Rng);
        var shards = SourceEggRules.CreateNormalHatchShards(hatched.Rng);
        var stats = state.GameStats;
        if (stats?.TryGetValue(HatchedEggsStat, out int hatchCount) != true)
            throw new InvalidDataException("hatch_stat_missing_migration_required");
        next = WriteEggHatchReceipt(state, egg.InstanceId, new(location, state.World.Rng, hatched.Rng)) with {
            GameStats = stats.SetItem(HatchedEggsStat, Math.Min(0xffffff, hatchCount + 1)),
            Party = state.Party.SetItem(index, hatched.Pokemon), World = state.World with { Rng = hatched.Rng },
            SeenSpecies = state.SeenSpecies.Add(egg.Pokemon.SourceSpeciesId),
            CaughtSpecies = state.CaughtSpecies.Add(egg.Pokemon.SourceSpeciesId), TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", PokemonName(hatched.Pokemon)),
            EggHatch = new(index, egg.InstanceId, egg.Pokemon.SourceSpeciesId, CampaignEggHatchStage.Animation, shards.AnimationIndexes),
            Dialogue = null, ScreenIsBlack = false, PresentationTicks = 0, PresentationEffects = [] };
        return true;
    }

    private bool TryDecideEggHatch(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        decision = No("hatch_not_pending");
        if (state.EggHatch is not {} scene) return false;
        if (state.Script.Wait is not {} wait || !state.Script.Locked || state.Party[scene.PartyIndex].InstanceId != scene.InstanceId)
        { decision = No("hatch_callback_context"); return true; }
        if (scene.Stage == CampaignEggHatchStage.Naming && command.Kind == "nickname") return false;
        if (command.Kind != "ack") { decision = No("hatch_ack_required"); return true; }
        CampaignState Text(string symbol, CampaignEggHatchStage stage, bool choice = false) => state with {
            EggHatch = scene with { Stage = stage }, Dialogue = new(symbol, _content.TextPages(symbol), 0, choice, false),
            Script = state.Script with { Wait = wait with { Kind = choice ? StoryWaitKind.Choice : StoryWaitKind.Dialog } } };
        CampaignState next;
        switch (scene.Stage)
        {
            case CampaignEggHatchStage.Animation:
                var shards = SourceEggRules.CreateNormalHatchShards(state.World.Rng);
                if (!shards.AnimationIndexes.SequenceEqual(scene.ShardAnimationIndexes)) throw new InvalidDataException("hatch_shard_projection_changed");
                var hatch = EggHatchReceipt(state, scene.InstanceId) ?? throw new InvalidDataException("hatch_receipt_missing");
                next = WriteEggHatchReceipt(Text("gText_HatchedFromEgg", CampaignEggHatchStage.Hatched), scene.InstanceId,
                    hatch with { RngAfter = shards.Rng }) with { World = state.World with { Rng = shards.Rng } }; break;
            case CampaignEggHatchStage.Hatched:
                next = Text("gText_NickHatchPrompt", CampaignEggHatchStage.NicknameChoice, true); break;
            case CampaignEggHatchStage.NicknameChoice:
                if (command.Choice is not (0 or 1)) { decision = No("choice_required"); return true; }
                if (command.Choice == 0) { next = CompleteEggHatch(state); break; }
                var mon = state.Party[scene.PartyIndex];
                next = state with { EggHatch = scene with { Stage = CampaignEggHatchStage.Naming }, Nickname = new(scene.PartyIndex, mon.CustomNickname, false),
                    Dialogue = null, TextBuffers = state.TextBuffers.SetItem("STR_VAR_2", PokemonName(mon)).SetItem("STR_VAR_3", PokemonName(mon)),
                    Script = state.Script with { Wait = wait with { Kind = StoryWaitKind.Nickname } } }; break;
            case CampaignEggHatchStage.Naming:
                if (state.Nickname?.Submitted != true) { decision = No("nickname_submission_required"); return true; }
                next = CompleteEggHatch(state); break;
            default: throw new InvalidDataException("hatch_stage_invalid");
        }
        decision = new(CommandStatus.Accepted, next); return true;
    }

    private CampaignState CompleteEggHatch(CampaignState state)
    {
        var wait = state.Script.Wait!;
        state = state with { EggHatch = null, Nickname = null, Dialogue = null, ScreenIsBlack = false, PresentationTicks = 0, PresentationEffects = [], Motions = [],
            Script = state.Script with { Wait = wait with { Kind = StoryWaitKind.Presentation } } };
        return Pump(Advance(state, StoryRules.Continue(state.Script, wait.CorrelationId, _content)));
    }
}
