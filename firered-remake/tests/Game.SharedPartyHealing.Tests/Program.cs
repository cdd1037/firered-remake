using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
if(args.Length<2)throw new ArgumentException("CONTENT_ROOT OUTPUT [--capture]");
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]);var catalog=proof.Catalog;
string Json<T>(T x)=>JsonSerializer.Serialize(x);
string Hash(string x)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(x)));
MoveId Id(string s)=>new("pret:MOVE_"+s);
WildBattleCombatant Mon(string species,string moves="DEFENSE_CURL",uint status=0,uint pid=0){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),70,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
 return new(p with{Moves=moves.Split(',').Select(s=>new PokemonMoveSlot(Id(s),catalog.Moves[Id(s)].Pp,catalog.Moves[Id(s)].Pp)).ToImmutableArray()}){Status=status};
}
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(WildBattleTransformationRules.PersistentPokemon(m),m.HeldItem,WildBattleTransformationRules.PersistentStatus(m));
var original=PretRocketHideoutPokemon.Load(args[0]).Catalog;
var supplement=PretVermilionGymTrainers.ExtendPokemon(args[0],PretCeladonApproachPokemon.Load(args[0]).Catalog,celadonApproachRules:true);
var prior=original with{Moves=original.Moves.SetItems(supplement.Moves)};
var old=PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior,args[0],args[0]).Catalog,args[0]).Catalog,args[0]).Catalog;
var legacy=PretMetronomeMoveCatalog.Load(args[0]).Extend(PretFuchsiaRoutePokemon.Load(old,args[0]).Catalog);
var snapshots=new List<object>();
foreach(bool canonical in new[]{false,true})foreach(string move in new[]{"HEAL_BELL","AROMATHERAPY"})foreach(bool reverse in new[]{false,true})foreach(bool blocked in new[]{false,true}){
 var actor=Mon("CLEFAIRY",move,WildBattleStatus.Burn) with{CalledOverlay=blocked?new(){Ability="ABILITY_SOUNDPROOF"}:null};
 var other=Mon("RATTATA","TACKLE",WildBattleStatus.Poison);var player=reverse?other:actor;var opponent=reverse?actor:other;
 var members=ImmutableArray.Create(Member(actor),Member(Mon("VOLTORB","SCREECH",WildBattleStatus.Paralysis)),Member(Mon("RATTATA","TACKLE",WildBattleStatus.Poison)) with{IsEgg=true},Member(Mon("RATTATA","TACKLE",WildBattleStatus.Burn)) with{Pokemon=Mon("RATTATA","TACKLE").Pokemon with{CurrentHp=0}});
 var state=new WildBattleState(player,opponent,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=canonical?BattleRulesProfile.CanonicalSourceSinglesVersion:null,
 CalledParty=new(reverse?[Member(player)]:members,reverse?members:[Member(opponent)],0,0,WildBattleCalledBattleKind.Trainer)};
 var r=WildBattleRules.ResolveSourceMoveAction(canonical?catalog:legacy,state,new(reverse?WildBattleSide.Opponent:WildBattleSide.Player,0));
 if(!r.Succeeded)throw new Exception($"baseline {canonical}/{move}/{reverse}/{blocked}: "+r.Fault);
 snapshots.Add(new{canonical,move,reverse,blocked,input=state,result=r,sha256=Hash(Json(r))});
}
if(args.Contains("--capture")){
 if(File.Exists(args[1]))throw new Exception("Refuse baseline overwrite");
 if(typeof(WildBattleRules).Assembly.GetType("Game.Domain.Battle.SourceRecipeContext")!.GetMethod("ExecutePartyHealing",BindingFlags.Instance|BindingFlags.NonPublic)is not null)throw new Exception("Capture requires unchanged 294eb6c product");
 File.WriteAllText(args[1],Json(new{baseCommit="294eb6c9c7d4b41d8733acc26cbe89a74d2325d7",snapshots}));Console.WriteLine("Captured "+snapshots.Count+" immutable serialized singles cases");return 0;
}
using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"singles-before-294eb6c.json")));
if(Json(snapshots)!=baseline.RootElement.GetProperty("snapshots").GetRawText())throw new Exception("Immutable serialized singles cases differ");
Console.WriteLine("PASS 16 immutable canonical/historical singles results");
int groups=0,checks=0,failures=0,turns=0;var rows=new List<object>();
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Test(string name,Action test){groups++;try{test();Console.WriteLine("PASS "+name);rows.Add(new{name,passed=true});}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+" "+e);rows.Add(new{name,passed=false,error=e.ToString()});}}
SourceBattleFrame Frame(int actor,string move){
 var mons=Enumerable.Range(0,4).Select(i=>Mon("RATTATA","TAIL_WHIP")).ToArray();
 mons[actor]=Mon(move=="HEAL_BELL"?"CHIMECHO":"VILEPLUME",move,WildBattleStatus.Burn) with{SpeedStage=12};
 mons[actor^2]=Mon("RATTATA","TAIL_WHIP",2) with{Nightmare=true};
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
 BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),Parties=[new(BattleSide.Player,[Member(mons[0]),Member(mons[2])]),new(BattleSide.Opponent,[Member(mons[1]),Member(mons[3])])],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,mons[i])).ToImmutableArray()};
}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant body){var cell=f.Slots[slot];var key=cell.Member!.Value;var party=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(slot,cell with{Body=body}),Parties=f.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,Member(body))})};}
SourceBattleFrame Reserve(SourceBattleFrame f,BattleSide side,params WildBattleCalledPartyMember[] members){var p=f.Party(side);return f with{Parties=f.Parties.SetItem((int)side,p with{Members=p.Members.AddRange(members)})};}
SourceBattleFrame Reserves(SourceBattleFrame f,BattleSide side)=>Reserve(f,side,Member(Mon("RATTATA","TAIL_WHIP",WildBattleStatus.Poison)),Member(Mon("VOLTORB","SCREECH",WildBattleStatus.Poison)),Member(Mon("RATTATA","TAIL_WHIP",WildBattleStatus.Burn)) with{IsEgg=true},Member(Mon("RATTATA","TAIL_WHIP",WildBattleStatus.Burn)) with{Pokemon=Mon("RATTATA","TAIL_WHIP").Pokemon with{CurrentHp=0}});
SourceKernelResult Action(SourceBattleFrame f,int actor,int chosen=0,int? target=null){var move=catalog.Moves[f.Slots[actor].Body!.Pokemon.Moves[chosen].Move];var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,chosen,new(target??(move.Target=="MOVE_TARGET_USER"?actor:actor^1))));Check(r.Succeeded,"action "+r.Fault);Check(SourceBattleIdentityRules.Validate(r.State)==null,"identity "+SourceBattleIdentityRules.Validate(r.State));return r;}
SourceFourActionResult Good(SourceFourActionResult r){Check(r.Succeeded,"four action "+r.Fault);Check(SourceBattleIdentityRules.Validate(r.State)==null,"valid actual parties");return r;}
SourceBattleFrame Entry(SourceBattleFrame f)=>Good(SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision)).State;
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int actor,int chosen=0)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{int n=c.Slot.Value==actor?chosen:0;var m=catalog.Moves[c.Body!.Pokemon.Moves[n].Move];return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,n,new(m.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:c.Slot.Value^1));}).ToImmutableArray();
SourceFourActionResult Next(SourceFourActionResult r){var copy=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var a=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var b=SourceFourActionTurns.Advance(catalog,copy,r.Continuation.Value);Check(Json(a)==Json(b),"exact serialized continuation result/events/RNG");return Good(a);}
SourceFourActionResult Begin(SourceBattleFrame f,int actor,int chosen=0,ImmutableArray<SourceActionSelection> custom=default)=>Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,custom.IsDefault?Choices(f,actor,chosen):custom));
SourceFourActionResult Finish(SourceFourActionResult r){int count=0;while(r.State.FourTurn!=null){Check(++count<20,"bounded turn");r=Next(r);}Check(r.CompletedCleanTurn,"complete turn");turns++;return r;}
Test("both party cures use each actual actor, active partner and six real members",()=>{
 foreach(string move in new[]{"HEAL_BELL","AROMATHERAPY"})foreach(int actor in Enumerable.Range(0,4)){
  var side=(BattleSide)(actor&1);var f=Reserves(Frame(actor,move),side);var r=Action(f,actor);var party=r.State.Party(side).Members;
  Check(r.Trace.IsEmpty,"no accuracy/party RNG");Check(r.State.Slots[actor].Body!.Status==0&&r.State.Slots[actor^2].Body!.Status==0&&!r.State.Slots[actor^2].Body!.Nightmare,"both live statuses and Nightmare cured");
  Check(party[0].Status==0&&party[1].Status==0&&party[2].Status==0&&party[5].Status==0&&party[5].Pokemon.CurrentHp==0,"real active, reserve and fainted statuses");
  Check(party[3].Status==(move=="HEAL_BELL"?WildBattleStatus.Poison:0)&&party[4].Status==(move=="HEAL_BELL"?WildBattleStatus.Burn:0),"source Soundproof and egg distinction");
  Check(Json(f.Party((BattleSide)(1-(actor&1))))==Json(r.State.Party((BattleSide)(1-(actor&1)))),"opposite party unchanged");
  Check(r.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp==f.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp-1,"only actor PP");
 }
});
Test("Soundproof checks both active slots, while all-healthy actions still spend one PP",()=>{
 foreach(string move in new[]{"HEAL_BELL","AROMATHERAPY"}){
  var f=Frame(2,move);f=Put(f,2,Mon("VOLTORB",move,WildBattleStatus.Burn));f=Put(f,0,Mon("VOLTORB","SCREECH",WildBattleStatus.Burn));f=Put(f,1,Mon("DUSCLOPS","LEER"));f=Put(f,3,Mon("DUSCLOPS","LEER"));
  var r=Action(f,2);Check(r.Events.OfType<SourceAbilityPreventedEvent>().Count()==(move=="HEAL_BELL"?2:0),"two independently blocked active bodies");
  Check(r.State.Slots[2].Body!.Status==(move=="HEAL_BELL"?WildBattleStatus.Burn:0)&&r.State.Slots[0].Body!.Status==(move=="HEAL_BELL"?WildBattleStatus.Burn:0),"live cure distinction");
  Check(r.State.Slots[2].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id(move)].Pp-1,"self party cure unaffected by two foe Pressure");
  var clean=Frame(0,move);clean=Put(clean,0,clean.Slots[0].Body! with{Status=0});clean=Put(clean,2,clean.Slots[2].Body! with{Status=0,Nightmare=false});var noEffect=Action(clean,0);
  Check(!noEffect.Events.OfType<SourceMoveFailedEvent>().Any()&&noEffect.Trace.IsEmpty,"healthy party is successful without RNG");Finish(Begin(clean,0));
 }
});
Test("real Trace, Heal Bell, switch out, bench cure and switch back preserve permanent ability",()=>{
 var f=Frame(0,"HEAL_BELL");f=Put(f,2,Mon("PORYGON","RECOVER",WildBattleStatus.Burn));f=Put(f,1,Mon("VOLTORB","SCREECH"));f=Put(f,3,Mon("VOLTORB","SCREECH"));f=Reserve(f,BattleSide.Player,Member(Mon("RATTATA","TAIL_WHIP")));f=Entry(f);
 Check(WildBattleEffective.Ability(f.Slots[2].Body!)=="ABILITY_SOUNDPROOF"&&f.Party(BattleSide.Player).Members[1].Pokemon.AbilityId=="ABILITY_TRACE","actual Trace with permanent identity");
 var first=Next(Begin(f,0));Check(first.State.Slots[2].Body!.Status==WildBattleStatus.Burn,"copied active Soundproof blocks");f=Finish(first).State;
 var select=Choices(f,0).SetItem(2,new(f.Slots[2].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,2)));f=Finish(Begin(f,0,custom:select)).State;
 Check(f.Party(BattleSide.Player).Members[1].Status==0&&f.Party(BattleSide.Player).Members[1].Pokemon.AbilityId=="ABILITY_TRACE","bench uses permanent Trace, no stale copied Soundproof");
 select=Choices(f,0).SetItem(2,new(f.Slots[2].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Player,1)));f=Finish(Begin(f,0,custom:select)).State;
 Check(f.Slots[2].Member==new PartyMemberKey(BattleSide.Player,1)&&f.Slots[2].Body!.Status==0&&WildBattleEffective.Ability(f.Slots[2].Body!)=="ABILITY_SOUNDPROOF","same healed actual member reenters and traces again");
});
Test("actual Transform retains Ditto party identity through blocked bell and active Aromatherapy",()=>{
 var f=Frame(0,"HEAL_BELL");f=Put(f,0,Mon("CHIMECHO","HEAL_BELL,AROMATHERAPY") with{SpeedStage=12});f=Put(f,2,Mon("DITTO","TRANSFORM",WildBattleStatus.Burn));f=Put(f,1,Mon("VOLTORB","SCREECH"));
 f=Action(f,2,target:1).State;Check(f.Slots[2].Body!.Transform!=null,"real Transform executed");
 var first=Next(Begin(f,0));Check(first.State.Slots[2].Body!.Status==WildBattleStatus.Burn,"copied Soundproof blocks Heal Bell");f=Finish(first).State;
 var aroma=Next(Begin(f,0,1));Check(aroma.State.Slots[2].Body!.Status==0&&aroma.State.Party(BattleSide.Player).Members[1].Status==0,"actual cured active projected to party");
 Check(aroma.State.Party(BattleSide.Player).Members[1].Pokemon.Species.Value=="pret:SPECIES_DITTO"&&aroma.State.Party(BattleSide.Player).Members[1].Pokemon.AbilityId=="ABILITY_LIMBER","no transformed species or copied ability in permanent member");Finish(aroma);
});
Test("absent copied partner uses permanent ability for party cure and preserves its live status",()=>{
 foreach(string move in new[]{"HEAL_BELL","AROMATHERAPY"}){
  var f=Frame(0,move);f=Put(f,2,Mon("PORYGON","RECOVER",WildBattleStatus.Burn));f=Put(f,1,Mon("VOLTORB","SCREECH"));f=Put(f,3,Mon("VOLTORB","SCREECH"));f=Entry(f);f=f with{SourceAbsentMask=4,TurnStartAbsentMask=4};
  var r=Next(Begin(f,0));Check(r.State.Slots[2].Body!.Status==WildBattleStatus.Burn&&r.State.Slots[2].Body!.SourcePartyStatusOverride==0,"absent live status retained with explicit permanent cure");
  Check(r.State.Party(BattleSide.Player).Members[1].Status==0&&!r.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Target.Value==2),"permanent Trace used instead of stale copied Soundproof");
  var done=Finish(r);Check(done.State.Party(BattleSide.Player).Members[1].Status==0&&done.State.Slots[2].Body!.Status==WildBattleStatus.Burn,"continuations cannot overwrite permanent cure");
 }
});
Test("partner cure happens before its queued sleep canceller and all four real actions finish",()=>{
 var f=Frame(0,"HEAL_BELL");var r=Next(Begin(f,0));Check(r.State.Slots[2].Body!.Status==0&&!r.State.Slots[2].Body!.Nightmare,"sleep/nightmare gone before partner action");
 var events=r.Events.ToBuilder();while(r.State.FourTurn!=null){r=Next(r);events.AddRange(r.Events);}turns++;Check(events.OfType<SourceMoveUsedEvent>().Select(e=>e.Actor.Value).Distinct().Count()==4,"four actual occupants acted");
});
Test("canonical called cures retain caller PP, nonzero real party index and sleep-status projection",()=>{
 foreach(string move in new[]{"HEAL_BELL","AROMATHERAPY"})foreach(bool reverse in new[]{false,true}){
  var actor=Mon("CLEFAIRY","METRONOME") with{SourcePartyStatusOverride=2};var foe=Mon("RATTATA","TACKLE");var p=reverse?foe:actor;var o=reverse?actor:foe;
  var party=ImmutableArray.Create(Member(Mon("CLEFAIRY","METRONOME",WildBattleStatus.Burn)),Member(actor),Member(Mon("CLEFAIRY","METRONOME",WildBattleStatus.Poison)));
  var state=new WildBattleState(p,o,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion,Terrain=WildBattleTerrain.Plain,
    CalledParty=new(reverse?[Member(p)]:party,reverse?party:[Member(o)],reverse?0:1,reverse?1:0,WildBattleCalledBattleKind.Trainer)};
  var r=WildBattleRules.ResolveCalledMoveAction(catalog,state,new(reverse?WildBattleSide.Opponent:WildBattleSide.Player,0,Id(move)));Check(r.Succeeded,"canonical called healing "+r.Fault);
  var body=reverse?r.State!.Opponent:r.State!.Player;var healed=reverse?r.State!.CalledParty!.Opponent:r.State!.CalledParty!.Player;
  Check(body.SourcePartyStatusOverride==null&&healed.All(m=>m.Status==0),"real cure clears persisted sleep while live status already healthy");
  Check(body.Pokemon.Moves[0].Move==Id("METRONOME")&&body.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("METRONOME")].Pp-1,"one original caller PP only");
  Check(healed[0].Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("METRONOME")].Pp&&healed[2].Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("METRONOME")].Pp,"equal-valued nonactive identities retain their PP");
 }
});
Test("malformed party, wrong target, tampered move, stale action and live status overrides reject atomically",()=>{
 var f=Frame(0,"HEAL_BELL");var action=new SourceResolvedMoveAction(f.Slots[0].Occupant!.Value,f.Revision,0,new(0));
 void Atomic(SourceBattleFrame input,SourceKernelResult r){Check(!r.Succeeded&&ReferenceEquals(input,r.State)&&r.Events.IsEmpty&&r.Trace.IsEmpty&&r.Invocation==null,"atomic failure "+r.Fault);}
 Atomic(f,SourceBattleKernel.ResolveAction(catalog,f,action with{Revision=1}));Atomic(f,SourceBattleKernel.ResolveAction(catalog,f,action with{Target=new(1)}));
 var broken=f with{Parties=f.Parties.SetItem(0,f.Parties[0] with{Members=f.Parties[0].Members.RemoveAt(1)})};Atomic(broken,SourceBattleKernel.ResolveAction(catalog,broken,action));
 var changed=catalog with{Moves=catalog.Moves.SetItem(Id("HEAL_BELL"),catalog.Moves[Id("HEAL_BELL")] with{Pp=99})};Atomic(f,SourceBattleKernel.ResolveAction(changed,f,action));
 var divergence=Put(f,2,f.Slots[2].Body! with{SourcePartyStatusOverride=0});Atomic(divergence,SourceBattleKernel.ResolveAction(catalog,divergence,action));
 divergence=Put(f,2,f.Slots[2].Body! with{SourcePartyStatusOverride=WildBattleStatus.Burn}) with{SourceAbsentMask=4,TurnStartAbsentMask=4};Atomic(divergence,SourceBattleKernel.ResolveAction(catalog,divergence,action));
 var begun=Begin(f,0);var advanced=Next(begun);var stale=SourceFourActionTurns.Advance(catalog,advanced.State,begun.Continuation!.Value);Check(!stale.Succeeded&&ReferenceEquals(stale.State,advanced.State)&&stale.Events.IsEmpty&&stale.Trace.IsEmpty,"stale real continuation rejects atomically");Finish(advanced);
});
File.WriteAllText(args[1],Json(new{snapshots=snapshots.Count,groups,checks,failures,turns,runtime=Environment.Version.ToString(),rows}));Console.WriteLine($"groups={groups} checks={checks} turns={turns} failures={failures}");return failures==0&&groups==8&&turns==11?0:1;
