// Reconstructed M04 tests; current validation is recorded in the accompanying recovery evidence.
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

if (args.Length != 2) return 2;
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog = proof.Catalog;
int checks=0, groups=0, failures=0, vectors=0, damageVectors=0, cleanTurns=0;
var summary=new List<object>();
void Check(bool value,string label){checks++;if(!value)throw new InvalidDataException(label);}
void Test(string label,Action action){if(Environment.GetEnvironmentVariable("COMMON_ABILITY_ADAPTERS_ONLY")=="1"&&!label.Contains("Plus")&&!label.Contains("Minus"))return;groups++;try{action();summary.Add(new{label,passed=true});Console.WriteLine("PASS "+label);}catch(Exception e){failures++;summary.Add(new{label,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+label+" "+e);}}
string Json<T>(T item)=>JsonSerializer.Serialize(item);
MoveId Id(string name)=>new("pret:MOVE_"+name);
WildBattleCombatant Mon(string species,string move="TACKLE",int level=50,uint personality=0){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(personality)),new(0)).Pokemon;
 return new(p with{Moves=[new(Id(move),catalog.Moves[Id(move)].Pp,catalog.Moves[Id(move)].Pp)]});
}
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? supplied=null){
 var m=supplied??[Mon("RATTATA"),Mon("RATTATA"),Mon("PIDGEY"),Mon("RATTATA")];
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,
  SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),
  Parties=[new(BattleSide.Player,[Member(m[0]),Member(m[2])]),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],
  Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray()};
}
SourceBattleFrame Body(SourceBattleFrame f,int slot,WildBattleCombatant body){var c=f.Slots[slot];var key=c.Member!.Value;var p=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(slot,c with{Body=body}),Parties=f.Parties.SetItem((int)key.Side,p with{Members=p.Members.SetItem(key.Index,Member(body))})};}
SourceBattleFrame Hp(SourceBattleFrame f,int slot,int hp)=>Body(f,slot,f.Slots[slot].Body! with{Pokemon=f.Slots[slot].Body!.Pokemon with{CurrentHp=hp}});
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,c.Slot.Opposite)).ToImmutableArray();
SourceBattleFrame Queue(SourceBattleFrame f){var r=SourceBattleIdentityRules.CommitSelections(f,f.Revision,Choices(f));Check(r.Succeeded,r.Fault??"queue");return r.State with{Queue=r.State.Queue.Reverse().ToImmutableArray()};}
SourceKernelResult Run(SourceBattleFrame f,int actor=0,int target=1,bool redirected=false){var r=SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[actor].Occupant!.Value,f.Revision,0,new(target)){LightningRodRedirected=redirected});Check(r.Succeeded,r.Fault??"kernel");Check(SourceBattleIdentityRules.Validate(r.State)is null,"kernel output valid "+SourceBattleIdentityRules.Validate(r.State));return r;}
SourceFourActionResult Begin(SourceBattleFrame f){var r=SourceFourActionTurns.BeginActions(catalog,f,f.Revision,Choices(f));Check(r.Succeeded,r.Fault??"begin");return r;}
SourceFourActionResult Next(SourceFourActionResult r){var next=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);Check(next.Succeeded,next.Fault??"next");Check(SourceBattleIdentityRules.Validate(next.State)is null,"turn identity");return next;}
void Atomic(SourceBattleFrame f,SourceKernelResult r,string code){Check(r.Fault==code&&ReferenceEquals(f,r.State)&&r.Events.IsEmpty&&r.Trace.IsEmpty&&r.Invocation is null,"atomic "+code+" actual "+r.Fault);}
using var damageOracle=Process.Start(new ProcessStartInfo(Path.GetFullPath(Path.Combine(args[1],"source-damage-oracle"))){RedirectStandardInput=true,RedirectStandardOutput=true,UseShellExecute=false})!;
int Expected(SourceBattleFrame f,SourceHpChangedEvent hit,string moveName,bool screen=false,bool boost=false){
 var actor=f.Slots[hit.Actor.Value].Body!;var target=f.Slots[hit.Target.Value].Body!;var move=catalog.Moves[Id(moveName)];
 bool physical=TypeRules.Category(Enum.Parse<ElementType>(move.Type[5..],true))==DamageCategory.Physical;
 var species=catalog.Species[actor.Pokemon.Species];bool stab=species.Type1==move.Type||species.Type2==move.Type;
 damageOracle.StandardInput.WriteLine(string.Join(' ',new[]{physical?actor.Pokemon.Stats.Attack:actor.Pokemon.Stats.SpecialAttack,actor.Pokemon.Level,move.Power,physical?target.Pokemon.Stats.Defense:target.Pokemon.Stats.SpecialDefense,physical?1:0,screen?1:0,SourceMoveTargeting.CountSourcePresentDefenders(f,hit.Target),move.Target=="MOVE_TARGET_BOTH"?1:0,hit.Critical?1:0,stab?1:0,hit.RandomPercent,boost?1:0,move.Effect=="EFFECT_EXPLOSION"?1:0}));
 damageOracle.StandardInput.Flush();damageVectors++;return int.Parse(damageOracle.StandardOutput.ReadLine()!);
}
Test("actual .NET10 and authenticated source catalog",()=>{Check(Environment.Version.Major==10,"runtime");Check(catalog.Species.Count==386&&catalog.Moves.Count==354,"complete authentic catalog");});
Test("11520 source-derived learned and called target RNG vectors",()=>{
 var names=new Dictionary<int,string>{{0,"MOVE_TARGET_SELECTED"},{1,"MOVE_TARGET_DEPENDS"},{2,"MOVE_TARGET_USER_OR_SELECTED"},{4,"MOVE_TARGET_RANDOM"},{8,"MOVE_TARGET_BOTH"},{16,"MOVE_TARGET_USER"},{32,"MOVE_TARGET_FOES_AND_ALLY"},{64,"MOVE_TARGET_OPPONENTS_FIELD"}};
 foreach(string line in File.ReadLines(Path.Combine(args[1],"source-target-vectors.csv"))){
  var x=line.Split(',').Select(long.Parse).ToArray();var f=Frame();
  for(int i=0;i<4;i++){
   var b=f.Slots[i].Body!;b=b with{Pokemon=b.Pokemon with{AbilityId=(x[7]&(1<<i))!=0?"ABILITY_LIGHTNING_ROD":"ABILITY_RUN_AWAY",CurrentHp=(x[6]&(1<<i))!=0?b.Pokemon.Stats.MaxHp:0}};f=Body(f,i,b);
  }
  f=f with{Rng=new((uint)x[10]),SourceAbsentMask=(byte)x[5],TurnStartAbsentMask=(byte)x[5]};
  int side=((int)x[1]&1)^1;
  if(x[8]!=0)f=f with{Sides=f.Sides.SetItem(side,f.Sides[side] with{FollowMeTimer=1,FollowMeTarget=new((int)x[9])})};
  f=Queue(f);
  string mode=names[(int)x[3]];bool electric=x[4]!=0;
  var move=catalog.Moves.Values.FirstOrDefault(m=>m.Target==mode&&(m.Type=="TYPE_ELECTRIC")==electric&&(mode!="MOVE_TARGET_USER"||m.Power==0));
  if(move is null && x[0]==0) continue;
  move ??= catalog.Moves[Id("TACKLE")];
  var actual=x[0]==0?SourceMoveTargeting.ResolveLearned(f,new((int)x[1]),new((int)x[2]),move):SourceMoveTargeting.ResolveCalled(f,new((int)x[1]),move,mode);
  Check(actual.Succeeded,actual.Fault??"source resolver");
  Check(actual.Target?.Value==x[11]&&actual.Rng.Seed==x[12]&&actual.Rng.Cursor==(ulong)x[13]&&actual.LightningRodRedirected==(x[14]!=0),"source vector "+line+" actual "+Json(actual));vectors++;
 }
 Check(vectors==11520,"nonempty full source vectors");
});
Test("source absence HP and deferred vacancy are distinct target predicates",()=>{
 var f=Hp(Frame(),1,0);var move=catalog.Moves[Id("TACKLE")];
 Check(SourceMoveTargeting.ResolveLearned(f,new(0),new(1),move).Target==new BattlerSlot(1),"HP zero alone does not reselect");
 var deferred=f with{DeferredKoVacancyMask=2};Check(SourceMoveTargeting.ResolveLearned(deferred,new(0),new(1),move).Target==new BattlerSlot(3),"approved vacancy fallback");
 Check(SourceMoveTargeting.CountSourcePresentDefenders(deferred,new(1))==2,"deferred vacancy still source-present for damage");
 var absent=f with{SourceAbsentMask=2};Check(SourceMoveTargeting.ResolveLearned(absent,new(0),new(1),move).Target==new BattlerSlot(3),"source absence fallback");
 Check(SourceMoveTargeting.CountSourcePresentDefenders(absent,new(1))==1,"source absence changes damage count");
 f=Hp(deferred,3,0) with{DeferredKoVacancyMask=10};
 var called=SourceMoveTargeting.ResolveCalled(f,new(0),move);Check(called.Succeeded&&called.Target is null&&called.Trace.IsEmpty&&called.Rng==f.Rng,"empty caller rejection domain returns before RNG");
 Check(SourceMoveTargeting.ResolveLearned(f,new(0),new(1),move).Target is null,"no invented enemy target");
});
Test("real Lightning Rod redirects without later-generation immunity or stat gain",()=>{
 var rod=Mon("ELECTRIKE","TACKLE",50,1);Check(rod.Pokemon.AbilityId=="ABILITY_LIGHTNING_ROD","authentic Electrike donor");
 var f=Queue(Frame([Mon("JOLTEON","THUNDER_SHOCK"),Mon("RATTATA"),Mon("PIDGEY"),rod]));
 var selected=SourceMoveTargeting.ResolveLearned(f,new(0),new(1),catalog.Moves[Id("THUNDER_SHOCK")]);Check(selected.Target==new BattlerSlot(3)&&selected.LightningRodRedirected,"rod target");
 var r=Run(f,0,3,true);Check(r.Events.OfType<SourceAbilityActivatedEvent>().Any(e=>e.Ability=="ABILITY_LIGHTNING_ROD"&&e.Side.Value==3),"source announcement");
 Check(r.Events.OfType<SourceHpChangedEvent>().Single().HpAfter<rod.Pokemon.CurrentHp,"electric hit reaches Lightning Rod");Check(r.State.Slots[3].Body!.SpecialAttackStageDelta==0,"no GenIV special attack boost");
});
Test("Lightning Rod source scan does not silently discard absent or zero-HP ability bodies",()=>{
 var f=Queue(Frame([Mon("JOLTEON","SHOCK_WAVE"),Mon("RATTATA"),Mon("PIDGEY"),Mon("ELECTRIKE","TACKLE",50,1)]));
 var move=catalog.Moves[Id("SHOCK_WAVE")];
 foreach(var altered in new[]{f with{SourceAbsentMask=8,TurnStartAbsentMask=8},Hp(f,3,0)}){
  var r=SourceMoveTargeting.ResolveLearned(altered,new(0),new(1),move);Check(r.Target==new BattlerSlot(3)&&r.LightningRodRedirected,"retain source Lightning Rod register scan");
 }
 var deferred=Hp(f,3,0) with{DeferredKoVacancyMask=8};var stopped=SourceMoveTargeting.ResolveLearned(deferred,new(0),new(1),move);Check(stopped.Target is null&&stopped.Trace.IsEmpty,"approved vacancy at action boundary cannot be redirected into");
});
Test("BOTH shares one invocation with independent hit RNG history and actual party identities",()=>{
 var f=Frame([Mon("RATTATA","SURF"),Mon("RATTATA"),Mon("PIDGEY"),Mon("RATTATA")]);var r=Run(f);var hits=r.Events.OfType<SourceHpChangedEvent>().ToArray();
 Check(hits.Select(h=>h.Target.Value).SequenceEqual(new[]{1,3}),"source first enemy then partner");Check(r.Events.OfType<SourceAttackStringEvent>().Count()==1&&r.Events.OfType<SourceMoveUsedEvent>().Count()==1,"one action string");
 Check(r.Events.OfType<SourcePpCommandEvent>().Sum(e=>e.Before-e.After)==1,"only one PP deduction");
 foreach(var hit in hits){Check(hit.Damage==Expected(f,hit,"SURF"),"source spread damage");var c=r.State.Slots[hit.Target.Value];Check(c.Registers.History!.LastTakenFrom[0]==Id("SURF"),"per-target history");Check(c.Body!.Pokemon.CurrentHp==r.State.Party(c.Slot.Side).Members[c.Member!.Value.Index].Pokemon.CurrentHp,"distinct party member HP");}
 Check(r.Trace.Count(t=>t.Purpose.EndsWith(".accuracy"))==2&&r.Trace.Count(t=>t.Purpose.EndsWith(".secondary-effect"))==2,"one shared recipe per target");
 Check(r.State.Slots[2].Body!.Pokemon.CurrentHp==f.Slots[2].Body!.Pokemon.CurrentHp,"Surf GenIII only opponents");
});
Test("spread Pressure costs once but uses all source-stored ability slots",()=>{
 var pressure=Mon("DUSCLOPS");Check(pressure.Pokemon.AbilityId=="ABILITY_PRESSURE","authentic Pressure");
 var f=Frame([Mon("RATTATA","SURF"),pressure,Mon("RATTATA"),pressure]);var r=Run(f);
 Check(r.Events.OfType<SourcePpCommandEvent>().Sum(e=>e.Before-e.After)==3&&r.Events.OfType<SourcePpCommandEvent>().Count()==2,"BOTH one cost including two Pressure enemies");
 f=Frame([Mon("RATTATA","EXPLOSION"),pressure,pressure,pressure]);f=Hp(f,2,0) with{SourceAbsentMask=4,TurnStartAbsentMask=4};r=Run(f);
 Check(r.Events.OfType<SourcePpCommandEvent>().Single().Before-r.Events.OfType<SourcePpCommandEvent>().Single().After==4,"all-target Pressure includes source-absent retained ally");
});
Test("source screens and spread use staged integer divisions with absence-only counts",()=>{
 foreach(string name in new[]{"SWIFT","SURF"})foreach(bool screen in new[]{false,true})foreach(bool absent in new[]{false,true})foreach(uint seed in new uint[]{1,3,31,81}){
  var f=Frame([Mon("RATTATA",name),Mon("RATTATA"),Mon("PIDGEY"),Mon("RATTATA")]) with{Rng=new(seed)};
  if(screen)f=f with{Sides=f.Sides.SetItem(1,new(BattleSide.Opponent){ReflectTurnsRemaining=name=="SWIFT"?3:0,LightScreenTurnsRemaining=name=="SURF"?3:0})};
  if(absent)f=Hp(f,3,0) with{SourceAbsentMask=8,TurnStartAbsentMask=8};
  var r=Run(f);foreach(var hit in r.Events.OfType<SourceHpChangedEvent>())Check(hit.Damage==Expected(f,hit,name,screen),"FR screen/spread "+name+" screen="+screen+" absence="+absent);
 }
});
Test("BOTH move-end partner traversal tests HP rather than source absence",()=>{
 var f=Frame([Mon("RATTATA","SURF"),Mon("RATTATA"),Mon("RATTATA"),Mon("RATTATA")]) with{SourceAbsentMask=8,TurnStartAbsentMask=8};
 var r=Run(f);var hits=r.Events.OfType<SourceHpChangedEvent>().ToArray();Check(hits.Select(h=>h.Target.Value).SequenceEqual(new[]{1,3}),"source-absent live partner receives next target");
 foreach(var hit in hits)Check(hit.Damage==Expected(f,hit,"SURF"),"HP traversal retains absence-only count");
});
Test("BOTH first immunity or miss cannot poison second target bookkeeping",()=>{
 var f=Frame([Mon("RATTATA","SURF"),Mon("POLIWAG"),Mon("PIDGEY"),Mon("RATTATA")]);Check(f.Slots[1].Body!.Pokemon.AbilityId=="ABILITY_WATER_ABSORB","authentic absorb donor");var r=Run(f);
 Check(r.Events.OfType<SourceHpChangedEvent>().Single().Target.Value==3,"absorbed first still reaches second");Check(r.State.Slots[3].Registers.History!.LastTakenFrom[0]==Id("SURF"),"second lands independently");
 bool found=false;for(uint seed=1;seed<500&&!found;seed++){
  f=Frame([Mon("RATTATA","RAZOR_LEAF"),Mon("RATTATA"),Mon("PIDGEY"),Mon("RATTATA")]) with{Rng=new(seed)};r=Run(f);
  if(r.Events.OfType<SourceMoveMissedEvent>().Any(e=>e.Target.Value==1)&&r.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Target.Value==3)){found=true;Check(r.State.Slots[3].Registers.History!.LastTakenFrom[0]==Id("RAZOR_LEAF"),"second after miss history");Check(r.State.Slots[1].Registers.History!.LastTakenFrom[0]is null,"miss has no mirror history");}
 }Check(found,"source first-miss second-hit fixture found");
});
Test("BOTH second pass works after final PP and first target KO with count unchanged",()=>{
 var actor=Mon("RATTATA","SURF",100);actor=actor with{Pokemon=actor.Pokemon with{Moves=[actor.Pokemon.Moves[0] with{CurrentPp=1}]}};
 var f=Frame([actor,Mon("RATTATA","TACKLE",1),Mon("PIDGEY"),Mon("RATTATA","TACKLE",100)]);var r=Run(f);var hits=r.Events.OfType<SourceHpChangedEvent>().ToArray();
 Check(hits.Length==2&&r.Events.OfType<SourceFaintedEvent>().Any(e=>e.Side.Value==1),"both executes after first KO");Check(hits[1].Damage==Expected(f,hits[1],"SURF"),"first KO does not become source absence");Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==0,"final PP spent once");
});
Test("numeric-all Explosion pays once uses numeric order and keeps acting at zero HP",()=>{
 var f=Frame([Mon("RATTATA"),Mon("RATTATA"),Mon("RATTATA","EXPLOSION"),Mon("RATTATA")]);var r=Run(f,2,3);var hits=r.Events.OfType<SourceHpChangedEvent>().ToArray();
 Check(hits.Select(h=>h.Target.Value).SequenceEqual(new[]{0,1,3}),"numeric targets including ally");Check(r.Events.OfType<SourceSelfDestructEvent>().Count()==1&&r.Events.OfType<SourceSelfDestructEvent>().Single().Sequence<hits[0].Sequence,"attacker zero first");
 Check(r.Events.OfType<SourceFaintedEvent>().Last().Side.Value==2,"attacker faints last");Check(r.Events.OfType<SourcePpCommandEvent>().Count()==1,"one ppreduce");
 foreach(var hit in hits)Check(hit.Damage==Expected(f,hit,"EXPLOSION"),"no BOTH reduction");
 Check(r.Trace.Count(t=>t.Purpose.EndsWith(".damage"))==3&&r.Trace.Count(t=>t.Purpose.EndsWith(".secondary-effect"))==0,"Explosion exact draw families");
});
Test("Explosion source absence and Damp scan retain different predicates",()=>{
 var f=Frame([Mon("RATTATA","EXPLOSION"),Mon("RATTATA"),Mon("RATTATA"),Mon("RATTATA")]);f=Hp(f,2,0) with{SourceAbsentMask=4,TurnStartAbsentMask=4};var r=Run(f);
 Check(r.Events.OfType<SourceHpChangedEvent>().Select(e=>e.Target.Value).SequenceEqual(new[]{1,3}),"numeric loop skips source absence");
 var damp=Mon("PSYDUCK","TACKLE",50,0);Check(damp.Pokemon.AbilityId=="ABILITY_DAMP","authentic Damp donor");f=Body(f,2,damp);f=Hp(f,2,0);r=Run(f);
 Check(r.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Ability=="ABILITY_DAMP"&&e.Target.Value==2),"fainted absent Damp blocks");Check(!r.Events.OfType<SourceSelfDestructEvent>().Any()&&!r.Events.OfType<SourceHpChangedEvent>().Any()&&r.Trace.IsEmpty,"Damp before HP and RNG");
});
Test("Plus and Minus boost from actual partner enemy fainted and source-absent donors",()=>{
 foreach(string species in new[]{"PLUSLE","MINUN"})foreach(int donorSlot in new[]{1,2,3})foreach(bool faint in new[]{false,true}){
  var f=Frame([Mon(species,"SPARK"),Mon("RATTATA"),Mon("RATTATA"),Mon("RATTATA")]);
  string donor=species=="PLUSLE"?"MINUN":"PLUSLE";f=Body(f,donorSlot,Mon(donor,"GROWL"));
  int target=donorSlot==1?3:1;if(faint)f=Hp(f,donorSlot,0) with{SourceAbsentMask=(byte)(1<<donorSlot),TurnStartAbsentMask=(byte)(1<<donorSlot)};
  var r=Run(f,0,target);var hit=r.Events.OfType<SourceHpChangedEvent>().Single();Check(hit.Damage==Expected(f,hit,"SPARK",boost:true),"source full-field Plus/Minus "+species+" donor="+donorSlot+" faint="+faint);
 }
 var noDonor=Frame([Mon("PLUSLE","SPARK"),Mon("RATTATA"),Mon("PLUSLE","GROWL"),Mon("RATTATA")]);var plain=Run(noDonor);Check(plain.Events.OfType<SourceHpChangedEvent>().Single().Damage==Expected(noDonor,plain.Events.OfType<SourceHpChangedEvent>().Single(),"SPARK"),"same ability not counterpart");
});
WildBattleState Singles(WildBattleCombatant player,WildBattleCombatant opponent)=>new(player,opponent,0,0,0,new(31),0,WildBattleOutcome.Ongoing,RulesVersion:WildBattleRules.FuchsiaRulesVersion){Terrain=WildBattleTerrain.Plain,SourceMechanicsRulesVersion=BattleRulesProfile.CanonicalSourceSinglesVersion};
Test("real Plusle and Minun enter canonical dynamic and copied-ability actions",()=>{
 var control=WildBattleRules.ResolveSourceMoveAction(catalog,Singles(Mon("RATTATA","HIDDEN_POWER"),Mon("RATTATA")),new(WildBattleSide.Player,0));
 Check(control.Succeeded&&control.Battle.Events.OfType<WildBattleHpChangedEvent>().Any(),"canonical dynamic positive control executes HIDDEN_POWER");
 foreach(string species in new[]{"PLUSLE","MINUN"})foreach(bool opponent in new[]{false,true}){
  var actual=Mon(species,"HIDDEN_POWER");Check(actual.Pokemon.AbilityId==(species=="PLUSLE"?"ABILITY_PLUS":"ABILITY_MINUS"),"real scalar donor");
  var state=opponent?Singles(Mon("RATTATA"),actual):Singles(actual,Mon("RATTATA"));
  var result=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(opponent?WildBattleSide.Opponent:WildBattleSide.Player,0));
  Check(result.Succeeded&&result.Battle.Events.OfType<WildBattleHpChangedEvent>().Any(),"dynamic action executes "+species+" actual "+result.Fault);
  Check(SourceBattleAdmission.ValidateEntry(proof,BattleRulesProfile.CanonicalSourceSingles,[state.Player],0,state.Opponent)==null,"entry admits authenticated counterpart capability");
  // The effective copied ability uses the same canonical field query.
  state=Singles(Mon("RATTATA","HIDDEN_POWER") with{CalledOverlay=new(){Ability=actual.Pokemon.AbilityId}},actual);
  result=WildBattleRules.ResolveSourceMoveAction(catalog,state,new(WildBattleSide.Player,0));
  Check(result.Succeeded&&result.Battle.Events.OfType<WildBattleHpChangedEvent>().Any(),"copied Plus/Minus dynamic action executes "+result.Fault);
 }
});
Test("legacy singles and actual Transform retain Generation III Lightning Rod damage",()=>{
 var rod=Mon("ELECTRIKE","SHOCK_WAVE",50,1);Check(rod.Pokemon.AbilityId=="ABILITY_LIGHTNING_ROD","real scalar Lightning Rod donor");
 var natural=Singles(Mon("RATTATA","SHOCK_WAVE"),rod);
 var hit=WildBattleRules.ResolveSourceMoveAction(catalog,natural,new(WildBattleSide.Player,0));
 Check(hit.Succeeded,hit.Fault??"singles rod");Check(hit.State!.Opponent.Pokemon.CurrentHp<rod.Pokemon.CurrentHp&&hit.State.Opponent.SpecialAttackStageDelta==0,"single enemy takes electric damage without boost");
 var original=Mon("DITTO","TRANSFORM");var copy=WildBattleRules.ResolveSourceMoveAction(catalog,Singles(original,rod),new(WildBattleSide.Player,0));
 Check(copy.Succeeded&&copy.State!.Player.Transform is not null,copy.Fault??"actual Transform");
 Check(WildBattleEffective.Ability(copy.State!.Player)=="ABILITY_LIGHTNING_ROD","Transform copies authenticated rod");
 var copiedHit=WildBattleRules.ResolveSourceMoveAction(catalog,copy.State,new(WildBattleSide.Opponent,0));
 Check(copiedHit.Succeeded,copiedHit.Fault??"transformed target electric action");
 Check(copiedHit.State!.Player.Pokemon.CurrentHp<copy.State.Player.Pokemon.CurrentHp&&copiedHit.State.Player.SpecialAttackStageDelta==0,"copied ability grants neither immunity nor boost");
 Check(WildBattleTransformationRules.PersistentPokemon(copiedHit.State.Player).Species==original.Pokemon.Species,"original party identity survives copied ability");
});
Test("genuine four-action spread turns complete with serialized continuations",()=>{
 var f=Frame([Mon("RATTATA","SURF"),Mon("RATTATA","ACID_ARMOR"),Mon("RATTATA","SWIFT"),Mon("RATTATA","ACID_ARMOR")]);
 var selections=Choices(f).Select(s=>s.Actor.Slot.Side==BattleSide.Opponent?s with{Target=s.Actor.Slot}:s).ToImmutableArray();
 var r=SourceFourActionTurns.BeginActions(catalog,f,f.Revision,selections);Check(r.Succeeded,r.Fault??"spread begin");int hits=0;
 for(int i=0;i<5;i++){var saved=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var replay=SourceFourActionTurns.Advance(catalog,saved,r.Continuation!.Value);r=Next(r);Check(Json(replay)==Json(r),"exact spread JSON continuation "+i);hits+=r.Events.OfType<SourceHpChangedEvent>().Count();}
 Check(r.CompletedCleanTurn&&hits==4,"two actors each hit two targets in a complete turn");cleanTurns++;
});
Test("genuine four-action targeting uses retained order and Lightning Rod",()=>{
 var f=Frame([Mon("JOLTEON","SHOCK_WAVE"),Mon("RATTATA","ACID_ARMOR"),Mon("RATTATA","SWIFT"),Mon("ELECTRIKE","ACID_ARMOR",50,1)]);
 var choices=Choices(f).Select(s=>s.Actor.Slot.Side==BattleSide.Opponent?s with{Target=s.Actor.Slot}:s).ToImmutableArray();
 var r=SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices);Check(r.Succeeded,r.Fault??"rod begin");var all=new List<SourceBattleEvent>();
 for(int i=0;i<5;i++){r=Next(r);all.AddRange(r.Events);}
 Check(r.CompletedCleanTurn,"full Lightning Rod turn");Check(all.OfType<SourceHpChangedEvent>().Single(h=>h.Move==Id("SHOCK_WAVE")).Target.Value==3,"redirected at action time");
 Check(all.OfType<SourceAbilityActivatedEvent>().Count(e=>e.Ability=="ABILITY_LIGHTNING_ROD")==1,"one announcement");cleanTurns++;
});
Test("spread action finishes all target passes before opponent EXP boundary",()=>{
 var f=Frame([Mon("RATTATA","EXPLOSION",100),Mon("RATTATA","TACKLE",1),Mon("RATTATA","TACKLE",1),Mon("RATTATA","TACKLE",1)]);
 var r=Begin(f);r=Next(r);Check(r.Events.OfType<SourceHpChangedEvent>().Count()==3,"all hits before owner returns");
 Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending&&r.State.FourTurn.Defeats.Length==4&&r.State.FourTurn.Defeats.Count(d=>d.RequiresExperience)==2,"all occupants once and both enemy EXP records");
 Check(r.State.FourTurn.OutcomeAfterRewards==WildBattleOutcome.Draw&&r.State.Outcome==WildBattleOutcome.Ongoing,"draw waits behind EXP");
 Check(r.State.Rewards?.Context is null,"this fixture has no authoritative reward context");
 for(int attempt=0;attempt<2;attempt++) {
  var pending=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);
  Check(pending.Fault=="source_four_reward_context_required"&&ReferenceEquals(pending.State,r.State)&&pending.Trace.IsEmpty&&pending.Events.IsEmpty,"missing reward facts cannot skip or acknowledge EXP, including repeated request");
 }
});
Test("second-pass RNG failure rolls back entire action including first hit and PP",()=>{
 var f=Frame([Mon("RATTATA","SURF"),Mon("RATTATA"),Mon("RATTATA"),Mon("RATTATA")]) with{Rng=new(31,ulong.MaxValue-4)};
 var one=Hp(f,3,0) with{SourceAbsentMask=8,TurnStartAbsentMask=8};var firstOnly=Run(one);
 Check(firstOnly.Trace.Length==4&&firstOnly.State.Rng.Cursor==ulong.MaxValue&&firstOnly.Events.OfType<SourceHpChangedEvent>().Count()==1,"the first pass finishes before second-target exhaustion");
 Atomic(f,SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[0].Occupant!.Value,f.Revision,0,new(1))),"source_kernel_counter_exhausted");
 Check(f.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("SURF")].Pp&&f.Slots[1].Body!.Pokemon.CurrentHp==f.Slots[1].Body!.Pokemon.Stats.MaxHp,"immutable initial costs and target");
 var scheduled=Begin(Frame([Mon("JOLTEON","SURF"),Mon("RATTATA","ACID_ARMOR"),Mon("RATTATA","ACID_ARMOR"),Mon("RATTATA","ACID_ARMOR")]));
 var ownerInput=scheduled.State with{Rng=new(31,ulong.MaxValue-4)};
 Check(ownerInput.Queue[ownerInput.NextAction].Slot.Value==0,"real retained queue executes spread first");
 var ownerFailure=SourceFourActionTurns.Advance(catalog,ownerInput,scheduled.Continuation!.Value);
 Check(ownerFailure.Fault=="source_kernel_counter_exhausted"&&ReferenceEquals(ownerInput,ownerFailure.State)&&ownerFailure.Events.IsEmpty&&ownerFailure.Trace.IsEmpty,"four-action owner also rolls back entire spread action");
 var exhausted=f with{Rng=new(31,ulong.MaxValue)};var selection=SourceMoveTargeting.ResolveCalled(exhausted,new(0),catalog.Moves[Id("TACKLE")]);
 Check(selection.Fault=="source_target_counter_exhausted"&&selection.Rng==exhausted.Rng&&selection.Trace.IsEmpty,"rejection sampling overflow atomic");
});
Test("unmigrated callers all-target vacancies and malformed actions fail atomically",()=>{
 var f=Frame([Mon("RATTATA","EXPLOSION"),Mon("RATTATA"),Mon("RATTATA"),Mon("RATTATA")]);f=Hp(f,2,0) with{DeferredKoVacancyMask=4};
 Atomic(f,SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[0].Occupant!.Value,f.Revision,0,new(1))),"source_kernel_spread_deferred_target_not_migrated");
 foreach(string name in new[]{"EARTHQUAKE","MAGNITUDE","METRONOME","FOLLOW_ME","PERISH_SONG","TEETER_DANCE"}){
  f=Frame([Mon("RATTATA",name),Mon("RATTATA"),Mon("RATTATA"),Mon("RATTATA")]);
  Atomic(f,SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[0].Occupant!.Value,f.Revision,0,new(1))),"source_kernel_recipe_not_migrated");
 }
 f=Frame();Atomic(f,SourceBattleKernel.ResolveAction(catalog,f,new(f.Slots[0].Occupant!.Value,f.Revision+1,0,new(1))),"source_kernel_stale_action");
});
damageOracle.StandardInput.Close();damageOracle.WaitForExit();Check(damageOracle.ExitCode==0,"damage oracle completed");
Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"targeting-result.json"),Json(new{passed=failures==0,checks,groups,failures,vectors,damageVectors,cleanTurns,runtime=Environment.Version.ToString(),results=summary}));
Console.WriteLine($"{checks} checks; {groups} groups; {failures} failures; {vectors} source target vectors; {damageVectors} source damage vectors; {cleanTurns} complete genuine turns");return failures==0?0:1;
