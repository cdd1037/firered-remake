using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Capture;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.WildBattle;
using Game.Domain.World;

if(args.Length!=3)throw new ArgumentException("CONTENT_ROOT CURRENT_CHECKPOINT OUTPUT_JSON");
var content=PretCampaignCatalog.LoadCurrent(args[0]);var validator=CampaignSession.CreateCurrent(content);
var checkpoint=SaveCodec.Decode<CampaignState>(File.ReadAllBytes(args[1]),validator.RulesVersion,content.ContentVersion,validate:validator.ValidatePlayerSave);
var mechanics=content.CelioOwnership!.Mechanics;var catalog=mechanics.Catalog;
int checks=0,commands=0;var cases=new List<object>();
void Check(bool ok,string why){checks++;if(!ok)throw new InvalidDataException(why);}
void Test(string name,Action run){if(Environment.GetEnvironmentVariable("FIRERED_CANONICAL_WILD_CASE") is {Length:>0} selected&&!name.StartsWith(selected,StringComparison.Ordinal))return;run();cases.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}
void Send(CampaignSession session,CampaignCommand command)
{
    string id="canonical-wild:"+commands++;long revision=session.Revision;var before=session.State;var result=session.Execute(id,revision,command);
    if(result.Status!=CommandStatus.Accepted)throw new InvalidDataException(command+" => "+result.Error);
    var stale=session.Execute(id,revision,command);
    Check(stale.Status==CommandStatus.Rejected&&stale.Error=="stale_revision"&&ReferenceEquals(stale.State,result.State),"atomic stale command");
    if(session.State.WildBattle?.Frame is {} frame)
    {
        Check(SourceBattleIdentityRules.Validate(frame) is null,"frame structural identity");
        Check(typeof(SourceBattleFrame).GetProperty("WildTurn")?.GetValue(frame) is null&&typeof(SourceBattleFrame).GetProperty("SinglesEncoding")?.GetValue(frame) is null,"no competing singles owner");
        Check(frame.Party(BattleSide.Player).Members.Select(m=>CanonicalJson.Hash(m.Pokemon)).SequenceEqual(session.State.Party.Select(p=>CanonicalJson.Hash(p.Pokemon))),"canonical party projection");
    }
}
void Drain(CampaignSession session,bool stopLearning=false,bool nickname=false,bool stopCapture=false)
{
    for(int n=0;n<1500;n++)
    {
        if(session.State.Evolution is {} evolution){Send(session,evolution.Stage==CampaignEvolutionStage.Proposal?new("evolution_choice",Choice:0):new("ack"));continue;}
        if(session.State.WildBattle is not {} b)return;
        if(b.Capture is {} c)
        {
            if(stopCapture)return;
            if(c.Stage==CampaignCaptureStage.Naming)Send(session,new("nickname",Text:"CANON"));
            else if(c.Stage==CampaignCaptureStage.NicknameChoice)Send(session,new("capture_choice",Choice:nickname?1:0));
            else Send(session,new("ack"));
        }
        else if(b.Phase==CampaignBattlePhase.Events)Send(session,new("ack"));
        else if(b.Phase==CampaignBattlePhase.Results)
        {
            if(b.PageIndex+1>=b.Pages.Length&&!b.PendingMoves.IsEmpty){if(stopLearning)return;Send(session,new("wild_learn",MoveSlot:-1));}
            else Send(session,new("ack"));
        }
        else return;
    }
    throw new InvalidDataException("bounded presentation did not finish");
}
CampaignState FieldFixture()
{
    var original=checkpoint.State;var map=content.Maps[new MapId("map.route-1")];var definition=content.MapDefinition(map.Id);
    var cell=Enumerable.Range(0,map.Height).SelectMany(y=>Enumerable.Range(0,map.Width).Select(x=>(x,y))).First(p=>
        map.GetTile(p.x,p.y) is {Blocked:false}&&map.SourceEncounterTypeAt(p.x,p.y)==1&&!definition.Actors.Any(a=>a.X==p.x&&a.Y==p.y));
    var tile=map.GetTile(cell.x,cell.y)!;
    return original with{
        World=original.World with{Player=new(map.Id,cell.x,cell.y,tile.Elevation,Direction.South)},
        Actors=definition.Actors.ToImmutableSortedDictionary(a=>a.LocalId,a=>new CampaignActor(a.LocalId,a.SourceLocalId,a.GraphicsId,a.X,a.Y,a.Elevation,Direction.South,a.MovementType,a.ScriptId,a.HideFlag,false,false,a.X,a.Y),StringComparer.Ordinal),
        ObjectSlots=ImmutableSortedDictionary<int,string>.Empty.Add(0,"LOCALID_PLAYER"),SelectedObjectSlot=0,LastTalked=null,MapEdits=null,IsSurfing=false,IsCycling=false,
        Roamer=original.Roamer! with{Runtime=original.Roamer.Runtime with{Location=content.CelioRoamerMaps![new("map.route-2")]}},
        Exploration=original.Exploration! with{EarlyEncounter=original.Exploration.EarlyEncounter! with{PreviousMetatileBehavior=(ushort)map.SourceBehaviorAt(cell.x,cell.y)!,StepsSinceLastEncounter=21,EncounterRateBuff=ushort.MaxValue}},
        Script=original.Script with{Variables=original.Script.Variables.SetItem("VAR_REPEL_STEP_COUNT",0)},
        Inventory=original.Inventory! with{Balls=[new(new ItemId("pret:ITEM_ULTRA_BALL"),20)]}
    };
}
CampaignSession StartField()
{
    var fixture=FieldFixture();Check(validator.ValidatePlayerSave(fixture) is null,"controlled current field fixture: "+validator.ValidatePlayerSave(fixture));
    var session=CampaignSession.RestoreCurrent(content,fixture,checkpoint.Generation);Send(session,new("turn",Direction:Direction.North));Drain(session);
    Check(session.State.WildBattle is {Frame:not null,CelioRoamer:null,Phase:CampaignBattlePhase.ChooseMove},"public field starts canonical ordinary battle");
    return session;
}
void StrictRoundtrip(CampaignSession session)
{
    Check(session.ValidatePlayerSave(session.State) is null,"strict current save: "+session.ValidatePlayerSave(session.State));
    var bytes=SaveCodec.Encode(session.State,session.Revision,session.RulesVersion,content.ContentVersion);
    var decoded=SaveCodec.Decode<CampaignState>(bytes,session.RulesVersion,content.ContentVersion,validate:session.ValidatePlayerSave);
    var restored=CampaignSession.RestoreCurrent(content,decoded.State,decoded.Generation);
    Check(CanonicalJson.Hash(restored.State)==CanonicalJson.Hash(session.State),"strict save Continue preserves entire payload");
}
Test("public ordinary entry, frame authority, KO rewards once, current save",()=>{
    var session=StartField();var b=session.State.WildBattle!;var frame=b.Frame!;
    Check(typeof(CampaignWildBattle).GetField("_legacyState",BindingFlags.Instance|BindingFlags.NonPublic) is null,"canonical instance stores no legacy snapshot");
    Check(validator.ValidatePlayerSave(session.State)=="save_event_active","active canonical battle save refusal");
    Check(typeof(CampaignWildBattle).GetProperty("State")!.SetMethod is null&&typeof(CampaignWildBattle).GetProperty("PendingMoves")!.SetMethod is null,"combat and learning views are read-only");
    bool guarded=false;try{_=b with{Frame=null!};}catch(InvalidDataException){guarded=true;}Check(guarded,"canonical frame cannot be removed");
    string before=CanonicalJson.Hash(frame);_=b.State;_=b.State;Check(CanonicalJson.Hash(b.Frame)==before,"read projection consumes no RNG or mutation");
    var xp=session.State.Party.Sum(p=>(long)p.Pokemon.Experience);int history=session.State.Exploration!.Battles.Length;
    int slot=Enumerable.Range(0,b.State.Player.Pokemon.Moves.Length).Where(i=>b.State.Player.Pokemon.Moves[i].CurrentPp>0).OrderByDescending(i=>catalog.Moves[b.State.Player.Pokemon.Moves[i].Move].Power).First();
    Send(session,new("battle_move",MoveSlot:slot));Drain(session);
    Check(session.State.WildBattle is null,"ordinary KO completes entire modal lifecycle");
    Check(session.State.Exploration!.Battles.Length==history+1&&session.State.Exploration.Battles[^1].Outcome==WildBattleOutcome.Won,"one terminal history");
    Check(session.State.Party.Sum(p=>(long)p.Pokemon.Experience)-xp==session.State.Exploration.Battles[^1].ExperienceGained,"exactly once canonical EXP credited");
    StrictRoundtrip(session);
});
Test("public capture, exact instance, nickname and PC delivery",()=>{
    var session=StartField();var b=session.State.WildBattle!;var enemy=b.Frame!.Party(BattleSide.Opponent).Members[0];long id=session.State.NextPokemonId;int count=session.State.Party.Length;
    for(int attempt=0;attempt<20&&session.State.WildBattle is {} live;attempt++){enemy=live.Frame!.Party(BattleSide.Opponent).Members[0];Send(session,new("battle_ball",Text:"pret:ITEM_ULTRA_BALL"));Drain(session,stopCapture:true);if(session.State.WildBattle?.Capture is {} pending){Check(CanonicalJson.Hash(pending.Pokemon)==CanonicalJson.Hash(enemy.Pokemon),"capture pending retains exact persistent enemy before storage");Drain(session,nickname:true);}}
    Check(session.State.WildBattle is null&&session.State.NextPokemonId==id+1,"capture commits one identity");
    var all=session.State.Party.Concat(session.State.Storage!.Boxes.SelectMany(box=>box.Slots).OfType<PartyPokemon>());var captured=all.Single(p=>p.InstanceId==id);
    Check(captured.Nickname=="CANON"&&captured.Pokemon.Personality==enemy.Pokemon.Personality&&captured.Pokemon.Ivs==enemy.Pokemon.Ivs&&captured.Pokemon.Species==enemy.Pokemon.Species&&captured.Pokemon.Experience==enemy.Pokemon.Experience,"capture preserves identity and chosen nickname");
    Check(captured.HeldItem==WildBattleSourcePartyEffects.PersistentHeldItem(enemy)&&captured.Pokemon.CurrentHp==captured.Pokemon.Stats.MaxHp&&captured.Status==0&&captured.Pokemon.Moves.All(m=>m.CurrentPp==m.MaximumPp),"existing PC receive heals HP PP status and retains held item");
    Check(session.State.Party.Length==(count==6?6:count+1),"capture destination capacity");
    Check(session.State.Exploration!.Battles[^1].ExperienceGained==0&&session.State.Exploration.Battles[^1].Outcome==WildBattleOutcome.Caught,"capture earns zero EXP");
    StrictRoundtrip(session);
});

Test("public Celio entry and source flee settlement survive Continue",()=>{
    var fixture=FieldFixture();var rng=fixture.World.Rng;
    while(Gen3Rng.Next(rng,"fixture.roamer-gate",4).Value%4!=0)rng=Gen3Rng.Next(rng,"fixture.advance").State;
    fixture=fixture with{World=fixture.World with{Rng=rng},Roamer=fixture.Roamer! with{Runtime=fixture.Roamer.Runtime with{Location=content.CelioRoamerMaps![fixture.World.Player.MapId]}}};
    Check(validator.ValidatePlayerSave(fixture) is null,"current Celio field fixture");
    var session=CampaignSession.RestoreCurrent(content,fixture,checkpoint.Generation);Send(session,new("turn",Direction:Direction.North));Drain(session);
    Check(session.State.WildBattle?.Frame?.WildEncounter?.CelioRoamer?.Individual==fixture.Roamer!.Individual,"public encounter retains actual roamer identity");
    Send(session,new("battle_move",MoveSlot:0));Drain(session);
    Check(session.State.WildBattle is null&&session.State.Exploration!.Battles[^1].CelioRoamer?.Battle.Outcome==RoamerBattleOutcome.MonFled,"typed enemy flee outcome retained");
    Check(session.State.Roamer!.Individual!.CurrentHp<fixture.Roamer.Individual!.CurrentHp,"roamer battle damage persisted");
    StrictRoundtrip(session);
});
Test("public Celio capture and PC delivery retain typed provenance",()=>{
    var fixture=FieldFixture();var rng=fixture.World.Rng;
    var party=(ImmutableArray<WildBattleCombatant>)typeof(CampaignSession).GetMethod("SourceTrainerEntryParty",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(validator,[fixture])!;
    int active=Enumerable.Range(0,party.Length).First(i=>party[i].Pokemon.CurrentHp>0);bool found=false;
    var caughtSpecies=catalog.Species.Values.Where(p=>fixture.CaughtSpecies.Contains(p.SourceSpeciesId)).Select(p=>p.Id).ToImmutableHashSet();
    for(int n=0;n<20000;n++,rng=Gen3Rng.Next(rng,"fixture.capture-search").State)
    {
        var gate=Gen3Rng.Next(rng,"fixture.roamer-gate",4);if(gate.Value%4!=0)continue;
        var entry=SourceWildBattlePolicy.StartCelioRoamer(content.CelioOwnership!,fixture.Roamer!.Individual!,fixture.TrainerId,party,active,gate.State,WildBattleTerrain.Grass);
        if(!entry.Succeeded)throw new InvalidDataException(entry.Fault);
        var frame=entry.State!;var choice=new SourceActionSelection(frame.Get(new(0)).Occupant!.Value,SourceActionKind.Capture){Capture=new(CaptureBall.Ultra,new(false,caughtSpecies,CaptureEncounterKind.Roamer))};
        var action=SourceWildBattlePolicy.BeginAction(catalog,frame,frame.Revision,choice);if(!action.Succeeded)throw new InvalidDataException(action.Fault);
        for(int step=0;step<5&&action.State.Outcome==WildBattleOutcome.Ongoing;step++){action=SourceFourActionTurns.Advance(catalog,action.State,action.Continuation!.Value);if(!action.Succeeded)throw new InvalidDataException(action.Fault);}
        if(action.State.Outcome==WildBattleOutcome.Caught){found=true;break;}
    }
    Check(found,"bounded canonical capture seed search");
    fixture=fixture with{World=fixture.World with{Rng=rng},Roamer=fixture.Roamer! with{Runtime=fixture.Roamer.Runtime with{Location=content.CelioRoamerMaps![fixture.World.Player.MapId]}}};
    var session=CampaignSession.RestoreCurrent(content,fixture,checkpoint.Generation);Send(session,new("turn",Direction:Direction.North));Drain(session);
    Send(session,new("battle_ball",Text:"pret:ITEM_ULTRA_BALL"));Drain(session,nickname:true);
    Check(session.State.WildBattle is null&&!session.State.Roamer!.Individual!.Active,"actual public capture deactivates roamer");
    var capture=session.State.Exploration!.Captured!.Value[^1];
    Check(capture.CelioRoamer?.Individual.Personality==fixture.Roamer!.Individual!.Personality&&session.State.Exploration.Battles[^1].CelioRoamer?.Battle.Outcome==RoamerBattleOutcome.Caught,"capture and terminal ledger preserve typed Celio receipt");
    StrictRoundtrip(session);
});

// The following tests create explicit in-memory battle fixtures from authenticated source
// Pokemon. They do not claim a natural field encounter, legal player checkpoint or save conversion.
PartyPokemon Owned(string species,int level,long id,params string[] moves)
{
    var mon=SourcePokemonFactory.Create(mechanics,new(new("pret:SPECIES_"+species),level,checkpoint.State.TrainerId,
        IvPolicy.Fixed(31),PersonalityPolicy.Fixed((uint)id)),new(12)).Pokemon;
    if(moves.Length!=0)mon=mon with{Moves=moves.Select(name=>{var move=catalog.Moves[new("pret:MOVE_"+name)];return new PokemonMoveSlot(move.Id,move.Pp,move.Pp);}).ToImmutableArray()};
    return checkpoint.State.Party[0] with{InstanceId=id,Pokemon=mon,Nickname=species,HeldItem="ITEM_NONE",Status=0,MetLevel=level,MetLocation="MAPSEC_ROUTE_1"};
}
CampaignSession Controlled(ImmutableArray<PartyPokemon> party,PartyPokemon enemy,uint seed=123)
{
    var session=CampaignSession.CreateCurrent(content);var basis=FieldFixture() with{Party=party};
    basis=basis with{Inventory=basis.Inventory! with{BagItems=basis.Inventory.BagItems.Add(new(new("pret:ITEM_POTION"),1))}};
    var entry=SourceWildBattlePolicy.StartOwned(content.CelioOwnership!,party.Select(p=>new WildBattleCombatant(p.Pokemon,HeldItem:p.HeldItem,Status:p.Status)).ToImmutableArray(),0,
        new(enemy.Pokemon,HeldItem:enemy.HeldItem,Status:enemy.Status),new(seed));
    Check(entry.Succeeded,"controlled source admission: "+entry.Fault);
    var prepare=typeof(CampaignSession).GetMethod("PrepareCanonicalWildFrame",BindingFlags.Instance|BindingFlags.NonPublic)!;
    var frame=(SourceBattleFrame)prepare.Invoke(session,[basis,entry.State!])!;
    var view=CampaignWildReadProjection.From(frame);
    var battle=new CampaignWildBattle("wild-battle:"+basis.Exploration!.NextWildBattleId,party[0],frame,entry.Events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(),0,
        view.Player.Pokemon,view.Opponent.Pokemon,CampaignBattlePhase.Events,[],0,entry.Trace){SourceEvents=entry.Events,
        EntryLevels=party.Select(p=>p.Pokemon.Level).ToImmutableArray(),ActivePartyIndex=0,Origin=new("MAPSEC_ROUTE_1",enemy.Pokemon.Level,enemy.Nickname),
        FaintedMembers=ImmutableSortedSet<long>.Empty,OpponentHpAtLastSwitch=enemy.Pokemon.CurrentHp};
    var state=basis with{WildBattle=battle,World=basis.World with{Rng=frame.Rng},Exploration=basis.Exploration with{NextWildBattleId=basis.Exploration.NextWildBattleId+1}};
    typeof(CampaignSession).GetField("_commands",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,new CommandSession<CampaignState,CampaignCommand>(state,session.RulesVersion,content.ContentVersion,checkpoint.Generation));
    Drain(session);return session;
}
Test("voluntary switch and medicine retain real party ownership",()=>{
    var a=Owned("PIDGEY",20,101,"GROWL");var b=Owned("RATTATA",30,102,"TACKLE");b=b with{Pokemon=b.Pokemon with{CurrentHp=b.Pokemon.Stats.MaxHp-10}};var enemy=Owned("PIDGEY",4,201,"GROWL");
    var session=Controlled([a,b],enemy);Send(session,new("battle_switch",PartyIndex:1));Drain(session);
    Check(session.State.WildBattle!.ActivePartyIndex==1&&session.State.WildBattle.Frame!.Get(new(0)).Member!.Value.Index==1,"switch updates canonical active identity");
    Check(session.State.Party[0].Pokemon.Personality==a.Pokemon.Personality&&session.State.Party[1].Pokemon.Personality==b.Pokemon.Personality,"switch retains permanent identities");
    Send(session,new("battle_medicine",Text:"pret:ITEM_POTION",PartyIndex:1){TargetInstanceId=b.InstanceId});Drain(session);
    Check(session.State.Party[1].Pokemon.CurrentHp==b.Pokemon.Stats.MaxHp&&!session.State.Inventory!.BagItems.Any(s=>s.Item.Value=="pret:ITEM_POTION"),"prepared medicine applies and pays once before same canonical queue");
    Send(session,new("battle_move",MoveSlot:0));Drain(session);Check(session.State.WildBattle is null,"switch then KO complete");
});
Test("faint replacement preserves continuation and charges friendship once",()=>{
    var a=Owned("PIDGEY",3,101,"GROWL");a=a with{Pokemon=a.Pokemon with{CurrentHp=1}};
    var b=Owned("RATTATA",70,102,"TACKLE");var enemy=Owned("PIDGEY",30,201,"TACKLE");
    var session=Controlled([a,b],enemy);Send(session,new("battle_move",MoveSlot:0));Drain(session);
    Check(session.State.WildBattle is {Replacement:CampaignReplacementStage.UseNext},"actual faint exposes replacement modal");
    var frame=session.State.WildBattle!.Frame!;var rng=frame.Rng;int friendship=session.State.Party[0].Pokemon.Friendship;
    Send(session,new("battle_next",Choice:1));Check(session.State.WildBattle!.Frame==frame&&session.State.World.Rng==rng,"party menu selection advances no battle/RNG");
    Send(session,new("battle_switch",PartyIndex:1));Drain(session);
    Check(session.State.Party[0].Pokemon.Friendship==friendship&&friendship==a.Pokemon.Friendship-1,"faint friendship once across continuation");
    Check(session.State.WildBattle!.Frame!.Get(new(0)).Member!.Value.Index==1,"replacement installs actual reserve");
    Send(session,new("battle_move",MoveSlot:0));Drain(session);Check(session.State.WildBattle is null,"replacement resumed through terminal KO");
});
Test("EXP learning pause uses frame cursor and never reapplies reward",()=>{
    var a=Owned("BULBASAUR",19,101,"TACKLE","GROWL","LEECH_SEED","VINE_WHIP");
    var species=catalog.Species[a.Pokemon.Species];var target=species.LevelUpMoves.First(m=>m.Level>19&&m.Level<40&&!a.Pokemon.Moves.Any(x=>x.Move==m.Move));
    a=Owned("BULBASAUR",target.Level-1,101,"TACKLE","GROWL","LEECH_SEED","VINE_WHIP");
    a=a with{Pokemon=a.Pokemon with{Experience=GrowthRules.ExperienceAtLevel(species.GrowthRate,target.Level)-1}};
    var enemy=Owned("PIDGEY",2,201,"GROWL");enemy=enemy with{Pokemon=enemy.Pokemon with{CurrentHp=1}};
    var session=Controlled([a],enemy);Send(session,new("battle_move",MoveSlot:0));Drain(session,stopLearning:true);
    var b=session.State.WildBattle!;Check(b.Frame!.Rewards?.Pending is not null&&b.PendingMoves[0].Move==target.Move,"canonical learning cursor exposed");
    long xp=session.State.Party[0].Pokemon.Experience;var rewardFrame=b.Frame;
    var rejected=session.Execute("wrong-learning",session.Revision,new("wild_learn",MoveSlot:4));
    Check(rejected.Status==CommandStatus.Rejected&&ReferenceEquals(session.State.WildBattle!.Frame,rewardFrame),"bad learning choice leaves canonical state identical");
    Send(session,new("wild_learn",MoveSlot:0));Drain(session);
    Check(session.State.WildBattle is null&&session.State.Party[0].Pokemon.Experience==xp,"learning resumes without duplicate EXP");
    Check(session.State.Party[0].Pokemon.Moves[0].Move==target.Move,"chosen learning persisted");
});

Test("Transform retains permanent identity and PP through escape",()=>{
    var ditto=Owned("DITTO",50,101,"TRANSFORM");var enemy=Owned("PIDGEY",20,201,"GROWL");
    var session=Controlled([ditto],enemy);Send(session,new("battle_move",MoveSlot:0));Drain(session);
    Check(session.State.WildBattle!.Frame!.Get(new(0)).Body!.Transform is not null,"actual shared Transform recipe executed");
    Check(session.State.Party[0].Pokemon.Species==ditto.Pokemon.Species&&session.State.Party[0].Pokemon.Personality==ditto.Pokemon.Personality,"Transform preserves permanent species/personality");
    for(int i=0;i<10&&session.State.WildBattle is not null;i++){Send(session,new("battle_menu",Text:"run"));Drain(session);}
    Check(session.State.WildBattle is null&&session.State.Party[0].Pokemon.Species==ditto.Pokemon.Species&&session.State.Party[0].Pokemon.Moves[0].Move==ditto.Pokemon.Moves[0].Move&&session.State.Party[0].Pokemon.Moves[0].CurrentPp==ditto.Pokemon.Moves[0].CurrentPp-1,"escape persists original Transform PP exactly once");
});
Test("Poke Flute uses canonical item action and keeps reusable key item",()=>{
    var a=Owned("PIDGEY",20,101,"GROWL") with{Status=3};var enemy=Owned("PIDGEY",4,201,"GROWL") with{Status=3};
    var session=Controlled([a],enemy);int before=session.State.Inventory!.KeyItems!.Value.Single(i=>i.Item.Value=="pret:ITEM_POKE_FLUTE").Quantity;
    Send(session,new("battle_poke_flute",Text:"pret:ITEM_POKE_FLUTE"));Drain(session);
    Check(session.State.Party[0].Status==0&&session.State.WildBattle!.Frame!.Party(BattleSide.Opponent).Members[0].Status==0,"flute wakes actual canonical parties");
    Check(session.State.Inventory!.KeyItems!.Value.Single(i=>i.Item.Value=="pret:ITEM_POKE_FLUTE").Quantity==before,"reusable flute retained");
    Send(session,new("battle_menu",Text:"run"));Drain(session);Check(session.State.WildBattle is null,"flute lifecycle completes through escape");
});

CampaignSession Scene(string mapName,string script,bool scope=true)
{
    var session=CampaignSession.CreateCurrent(content);var basis=FieldFixture();var map=new MapId(mapName);var defs=content.MapDefinition(map);
    var target=defs.Actors.FirstOrDefault(a=>a.ScriptId==script);
    var party=ImmutableArray.Create(Owned("SNORLAX",100,1,"STRENGTH"));
    var flags=basis.Script.Flags.SetItem("FLAG_RESCUED_LOSTELLE",false).SetItem("FLAG_HIDE_LOSTELLE_IN_BERRY_FOREST",false)
        .SetItem("FLAG_HIDE_ROUTE_12_SNORLAX",false).SetItem("FLAG_SYS_SPECIAL_WILD_BATTLE",false);
    var keys=basis.Inventory!.KeyItems!.Value;if(!scope)keys=keys.RemoveAll(i=>i.Item==RocketHideoutItemRules.SilphScopeId);
    var state=basis with{World=basis.World with{Player=new(map,target?.X??11,target is null?15:target.Y+1,target?.Elevation??3,Direction.North)},
        Actors=defs.Actors.ToImmutableSortedDictionary(a=>a.LocalId,a=>new CampaignActor(a.LocalId,a.SourceLocalId,a.GraphicsId,a.X,a.Y,a.Elevation,Direction.South,a.MovementType,a.ScriptId,a.HideFlag,true,false,a.X,a.Y),StringComparer.Ordinal),
        ObjectSlots=null,LastTalked=target?.LocalId,EntryPrograms=[],Dialogue=null,Motions=[],PresentationTicks=0,PresentationEffects=[],
        Script=StoryState.Empty with{Variables=basis.Script.Variables.SetItem("VAR_LAST_TALKED",target?.SourceLocalId??0).SetItem("VAR_MAP_SCENE_THREE_ISLAND",3).SetItem("VAR_MAP_SCENE_POKEMON_TOWER_6F",0),Flags=flags},
        Party=party,NextPokemonId=2,Storage=PokemonStorageState.Empty,Exploration=basis.Exploration! with{Battles=[],Captured=[],NextWildBattleId=1},
        Inventory=basis.Inventory with{KeyItems=keys,Balls=[new(new("pret:ITEM_MASTER_BALL"),1)]}};
    var start=typeof(CampaignSession).GetMethod("StartStory",BindingFlags.Instance|BindingFlags.NonPublic)!;
    var pump=typeof(CampaignSession).GetMethod("Pump",BindingFlags.Instance|BindingFlags.NonPublic)!;
    state=(CampaignState)pump.Invoke(session,[start.Invoke(session,[state,script])])!;
    typeof(CampaignSession).GetField("_commands",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,new CommandSession<CampaignState,CampaignCommand>(state,session.RulesVersion,content.ContentVersion,checkpoint.Generation));
    for(int i=0;i<250&&session.State.WildBattle is null;i++)Send(session,new("ack",Choice:session.State.Script.Wait?.Kind==StoryWaitKind.Choice?1:null));
    Check(session.State.WildBattle?.Frame is not null,"authenticated source scene entered canonical battle: "+script);Drain(session);return session;
}
Test("Lostelle exact source Hypno escape resumes rescue caller",()=>{
    var session=Scene("map.three-island-berry-forest",FirstSeviiHypnoBattleContract.ScriptEntry);
    var battle=session.State.WildBattle!;Check(battle.LostelleHypno is not null&&battle.Frame!.WildEncounter is {LostelleHypno:true,FirstSeviiWild:true},"Hypno typed source provenance");
    for(int i=0;i<10&&session.State.WildBattle is not null;i++){Send(session,new("battle_menu",Text:"run"));Drain(session);}
    Check(session.State.Exploration!.Battles.Single() is {LostelleHypno:not null,Outcome:WildBattleOutcome.Escaped},"actual Hypno escape history");
    for(int i=0;i<50&&session.State.Dialogue?.TextSymbol!="ThreeIsland_BerryForest_Text_ThankYouHaveThis";i++)Send(session,new("ack"));
    Check(session.State.Dialogue?.TextSymbol=="ThreeIsland_BerryForest_Text_ThankYouHaveThis","source rescue continuation survives canonical terminal");
});
Test("Snorlax source capture clears actor and resumes source outcome",()=>{
    var session=Scene("map.route-12","Route12_EventScript_Snorlax");var battle=session.State.WildBattle!;
    Check(battle.ScriptedSnorlaxRoute==12&&battle.Frame!.WildEncounter!.ScriptedSnorlax,"Snorlax source policy retained");
    Send(session,new("battle_ball",Text:"pret:ITEM_MASTER_BALL"));Drain(session);
    Check(session.State.Exploration!.Battles.Single() is {ScriptedSnorlaxRoute:12,Outcome:WildBattleOutcome.Caught},"Snorlax actual capture history");
    Check(session.State.Script.Flags.GetValueOrDefault("FLAG_HIDE_ROUTE_12_SNORLAX")&&!session.State.Script.Flags.GetValueOrDefault("FLAG_SYS_SPECIAL_WILD_BATTLE"),"source OnResume clears special battle flag and hides Snorlax");
    Check(session.State.Actors.Values.Where(a=>a.ScriptId=="Route12_EventScript_Snorlax").All(a=>!a.Active),"source Snorlax actor removed after capture");
});
Test("revealed Marowak dodges ball and real victory resumes Tower source",()=>{
    var session=Scene("map.pokemon-tower-6f","PokemonTower_6F_EventScript_MarowakGhost");
    Check(session.State.WildBattle!.Frame!.WildEncounter!.TowerBattleKind==PokemonTowerBattleKind.RevealedMarowak,"revealed mother stays distinct");
    Send(session,new("battle_ball",Text:"pret:ITEM_MASTER_BALL"));
    Check(session.State.WildBattle!.Events.Any(e=>e is WildBattleTowerBallDodgedEvent)&&session.State.WildBattle.Capture is null,"source ghost ball dodge survives canonical event projection");Drain(session);
    for(int i=0;i<10&&session.State.WildBattle is not null;i++){Send(session,new("battle_move",MoveSlot:0));Drain(session);}
    Check(session.State.Exploration!.Battles.Single() is {ScriptedMarowak:true,Outcome:WildBattleOutcome.Won},"actual source Marowak victory");
    Check(session.State.Script.Variables.GetValueOrDefault("VAR_RESULT")==0&&session.State.WildBattle is null,"Tower source gets correct winning callback result");
});

File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[2]))!,"field-fixture.json"),SaveCodec.Encode(FieldFixture(),checkpoint.Generation,validator.RulesVersion,content.ContentVersion));
File.WriteAllText(args[2],JsonSerializer.Serialize(new{passed=true,checks,commands,runtime=Environment.Version.ToString(),cases,
    scope="Current converted checkpoint, controlled source route/rate setup; public current battle commands, modal continuations and strict stable saves. No historical regional route replay or native visual/audio claim."},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(File.ReadAllText(args[2]));
