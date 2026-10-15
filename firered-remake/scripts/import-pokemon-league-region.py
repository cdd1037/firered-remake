#!/usr/bin/env python3
"""Import seven pinned Pokemon League interior maps and non-executable first-clear contracts. No upstream code or ROM build."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse, ast, collections,  importlib.util, json, re, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PROFILE = 'firered-pokemon-league-region-v1'
PACKAGE = 'pokemon-league-region'
TILESETS = {'gTileset_Building':'primary/building','gTileset_PokemonCenter':'secondary/pokemon_center','gTileset_PokemonLeague':'secondary/pokemon_league','gTileset_HallOfFame':'secondary/hall_of_fame'}
MAPS = ['IndigoPlateau_PokemonCenter_1F','PokemonLeague_LoreleisRoom','PokemonLeague_BrunosRoom','PokemonLeague_AgathasRoom','PokemonLeague_LancesRoom','PokemonLeague_ChampionsRoom','PokemonLeague_HallOfFame']
ANCHORS = ['IndigoPlateau_Exterior','IndigoPlateau_PokemonCenter_2F']
SCRIPT_MAPS = MAPS + ['IndigoPlateau_Exterior']
def canonical(v): return (json.dumps(v, sort_keys=True, indent=2)+'\n').encode()
def need(v, m):
    if not v: raise ValueError(m)
def module(path, name):
    spec=importlib.util.spec_from_file_location(name,path); m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
def safe(path): need(not any(p.is_symlink() for p in [path,*path.parents]), 'Symlink in output: '+str(path))

def script_inventory(src, reader):
    # Index common labels read-only; retain only files in the explicit gym/gate closure.
    paths=sorted(p for p in src.tree if p.endswith('.inc') and ((p.startswith(('data/scripts/','data/text/')) or p.endswith('/text.inc') or any(p.startswith('data/maps/'+m+'/') for m in SCRIPT_MAPS))))
    paths+=['data/event_scripts.s']
    blocks={};aliases={}
    for path in paths:
        text=reader.git(src.reference,'cat-file','blob',src.tree[path][2]).decode();current=None;macro=False
        for line_no,line in enumerate(text.splitlines(),1):
            code=line.split('@')[0].strip()
            if code.startswith('.macro'):macro=True;current=None;continue
            if code=='.endm':macro=False;continue
            if macro:continue
            m=re.fullmatch(r'(\w+)::?',code)
            if m:
                current=m[1];need(current not in blocks,'Duplicate source label '+current)
                blocks[current]=dict(label=current,path=path,line=line_no,lines=[])
            elif current:blocks[current]['lines'].append(dict(line=line_no,text=line))
            m=re.fullmatch(r'\.(?:equ|set)\s+(\w+),\s*(\w+)',code)
            if m:aliases[m[1]]=m[2]
    roots={n for n,b in blocks.items() if any(b['path'].startswith('data/maps/'+m+'/') for m in SCRIPT_MAPS)}
    for name in MAPS:
        for kind in ['object_events','coord_events','bg_events']:
            roots.update(e['script'] for e in src.json('data/maps/'+name+'/map.json')[kind] if e.get('script') not in (None,'0','0x0','NULL'))
    roots.update(['EventScript_ResetEliteFourEnd','Std_MsgboxNPC','Std_MsgboxSign','Std_MsgboxDefault','Std_MsgboxYesNo'])
    selected=set();pending=sorted(roots);references=[];unresolved=[];opcodes=collections.Counter()
    while pending:
        label=pending.pop()
        if label in selected:continue
        need(label in blocks,'Missing source label '+label);selected.add(label)
        for row in blocks[label]['lines']:
            code=row['text'].split('@')[0].strip()
            if not code or code.startswith(('.','#')):continue
            op=code.split()[0];opcodes[op]+=1
            args=code.split(None,1)[1] if len(code.split(None,1))==2 else '';targets=[]
            if op in ('call','goto','goto_if_questlog','message'):targets=[args]
            elif op=='case':targets=[re.split(r'[,\s]+',args.strip())[-1]]
            elif op in ('map_script','map_script_2','applymovement') or op.startswith(('goto_if_','call_if_')):targets=[args.split(',')[-1].strip()]
            elif op in ('msgbox','giveitem_msg','msgreceiveditem','pokemart'):targets=[args.split(',')[0].strip()]
            elif op.startswith('trainerbattle_'):targets=[v.strip() for v in args.split(',')[1:]]
            for target in targets:
                if target not in ('0','0x0','NULL','NO_MUSIC') and aliases.get(target,target) not in blocks:unresolved.append(dict(label=label,line=row['line'],target=target))
            for token in re.findall(r'\b[A-Za-z_]\w*\b',code):
                token=aliases.get(token,token)
                if token in blocks:
                    references.append(dict(source=label,target=token,line=row['line']))
                    if token not in selected:pending.append(token)
    need(not unresolved,'Unresolved script references: '+repr(unresolved))
    result=[]
    for label in sorted(selected):
        b=blocks[label];src.read(b['path'],'pokemon-league-script-closure')
        result.append(dict(label=label,sourceRef=src.ref(b['path'],label,b['line']),lines=b['lines']))
    return dict(schemaVersion=1,profile=PROFILE,executable=False,roots=sorted(roots),blocks=result,
        aliases={k:v for k,v in aliases.items() if v in selected},references=references,unresolvedLabelReferences=unresolved,
        opcodeCounts=dict(sorted(opcodes.items())),sourcePaths=sorted({b['sourceRef']['path'] for b in result}))

def constants_inventory(src, shared, worlds, scripts):
    constants=shared.constants_inventory(src,worlds,scripts);definitions={}
    template=src.text('src/data/heal_locations.constants.json.txt')
    need('HEAL_LOCATION_NONE,' in template and '{{ heal_location.id }},' in template,'Heal location enum source differs')
    for i,row in enumerate(src.json('src/data/heal_locations.json')['heal_locations'],1):
        constants['values'][row['id']]=dict(value=i,sourceRef=src.ref('src/data/heal_locations.json',row['id']))
    for path in sorted(src.data):
        if not (path.startswith('include/') or path=='src/data/tilesets/metatiles.h'):continue
        for m in re.finditer(r'^#define[ \t]+([A-Za-z_]\w*)[ \t]+([^\n]+)',src.text(path),re.M):
            definitions[m[1]]=(m[2].split('//')[0].strip(),path,src.text(path).count('\n',0,m.start())+1)
    for m in re.finditer(r'^\s*([A-Z][A-Z0-9_]+)\s*=\s*([0-9]+)\s*$',src.text('asm/macros/event.inc'),re.M):
        definitions[m[1]]=(m[2],'asm/macros/event.inc',src.text('asm/macros/event.inc').count('\n',0,m.start())+1)
    pending=set()
    def resolve(name):
        if name in constants['values']:return constants['values'][name]['value']
        need(name in definitions and name not in pending,'Unresolved/cyclic constant '+name);pending.add(name)
        expression,path,line=definitions[name]
        def number(n):
            if isinstance(n,ast.Constant) and type(n.value) is int:return n.value
            if isinstance(n,ast.Name):return resolve(n.id)
            if isinstance(n,ast.BinOp):
                x,y=number(n.left),number(n.right)
                if isinstance(n.op,ast.Add):return x+y
                if isinstance(n.op,ast.Sub):return x-y
                if isinstance(n.op,ast.LShift) and 0<=y<32:return x<<y
                if isinstance(n.op,ast.BitOr):return x|y
            raise ValueError('Unsupported source expression '+name+': '+expression)
        value=number(ast.parse(expression,mode='eval').body);need(0<=value<=0x7fffffff,'Constant out of range')
        constants['values'][name]=dict(value=value,sourceRef=src.ref(path,name,line));pending.remove(name);return value
    operands=set(re.findall(r'\b[A-Z][A-Z0-9_]+\b','\n'.join(r['text'].split('@')[0] for b in scripts['blocks'] for r in b['lines'] if not r['text'].strip().startswith('.string'))))
    for symbol in sorted(operands & definitions.keys()):resolve(symbol)
    for symbol in sorted(set(re.findall(r'\bMETATILE_\w+\b','\n'.join(r['text'] for b in scripts['blocks'] for r in b['lines'])))):resolve(symbol)
    constants['nonNumericOperandTokens']=sorted(operands-constants['values'].keys())
    maps={src.json('data/maps/'+n+'/map.json')['id']:n for n in MAPS+ANCHORS}
    constants['symbolicMapOperands']={s:src.ref('data/maps/'+maps[s]+'/map.json',s) for s in constants['nonNumericOperandTokens'] if s in maps}
    need(set(constants['nonNumericOperandTokens'])<= {'STR_VAR_1','STR_VAR_2','STR_VAR_3','NO_MUSIC'}|maps.keys(),'Unexpected unresolved numeric operand: '+repr(constants['nonNumericOperandTokens']))
    return constants

def c_function(src, path, symbol):
    text=src.text(path)
    match=re.search(r'^[^\n;{}]*\b'+re.escape(symbol)+r'\([^;{}]*\)\n\{\n.*?^\}',text,re.M|re.S)
    need(match is not None,'Missing C mechanism '+symbol)
    start=text.count('\n',0,match.start())+1
    return dict(symbol=symbol,sourceRef=src.ref(path,symbol,start),endLine=start+match[0].count('\n'),text=match[0],runtimeImplemented=False)

def contracts(src,reader,worlds,scripts,constants):
    blocks={b['label']:b for b in scripts['blocks']};world={w['sourceName']:w for w in worlds}
    def commands(label):return [r['text'].split('@')[0].strip() for r in blocks[label]['lines'] if r['text'].split('@')[0].strip()]
    def expect(label, *needles):
        actual=commands(label)
        need(all(n in actual for n in needles),'Original mechanism differs: '+label)
        return blocks[label]
    tile_patches=[]
    for block in scripts['blocks']:
        patches=[]
        for row in block['lines']:
            m=re.fullmatch(r'\s*setmetatile (\d+), (\d+), (METATILE_\w+), ([01])',row['text'])
            if m:patches.append(dict(x=int(m[1]),y=int(m[2]),metatile=m[3],metatileId=constants['values'][m[3]]['value'],collision=int(m[4]),line=row['line']))
        if patches:tile_patches.append(dict(script=block['label'],sourceRef=block['sourceRef'],changes=patches,runtimeAdmitted=False))
    members=['Lorelei','Bruno','Agatha','Lance'];rooms=[]
    for index,name in enumerate(members):
        prefix='PokemonLeague_'+name+'sRoom';w=world[prefix];sym=name.upper()
        entry=expect(prefix+'_EventScript_EnterRoom','setvar VAR_MAP_SCENE_POKEMON_LEAGUE, '+str(index+1))
        expect(prefix+'_OnFrame','map_script_2 VAR_MAP_SCENE_POKEMON_LEAGUE, '+str(index)+', '+prefix+'_EventScript_EnterRoom')
        expect(prefix+'_OnLoad','call_if_eq VAR_MAP_SCENE_POKEMON_LEAGUE, '+str(index+1)+', '+prefix+'_EventScript_CloseEntry','call_if_set FLAG_DEFEATED_'+sym+', '+prefix+'_EventScript_SetDoorOpen')
        victory=expect(prefix+'_EventScript_Defeated'+name,'setflag FLAG_DEFEATED_'+sym,'call PokemonLeague_EventScript_OpenDoor'+('Lance' if index==3 else ''))
        battle=expect(prefix+'_EventScript_Battle','trainerbattle_no_intro TRAINER_ELITE_FOUR_'+sym+', '+prefix+'_Text_Defeat')
        rooms.append(dict(order=index+1,map=w['events']['id'],sourceName=prefix,actorLocalId=1,actor=w['events']['object_events'][0],entryScene=index,insideScene=index+1,
            defeatedFlag='FLAG_DEFEATED_'+sym,battleTrainer='TRAINER_ELITE_FOUR_'+sym,rematchTrainer='TRAINER_ELITE_FOUR_'+sym+'_2',battleSelectionFlag='FLAG_SYS_CAN_LINK_WITH_RS',
            introSelectionFlag='FLAG_SYS_CAN_LINK_WITH_RS' if index==0 else 'FLAG_SYS_GAME_CLEAR',entry=entry,victory=victory,battle=battle,runtimeAdmitted=False))
    need(len(tile_patches)==5,'Unexpected door/entry tile patch labels')
    for name,expected in [('PokemonLeague_EventScript_CloseEntry',6),('PokemonLeague_LancesRoom_EventScript_SetEntryClosed',8),('PokemonLeague_EventScript_SetDoorOpen',2),('PokemonLeague_EventScript_SetDoorOpenLance',2)]:
        need(len(next(p for p in tile_patches if p['script']==name)['changes'])==expected,'Door tile count differs')
    champion=[]
    # VAR_STARTER_MON is the player's choice. Trainer suffixes name the rival's starter.
    for value,player,rival in [(0,'BULBASAUR','CHARMANDER'),(1,'SQUIRTLE','BULBASAUR'),(2,'CHARMANDER','SQUIRTLE')]:
        script='PokemonLeague_ChampionsRoom_EventScript_Battle'+rival.title()
        expect('PokemonLeague_ChampionsRoom_EventScript_Battle','call_if_eq VAR_STARTER_MON, '+str(value)+', '+script)
        expect(script,'trainerbattle_no_intro TRAINER_CHAMPION_FIRST_'+rival+', PokemonLeague_ChampionsRoom_Text_Defeat')
        champion.append(dict(starterVariableValue=value,playerStarter='SPECIES_'+player,rivalStarter='SPECIES_'+rival,firstTrainer='TRAINER_CHAMPION_FIRST_'+rival,rematchTrainer='TRAINER_CHAMPION_REMATCH_'+rival,battleScript=blocks[script]))
    need('SPECIES_BULBASAUR,\n    SPECIES_SQUIRTLE,\n    SPECIES_CHARMANDER' in src.text('src/field_specials.c'),'Player starter order differs')
    reset=expect('EventScript_ResetEliteFour',*[f'clearflag FLAG_DEFEATED_{n}' for n in ['LORELEI','BRUNO','AGATHA','LANCE','CHAMP']],
        *[f'cleartrainerflag TRAINER_CHAMPION_{stage}_{r}' for stage in ['FIRST','REMATCH'] for r in ['SQUIRTLE','BULBASAUR','CHARMANDER']], 'setvar VAR_MAP_SCENE_POKEMON_LEAGUE, 0','return')
    need(len(commands('EventScript_ResetEliteFour'))==13,'Unexpected reset commands')
    hof=expect('PokemonLeague_HallOfFame_EventScript_EnterRoom','dofieldeffect FLDEFF_HALL_OF_FAME_RECORD','waitfieldeffect FLDEFF_HALL_OF_FAME_RECORD','call EventScript_SetDefeatedEliteFourFlagsVars','setrespawn HEAL_LOCATION_PALLET_TOWN','special EnterHallOfFame')
    c_specs={
        'src/battle_setup.c':['CB2_EndTrainerBattle'],
        'src/overworld.c':['DoWhiteOut','ComputeWhiteOutMoneyLoss','CB2_WhiteOut','Overworld_SetWhiteoutRespawnPoint','SetContinueGameWarpToHealLocation','CB2_ContinueSavedGame'],
        'src/post_battle_event_funcs.c':['EnterHallOfFame'],
        'src/heal_location.c':['GetHealLocationIndexFromMapGroupAndNum','GetHealLocation','SetWhiteoutRespawnWarpAndHealerNpc','SetWhiteoutRespawnHealerNpcAsLastTalked'],
        'src/field_effect.c':['FldEff_HallOfFameRecord','Task_HallOfFameRecord','HallOfFameRecordEffect_Init','HallOfFameRecordEffect_WaitForBallPlacement','HallOfFameRecordEffect_WaitForBallFlashing','HallOfFameRecordEffect_WaitForSoundAndEnd'],
        'src/hall_of_fame.c':['CB2_DoHallOfFameScreen','CB2_DoHallOfFameScreenDontSaveData','Task_Hof_InitMonData','Task_Hof_InitTeamSaveData','Task_Hof_TrySaveData','Task_Hof_HandleExit','SetWarpsToRollCredits'],
        'src/save.c':['HandleSavingData','TrySavingData'],
        'src/load_save.c':['SetContinueGameWarpStatus'],
        'src/field_specials.c':['GetStarterSpeciesById','GetStarterSpecies','DoPokemonLeagueLightingEffect'],
        'src/credits.c':['DoCredits','CB2_Credits']}
    mechanisms=[c_function(src,path,symbol) for path,symbols in c_specs.items() for symbol in symbols]
    funcs={x['symbol']:x['text'] for x in mechanisms}
    need('RunScriptImmediately(EventScript_ResetEliteFourEnd);' in funcs['DoWhiteOut'],'Whiteout reset absent')
    need('SetMainCallback2(CB2_WhiteOut);' in funcs['CB2_EndTrainerBattle'],'Loss callback differs')
    need('TrySavingData(SAVE_HALL_OF_FAME);' in funcs['Task_Hof_TrySaveData'],'HOF save differs')
    need('VarSet(VAR_MAP_SCENE_INDIGO_PLATEAU_EXTERIOR, 1);' in funcs['SetWarpsToRollCredits'] and ', -1, 11, 6);' in funcs['SetWarpsToRollCredits'],'Credits exterior warp differs')
    need('SoftReset(RESET_ALL);' in funcs['CB2_Credits'],'Credits terminal differs')
    need('warp->x = 13;\n            warp->y = 12;' in funcs['SetWhiteoutRespawnWarpAndHealerNpc'],'League whiteout coordinates differ')
    need('FieldEffectActiveListRemove(FLDEFF_HALL_OF_FAME_RECORD);' in funcs['HallOfFameRecordEffect_WaitForSoundAndEnd'],'HOF effect completion differs')
    heal=src.json('src/data/heal_locations.json')['heal_locations']; heal=[dict(record=r,sourceRef=src.ref('src/data/heal_locations.json',r['id'])) for r in heal if r['id'] in ['HEAL_LOCATION_INDIGO_PLATEAU','HEAL_LOCATION_PALLET_TOWN']]
    trainer_occurrences=[]
    for block in scripts['blocks']:
        for row in block['lines']:
            m=re.match(r'\s*trainerbattle_no_intro (TRAINER_\w+), (\w+)',row['text'])
            if m:trainer_occurrences.append(dict(trainer=m[1],sourceTrainerId=constants['values'][m[1]]['value'],defeatText=m[2],script=block['label'],sourceRef=src.ref(block['sourceRef']['path'],block['label'],row['line']),runtimeAdmitted=False))
    need(len(trainer_occurrences)==14,'League battle reference count differs')
    lobby=world[MAPS[0]];external=src.json('data/maps/IndigoPlateau_Exterior/map.json');upper=src.json('data/maps/IndigoPlateau_PokemonCenter_2F/map.json')
    return dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,firstClearOnly=True,
        mapTopology=reader.topology(src,worlds),objects=[dict(map=w['events']['id'],localId=i,event=e) for w in worlds for i,e in enumerate(w['events']['object_events'],1)],
        rooms=rooms,tilePatches=tile_patches,trainerOccurrences=trainer_occurrences,
        lobby=dict(map=lobby['events']['id'],healLocation='HEAL_LOCATION_INDIGO_PLATEAU',entry=blocks['IndigoPlateau_PokemonCenter_1F_OnTransition'],nurse=blocks['IndigoPlateau_PokemonCenter_1F_EventScript_Nurse'],shop=blocks['IndigoPlateau_PokemonCenter_1F_Items'],
            firstClearGuard='National Dex is not enabled; source guard stays (5,3). Later National Dex + no R/S-link flag moves it to (4,2), blocking entry.',healLocations=heal),
        champion=dict(branches=champion,starterSource=src.ref('src/field_specials.c','sStarterSpecies'),entry=blocks['PokemonLeague_ChampionsRoom_EventScript_EnterRoom'],
            trigger='OnResume sets VAR_TEMP_1=1 if either matching first/rematch trainer flag is defeated. OnFrame otherwise runs mandatory approach.',hofWarp=dict(map='MAP_POKEMON_LEAGUE_HALL_OF_FAME',x=5,y=12),runtimeAdmitted=False),
        defeatReset=dict(script=reset,entryScript=blocks['EventScript_ResetEliteFourEnd'],trigger='Every source DoWhiteOut, before money loss, healing and respawn; not only a League room loss.',
            trainerFlags='Clears the six Champion trainer flags. Elite Four progression uses five story flags; ordinary Elite Four trainer flags are not cleared here.',
            noAutomaticHealingBetweenRooms=True,noLossContinuationToVictoryScript=True,respawn=dict(map='MAP_INDIGO_PLATEAU_POKEMON_CENTER_1F',x=13,y=12,lastTalkedLocalId=2,sourceRef=src.ref('src/heal_location.c','SetWhiteoutRespawnWarpAndHealerNpc')),runtimeAdmitted=False),
        hallOfFame=dict(entry=hof,flagReset=blocks['EventScript_SetDefeatedEliteFourFlagsVars'],
            effectsOrder=['Record field effect and wait','Set temp scene1','Reset Elite Four and update postgame flags/fan-club; ready Pallet Oak scene2 if no National Dex; reset legendary flee flags','Set Pallet respawn','Fade','EnterHallOfFame heals party','Set FLAG_SYS_GAME_CLEAR and first-HOF play time','Set Continue warp status and Pallet Town (6,8) warp','Give eligible non-egg party Champion Ribbons','Build Hall of Fame team record','Save quest log, append bounded history','SAVE_HALL_OF_FAME writes HOF sectors then full normal save','Display record; exit to Indigo credits scene'],
            recordFieldEffect=dict(id='FLDEFF_HALL_OF_FAME_RECORD',sourceRef=src.ref('data/field_effect_scripts.s','gFldEffScript_HallOfFameRecord'),completion='WaitForSoundAndEnd observes glow sprite state >6, destroys sprite, removes active effect62 and destroys task. Source mechanism only; field-effect presentation is unimplemented.'),
            saveRecordFields=['species-or-egg','OT ID','personality','level','10 nickname bytes'],gameClearFlag='FLAG_SYS_GAME_CLEAR',saveMode='SAVE_HALL_OF_FAME',continueMap='MAP_PALLET_TOWN',continueX=6,continueY=8,
            revisionGuard='REVISION=0: EnterHallOfFame must not grant the REVISION>=0xA Aurora/Mystic tickets.',runtimeAdmitted=False),
        nativeMechanisms=mechanisms,
        reusedAnchors=[dict(sourceName='IndigoPlateau_Exterior',stableId='map.indigo-plateau-exterior',sourceRef=src.ref('data/maps/IndigoPlateau_Exterior/map.json',external['id']),geometryOwner='victory-road-region',generatedGeometryCopied=False,events=external)],
        boundaries=[dict(kind='UnsupportedCenter2F',map=lobby['events']['id'],warpIndex=2,destination=upper['id'],destinationWarpIndex=0,runtimeAdmitted=False,reason='Original 2F warp metadata retained; no 2F geometry or link services imported.'),
            dict(kind='Credits',map='MAP_INDIGO_PLATEAU_EXTERIOR',sceneVariable='VAR_MAP_SCENE_INDIGO_PLATEAU_EXTERIOR',sceneValue=1,x=11,y=6,script=blocks['IndigoPlateau_Exterior_EventScript_Credits'],runtimeAdmitted=False,terminal='DoCredits -> CB2_Credits -> SoftReset(RESET_ALL). Continue uses the saved Pallet warp.'),
            dict(kind='RematchesAndPostgame',selectionFlag='FLAG_SYS_CAN_LINK_WITH_RS',runtimeAdmitted=False,reason='Rematch scripts/declarations retained as source evidence only; first-clear profile must reject unrelated postgame state.')],
        limitations=['Source/content candidate only. No Application/Domain/Godot battle or UI changes.','No runtime, product-build, save, credits, native graphics or audible-output acceptance.','Script closure membership, including common cable/nurse/postgame routines, never grants executable admission.'])

def declarations(src, contract):
    trainers=[]
    for trainer in sorted({x['trainer'] for x in contract['trainerOccurrences']}):
        path='src/data/trainers.h';t=src.text(path);m=re.search(r'^    \['+trainer+r'\] = \{\n(.*?)^    \},',t,re.M|re.S);need(m is not None,'Missing trainer '+trainer)
        fields=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M));pm=re.fullmatch(r'ITEM_CUSTOM_MOVES\((\w+)\)',fields['party']);need(pm is not None,'Unexpected League party form')
        pp='src/data/trainer_parties.h';p=src.text(pp);party=re.search(r'^static const struct TrainerMonItemCustomMoves '+pm[1]+r'\[\] = \{\n(.*?)^\};',p,re.M|re.S);need(party is not None,'Missing League party')
        mons=[]
        for row in re.findall(r'    \{\n(.*?)    \},\n',party[1],re.S):
            f=dict(re.findall(r'^\s*\.(\w+) = (.+),$',row,re.M));mons.append(dict(species=f['species'],level=int(f['lvl']),iv=int(f['iv']),heldItem=f['heldItem'],moves=[v.strip() for v in f['moves'].strip('{}').split(',')],rawFields=f))
        need(len(mons) in (5,6),'League party length differs')
        trainers.append(dict(trainer=trainer,firstClear='_REMATCH_' not in trainer and not trainer.endswith('_2'),fields=fields,party=mons,battleAdmission=False,
            sourceRef=src.ref(path,trainer,t.count('\n',0,m.start())+1),partySource=src.ref(pp,pm[1],p.count('\n',0,party.start())+1)))
    need(len(trainers)==14 and sum(t['firstClear'] for t in trainers)==7,'Trainer declaration scope differs')
    return dict(schemaVersion=1,profile=PROFILE,runtimeAdmitted=False,ownedCatalogModified=False,trainers=trainers,firstClearTrainerCount=7,
        trainerSpecies=sorted({m['species'] for t in trainers if t['firstClear'] for m in t['party']}),trainerMoves=sorted({m for t in trainers if t['firstClear'] for p in t['party'] for m in p['moves']}))

def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',type=Path,required=True);ap.add_argument('--dependency-root',type=Path,required=True);ap.add_argument('--project-root',type=Path,default=ROOT);ap.add_argument('--check',action='store_true');a=ap.parse_args()
    target=a.project_root.absolute();dep=a.dependency_root.absolute();ref=a.reference_root.absolute();safe(target)
    need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)),'Reference/output overlap')
    reader=module(dep / 'scripts/import-cinnabar-source-preparation.py', 'pokemon_league_reader');reader.PRODUCT=dep;reader.PROFILE=PROFILE;reader.TILESETS=TILESETS
    src=reader.Source(ref)
    for name in SCRIPT_MAPS:
        for part in ['map.json','scripts.inc','text.inc']:
            p='data/maps/'+name+'/'+part
            if p in src.tree:src.read(p,'pokemon-league-map-or-reused-anchor')
    for name in ANCHORS:src.read('data/maps/'+name+'/map.json','reused-or-boundary-anchor')
    for p in ['src/data/tilesets/metatiles.h','src/data/trainers.h','src/data/trainer_parties.h','src/overworld.c','src/event_data.c','src/scrcmd.c','src/event_object_movement.c','src/battle_setup.c','src/field_specials.c','src/post_battle_event_funcs.c','src/hall_of_fame.c','src/save.c','src/load_save.c','src/credits.c','src/data/heal_locations.json','src/data/heal_locations.constants.json.txt','src/data/heal_locations.json.txt','src/heal_location.c','src/field_effect.c','data/field_effect_scripts.s','asm/macros/event.inc','include/gba/defines.h','include/hall_of_fame.h','include/save.h']:
        src.read(p,'source-mechanism-or-dependency')
    for n in ['field_weather','map_scripts','menu','battle_ai','flags','vars','items','opponents','trainers','event_objects','event_object_movement','metatile_behaviors','metatile_labels','map_types','maps','songs','fame_checker','global','script_menu','pokemon','species','moves','field_effects','party_menu','battle','region_map_sections','quest_log','layouts','game_stat']:
        path='include/constants/'+n+'.h'
        if path in src.tree:src.read(path,'script-and-event-constant')
    for base in TILESETS.values():
        for part in ['tiles.png','metatiles.bin','metatile_attributes.bin']+[f'palettes/{i:02d}.pal' for i in range(16)]:src.read('data/tilesets/'+base+'/'+part,'source-tileset')
    worlds=[reader.decode(src,n) for n in MAPS];scripts=script_inventory(src,reader)
    shared=reader.load_helper('_regional_service_shared.py');shared.PROFILE=PROFILE;shared.TILESETS=TILESETS;converter=reader.load_helper('export-runtime-maps.py')
    with tempfile.TemporaryDirectory(prefix='pokemon-league-region-',dir=target) as temp:
        stage=Path(temp)
        for w in worlds:shared.import_map(src,w['sourceName'],w['stableId'],stage/reader.directory(w['sourceName']))
        # Source graphics_id=0 on invisible journal actors means no visible sprite.
        # Preserve original map events, but do not ask the reviewed named-graphics reader to render them.
        visible=[dict(w,events=dict(w['events'],object_events=[e for e in w['events']['object_events'] if e['graphics_id']!='0'])) for w in worlds]
        npcs=shared.npc_inventory(src,visible,stage/'npc')
        npcs['invisibleActors']=[dict(map=w['events']['id'],localId=i,event=e) for w in worlds for i,e in enumerate(w['events']['object_events'],1) if e['graphics_id']=='0']
        constants=constants_inventory(src,shared,worlds,scripts);contract=contracts(src,reader,worlds,scripts,constants);deps=declarations(src,contract)
        entries=[]
        for w in worlds:
            name=reader.directory(w['sourceName']);rich=(stage/name/'runtime-map.json').read_bytes();compact=converter.encode(converter.normalize_runtime(json.loads(rich)));(stage/name/'runtime-map.json').write_bytes(compact)
            entries.append(dict(sourceName=w['sourceName'],stableId=w['stableId'],directory=name,sourceMapSymbol=w['events']['id'],path=name+'/runtime-map.json',reused=False))
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False,maps=MAPS);lockdata=canonical(lock)
        map_symbols={w['events']['id']:w['stableId'] for w in worlds}
        for name in ANCHORS:map_symbols[src.json('data/maps/'+name+'/map.json')['id']]='map.'+reader.directory(name)
        docs=package_documents(target / 'content/generated', constants, {'catalog.json':dict(schemaVersion=1,profile=PROFILE,sourceCommit=reader.COMMIT,runtimeIntegrated=False,maps=entries,tilesets=TILESETS,mapSymbols=map_symbols,inputFiles=lock['files']),
            'scripts.json':scripts,'contracts.json':contract,'npc-source.json':npcs,'geometry.json':reader.geometry_inventory(worlds)}, check=a.check)
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=reader.COMMIT,inputCount=len(src.entries),mapCount=len(worlds),newMapCount=7,cellCount=sum(w['width']*w['height'] for w in worlds),warpCount=sum(len(w['events']['warp_events']) for w in worlds),npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(scripts['blocks']),trainerOccurrenceCount=14,firstClearTrainerCount=7,unresolvedLabelCount=0,outputs=outputs)
        manifestdata=canonical(manifest);(stage/'manifest.json').write_bytes(manifestdata)
        writes=[(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]+[(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path,b in writes:
            safe(path)
            if a.check:need(path.is_file() and path.read_bytes()==b,'Regeneration differs: '+str(path))
            else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
        print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'}|dict(outputCount=len(outputs),fileCount=len(writes),totalBytes=sum(len(b) for _,b in writes)),indent=2))
if __name__=='__main__':main()
