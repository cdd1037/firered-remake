#!/usr/bin/env python3
"""Import the pinned original Teachy TV catalog and its small dedicated resources.

Reads authenticated local Git blobs only; never runs upstream programs. Generated
catalogs, pixels and preserved reference inputs belong in content/, not source.
"""
import argparse
import sys
sys.dont_write_bytecode = True
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-teachy-tv-v1'


def need(ok, message):
    if not ok:
        raise ValueError(message)


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode()


def clean(text):
    return re.sub(r'//[^\n]*|/\*.*?\*/', '', text, flags=re.S)


def array(text, symbol):
    match = re.search(r'\b' + re.escape(symbol) + r'\[.*?\](?:\)\(u8\))?\s*=\s*\{(.*?)\n\};', text, re.S)
    need(match is not None, 'teachy_tv_array:' + symbol)
    return clean(match[1])


def words(text, symbol):
    return [x.strip() for x in array(text, symbol).split(',') if x.strip()]


def source_ref(text, path, symbol, needle=None):
    match = re.search(needle or (r'\b' + re.escape(symbol) + r'\['), text)
    need(match is not None, 'teachy_tv_source_ref:' + symbol)
    return dict(path=path, line=text[:match.start()].count('\n') + 1, symbol=symbol)


def function(text, symbol):
    match = re.search(r'^(?:static )?[\w *]+\b' + symbol + r'\([^;]*?\)\n\{(.*?)\n\}', text, re.S | re.M)
    need(match is not None, 'teachy_tv_function:' + symbol)
    return match[0], text[:match.start()].count('\n') + 1


def text_catalog(sources):
    records = {}
    path = 'src/data/text/teachy_tv.h'
    text = sources[path].decode()
    for match in re.finditer(r'const u8 (\w+)\[\] = _\((.*?)\);', text, re.S):
        value = ''.join(re.findall(r'"((?:[^"\\]|\\.)*)"', match[2]))
        records[match[1]] = dict(symbol=match[1], encodedText=value,
            controlCodes=re.findall(r'\{[^{}]+\}|\\[npl]', value),
            source=dict(path=path, line=text[:match.start()].count('\n') + 1, symbol=match[1]))
    path = 'data/text/pokedude.inc'
    text = sources[path].decode()
    for match in re.finditer(r'^(\w+)::\n((?:\s*\.string[^\n]*\n?)+)', text, re.M):
        value = ''.join(re.findall(r'\.string "((?:[^"\\]|\\.)*)"', match[2]))
        records[match[1]] = dict(symbol=match[1], encodedText=value,
            controlCodes=re.findall(r'\{[^{}]+\}|\\[npl]|\$', value),
            source=dict(path=path, line=text[:match.start()].count('\n') + 1, symbol=match[1]))
    return records


def fields(body):
    return {m[1]: m[2].strip() for m in re.finditer(r'\.(\w+)\s*=\s*(\{[^{}]*\}|[^,\n]+)', body)}


def numeric_rows(text, symbol):
    rows = []
    for match in re.finditer(r'\{\s*\.cursorPos\s*=\s*\{([^}]+)\},\s*\.delay\s*=\s*\{([^}]+)\}\s*\}', array(text, symbol)):
        row = dict(cursorPositions=[int(x.strip()) for x in match[1].split(',')],
                   delays=[int(x.strip()) for x in match[2].split(',')])
        need(len(row['cursorPositions']) == len(row['delays']) == 2, 'teachy_tv_controller_width')
        rows.append(row)
    need(rows, 'teachy_tv_controller_empty:' + symbol)
    return rows


def convert(lock, sources):
    outer_path, battle_path = 'src/teachy_tv.c', 'src/battle_controller_pokedude.c'
    outer, battle = sources[outer_path].decode(), sources[battle_path].decode()
    texts = text_catalog(sources)
    indices = re.findall(r'\bTTVSCR_\w+\b', sources['include/teachy_tv.h'].decode())
    need(indices == ['TTVSCR_BATTLE','TTVSCR_STATUS','TTVSCR_MATCHUPS','TTVSCR_CATCHING','TTVSCR_TMS','TTVSCR_REGISTER'], 'teachy_tv_enum')
    menu = []
    for name in ['sListMenuItems', 'sListMenuItems_NoTMCase']:
        entries = [dict(labelSymbol=m[1], sourceIndex=(-2 if m[2] == '-2' else indices.index(m[2])))
                   for m in re.finditer(r'\.label\s*=\s*(\w+),\s*\.index\s*=\s*(-2|TTVSCR_\w+)', array(outer, name))]
        need(len(entries) == (7 if name == 'sListMenuItems' else 5), 'teachy_tv_menu_count')
        menu.append(dict(sourceSymbol=name, requiresTmCase=name == 'sListMenuItems', maxVisibleRows=6 if len(entries)==7 else 5, entries=entries))
    scripts = ['sBattleScript','sStatusScript','sMatchupsScript','sCatchingScript','sTMsScript','sRegisterKeyItemScript']
    first_texts = ['Battle','Status','Matchups','Catching','TMs','Register']
    resumes = [int(x) for x in words(outer, 'sWhereToReturnToFromBattle')]
    need(resumes == [12,12,12,12,9,9], 'teachy_tv_resume_indices')
    lessons = []
    commands = set()
    for index, script in enumerate(scripts):
        command_names = words(outer, script)
        need(len(command_names) == (19 if index < 4 else 16), 'teachy_tv_script_length')
        commands.update(command_names)
        lesson = dict(sourceIndex=index, sourceSymbol=indices[index], labelSymbol=menu[0]['entries'][index]['labelSymbol'],
            requiresTmCase=index >= 4, scriptSymbol=script, commands=command_names,
            introductionText='gTeachyTvText_' + first_texts[index] + 'Script1',
            conclusionText='gTeachyTvText_' + first_texts[index] + 'Script2',
            demonstrationKind='PokedudeBattle' if index<4 else 'TmCase' if index==4 else 'RegisterItem',
            resumeCommandIndex=resumes[index], source=source_ref(outer, outer_path, script))
        if index < 4:
            suffix = first_texts[index]
            party = []
            for match in re.finditer(r'\{\s*\.side\s*=.*?\n\s*\}', array(battle, 'sParties_' + suffix), re.S):
                data = fields(match[0])
                moves = [x.strip() for x in data['moves'].strip('{} ').split(',')]
                moves += ['MOVE_NONE'] * (4-len(moves))
                party.append(dict(side=data['side'], level=int(data['level']), species=data['species'], moves=moves,
                                  nature=data['nature'], gender=data['gender'], fixedIv=0))
            voice_texts = words(battle, 'sPokedudeTexts_' + ('TypeMatchup' if index==2 else suffix))
            voiceovers = []
            for match in re.finditer(r'\{([^{}]*)\}', array(battle, 'sPokedudeTextScripts_' + suffix), re.S):
                data = fields(match[1])
                voiceovers.append(dict(controllerCommand=data['btlcmd'], side=data['side'],
                    stringId=data.get('stringid','0'), callback=data['callback'], textSymbol=voice_texts[len(voiceovers)]))
            need(len(voiceovers) == len(voice_texts) and all(x['textSymbol'] in texts for x in voiceovers), 'teachy_tv_voiceovers')
            need(len(party) == (3 if index==2 else 2), 'teachy_tv_party')
            lesson['battle'] = dict(sourceBattleType='BATTLE_TYPE_POKEDUDE', sourceBattleTypeValue=1<<16,
                party=party, chooseAction=numeric_rows(battle, 'sInputScripts_ChooseAction_' + suffix),
                chooseMove=numeric_rows(battle, 'sInputScripts_ChooseMove_' + suffix), voiceovers=voiceovers,
                source=source_ref(battle, battle_path, 'sParties_' + suffix))
        else:
            lesson['battle'] = None
        lessons.append(lesson)
    command_inventory = []
    for symbol in sorted(commands):
        body,line = function(outer, symbol)
        command_inventory.append(dict(symbol=symbol, source=dict(path=outer_path,line=line,symbol=symbol),
            textSymbols=sorted(set(re.findall(r'\bgTeachyTvText_\w+', body))),
            tickConditions=re.findall(r'if\s*\(([^\n]*data\[2\][^\n]*)\)',body),
            kind='Demonstration' if symbol=='TTVcmd_TaskBattleOrFadeByOptionChosen' else
                 'ReturnToMenu' if symbol=='TTVcmd_End' else
                 'Confirm' if symbol=='TTVcmd_EraseTextWindowIfKeyPressed' else 'Presentation'))
    controllers = []
    for path,names in {
        'src/item_menu.c':['BackUpPlayerBag','RestorePlayerBag','InitPokedudeBag','Task_BButtonInterruptTeachyTv','Task_Bag_TeachyTvRegister','Task_Bag_TeachyTvCatching','Task_Bag_TeachyTvStatus','Task_Bag_TeachyTvTMs'],
        'src/tm_case.c':['Pokedude_InitTMCase','Task_Pokedude_Start','Task_Pokedude_Run'],
        'src/party_menu.c':['Task_PartyMenu_PokedudeStep','Task_PartyMenuFromBag_PokedudeStep','PartyMenuHandlePokedudeCancel']}.items():
        for name in names:
            body,line=function(sources[path].decode(),name)
            controllers.append(dict(symbol=name, source=dict(path=path,line=line,symbol=name),
                sourceCases=re.findall(r'^\s*case\s+([^:]+):',body,re.M),
                textSymbols=sorted(set(re.findall(r'\bgPokedudeText_\w+',body))),
                itemSymbols=sorted(set(re.findall(r'\bITEM_\w+',body)))))
    hooks=[]
    for row in lock['files']:
        path=row['path']
        if not path.startswith('src/') or not path.endswith('.c'): continue
        for line,text in enumerate(sources[path].decode().splitlines(),1):
            if 'BATTLE_TYPE_POKEDUDE' in text:
                hooks.append(dict(source=dict(path=path,line=line,symbol='BATTLE_TYPE_POKEDUDE'), code=text.strip()))
    resources=[row for row in lock['files'] if row['kind']=='resource']
    def demo_items(path, name):
        body,_=function(sources[path].decode(),name)
        return [dict(item=m[1],quantity=int(m[2])) for m in re.finditer(r'AddBagItem\((ITEM_\w+), (\d+)\)',body)]
    animations=[]
    animation_path='src/data/object_events/object_event_anims.h'
    animation_source=sources[animation_path].decode()
    for index,(key,name) in enumerate(re.findall(r'\[(\w+)\]\s*=\s*(\w+)',array(animation_source,'sAnimTable_Standard'))[:8]):
        body=array(animation_source,name)
        frames=[dict(frame=int(m[1]),ticks=int(m[2]),hFlip='hFlip = TRUE' in m[3])
                for m in re.finditer(r'ANIMCMD_FRAME\((\d+),\s*(\d+)([^)]*)\)',body)]
        animations.append(dict(sourceIndex=index,sourceSymbol=key,frames=frames,loops='ANIMCMD_JUMP(0)' in body,
            source=source_ref(animation_source,animation_path,name)))
    need(len(texts)==42 and len(commands)==16, 'teachy_tv_catalog_completeness')
    return dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],
        menu=menu,helloText='gTeachyTvText_PokedudeSaysHello',lessons=lessons,texts=list(texts.values()),
        commandInventory=command_inventory,externalControllers=controllers,battleModeReferences=hooks,
        demonstrationInventory=dict(bag=demo_items('src/item_menu.c','InitPokedudeBag'),tmCase=demo_items('src/tm_case.c','Pokedude_InitTMCase')),
        resourceInputs=resources,returnCallbacks=dict(bag='CB2_BagMenuFromStartMenu',registeredItem='CB2_ReturnToField'),
        presentationContract=dict(route1Crop=dict(x=8,y=6,width=16,height=9,sourceLayout='Route1_Layout'),
            initialBackgroundScroll=dict(x=-16,y=40),hostAnimations=animations,
            normalMapMusicOnQuit='Overworld_PlaySpecialMapMusic',menuMusic='MUS_TEACHY_TV_MENU',showMusic='MUS_FOLLOW_ME',
            sourceShowMidi='sound/songs/midi/mus_follow_me.mid',
            unusedNamedShowMidi='sound/songs/midi/mus_teachy_tv_show.mid',
            noiseRng='TeachyTvBg2AnimController consumes 12*26 Random() draws per invocation; keep semantic RNG policy explicit.'),
        limitations=['Catalog and outer coordinator only; battle, bag, party and TM-case demos require their real shared rules owners.',
            'No scripted victory, damage, capture, rewards or EXP are synthesized by this catalog.',
            'Original text controls are preserved encoded; existing source text renderer must interpret them.',
            'Dedicated graphics and Route1 source closure are resources; runtime composition and audible output are separate gates.'])


def read_sources(git_dir, lock_path):
    raw=lock_path.read_bytes()
    lock=json.loads(raw)
    need((lock['commit'], lock['profile']) == (COMMIT, PROFILE), 'teachy_tv_lock_identity')
    def git(*args):
        return subprocess.check_output(['git','--git-dir='+str(git_dir),*args],timeout=30)
    sources={}
    for row in lock['files']:
        path=row['path']
        need(not Path(path).is_absolute() and '..' not in Path(path).parts and path not in sources,'teachy_tv_path')
        raw=git('show',COMMIT+':'+path)
        sources[path]=raw
    return lock,sources


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--git-dir',required=True,type=Path)
    parser.add_argument('--lock',type=Path,default=ROOT/'scripts/teachy-tv.lock.json')
    parser.add_argument('--output',type=Path,default=ROOT/'content/generated/teachy-tv')
    parser.add_argument('--raw-output',type=Path,default=ROOT/'content/raw/pokefirered/037335f4')
    args=parser.parse_args()
    lock,sources=read_sources(args.git_dir,args.lock)
    data=encoded(convert(lock,sources))
    # No files are written until all source identities and the complete conversion pass.
    for directory in [args.output,args.raw_output]:
        need(not any(p.is_symlink() for p in [directory,*directory.parents]),'teachy_tv_output_symlink')
    for path,raw in sources.items():
        target=args.raw_output/path
        need(not any(p.is_symlink() for p in [target,*target.parents]),'teachy_tv_output_symlink')
        target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(raw)
    args.output.mkdir(parents=True,exist_ok=True)
    (args.output/'teachy-tv.json').write_bytes(data)
    print(json.dumps(dict(profile=PROFILE,lessons=6,texts=len(convert(lock,sources)['texts']),bytes=len(data))))


if __name__=='__main__':
    main()
