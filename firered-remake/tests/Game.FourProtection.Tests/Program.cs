using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length < 3) throw new ArgumentException("PRODUCT_ROOT MODE OUTPUT [SOURCE_VECTORS]");
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog = proof.Catalog;
string Json<T>(T x) => JsonSerializer.Serialize(x);
MoveId Id(string s) => new("pret:MOVE_" + s);
WildBattleCombatant Mon(string species, int level, params string[] names) {
    var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
    return new(p with { Moves = names.Select(n => new PokemonMoveSlot(Id(n), catalog.Moves[Id(n)].Pp, catalog.Moves[Id(n)].Pp)).ToImmutableArray() });
}
if (args[1] == "legacy") {
    var raw = PretRocketHideoutPokemon.Load(args[0]).Catalog;
    var extra = PretVermilionGymTrainers.ExtendPokemon(args[0], PretCeladonApproachPokemon.Load(args[0]).Catalog, celadonApproachRules: true);
    var tower = PretPokemonTowerPokemon.Load(raw with { Moves = raw.Moves.SetItems(extra.Moves) }, args[0], args[0]).Catalog;
    catalog = PretMetronomeMoveCatalog.Load(args[0]).Extend(PretFuchsiaRoutePokemon.Load(PretEeveePokemon.Load(PretSnorlaxPokemon.Load(tower, args[0]).Catalog, args[0]).Catalog, args[0]).Catalog);
    WildBattleCombatant LegacyMon(string name, params string[] moves) => new(PokemonFactory.Create(catalog, new(new("pret:SPECIES_" + name), 30, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon with { Moves = moves.Select(m => new PokemonMoveSlot(Id(m), catalog.Moves[Id(m)].Pp, catalog.Moves[Id(m)].Pp)).ToImmutableArray() });
    var snapshots = new List<object>();
    foreach (string move in new[] { "PROTECT", "DETECT", "ENDURE" }) foreach (uint seed in new uint[] { 0, 31 }) {
        var a = LegacyMon("CLEFAIRY", move, "METRONOME"); var b = LegacyMon("SNORLAX", "TACKLE");
        var s = new WildBattleState(a, b, 0, 0, 0, new(seed), 0, WildBattleOutcome.Ongoing, RulesVersion: WildBattleRules.FuchsiaRulesVersion) { Terrain = WildBattleTerrain.Plain };
        var sequence = new List<object>();
        for (int i = 0; i < 5 && s.Outcome == WildBattleOutcome.Ongoing; i++) {
            var r = WildBattleRules.ResolveMove(catalog, s, 0); if (!r.Succeeded) throw new Exception(r.Fault?.ToString()); sequence.Add(r); s = r.State!;
        }
        snapshots.Add(new { move, seed, hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json(sequence)))) });
    }
    string result = JsonSerializer.Serialize(snapshots, new JsonSerializerOptions { WriteIndented = true }) + "\n";
    File.WriteAllText(args[2], result);
    if (args.Length > 3 && result != File.ReadAllText(args[3])) throw new Exception("Immutable e14578e singles hashes changed");
    Console.WriteLine("PASS six immutable singles action/state/event/RNG sequence hashes"); return;
}
using var source = JsonDocument.Parse(File.ReadAllBytes(args[3]));
int groups = 0, checks = 0, failures = 0, cleanTurns = 0;
var results = new List<object>();
void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
void Test(string label, Action run) { if (args[1] == "result" && !label.StartsWith("Endure result-message", StringComparison.Ordinal)) return; groups++; try { run(); results.Add(new { label, passed = true }); Console.WriteLine("PASS " + label); } catch (Exception ex) { failures++; results.Add(new { label, passed = false, error = ex.ToString() }); Console.WriteLine("FAIL " + label + " " + ex); } }
WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon, m.HeldItem, WildBattleTransformationRules.PersistentStatus(m));
SourceBattleFrame Frame(WildBattleCombatant[]? supplied = null, uint seed = 31) {
    var m = supplied ?? [Mon("ELECTRODE", 100, "PROTECT"), Mon("SPEAROW", 50, "DETECT"), Mon("PIDGEY", 30, "ENDURE"), Mon("SNORLAX", 5, "PROTECT")];
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
    return new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, pos, target);
}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r) { Check(r.Succeeded,r.Fault ?? "accepted"); Check(SourceBattleIdentityRules.Validate(r.State) is null,"valid output"); return r; }
SourceFourActionResult Begin(SourceBattleFrame f,int[]? p=null,int[]? t=null) => Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f,p,t)));
SourceFourActionResult Next(SourceFourActionResult r) => Ok(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
(SourceBattleFrame State,List<SourceBattleEvent> Events,List<RandomTrace> Trace) Finish(SourceFourActionResult r) {
    var events=r.Events.ToList(); var trace=r.Trace.ToList(); int n=0;
    while(r.State.FourTurn is not null && r.State.Outcome == WildBattleOutcome.Ongoing) {
        Check(n++<30,"bounded continuation");
        if(r.State.FourTurn.Phase == SourceFourActionPhase.ReplacementsPending) {
            var p=r.State.PendingSwitches[0];r=Ok(SourceFourActionTurns.ChooseReplacement(catalog,r.State,r.Continuation!.Value,p.Slot,new(p.Slot.Side,p.LegalPartyIndices[0])));
        } else {
            var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!; var actual=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);
            var replay=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);
            Check(Json(actual)==Json(replay),"exact pause JSON state/events/RNG replay");r=Ok(actual);
        }
        events.AddRange(r.Events);trace.AddRange(r.Trace);
    }
    if(r.CompletedCleanTurn)cleanTurns++;return(r.State,events,trace);
}
void Atomic(SourceBattleFrame f, SourceFourActionResult r,string why) => Check(!r.Succeeded && ReferenceEquals(f,r.State) && r.Trace.IsEmpty && r.Events.IsEmpty,why+" atomic: "+r.Fault);
Test("224 original C vectors: inclusive thresholds, shared family history and all four action positions",()=>{
    Check(Environment.Version.Major==10,"runtime10");Check(source.RootElement.GetProperty("sourceCommit").GetString()==PokemonFactory.SourceCommit,"pin");
    string Name(int n)=>n switch{182=>"PROTECT",197=>"DETECT",203=>"ENDURE",_=>"POUND"};
    foreach(var v in source.RootElement.GetProperty("vectors").EnumerateArray()) {
        int index=v.GetProperty("index").GetInt32();var f=Frame();var b=f.Slots[index].Body!;
        b=b with{ProtectConsecutiveUses=v.GetProperty("before").GetInt32(),LastUsedMove=Id(Name(v.GetProperty("previous").GetInt32())),Pokemon=b.Pokemon with{Moves=[new(Id(Name(v.GetProperty("move").GetInt32())),5,5)]}};
        // Max PP stays the authenticated move's actual base value.
        b=b with{Pokemon=b.Pokemon with{Moves=[new(b.Pokemon.Moves[0].Move,catalog.Moves[b.Pokemon.Moves[0].Move].Pp,catalog.Moves[b.Pokemon.Moves[0].Move].Pp)]}};
        var r=Begin(Put(f,index,b));Check(r.State.Queue.Select(a=>a.Slot.Value).SequenceEqual(new[]{0,1,2,3}),"real sorted four-position order");
        for(int i=0;i<index;i++)r=Next(r);
        r=new(r.State with{Rng=new(v.GetProperty("seed").GetUInt32())},[],[],null);r=Next(r);
        var e=r.Events.OfType<SourceProtectAttemptEvent>().Single();var after=r.State.Slots[index].Body!;
        Check(e.Roll==v.GetProperty("roll").GetInt32()&&e.After==v.GetProperty("after").GetInt32(),"C roll and counter");
        Check(after.Protected==(v.GetProperty("protected").GetInt32()!=0)&&after.Enduring==(v.GetProperty("enduring").GetInt32()!=0),"C flags");
        Check(e.Succeeded==(v.GetProperty("missed").GetInt32()==0)&&e.NotLastAction==(index!=3),"C action-index predicate");
        Check(r.Trace.Length==1&&r.State.Rng.Seed==v.GetProperty("seedAfter").GetUInt32(),"one exact source draw, including last action failure");
    }
});
Test("36 verbatim C normal-damage vectors agree with the shared Endure adjustment",()=>{
    var method=typeof(SourceBattleKernel).Assembly.GetType("Game.Domain.Battle.SourceHeldDamageRules")!.GetMethod("Adjust",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
    foreach(var v in source.RootElement.GetProperty("damageVectors").EnumerateArray()) {
        uint seed=v.GetProperty("seed").GetUInt32();uint after=unchecked(seed*1103515245u+24691u);int raw=(int)(after>>16);
        int damage=Math.Max(1,v.GetProperty("damage").GetInt32()*(100-raw%16)/100);
        object adjusted=method.Invoke(null,[v.GetProperty("hp").GetInt32(),damage,false,v.GetProperty("endure").GetInt32()!=0,false,false])!;
        int actual=(int)adjusted.GetType().GetProperty("Damage")!.GetValue(adjusted)!;bool endured=(bool)adjusted.GetType().GetProperty("Endured")!.GetValue(adjusted)!;
        Check(actual==v.GetProperty("damageAfter").GetInt32()&&endured==(v.GetProperty("endured").GetInt32()!=0),"extracted C normal adjustment");
        Check(after==v.GetProperty("seedAfter").GetUInt32()&&v.GetProperty("draws").GetInt32()==1,"source variance draw");
    }
});
Test("approved fifth-plus clamp and byte saturation; interruption resets on the next attempt",()=>{
    foreach(int before in new[]{4,5,254,255})foreach(uint seed in new uint[]{0,1}){
        var f=Frame();f=Put(f,0,f.Slots[0].Body! with{ProtectConsecutiveUses=before,LastUsedMove=Id("ENDURE")});var r=Begin(f);r=new(r.State with{Rng=new(seed)},[],[],null);r=Next(r);var e=r.Events.OfType<SourceProtectAttemptEvent>().Single();
        Check(e.Threshold==8191&&e.After==(e.Succeeded?Math.Min(255,before+1):0),"approved bounded 12.5 percent");
    }
    var x=Frame([Mon("ELECTRODE",100,"PROTECT","DETECT","ENDURE","DEFENSE_CURL"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("PIDGEY",30,"DEFENSE_CURL"),Mon("SPEAROW",50,"DEFENSE_CURL")]);
    for(int turn=0;turn<5;turn++) {var r=Begin(x,[turn%3,0,0,0]);r=new(r.State with{Rng=new(0)},[],[],null);r=Next(r);Check(r.Events.OfType<SourceProtectAttemptEvent>().Single().Before==turn,"family shares consecutive byte");x=Finish(r).State;Check(x.Slots.All(c=>!c.Body!.Protected&&!c.Body.Enduring),"full cleanup clears flags");}
    x=Finish(Begin(x,[3,0,0,0])).State;Check(x.Slots[0].Body!.ProtectConsecutiveUses==5,"source counter byte retained until next setprotectlike");
    var reset=Next(Begin(x));Check(reset.Events.OfType<SourceProtectAttemptEvent>().Single().Before==0&&reset.State.Slots[0].Body!.ProtectConsecutiveUses==1,"intervening resulting move resets probability");
});
Test("no PP, stale revision, stale continuation and malformed flags reject atomically with callers admitted",()=>{
    var f=Frame();var b=f.Slots[0].Body!;var empty=Put(f,0,b with{Pokemon=b.Pokemon with{Moves=[b.Pokemon.Moves[0] with{CurrentPp=0}]}});
    Atomic(empty,SourceFourActionTurns.BeginActions(catalog,empty,empty.Revision,Choices(empty)),"selection no PP");
    Atomic(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision+1,Choices(f)),"stale revision");
    foreach(var bad in new[]{b with{ProtectConsecutiveUses=-1},b with{ProtectConsecutiveUses=256},b with{Protected=true,Enduring=true}}){var q=Put(f,0,bad);Atomic(q,SourceFourActionTurns.BeginActions(catalog,q,q.Revision,Choices(q)),"malformed protection");}
    foreach(string move in new[]{"METRONOME","SLEEP_TALK","ASSIST","MIRROR_MOVE"})Check(SourceBattleKernel.SupportsRecipe(catalog.Moves[Id(move)]),"authenticated caller admitted "+move);
    var r=Begin(f);var token=r.Continuation!.Value;r=Next(r);Atomic(r.State,SourceFourActionTurns.Advance(catalog,r.State,token),"stale action continuation");
    var standalone=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[0].Occupant!.Value,f.Revision,0,new(0)));Check(!standalone.Succeeded&&ReferenceEquals(standalone.State,f)&&standalone.Events.IsEmpty&&standalone.Trace.IsEmpty,"Protect needs real order");
    var noPp=Begin(f);var owner=noPp.State.Slots[0].Body!;noPp=new(Put(noPp.State,0,owner with{Pokemon=owner.Pokemon with{Moves=[owner.Pokemon.Moves[0] with{CurrentPp=0}]}}),[],[],null);noPp=Next(noPp);
    Check(noPp.Events.OfType<SourceMoveFailedEvent>().Single().Reason=="no-pp"&&!noPp.Events.OfType<SourceProtectAttemptEvent>().Any()&&noPp.Trace.IsEmpty,"PP exhausted after selection spends no protection RNG");
    var overflow=Begin(f);overflow=new(overflow.State with{Rng=new(0,ulong.MaxValue)},[],[],null);Atomic(overflow.State,SourceFourActionTurns.Advance(catalog,overflow.State,overflow.Continuation!.Value),"RNG overflow");
});
Test("four-slot last-action rule counts cancelled pending positions and action failure leaves history for reset",()=>{
    var f=Frame();var r=Begin(f);
    // Actual current owner queue retains four positions. Cancelled entries are still source positions.
    r=new(r.State with{Queue=r.State.Queue.Select((q,i)=>i>0?q with{Kind=SourceActionKind.Cancelled,CancellationReason="test-cancelled"}:q).ToImmutableArray()},[],[],null);
    r=Next(r);Check(r.Events.OfType<SourceProtectAttemptEvent>().Single().Succeeded,"not last numeric position despite no living pending move");var done=Finish(r);Check(done.State.FourTurn is null,"cancelled positions finish once");
    var vacant=Frame();vacant=Put(vacant,3,vacant.Slots[3].Body! with{Pokemon=vacant.Slots[3].Body!.Pokemon with{CurrentHp=0}});vacant=vacant with{SourceAbsentMask=8,TurnStartAbsentMask=8};
    var actual=Begin(vacant);actual=Next(actual);actual=Next(actual);actual=Next(actual);
    Check(actual.Events.OfType<SourceProtectAttemptEvent>().Single() is {Side.Value:2,Succeeded:true}&&actual.State.Queue[3].Kind==SourceActionKind.Cancelled,"real absent source position remains pending after both other living actions");Finish(actual);
    f=Frame();f=Put(f,0,f.Slots[0].Body! with{Flinched=true,ProtectConsecutiveUses=3,LastUsedMove=Id("PROTECT")});r=Next(Begin(f));Check(!r.Events.OfType<SourceProtectAttemptEvent>().Any()&&!r.Events.OfType<SourcePpCommandEvent>().Any(),"canceller before PP and protection RNG");
    var clean=Finish(r).State;var next=Next(Begin(clean));Check(next.Events.OfType<SourceProtectAttemptEvent>().Single().Before==0,"failed action history resets next family attempt");
});
Test("spread Protect keeps each target script PP and accuracy; protected contact skips ability RNG",()=>{
    foreach(int protectedSlot in new[]{1,3}){
        var bodies=new[]{Mon("ELECTRODE",60,"SURF"),Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("PIDGEY",30,"DEFENSE_CURL"),Mon("SNORLAX",90,"DEFENSE_CURL")};
        bodies[protectedSlot]=Mon("SNORLAX",protectedSlot==1?100:90,"DETECT");var r=Begin(Frame(bodies));while(r.State.Queue[r.State.NextAction].Slot.Value!=0)r=Next(r);r=Next(r);
        Check(r.Events.OfType<SourceHpChangedEvent>().Single().Target.Value==(protectedSlot^2),"only unprotected spread recipient loses HP");
        Check(r.Events.OfType<SourceMoveFailedEvent>().Single(e=>e.Reason=="protected").Target.Value==protectedSlot,"protected result on correct position");
        var pp=r.Events.OfType<SourcePpCommandEvent>().ToArray();Check(pp.Length==2&&pp[0].Before-pp[0].After==1&&pp[1].Before==pp[1].After,"source second-target noattackstring suppresses second PP");
        Check(r.Trace.Count(t=>t.Purpose.EndsWith(".accuracy"))==1,"one unprotected accuracy draw");
        Check(r.State.Slots[protectedSlot].Registers.History!.LastHitByType=="TYPE_NORMAL"&&r.State.Slots[protectedSlot].Body!.LastLandedMove==Id("UNAVAILABLE"),"Protect source last-hit type clears before move-end failed landed value");
    }
    var contact=Begin(Frame([Mon("RATTATA",50,"POUND"),Mon("CLEFAIRY",30,"PROTECT"),Mon("PIDGEY",30,"DEFENSE_CURL"),Mon("SNORLAX",90,"DEFENSE_CURL")]));while(contact.State.Queue[contact.State.NextAction].Slot.Value!=0)contact=Next(contact);contact=Next(contact);
    Check(contact.Trace.IsEmpty&&!contact.Events.OfType<SourceHpChangedEvent>().Any(),"Protect bypasses accuracy/critical/contact rolls");
});
Test("Endure survives direct and multihit damage including one HP contact; residual and delayed damage still KO",()=>{
    foreach(string hit in new[]{"POUND","DOUBLE_SLAP"})foreach(int hp in new[]{1,2}){
        var target=Mon("CLEFAIRY",30,"ENDURE");target=target with{Pokemon=target.Pokemon with{CurrentHp=hp}};
        var r=Begin(Frame([Mon("RATTATA",100,hit) with{AccuracyStage=12},target,Mon("PIDGEY",30,"DEFENSE_CURL"),Mon("SNORLAX",90,"DEFENSE_CURL")]));while(r.State.Queue[r.State.NextAction].Slot.Value!=0)r=Next(r);r=Next(r);
        Check(r.State.Slots[1].Body!.Pokemon.CurrentHp==1&&r.Events.OfType<SourceDamageAdjustedEvent>().Single().Endured,"zero-damage Endure adjustment");
        Check(r.Trace.Any(t=>t.Purpose.EndsWith(".cute-charm"))==(hp>1),"source contact requires actual HP loss; Endure zero alone does not proc");
        if(hit=="DOUBLE_SLAP")Check(r.Events.OfType<SourceMultiHitEvent>().Single().ActualHits==1,"source stops multihit after endured");
    }
    foreach(string passive in new[]{"poison","burn","sandstorm","future"}){
        var a=Mon("PIDGEY",30,"ENDURE");a=a with{Pokemon=a.Pokemon with{CurrentHp=1},Status=passive=="poison"?WildBattleStatus.Poison:passive=="burn"?WildBattleStatus.Burn:0};
        var f=Frame([a,Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SPEAROW",50,"DEFENSE_CURL"),Mon("RATTATA",70,"DEFENSE_CURL")]);
        if(passive=="sandstorm")f=f with{Field=new(){SandstormTurnsRemaining=3}};
        if(passive=="future")f=f with{Slots=f.Slots.SetItem(0,f.Slots[0] with{Position=new(){FutureAttack=new(1,Id("FUTURE_SIGHT"),new(1),1000)}})};
        var done=Finish(Begin(f));Check(done.State.Slots[0].Body!.Pokemon.CurrentHp==0,"Endure does not block "+passive);Check(done.Events.OfType<SourceFaintedEvent>().Count(e=>e.Side.Value==0)==1,"single passive faint "+passive);
    }
});
Test("protected primary status and no-Protect-flag scripts retain their source decisions",()=>{
    var r=Begin(Frame([Mon("ELECTRODE",100,"POISON_POWDER"),Mon("SNORLAX",100,"PROTECT"),Mon("PIDGEY",30,"DEFENSE_CURL"),Mon("SPEAROW",50,"DEFENSE_CURL")]));r=Next(r);r=Next(r);
    Check(r.State.Slots[1].Body!.Status==0&&r.Trace.IsEmpty&&r.Events.OfType<SourcePpCommandEvent>().Single().Before-r.Events.OfType<SourcePpCommandEvent>().Single().After==1,"primary status protected before manual accuracy");
    var f=Frame([Mon("ELECTRODE",100,"PSYCH_UP"),Mon("SNORLAX",100,"PROTECT") with{DefenseStage=10},Mon("PIDGEY",30,"DEFENSE_CURL"),Mon("SPEAROW",50,"DEFENSE_CURL")]);
    Check(!catalog.Moves[Id("PSYCH_UP")].Flags.Contains("FLAG_PROTECT_AFFECTED"),"source metadata bypass");r=Begin(f);r=Next(r);r=Next(r);Check(r.State.Slots[0].Body!.DefenseStage==10,"unaffected move still applies through Protect");
});
Test("Explosion computes protected target damage before accuracy and still self-faints",()=>{
    var f=Frame([Mon("PIDGEY",5,"SELF_DESTRUCT"),Mon("SNORLAX",100,"ENDURE"),Mon("SPEAROW",50,"PROTECT"),Mon("RATTATA",100,"DEFENSE_CURL")]);
    var r=Begin(f);while(r.State.Queue[r.State.NextAction].Slot.Value!=0)r=Next(r);r=Next(r);
    Check(r.Events.OfType<SourceSelfDestructEvent>().Count()==1&&r.State.Slots[0].Body!.Pokemon.CurrentHp==0,"self-faint remains unconditional");
    Check(r.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Target.Value==2&&e.Reason=="protected")&&!r.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Target.Value==2),"protected ally keeps HP");
    Check(r.Trace.Count(t=>t.Purpose.EndsWith(".damage"))==3&&r.Trace.Count(t=>t.Purpose.EndsWith(".accuracy"))==2,"source numeric target critical/damage before protected check");
});
Test("Endure residual KO traverses actual rewards and reserve entry once before ready",()=>{
    var victim=Mon("PIDGEY",20,"ENDURE");victim=victim with{Pokemon=victim.Pokemon with{CurrentHp=1},Status=WildBattleStatus.Poison};
    var f=Frame([Mon("ELECTRODE",99,"DEFENSE_CURL"),victim,Mon("SPEAROW",98,"DEFENSE_CURL"),Mon("RATTATA",70,"DEFENSE_CURL")]);
    f=f with{Parties=f.Parties.SetItem(1,f.Parties[1] with{Members=f.Parties[1].Members.Add(Member(Mon("SNORLAX",100,"DEFENSE_CURL")))}),Field=new(){RainTurnsRemaining=3},
        Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
    var done=Finish(Begin(f));Check(done.State.FourTurn is null&&done.State.Slots[1].Member!.Value.Index==2,"genuine reserve installed");
    Check(done.State.Rewards!.CompletedOpponentMask==1&&done.Events.OfType<SourceRewardAppliedEvent>().Count()==2,"one defeated occupant shared rewards");
    Check(done.State.Field.RainTurnsRemaining==2&&done.Events.OfType<SourceStatusDamageEvent>().Count()==1&&done.Events.OfType<SourceTurnReadyEvent>().Count()==1,"no replay across pauses");
    Check(done.State.Slots[1].Body!.ProtectConsecutiveUses==0&&!done.State.Slots[1].Body!.Enduring,"entry does not inherit protection");
});
Test("Endure result-message follows real HP/critical commands, type flags and multihit; Explosion miss stays silent",()=>{
    foreach(var test in new[]{("POUND",1,"CLEFAIRY"),("POUND",2,"CLEFAIRY"),("ROCK_THROW",2,"PIDGEY"),("DOUBLE_SLAP",2,"CLEFAIRY")}) {
        var target=Mon(test.Item3,30,"ENDURE");target=target with{Pokemon=target.Pokemon with{CurrentHp=test.Item2}};
        var r=Begin(Frame([Mon("RATTATA",100,test.Item1) with{AccuracyStage=12},target,Mon("PIDGEY",30,"DEFENSE_CURL"),Mon("SNORLAX",90,"DEFENSE_CURL")]));
        while(r.State.Queue[r.State.NextAction].Slot.Value!=0)r=Next(r);r=Next(r);
        var message=r.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="endure"&&e.Reason=="endured");
        var hp=r.Events.OfType<SourceHpChangedEvent>().Last();Check(message.Side.Value==1&&message.Sequence>hp.Sequence,"result after actual HP command, including zero damage");
        Check(r.Events.OfType<SourceCriticalHitEvent>().All(e=>e.Sequence<message.Sequence),"result after critical text");
        if(test.Item1=="ROCK_THROW")Check(r.Events.OfType<SourceEffectivenessEvent>().Single() is {Effectiveness:WildBattleEffectiveness.SuperEffective} type&&type.Sequence>message.Sequence,"source reentered resultmessage prints type after Endure");
        if(test.Item1=="DOUBLE_SLAP")Check(r.Events.OfType<SourceMultiHitEvent>().Single().Sequence>message.Sequence,"one Endure result precedes source hit-count text");
    }
    var endurer=Mon("PIDGEY",30,"ENDURE");endurer=endurer with{Pokemon=endurer.Pokemon with{CurrentHp=1}};
    var ready=Begin(Frame([Mon("RATTATA",100,"SELF_DESTRUCT") with{AccuracyStage=0},endurer,Mon("SNORLAX",100,"DEFENSE_CURL"),Mon("SPEAROW",90,"DEFENSE_CURL")]));
    while(ready.State.Queue[ready.State.NextAction].Slot.Value!=0)ready=Next(ready);
    bool checkedMiss=false;
    for(uint seed=0;seed<16&&!checkedMiss;seed++) {
        var r=Next(new(ready.State with{Rng=new(seed)},[],[],null));
        if(!r.Events.OfType<SourceMoveMissedEvent>().Any(e=>e.Target.Value==1))continue;
        Check(r.Events.OfType<SourceDamageAdjustedEvent>().Any(e=>e.Target.Value==1&&e.Endured),"Explosion really adjusted before the later miss");
        Check(!r.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Target.Value==1)&&!r.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Side.Value==1&&e.Volatile=="endure"&&e.Reason=="endured"),"missed Explosion emits no fake endured-hit text");checkedMiss=true;
    }
    Check(checkedMiss,"exercised actual Explosion miss");
    var struggler=Mon("RATTATA",100,"POUND");struggler=struggler with{Pokemon=struggler.Pokemon with{Moves=[struggler.Pokemon.Moves[0] with{CurrentPp=0}]}};
    var f=Frame([struggler,endurer,Mon("SNORLAX",5,"DEFENSE_CURL"),endurer]);
    var choices=Choices(f).Select(a=>a.Actor.Slot.Value==0?a with{MoveSlot=WildBattleRules.StruggleSlot,Target=new(1)}:a).ToImmutableArray();
    var forced=Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices));while(forced.State.Queue[forced.State.NextAction].Slot.Value!=0)forced=Next(forced);forced=Next(forced);
    var text=forced.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="endure"&&e.Reason=="endured");
    Check(text.Sequence>forced.Events.OfType<SourceHpChangedEvent>().Single().Sequence&&text.Sequence<forced.Events.OfType<SourceRecoilEvent>().Single().Sequence,"Struggle result precedes source recoil");
});
File.WriteAllText(args[2],JsonSerializer.Serialize(new{groups,checks,failures,cleanTurns,results},new JsonSerializerOptions{WriteIndented=true})+"\n");
Console.WriteLine($"{groups} groups, {checks} checks, {cleanTurns} complete turns, {failures} failures");
if(groups==0||checks==0||failures!=0)Environment.Exit(1);
