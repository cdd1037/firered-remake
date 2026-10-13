using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Growth;
using System.Security.Cryptography;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;

if (args.Length != 2) throw new ArgumentException("RESOURCE_ROOT OUTPUT_JSON");
string root = Path.GetFullPath(args[0]);
int checks = 0;
var observed = new SortedDictionary<string, object>(StringComparer.Ordinal);
void Check(bool ok, string label) { checks++; if (!ok) throw new InvalidDataException(label); }
object? Call(CampaignSession owner, string method, params object?[] values)
{
    try
    {
        var target = typeof(CampaignSession).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(m => m.Name == method && m.GetParameters().Length >= values.Length &&
                m.GetParameters().Skip(values.Length).All(p => p.IsOptional));
        return target.Invoke(owner, values.Concat(target.GetParameters().Skip(values.Length).Select(p => p.DefaultValue)).ToArray());
    }
    catch (TargetInvocationException e) { throw e.InnerException!; }
}
CampaignState ReadPayload(string file) => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, file)))
    .RootElement.GetProperty("Payload").Deserialize<CampaignState>()!;
CampaignState Heal(CampaignState state) => state with { Party = state.Party.Select(p => p with {
    Status = 0, Pokemon = p.Pokemon with { CurrentHp = p.Pokemon.Stats.MaxHp,
        Moves = p.Pokemon.Moves.Select(m => m with { CurrentPp = m.MaximumPp }).ToImmutableArray() }
}).ToImmutableArray() };
var current = PretCampaignCatalog.LoadCurrent(root);
var owner = CampaignSession.CreateCurrent(current);
var currentState = ReadPayload("evidence/current-composition-20261013/conversion-run01/current.json");
Check(owner.ValidatePlayerSave(currentState) is null, "current fixture strict validity");
Check(Environment.Version.Major == 10, "actual runtime10");
CampaignState Place(ICampaignCatalog content, CampaignSession session, CampaignState state, string script)
{
    var map = content.Maps.Keys.Single(map => content.MapDefinition(map).Actors.Any(a => a.ScriptId == script));
    var actor = content.MapDefinition(map).Actors.Single(a => a.ScriptId == script);
    var field = content.ConnectedFieldMaps![map];
    var options = new[] { (X: actor.X, Y: actor.Y + 1, Face: Direction.North), (X: actor.X + 1, Y: actor.Y, Face: Direction.West),
        (X: actor.X, Y: actor.Y - 1, Face: Direction.South), (X: actor.X - 1, Y: actor.Y, Face: Direction.East) };
    var target = options.First(p => field.CellAt(p.X, p.Y) is { Collision: 0, Elevation: 3 } && !content.MapDefinition(map).Actors.Any(a => a.X == p.X && a.Y == p.Y));
    return (CampaignState)Call(session, "Pump", Call(session, "EnterMap", state with { World = state.World with {
        Player = new(map, target.X, target.Y, 3, target.Face) }, IsSurfing = false }, null))!;
}
int sequence = 0, replacements=0, shiftChoices=0, learningChoices=0;
var commands = new List<object>();
void Send(CampaignSession session, CampaignCommand command)
{
    if(command.Kind=="battle_switch"&&session.State.ForestTrainerBattle?.Frame.FourTurn?.Phase==SourceFourActionPhase.ReplacementsPending)replacements++;
    if(command.Kind=="trainer_shift")shiftChoices++;
    if(command.Kind=="wild_learn")learningChoices++;
    var result = session.Execute("canonical-trainer:" + sequence, session.Revision, command);
    commands.Add(new { index = sequence++, command, result.Status, result.Error, result.StateHash });
    Check(result.Status == CommandStatus.Accepted, command.Kind + ": " + result.Error);
}
void Drain(CampaignSession session, ICampaignCatalog content, bool acceptShift=true)
{
    for (int budget = 0; budget < 500; budget++)
    {
        var state = session.State;
        if (state.ForestTrainerBattle is { } trainer)
        {
            var p = trainer.Presentation;
            if (trainer.Stage == CampaignForestTrainerStage.Intro || p.Phase == CampaignBattlePhase.Events) { Send(session, new("ack")); continue; }
            if (p.Phase == CampaignBattlePhase.Results) { Send(session, !p.PendingMoves.IsEmpty && p.PageIndex + 1 >= p.Pages.Length ? new("wild_learn", MoveSlot: -1) : new("ack")); continue; }
            if (trainer.Combat.Phase == ForestTrainerCombatPhase.AwaitingShiftChoice) { if(!acceptShift){Send(session,new("trainer_shift",Choice:0));continue;} Send(session, new("trainer_shift", Choice: 1)); Send(session,new("battle_switch",PartyIndex:Enumerable.Range(0,state.Party.Length).First(i=>i!=p.ActivePartyIndex&&state.Party[i].Pokemon.CurrentHp>0))); continue; }
            if (trainer.Combat.Phase == ForestTrainerCombatPhase.AwaitingPlayerReplacement) { Send(session, new("battle_switch", PartyIndex: Enumerable.Range(0, state.Party.Length).First(i => state.Party[i].Pokemon.CurrentHp > 0))); continue; }
            var mechanics = content.FiveIslandOpponents?.Mechanics.Catalog ?? content.FourIslandOpponents!.Mechanics.Catalog;
            int Score(int slot)
            {
                if (slot < 0) return 0;
                var move = mechanics.Moves[trainer.Combat.Battle.Player.Pokemon.Moves[slot].Move];
                if (move.Power == 0) return 0;
                try { return WildBattleDamage.Calculate(mechanics, trainer.Combat.Battle.Player, trainer.Combat.Battle.Opponent, move.Id, false, 100, trainer.Combat.Battle.RulesVersion).Damage; }
                catch (NotSupportedException) { return move.Power; }
            }
            var slots = Enumerable.Range(-1, trainer.Combat.Battle.Player.Pokemon.Moves.Length + 1)
                .Where(i => SourceWildBattlePolicy.ValidateMoveSelection(mechanics, trainer.Frame, i) is null);
            Send(session, new("battle_move", MoveSlot: slots.OrderByDescending(Score).First())); continue;
        }
        if (state.Evolution is not null) { Send(session, new("evolution_choice", Choice: 1)); continue; }
        if (state.Script.Wait is { } wait) { Send(session, wait.Kind == StoryWaitKind.ButtonPress ? new("button_press", Text: "A") : new("ack", Choice: wait.Kind == StoryWaitKind.Choice ? 1 : null)); continue; }
        return;
    }
    throw new InvalidDataException("bounded battle drain exceeded");
}
void SaveCheck(string key, CampaignSession session, ICampaignCatalog content)
{
    Check(session.ValidatePlayerSave(session.State) is null, key + " final strict save validity: " + session.ValidatePlayerSave(session.State));
    var bytes = SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion);
    var decoded = SaveCodec.Decode<CampaignState>(bytes, session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
    var continued=CampaignSession.RestoreCurrent(content,decoded.State,decoded.Generation);
    Check(CanonicalJson.Hash(continued.State) == CanonicalJson.Hash(session.State), key + " exact current Continue");
    Console.WriteLine("Completed " + key);
    observed[key] = new { state = CanonicalJson.Hash(session.State), bytes = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)), session.State.World.Rng,
        history = session.State.ForestTrainerHistory!.Value.Last() };
}


var roseState = Place(current, owner, Heal(currentState), "SixIsland_WaterPath_EventScript_Rose");
var rose = CampaignSession.RestoreCurrent(current, roseState, 47819);
Send(rose,new("interact"));
Check(rose.State.ForestTrainerBattle?.Trainer.Value=="pret:TRAINER_AROMA_LADY_ROSE","authentic Rose public entry");
var entry=rose.State.ForestTrainerBattle!;
Check(entry.Frame.TrainerEncounter is not null && entry.Frame.Party(BattleSide.Player).Members.Length==6,"canonical full party authority");
Check(entry.Presentation.RandomTrace.Take(4).Select(t=>t.Purpose).SequenceEqual(Enumerable.Range(0,4).Select(i=>$"six-island-trainer.entry.ai-setup.slot-{i}")),"original four setup draws first");
while(rose.State.ForestTrainerBattle!.Presentation.Phase!=CampaignBattlePhase.ChooseMove)Send(rose,new("ack"));
var before=CanonicalJson.Hash(rose.State);
var rejected=rose.Execute("invalid-item",rose.Revision,new("battle_medicine",Text:"pret:ITEM_REVIVE",PartyIndex:1){TargetInstanceId=rose.State.Party[1].InstanceId});
Check(rejected.Status==CommandStatus.Rejected&&CanonicalJson.Hash(rose.State)==before,"failed item selection atomic");
Drain(rose,current);
Check(rose.State.ForestTrainerHistory!.Value.Last() is {Outcome:WildBattleOutcome.Won,ExperienceGained:>0,MoneyGained:>0},"Rose win EXP prize");
Check(rose.State.Party.Sum(p=>(long)p.Pokemon.Experience)-roseState.Party.Sum(p=>(long)p.Pokemon.Experience)==rose.State.ForestTrainerHistory!.Value.Last().ExperienceGained,"party EXP delta equals single shared reward total");
SaveCheck("rose-win-shift",rose,current);

// Faint a real lead on a bounded variant of the same current fixture. The public item action
// revives a reserve before the opponent takes its turn, then the normal KO replacement resumes.
var faintState=roseState with{Party=roseState.Party.SetItem(0,roseState.Party[0] with{Pokemon=roseState.Party[0].Pokemon with{CurrentHp=1}})
    .SetItem(1,roseState.Party[1] with{Pokemon=roseState.Party[1].Pokemon with{CurrentHp=0}})};
var faint=CampaignSession.RestoreCurrent(current,faintState,47819);Send(faint,new("interact"));
Check(faint.State.Party[1].Pokemon.Friendship==faintState.Party[1].Pokemon.Friendship,"already-fainted reserve does not acquire a new friendship penalty at entry");
while(faint.State.ForestTrainerBattle!.Presentation.Phase!=CampaignBattlePhase.ChooseMove)Send(faint,new("ack"));
int revives=faint.State.Inventory!.BagItems.Single(x=>x.Item.Value=="pret:ITEM_REVIVE").Quantity;
Send(faint,new("battle_medicine",Text:"pret:ITEM_REVIVE",PartyIndex:1){TargetInstanceId=faint.State.Party[1].InstanceId});
Check(faint.State.Party[1].Pokemon.CurrentHp>0&&faint.State.Inventory!.BagItems.Single(x=>x.Item.Value=="pret:ITEM_REVIVE").Quantity==revives-1,"reserve Revive paid and applied once");
Drain(faint,current);SaveCheck("rose-revive-replacement",faint,current);

Check(replacements>0,"public fainted player replacement exercised");
Check(shiftChoices>0,"public Shift choice exercised");

var charizard=roseState.Party[1];
var species=current.CelioOpponents!.Mechanics.Catalog.Species[charizard.Pokemon.Species];
Check(species.LevelUpMoves.Any(m=>m.Level==64&&m.Move.Value=="pret:MOVE_FIRE_SPIN"),"authenticated level64 learning candidate");
var learningState=roseState with{Party=roseState.Party.SetItem(1,charizard with{Pokemon=charizard.Pokemon with{Experience=GrowthRules.ExperienceAtLevel(species.GrowthRate,64)-1}})};
// Party reorder is an ordinary supported persistent operation; keep all actual identities.
learningState=learningState with{Party=learningState.Party.SetItem(0,learningState.Party[1]).SetItem(1,learningState.Party[0])};
var learning=CampaignSession.RestoreCurrent(current,learningState,47819);Send(learning,new("interact"));
Drain(learning,current);
Check(learning.State.Party[0].Pokemon.Level==64,"actual EXP level-up applied");
Check(learningChoices>0,"actual source learning cursor answered once");
Check(learning.State.Party.Sum(p=>(long)p.Pokemon.Experience)-learningState.Party.Sum(p=>(long)p.Pokemon.Experience)==learning.State.ForestTrainerHistory!.Value.Last().ExperienceGained,"learning continuation does not duplicate EXP");
SaveCheck("rose-level-learning",learning,current);

// Explicit offline conversion of this one retained pre-Gideon source checkpoint only.
// The normal constructor/loader never selects its historical runtime.
string gideonSource=Path.Combine(root,"tests/Game.FiveIslandCampaign.Tests/evidence/recovery-20261013-run02/before-gideon.json");
var original=File.ReadAllBytes(gideonSource);var sourceHash=Convert.ToHexStringLower(SHA256.HashData(original));
using var envelope=JsonDocument.Parse(original);
Check(envelope.RootElement.GetProperty("RulesVersion").GetString()==CampaignSession.FiveIslandRulesVersion,"selected Five checkpoint rules");
var selected=SaveCodec.Decode<CampaignState>(original,CampaignSession.FiveIslandRulesVersion,
    envelope.RootElement.GetProperty("ContentVersion").GetString()!,validate:owner.ValidatePlayerSave);
var converted=SaveCodec.Encode(selected.State,selected.Generation,owner.RulesVersion,current.ContentVersion);
var conversion=SaveCodec.Decode<CampaignState>(converted,owner.RulesVersion,current.ContentVersion,validate:owner.ValidatePlayerSave);
Check(CanonicalJson.Hash(selected.State)==CanonicalJson.Hash(conversion.State)&&conversion.Generation==selected.Generation,"explicit current conversion preserves full payload and generation");
string conversionPath=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!,"converted-before-gideon.json");
File.WriteAllBytes(conversionPath,converted);
var gideon=CampaignSession.RestoreCurrent(current,Place(current,owner,conversion.State,"FiveIsland_RocketWarehouse_EventScript_Gideon"),conversion.Generation);
Send(gideon,new("interact"));
Check(gideon.State.ForestTrainerBattle?.Trainer.Value=="pret:TRAINER_SCIENTIST_GIDEON","Gideon authentic source entry");
Drain(gideon,current,false);
Console.WriteLine("Gideon outcome "+gideon.State.ForestTrainerHistory!.Value.Last().Outcome+" callback="+gideon.State.Script.Flags.GetValueOrDefault("FLAG_RECOVERED_SAPPHIRE"));
Check(gideon.State.Script.Flags.GetValueOrDefault("FLAG_RECOVERED_SAPPHIRE"),"Gideon actual source callback recovered Sapphire");
SaveCheck("gideon-source-callback",gideon,current);
Check(sourceHash==Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(gideonSource))),"original selected checkpoint remains byte-identical");
observed["conversion"]=new{sourceHash,generation=selected.Generation,payload=CanonicalJson.Hash(conversion.State),convertedHash=Convert.ToHexStringLower(SHA256.HashData(converted))};
observed["public-commands"]=commands;
Check(sequence>40,"meaningful public command coverage");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
File.WriteAllText(args[1],JsonSerializer.Serialize(new{passed=true,checks,commands=sequence,runtime=Environment.Version.ToString(),observed},new JsonSerializerOptions{WriteIndented=true}));
new XDocument(new XElement("testsuite",new XAttribute("name","CanonicalTrainerApplication"),new XAttribute("tests",checks),new XAttribute("failures",0))).Save(Path.ChangeExtension(args[1],".xml"));
Console.WriteLine($"PASS {checks} checks; {sequence} public commands");
