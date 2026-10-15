#!/usr/bin/env python3
"""Project only the pinned Lost Cave trainer/encounter facts; execute no upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
import argparse,  json, re, subprocess
from pathlib import Path
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE='firered-lost-cave-trainers-v1'
MAPS=['FiveIsland_WaterLabyrinth','FiveIsland_ResortGorgeous','FiveIsland_ResortGorgeous_House','FiveIsland_LostCave_Entrance']+['FiveIsland_LostCave_Room'+str(i) for i in range(1,15)]

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-root',type=Path,required=True);p.add_argument('--project-root',type=Path,default=_PROJECT_ROOT);p.add_argument('--check',action='store_true');a=p.parse_args()
    raw={}
    def read(path):
        if path not in raw:raw[path]=subprocess.check_output(['git','--git-dir='+str(a.reference_root/'.git'),'show',COMMIT+':'+path])
        return raw[path].decode()
    def one(path,pattern):
        m=list(re.finditer(pattern,read(path),re.M|re.S));assert len(m)==1,(path,pattern,len(m));return m[0]
    def number(path,symbol):
        value=one(path,r'^#define '+re.escape(symbol)+r'[ \t]+([^\n]+)')[1].split('//')[0].strip()
        return int(value,0) if re.fullmatch(r'(?:0x[0-9a-fA-F]+|\d+)',value) else 1 << int(re.fullmatch(r'\(1 << (\d+)\)',value)[1])
    def ref(path,symbol,m):return dict(path=path,symbol=symbol,startLine=read(path).count('\n',0,m.start())+1,endLine=read(path).count('\n',0,m.end())+1)
    def ident(symbol):return {'value':'pret:'+symbol}
    def encode(name):return [int(one('charmap.txt',"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in name]
    occurrences=[]
    shared='data/scripts/trainers.inc'
    for name in MAPS:
        world=json.loads(read('data/maps/'+name+'/map.json'));path='data/maps/'+name+'/scripts.inc'
        local=read(path);labels=set(re.findall(r'^(\w+)::',local,re.M))|{o['script'] for o in world['object_events'] if o.get('script') not in (None,'0','0x0','NULL')}
        for label in sorted(labels):
            sp=path if re.search(r'^'+label+r'::',local,re.M) else shared
            if sp==shared and not re.search(r'^'+label+r'::',read(shared),re.M):continue
            block=one(sp,r'^'+label+r'::\n((?:(?!^\w+::).)*)')
            matches=list(re.finditer(r'^\t(trainerbattle_(?:single|no_intro)) ([^\n]+)$',block[1],re.M))
            for command in matches:
                args=command[2].split(', ');assert len(args)==(3 if command[1]=='trainerbattle_single' else 2)
                post=re.search(r'^\tmsgbox (\w+)',block[1][command.end():],re.M)
                occurrences.append(dict(trainer=args[0],opcode=command[1],arguments=args,script=label,map=name,source=ref(sp,label,block),postBattleText=post[1] if post else ''))
    assert len(occurrences)==11 and len({o['trainer'] for o in occurrences})==11
    trainers=[];battles=[];resolved=[]
    for o in sorted(occurrences,key=lambda o:o['trainer']):
        symbol=o['trainer'];path='src/data/trainers.h';m=one(path,r'^    \['+symbol+r'\] = \{\n(.*?)^    \},');fields=dict(re.findall(r'^        \.(\w+) = (.*),$',m[1],re.M))
        shape,party_symbol=re.fullmatch(r'((?:NO_)?ITEM_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)',fields['party']).groups();held=not shape.startswith('NO_');custom='CUSTOM' in shape
        pp='src/data/trainer_parties.h';party=one(pp,r'^static const struct TrainerMon'+('Item' if held else 'NoItem')+('Custom' if custom else 'Default')+'Moves '+party_symbol+r'\[\] = \{\n(.*?)^\};')
        rows=list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.heldItem = (ITEM_\w+),\n)?(?:        \.moves = \{([^}]+)\},\n)?    \},\n',party[1]));assert ''.join(r[0] for r in rows)==party[1]
        members=[];movesets=[]
        for row in rows:
            iv,level,species=int(row[1]),int(row[2]),row[3];assert bool(row[4])==held and bool(row[5])==custom
            np='src/data/text/species_names.h';nm=one(np,r'^    \['+species+r'\] = _\("([^"]+)"\),$')
            moves=[v.strip() for v in row[5].split(',')] if custom else []
            members.append(dict(species=ident(species),sourceSpeciesId=number('include/constants/species.h',species),speciesName=nm[1],encodedSpeciesName=encode(nm[1]),level=level,sourceIv=iv,heldItem=row[4] or 'ITEM_NONE',customMoves=[ident(x) for x in moves],source=ref(pp,party_symbol,party),speciesNameSource=ref(np,species,nm)))
            if not custom:
                pointer=one('src/data/pokemon/level_up_learnset_pointers.h',r'^    \['+species+r'\] = (\w+),$')[1];body=one('src/data/pokemon/level_up_learnsets.h',r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};')[1]
                assert '#if' not in body,'Conditional learnset requires explicit FireRed branch'
                for lv,move in re.findall(r'LEVEL_UP_MOVE\(\s*(\d+), (MOVE_\w+)\)',body):
                    if int(lv)<=level and move not in moves:moves=(moves+[move])[-4:]
            movesets.append(dict(species=species,level=level,moves=moves))
        name=re.fullmatch(r'_\("([^"]+)"\)',fields['trainerName'])[1];ai=fields['aiFlags'].split(' | ');items=[] if fields['items']=='{}' else fields['items'].strip('{}').split(', ');items+=['ITEM_NONE']*(4-len(items));money=one('src/battle_main.c',r'^\s*\{'+fields['trainerClass']+r', (\d+)\},$')
        trainers.append(dict(id=ident(symbol),sourceTrainerId=number('include/constants/opponents.h',symbol),trainerClass=fields['trainerClass'],sourceTrainerClass=number('include/constants/trainers.h',fields['trainerClass']),trainerPic=fields['trainerPic'],sourceTrainerPic=number('include/constants/trainers.h',fields['trainerPic']),sourceTrainerName=name,encodedTrainerName=encode(name),encounterMusicGender=sum(int(v) if v.isdigit() else number('include/constants/trainers.h',v) for v in fields['encounterMusic_gender'].split(' | ')),items=items,doubleBattle=fields['doubleBattle']=='TRUE',partyFlags=(number('include/constants/trainers.h','F_TRAINER_PARTY_HELD_ITEM') if held else 0)+(number('include/constants/trainers.h','F_TRAINER_PARTY_CUSTOM_MOVESET') if custom else 0),aiFlags=sum(number('include/constants/battle_ai.h',x) for x in ai),aiFlagSymbols=ai,baseMoney=int(money[1]),party=members,source=ref(path,symbol,m),moneySource=ref('src/battle_main.c',fields['trainerClass'],money)))
        no_intro=o['opcode']=='trainerbattle_no_intro';battles.append(dict(trainer=ident(symbol),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT' if no_intro else 'TRAINER_BATTLE_SINGLE'),rivalFlags=0,battleTypeFlags=8,scriptEntry=o['script'],introText='' if no_intro else o['arguments'][1],defeatText=o['arguments'][-1],victoryText='',postBattleText=o['postBattleText'],entryScript='EventScript_DoNoIntroTrainerBattle' if no_intro else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=o['source'],postBattleScript=None,musicOption=None))
        resolved.append(dict(trainer=symbol,party=movesets))
    wild=json.loads(read('src/data/wild_encounters.json'));group=next(g for g in wild['wild_encounter_groups'] if g.get('label')=='gWildMonHeaders');map_ids={json.loads(read('data/maps/'+n+'/map.json'))['id'] for n in MAPS};encounters=[e for e in group['encounters'] if e['map'] in map_ids and e['base_label'].endswith('_FireRed')];assert len(encounters)==16
    tm='src/data/pokemon/tmhm_learnsets.h';tmrow=one(tm,r'^    \[SPECIES_MURKROW\]\s*=\s*TMHM_LEARNSET\((.*?)\),');tm_source=ref(tm,'SPECIES_MURKROW',tmrow)
    machine_moves=['pret:MOVE_'+m for m in re.findall(r'TMHM\((?:TM|HM)\d+_(\w+)\)',tmrow[1])]
    ep='src/data/pokemon/egg_moves.h';egg=one(ep,r'egg_moves\(MURKROW,\s*(.*?)\),');egg_moves=['pret:'+m for m in re.findall(r'MOVE_\w+',egg[1])]
    sp='src/data/pokemon/species_info.h';species=one(sp,r'^    \[SPECIES_MURKROW\] =\n    \{\n(.*?)^    \},')
    groups=re.search(r'\.eggGroups = \{(EGG_GROUP_\w+), (EGG_GROUP_\w+)\}',species[1]).groups();cycles=int(re.search(r'\.eggCycles = (\d+)',species[1])[1])
    metadata=dict(breeding=dict(species=ident('SPECIES_MURKROW'),eggGroup1=number('include/constants/pokemon.h',groups[0]),eggGroup2=number('include/constants/pokemon.h',groups[1]),eggCycles=cycles),baseEggSpecies=ident('SPECIES_MURKROW'),eggMoves=[dict(value=m) for m in egg_moves],machineMoves=[dict(value=m) for m in machine_moves])
    doc=dict(completeSourceRoster=False,murkrowMetadata=metadata,murkrowMachineSource=tm_source,trainers=trainers,battles=battles,resolvedMoves=resolved,encounters=encounters,slotTables=group['fields'],sources=[dict(path=k) for k,v in sorted(raw.items())])
    outputs={'content/generated/data/encounters/lost-cave.json':trainer_json(a.project_root/'content/generated', doc, check=a.check).decode()}
    # Full typed source records are compiled once; no deserializer, data path or second catalog is needed at runtime.
    q=lambda x:json.dumps(x,ensure_ascii=False);arr=lambda xs:'['+', '.join(q(x) for x in xs)+']'
    def sr(r):return 'new('+', '.join([q(r['path']),q(r['symbol']),str(r['startLine']),str(r['endLine'])])+')'
    lines=['// Generated by scripts/imports/story/import-lost-cave-encounter-rules.py from pinned original source.','using System.Collections.Immutable;','using Game.Domain.Ids;','namespace Game.Domain.Trainers;','internal static class LostCaveTrainerSource','{','    internal static LostCaveTrainerCatalog Create(VsSeekerTrainerCatalog inherited, ImmutableDictionary<TrainerId, TrainerDefinition> shared) => new(','        TrainerIds.ToImmutableDictionary(id => id, id => shared[id]),','        new CeruleanTrainerBattleDefinition[] {']
    added={'TRAINER_LADY_SELPHY','TRAINER_RUIN_MANIAC_LAWSON','TRAINER_PSYCHIC_LAURA'}
    for b in battles:lines.append('            new(new('+q(b['trainer']['value'])+'), '+', '.join([str(b['mode']),'0','8',q(b['scriptEntry']),q(b['introText']),q(b['defeatText']),q(b['victoryText']),q(b['postBattleText']),q(b['entryScript']),q(b['callback']),sr(b['source'])])+'),')
    lines.append('        }.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal), inherited);')
    lines.append('    internal static ImmutableArray<TrainerId> InheritedIds { get; } = ['+', '.join('new('+q(t['id']['value'])+')' for t in trainers if t['id']['value'][5:] not in added)+'];')
    for title,kind,records,key in [('TrainerIds','TrainerId',trainers,'id'),('BattleScripts','string',battles,'scriptEntry')]:
        lines.append('    internal static ImmutableHashSet<'+kind+'> '+title+' { get; } = new HashSet<'+kind+'> {')
        for r in records:
            k='new('+q(r[key]['value'])+')' if kind=='TrainerId' else q(r[key])
            lines.append('        '+k+',')
        lines.append('    }.ToImmutableHashSet();')
    lines.append('}');outputs['src/Game.Domain/Trainers/LostCaveTrainerSource.cs']='\n'.join(lines)+'\n'
    def map_id(symbol):
        n=next(n for n in MAPS if json.loads(read('data/maps/'+n+'/map.json'))['id']==symbol)
        return 'map.'+re.sub(r'(?<=[a-z])(?=[A-Z])','-',n).replace('_','-').lower()
    weights={f['type']:f['encounter_rates'] for f in group['fields']}
    lines=['// Generated by scripts/imports/story/import-lost-cave-encounter-rules.py from pinned original FireRed rows.','using System.Collections.Immutable;','using Game.Domain.Ids;','namespace Game.Domain.Encounters;','public static class LostCaveEncounterTables','{']
    for title,typ,kind,indices in [('Land','EarlyLandEncounterTable','land_mons',range(12)),('Water','WaterEncounterTable','water_mons',range(5)),('OldRod','OldRodEncounterTable','fishing_mons',range(2)),('GoodRod','OldRodEncounterTable','fishing_mons',range(2,5))]:
        lines.append('    public static ImmutableArray<'+typ+'> '+title+' { get; } = [')
        for e in encounters:
            if kind not in e:continue
            values=e[kind];lines.append('        new(new('+q(map_id(e['map']))+'), '+str(values['encounter_rate'])+', [')
            for i in indices:
                m=values['mons'][i];lines.append('            new('+str(i)+', new('+q('pret:'+m['species'])+'), '+str(m['min_level'])+', '+str(m['max_level'])+', '+str(weights[kind][i])+'),')
            lines.append('        ]),')
        lines.append('    ];')
    lines.extend(['    // Super Rod is already authenticated globally; preserve its original objects and slot indices.','    public static ImmutableArray<OldRodEncounterTable> SuperRod { get; } = SuperRodEncounterRules.Tables','        .Where(t => Water.Any(w => w.Map == t.Map)).ToImmutableArray();','}']);outputs['src/Game.Domain/Encounters/LostCaveEncounterTables.cs']='\n'.join(lines)+'\n'
    moves=lambda xs:'['+', '.join('new('+q(m)+')' for m in xs)+']'
    lines=['// Generated from the same pinned source; the 58 inheritance slots remain distinct from 57 obtainable machines.','using Game.Domain.Items;','namespace Game.Domain.Pokemon;','internal static class LostCaveSpeciesSource','{',
        '    internal static SourceEggSpeciesRow Metadata { get; } = new(new(new("pret:SPECIES_MURKROW"), '+str(metadata['breeding']['eggGroup1'])+', '+str(metadata['breeding']['eggGroup2'])+', '+str(cycles)+'), new("pret:SPECIES_MURKROW"), '+moves(egg_moves)+', '+moves(machine_moves)+');',
        '    internal static OpeningItemSourceRef MachineSource { get; } = '+sr(tm_source)+';','}']
    outputs['src/Game.Domain/Pokemon/LostCaveSpeciesSource.cs']='\n'.join(lines)+'\n'
    for name,value in outputs.items():
        target=a.project_root/name
        if a.check:assert target.read_text()==value,'Generated output differs: '+name
        else:target.parent.mkdir(parents=True,exist_ok=True);target.write_text(value)
    print(json.dumps({'checked':a.check,'trainers':len(trainers),'members':sum(len(t['party']) for t in trainers),'wildMaps':len(encounters),'sourceSlots':sum(len(v['mons']) for e in encounters for v in e.values() if isinstance(v,dict)),'outputs':list(outputs)}))
if __name__=='__main__':main()
