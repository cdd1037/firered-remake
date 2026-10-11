using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

if (args.Length < 2) throw new ArgumentException("PRODUCT_ROOT OUTPUT_JSON [--capture-legacy]");
string root = args[0];
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(root); var catalog = proof.Catalog;
MoveId Id(string n) => new("pret:MOVE_" + n);
string Json<T>(T x) => JsonSerializer.Serialize(x);
var raw = PretRocketHideoutPokemon.Load(root).Catalog;
var extra = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules:true);
var prior = PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(raw with { Moves=raw.Moves.SetItems(extra.Moves) },root,root).Catalog,root).Catalog,root).Catalog;
var legacy = PretMetronomeMoveCatalog.Load(root).Extend(PretFuchsiaRoutePokemon.Load(prior,root).Catalog);
List<object> LegacySnapshots() {
    var snapshots = new List<object>();
    foreach (string species in new[]{"PIDGEY","GEODUDE"}) foreach (string target in new[]{"GASTLY","ONIX","RATTATA"})
    foreach (uint seed in Enumerable.Range(0,12).Select(i=>(uint)i)) {
        WildBattleCombatant LegacyMon(string name,int level) {
            var p=PokemonFactory.Create(legacy,new(new("pret:SPECIES_"+name),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
            return new(p with {Moves=[new(Id("TACKLE"),0,35)]});
        }
        var player=LegacyMon(species,25);var opponent=LegacyMon(target,40);
        if(seed%3==1) player=player with{Status=WildBattleStatus.Burn};
        if(seed%3==2) player=player with{Pokemon=player.Pokemon with{CurrentHp=1}};
        var begun=WildBattleRules.StartSourceWild(legacy,[player],0,opponent,new(seed));
        if(!begun.Succeeded)throw new Exception(begun.Fault?.ToString());
        var result=WildBattleRules.ResolveMove(legacy,begun.State!,WildBattleRules.StruggleSlot);
        if(!result.Succeeded)throw new Exception(result.Fault?.ToString());
        snapshots.Add(new{species,target,seed,sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(result)))).ToLowerInvariant()});
    }
    return snapshots;
}
var snapshots=LegacySnapshots();
if(args.Contains("--capture-legacy")){File.WriteAllText(args[1],Json(new{baseline="bda2d6a7bf3722e1dd910e711e25bdd1d5c8f095",snapshots}));Console.WriteLine("Captured "+snapshots.Count+" immutable legacy snapshots");return 0;}
int groups=0,checks=0,failures=0;var results=new List<object>();
void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
void Test(string label,Action run){groups++;try{run();results.Add(new{label,passed=true});Console.WriteLine("PASS "+label);}catch(Exception ex){failures++;results.Add(new{label,passed=false,error=ex.ToString()});Console.WriteLine("FAIL "+label+" "+ex);}}
using var baseline=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"legacy-baseline-bda2d6a.json")));
using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-vectors.json")));
WildBattleCombatant Mon(string species="PIDGEY",int level=80,params string[] moves){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
 if(moves.Length==0)moves=["DEFENSE_CURL"];
 return new(p with{Moves=moves.Select(n=>new PokemonMoveSlot(Id(n),catalog.Moves[Id(n)].Pp,catalog.Moves[Id(n)].Pp)).ToImmutableArray()});
}
WildBattleCombatant Empty(WildBattleCombatant m)=>m with{Pokemon=m.Pokemon with{Moves=m.Pokemon.Moves.Select(s=>s with{CurrentPp=0}).ToImmutableArray()}};
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? mons=null,uint seed=31){
 var m=mons??[Mon(),Mon("SNORLAX",100),Mon("SPEAROW"),Mon("GASTLY",100)];
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(seed),
  Parties=[new(BattleSide.Player,[Member(m[0]),Member(m[2])]),new(BattleSide.Opponent,[Member(m[1]),Member(m[3])])],
  Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),
  Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_8"),Metadata=Enumerable.Range(0,2).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
}
SourceBattleFrame Put(SourceBattleFrame f,int slot,WildBattleCombatant m){var c=f.Slots[slot];var k=c.Member!.Value;var p=f.Party(k.Side);return f with{Slots=f.Slots.SetItem(slot,c with{Body=m}),Parties=f.Parties.SetItem((int)k.Side,p with{Members=p.Members.SetItem(k.Index,Member(m))})};}
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>{
 if(SourceFourActionTurns.RequiresStruggle(catalog,f,c.Slot))return SourceActionSelection.Struggle(c.Occupant!.Value);
 int pos=c.Registers.Turn?.Lock?.PpSlot??Enumerable.Range(0,c.Body!.Pokemon.Moves.Length).First(i=>WildBattleRules.IsCalledMoveSelectable(catalog,c.Body,i));
 var move=catalog.Moves[c.Registers.Turn?.Lock?.Move??c.Body!.Pokemon.Moves[pos].Move];
 return new(c.Occupant!.Value,SourceActionKind.Move,pos,c.Registers.Turn?.Lock?.Target??(move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot:c.Slot.Opposite));
}).ToImmutableArray();
SourceFourActionResult Ok(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"result");Check(SourceBattleIdentityRules.Validate(r.State)is null,"output identity");return r;}
SourceFourActionResult Begin(SourceBattleFrame f,ImmutableArray<SourceActionSelection>? picks=null)=>Ok(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,picks??Choices(f)));
SourceFourActionResult Next(SourceFourActionResult r){var restored=JsonSerializer.Deserialize<SourceBattleFrame>(Json(r.State))!;var a=SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value);var b=SourceFourActionTurns.Advance(catalog,restored,r.Continuation.Value);Check(Json(a)==Json(b),"serialized exact continuation");return Ok(a);}
(SourceBattleFrame State,List<SourceBattleEvent> Events,List<RandomTrace> Trace) Finish(SourceFourActionResult r){var events=r.Events.ToList();var trace=r.Trace.ToList();int bound=0;while(r.State.FourTurn is not null&&r.State.FourTurn.Phase is not(SourceFourActionPhase.BattleEnded or SourceFourActionPhase.ReplacementsPending)){
 Check(++bound<40,"finite continuation");if(r.State.Rewards?.Pending is{} pending){var move=pending.Moves[0];r=Ok(SourceFourRewards.Learn(catalog,r.State,r.Continuation!.Value,new(pending.Recipient,move.Level,move.Move,null)));}else r=Next(r);events.AddRange(r.Events);trace.AddRange(r.Trace);}
 return(r.State,events,trace);
}
void Atomic(SourceBattleFrame f,SourceFourActionResult r,string why)=>Check(!r.Succeeded&&ReferenceEquals(f,r.State)&&r.Trace.IsEmpty&&r.Events.IsEmpty,why+": "+r.Fault);
Test("official runtime pinned source and unchanged original singleton recipe results",()=>{
 Check(Environment.Version.Major==10,"actual runtime 10");Check(source.RootElement.GetProperty("sourceCommit").GetString()==PokemonFactory.SourceCommit,"original source pin");
 Check(snapshots.Count==72&&Json(snapshots)==baseline.RootElement.GetProperty("snapshots").GetRawText(),"72 prior singleton full-result hashes unchanged");
});
Test("80 original CheckMoveLimitations vectors distinguish PP and Disable from implementation support",()=>{
 foreach(var v in source.RootElement.GetProperty("limits").EnumerateArray()){
  var m=Mon("PIDGEY",80,"TACKLE","GROWL","TAIL_WHIP","QUICK_ATTACK");int mask=v.GetProperty("mask").GetInt32(),disabled=v.GetProperty("disabled").GetInt32();
  m=m with{Pokemon=m.Pokemon with{Moves=m.Pokemon.Moves.Select((s,i)=>s with{CurrentPp=(mask&(1<<i))!=0?0:s.CurrentPp}).ToImmutableArray()},DisabledMove=disabled==0?null:m.Pokemon.Moves[disabled-1].Move,DisableTurnsRemaining=disabled==0?0:3};
  var f=Put(Frame(),0,m);Check(SourceFourActionTurns.RequiresStruggle(catalog,f,new(0))==(v.GetProperty("required").GetInt32()==1),"exact PP/Disable mask");
 }
 var unsupported=Put(Frame(),0,Mon("PIDGEY",80,"METRONOME"));Check(!SourceFourActionTurns.RequiresStruggle(catalog,unsupported,new(0)),"unsupported live move never grants Struggle");
 Atomic(unsupported,SourceFourActionTurns.BeginActions(catalog,unsupported,unsupported.Revision,Choices(unsupported).SetItem(0,SourceActionSelection.Struggle(unsupported.Slots[0].Occupant!.Value))),"unsupported move not fallback");
});
Test("192 original selection and GetMoveTarget vectors including source absent and Follow Me",()=>{
 foreach(var v in source.RootElement.GetProperty("targets").EnumerateArray()){
  int actor=v.GetProperty("actor").GetInt32(),mode=v.GetProperty("mode").GetInt32(),opponent=(actor&1)^1;var f=Frame(seed:v.GetProperty("seed").GetUInt32());
  if(mode==1){f=Put(f,opponent,f.Slots[opponent].Body! with{Pokemon=f.Slots[opponent].Body!.Pokemon with{CurrentHp=0}});f=f with{SourceAbsentMask=(byte)(1<<opponent),TurnStartAbsentMask=(byte)(1<<opponent)};}
  if(mode==2)f=f with{Sides=f.Sides.SetItem(opponent,f.Sides[opponent] with{FollowMeTimer=1,FollowMeTarget=new(opponent)})};
  f=Put(f,actor,Empty(f.Slots[actor].Body!));
  var selected=Begin(f);Check(selected.State.Queue.Single(a=>a.Slot.Value==actor).Target!.Value.Value==v.GetProperty("selection").GetInt32(),"owner records exact source discarded target");
  Check(selected.Trace.Count(t=>t.Purpose.EndsWith("struggle-selection-target"))==1,"one selection draw in owner");
  var draw=Gen3Rng.Next(f.Rng,"selection");Check(((actor^1)|(draw.Value&2))==v.GetProperty("selection").GetInt32(),"original selection flank");
  var result=SourceMoveTargeting.ResolveCalled(f with{Rng=draw.State},new(actor),catalog.Moves[Id("STRUGGLE")]);
  Check(result.Succeeded&&result.Target!.Value.Value==v.GetProperty("target").GetInt32(),"original action target");
  Check(result.Rng.Seed==v.GetProperty("finalSeed").GetUInt32()&&result.Rng.Cursor==(ulong)v.GetProperty("draws").GetInt32(),"exact source target RNG");
 }
});
Test("one actor one side and both sides exhaust PP and complete actual shared turns",()=>{
 foreach(int[] empty in new[]{new[]{0},new[]{0,2},new[]{0,1,2,3}}){var f=Frame();foreach(int i in empty)f=Put(f,i,Empty(f.Slots[i].Body!));var r=Finish(Begin(f));
  Check(r.State.FourTurn is null&&r.State.Outcome==WildBattleOutcome.Ongoing,"clean ongoing shared turn");
  Check(r.Events.OfType<SourceMoveUsedEvent>().Where(e=>e.Move==Id("STRUGGLE")).Select(e=>e.Actor.Value).Order().SequenceEqual(empty),"exact exhausted actors use Struggle");
  foreach(int i in empty){Check(r.State.Slots[i].Body!.Pokemon.Moves.All(m=>m.CurrentPp==0),"zero PP never negative");var pp=r.Events.OfType<SourcePpCommandEvent>().Single(e=>e.Actor.Value==i);Check(pp.PpSlot==-1&&pp.Before==0&&pp.After==0&&pp.CommandCost==1,"no PP deduction");}
  Check(r.Trace.Count(t=>t.Purpose.EndsWith("struggle-selection-target"))==empty.Length,"one discarded selection draw each");
  Check(r.Events.OfType<SourceMoveUsedEvent>().Count()==4,"all four actual actions execute");
 }
});
Test("empty PP targets the sole far-side opponent and Pressure spends no learned PP",()=>{
 var f=Frame();f=Put(f,0,Empty(f.Slots[0].Body!));f=Put(f,1,f.Slots[1].Body! with{Pokemon=f.Slots[1].Body!.Pokemon with{CurrentHp=0}});f=Put(f,3,Mon("DUSCLOPS",100));
 f=f with{SourceAbsentMask=2,TurnStartAbsentMask=2};Check(f.Slots[3].Body!.Pokemon.AbilityId=="ABILITY_PRESSURE","authentic Pressure species");
 var picks=Choices(f);Check(picks[0].Target!.Value.Value==1,"factory retains absent opposite placeholder");var r=Finish(Begin(f,picks));
 Check(r.State.FourTurn is null&&r.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Actor.Value==0&&e.Target.Value==3&&e.HpAfter<e.HpBefore),"real Struggle hits only far live foe");
 var pp=r.Events.OfType<SourcePpCommandEvent>().Single(e=>e.Actor.Value==0);Check(pp.PpSlot==-1&&pp.Before==0&&pp.After==0&&pp.CommandCost==2,"Pressure command calculated but NO_PPDEDUCT suppresses cost");
 Check(r.State.Slots[0].Body!.Pokemon.Moves.All(m=>m.CurrentPp==0),"learned PP untouched");
});
Test("Disable forces Struggle while invalid and stale selections remain atomic",()=>{
 var f=Frame();var m=f.Slots[0].Body!;f=Put(f,0,m with{DisabledMove=m.Pokemon.Moves[0].Move,DisableTurnsRemaining=3});Check(SourceFourActionTurns.RequiresStruggle(catalog,f,new(0)),"Disable is actual exhaustion");var r=Finish(Begin(f));Check(r.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==0&&e.Move==Id("STRUGGLE")),"disabled actor struggles");
 var normal=Frame();var picks=Choices(normal);Atomic(normal,SourceFourActionTurns.BeginActions(catalog,normal,normal.Revision,picks.SetItem(0,SourceActionSelection.Struggle(normal.Slots[0].Occupant!.Value))),"positive PP cannot struggle");
 Atomic(normal,SourceFourActionTurns.BeginActions(catalog,normal,normal.Revision-1,picks),"stale revision");
 Atomic(normal,SourceFourActionTurns.BeginActions(catalog,normal,normal.Revision,picks.SetItem(0,picks[0] with{Actor=picks[0].Actor with{EntrySerial=8}})),"stale occupant");
 var mixed=Put(normal,0,Mon("PIDGEY",80,"TACKLE","DEFENSE_CURL"));mixed=Put(mixed,0,mixed.Slots[0].Body! with{Pokemon=mixed.Slots[0].Body!.Pokemon with{Moves=mixed.Slots[0].Body!.Pokemon.Moves.SetItem(0,mixed.Slots[0].Body!.Pokemon.Moves[0] with{CurrentPp=0})}});
 var invalid=Choices(mixed).SetItem(0,new(mixed.Slots[0].Occupant!.Value,SourceActionKind.Move,0,new(1)));Atomic(mixed,SourceFourActionTurns.BeginActions(catalog,mixed,mixed.Revision,invalid),"zero selected PP with another live move is rejected");
 var exhausted=Put(normal,0,Empty(normal.Slots[0].Body!));var wrong=Choices(exhausted).SetItem(0,SourceActionSelection.Struggle(exhausted.Slots[0].Occupant!.Value,new(2)));Atomic(exhausted,SourceFourActionTurns.BeginActions(catalog,exhausted,exhausted.Revision,wrong),"Struggle cannot request ally");
 var overflow=exhausted with{Rng=new(31,ulong.MaxValue)};Atomic(overflow,SourceFourActionTurns.BeginActions(catalog,overflow,overflow.Revision,Choices(overflow)),"RNG overflow");
 var begin=Begin(exhausted);var advanced=Next(begin);Atomic(advanced.State,SourceFourActionTurns.Advance(catalog,advanced.State,begin.Continuation!.Value),"stale continuation");
});
Test("original typeless physical hit and damage-dealt recoil including Rock Head and miss",()=>{
 foreach(string species in new[]{"PIDGEY","GEODUDE"}){var f=Frame([Empty(Mon(species,80)),Mon("GASTLY",100),Mon("SPEAROW"),Mon("GASTLY",100)]);var r=Finish(Begin(f));
  var hp=r.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Actor.Value==0);var recoil=r.Events.OfType<SourceRecoilEvent>().Single(e=>e.Actor.Value==0);
  int lost=hp.HpBefore-hp.HpAfter;Check(lost>0&&recoil.Damage==source.RootElement.GetProperty("recoil")[Math.Min(lost,255)].GetInt32(),"source damage-dealt quarter recoil");
  Check(!r.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Move==Id("STRUGGLE")),"Ghost immunity bypassed without STAB/type flags");
  var actor=f.Slots[0].Body!;var defender=f.Get(hp.Target).Body!;
  int defense=hp.Critical?defender.Pokemon.Stats.Defense:defender.Pokemon.Stats.Defense*3/2;
  int basis=Math.Max(1,actor.Pokemon.Stats.Attack*50*(2*actor.Pokemon.Level/5+2)/defense/50)+2;
  int expected=Math.Max(1,basis*(hp.Critical?2:1)*hp.RandomPercent/100);
  Check(hp.Damage==expected,"source physical neutral calculation skips Normal STAB and Ghost immunity");
 }
 bool missed=false;for(uint seed=0;seed<20&&!missed;seed++){var f=Put(Frame(seed:seed),0,Empty(Mon()) with{AccuracyStage=0});var r=Finish(Begin(f));if(r.Events.OfType<SourceMoveMissedEvent>().Any(e=>e.Actor.Value==0)){missed=true;Check(!r.Events.OfType<SourceRecoilEvent>().Any(e=>e.Actor.Value==0),"miss has no recoil");}}
 Check(missed,"miss fixture exercised");
});
Test("recoil KO pauses rewards then partner acts with replacement deferred to end turn",()=>{
 var user=Empty(Mon("PIDGEOT",100));user=user with{Pokemon=user.Pokemon with{CurrentHp=1}};var foe=Mon("GASTLY",1);foe=foe with{Pokemon=foe.Pokemon with{CurrentHp=1}};
 var f=Frame([user,foe,Mon("SLOWPOKE",50),Mon("SNORLAX",100)]);f=f with{Sides=f.Sides.SetItem(1,f.Sides[1] with{FollowMeTimer=1,FollowMeTarget=new(1)})};
 var reserve=Member(Mon("PIDGEY",50));f=f with{Parties=f.Parties.SetItem(0,f.Parties[0] with{Members=f.Parties[0].Members.Add(reserve)}),Rewards=f.Rewards! with{Metadata=f.Rewards.Metadata.Add(new(new(BattleSide.Player,2),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected))}};
 var start=Begin(f);Check(start.State.Queue[0].Slot.Value==0,"fixture fastest actor");var first=Next(start);
 Check(first.Events.OfType<SourceFaintedEvent>().Select(e=>e.Side.Value).SequenceEqual(new[]{0,1}),"recoil then target faint source script order");
 Check(first.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending&&first.State.PendingSwitches.IsEmpty&&first.State.DeferredKoVacancyMask==3,"reward pause preserves deferred vacancies");
 var rest=Finish(first);Check(rest.State.FourTurn!.Phase==SourceFourActionPhase.ReplacementsPending,"replacement only after partner action and end turn");
 Check(rest.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==2)&&!rest.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==1),"surviving partner acts fainted opponent does not");
 Check(rest.Events.OfType<SourceRewardAppliedEvent>().Any(e=>e.Recipient.Index==1)&&rest.State.Rewards!.CompletedOpponentMask==1,"surviving participant receives exact once reward");
 Check(rest.State.Slots[0].Member==f.Slots[0].Member&&rest.State.Slots[0].Body!.Pokemon.CurrentHp==0,"replacement not performed early");
});
Test("earned queued Struggle survives restoration and original chosen-position priority resumes",()=>{
 var f=Frame();var m=Empty(Mon("PIDGEY",80,"DEFENSE_CURL","QUICK_ATTACK"));f=Put(f,0,m);f=f with{Slots=f.Slots.SetItem(0,f.Slots[0] with{Registers=f.Slots[0].Registers with{LastMoveSlot=1}})};
 var begin=Begin(f);var restored=Put(begin.State,0,m with{Pokemon=m.Pokemon with{Moves=m.Pokemon.Moves.SetItem(0,m.Pokemon.Moves[0] with{CurrentPp=1})}});var r=Finish(begin with{State=restored});
 Check(r.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==0&&e.Move==Id("STRUGGLE")),"earned choice persists through later PP recovery");Check(r.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==1,"restored PP untouched");
 var order=r.Events.OfType<SourceOrderEvent>().Single(e=>e.Phase==WildBattleOrderPhase.EndTurn);Check(order.Order[0].Value==0,"retained Quick Attack priority after Struggle");
});
Test("forced Rollout and recharge retain their selected move despite zero PP",()=>{
 foreach(var fixture in new[]{("ROLLOUT",WildBattleCalledLockKind.Rollout,3),("HYPER_BEAM",WildBattleCalledLockKind.Recharge,1)}){
  var f=Frame();f=Put(f,0,Empty(Mon("PIDGEY",80,fixture.Item1)));
  var locked=new SourceTurnLock(fixture.Item2,Id(fixture.Item1),0,Id(fixture.Item1),new(1),fixture.Item3,0,null,false);
  f=f with{Slots=f.Slots.SetItem(0,f.Slots[0] with{Registers=f.Slots[0].Registers with{Turn=new(WildBattleRules.CalledMoveKernelRulesVersion,locked,0,null)}})};
  Check(!SourceFourActionTurns.RequiresStruggle(catalog,f,new(0)),"forced move does not become Struggle");
  var picks=Choices(f);Check(picks[0].MoveSlot==0,"actual forced position");
  Atomic(f,SourceFourActionTurns.BeginActions(catalog,f,f.Revision,picks.SetItem(0,SourceActionSelection.Struggle(f.Slots[0].Occupant!.Value))),"forced -1 rejected");
  var r=Finish(Begin(f));Check(!r.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Move==Id("STRUGGLE")),"forced/recharge execution preserved");
 }
});
Test("authentic Twins one enemy or both exhausted skips scorer and uses actual Struggle",()=>{
 var definition=PretRoute8Twins.Load(root,root);
 foreach(int[] empty in new[]{new[]{1},new[]{3},new[]{1,3}}){var encounter=SourceTwinsTrainerEncounter.Begin(catalog,definition,[Member(Mon("PIDGEY",100)),Member(Mon("SPEAROW",100))],new(31));Check(encounter.Succeeded,encounter.Fault??"Twins entry");var f=encounter.State!;
  f=f with{Rewards=Frame().Rewards};foreach(int i in empty)f=Put(f,i,Empty(f.Slots[i].Body!));
  var ai=SourceTwinsTrainerEncounter.ChooseEnemyActions(catalog,f,f.Revision);Check(ai.Succeeded,ai.Fault??"Twins AI");Check(ai.Choices.Where(c=>c.Selection.IsStruggle).Select(c=>c.Actor.Value).SequenceEqual(empty),"actual exhausted enemy selection");
  foreach(var choice in ai.Choices.Where(c=>c.Selection.IsStruggle)){Check(choice.Scores.IsEmpty&&choice.SimulatedDamagePercent.IsEmpty&&choice.AbilityReads.IsEmpty,"no fictional AI scores");Check(!ai.Trace.Any(t=>t.Purpose.Contains("twins.ai.battler."+choice.Actor.Value+".")),"no controller setup draws");}
  Check(ai.Trace.Count(t=>t.Purpose.EndsWith("struggle-selection-target"))==empty.Length,"one source no-move draw each");
  var selected=Choices(ai.State).Where(c=>c.Actor.Slot.Side==BattleSide.Player).Concat(ai.Selections).ToImmutableArray();var begin=Begin(ai.State,selected);Check(!begin.Trace.Any(t=>t.Purpose.EndsWith("struggle-selection-target")),"prepared AI target never drawn twice");var r=Finish(begin);
  Check(r.Events.OfType<SourceMoveUsedEvent>().Count(e=>e.Move==Id("STRUGGLE"))==empty.Length,"real enemy Struggle executed");foreach(int i in empty)Check(r.State.Slots[i].Body!.Pokemon.Moves.All(m=>m.CurrentPp==0),"Twins empty PP retained");
  if(empty.Length==2)Check(ai.Trace.Length==2&&ai.State.Rng.Cursor==f.Rng.Cursor+2,"both exhausted exact selection RNG");
  var bad=SourceTwinsTrainerEncounter.ChooseEnemyActions(catalog,f,f.Revision-1);Check(!bad.Succeeded&&ReferenceEquals(bad.State,f)&&bad.Trace.IsEmpty&&bad.Choices.IsEmpty,"Twins stale atomic");
 }
});
Test("both sides exhausted retain exact source RNG for the selected controller-ready schedule",()=>{
 var definition=PretRoute8Twins.Load(root,root);
 foreach(var v in source.RootElement.GetProperty("schedule").EnumerateArray()){
  var initial=SourceTwinsTrainerEncounter.Begin(catalog,definition,[Member(Mon("PIDGEY",100)),Member(Mon("SPEAROW",100))],new(31));Check(initial.Succeeded,initial.Fault??"entry");
  var f=initial.State! with{Rng=new(v.GetProperty("seed").GetUInt32())};for(int slot=0;slot<4;slot++)f=Put(f,slot,Empty(f.Slots[slot].Body!));
  var ai=SourceTwinsTrainerEncounter.ChooseEnemyActions(catalog,f,f.Revision);Check(ai.Succeeded,ai.Fault??"both-empty AI");
  var selections=Choices(ai.State).Where(c=>c.Actor.Slot.Side==BattleSide.Player).Concat(ai.Selections).ToImmutableArray();var begin=Begin(ai.State,selections);
  var trace=ai.Trace.AddRange(begin.Trace);int[] readyOrder=[1,3,0,2];
  Check(trace.Select(t=>t.Purpose).SequenceEqual(readyOrder.Select(i=>$"source-battle.slot-{i}.struggle-selection-target")),"explicit source-ready order enemy1 enemy3 player0 player2 before sorting");
  Check(begin.State.Rng.Seed==v.GetProperty("finalSeed").GetUInt32()&&begin.State.Rng.Cursor==(ulong)v.GetProperty("draws").GetInt32(),"exact original C schedule final RNG");
  for(int i=0;i<4;i++)Check(begin.State.Queue.Single(a=>a.Slot.Value==readyOrder[i]).Target!.Value.Value==v.GetProperty("targets")[i].GetInt32(),"all four original discarded targets");
 }
});
File.WriteAllText(args[1],Json(new{groups,checks,failures,legacySnapshots=snapshots.Count,results}));
Console.WriteLine(Json(new{groups,checks,failures,legacySnapshots=snapshots.Count}));return groups<9||checks<500||failures>0?1:0;
