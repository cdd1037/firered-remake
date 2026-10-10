using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

if (args.Length != 3) throw new ArgumentException("CANONICAL_ROOT CURRENT_PROJECT_ROOT SOURCE_DAMAGE_ORACLE");
var mechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0], args[1]);
var catalog = mechanics.Catalog;
int groups = 0, checks = 0, failures = 0, sourceTurns = 0;
var observed = new List<object>();
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
void Test(string name, Action action) { groups++; try { action(); Console.WriteLine("PASS " + name); } catch(Exception ex) { failures++; Console.WriteLine("FAIL " + name + " " + ex); } }
MoveId Move(string name) => new("pret:MOVE_" + name);
PokemonInstance Mon(string name, int level = 37, uint pid = 0, params string[] moves)
{
    var mon = SourcePokemonFactory.Create(mechanics, new(new("pret:SPECIES_" + name), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
    if (moves.Length == 0) return mon;
    foreach (string move in moves) Check(catalog.Species[mon.Species].LevelUpMoves.Any(e => e.Move == Move(move) && e.Level <= level), "real retained source learnset " + name + "/" + move);
    return mon with { Moves = moves.Select(m => new PokemonMoveSlot(Move(m),catalog.Moves[Move(m)].Pp,catalog.Moves[Move(m)].Pp)).ToImmutableArray() };
}
WildBattleState Require(WildBattleResult result, string label)
{
    Check(result.Succeeded, label + ": " + result.Fault);
    Check(WildBattleRules.Validate(catalog, result.State) is null, label + " validates: " + WildBattleRules.Validate(catalog, result.State));
    return result.State!;
}
WildBattleState Start(WildBattleCombatant player, WildBattleCombatant opponent, uint seed=0) => Require(
    WildBattleRules.StartSourceWild(mechanics, BattleRulesProfile.CanonicalSourceSingles, [player], 0, opponent, new(seed)), "canonical entry");
int OracleBase(WildBattleCombatant actor, WildBattleCombatant target, PokemonMoveDefinition move, bool critical)
{
    using var process = new Process { StartInfo = new(args[2]) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false } };
    process.Start();
    int Ability(string name) => mechanics.Abilities[name].SourceAbilityId;
    process.StandardInput.WriteLine(string.Join(' ', new[] { actor.Pokemon.Stats.Attack, target.Pokemon.Stats.Defense,
        Ability(WildBattleEffective.Ability(actor)), Ability(WildBattleEffective.Ability(target)), (int)actor.Status, (int)target.Status,
        actor.AttackStage,target.DefenseStage,critical ? 1 : 0,move.Power,0,0,0,0,0,actor.AttackBadgeBoost?1:0,target.DefenseBadgeBoost?1:0 }));
    process.StandardInput.Close(); string output=process.StandardOutput.ReadToEnd();process.WaitForExit();
    Check(process.ExitCode==0 && int.TryParse(output,out _),"independent source C oracle");return int.Parse(output);
}
void ValidateHit(WildBattleState before, WildBattleResult result, string expectedMove, int? sourcePower = null)
{
    var state=Require(result,"complete source turn");sourceTurns++;
    var hit=result.Events.OfType<WildBattleHpChangedEvent>().Single(e=>e.Actor==WildBattleSide.Player && e.Move==Move(expectedMove));
    var move=catalog.Moves[hit.Move];int expected=OracleBase(before.Player,before.Opponent,sourcePower is { } power ? move with { Power=power } : move,hit.Critical);
    if(hit.Critical)expected*=2;
    var a=catalog.Species[before.Player.Pokemon.Species];var d=catalog.Species[before.Opponent.Pokemon.Species];
    if(move.Type==a.Type1||move.Type==a.Type2)expected=expected*15/10;
    ElementType Type(string symbol)=>Enum.Parse<ElementType>(symbol[5..],true);
    foreach(var effect in TypeRules.OrderedEffects(Type(move.Type),Type(d.Type1),Type(d.Type2))) expected=expected*effect.MultiplierTenths/10;
    expected=Math.Max(1,expected*hit.RandomPercent/100);
    Check(hit.Damage==expected,$"source C HP expected {expected} actual {hit.Damage}");
    Check(state.Turn==before.Turn+1 && state.SourceWild!.Turn is null,"whole turn completed");
    Check(result.Events.OfType<WildBattleMoveUsedEvent>().Any(e=>e.Actor==WildBattleSide.Opponent),"real opposing action");
    Check(state.Player.Pokemon.Moves[0].CurrentPp==before.Player.Pokemon.Moves[0].CurrentPp-1,"owned slot PP");
    Check(state.Player.Pokemon.AbilityId==before.Player.Pokemon.AbilityId && state.CalledParty!.Player[0].Pokemon.AbilityId==before.Player.Pokemon.AbilityId,"permanent ability unchanged");
    observed.Add(new{species=before.Player.Pokemon.Species.Value,ability=before.Player.Pokemon.AbilityId,targetAbility=before.Opponent.Pokemon.AbilityId,
        damage=hit.Damage,expected,hit.Critical,hit.RandomPercent,state.Turn,state.Rng});
}
Test("immutable source proof and runtime",()=>{
    Check(Environment.Version.Major==10,"official runtime major10");
    Check(catalog.Species.Count==386&&catalog.Moves.Count==354&&mechanics.Abilities.Count==76,"complete source inventory");
    Check(mechanics.SpeciesMechanics.Values.Sum(m=>m.SourceEvolutions.Length)==184,"all original evolution triples remain");
    Check(catalog.Species.Values.Sum(s=>s.LevelUpMoves.Length)==4011,"all canonical learnset rows");
});
Test("shared factory: every real species, source abilities and exceptional stats",()=>{
    foreach(var species in catalog.Species.Values)
    {
        foreach(uint pid in new uint[]{0,25})
        {
            var result=SourcePokemonFactory.Create(mechanics,new(species.Id,37,42,IvPolicy.Random,PersonalityPolicy.Fixed(pid)),new(123));
            Check(result.Trace.Length==2&&result.Rng.Cursor==2,"real IV draw contract");
            Check(SourcePokemonValidator.ValidatePermanent(mechanics,result.Pokemon)is null,"actual source instance "+species.Id.Value);
        }
    }
    var shedinja=Mon("SHEDINJA",50);var deoxys=Mon("DEOXYS",50);
    Check(shedinja.Stats.MaxHp==1&&shedinja.CurrentHp==1,"Shedinja HP1");
    Check(deoxys.SourceSpeciesId==410&&deoxys.Stats==new BattleStats(125,200,40,200,40,170),"FireRed non-link Deoxys stats and internal410 identity");
    observed.Add(new{shedinja.Stats,deoxysStats=deoxys.Stats});
});
foreach(var test in new[]{("AZUMARILL",25u,"ABILITY_HUGE_POWER","TACKLE"),("MEDICHAM",0u,"ABILITY_PURE_POWER","HI_JUMP_KICK"),("TOGETIC",0u,"ABILITY_HUSTLE","DOUBLE_EDGE")})
Test("authentic "+test.Item3+" complete source turn",()=>{
    var p=Mon(test.Item1,37,test.Item2,test.Item4);Check(p.AbilityId==test.Item3,"real ability slot");
    var state=Start(new(p),new(Mon("SNORLAX",80,0,"TACKLE")), test.Item1=="TOGETIC"?1u:0u);
    ValidateHit(state,WildBattleRules.ResolveMove(catalog,state,0),test.Item4);
});
Test("authentic Marvel Scale full turn and burn residual",()=>{
    var state=Start(new(Mon("SNORLAX",37,0,"TACKLE")),new(Mon("MILOTIC",37,0,"WATER_GUN")){Status=WildBattleStatus.Burn});
    var result=WildBattleRules.ResolveMove(catalog,state,0);ValidateHit(state,result,"TACKLE");
    Check(result.Events.OfType<WildBattleStatusDamageEvent>().Any(e=>e.Side==WildBattleSide.Opponent),"actual burn residual");
});
Test("authenticated canonical weight through full Low Kick turn",()=>{
    var state=Start(new(Mon("MANKEY",37,0,"LOW_KICK")),new(Mon("MILOTIC",37,0,"WATER_SPORT")));
    var result=WildBattleRules.ResolveMove(catalog,state,0);ValidateHit(state,result,"LOW_KICK",100);
    Check(mechanics.SpeciesMechanics[new("pret:SPECIES_MILOTIC")].WeightHectograms==1620,"canonical Milotic weight");
    Check(result.Events.OfType<WildBattleHpChangedEvent>().Any(e=>e.Actor==WildBattleSide.Player),"weight path executes actual hit");
});
Test("real Pressure and FireRed Deoxys through a full source turn",()=>{
    var state=Start(new(Mon("SNORLAX",37,0,"TACKLE")),new(Mon("DEOXYS",37,0,"LEER")));
    var result=WildBattleRules.ResolveMove(catalog,state,0);var next=Require(result,"Pressure turn");sourceTurns++;
    Check(next.Player.Pokemon.Moves[0].CurrentPp==state.Player.Pokemon.Moves[0].CurrentPp-2,"authentic Pressure source PP");
    Check(next.Turn==state.Turn+1&&result.Events.OfType<WildBattleMoveUsedEvent>().Count()==2,"Pressure both actions and endturn");
});
Test("canonical Transform validates permanent and live source identities",()=>{
    var state=Start(new(Mon("DITTO",37,0,"TRANSFORM")),new(Mon("AZUMARILL",37,25,"TACKLE")));
    var result=WildBattleRules.ResolveMove(catalog,state,0);var next=Require(result,"Transform turn");sourceTurns++;
    Check(next.Player.Transform is not null&&next.Player.Pokemon.AbilityId=="ABILITY_HUGE_POWER","real Transform donor");
    Check(WildBattleTransformationRules.PersistentPokemon(next.Player).SourceSpeciesId==132,"permanent Ditto remains");
    Check(SourcePokemonValidator.ValidateLive(mechanics,next)is null,"shared live validation");
    var restored=JsonSerializer.Deserialize<WildBattleState>(JsonSerializer.Serialize(next))!;
    Check(SourcePokemonValidator.ValidateLive(mechanics,restored)is null,"serialized explicit profile and transformation proof");
    Require(WildBattleRules.ResolveMove(catalog,restored,0),"transformed attack turn");sourceTurns++;
});
Test("Hustle preserves its real source miss branch",()=>{
    var state=Start(new(Mon("TOGETIC",37,0,"DOUBLE_EDGE")),new(Mon("SNORLAX",80,0,"TACKLE")),0);
    var result=WildBattleRules.ResolveMove(catalog,state,0);var next=Require(result,"Hustle miss turn");sourceTurns++;
    Check(result.Events.OfType<WildBattleMoveMissedEvent>().Any(e=>e.Actor==WildBattleSide.Player),"Hustle physical accuracy miss");
    Check(next.Turn==state.Turn+1&&next.Player.Pokemon.Moves[0].CurrentPp==state.Player.Pokemon.Moves[0].CurrentPp-1,"miss still owns turn and PP");
});
Test("campaign factory and encounter boundaries remain strict",()=>{
    var p=Mon("AZUMARILL",37,25,"TACKLE");
    Check(CampaignAdmissionPolicy.CurrentFuchsia.ValidatePokemon(p)=="campaign_species_unsupported","campaign donor rejected");
    bool rejected=false;try{PokemonFactory.Create(catalog,new(p.Species,37,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(25)),new(0));}catch(ArgumentException){rejected=true;}
    Check(rejected,"legacy factory rejects mechanics catalog");
    Check(!WildBattleRules.StartSourceWild(catalog,[new(p)],0,new(Mon("RATTATA",37,0,"TACKLE")),new(0)).Succeeded,"legacy campaign wild rejects canonical catalog");
    Check(!WildBattleRules.Start(catalog,new(p),new(Mon("RATTATA",37,0,"TACKLE")),new(0),WildBattleRules.FuchsiaRulesVersion).Succeeded,"historical public entry rejects canonical catalog");
});
Test("forged identity and copied catalog reject before turn mutation",()=>{
    var state=Start(new(Mon("AZUMARILL",37,25,"TACKLE")),new(Mon("SNORLAX",80,0,"TACKLE")));
    foreach(var bad in new[]{state.Player.Pokemon with{AbilityId="ABILITY_PURE_POWER"},state.Player.Pokemon with{SourceSpeciesId=1},state.Player.Pokemon with{Stats=state.Player.Pokemon.Stats with{Attack=999}}})
    {
        var forged=state with{Player=state.Player with{Pokemon=bad},CalledParty=state.CalledParty! with{Player=state.CalledParty.Player.SetItem(0,state.CalledParty.Player[0] with{Pokemon=bad})}};
        var result=WildBattleRules.ResolveMove(catalog,forged,0);
        Check(!result.Succeeded&&ReferenceEquals(result.State,forged)&&result.Events.IsEmpty&&result.Trace.IsEmpty,"atomic forged instance");
    }
    foreach(var copy in new[]{catalog with{},catalog with{Species=catalog.Species.SetItem(state.Player.Pokemon.Species,catalog.Species[state.Player.Pokemon.Species] with{Ability2="ABILITY_PURE_POWER"})}})
    {
        var result=WildBattleRules.ResolveMove(copy,state,0);
        Check(!result.Succeeded&&ReferenceEquals(result.State,state)&&result.Events.IsEmpty&&result.Trace.IsEmpty,"copied catalog cannot inherit proof");
    }
    var unprofiled=state with{SourceMechanicsRulesVersion=null};
    var missing=WildBattleRules.ResolveMove(catalog,unprofiled,0);Check(!missing.Succeeded&&missing.Trace.IsEmpty,"explicit mechanics profile required");
});
Test("semantic proof rejects altered stats, source ID, learnset, weight and raw evolution",()=>{
    var id=new SpeciesId("pret:SPECIES_AZUMARILL");var original=catalog.Species[id];
    void Reject(PokemonCatalog candidate,ImmutableArray<SourceSpeciesMechanics>? metadata=null)
    {
        bool rejected=false;try{SourceMechanicsCatalog.Authenticate(candidate,metadata??mechanics.SpeciesMechanics.Values.ToImmutableArray(),mechanics.Abilities.Values.ToImmutableArray(),mechanics.MaximumHpOneSpecies,mechanics.FormStatOverrides);}catch(ArgumentException){rejected=true;}
        Check(rejected,"semantic alteration rejected");
    }
    foreach(var s in new[]{original with{BaseStats=original.BaseStats with{Attack=51}},original with{SourceSpeciesId=185},original with{Ability2="ABILITY_PURE_POWER"},original with{LevelUpMoves=original.LevelUpMoves.RemoveAt(0)}})
        Reject(catalog with{Species=catalog.Species.SetItem(id,s)});
    Reject(catalog,mechanics.SpeciesMechanics.Values.Select(m=>m.Species==id?m with{WeightHectograms=m.WeightHectograms+1}:m).ToImmutableArray());
    var evolving=mechanics.SpeciesMechanics.Values.First(m=>m.SourceEvolutions.Length>0);
    Reject(catalog,mechanics.SpeciesMechanics.Values.Select(m=>m.Species==evolving.Species?m with{SourceEvolutions=[]}:m).ToImmutableArray());
    Check(!WildBattleRules.StartSourceWild(mechanics,BattleRulesProfile.CanonicalSourceSingles,[new(Mon("SLAKING",37,0,"SCRATCH"))],0,new(Mon("RATTATA",37,0,"TACKLE")),new(0)).Succeeded,"unsupported Truant identity does not claim executed mechanics");
});
var summary=new{groups,checks,failures,sourceTurns,runtime=Environment.Version.ToString(),semanticSha256=SourceMechanicsCatalog.SemanticSha256,observed};
Console.WriteLine(JsonSerializer.Serialize(summary,new JsonSerializerOptions{WriteIndented=true}));
return failures==0&&checks>0&&sourceTurns>=9?0:1;
