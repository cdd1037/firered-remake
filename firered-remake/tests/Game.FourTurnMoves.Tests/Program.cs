using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Growth;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length != 3) throw new ArgumentException("PRODUCT_ROOT SOURCE_VECTORS_JSON OUTPUT_JSON");
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog = proof.Catalog;
using var source = JsonDocument.Parse(File.ReadAllBytes(args[1]));
int groups = 0, checks = 0, failures = 0, cleanTurns = 0;
var results = new List<object>();
void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
void Test(string label, Action run) { groups++; try { run(); results.Add(new { label, passed = true }); Console.WriteLine("PASS " + label); } catch (Exception ex) { failures++; results.Add(new { label, passed = false, error = ex.ToString() }); Console.WriteLine("FAIL " + label + " " + ex); } }
string Json<T>(T x) => JsonSerializer.Serialize(x);
MoveId Id(string s) => new("pret:MOVE_" + s);
WildBattleCombatant Mon(string species, int level, params string[] names) {
    var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
    return new(p with { Moves = names.Select(n => new PokemonMoveSlot(Id(n), catalog.Moves[Id(n)].Pp, catalog.Moves[Id(n)].Pp)).ToImmutableArray() });
}
WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon, m.HeldItem, m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? supplied = null, uint seed = 31) {
    var m = supplied ?? [Mon("PIDGEY", 30, "MINIMIZE"), Mon("SNORLAX", 100, "DEFENSE_CURL"), Mon("SPEAROW", 20, "DEFENSE_CURL"), Mon("RATTATA", 20, "MINIMIZE")];
    return new() { Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn, SourceMechanicsRulesVersion = SourceBattleKernel.Version,
        BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(seed),
        Parties = [new(BattleSide.Player, [Member(m[0]), Member(m[2])]), new(BattleSide.Opponent, [Member(m[1]), Member(m[3])])],
        Slots = Enumerable.Range(0,4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, m[i])).ToImmutableArray() };
}
SourceBattleFrame Put(SourceBattleFrame f, int i, WildBattleCombatant m) { var c = f.Slots[i]; var key = c.Member!.Value; var p = f.Party(key.Side); return f with { Slots = f.Slots.SetItem(i,c with { Body=m }), Parties=f.Parties.SetItem((int)key.Side,p with { Members=p.Members.SetItem(key.Index,Member(m)) }) }; }
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f, int[]? positions = null, int[]? targets = null) => f.Slots.Where(c => f.CanAct(c.Slot)).Select(c => {
    int pos = positions?[c.Slot.Value] ?? c.Registers.Turn?.Lock?.PpSlot ?? 0;
    var move = catalog.Moves[c.Registers.Turn?.Lock?.Move ?? c.Body!.Pokemon.Moves[pos].Move];
    var target = c.Registers.Turn?.Lock?.Target ?? new BattlerSlot(targets?[c.Slot.Value] ?? (move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : c.Slot.Value ^ 1));
    return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,pos,target);
}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r) { Check(r.Succeeded,r.Fault ?? "accepted"); Check(SourceBattleIdentityRules.Validate(r.State) is null,"valid output"); return r; }
SourceFourActionResult Begin(SourceBattleFrame f,int[]? p=null,int[]? t=null) => Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,p,t)));
SourceFourActionResult Next(SourceFourActionResult r) => Ok(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
(SourceBattleFrame State,List<SourceBattleEvent> Events,List<RandomTrace> Trace) Turn(SourceBattleFrame f,int[]? p=null,int[]? t=null) {
    var r=Begin(f,p,t); var events=r.Events.ToList(); var trace=r.Trace.ToList();
    while(r.State.FourTurn is not null) { Check(r.State.FourTurn.Phase is SourceFourActionPhase.Actions or SourceFourActionPhase.EndTurn or SourceFourActionPhase.EndTurnResume,"no unexpected external boundary");
        var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!; var actual=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value); var replay=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);
        Check(Json(actual)==Json(replay),"exact pause JSON state/events/RNG replay"); r=Ok(actual); events.AddRange(r.Events);trace.AddRange(r.Trace);
    } Check(r.CompletedCleanTurn,"clean full turn"); cleanTurns++; return(r.State,events,trace);
}
SourceKernelResult Act(SourceBattleFrame f,int actor,int target,int pos=0) { var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,pos,new(target))); Check(r.Succeeded,r.Fault??"action"); return r; }
void Rejected(SourceBattleFrame f, SourceFourActionResult r,string why) => Check(!r.Succeeded && ReferenceEquals(f,r.State) && r.Trace.IsEmpty && r.Events.IsEmpty,why+" atomic: "+r.Fault);
Test("runtime, pin, real Twins default level-22 party and retained contact abilities",()=>{
    Check(Environment.Version.Major==10,"actual runtime10"); Check(source.RootElement.GetProperty("sourceCommit").GetString()==PokemonFactory.SourceCommit,"pinned source");
    foreach(var entry in new[]{("CLEFAIRY",new[]{"SING","DOUBLE_SLAP","FOLLOW_ME","MINIMIZE"}), ("JIGGLYPUFF",new[]{"DEFENSE_CURL","POUND","DISABLE","ROLLOUT"})}) {
        var m=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+entry.Item1),22,42,IvPolicy.Fixed(0),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
        Check(m.Moves.Select(x=>x.Move).SequenceEqual(entry.Item2.Select(Id)),"actual default moves "+entry.Item1);
        Check(m.AbilityId=="ABILITY_CUTE_CHARM","retain original ability "+entry.Item1);
        foreach(var move in m.Moves) Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[move.Move]),"shared move registered "+move.Move);
    }
});
Test("Minimize and Defense Curl obey source bit/PP/stat ordering including +6 cap and identical party identities",()=>{
    var f=Frame([Mon("PIDGEY",30,"MINIMIZE") with{EvasionStage=12},Mon("SNORLAX",100,"DEFENSE_CURL") with{DefenseStage=12},Mon("PIDGEY",30,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")]);
    var r=Turn(f);Check(r.State.Slots[0].Body!.Minimized&&r.State.Slots[1].Body!.DefenseCurled,"bits set at caps");
    Check(r.State.Slots[2].Body!.DefenseStage==7&&!r.State.Slots[2].Body!.Minimized,"same species independent slot");
    var e=r.Events;Check(e.FindIndex(x=>x is SourceMtMoonVolatileEvent { Side.Value:0,Volatile:"minimized" })<e.FindIndex(x=>x is SourcePpCommandEvent { Actor.Value:0 }),"minimize before PP");
    Check(e.FindIndex(x=>x is SourceDefenseCurledEvent { Side.Value:1 })>e.FindIndex(x=>x is SourcePpCommandEvent { Actor.Value:1 }),"defensecurl after PP");
    Check(e.OfType<SourceStatAtMaximumEvent>().Count()==2,"cap events");
    foreach(var c in r.State.Slots)Check(c.Body!.Pokemon.Moves[0].CurrentPp==f.Slots[c.Slot.Value].Body!.Pokemon.Moves[0].CurrentPp-1&&Json(c.Body.Pokemon)==Json(r.State.Party(c.Slot.Side).Members[c.Member!.Value.Index].Pokemon),"party PP identity");
});
Test("five complete Rollout turns use one PP then 30/60/120/240/480 source power and forced slot",()=>{
    uint seed=source.RootElement.GetProperty("rolloutFiveTurnSeed").GetUInt32();
    var f=Frame([Mon("PIDGEY",5,"ROLLOUT","MINIMIZE") with{AccuracyStage=12},Mon("SNORLAX",100,"MINIMIZE"),Mon("SPEAROW",20,"DEFENSE_CURL"),Mon("RATTATA",20,"DEFENSE_CURL")],seed);
    int pp=f.Slots[0].Body!.Pokemon.Moves[0].CurrentPp;var powers=source.RootElement.GetProperty("rollout").EnumerateArray().Select(v=>v.GetProperty("power").GetInt32()).Take(5).ToArray();
    for(int i=0;i<5;i++) {
        var before=f;var r=Turn(f);f=r.State;
        Check((f.Slots[0].Registers.Turn?.Lock?.TurnsRemaining??0)==4-i,"source counter "+i);
        Check(f.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==pp-1,"PP once across lock");
        Check(r.Events.OfType<SourcePpCommandEvent>().Count(x=>x.Actor.Value==0)==(i==0?1:0),"PP command exists first hit only");
        var h=r.Events.OfType<SourceHpChangedEvent>().Single(x=>x.Actor.Value==0);
        var target=before.Slots[1].Body!; // Snorlax acts first and raises Evasion only.
        // Pinned pokemon.c physical neutral fixture: no STAB, screens, weather or stat changes.
        int baseDamage=Math.Max(1,(2*before.Slots[0].Body!.Pokemon.Level/5+2)*powers[i]*before.Slots[0].Body!.Pokemon.Stats.Attack/target.Pokemon.Stats.Defense/50)+2;
        int expected=Math.Max(1,baseDamage*(h.Critical?2:1)*h.RandomPercent/100);
        Check(h.Damage==expected,"source extracted power at hit "+i+": "+h.Damage+"/"+expected);
        if(i<4){var bad=Choices(f).Select(a=>a.Actor.Slot.Value==0?a with{MoveSlot=1}:a).ToImmutableArray();Rejected(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,bad),"forced selected slot");}
    }
    Check(f.Slots[0].Registers.Turn is null,"lock ends after fifth hit");
});
Test("Rollout Defense Curl multiplier, first miss and continued miss consume exact source RNG",()=>{
    for(uint seed=0;seed<48;seed++) {
        var f=Frame([Mon("PIDGEY",30,"ROLLOUT"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SPEAROW",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")],seed);
        var a=Act(f,0,1);var b=Act(Put(f,0,f.Slots[0].Body! with{DefenseCurled=true}),0,1);
        Check(Json(a.Trace)==Json(b.Trace),"curl changes no draws");
        if(a.Events.OfType<SourceHpChangedEvent>().Any()) { Check(a.State.Slots[0].Registers.Turn?.Lock?.TurnsRemaining==4,"start lock");Check(b.Events.OfType<SourceHpChangedEvent>().Single().Damage>a.Events.OfType<SourceHpChangedEvent>().Single().Damage,"curl boosts power");
            var locked=a.State with{Rng=new(0)}; var miss=Put(locked,0,locked.Slots[0].Body! with{AccuracyStage=0});
            var r=Act(miss,0,1);if(r.Events.OfType<SourceMoveMissedEvent>().Any()){Check(r.State.Slots[0].Registers.Turn is null,"miss cancels");Check(r.Trace.Length==1&&!r.Events.OfType<SourcePpCommandEvent>().Any(),"continuation miss accuracy only");}
        } else {Check(a.Trace.Length==1&&a.State.Slots[0].Registers.Turn is null,"first miss no damage RNG/lock");Check(a.Events.OfType<SourcePpCommandEvent>().Count()==1,"first miss spends PP");}
    }
});
Test("Disable source duration, first matching PP predicate and no target failure consume exact draws",()=>{
    foreach(var v in source.RootElement.GetProperty("disable").EnumerateArray()) {
        uint seed=v.GetProperty("seed").GetUInt32();var f=Frame([Mon("PIDGEY",30,"DISABLE") with{AccuracyStage=12},Mon("SNORLAX",100,"POUND") with{LastUsedMove=Id("POUND")},Mon("SPEAROW",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")],seed);
        var a=Act(f,0,1);Check(a.State.Slots[1].Body!.DisabledMove==Id("POUND"),"actual last move");Check(a.State.Slots[1].Body!.DisableTurnsRemaining==v.GetProperty("turns").GetInt32(),"source duration");Check(a.State.Rng.Seed==v.GetProperty("finalSeed").GetUInt32()&&a.Trace.Length==2,"source RNG");
        var empty=Put(f,1,f.Slots[1].Body! with{LastUsedMove=null});var failure=Act(empty,0,1);Check(failure.Trace.Length==1&&!failure.Events.OfType<SourceDisableChangedEvent>().Any(),"no duration roll when fail");
        var noPp=f.Slots[1].Body!;noPp=noPp with{Pokemon=noPp.Pokemon with{Moves=[noPp.Pokemon.Moves[0] with{CurrentPp=0}]}};
        failure=Act(Put(f,1,noPp),0,1);Check(failure.Trace.Length==1,"last slot zero PP fails");
    }
});
Test("Disable applied after selection blocks execution before PP; selectable again after source timer expiry",()=>{
    var f=Frame([Mon("ELECTRODE",50,"DISABLE","DEFENSE_CURL"),Mon("SNORLAX",30,"POUND","DEFENSE_CURL") with{LastUsedMove=Id("POUND")},Mon("SPEAROW",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")]);
    var r=Turn(f);Check(r.Events.OfType<SourceMoveFailedEvent>().Any(x=>x.Actor.Value==1&&x.Reason=="disabled"),"disabled during selected action");
    Check(!r.Events.OfType<SourcePpCommandEvent>().Any(x=>x.Actor.Value==1),"disabled action PP untouched");
    f=r.State;Rejected(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f)),"already disabled selection");
    int remaining=f.Slots[1].Body!.DisableTurnsRemaining;for(int i=0;i<remaining;i++){r=Turn(f,[1,1,0,0]);f=r.State;}
    Check(f.Slots[1].Body!.DisabledMove is null&&f.Slots[1].Body!.DisableTurnsRemaining==0,"source residual expiration");
    Check(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,[1,0,0,0])).Succeeded,"expired move selectable");
});
Test("forced Rollout can become disabled, paralysis preserves lock, flinch cancels and sleep cancels at ready",()=>{
    var f=Frame([Mon("PIDGEY",5,"ROLLOUT") with{AccuracyStage=12},Mon("SNORLAX",100,"MINIMIZE"),Mon("SPEAROW",20,"DEFENSE_CURL"),Mon("RATTATA",20,"DEFENSE_CURL")]);var started=Act(f,0,1).State;
    var disabled=Put(started,0,started.Slots[0].Body! with{DisabledMove=Id("ROLLOUT"),DisableTurnsRemaining=2});
    var r=Turn(disabled);Check(r.Events.OfType<SourceMoveFailedEvent>().Any(x=>x.Actor.Value==0&&x.Reason=="disabled")&&r.State.Slots[0].Registers.Turn is null,"forced bypass selection reaches disable canceller");
    var flinch=Put(started,0,started.Slots[0].Body! with{Flinched=true});var a=Act(flinch,0,1);Check(a.State.Slots[0].Registers.Turn is null&&a.Trace.IsEmpty,"flinch no RNG and cancels");
    bool blocked=false;for(uint seed=0;seed<20&&!blocked;seed++){var paralyzed=Put(started with{Rng=new(seed)},0,started.Slots[0].Body! with{Status=WildBattleStatus.Paralysis});a=Act(paralyzed,0,1);if(a.Events.OfType<SourceActionBlockedEvent>().Any()){blocked=true;Check(a.State.Slots[0].Registers.Turn?.Lock?.TurnsRemaining==4,"source paralysis no cancel");}}Check(blocked,"tested paralysis branch");
    var asleep=Put(started,0,started.Slots[0].Body! with{Status=3});r=Turn(asleep);Check(r.State.Slots[0].Registers.Turn is null,"asleep lock cleared at full ready boundary");
});
Test("Rollout retains selected target through Follow Me and survives zero PP continuation",()=>{
    var f=Frame([Mon("PIDGEY",5,"ROLLOUT") with{AccuracyStage=12},Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SPEAROW",20,"DEFENSE_CURL"),Mon("RATTATA",50,"FOLLOW_ME")]);
    var r=Turn(f);Check(r.Events.OfType<SourceHpChangedEvent>().Single().Target.Value==3,"Follow Me actual target");Check(r.State.Slots[0].Registers.Turn?.Lock?.Target.Value==1,"retain source selected target");
    f=r.State;var actor=f.Slots[0].Body!;f=Put(f,0,actor with{Pokemon=actor.Pokemon with{Moves=[actor.Pokemon.Moves[0] with{CurrentPp=0}]}});
    f=Put(f,3,Mon("RATTATA",50,"DEFENSE_CURL"));r=Turn(f);Check(r.Events.OfType<SourceHpChangedEvent>().Single().Target.Value==1,"next forced target uses original selected position");Check(!r.Events.OfType<SourcePpCommandEvent>().Any(x=>x.Actor.Value==0),"zero PP continues without PP command");
});
Test("multihit exact source count and RNG, one retained target and one PP, including immunity and KO",()=>{
    foreach(var v in source.RootElement.GetProperty("multihit").EnumerateArray()) {
        uint seed=v.GetProperty("seed").GetUInt32();var f=Frame([Mon("PIDGEY",5,"DOUBLE_SLAP") with{AccuracyStage=12},Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SPEAROW",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")],seed);
        var a=Act(f,0,3);var h=a.Events.OfType<SourceMultiHitEvent>().Single();Check(h.PlannedHits==v.GetProperty("hits").GetInt32()&&h.ActualHits==h.PlannedHits,"source count");
        Check(a.Events.OfType<SourceHpChangedEvent>().All(x=>x.Target.Value==3)&&a.Events.OfType<SourcePpCommandEvent>().Count()==1,"same actual target all hits PP once");
        Check(a.State.Rng.Seed==v.GetProperty("finalSeed").GetUInt32()&&a.Trace.Length==v.GetProperty("draws").GetInt32(),"source full draw order");
        Check(a.State.Party(BattleSide.Opponent).Members[0].Pokemon.CurrentHp==f.Party(BattleSide.Opponent).Members[0].Pokemon.CurrentHp,"teammate party untouched");
    }
    var immune=Frame([Mon("PIDGEY",30,"DOUBLE_SLAP") with{AccuracyStage=12},Mon("GASTLY",30,"DEFENSE_CURL"),Mon("SPEAROW",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")]);var q=Act(immune,0,1);
    Check(q.Events.OfType<SourceMultiHitEvent>().Single().ActualHits==0&&!q.Trace.Any(x=>x.Purpose.EndsWith(".damage")),"immunity exits before damage draw");
    var ko=Frame([Mon("PIDGEY",1,"DOUBLE_SLAP"),Mon("SNORLAX",100,"DOUBLE_SLAP") with{AccuracyStage=12},Mon("SPEAROW",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")]);
    var turn=Begin(ko);while(turn.State.Queue[turn.State.NextAction].Slot.Value!=1)turn=Next(turn);turn=Next(turn);
    Check(turn.Events.OfType<SourceMultiHitEvent>().Single().ActualHits==1&&turn.Events.OfType<SourceFaintedEvent>().Single().Side.Value==0,"multihit stops after true KO");Check(turn.State.Queue.Single(x=>x.Slot.Value==0).Kind==SourceActionKind.Cancelled,"same occupant queued action cancelled");
});
Test("two ordinary multihit actors finish a complete four-action turn with distinct same-species recipients",()=>{
    var f=Frame([Mon("PIDGEY",5,"DOUBLE_SLAP") with{AccuracyStage=12},Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SPEAROW",5,"DOUBLE_SLAP") with{AccuracyStage=12},Mon("SNORLAX",100,"MINIMIZE")]);
    var r=Turn(f);var hits=r.Events.OfType<SourceMultiHitEvent>().ToArray();Check(hits.Length==2&&hits.All(h=>h.ActualHits==h.PlannedHits),"both original multihit actors execute");
    Check(hits.Select(h=>h.Target.Value).Order().SequenceEqual(new[]{1,3}),"identical species keep distinct target identities");Check(r.Events.OfType<SourcePpCommandEvent>().Count()==4,"four actors spend PP once each");
    foreach(var c in r.State.Slots)Check(Json(c.Body!.Pokemon)==Json(r.State.Party(c.Slot.Side).Members[c.Member!.Value.Index].Pokemon),"full-turn permanent party sync");
});
Test("actual reward learning over forced Rollout position refreshes PP identity and preserves queued lock precedence",()=>{
    var d=Mon("DROWZEE",30,"ROLLOUT","POUND","HYPNOSIS","DISABLE") with{AccuracyStage=12};
    d=d with{Pokemon=d.Pokemon with{Experience=GrowthRules.ExperienceAtLevel(catalog.Species[d.Pokemon.Species].GrowthRate,31)-1}};
    var foe=Mon("PIDGEY",20,"TACKLE");foe=foe with{Pokemon=foe.Pokemon with{CurrentHp=1}};
    var f=Frame([d,foe,Mon("RATTATA",70,"QUICK_ATTACK"),Mon("SNORLAX",100,"DEFENSE_CURL")]);
    f=Act(f,0,3).State; // Genuine first Rollout hit establishes the retained slot3 lock.
    f=f with{Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
    var r=Begin(f,null,[3,0,1,3]);r=Next(r);Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending,"genuine teammate KO");r=Next(r);
    var pending=r.State.Rewards!.Pending!;Check(pending.Recipient.Index==0&&pending.Moves[0].Move==Id("PSYCHIC"),"actual level31 Psychic learning pause");
    var before=r.State.Slots[0].Registers.Turn!.Lock!;
    r=Ok(SourceFourRewards.Learn(catalog,r.State,r.Continuation!.Value,new(pending.Recipient,31,Id("PSYCHIC"),0)));
    var after=r.State.Slots[0].Registers.Turn!.Lock!;Check(after==before with{PpSlotMove=Id("PSYCHIC")},"only PP slot storage identity refreshed");
    Check(r.State.Queue.Single(a=>a.Slot.Value==0).OriginalChosenMove==Id("ROLLOUT"),"original queued selection retained");
    while(r.State.Queue[r.State.NextAction].Slot.Value!=0)r=Next(r);
    var rechargeCell=r.State.Slots[0];var recharge=r.State with{Slots=r.State.Slots.SetItem(0,rechargeCell with{Registers=rechargeCell.Registers with{Turn=rechargeCell.Registers.Turn! with{Lock=rechargeCell.Registers.Turn.Lock! with{Kind=WildBattleCalledLockKind.Recharge,Move=Id("HYPER_BEAM"),TurnsRemaining=1}}}})};
    recharge=Put(recharge,0,recharge.Slots[0].Body! with{DisabledMove=Id("HYPER_BEAM"),DisableTurnsRemaining=2});
    var charged=Next(new SourceFourActionResult(recharge,[],[],null));Check(charged.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="must-recharge"&&!charged.Trace.Any(t=>t.Purpose.Contains(".target."))&&!charged.Events.OfType<SourcePpCommandEvent>().Any(),"forced recharge precedence beats Disable and learned-position retargeting");
    r=Next(r);Check(r.Events.OfType<SourceHpChangedEvent>().Single().Move==Id("ROLLOUT")&&r.Events.OfType<SourceHpChangedEvent>().Single().Target.Value==3,"forced lock beats overwritten selection");
    Check(!r.Events.OfType<SourcePpCommandEvent>().Any()&&r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("PSYCHIC")].Pp,"new learned PP untouched on continued Rollout");
    while(r.State.FourTurn is not null)r=Next(r);Check(r.CompletedCleanTurn,"learning/remaining actions/endturn finish cleanly");cleanTurns++;
});
Test("new sleep cancels retained Rollout immediately; Minimize Stomp predicate doubles source damage",()=>{
    var f=Frame([Mon("PIDGEY",5,"ROLLOUT") with{AccuracyStage=12},Mon("SNORLAX",100,"SING") with{AccuracyStage=12},Mon("SPEAROW",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")]);
    f=Act(f,0,1).State;var asleep=Act(f,1,0);
    Check(WildBattleStatus.IsSleeping(asleep.State.Slots[0].Body!.Status)&&asleep.State.Slots[0].Registers.Turn is null,"Sing clears chain in same action");
    Check(asleep.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Side.Value==0&&e.Volatile=="called-rollout"&&e.Reason=="sleep"),"immediate sleep cancellation event");
    var stomp=Frame([Mon("ELECTRODE",30,"STOMP"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SPEAROW",20,"DEFENSE_CURL"),Mon("RATTATA",20,"DEFENSE_CURL")]);
    var normal=Turn(stomp);var small=Turn(Put(stomp,1,stomp.Slots[1].Body! with{Minimized=true}));
    int a=normal.Events.OfType<SourceHpChangedEvent>().Single().Damage,b=small.Events.OfType<SourceHpChangedEvent>().Single().Damage;
    Check(b>=a*2&&b<=a*2+1&&Json(normal.Trace)==Json(small.Trace),"source minimize-hit predicate before damage randomization");
});
Test("malformed timers locks selected-slot and source metadata reject atomically",()=>{
    var f=Frame();foreach(var body in new[]{f.Slots[0].Body! with{DisabledMove=Id("MINIMIZE")},f.Slots[0].Body! with{DisableTurnsRemaining=2},f.Slots[0].Body! with{DisableTurnsRemaining=6,DisabledMove=Id("MINIMIZE")},f.Slots[0].Body! with{RolloutMoveSlot=0}}){var bad=Put(f,0,body);Rejected(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,Choices(bad)),"bad body");}
    var roll=Act(Frame([Mon("PIDGEY",5,"ROLLOUT") with{AccuracyStage=12},Mon("SNORLAX",100,"MINIMIZE"),Mon("SPEAROW",20,"DEFENSE_CURL"),Mon("RATTATA",20,"MINIMIZE")]),0,1).State;var cell=roll.Slots[0];var locked=cell.Registers.Turn!.Lock!;
    foreach(var badLock in new[]{locked with{TurnsRemaining=5},locked with{StoredDamage=1},locked with{UnableToUseMove=true},locked with{PpSlot=8},locked with{Move=Id("TACKLE")}}){var bad=roll with{Slots=roll.Slots.SetItem(0,cell with{Registers=cell.Registers with{Turn=cell.Registers.Turn with{Lock=badLock}}})};Rejected(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,Choices(roll)),"bad lock");}
    var mutated=catalog with{Moves=catalog.Moves.SetItem(Id("MINIMIZE"),catalog.Moves[Id("MINIMIZE")] with{Accuracy=1})};Rejected(f,SourceFourActionTurns.BeginActions(mutated,f,f.Revision,Choices(f)),"unauthenticated recipe");
});
var output=new{sourceCommit=PokemonFactory.SourceCommit,groups,checks,failures,cleanTurns,results};File.WriteAllText(args[2],JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"{groups} groups, {checks} assertions, {failures} failures, {cleanTurns} clean turns");return groups>0&&checks>0&&cleanTurns>0&&failures==0?0:1;
