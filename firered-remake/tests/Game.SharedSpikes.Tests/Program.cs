using System.Collections.Immutable;
using System.Text.Json;
using System.Security.Cryptography;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

if(args.Length != 3) return 2;
var proof=SourceMechanicsCatalogLoader.LoadAuthenticated(args[0]); var catalog=proof.Catalog;
int groups=0,checks=0,failures=0,turns=0;
void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}
void Test(string name,Action f){groups++;try{f();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+" "+e);}}
string J<T>(T x)=>JsonSerializer.Serialize(x);
MoveId Id(string n)=>new("pret:MOVE_"+n);
WildBattleCombatant Mon(string species="DROWZEE",string moves="MEDITATE",int level=70,int? hp=null,uint pid=0){
 var p=SourcePokemonFactory.Create(proof,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(pid)),new(0)).Pokemon;
 foreach(var m in moves.Split(','))Check(catalog.Species[p.Species].LevelUpMoves.Any(x=>x.Move==Id(m)&&x.Level<=level),"natural move "+species+" "+m);
 return new(p with{CurrentHp=hp??p.CurrentHp,Moves=moves.Split(',').Select(m=>new PokemonMoveSlot(Id(m),catalog.Moves[Id(m)].Pp,catalog.Moves[Id(m)].Pp)).ToImmutableArray()});
}
WildBattleCalledPartyMember Member(WildBattleCombatant m)=>new(m.Pokemon,m.HeldItem,m.Status);
SourceBattleFrame Frame(WildBattleCombatant[]? mons=null,WildBattleCombatant[]? players=null,WildBattleCombatant[]? opponents=null){
 var m=mons??[Mon("SKARMORY","SPIKES"),Mon(),Mon(),Mon()];
 var p=new[]{Member(m[0]),Member(m[2])}.Concat((players??[]).Select(Member)).ToImmutableArray();
 return new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,SourceMechanicsRulesVersion=SourceBattleKernel.Version,
 BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),Parties=[new(BattleSide.Player,p),new(BattleSide.Opponent,new[]{Member(m[1]),Member(m[3])}.Concat((opponents??[]).Select(Member)).ToImmutableArray())],
 Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,m[i])).ToImmutableArray(),
 Rewards=new(){Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_ROUTE_1"),Metadata=Enumerable.Range(0,p.Length).Select(i=>new SourceRewardMetadata(new(BattleSide.Player,i),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()}};
}
SourceFourActionResult Good(SourceFourActionResult r){Check(r.Succeeded,r.Fault??"success");Check(SourceBattleIdentityRules.Validate(r.State)==null,"identity "+SourceBattleIdentityRules.Validate(r.State));return r;}
SourceBattleFrame Entry(SourceBattleFrame f)=>Good(SourceFourEntryAbilities.InitializeEntry(catalog,f,f.Revision)).State;
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame f,int[]? moves=null)=>f.Slots.Where(c=>f.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,moves?[c.Slot.Value]??0,new(catalog.Moves[c.Body!.Pokemon.Moves[moves?[c.Slot.Value]??0].Move].Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot.Value:c.Slot.Value^1))).ToImmutableArray();
SourceActionSelection Switch(SourceBattleFrame f,int slot,int index)=>new(f.Slots[slot].Occupant!.Value,SourceActionKind.Switch,Incoming:new((BattleSide)(slot&1),index));
SourceFourActionResult Begin(SourceBattleFrame f,ImmutableArray<SourceActionSelection>? choices=null)=>Good(SourceFourActionTurns.BeginActions(catalog,f,f.Revision,choices??Choices(f)));
SourceFourActionResult Next(SourceFourActionResult r)=>Good(SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value));
SourceFourActionResult Choose(SourceFourActionResult r,int slot,int index)=>Good(SourceFourActionTurns.ChooseReplacement(catalog,r.State,r.Continuation!.Value,new(slot),new((BattleSide)(slot&1),index)));
SourceFourActionResult Baton(SourceFourActionResult r,int slot,int index)=>Good(SourceFourActionTurns.ChooseActionSwitch(catalog,r.State,r.Continuation!.Value,new(slot),new((BattleSide)(slot&1),index)));
SourceBattleFrame Body(SourceBattleFrame f,int slot,WildBattleCombatant b){var c=f.Slots[slot];var key=c.Member!.Value;var party=f.Party(key.Side);return f with{Slots=f.Slots.SetItem(slot,c with{Body=b}),Parties=f.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,Member(b))})};}
SourceBattleFrame Hazard(SourceBattleFrame f,BattleSide side,int layers)=>f with{Sides=f.Sides.SetItem((int)side,f.Sides[(int)side] with{Spikes=layers})};
void Atomic(SourceBattleFrame f,SourceFourActionResult r,string name)=>Check(!r.Succeeded&&ReferenceEquals(r.State,f)&&r.Events.IsEmpty&&r.Trace.IsEmpty,"atomic "+name+" "+r.Fault);
(SourceFourActionResult Result,ImmutableArray<SourceBattleEvent> Events) Finish(SourceFourActionResult r){var events=r.Events.ToBuilder();for(int i=0;i<40&&r.State.FourTurn!=null&&r.State.Outcome==WildBattleOutcome.Ongoing;i++){
 if(r.State.Rewards?.Pending is{} p)r=Good(SourceFourRewards.Learn(catalog,r.State,r.Continuation!.Value,new(p.Recipient,p.Moves[0].Level,p.Moves[0].Move,null)));
 else if(r.State.FourTurn.Phase==SourceFourActionPhase.ReplacementsPending){var p2=r.State.PendingSwitches[0];r=Choose(r,p2.Slot.Value,p2.LegalPartyIndices[0]);}
 else r=Next(r);events.AddRange(r.Events);
 }Check(r.CompletedCleanTurn||r.State.Outcome!=WildBattleOutcome.Ongoing,"completed actual turn or battle");turns++;return(r,events.ToImmutable());}
SourceFourActionResult Until(SourceFourActionResult r,Func<SourceFourActionResult,bool> stop){for(int i=0;i<24&&!stop(r);i++)r=Next(r);Check(stop(r),"reached actual boundary");return r;}

Test("compiled original C differential layer and Pressure samples",()=>{
 using var doc=JsonDocument.Parse(File.ReadAllText(args[2]));var root=doc.RootElement;
 Check(root.GetProperty("entry_samples").GetArrayLength()==216&&root.GetProperty("set_samples").GetArrayLength()==1080,"exact source sample counts");
 Check(root.GetProperty("passed").GetBoolean()&&root.GetProperty("total_vectors").GetInt32()==7221510,"nonempty pinned extracted oracle");
 foreach(var sample in root.GetProperty("entry_samples").EnumerateArray()){
 var x=sample.GetProperty("input");int damage=sample.GetProperty("output").GetProperty("damage").GetInt32();
 if(damage>0)Check(WildBattleSourcePartyEffects.SpikesDamage(x.GetProperty("maxHP").GetInt32(),x.GetProperty("layers").GetInt32())==damage,"source C fraction");
 }
 foreach(var sample in root.GetProperty("set_samples").EnumerateArray()){
 var x=sample.GetProperty("input");var expected=sample.GetProperty("output");int actor=x.GetProperty("attacker").GetInt32(),count=x.GetProperty("battlersCount").GetInt32(),mask=x.GetProperty("pressureMask").GetInt32(),layer=x.GetProperty("layers").GetInt32(),markers=x.GetProperty("markerFlags").GetInt32();
 var flags=WildBattleInvocationFlags.None;if((markers&1)!=0)flags|=WildBattleInvocationFlags.NoPpDeduct;if((markers&2)!=0)flags|=WildBattleInvocationFlags.NoAttackString;
 if(layer==3||x.GetProperty("noPressure").GetInt32()!=0)flags|=WildBattleInvocationFlags.PpNotAffectedByPressure;
 int opposite=Enumerable.Range(0,count).Count(i=>(i&1)!=(actor&1)&&(mask&(1<<i))!=0);
 var reduced=WildBattleSourceCommands.ReducePp(x.GetProperty("pp").GetInt32(),"MOVE_TARGET_OPPONENTS_FIELD",new BattlerSlot(actor),new BattlerSlot(actor^1),"ABILITY_NONE",opposite,0,flags);
 Check(reduced.After==expected.GetProperty("pp").GetInt32(),"source C pressure cost/suppression");
 }
});
Test("original layer fractions and minimum one",()=>{foreach(int hp in Enumerable.Range(1,1024))foreach(int layer in new[]{1,2,3})Check(WildBattleSourcePartyEffects.SpikesDamage(hp,layer)==Math.Max(1,hp/(new[]{8,6,4}[layer-1])),"source integer fraction");});
Test("real setter shares opposing side, cap spends PP, no hazard RNG or accuracy",()=>{
 var f=Entry(Frame());for(int layer=1;layer<=4;layer++){
 var (r,ev)=Finish(Begin(f));f=r.State;Check(f.Sides[1].Spikes==Math.Min(3,layer)&&f.Sides[0].Spikes==0,"side layers");
 Check(ev.OfType<SourcePpCommandEvent>().Single(x=>x.Actor.Value==0).CommandCost==1,"base PP");
 var added=ev.OfType<SourceCalledSpikesChangedEvent>().ToArray();Check(added.Length==(layer<4?1:0),"exactly one layer event");
 if(layer<4){Check(added[0].Sequence<ev.OfType<SourceAttackStringEvent>().Single(x=>x.Actor.Value==0).Sequence,"set before print/PP");
 Check(ev.OfType<SourceMtMoonVolatileEvent>().Single(x=>x.Volatile=="spikes"&&x.Reason=="move").Sequence>ev.OfType<SourcePpCommandEvent>().Single(x=>x.Actor.Value==0).Sequence,"Spikes printed result follows actual attack/PP commands");}
 else Check(ev.OfType<SourceMoveFailedEvent>().Any(x=>x.Reason=="spikes-at-maximum"),"source cap failure");
 Check(ev.OfType<SourceHpChangedEvent>().Count()==0,"no damage to existing occupants");
 }Check(f.Slots[0].Body!.Pokemon.Moves[0].CurrentPp==catalog.Moves[Id("SPIKES")].Pp-4,"four actual PP commands");
});
Test("two opposite Pressure holders count, same-side Pressure excluded and cap suppresses",()=>{
 var f=Entry(Frame([Mon("SKARMORY","SPIKES"),Mon("LUGIA","RECOVER"),Mon("LUGIA","RECOVER"),Mon("LUGIA","RECOVER")]));
 var begin=Begin(f);var r=Until(begin,x=>x.Events.OfType<SourceCalledSpikesChangedEvent>().Any());
 Check(r.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==3&&r.Trace.IsEmpty,"two opponent Pressure no RNG");
 f=Finish(r).Result.State;f=Hazard(f,BattleSide.Opponent,3);r=Until(Begin(f),x=>x.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="spikes-at-maximum"));
 Check(r.Events.OfType<SourcePpCommandEvent>().Single().CommandCost==1&&r.Trace.IsEmpty,"cap suppresses pressure");Finish(r);
});
Test("voluntary grounded/Flying/Levitate/Trace entries use actual incoming effective ability",()=>{
 foreach(var (species,move,immune) in new[]{("DIGLETT","SAND_ATTACK",false),("GYARADOS","DRAGON_DANCE",true),("KOFFING","SMOKESCREEN",true),("PORYGON","AGILITY",false)})foreach(var layers in new[]{1,2,3}){
 var incoming=Mon(species,move);var f=Entry(Frame(players:[incoming]));f=Hazard(f,BattleSide.Player,layers);var r=Next(Begin(f,Choices(f).SetItem(0,Switch(f,0,2))));
 var hp=r.Events.OfType<SourceHpChangedEvent>().Where(x=>x.Move==Id("SPIKES")).ToArray();Check(hp.Length==(immune?0:1),"immunity "+species);
 var text=r.Events.OfType<SourceMtMoonVolatileEvent>().Where(x=>x.Volatile=="spikes"&&x.Reason=="entry").ToArray();
 Check(text.Length==(immune?0:1)&&(immune||text[0].Sequence>hp[0].Sequence),"entry result follows actual HP, immune entry is silent");
 Check(r.State.Slots[0].Body!.Pokemon.CurrentHp==incoming.Pokemon.CurrentHp-(immune?0:WildBattleSourcePartyEffects.SpikesDamage(incoming.Pokemon.Stats.MaxHp,layers)),"entry HP");
 Check(r.State.Party(BattleSide.Player).Members[2].Pokemon.CurrentHp==r.State.Slots[0].Body!.Pokemon.CurrentHp,"permanent HP sync");
 Check(r.State.Slots[0].Registers.Damage is null&&r.State.Sides[0].Spikes==layers,"passive damage and persistent layers");
 if(species=="PORYGON")Check(hp[0].Sequence<r.Events.OfType<SourceAbilityActivatedEvent>().Single(x=>x.Ability=="ABILITY_TRACE").Sequence,"Spikes before Trace");Finish(r);
 }
});
Test("two same-side actual switches each take hazard once before queued moves",()=>{
 var f=Entry(Frame(players:[Mon(),Mon()]));f=Hazard(f,BattleSide.Player,2);var r=Begin(f,Choices(f).SetItem(0,Switch(f,0,2)).SetItem(2,Switch(f,2,3)));var a=Next(r);var b=Next(a);
 Check(a.Events.OfType<SourceHpChangedEvent>().Single(x=>x.Move==Id("SPIKES")).Target.Value==0&&b.Events.OfType<SourceHpChangedEvent>().Single(x=>x.Move==Id("SPIKES")).Target.Value==2,"side latch cleared per real entrant");Finish(b);
});
Test("Baton keeps stages, takes passive damage and preserves serialized continuation",()=>{
 var f=Entry(Frame([Mon("GOREBYSS","AMNESIA,BATON_PASS"),Mon(),Mon(),Mon()],[Mon()]));f=Finish(Begin(f)).Result.State;f=Hazard(f,BattleSide.Player,3);
 var r=Until(Begin(f,Choices(f,[1,0,0,0])),x=>x.State.FourTurn?.Phase==SourceFourActionPhase.ActionSwitchPending);var json=JsonSerializer.Deserialize<SourceBattleFrame>(J(r.State))!;
 var a=Baton(r,0,2);var b=Good(SourceFourActionTurns.ChooseActionSwitch(catalog,json,r.Continuation!.Value,new(0),new(BattleSide.Player,2)));
 Check(J(a)==J(b),"exact restore state events RNG");Check(a.State.Slots[0].Body!.SpecialDefenseStageDelta==2,"passed stages");Check(a.Events.OfType<SourceHpChangedEvent>().Single().Move==Id("SPIKES"),"entry damage");
 Check(a.State.Slots[0].Registers.History!.LastResultingMove==Id("NONE")&&a.State.Slots[0].Registers.Damage is null,"source NONE after switch passive damage");
 Atomic(a.State,SourceFourActionTurns.ChooseActionSwitch(catalog,a.State,r.Continuation!.Value,new(0),new(BattleSide.Player,2)),"stale Baton");Finish(a);
});
Test("Roar entry damage precedes weather and invalidates target old action",()=>{
 var f=Entry(Frame([Mon("GROWLITHE","ROAR",90),Mon("GROWLITHE","ROAR",40),Mon(),Mon()],opponents:[Mon("KYOGRE","CALM_MIND")]));f=Hazard(f,BattleSide.Opponent,3);
 var r=Until(Begin(f),x=>x.Events.OfType<SourceForcedOutEvent>().Any());var hp=r.Events.OfType<SourceHpChangedEvent>().Single(x=>x.Move==Id("SPIKES"));
 Check(hp.Target.Value==1&&hp.Sequence<r.Events.OfType<SourcePermanentWeatherChangedEvent>().Single().Sequence,"forced hazard before Drizzle");Check(r.State.Queue.Single(x=>x.Slot.Value==1).Kind==SourceActionKind.Cancelled,"old action cannot migrate");Check(r.Events.OfType<SourceCalledPartyChangedEvent>().Count(x=>x.Side==BattleSide.Opponent&&x.PartyIndex==2)==1,"forced entrant permanent HP reported once");Finish(r);
});
Test("voluntary hazard KO skips Intimidate, records incoming identity and waits until turn end",()=>{
 var incoming=Mon("GROWLITHE","LEER",hp:1);var f=Entry(Frame(players:[incoming]));f=Hazard(f,BattleSide.Player,1);
 var r=Next(Begin(f,Choices(f).SetItem(0,Switch(f,0,2))));Check(r.State.Slots[0].Body!.Pokemon.CurrentHp==0&&r.State.FourTurn!.Defeats.Single().Occupant.Member.Index==2,"new identity defeated");
 Check(r.State.DeferredKoVacancyMask==1&&r.State.PendingSwitches.IsEmpty&&r.State.FourTurn!.Phase==SourceFourActionPhase.Actions,"approved delayed KO entry");
 Check(!r.Events.OfType<SourceAbilityActivatedEvent>().Any()&&r.Events.OfType<SourceFaintedEvent>().Count()==1,"no entry ability after KO");
 var (done,events)=Finish(r);Check(done.State.Slots[0].Member!.Value.Index==0,"real departing healthy reserve used at end turn");Check(events.OfType<SourceMoveUsedEvent>().Any(x=>x.Actor.Value==2),"partner completes queued action");
});
Test("Baton hazard KO completes action and gives partner its turn before replacement",()=>{
 var f=Entry(Frame([Mon("GIRAFARIG","BATON_PASS"),Mon(),Mon(),Mon()],[Mon("GROUDON","BULK_UP",hp:1)]));f=Hazard(f,BattleSide.Player,3);
 var r=Until(Begin(f),x=>x.State.FourTurn?.Phase==SourceFourActionPhase.ActionSwitchPending);r=Baton(r,0,2);
 Check(r.State.DeferredKoVacancyMask==1&&r.State.PendingSwitches.IsEmpty&&r.State.Field.PermanentWeather is null,"KO before Drought; no premature choice");Finish(r);
});
Test("forced hazard KO settles incoming opponent EXP once before remaining actions",()=>{
 var f=Entry(Frame([Mon("GROWLITHE","ROAR",90),Mon(),Mon(),Mon()],opponents:[Mon("GROUDON","BULK_UP",hp:1)]));f=Hazard(f,BattleSide.Opponent,3);
 var r=Until(Begin(f),x=>x.Events.OfType<SourceForcedOutEvent>().Any());Check(r.State.FourTurn!.Defeats.Single().Occupant.Member.Index==2&&r.State.FourTurn.Phase==SourceFourActionPhase.RewardsPending,"reward new opponent identity");
 Check(r.State.Field.PermanentWeather is null&&r.Events.OfType<SourceFaintedEvent>().Count()==1,"no Drought and exactly one faint");var (done,ev)=Finish(r);
 Check((done.State.Rewards!.CompletedOpponentMask&4)!=0&&ev.OfType<SourceOpponentRewardCompletedEvent>().Count(x=>x.Defeated.Member.Index==2)==1,"incoming reward once");
});
Test("end-turn hazard KO chain preserves residual cursor and takes distinct entrants once",()=>{
 var doomed=Mon(hp:1) with{Status=WildBattleStatus.Poison};var f=Entry(Frame([doomed,Mon(),Mon(),Mon()],[Mon("SLAKOTH","AMNESIA",hp:1),Mon()]));f=Hazard(f,BattleSide.Player,3);f=f with{Field=new(){RainTurnsRemaining=3}};
 var r=Until(Begin(f),x=>x.State.FourTurn?.Phase==SourceFourActionPhase.ReplacementsPending);var cursor=r.State.FourTurn!.EndTurn;Check(r.State.Field.RainTurnsRemaining==2,"one ordinary residual pass");
 r=Choose(r,0,2);Check(r.State.Slots[0].Body!.Pokemon.CurrentHp==0&&!r.State.Slots[0].Body!.TruantLoafing&&r.State.PendingSwitches[0].LegalPartyIndices.SequenceEqual(new[]{3}),"entry KO before Truant then next actual reserve");
 Check(r.State.FourTurn!.EndTurn==cursor&&r.State.Field.RainTurnsRemaining==2,"same immutable residual cursor");
 var restored=JsonSerializer.Deserialize<SourceBattleFrame>(J(r.State))!;var a=Choose(r,0,3);var b=Good(SourceFourActionTurns.ChooseReplacement(catalog,restored,r.Continuation!.Value,new(0),new(BattleSide.Player,3)));Check(J(a)==J(b),"serialized chain exact");
 var done=Finish(a).Result;Check(done.State.Field.RainTurnsRemaining==2&&done.State.Turn==1&&done.State.Sides[0].Spikes==3,"no residual replay and hazards persist");
});
Test("hazard KO of final side member ends battle without extra entry or ability",()=>{
 var dead=Mon(hp:0);var low=Mon(hp:1) with{Status=WildBattleStatus.Poison};var f=Frame([low,Mon(),dead,Mon()],[Mon("GROUDON","BULK_UP",hp:1)]);f=f with{SourceAbsentMask=4};f=Entry(f);f=Hazard(f,BattleSide.Player,3);
 var r=Until(Begin(f),x=>x.State.FourTurn?.Phase==SourceFourActionPhase.ReplacementsPending);r=Choose(r,0,2);
 Check(r.State.Outcome==WildBattleOutcome.Lost&&r.State.FourTurn!.Phase==SourceFourActionPhase.BattleEnded&&r.State.PendingSwitches.IsEmpty,"terminal owner outcome");
 Check(r.Events.OfType<SourceEndedEvent>().Count()==1&&r.State.Field.PermanentWeather is null,"one end event without Drought");Atomic(r.State,SourceFourActionTurns.Advance(catalog,r.State,r.Continuation!.Value),"terminal advance");
});
Test("initial hazards, invalid layers, unsupported entries and counter overflow remain atomic",()=>{
 var raw=Hazard(Frame(players:[Mon()]),BattleSide.Player,1);Atomic(raw,SourceFourEntryAbilities.InitializeEntry(catalog,raw,0),"initial hazards");
 var f=Entry(Frame(players:[Mon()]));f=Hazard(f,BattleSide.Player,1);
 foreach(var bad in new[]{f with{EventSequence=long.MaxValue},f with{Revision=long.MaxValue},Hazard(f,BattleSide.Player,4),Body(f,0,f.Slots[0].Body! with{Rooted=true})})
 Atomic(bad,SourceFourActionTurns.BeginActions(catalog,bad,bad.Revision,Choices(bad).SetItem(0,Switch(bad,0,2))),"bad state or exhausted counter");
 var r=Begin(f,Choices(f).SetItem(0,Switch(f,0,2)));var over=r.State with{EventSequence=long.MaxValue-2};
 Atomic(over,SourceFourActionTurns.Advance(catalog,over,new(over.Revision,over.EventSequence,over.NextAction,over.FourTurn!.Phase)),"entry event overflow");
 var invalidReserve=Mon() with{HeldItem="ITEM_SITRUS_BERRY"};var p=f.Party(BattleSide.Player);var unsupported=f with{Parties=f.Parties.SetItem(0,p with{Members=p.Members.SetItem(2,Member(invalidReserve))})};
 var chosen=Begin(unsupported,Choices(unsupported).SetItem(0,Switch(unsupported,0,2)));Atomic(chosen.State,SourceFourActionTurns.Advance(catalog,chosen.State,chosen.Continuation!.Value),"unsupported incoming item");
});

Test("real same-turn Spikes then partner Roar runs one shared side hazard",()=>{
 var f=Entry(Frame([Mon("SKARMORY","SPIKES"),Mon(),Mon("GROWLITHE","ROAR"),Mon()],opponents:[Mon()]));
 var (done,ev)=Finish(Begin(f));var layer=ev.OfType<SourceCalledSpikesChangedEvent>().Single();var hp=ev.OfType<SourceHpChangedEvent>().Single(x=>x.Move==Id("SPIKES"));
 Check(layer.Sequence<hp.Sequence&&hp.Target.Value==3&&done.State.Sides[1].Spikes==1,"actual field move then forced far-side entry");
});
Test("Spikes can fill opposing field while both targets await approved delayed replacements",()=>{
 var f=Entry(Frame([Mon("SKARMORY","SPIKES",100),Mon(hp:1),Mon("ELECTRODE","EXPLOSION",100),Mon(hp:1)],opponents:[Mon(),Mon()]));
 var r=Next(Begin(f));Check(r.Events.OfType<SourceFaintedEvent>().Count()==3&&r.State.Slots[0].Body!.Pokemon.CurrentHp>0,"actual Explosion KOs both foes and its user");
 while(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending)r=Next(r);
 Check(r.State.DeferredKoVacancyMask==14&&r.State.PendingSwitches.IsEmpty,"both opponents await end-turn entry");
 r=Until(r,x=>x.Events.OfType<SourceCalledSpikesChangedEvent>().Any());Check(r.State.Sides[1].Spikes==1&&!r.Events.OfType<SourceMoveFailedEvent>().Any(x=>x.Reason=="deferred-ko-no-target"),"field has no live-target requirement");Finish(r);
});
Test("opponent end-turn hazard KO chains EXP and final win before exposing readiness",()=>{
 foreach(bool final in new[]{false,true}){
 var dead=Mon(hp:0);var low=Mon(hp:1) with{Status=WildBattleStatus.Poison};var reserves=final?new[]{Mon("GROUDON","BULK_UP",hp:1)}:new[]{Mon("GROUDON","BULK_UP",hp:1),Mon()};
 var f=Frame([Mon("SKARMORY","SPIKES"),low,Mon(),dead],opponents:reserves);f=Entry(f with{SourceAbsentMask=8});f=Hazard(f,BattleSide.Opponent,3);f=f with{Field=new(){RainTurnsRemaining=3}};
 var r=Until(Begin(f),x=>x.State.FourTurn?.Phase==SourceFourActionPhase.ReplacementsPending);var cursor=r.State.FourTurn!.EndTurn;
 Check(r.State.Rewards!.CompletedOpponentMask==1,"first opposing death already rewarded");r=Choose(r,1,2);
 Check(r.State.FourTurn!.Phase==SourceFourActionPhase.RewardsPending&&r.State.FourTurn.Defeats.Single().Occupant.Member.Index==2,"new entry death rewards its own member");
 Check(r.State.Outcome==WildBattleOutcome.Ongoing&&!r.Events.OfType<SourceEndedEvent>().Any(),"EXP precedes terminal outcome");
 Check(r.State.Field.PermanentWeather is null&&r.State.FourTurn.EndTurn==cursor,"no Drought or residual cursor replay");
 var (done,ev)=Finish(r);Check(done.State.Rewards!.CompletedOpponentMask==5&&ev.OfType<SourceOpponentRewardCompletedEvent>().Count(x=>x.Defeated.Member.Index==2)==1,"no duplicate hazard EXP");
 Check(final?done.State.Outcome==WildBattleOutcome.Won&&ev.OfType<SourceEndedEvent>().Count()==1:done.CompletedCleanTurn&&done.State.Slots[1].Member!.Value.Index==3,"correct exhaustion or final surviving entry");
 Check(done.State.Field.RainTurnsRemaining==2,"rain ticks once across chain");
 }
});
Test("incoming Trace takes hazard before copying opponent Levitate",()=>{
 var incoming=Mon("PORYGON","AGILITY");var f=Entry(Frame([Mon("SKARMORY","SPIKES"),Mon("KOFFING","TACKLE"),Mon(),Mon("KOFFING","TACKLE")],[incoming]));f=Hazard(f,BattleSide.Player,2);
 var r=Next(Begin(f,Choices(f).SetItem(0,Switch(f,0,2))));Check(r.State.Slots[0].Body!.Pokemon.CurrentHp==incoming.Pokemon.CurrentHp-incoming.Pokemon.Stats.MaxHp/6,"Trace has no pre-entry copied immunity");
 Check(WildBattleEffective.Ability(r.State.Slots[0].Body!)=="ABILITY_LEVITATE"&&r.Events.OfType<SourceHpChangedEvent>().Single().Sequence<r.Events.OfType<SourceCalledOverlayChangedEvent>().Single().Sequence,"then copies actual Levitate");Finish(r);
});
Test("field setting preserves resolved far target for PP and source move history",()=>{
 var f=Frame([Mon("SKARMORY","SPIKES"),Mon(hp:0),Mon(),Mon()]);f=Entry(f with{SourceAbsentMask=2});
 var r=Until(Begin(f),x=>x.Events.OfType<SourceCalledSpikesChangedEvent>().Any());Check(r.Events.OfType<SourcePpCommandEvent>().Single().Target.Value==3,"source resolved target is retained");
 Check(r.State.Slots[3].Body!.LastLandedMove==Id("SPIKES")&&r.State.Slots[1].Body!.LastLandedMove is null,"only actual target move history changes");Finish(r);
});
var result=new{groups,checks,failures,turns,runtime=Environment.Version.ToString()};File.WriteAllText(args[1],J(result));Console.WriteLine(J(result));return failures==0&&groups>0&&checks>0?0:1;
