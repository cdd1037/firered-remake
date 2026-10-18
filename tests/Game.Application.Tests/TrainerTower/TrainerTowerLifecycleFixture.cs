using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Events;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace FireRed.Tests.Application.TrainerTower;

internal sealed class TrainerTowerLifecycleFixture {
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly PretCampaignCatalog content;
    readonly CampaignSession validator;
    readonly LoadedSave<CampaignState> checkpoint;
    readonly PokemonCatalog pokemon;
    int sequence;
    public TrainerTowerLifecycleFixture() {
        Check(Environment.Version.Major == 10, "Actual hosted runtime major 10");
        content = CurrentCampaignFixture.Catalog;
        validator = CampaignSession.CreateCurrent(content);
        pokemon = content.PokemonContent!.Pokemon.Mechanics.Catalog;
        // Shared CurrentCampaignFixture owns the explicit legacy conversion and
        // TEST-only synthetic hatch-total premise; this fixture never infers it.
        checkpoint = CurrentCampaignFixture.Checkpoint;
        _ = Strict(checkpoint.State, checkpoint.Generation, "controlled-current-input");
    }
    static void Check(bool yes, string message) => Assert.IsTrue(yes, message);
    static string Hash<T>(T value) => CanonicalJson.Hash(value);
    static CampaignState Call(CampaignSession session, string method, params object?[] values) {
        try {
            var target = typeof(CampaignSession).GetMethods(Private).Single(m => m.Name == method &&
                m.GetParameters().Length >= values.Length && m.GetParameters().Skip(values.Length).All(p => p.IsOptional));
            return (CampaignState)target.Invoke(session, values.Concat(target.GetParameters().Skip(values.Length)
                .Select(p => p.DefaultValue)).ToArray())!;
        } catch (TargetInvocationException e) { throw e.InnerException!; }
    }
    CampaignSession Strict(CampaignState state, long revision, string label) {
        var error = validator.ValidatePlayerSave(state);
        Check(error is null, label + " strict save rejected: " + error);
        var bytes = SaveCodec.Encode(state, revision, validator.RulesVersion, content.ContentVersion);
        var loaded = SaveCodec.Decode<CampaignState>(bytes, validator.RulesVersion, content.ContentVersion,
            validate: validator.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, loaded.State, loaded.Generation);
        Check(Hash(restored.State) == Hash(state), label + " whole-state roundtrip changed payload");
        Check(restored.Revision == revision, label + " generation changed");
        return restored;
    }
    CampaignSession Place(CampaignState state, long revision, string mapName, int x, int y) {
        var sourceError=validator.ValidatePlayerSave(state);
        Check(sourceError is null, "Placement requires a valid stable source state: " + sourceError);
        var session = CampaignSession.CreateCurrent(content);
        var map = new MapId(mapName);
        var tile = content.Maps[map].GetTile(x,y)!;
        state = state with { World = state.World with { Player = new(map,x,y,tile.Elevation,Direction.North) },
            Dialogue = null, Motions = [], PresentationEffects = [], PresentationTicks = 0, EntryPrograms = [],
            Script = state.Script with { Stack = [], Wait = null, Completed = true, Locked = false },
            IsSurfing = false, IsCycling = false };
        // Controlled test placement invokes normal map initialization. Battle entry,
        // all combat, terminal settlement and return below use Execute only.
        state = Call(session, "Pump", Call(session, "EnterMap", state, null));
        return Strict(state, revision, "placed-" + mapName);
    }
    void Send(CampaignSession session, CampaignCommand command) {
        var revision = session.Revision;
        var id = "tower-probe:" + sequence++;
        var result = session.Execute(id, revision, command);
        Check(result.Status == CommandStatus.Accepted, command + " -> " + result.Status + ": " + result.Error);
        var stale = session.Execute(id, revision, command);
        Check(stale.Status == CommandStatus.Rejected && stale.Error == "stale_revision" &&
            ReferenceEquals(stale.State, result.State), "Stale duplicate must leave committed state unchanged");
    }
    void Ack(CampaignSession session) {
        var kind = session.State.Script.Wait?.Kind;
        Send(session, kind == StoryWaitKind.ButtonPress ? new("button_press", Text: "A") : new("ack"));
    }
    void Until(CampaignSession session, Func<bool> done, string label, int budget = 300) {
        for (int i = 0; i < budget && !done(); i++) Ack(session);
        Check(done(), "Bounded ACK failed: " + label);
    }
    CampaignState BattleReady(CampaignState state) {
        // Preserve all recorded individuals and their ownership receipts. Controlled
        // fixture training sets level 100 with source stats/XP and legal TM moves.
        return state with { Party = state.Party.Select((p,i) => {
            if(p.IsEgg)return p; // Egg source level/stat basis must remain untouched.
            var mon = p.Pokemon; var species = pokemon.Species[mon.Species]; var b = species.BaseStats;
            int Other(int v,int iv,int ev,Stat stat) => StatRules.CalculateOther(v,iv,ev,100,mon.Nature,stat);
            var stats = new BattleStats(StatRules.CalculateHp(b.Hp,mon.Ivs.Hp,mon.Evs.Hp,100),
                Other(b.Attack,mon.Ivs.Attack,mon.Evs.Attack,Stat.Attack),
                Other(b.Defense,mon.Ivs.Defense,mon.Evs.Defense,Stat.Defense),
                Other(b.SpecialAttack,mon.Ivs.SpecialAttack,mon.Evs.SpecialAttack,Stat.SpecialAttack),
                Other(b.SpecialDefense,mon.Ivs.SpecialDefense,mon.Evs.SpecialDefense,Stat.SpecialDefense),
                Other(b.Speed,mon.Ivs.Speed,mon.Evs.Speed,Stat.Speed));
            string[] names = i switch {
                0 => ["PSYCHIC","THUNDERBOLT","ICE_BEAM","RECOVER"],
                1 => ["FLAMETHROWER","DRAGON_CLAW","BRICK_BREAK","AERIAL_ACE"],
                2 => ["SURF","ICE_BEAM","THUNDERBOLT","PSYCHIC"],
                _ => [] };
            var moves = names.Length == 0 ? mon.Moves.Select(m => m with { CurrentPp=m.MaximumPp }).ToImmutableArray()
                : names.Select(n => pokemon.Moves[new("pret:MOVE_" + n)]).Select(m => new PokemonMoveSlot(m.Id,m.Pp,m.Pp)).ToImmutableArray();
            return p with { Status=0, Pokemon=mon with { Level=100,Experience=GrowthRules.ExperienceAtLevel(species.GrowthRate,100),
                Stats=stats,CurrentHp=stats.MaxHp,StatCalculationEvs=null,Moves=moves } };
        }).ToImmutableArray() };
    }
    CampaignSession Start(TrainerTowerChallengeKind course, CampaignSession? origin = null) {
        var state = BattleReady(origin?.State ?? checkpoint.State);
        state = state with { Script=state.Script with { Variables=state.Script.Variables.SetItem("VAR_MAP_SCENE_TRAINER_TOWER",0) } };
        var session = Place(state,origin?.Revision ?? checkpoint.Generation,"map.trainer-tower-lobby",9,8);
        var oldRecords = session.State.TrainerTower!.Records;
        var retainedBefore = Hash(session.State.TrainerTowerHistory);
        Send(session,new("move",Direction.North));
        Until(session,()=>session.SourceMenu is not null,"real lobby yes/no/info menu");
        Check(session.SourceMenu!.Definition.Options.Length == 3,"Expected lobby three-choice menu");
        Send(session,new("ack",Choice:0));
        Check(session.SourceMenu!.Definition.Options.Length == 5,"Expected lobby five-course/cancel menu");
        Send(session,new("ack",Choice:(int)course));
        Until(session,()=>session.State.Script.Completed && session.State.Script.Wait is null && session.State.Dialogue is null,"course start");
        var tower=session.State.TrainerTower!;
        Check(tower.SelectedCourse==course && tower.CurrentRecord.Validated && tower.CurrentRecord.FloorsCleared==0 && tower.TimerRunning,"Public course start fields");
        for(int i=0;i<4;i++) if(i!=(int)course) Check(tower.Records[i]==oldRecords[i],"Other course record changed on selection");
        Check(tower.CurrentRecord.ReceivedPrize==oldRecords[(int)course].ReceivedPrize &&
            tower.CurrentRecord.BestTimeFrames==oldRecords[(int)course].BestTimeFrames,"Restart erased existing prize or best time");
        Check(Hash(session.State.TrainerTowerHistory)==retainedBefore,"Choosing a course must preserve the global retained receipt");
        var rngBefore=session.State.World.Rng;
        Send(session,new("trainer_tower_tick",Quantity:123));
        Check(session.State.World.Rng==rngBefore,"Clock tick must not draw RNG");
        Check(session.State.TrainerTower!.CurrentRecord.TimerFrames==123,"Public timer advances exactly 123 frames");
        return Strict(session.State,session.Revision,"started-"+course);
    }
    static SourceBattleFrame? Frame(CampaignState state) => state.ForestTrainerBattle?.Frame ?? state.DoubleBattle?.Frame;
    static bool Settled(CampaignState state) => state.ForestTrainerBattle?.Stage==CampaignForestTrainerStage.Finished || state.DoubleBattle?.Stage==CampaignDoubleBattleStage.Finished;
    static int ReceiptCount(CampaignState state) => state.TrainerTowerHistory?.Length ?? 0;
    static long LastReceiptNumber(CampaignState state) {
        var history=state.TrainerTowerHistory??[];
        Check(history.Length<=1,"Current Tower representation must be bounded to the last receipt");
        if(history.IsEmpty)return 0;
        const string prefix="trainer-tower-battle:";
        var id=history[0].Id;
        Check(id.StartsWith(prefix,StringComparison.Ordinal) &&
            long.TryParse(id[prefix.Length..],System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,out _),"Retained receipt ID must be a positive canonical Int64");
        var number=long.Parse(id[prefix.Length..],System.Globalization.CultureInfo.InvariantCulture);
        Check(number>0 && id==prefix+number.ToString(System.Globalization.CultureInfo.InvariantCulture),"Retained receipt ID spelling must be canonical");
        return number;
    }
    public CampaignSession Course(TrainerTowerChallengeKind course, bool lose=false, CampaignSession? origin=null,
        Action<CampaignSession>? onSettled=null) {
        var session=Start(course,origin);
        if(lose) {
            var state=session.State with { Party=session.State.Party.Select((p,i)=>p.IsEgg?p:p with {
                Pokemon=p.Pokemon with { CurrentHp=i<2?1:0, Moves=[Slot(i==0?"SAFEGUARD":"GROWL")] }
            }).ToImmutableArray() };
            session=Strict(state,session.Revision,"controlled-loss-precondition");
        }
        var courseBefore=session.State.TrainerTower!;
        var serialBefore=LastReceiptNumber(session.State);
        var ordinarySingleBefore=Hash(session.State.ForestTrainerHistory);
        var ordinaryDoubleBefore=Hash(session.State.DoubleBattleHistory);
        var money=session.State.Money;
        var xp=session.State.Party.Select(p=>p.Pokemon.Experience).ToArray();
        var evs=session.State.Party.Select(p=>p.Pokemon.Evs).ToArray();
        var format=content.TrainerTowerCatalog!.Course(course).Floors[0].Format;
        session=Place(session.State,session.Revision,"map.trainer-tower-1f",format==TrainerTowerBattleFormat.Double?8:9,13);
        Send(session,new("move",Direction.East));
        Until(session,()=>Frame(session.State) is not null,"real floor coordinate-trigger entry");
        foreach(var blocked in new CampaignCommand[] { new("move",Direction.West), new("battle_menu",Text:"run"),
            new("field_fly",PartyIndex:1), new("party_swap",Choice:0,PartyIndex:1) }) {
            var before=session.State;var revision=session.Revision;
            var rejected=session.Execute("tower-blocked:"+sequence++,revision,blocked);
            Check(rejected.Status==CommandStatus.Rejected && ReferenceEquals(before,session.State) && session.Revision==revision,
                "Active Tower escape/field action must reject unchanged: "+blocked.Kind+" -> "+rejected.Error);
        }
        var expectedBattles=format==TrainerTowerBattleFormat.Knockout&&!lose?3:1;
        int finished=0; string? lastSettled=null; bool sawPostResultMotion=false;
        for(int budget=0;budget<12000;budget++) {
            var state=session.State;var frame=Frame(state);
            if(frame is not null) {
                Check(frame.TrainerTowerEncounter is not null,"Actual Tower encounter descriptor required");
                Check(validator.ValidatePlayerSave(state)=="save_event_active","Live battle cannot be a stable player save");
                Check(state.TrainerTower!.CurrentRecord.FloorsCleared==0,"Progress advanced before floor return");
                if(Settled(state)) {
                    var id=state.ForestTrainerBattle?.Id??state.DoubleBattle!.Id;
                    if(id!=lastSettled) {
                        finished++;lastSettled=id;
                        var receipt=state.TrainerTowerHistory!.Value[^1];
                        Check(id=="trainer-tower-battle:"+(serialBefore+finished),"Next ID must extend the retained canonical serial");
                        Check(receipt.Id==id && receipt.Course==course && receipt.FloorIndex==0 && receipt.TrainerIndex==finished-1 && receipt.Level==100,"Receipt descriptor identity");
                        Check(receipt.Outcome==(lose?WildBattleOutcome.Lost:WildBattleOutcome.Won),"Unexpected actual battle outcome: "+receipt.Outcome);
                        var members=content.TrainerTowerCatalog.DescribeBattle(course,0,finished-1,100).Members.Length;
                        Check(receipt.OpponentsSeen.Length>0 && (lose || receipt.OpponentsSeen.Length==members),"Receipt seen-opponent completeness");
                        Check(ReceiptCount(state)==1 && LastReceiptNumber(state)==serialBefore+finished,"Settlement replaces only the last receipt and advances its ID once");
                        Check(state.TrainerTower.FloorChain!.TrainerIndex==finished-1,"Receipt settlement must not advance chain before result ACK");
                        onSettled?.Invoke(session);
                    }
                    Ack(session);continue;
                }
                Check(LastReceiptNumber(state)==serialBefore+finished,"Live entry/combat must not consume a settlement receipt");
                var activeId=state.ForestTrainerBattle?.Id??state.DoubleBattle!.Id;
                Check(activeId=="trainer-tower-battle:"+(serialBefore+finished+1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "Each live entry derives the next ID from the retained last receipt");
                BattleCommand(session);continue;
            }
            if(finished==expectedBattles && state.Script.Completed && state.Script.Wait is null && state.Dialogue is null && state.Motions.IsEmpty) break;
            if(state.Motions.Length>0 && state.Script.Wait?.Kind==StoryWaitKind.Presentation && state.TrainerTower!.CurrentRecord.FloorsCleared==1) {
                sawPostResultMotion=true;
                Check(validator.ValidatePlayerSave(state)=="save_event_active","Final actor motion must keep save gate closed");
            }
            Check(ReceiptCount(state)==1 && LastReceiptNumber(state)==serialBefore+finished,"ACK or chain bridge duplicated or discarded the retained receipt");
            Check(validator.ValidatePlayerSave(state) is not null,"Active chain/story bridge must not save");
            Ack(session);
        }
        var final=session.State;
        Check(finished==expectedBattles,"Not all expected real battles settled");
        Check(final.ForestTrainerBattle is null && final.DoubleBattle is null && final.TrainerTower!.FloorChain is null,"Final return must clear active battle and chain");
        Check(final.TrainerTower!.CurrentRecord.FloorsCleared==(lose?0:1),"Floor increments once only after complete win chain");
        Check(ReceiptCount(final)==1 && LastReceiptNumber(final)==serialBefore+expectedBattles,"Return changed bounded receipt or its monotonic ID");
        Check(final.TrainerTower.TimerRunning && final.TrainerTower.CurrentRecord.TimerFrames==123,"Return must preserve clock intent and elapsed frames");
        Check(final.Money==money,"Tower settlement changed money");
        Check(final.Party.Select(p=>p.Pokemon.Experience).SequenceEqual(xp),"Tower awarded XP");
        Check(final.Party.Select(p=>p.Pokemon.Evs).SequenceEqual(evs),"Tower awarded EVs");
        Check(Hash(final.ForestTrainerHistory)==ordinarySingleBefore && Hash(final.DoubleBattleHistory)==ordinaryDoubleBefore,"Tower polluted ordinary trainer histories");
        for(int i=0;i<4;i++) if(i!=(int)course) Check(final.TrainerTower.Records[i]==courseBefore.Records[i],"Battle changed another course record");
        if(lose) {
            Check(final.World.Player.MapId.Value=="map.trainer-tower-lobby" && final.World.Player.X==9 && final.World.Player.Y==8,"Loss must finish lobby walk after warp at (9,7)");
            Check(final.Party.All(p=>p.Pokemon.CurrentHp==p.Pokemon.Stats.MaxHp && p.Status==0 && p.Pokemon.Moves.All(m=>m.CurrentPp==m.MaximumPp)),"Loss must heal party");
            Check(!final.TrainerTower.CurrentRecord.HasLost && final.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_TRAINER_TOWER")==0,"Lobby must consume loss and reset entry gate");
        } else if(format is TrainerTowerBattleFormat.Double or TrainerTowerBattleFormat.Knockout)
            Check(sawPostResultMotion,"Expected final actor-motion ACK boundary");
        return Strict(final,session.Revision,"returned-"+course+(lose?"-loss":"-win"));
    }
    public void RejectMismatchedFinishedCallbacks(TrainerTowerChallengeKind course) {
        SourceBattleFrame? capturedFrame=null;
        string? capturedId=null;
        var checks=0;
        var restored=Course(course,onSettled:session=> {
            var state=session.State;
            var frame=Frame(state)!;
            var receipt=state.TrainerTowerHistory!.Value.Single();
            capturedFrame=frame;capturedId=receipt.Id;
            Check(receipt.Outcome==WildBattleOutcome.Won && receipt.OpponentsSeen.Length==2,
                "Guard checks require a real two-opponent Finished win");
            void Denied(string method,CampaignState candidate,SourceBattleFrame actualFrame,string id,
                string expected,ImmutableArray<int>? sent=null) {
                var before=session.State;var revision=session.Revision;var candidateHash=Hash(candidate);
                object?[] arguments=sent is {} seen ? [candidate,actualFrame,id,seen] : [candidate,actualFrame,id];
                var failure=Assert.ThrowsExactly<InvalidDataException>(()=>Call(session,method,arguments));
                Check(failure.Message==expected,"Wrong guard: "+method+" expected="+expected+" actual="+failure.Message);
                Check(ReferenceEquals(before,session.State) && session.Revision==revision && Hash(candidate)==candidateHash,
                    "Rejected private callback mutated the live session or supplied immutable state");
                checks++;
            }
            Denied("SettleTrainerTowerBattle",state,frame,receipt.Id,
                "trainer_tower_settlement_duplicate",receipt.OpponentsSeen);
            var number=LastReceiptNumber(state);
            var previous="trainer-tower-battle:"+(number>1?number-1:number+1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Denied("SettleTrainerTowerBattle",state,frame,previous,
                "trainer_tower_settlement_identity",receipt.OpponentsSeen);
            Denied("ReturnFromTrainerTowerBattle",state with{TrainerTowerHistory=null},frame,receipt.Id,
                "trainer_tower_history_missing");
            Denied("ReturnFromTrainerTowerBattle",state with{TrainerTowerHistory=[]},frame,receipt.Id,
                "trainer_tower_history_missing");
            Denied("ReturnFromTrainerTowerBattle",state with{TrainerTowerHistory=[receipt with{Outcome=WildBattleOutcome.Lost}]},frame,receipt.Id,
                "trainer_tower_history_missing");
            Denied("ReturnFromTrainerTowerBattle",state with{TrainerTowerHistory=[receipt with{OpponentsSeen=receipt.OpponentsSeen.Reverse().ToImmutableArray()}]},frame,receipt.Id,
                "trainer_tower_history_missing");
            Denied("ReturnFromTrainerTowerBattle",state with{TrainerTowerHistory=[receipt with{OpponentsSeen=[0]}]},frame,receipt.Id,
                "save_trainer_tower_seen");
            Denied("ReturnFromTrainerTowerBattle",state with{TrainerTowerHistory=[receipt with{Id=previous}]},frame,receipt.Id,
                "trainer_tower_history_missing");
            Denied("ReturnFromTrainerTowerBattle",state,frame with{},receipt.Id,
                "trainer_tower_battle_identity");
        });
        Check(checks==9 && capturedFrame is not null && capturedId is not null,"All real-Finished corruption guards must execute");
        var stable=restored.State;var generation=restored.Revision;var stableHash=Hash(stable);
        var duplicateReturn=Assert.ThrowsExactly<InvalidDataException>(()=>
            Call(restored,"ReturnFromTrainerTowerBattle",stable,capturedFrame!,capturedId!));
        Check(duplicateReturn.Message=="trainer_tower_battle_identity","Return after active clear must fail identity guard");
        Check(ReferenceEquals(stable,restored.State) && restored.Revision==generation && Hash(restored.State)==stableHash,
            "Return after active clear changed stable state");
    }
    PokemonMoveSlot Slot(string name) { var move=pokemon.Moves[new("pret:MOVE_"+name)];return new(move.Id,move.Pp,move.Pp); }
    static ElementType Type(string value)=>Enum.Parse<ElementType>(value.Replace("TYPE_",""),true);
    double Score(PokemonInstance actor,PokemonInstance target,int slot) {
        if(slot<0)return 1;
        var m=pokemon.Moves[actor.Moves[slot].Move];
        if(m.Power==0) return 0;
        var us=pokemon.Species[actor.Species];var them=pokemon.Species[target.Species];
        double factor=TypeRules.EffectivenessTenths(Type(m.Type),Type(them.Type1))/10.0;
        if(them.Type2!=them.Type1)factor*=TypeRules.EffectivenessTenths(Type(m.Type),Type(them.Type2))/10.0;
        if(target.AbilityId=="ABILITY_LEVITATE" && m.Type=="TYPE_GROUND") factor=0;
        if(target.AbilityId=="ABILITY_VOLT_ABSORB" && m.Type=="TYPE_ELECTRIC") factor=0;
        if(target.AbilityId=="ABILITY_WATER_ABSORB" && m.Type=="TYPE_WATER") factor=0;
        var physical=TypeRules.Category(Type(m.Type))==DamageCategory.Physical;
        var attack=physical?actor.Stats.Attack:actor.Stats.SpecialAttack;
        var defense=physical?target.Stats.Defense:target.Stats.SpecialDefense;
        return m.Power*factor*(m.Type==us.Type1||m.Type==us.Type2?1.5:1)*attack/Math.Max(1.0,defense)*(m.Accuracy==0?1:m.Accuracy/100.0);
    }
    void BattleCommand(CampaignSession session) {
        var state=session.State;
        if(state.ForestTrainerBattle is {} single) {
            var p=single.Presentation;var frame=single.Frame;
            if(p.Phase is CampaignBattlePhase.Events or CampaignBattlePhase.Results) { Ack(session);return; }
            if(frame.FourTurn?.Phase==SourceFourActionPhase.ShiftPending) { Send(session,new("trainer_shift",Choice:0));return; }
            if(frame.PendingSwitches.Any(pending=>pending.Slot.Side==BattleSide.Player)) {
                Send(session,new("battle_switch",PartyIndex:SourceBattleIdentityRules.LegalReserveIndices(frame,BattleSide.Player).First()));return;
            }
            var actor=frame.Get(new(0)).Body!.Pokemon;var target=frame.Get(new(1)).Body!.Pokemon;
            int slot=Enumerable.Range(-1,actor.Moves.Length+1).Where(i=>SourceWildBattlePolicy.ValidateMoveSelection(pokemon,frame,i)is null)
                .OrderByDescending(i=>Score(actor,target,i)).First();
            Send(session,new("battle_move",MoveSlot:slot));return;
        }
        var b=state.DoubleBattle!;
        if(b.CurrentEvent is not null || b.Frame.FourTurn is not null && b.Frame.PendingSwitches.IsEmpty) { Ack(session);return; }
        if(!b.Frame.PendingSwitches.IsEmpty) {
            var pending=b.Frame.PendingSwitches[0];
            Send(session,new("double_switch",PartyIndex:pending.Slot.Value,Choice:SourceBattleIdentityRules.LegalReserveIndices(b.Frame,BattleSide.Player).First()));return;
        }
        if(b.SelectingSlot is not {} selecting) { Ack(session);return; }
        var cell=b.Frame.Get(new(selecting));var body=cell.Body!.Pokemon;
        if(SourceFourActionTurns.RequiresStruggle(pokemon,b.Frame,cell.Slot)) { Send(session,new("double_move",PartyIndex:selecting,MoveSlot:-1));return; }
        var options=from slot in Enumerable.Range(0,body.Moves.Length)
                    from target in b.Frame.Slots.Where(c=>b.Frame.CanAct(c.Slot) &&
                        (c.Slot.Side==BattleSide.Opponent || pokemon.Moves[body.Moves[slot].Move].Power==0))
                    let error=SourceBattleKernel.ValidateAction(pokemon,b.Frame,new(cell.Occupant!.Value,b.Frame.Revision,slot,target.Slot))
                    where error is null or "source_kernel_switch_owner_required" or "source_kernel_flinch_order_not_migrated" or "source_kernel_protection_order_required"
                    orderby Score(body,target.Body!.Pokemon,slot) descending
                    select (slot,target.Slot);
        var available=options.ToArray();
        Check(available.Length>0,"No public double target for actor="+selecting+" moves="+string.Join(",",body.Moves.Select(m=>m.Move.Value)));
        var choice=available[0];
        Send(session,new("double_move",PartyIndex:selecting,MoveSlot:choice.slot,Choice:choice.Slot.Value));
    }
}
