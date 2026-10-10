#!/usr/bin/env python3
"""Import bounded original FireRed main-menu metadata from pinned local Git/raw bytes.

Never executes source scripts, fetches a ROM, or substitutes invented UI geometry.
All textual source and JSON use strict UTF-8; all input bytes are authenticated.
"""
import argparse
import ast
import hashlib
import json
import pathlib
import re
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-main-menu-source-v1'
LOCK_SHA256 = '3b2df46bd09fe2a92df75b339be5c22506e9560c925c751d175df54b0023c5ae'
MANIFEST_SHA256 = 'fc7c5172d6663679c2a549b702bb43291ac49dc6ad977fb475f626ae82c6f3e5'
PATHS = frozenset('''src/main_menu.c
src/strings.c
src/start_menu.c
src/save.c
src/new_game.c
src/menu2.c
data/text/save.inc
include/save.h
include/global.h
include/constants/global.h
include/constants/flags.h
include/constants/opponents.h
include/constants/rgb.h
include/constants/songs.h
include/text.h
include/window.h
include/characters.h
include/pokedex.h
include/gba/defines.h
include/gba/io_reg.h
tools/gbagfx/gfx.c
graphics/main_menu/bg.pal
graphics/main_menu/textbox.pal'''.splitlines())


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    if relative is None:
        target = root
    else:
        path = pathlib.PurePosixPath(relative)
        if not relative or path.is_absolute() or '\\' in relative or any(p in ('', '.', '..') for p in relative.split('/')):
            raise ValueError('Unsafe main-menu path: ' + relative)
        target = root / path
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Symlink input/output rejected: ' + str(target))
    return target


def source_hash(files):
    return sha(''.join(e['path'] + '\0' + e['sha256'] + '\n' for e in sorted(files, key=lambda e: e['path'])).encode('utf-8'))


def load_lock(path=None):
    data = safe(path or ROOT / 'content/main-menu-source-lock.json').read_bytes()
    if sha(data) != LOCK_SHA256:
        raise ValueError('Reviewed main-menu source lock differs')
    lock = json.loads(data.decode('utf-8'), object_pairs_hook=unique_pairs)
    if (lock['schemaVersion'], lock['repository'], lock['commit'], lock['tree'], lock['profile'], lock['gameEdition'], lock['romRevision']) != (1, 'pret/pokefirered', COMMIT, TREE, PROFILE, 'FIRERED', 0):
        raise ValueError('Main-menu source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Main-menu input inventory differs')
    return lock


def verify(data, entry):
    blob = hashlib.sha1(b'blob ' + str(len(data)).encode('ascii') + b'\0' + data).hexdigest()
    if (len(data), sha(data), blob) != (entry['byteLength'], entry['sha256'], entry['gitBlobSha1']):
        raise ValueError('Main-menu source hash differs: ' + entry['path'])
    data.decode('utf-8')


def raw_manifest(lock):
    return canonical(dict(schemaVersion=1, profile=PROFILE, commit=COMMIT, tree=TREE,
        lockSha256=LOCK_SHA256, fullSourceHash=source_hash(lock['files']), inputCount=len(PATHS)))


def read_sources(lock, raw, repository=None):
    safe(raw)
    def git(*args):
        return subprocess.run(['git', '-C', str(repository), *args], check=True, capture_output=True, timeout=30).stdout
    if repository is None:
        if safe(raw, 'manifest.json').read_bytes() != raw_manifest(lock):
            raise ValueError('Main-menu raw manifest differs')
    elif git('rev-parse', COMMIT + '^{tree}').decode('ascii').strip() != TREE:
        raise ValueError('Main-menu reference tree differs')
    result = {}
    for entry in lock['files']:
        target = safe(raw, entry['path'])
        if repository is None:
            data = target.read_bytes()
        else:
            row = git('ls-tree', COMMIT, '--', entry['path']).decode('utf-8')
            if row != '100644 blob ' + entry['gitBlobSha1'] + '\t' + entry['path'] + '\n':
                raise ValueError('Main-menu source is not a pinned regular Git blob')
            data = git('cat-file', 'blob', entry['gitBlobSha1'])
        verify(data, entry)
        result[entry['path']] = data
    return result


class Source:
    def __init__(self, lock, data):
        if set(data) != PATHS:
            raise ValueError('Incomplete main-menu source set')
        self.entries = {e['path']: e for e in lock['files']}
        for p, b in data.items():
            verify(b, self.entries[p])
        self.texts = {p: b.decode('utf-8') for p, b in data.items()}
        self.defines = {}
        for path, text in self.texts.items():
            for m in re.finditer(r'^#define\s+(\w+)\s+([^\n]+)', text, re.M):
                self.defines[m[1]] = (m[2].split('//')[0].strip(), path, m.start())
        self.constants = {}

    def ref(self, path, symbol, start, end=None):
        text = self.texts[path]
        return dict(path=path, symbol=symbol, startLine=text.count('\n', 0, start)+1,
            endLine=text.count('\n', 0, end if end is not None else start)+1, sha256=self.entries[path]['sha256'])

    def match(self, path, pattern):
        matches = list(re.finditer(pattern, self.texts[path], re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Expected one main-menu source shape: ' + pattern)
        return matches[0]

    def block(self, path, symbol, function=False):
        pattern = (r'^(?:static[ \t]+)?\w+[ \t*]+' + re.escape(symbol) + r'\([^;\n]*\)\s*\{' if function else
            r'\b' + re.escape(symbol) + r'(?:\[\])?\s*(?:=\s*)?\{')
        m = self.match(path, pattern)
        text = self.texts[path]; start = m.end()-1; depth = 0
        # Tokenize strings/comments as indivisible units before balancing C braces.
        for token in re.finditer(r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|/\*.*?\*/|//[^\n]*|[{}]', text[start:], re.S):
            if token[0] == '{': depth += 1
            elif token[0] == '}':
                depth -= 1
                if depth == 0:
                    end = start + token.end()
                    return text[start+1:end-1], self.ref(path, symbol, m.start(), end)
        raise ValueError('Unbalanced source block: ' + symbol)

    def number(self, expression):
        def walk(node):
            if isinstance(node, ast.Constant) and isinstance(node.value, int): return node.value
            if isinstance(node, ast.Name): return self.constant(node.id)
            if isinstance(node, ast.UnaryOp) and isinstance(node.op, ast.USub): return -walk(node.operand)
            if isinstance(node, ast.BinOp):
                left, right = walk(node.left), walk(node.right)
                if isinstance(node.op, ast.Add): return left + right
                if isinstance(node.op, ast.Sub): return left - right
                if isinstance(node.op, ast.LShift): return left << right
            raise ValueError('Unsupported source numeric expression: ' + expression)
        return walk(ast.parse(expression.strip(), mode='eval').body)

    def constant(self, symbol):
        if symbol not in self.constants:
            expression, path, offset = self.defines[symbol]
            self.constants[symbol] = dict(value=self.number(expression), source=self.ref(path, symbol, offset))
        return self.constants[symbol]['value']

    def enum(self, path, symbol=None, member=None):
        if symbol:
            body, ref = self.block(path, symbol)
        else:
            matches = [m for m in re.finditer(r'enum\s*\{([^}]+)\}', self.texts[path]) if re.search(r'\b'+re.escape(member)+r'\b', m[1])]
            if len(matches) != 1: raise ValueError('Missing source enum: ' + str(member))
            m = matches[0]; body = m[1]; ref = self.ref(path, member, m.start(), m.end())
        value = -1
        for item in body.split(','):
            item = item.strip()
            if not item: continue
            match = re.fullmatch(r'(\w+)(?:\s*=\s*([^,]+))?', item)
            if match is None: raise ValueError('Unsupported enum shape')
            value = self.number(match[2]) if match[2] else value+1
            self.constants[match[1]] = dict(value=value, source={**ref, 'symbol':match[1]})


def convert(lock, data):
    s = Source(lock, data)
    path = 'src/main_menu.c'
    s.enum(path, 'MainMenuType'); s.enum(path, 'MainMenuWindow')
    s.enum('include/text.h', member='FONT_NORMAL')
    s.enum('include/pokedex.h', member='FLAG_GET_CAUGHT')
    for symbol in ['PLAYER_NAME_LENGTH','MALE','FEMALE','EOS','DISPLAY_WIDTH','DISPLAY_HEIGHT','SE_SELECT',
        'SAVE_STATUS_EMPTY','SAVE_STATUS_OK','SAVE_STATUS_INVALID','SAVE_STATUS_NO_FLASH','SAVE_STATUS_ERROR',
        'FLAG_SYS_POKEDEX_GET','FLAG_BADGE01_GET','FLAG_BADGE08_GET']:
        s.constant(symbol)
    body, ref = s.block(path, 'sWindowTemplate')
    windows = []
    for m in re.finditer(r'\[(MAIN_MENU_WINDOW_\w+)\]\s*=\s*\{([^}]+)\}', body):
        fields = dict((k, s.number(v)) for k,v in re.findall(r'\.(\w+)\s*=\s*([^,\n]+)', m[2]))
        if set(fields) != {'bg','tilemapLeft','tilemapTop','width','height','paletteNum','baseBlock'}:
            raise ValueError('Main-menu window fields differ')
        fields.update(symbol=m[1], id=s.constant(m[1]), source={**ref,'symbol':m[1]})
        windows.append(fields)
    if len(windows) != 5 or '[MAIN_MENU_WINDOW_COUNT] = DUMMY_WIN_TEMPLATE' not in body:
        raise ValueError('Main-menu window inventory differs')
    bgbody,bgref=s.block(path,'sBgTemplate')
    backgrounds=[dict(symbol='sBgTemplate',source=bgref,**{k:s.number(v) for k,v in re.findall(r'\.(\w+)\s*=\s*([^,\n]+)',bgbody)})]
    colors=[]
    for symbol in ['sTextColor1','sTextColor2']:
        colorbody,colorref=s.block(path,symbol)
        values=[s.number(x) for x in colorbody.split(',')]
        if len(values)!=3 or not all(0<=x<16 for x in values): raise ValueError('Main-menu colors differ')
        colors.append(dict(symbol=symbol,background=values[0],foreground=values[1],shadow=values[2],source=colorref))
    printer,_=s.block('src/menu2.c','AddTextPrinterParameterized3',True)
    if not re.search(r'printer\.fgColor = color\[1\];\s*printer\.bgColor = color\[0\];\s*printer\.shadowColor = color\[2\];',printer):
        raise ValueError('Text printer palette channel order differs')
    palettes=[]
    for symbol in ['sBg_Pal','sTextbox_Pal']:
        m=s.match(path,r'\b'+symbol+r'\[\] = INCBIN_U16\("([^"]+)\.gbapal"\);')
        palpath=m[1]+'.pal'; lines=s.texts[palpath].splitlines()
        if lines[:3]!=['JASC-PAL','0100','16'] or len(lines)!=19: raise ValueError('Unexpected main-menu palette')
        rgb=[list(map(int,line.split())) for line in lines[3:]]
        if any(len(c)!=3 or any(v<0 or v>255 for v in c) for c in rgb): raise ValueError('Bad palette color')
        load=s.match(path,r'LoadPalette\('+symbol+r', BG_PLTT_ID\((\d+)\), sizeof\('+symbol+r'\)\);')
        five=[[v//8 for v in c] for c in rgb]
        palettes.append(dict(symbol=symbol,bank=int(load[1]),rgb555=[r|(g<<5)|(b<<10) for r,g,b in five],
            rgb8=[[(v<<3)|(v>>2) for v in c] for c in five],source=s.ref(palpath,symbol,0,len(s.texts[palpath]))))
    s.match('tools/gbagfx/gfx.c',r'#define DOWNCONVERT_BIT_DEPTH\(x\) \(\(x\) / 8\)')
    genders=s.match(path,r'if \(gSaveBlock2Ptr->playerGender == (\w+)\)\s*pal = RGB\(([^)]+)\);\s*else\s*pal = RGB\(([^)]+)\);\s*LoadPalette\(&pal, BG_PLTT_ID\((\d+)\) \+ (\d+), PLTT_SIZEOF\(1\)\);')
    gender_colors=[]
    for symbol,expr in [(genders[1],genders[2]),('FEMALE',genders[3])]:
        rgb=[s.number(x) for x in expr.split(',')]
        if len(rgb)!=3 or not all(0<=x<=31 for x in rgb): raise ValueError('Gender colors differ')
        gender_colors.append(dict(genderSymbol=symbol,gender=s.constant(symbol),bank=int(genders[4]),index=int(genders[5]),
            rgb555=rgb[0]|(rgb[1]<<5)|(rgb[2]<<10),rgb8=[(x<<3)|(x>>2) for x in rgb],
            source=s.ref(path,'Task_PrintMainMenuText',genders.start(),genders.end())))
    placements=[]; functions=['Task_PrintMainMenuText','PrintMessageOnWindow4','PrintPlayerName','PrintPlayTime','PrintDexCount','PrintBadgeCount']
    for fn in functions:
        function,fnref=s.block(path,fn,True)
        for index,m in enumerate(re.finditer(r'AddTextPrinterParameterized3\(\s*(\w+),\s*(\w+),\s*(-?\d+),\s*(-?\d+),\s*(\w+),\s*(-?\d+),\s*(\w+)\);',function)):
            placements.append(dict(id=fn+':'+str(index),windowSymbol=m[1],windowId=s.constant(m[1]),fontSymbol=m[2],fontId=s.constant(m[2]),
                x=int(m[3]),y=int(m[4]),colorSymbol=m[5],speed=int(m[6]),textSymbol=m[7],source=fnref))
    if len(placements)!=15: raise ValueError('Main-menu text calls differ')
    fills=sorted(set(int(v) for v in re.findall(r'FillWindowPixelBuffer\(\w+, PIXEL_FILL\((\d+)\)\);',s.texts[path])))
    if len(fills)!=1: raise ValueError('Unexpected window fill colors')
    textsymbols={p['textSymbol'] for p in placements if p['textSymbol'].startswith('gText')}
    textsymbols.update(['gText_SaveFileHasBeenDeleted','gText_SaveFileCorrupted','gText_1MSubCircuitBoardNotInstalled','gTextJPDummy_Hiki','gTextJPDummy_Ko'])
    texts={}
    for symbol in sorted(textsymbols):
        m=s.match('src/strings.c',r'^const u8 '+symbol+r'\[\] = _\("((?:\\.|[^"\\])*)"\);')
        texts[symbol]=dict(text=m[1],encoding='pret-escaped-text',source=s.ref('src/strings.c',symbol,m.start(),m.end()))
    save_text=s.texts['data/text/save.inc']
    for symbol in ['gText_WouldYouLikeToSaveTheGame','gText_AlreadySaveFile_WouldLikeToOverwrite','gText_DifferentGameFile']:
        m=s.match('data/text/save.inc',r'^'+symbol+r'::\s*\n((?:\s*\.string "[^"\n]*"\n)+)')
        texts[symbol]=dict(text=''.join(re.findall(r'\.string "([^"\n]*)"',m[1])),encoding='pret-escaped-text-with-eos',
            source=s.ref('data/text/save.inc',symbol,m.start(),m.end()))
    selection,selref=s.block(path,'MoveWindowByMenuTypeAndCursorPos',True)
    h=s.match(path,r'SetGpuReg\(REG_OFFSET_WIN0H, WIN_RANGE\((\d+), (\d+)\)\);\s*switch \(menuType\)')
    pairs=re.findall(r'win0vTop = (0x[0-9A-Fa-f]+) << 8;\s*win0vBot = (0x[0-9A-Fa-f]+);',selection)
    if len(pairs)!=4: raise ValueError('Selection bounds differ')
    adjust=s.match(path,r'SetGpuReg\(REG_OFFSET_WIN0V, \(win0vTop \+ \((\d+) << 8\)\) \| \(win0vBot - (\d+)\)\);')
    regions=[]
    for symbol,(top,bottom) in zip(['MAIN_MENU_WINDOW_NEWGAME_ONLY','MAIN_MENU_WINDOW_CONTINUE','MAIN_MENU_WINDOW_NEWGAME','MAIN_MENU_WINDOW_MYSTERYGIFT'],pairs):
        regions.append(dict(windowSymbol=symbol,left=int(h[1]),right=int(h[2]),top=int(top,16)+int(adjust[1]),bottom=int(bottom,16)-int(adjust[2]),source=selref))
    error,errref=s.block(path,'PrintMessageOnWindow4',True)
    axes={axis:(int(a),int(b)) for axis,a,b in re.findall(r'SetGpuReg\(REG_OFFSET_WIN0([HV]), WIN_RANGE\(\s*(\d+),\s*(\d+)\)\);',error)}
    regions.append(dict(windowSymbol='MAIN_MENU_WINDOW_ERROR',left=axes['H'][0],right=axes['H'][1],top=axes['V'][0],bottom=axes['V'][1],source=errref))
    statusbody,statusref=s.block(path,'Task_SetWin0BldRegsAndCheckSaveFile',True)
    statuses=[]
    for m in re.finditer(r'case (SAVE_STATUS_\w+):((?:(?!case SAVE_STATUS_).)*?)\s*break;',statusbody,re.S):
        status,code=m[1],m[2]; message=re.search(r'PrintSaveErrorStatus\(taskId, (\w+)\);',code)
        menus=re.findall(r'tMenuType = (MAIN_MENU_\w+);',code)
        ordinary=[x for x in menus if x!='MAIN_MENU_MYSTERYGIFT']
        if not ordinary or len(set(ordinary))!=1: raise ValueError('Unexpected save status branch')
        frame=re.search(r'\b(LoadUserFrameToBg|SetStdFrame0OnBg)\(0\);',code)
        statuses.append(dict(symbol=status,value=s.constant(status),menuTypeSymbol=ordinary[0],menuType=s.constant(ordinary[0]),
            errorTextSymbol=message[1] if message else None,frameFunction=frame[1],mysteryGiftEligible='MAIN_MENU_MYSTERYGIFT' in menus,
            source=statusref))
    if len(statuses)!=5: raise ValueError('Save-status branches differ')
    cursorbody,cursorref=s.block(path,'sMenuCursorYMax')
    cursor_max=[s.number(v) for v in cursorbody.split(',')]
    # Semantic guards are intentionally explicit: this is a bounded source adapter, not a C interpreter.
    guarded={
        'Task_SetWin0BldRegsNoSaveFileCheck':['if (gTasks[taskId].tMenuType == MAIN_MENU_NEWGAME)','gTasks[taskId].func = Task_ExecuteMainMenuSelection;'],
        'Task_ExecuteMainMenuSelection':['StartNewGameScene();','TryStartQuestLogPlayback(taskId);'],
        'Task_SaveErrorStatus_RunPrinterThenWaitButton':['!IsTextPrinterActive(MAIN_MENU_WINDOW_ERROR) && JOY_NEW(A_BUTTON)'],
        'HandleMenuInput':['JOY_NEW(B_BUTTON)','gTasks[taskId].tCursorPos > 0','gTasks[taskId].tCursorPos < sMenuCursorYMax[gTasks[taskId].tMenuType]'],
        'PrintPlayerName':['i < PLAYER_NAME_LENGTH','*ptr = EOS;'],
        'PrintPlayTime':['playTimeHours, STR_CONV_MODE_LEFT_ALIGN, 3','playTimeMinutes, STR_CONV_MODE_LEADING_ZEROS, 2'],
        'PrintDexCount':['FlagGet(FLAG_SYS_POKEDEX_GET) == TRUE','IsNationalPokedexEnabled()','GetNationalPokedexCount(FLAG_GET_CAUGHT)','GetKantoPokedexCount(FLAG_GET_CAUGHT)'],
        'PrintBadgeCount':['flagId < FLAG_BADGE01_GET + 8','STR_CONV_MODE_LEADING_ZEROS, 1'],
        'LoadUserFrameToBg':['optionsWindowFrameType)->tiles, 0x120, 0x1B1','BG_PLTT_ID(2)']}
    rules_refs=[]
    for fn,needles in guarded.items():
        block,ref=s.block(path,fn,True)
        if any(needle not in block for needle in needles): raise ValueError('Unsupported main-menu source semantics: '+fn)
        rules_refs.append(ref)
    for path2,fn,needles in [
        ('src/start_menu.c','SaveDialogCB_AskSaveHandleInput',['gSaveFileStatus != SAVE_STATUS_EMPTY && gSaveFileStatus != SAVE_STATUS_INVALID','!gDifferentSaveFile']),
        ('src/start_menu.c','SaveDialogCB_PrintAskOverwriteText',['gDifferentSaveFile == TRUE','gText_DifferentGameFile','gText_AlreadySaveFile_WouldLikeToOverwrite']),
        ('src/start_menu.c','SaveDialogCB_AskOverwritePrintYesNoMenu',['DisplayYesNoMenuDefaultYes();']),
        ('src/start_menu.c','SaveDialogCB_AskReplacePreviousFilePrintYesNoMenu',['DisplayYesNoMenuDefaultNo();']),
        ('src/new_game.c','NewGameInitData',['gDifferentSaveFile = TRUE;']),
        ('src/save.c','GetSaveValidStatus',['slot1Status == SAVE_STATUS_OK','slot2Status == SAVE_STATUS_OK','return SAVE_STATUS_ERROR;','return SAVE_STATUS_INVALID;'])]:
        block,ref=s.block(path2,fn,True)
        if any(needle not in block for needle in needles): raise ValueError('Unsupported main-menu save semantics: '+fn)
        rules_refs.append(ref)
    for needle in ['u8 playerName[PLAYER_NAME_LENGTH + 1];','u8 playerGender; // MALE, FEMALE','u16 playTimeHours;','u8 playTimeMinutes;']:
        if needle not in s.texts['include/global.h']: raise ValueError('Save profile contract differs')
    darken=set(int(x) for x in re.findall(r'SetGpuReg\(REG_OFFSET_BLDY, (\d+)\);',s.texts[path]) if int(x))
    if len(darken)!=1: raise ValueError('Unsupported darkening coefficients')
    frame,frameref=s.block(path,'MainMenu_DrawWindow',True)
    draw_calls=[]
    for m in re.finditer(r'FillBgTilemapBufferRect\((.*?)\);',frame,re.S):
        operands=[' '.join(a.split()) for a in m[1].split(',')]
        if len(operands)!=7: raise ValueError('Unexpected frame drawing call')
        draw_calls.append(dict(bg=operands[0],tileId=s.number(operands[1]),left=operands[2],top=operands[3],
            width=operands[4],height=operands[5],paletteNum=s.number(operands[6])))
    if len(draw_calls)!=8: raise ValueError('Frame draw inventory differs')
    rules=dict(logicalWidth=s.constant('DISPLAY_WIDTH'),logicalHeight=s.constant('DISPLAY_HEIGHT'),tileSize=8,
        windowFillColor=fills[0],darkeningCoefficient=next(iter(darken)),initialCursor=0,cursorYMax=cursor_max,
        navigationWraps=False,emptySaveEntersNewGameDirectly=True,newGameEntry='StartNewGameScene',continueEntry='TryStartQuestLogPlayback',
        cancelEntry='CB2_InitTitleScreen',errorAcknowledgementButton='A_BUTTON',newGameSelectionPromptsOverwrite=False,
        overwritePromptLocation='field-save',ordinaryOverwriteDefaultYes=True,differentFileOverwriteDefaultYes=False,
        playerNameLength=s.constant('PLAYER_NAME_LENGTH'),playerNameBufferLength=s.constant('PLAYER_NAME_LENGTH')+1,
        playerNameTerminator=s.constant('EOS'),playerGenderSymbols=['MALE','FEMALE'],playTimeHoursDigits=3,playTimeMinutesDigits=2,
        pokedexVisibleFlag='FLAG_SYS_POKEDEX_GET',pokedexCountKind='FLAG_GET_CAUGHT',badgeStartFlag='FLAG_BADGE01_GET',badgeCount=8,
        frameTileBase=draw_calls[0]['tileId'],framePaletteBank=draw_calls[0]['paletteNum'],frameDrawCalls=draw_calls,
        sourceRefs=[*rules_refs,frameref,cursorref])
    catalog=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,tree=TREE,lockSha256=LOCK_SHA256,
        fullSourceHash=source_hash(lock['files']),inputFiles=lock['files'],windows=windows,backgrounds=backgrounds,textColors=colors,
        palettes=palettes,genderColors=gender_colors,textPlacements=placements,texts=texts,selectionRegions=regions,
        saveStatuses=statuses,constants=s.constants,rules=rules,limitations=[
            'Authenticated source metadata only; application entry, save validation and native-frame verification remain separate gates.',
            'Title animation, Oak introduction, gender/name entry screens, intro artwork and quest-log playback are not implemented by this import.',
            'Mystery Gift source IDs and geometry are retained for provenance, but the network/event branch remains outside ordinary single-player scope.',
            'SAVE_STATUS_ERROR means a valid original backup slot exists alongside a damaged slot. Do not offer Continue for an unrecoverable remake save.',
            'Original GBA flash-sector status semantics are evidence, not a substitute for the remake save codec or runtime recovery policy.',
            'Palette RGB555 is authoritative. RGB8 uses explicit five-to-eight-bit replication consistent with the existing local graphics adapters.',
            'Player name length is seven encoded source characters plus EOS, not seven UTF-8 bytes or unrestricted Unicode input.',
            'No play time is inferred from steps. Fonts and user-frame pixels remain owned by the existing authenticated font/frame imports.'])
    output=canonical(catalog)
    manifest=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,tree=TREE,lockSha256=LOCK_SHA256,
        fullSourceHash=source_hash(lock['files']),inputCount=len(PATHS),upstreamScriptsExecuted=False,romDownloaded=False,
        outputs=[dict(path='main-menu.json',byteLength=len(output),sha256=sha(output))]))
    return {'main-menu.json':output,'manifest.json':manifest}


def import_content(raw, out, repository=None, lock_path=None):
    raw,out=safe(raw),safe(out)
    if raw.resolve().is_relative_to(out.resolve()) or out.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Main-menu raw and output directories overlap')
    if repository is not None:
        repo=pathlib.Path(repository).resolve()
        if any(p.resolve().is_relative_to(repo) or repo.is_relative_to(p.resolve()) for p in (raw,out)):
            raise ValueError('Main-menu materialization overlaps reference repository')
    lock=load_lock(lock_path)
    sources=read_sources(lock,raw,repository)
    outputs=convert(lock,sources)
    if sha(outputs['manifest.json']) != MANIFEST_SHA256:
        raise ValueError('Reviewed main-menu generated output differs')
    material={(raw,p):b for p,b in sources.items()} if repository is not None else {}
    if repository is not None: material[(raw,'manifest.json')]=raw_manifest(lock)
    material.update({(out,p):b for p,b in outputs.items()})
    targets=[(safe(root,p),safe(root,p+'.tmp'),b) for (root,p),b in material.items()]
    if any(p.exists() and not p.is_file() for target,stage,_ in targets for p in (target,stage)):
        raise ValueError('Main-menu output is not a regular file')
    if any(p.exists() and not p.is_dir() for target,_,_ in targets for p in target.parents):
        raise ValueError('Main-menu output parent is not a directory')
    for target,stage,data in targets:
        target.parent.mkdir(parents=True,exist_ok=True)
        stage.write_bytes(data); stage.replace(target)
    return dict(profile=PROFILE,inputCount=len(PATHS),contentSha256=sha(outputs['main-menu.json']),
        manifestSha256=sha(outputs['manifest.json']),rawManifestSha256=sha(raw_manifest(lock)),fullSourceHash=source_hash(lock['files']))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo',type=pathlib.Path)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/main-menu')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/main-menu')
    parser.add_argument('--lock',type=pathlib.Path,default=ROOT/'content/main-menu-source-lock.json')
    args=parser.parse_args()
    print(json.dumps(import_content(args.raw,args.out,args.reference_repo,args.lock),sort_keys=True))


if __name__=='__main__': main()
