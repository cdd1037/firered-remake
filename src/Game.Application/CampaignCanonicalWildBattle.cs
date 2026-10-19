using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Encounters;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Application;

/// <summary>Read-only compatibility view for the shared singles renderer. No command consumes it.</summary>
public static class CampaignWildReadProjection
{
    public static WildBattleState From(SourceBattleFrame frame)
    {
        if(frame.Layout!=SourceBattleLayout.Singles)throw new InvalidDataException("wild_read_projection_layout");
        var player=frame.Get(new(0));var opponent=frame.Get(new(1));var encounter=frame.WildEncounter;
        var pending=frame.PendingSwitches.FirstOrDefault(p=>p.Slot.Value==0);
        return new(SourceBattleReadProjection.Combatant(frame,new(0)),SourceBattleReadProjection.Combatant(frame,new(1)),
            frame.Turn,frame.Revision,frame.EventSequence,frame.Rng,frame.RandomTurnNumber,frame.Outcome,frame.RunAttempts,frame.RulesVersion,
            pending is not null&&pending.Kind!=WildBattleCalledSwitchKind.BatonPass,player.Registers.ChosenMoveSlot,opponent.Registers.ChosenMoveSlot,
            false,player.Registers.LastMoveSlot,opponent.Registers.LastMoveSlot)
        {
            Campaign=frame.Campaign,Obedience=frame.Obedience,SourceMechanicsRulesVersion=frame.SourceMechanicsRulesVersion,SourceCampaignRulesVersion=frame.SourceCampaignRulesVersion,
            PermanentWeather=frame.Field.PermanentWeather,SourceMoneyMultiplier=frame.Field.MoneyMultiplier,Terrain=frame.Field.Terrain,
            PayDayMoney=frame.Field.PayDayMoney,RainTurnsRemaining=frame.Field.RainTurnsRemaining,SandstormTurnsRemaining=frame.Field.SandstormTurnsRemaining,
            ScriptedSnorlax=encounter?.ScriptedSnorlax??false,TowerBattleKind=encounter?.TowerBattleKind,
            PlayerReflectTurnsRemaining=frame.Sides[0].ReflectTurnsRemaining,OpponentReflectTurnsRemaining=frame.Sides[1].ReflectTurnsRemaining,
            CalledParty=new(frame.Party(BattleSide.Player).Members,frame.Party(BattleSide.Opponent).Members,player.Member!.Value.Index,opponent.Member!.Value.Index,frame.BattleKind)
            {
                PlayerSpikes=frame.Sides[0].Spikes,OpponentSpikes=frame.Sides[1].Spikes,
                PlayerUsedHeldItem=player.Position.UsedHeldItem,OpponentUsedHeldItem=opponent.Position.UsedHeldItem,
                PendingSwitch=pending is null?null:new(WildBattleSide.Player,pending.Kind,pending.LegalPartyIndices,null)
            },
            CalledField=new(){SunTurnsRemaining=frame.Field.SunTurnsRemaining,HailTurnsRemaining=frame.Field.HailTurnsRemaining,
                PlayerLightScreenTurnsRemaining=frame.Sides[0].LightScreenTurnsRemaining,OpponentLightScreenTurnsRemaining=frame.Sides[1].LightScreenTurnsRemaining,
                PlayerMistTurnsRemaining=frame.Sides[0].MistTurnsRemaining,OpponentMistTurnsRemaining=frame.Sides[1].MistTurnsRemaining,
                PlayerSafeguardTurnsRemaining=frame.Sides[0].SafeguardTurnsRemaining,OpponentSafeguardTurnsRemaining=frame.Sides[1].SafeguardTurnsRemaining},
            SourceWild=encounter is null?null:new(){VictoryRoad=encounter?.VictoryRoad,SeviiRuby=encounter?.SeviiRuby,FourIsland=encounter?.FourIsland,
                SixIsland=encounter?.SixIsland,FiveIsland=encounter?.FiveIsland,CelioRoamer=encounter?.CelioRoamer}
        };
    }
}

public sealed partial class CampaignSession
{
    private PokemonCatalog CanonicalWildPokemon=>SafariOrdinaryOwnership?.Mechanics.Catalog??
        throw new InvalidDataException("canonical_wild_owned_catalog");
    private static SourceTurnContinuation WildContinuation(SourceBattleFrame frame)=>
        new(frame.Revision,frame.EventSequence,frame.NextAction,frame.FourTurn?.Phase??throw new InvalidDataException("canonical_wild_continuation"));
    private SourceBattleFrame PrepareCanonicalWildFrame(CampaignState state,SourceBattleFrame frame)=>frame with
    {
        Campaign=_binding.Ownership,
        Obedience=CampaignObedienceContext(state),
        Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryWild,state.TrainerId,state.PlayerName,
            _content.MapDefinition(state.World.Player.MapId).RegionMapSection),Metadata=state.Party.Select((p,i)=>new SourceRewardMetadata(
                new(BattleSide.Player,i),p.OriginalTrainerName,p.MetLocation,p.Ball,SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}
    };
    private static ImmutableArray<WildBattleEvent> ProjectCanonicalWildEvents(ImmutableArray<SourceBattleEvent> events)=>
        events.Where(e=>e is not (SourceRewardAppliedEvent or SourceRewardLearningEvent or SourceOpponentRewardCompletedEvent))
            .Select(SourceBattleEventAdapter.Export).ToImmutableArray();

    private Decision<CampaignState> CommitCanonicalWildResult(CampaignState state,SourceFourActionResult result,bool resetTrace=false)
    {
        if(!result.Succeeded)return new(CommandStatus.Rejected,state,result.Fault);
        return new(CommandStatus.Accepted,PrepareWildPresentation(ApplyCanonicalWildResult(state,result,resetTrace)));
    }
    private CampaignState ApplyCanonicalWildResult(CampaignState state,SourceFourActionResult result,bool resetTrace=false)
    {
        if(!result.Succeeded)throw new InvalidDataException(result.Fault);
        var b=state.WildBattle??throw new InvalidDataException("canonical_wild_context");
        var pages=ImmutableArray.CreateBuilder<CampaignWildPage>();
        foreach(var e in result.Events)
        {
            if(e is SourceRewardAppliedEvent applied)
            {
                string name=PokemonName(state.Party[applied.Recipient.Index]);
                foreach(var reward in applied.Reward.Events)
                {
                    if(reward is WildRewardExperienceEvent xp)pages.Add(new($"{name} gained\n{xp.Gained} EXP. Points!",reward,applied.Recipient.Index));
                    if(reward is WildRewardLevelEvent level){pages.Add(new($"{name} grew to\nlevel {level.After}!",reward,applied.Recipient.Index));pages.Add(new("",reward,applied.Recipient.Index));}
                    if(reward is WildRewardMoveLearnedEvent learned)pages.Add(new($"{name} learned\n{BattleContent.MoveNames[learned.Move.Value]}!",reward,applied.Recipient.Index));
                }
            }
            else if(e is SourceRewardLearningEvent {Outcome:SourceRewardLearningOutcome.Added or SourceRewardLearningOutcome.Replaced} learned)
                foreach(var text in BattleMessagePages("sText_PkmnLearnedMove",
                    ("B_BUFF1", PokemonName(state.Party[learned.Recipient.Index])), ("B_BUFF2", BattleContent.MoveNames[learned.Candidate.Move.Value])))
                    pages.Add(new(text,null,learned.Recipient.Index));
        }
        var next=b with{DisplayBatch=BeginDisplayBatch(b),Frame=result.State,SourceEvents=result.Events,Events=ProjectCanonicalWildEvents(result.Events),EventIndex=0,
            Phase=CampaignBattlePhase.Events,Pages=pages.ToImmutable(),PageIndex=0,
            LastSwitch=result.Events.OfType<SourceCalledPartySwitchedEvent>().Any(e=>e.Side.Value==0&&e.Kind!=WildBattleCalledSwitchKind.Voluntary)?null:b.LastSwitch,
            ExperienceGained=checked(b.ExperienceGained+result.Events.OfType<SourceRewardAppliedEvent>().Sum(e=>e.Reward.ExperienceCredited)),
            RandomTrace=resetTrace?result.Trace:b.RandomTrace.AddRange(result.Trace)};
        return ProjectSourceWildOwnedParty(state with{WildBattle=next,World=state.World with{Rng=result.State.Rng}});
    }

    private CampaignState ContinueCanonicalWild(CampaignState state)
    {
        var b=state.WildBattle!;var frame=b.Frame??throw new InvalidDataException("canonical_wild_frame_required");
        if(!b.Pages.IsEmpty||frame.Rewards?.Pending is not null)
            return state with{WildBattle=b with{Phase=CampaignBattlePhase.Results}};
        if(!frame.PendingSwitches.IsEmpty)
        {
            var pending=frame.PendingSwitches[0];
            return state with{WildBattle=b with{Phase=CampaignBattlePhase.ChooseMove,
                Replacement=pending.Kind==WildBattleCalledSwitchKind.BatonPass?CampaignReplacementStage.Select:b.Replacement??CampaignReplacementStage.UseNext}};
        }
        if(frame.FourTurn is {Phase:SourceFourActionPhase.RewardsPending})
            return PrepareWildPresentation(ApplyCanonicalWildResult(state,SourceFourRewards.Settle(CanonicalWildPokemon,frame,WildContinuation(frame))));
        if(frame.Outcome!=WildBattleOutcome.Ongoing)
        {
            state=SettleCelioRoamer(state);b=state.WildBattle!;
            if(frame.Outcome==WildBattleOutcome.Caught)return BeginCapturePresentation(state);
            return SettleWildBattle(state);
        }
        if(frame.FourTurn is not null)
            return PrepareWildPresentation(ApplyCanonicalWildResult(state,SourceFourActionTurns.Advance(CanonicalWildPokemon,frame,WildContinuation(frame))));
        var player=b.State.Player;var opponent=b.State.Opponent;
        return state with{WildBattle=b with{Phase=CampaignBattlePhase.ChooseMove,Replacement=null,
            DisplayPlayer=BattleDisplayPokemon(player),DisplayOpponent=BattleDisplayPokemon(opponent),
            DisplayPlayerAppearance=BattleAppearance(player),DisplayOpponentAppearance=BattleAppearance(opponent)}};
    }

    private SourceWildEncounterContext CanonicalWildEncounter(CampaignState state,PokemonInstance pokemon,FirstSeviiEncounterKind kind,
        VictoryRoadWildOrigin? victory,SeviiRubyWildOrigin? ruby,FourIslandWildOrigin? four,SixIslandWildOrigin? six,FiveIslandWildOrigin? five,CeruleanCaveWildOrigin? cave,SevenIslandWildOrigin? seven,
        EventIslandWildOrigin? eventIsland,PowerPlantWildOrigin? powerPlant,CampaignLostelleHypnoReceipt? hypno,PokemonTowerBattleKind? tower,bool snorlax,FishingEncounterOrigin? fishing=null,SeafoamWildOrigin? seafoam=null,SourceTableWildOrigin? sourceTable=null,RockSmashWildOrigin? rockSmash=null,WaterEncounterCandidate? waterCandidate=null)
    {
        var map=state.World.Player.MapId;
        int origins=(sourceTable is null?0:1)+(seafoam is null?0:1)+(victory is null?0:1)+(ruby is null?0:1)+(four is null?0:1)+(six is null?0:1)+(five is null?0:1)+
            (powerPlant is null?0:1)+(eventIsland is null?0:1)+(seven is null?0:1)+(cave is null?0:1)+(hypno is null?0:1)+(tower is null?0:1)+(snorlax?1:0);
        if (rockSmash is not null)
        {
            if (origins != 0 || fishing is not null || _content.FieldRockSmash is not {} rocks || rockSmash.Map != map ||
                !RockSmashWildBattleContract.Matches(rocks, rockSmash, pokemon)) throw new InvalidDataException("canonical_rock_smash_origin");
            return new() { RockSmash = rockSmash };
        }
        if (fishing is not null)
        {
            if (origins != 0 || !IsTypedFishingOrigin(fishing) || fishing.Map != map || !FishingBattleContract.Matches(fishing,pokemon,_content.FishingTables))
                throw new InvalidDataException("canonical_fishing_origin");
            return new() { Fishing = fishing, FishingTables = _content.FishingTables };
        }
        if(sourceTable is not null&&(!IsSourceTableMap(map)||sourceTable.Map!=map||
            sourceTable.Kind!=SourceTableEncounterKind.Scripted&&(int)sourceTable.Kind!=(int)kind||!SourceTableWildBattleContract.Matches(sourceTable,pokemon)))
            throw new InvalidDataException("source_table_wild_origin");
        if(sourceTable is null&&IsSourceTableMap(map)&&!(snorlax&&map.Value=="map.route-16"))throw new InvalidDataException("source_table_wild_missing_origin");
        if(seafoam is not null&&(seafoam.Map!=map||!SeafoamWildBattleContract.Matches(seafoam,pokemon,_content.FishingTables)))throw new InvalidDataException("seafoam_wild_origin");
        if(seafoam is null&&IsSeafoamMap(map))throw new InvalidDataException("seafoam_wild_missing_origin");
        if(powerPlant is not null&&(powerPlant.Map!=map||!PowerPlantWildBattleContract.Matches(powerPlant,pokemon)))throw new InvalidDataException("power_plant_wild_origin");
        if(powerPlant is null&&map.Value==PowerPlantMap)throw new InvalidDataException("power_plant_wild_missing_origin");
        if(eventIsland is not null&&(eventIsland.Map!=map||!EventIslandWildBattleContract.Matches(eventIsland,pokemon)))throw new InvalidDataException("event_island_wild_origin");
        if(origins>1)throw new InvalidDataException("canonical_wild_conflicting_origins");
        bool MatchKind(int value)=>value==(kind switch{FirstSeviiEncounterKind.Land=>0,FirstSeviiEncounterKind.Surf=>1,_=>2});
        if(seven is not null&&(seven.Map!=map||!MatchKind((int)seven.Kind)||
            SevenIslandEncounterTables.IsChamber(map)&&!state.Script.Flags.GetValueOrDefault("FLAG_SYS_UNLOCKED_TANOBY_RUINS")))throw new InvalidDataException("seven_island_wild_field_origin");
        if(seven is null&&SevenIslandMap(map))throw new InvalidDataException("seven_island_wild_missing_field_origin");
        if(cave is not null&&(cave.Map!=map||cave.Kind!=CeruleanCaveEncounterKind.Mewtwo&&!MatchKind((int)cave.Kind)))throw new InvalidDataException("cerulean_cave_wild_field_origin");
        if(cave is null&&CeruleanCaveEncounterTables.Land.Any(t=>t.Map==map))throw new InvalidDataException("cerulean_cave_wild_missing_field_origin");
        if(five is not null&&(five.Map!=map||!MatchKind((int)five.Kind)))throw new InvalidDataException("five_island_wild_field_origin");
        if(six is not null&&(six.Map!=map||!MatchKind((int)six.Kind)))throw new InvalidDataException("six_island_wild_field_origin");
        if(four is not null&&(four.Map!=map||!MatchKind((int)four.Kind)))throw new InvalidDataException("four_island_wild_field_origin");
        if(ruby is not null&&(ruby.Map!=map||!MatchKind((int)ruby.Kind)))throw new InvalidDataException("sevii_ruby_wild_field_origin");
        if(victory is not null&&(victory.Map!=map||!MatchKind((int)victory.Kind)))throw new InvalidDataException("victory_road_wild_field_origin");
        if(five is null&&(FiveIslandEncounterTables.Land.Any(t=>t.Map==map)||FiveIslandEncounterTables.Water.Any(t=>t.Map==map)))throw new InvalidDataException("five_island_wild_missing_field_origin");
        if(six is null&&(SixIslandEncounterTables.Land.Any(t=>t.Map==map)||SixIslandEncounterTables.Water.Any(t=>t.Map==map)))throw new InvalidDataException("six_island_wild_missing_field_origin");
        if(four is null&&(FourIslandEncounterTables.Land.Any(t=>t.Map==map)||FourIslandEncounterTables.Water.Any(t=>t.Map==map)))throw new InvalidDataException("four_island_wild_missing_field_origin");
        if(ruby is null&&(SeviiRubyEncounterTables.Land.Any(t=>t.Map==map)||SeviiRubyEncounterTables.Water.Any(t=>t.Map==map)))throw new InvalidDataException("sevii_ruby_wild_missing_field_origin");
        if(hypno is not null&&!FirstSeviiHypnoBattleContract.SourceInitialInstance(pokemon))throw new InvalidDataException("first_sevii_hypno_source_identity");
        if (origins == 0 && kind == FirstSeviiEncounterKind.Land && OrdinaryLandDefinition(map) is { } ordinary)
            return new() { OrdinaryLand = ordinary.BindBattle(state.Exploration?.EarlyEncounter ??
                throw new InvalidDataException("canonical_ordinary_land_pending")) };
        if (origins == 0 && kind == FirstSeviiEncounterKind.Surf)
        {
            if (waterCandidate is null || waterCandidate.Map != map)
                throw new InvalidDataException("canonical_ordinary_water_pending");
            var binding = WaterEncounterCatalog.BindBattle(_content.WaterEncounters ??
                throw new InvalidDataException("canonical_water_catalog_missing"), _binding.Ownership, waterCandidate);
            if (!binding.Matches(pokemon)) throw new InvalidDataException("canonical_ordinary_water_pokemon");
            return new() { OrdinaryWater = binding };
        }
        return new(){FishingTables=_content.FishingTables,SourceTable=sourceTable,Seafoam=seafoam,PowerPlant=powerPlant,EventIsland=eventIsland,SevenIsland=seven,CeruleanCave=cave,VictoryRoad=victory,SeviiRuby=ruby,FourIsland=four,SixIsland=six,FiveIsland=five,TowerBattleKind=tower,ScriptedSnorlax=snorlax,FirstSeviiWild=hypno is not null||IsFirstSeviiWildMap(map),LostelleHypno=hypno is not null};
    }
}
