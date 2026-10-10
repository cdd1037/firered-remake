#!/usr/bin/env python3
"""Pinned Red/Green bicycle sheets and bounded land-cycling source contracts, read only from local Git or locked cache.

No upstream programs, ROMs, downloads or executable game rules are used.
"""
import argparse
import binascii
import hashlib
import json
import pathlib
import re
import struct
import subprocess
import zlib

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-city-cycle-art-v1'
LOCK_SHA256 = 'a442f8d442959fe69c100fb5c4f3cae6317a887aa3dc8599a6f70ec8dbc753b4'
MANIFEST_SHA256 = 'e5258cb49405024cf6a656524b68e941d9371ed30b33568fa967f6aeb8fbf97f'
PNG_SIGNATURE = b'\x89PNG\r\n\x1a\n'
OBJECT_ROOT = 'src/data/object_events/'
PALETTE_PATH = 'graphics/object_events/palettes/player.pal'
PATHS = frozenset([
    'include/constants/global.h', 'include/constants/event_objects.h',
    'src/field_player_avatar.c', 'src/event_object_movement.c', 'tools/gbagfx/gfx.c',
    'graphics/object_events/pics/people/red_fish.png','graphics/object_events/pics/people/green_fish.png','include/constants/event_object_movement.h','src/strings.c','src/quest_log_player.c',
    'src/bike.c','src/item_use.c','src/overworld.c','src/metatile_behavior.c',
    'src/data/object_events/movement_action_func_tables.h','include/global.fieldmap.h','include/constants/metatile_behaviors.h',
    PALETTE_PATH, 'graphics/object_events/pics/people/red_bike.png',
    'graphics/object_events/pics/people/green_bike.png',
    *[OBJECT_ROOT + name + '.h' for name in ('object_event_graphics', 'object_event_graphics_info',
        'object_event_graphics_info_pointers', 'object_event_pic_tables', 'object_event_anims')]])


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    if relative is not None:
        path = pathlib.PurePosixPath(relative)
        require(bool(relative) and relative != '.' and not path.is_absolute() and '..' not in path.parts
                and '\\' not in relative and str(path) == relative, 'Unsafe player art path')
    target = root if relative is None else root / relative
    require(not any(p.is_symlink() for p in [target, *target.parents]), 'Player art symlink rejected')
    require(target.resolve().is_relative_to(root.resolve()), 'Player art path escapes root')
    return target


def read_bounded(path, length):
    require(path.stat().st_size == length, 'Player art length differs: ' + str(path))
    with path.open('rb') as stream:
        data = stream.read(length + 1)
    require(len(data) == length, 'Player art input changed while reading')
    return data


def source_set_hash(files):
    return sha(''.join(e['path'] + '\0' + e['sha256'] + '\n'
                       for e in sorted(files, key=lambda e: e['path'])).encode())


def load_lock(path=None):
    data = read_bounded(safe(path or ROOT / 'content/sources/city-services-cycle-art.lock.json'), 6282)
    require(sha(data) == LOCK_SHA256, 'Reviewed player art lock differs')
    lock = json.loads(data)
    require((lock['schemaVersion'], lock['repository'], lock['commit'], lock['tree'], lock['profile'],
             lock['gameEdition'], lock['romRevision']) ==
            (1, 'pret/pokefirered', COMMIT, TREE, PROFILE, 'FIRERED', 0), 'Player art source identity differs')
    require(len(lock['files']) == len(PATHS) and {e['path'] for e in lock['files']} == PATHS,
            'Player art dependency set differs')
    return lock


def raw_manifest(lock):
    return canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, tree=TREE,
        lockSha256=LOCK_SHA256, fullSourceHash=source_set_hash(lock['files']), inputCount=len(PATHS)))


def read_sources(lock, raw, repository):
    if repository is None:
        expected = raw_manifest(lock)
        require(read_bounded(safe(raw, 'manifest.json'), len(expected)) == expected, 'Raw player manifest differs')
    else:
        tree = subprocess.run(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}'],
                              check=True, capture_output=True, timeout=30).stdout.decode().strip()
        require(tree == TREE, 'Player art Git tree differs')
    sources = {}
    for entry in lock['files']:
        path = entry['path']
        if repository is None:
            data = read_bounded(safe(raw, path), entry['byteLength'])
        else:
            row = subprocess.run(['git', '-C', str(repository), 'ls-tree', COMMIT, '--', path],
                                 check=True, capture_output=True, timeout=30).stdout.decode()
            require(row == '100644 blob ' + entry['gitBlobSha1'] + '\t' + path + '\n', 'Source blob binding differs')
            data = subprocess.run(['git', '-C', str(repository), 'show', COMMIT + ':' + path],
                                  check=True, capture_output=True, timeout=30).stdout
        blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
        require((len(data), sha(data), blob) == (entry['byteLength'], entry['sha256'], entry['gitBlobSha1']),
                'Player art source bytes differ: ' + path)
        sources[path] = data
    return sources


def indexed_png(data, width=288):
    """Strict 288x32 source indices; bounded decompression before pixel allocation."""
    require(width in (288,384), 'Unexpected source sheet width')
    require(data.startswith(PNG_SIGNATURE) and len(data) < 4096, 'Unexpected player PNG signature/length')
    position, chunks = 8, []
    while position < len(data):
        require(position + 12 <= len(data), 'Truncated source PNG')
        length = struct.unpack_from('>I', data, position)[0]
        end = position + length + 12
        require(end <= len(data), 'Truncated source PNG payload')
        tag, payload = data[position + 4:position + 8], data[position + 8:end - 4]
        require(binascii.crc32(tag + payload) & 0xffffffff == struct.unpack_from('>I', data, end - 4)[0],
                'Source PNG CRC differs')
        chunks.append((tag, payload))
        position = end
    require([c[0] for c in chunks] == [b'IHDR', b'PLTE', b'tRNS', b'IDAT', b'IEND'], 'Source PNG chunks differ')
    require(chunks[0][1] == struct.pack('>IIBBBBB', width, 32, 4, 3, 0, 0, 0), 'Source PNG geometry differs')
    require(len(chunks[1][1]) == 48 and chunks[2][1] == b'\0' and chunks[4][1] == b'', 'Source PNG palette differs')
    decoder = zlib.decompressobj()
    scanlines = decoder.decompress(chunks[3][1], (width//2+1)*32+1)
    require(len(scanlines) == (width//2+1)*32 and decoder.eof and not decoder.unused_data and not decoder.unconsumed_tail,
            'Source PNG pixel stream differs')
    previous, indices = [0] * (width//2), bytearray()
    stride=width//2+1
    for y in range(32):
        method, row = scanlines[y * stride], []
        require(method in range(5), 'Source PNG filter unsupported')
        for x, value in enumerate(scanlines[y * stride + 1:(y + 1) * stride]):
            left, up, upper_left = row[x - 1] if x else 0, previous[x], previous[x - 1] if x else 0
            if method == 0:
                prediction = 0
            elif method == 1:
                prediction = left
            elif method == 2:
                prediction = up
            elif method == 3:
                prediction = (left + up) // 2
            else:
                estimate = left + up - upper_left
                distances = abs(estimate - left), abs(estimate - up), abs(estimate - upper_left)
                prediction = (left, up, upper_left)[distances.index(min(distances))]
            decoded = (value + prediction) & 255
            row.append(decoded)
            indices.extend((decoded >> 4, decoded & 15))
        previous = row
    return bytes(indices)


def palette(data):
    lines = data.decode('ascii').splitlines()
    require(lines[:3] == ['JASC-PAL', '0100', '16'] and len(lines) == 19, 'Player palette profile differs')
    colors, words = [], []
    for line in lines[3:]:
        require(re.fullmatch(r'\d+ \d+ \d+', line) is not None, 'Player palette RGB differs')
        rgb = [int(c) for c in line.split()]
        require(all(c <= 255 for c in rgb), 'Player palette channel exceeds 255')
        five = [c // 8 for c in rgb]
        words.append(five[0] | five[1] << 5 | five[2] << 10)
        colors.append(tuple(c * 255 // 31 for c in five))
    return colors, words


def png_chunk(tag, data):
    return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', binascii.crc32(tag + data) & 0xffffffff)


def rgba_png(rgba, width=288):
    require(len(rgba) == width * 32 * 4, 'Player RGBA length differs')
    scanlines = b''.join(b'\0' + rgba[y * width*4:(y + 1) * width*4] for y in range(32))
    count = len(scanlines)
    stream = b'\x78\x01\x01' + struct.pack('<HH', count, count ^ 0xffff) + scanlines
    stream += struct.pack('>I', zlib.adler32(scanlines) & 0xffffffff)
    return PNG_SIGNATURE + png_chunk(b'IHDR', struct.pack('>IIBBBBB', width, 32, 8, 6, 0, 0, 0)) + png_chunk(b'IDAT', stream) + png_chunk(b'IEND', b'')


def block(text, symbol):
    matches = re.findall(r'\b' + re.escape(symbol) + r'(?:\[[^\n]*?\])?\s*=\s*\{(.*?)\n\};', text, re.S)
    require(len(matches) == 1, 'Missing/ambiguous player art declaration: ' + symbol)
    return matches[0]


def convert(lock, sources):
    text = {p: b.decode() for p, b in sources.items() if not p.endswith('.png')}
    def ref(path, symbol):
        return dict(kind='pret', repository='pret/pokefirered', commit=COMMIT, path=path,
                    symbol=symbol, sha256=sha(sources[path]))
    def has(path, pattern):
        require(re.search(pattern, text[path], re.M) is not None, 'Player source binding differs: ' + pattern)
    for name, value in [('MALE', 0), ('FEMALE', 1), ('GENDER_COUNT', 2)]:
        has('include/constants/global.h', r'^#define\s+' + name + r'\s+' + str(value) + r'\s*$')
    has('src/field_player_avatar.c', r'\[PLAYER_AVATAR_GFX_BIKE\]\s*=\s*\{OBJ_EVENT_GFX_RED_BIKE,\s*OBJ_EVENT_GFX_GREEN_BIKE\}')
    has(OBJECT_ROOT + 'object_event_graphics.h', r'gObjectEventPal_Player\[\] = INCBIN_U16\("graphics/object_events/palettes/player.gbapal"\)')
    has('tools/gbagfx/gfx.c', r'#define DOWNCONVERT_BIT_DEPTH\(x\) \(\(x\) / 8\)')
    has('tools/gbagfx/gfx.c', r'#define UPCONVERT_BIT_DEPTH\(x\) \(\(\(x\) \* 255\) / 31\)')
    has('src/event_object_movement.c', r'sprite->centerToCornerVecX = -\(graphicsInfo->width >> 1\);\s*'
        r'sprite->centerToCornerVecY = -\(graphicsInfo->height >> 1\);\s*'
        r'sprite->x \+= 8;\s*sprite->y \+= 16 \+ sprite->centerToCornerVecY;')
    animations = text[OBJECT_ROOT + 'object_event_anims.h']
    animation_table = block(animations, 'sAnimTable_Standard')
    directions = {}
    for direction, stand, walk in [('south', 0, [3, 0, 4, 0]), ('north', 1, [5, 1, 6, 1]),
                                    ('west', 2, [7, 2, 8, 2]), ('east', 2, [7, 2, 8, 2])]:
        flip = direction == 'east'
        for prefix, enum, frames, ticks in [('Face', 'FACE', [stand], 16), ('GoFast', 'GO_FAST', walk, 4)]:
            symbol = 'sAnim_' + prefix + direction.title()
            require(re.search(r'\[ANIM_STD_' + enum + '_' + direction.upper() + r'\]\s*=\s*' + symbol + ',', animation_table),
                    'Player animation table binding differs')
            commands = block(animations, symbol)
            expected = ''.join(f'ANIMCMD_FRAME({frame},{ticks}' + (',.hFlip=TRUE' if flip else '') + '),' for frame in frames) + 'ANIMCMD_JUMP(0),'
            require(re.sub(r'\s+', '', commands) == expected, 'Player standing/walking animation differs')
        directions[direction] = dict(stand=stand, walk=walk, flipH=flip)
    colors, words = palette(sources[PALETTE_PATH])
    outputs, assets = {}, []
    for gender, name, gfx_id in [(0, 'Red', 1), (1, 'Green', 8)]:
        stem, symbol = name.lower(), name + 'Bike'
        graphics_id = 'OBJ_EVENT_GFX_' + name.upper() + '_BIKE'
        image_path = f'graphics/object_events/pics/people/{stem}_bike.png'
        has('include/constants/event_objects.h', r'^#define ' + graphics_id + r' ' + str(gfx_id) + r'$')
        has(OBJECT_ROOT + 'object_event_graphics_info_pointers.h', r'\[' + graphics_id + r'\]\s*=\s*&gObjectEventGraphicsInfo_' + symbol + ',')
        info = block(text[OBJECT_ROOT + 'object_event_graphics_info.h'], 'gObjectEventGraphicsInfo_' + symbol)
        fields = dict(re.findall(r'\.(\w+)\s*=\s*([^,\n]+)', info))
        palette_tag = 'OBJ_EVENT_PAL_TAG_PLAYER_' + name.upper()
        expected = dict(width='32', height='32', paletteTag=palette_tag, paletteSlot='PALSLOT_PLAYER',
                        anims='sAnimTable_Standard', images='sPicTable_' + symbol, inanimate='FALSE')
        require(all(fields.get(k) == v for k, v in expected.items()), 'Player graphics info differs')
        has('src/event_object_movement.c', r'\{gObjectEventPal_Player,\s*' + palette_tag + r'\}')
        has(OBJECT_ROOT + 'object_event_graphics.h', r'gObjectEventPic_' + symbol + r'\[\] = INCBIN_U16\("' + image_path.replace('.png', '.4bpp') + r'"\)')
        table = block(text[OBJECT_ROOT + 'object_event_pic_tables.h'], 'sPicTable_' + symbol)
        frames = re.findall(r'overworld_frame\((\w+), (\d+), (\d+), (\d+)\)', table)
        require(frames[:9] == [('gObjectEventPic_' + symbol, '4', '4', str(i)) for i in range(9)]
                and len(frames) == 9, 'Player normal frame table differs')
        indices = indexed_png(sources[image_path])
        rgba = bytes(c for i in indices for c in (*colors[i], 0 if i == 0 else 255))
        filename = 'cycle-' + ('male' if gender == 0 else 'female') + '.png'
        outputs[filename] = rgba_png(rgba)
        assets.append(dict(gender=gender, genderSymbol='MALE' if gender == 0 else 'FEMALE', graphicsId=graphics_id,
            sourceGraphicsId=gfx_id, texturePath=filename, byteLength=len(outputs[filename]), sha256=sha(outputs[filename]),
            rgbaSha256=sha(rgba), indexSha256=sha(indices), width=288, height=32, frameWidth=32, frameHeight=32, frameCount=9,
            frameLayout='horizontal', sourceFrameIndices=list(range(9)), sourceImagePath=image_path,
            sourcePalettePath=PALETTE_PATH, paletteTag=palette_tag, paletteSlot='PALSLOT_PLAYER', paletteRgb555=words,
            transparentIndex=0, directions=directions, tileCenterAnchor=dict(x=16, y=24),
            drawOffsetFromTileOrigin=dict(x=-8, y=-16), walkFrameDurationSourceFrames=4, sourceTimingHz=60,
            sourceRefs=[ref(image_path, 'gObjectEventPic_' + symbol), ref(PALETTE_PATH, 'gObjectEventPal_Player'),
                ref(OBJECT_ROOT + 'object_event_graphics.h', 'gObjectEventPic_' + symbol),
                ref(OBJECT_ROOT + 'object_event_graphics_info.h', 'gObjectEventGraphicsInfo_' + symbol),
                ref(OBJECT_ROOT + 'object_event_graphics_info_pointers.h', graphics_id),
                ref(OBJECT_ROOT + 'object_event_pic_tables.h', 'sPicTable_' + symbol),
                ref(OBJECT_ROOT + 'object_event_anims.h', 'sAnimTable_Standard'),
                ref('src/event_object_movement.c', 'sObjectEventSpritePalettes'),
                ref('src/field_player_avatar.c', 'sPlayerAvatarGfxIds'),
                ref('include/constants/global.h', 'MALE' if gender == 0 else 'FEMALE'),
                ref('include/constants/event_objects.h', graphics_id), ref('tools/gbagfx/gfx.c', 'UPCONVERT_BIT_DEPTH')]))
    outputs['city-cycle-art.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        fullSourceHash=source_set_hash(lock['files']), paletteExpansion='floor(rgb5 * 255 / 31)', assets=assets,
        limitations=['Original Red/Green Bicycle art and normal land-cycling source contracts only.',
                    'No executable cycling, surfing, fishing, field-action or native rendering acceptance.']))
    outputs['cycle-rules.json'] = canonical(cycle_rules(sources, ref))
    outputs.update(fishing_art(sources, ref))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, tree=TREE,
        lockSha256=LOCK_SHA256, fullSourceHash=source_set_hash(lock['files']), inputCount=len(PATHS),
        upstreamScriptsExecuted=False, romDownloaded=False,
        outputs=[dict(path=p, byteLength=len(b), sha256=sha(b)) for p, b in sorted(outputs.items())]))
    return outputs


def cycle_rules(sources, ref):
    text={p:b.decode() for p,b in sources.items() if not p.endswith('.png')}
    def function(path,symbol):
        matches=list(re.finditer(r'^(?:static )?[\w *]+\b'+re.escape(symbol)+r'\([^;\n]*\)\s*\{',text[path],re.M))
        require(len(matches)==1,'Missing cycle function: '+symbol)
        start=matches[0].start();cursor=matches[0].end();depth=1
        while depth:
            require(cursor<len(text[path]),'Unclosed cycle function')
            char=text[path][cursor];depth+=(char=='{')-(char=='}');cursor+=1
        return dict(symbol=symbol,sourceText=text[path][start:cursor],sourceRef=ref(path,symbol))
    functions=[]
    inventory={
        'src/item_use.c':['FieldUseFunc_Bike','ItemUseOnFieldCB_Bicycle'],
        'src/bike.c':['IsBikingDisallowedByPlayer','MetatileBehaviorForbidsBiking','GetOnOffBike','BikeInputHandler_Normal','BikeInputHandler_Turning','BikeTransition_MoveDirection','GetBikeCollisionAt','BikeTransition_Downhill','BikeTransition_Uphill'],
        'src/overworld.c':['Overworld_IsBikingAllowed','StoreInitialPlayerAvatarState','GetAdjustedInitialTransitionFlags'],
        'src/field_player_avatar.c':['PlayerRideWaterCurrent','PlayerAvatarTransition_Bike'],
        'src/metatile_behavior.c':['MetatileBehavior_IsRunningDisallowed','MetatileBehavior_IsFortreeBridge','MetatileBehavior_IsIsolatedVerticalRail','MetatileBehavior_IsIsolatedHorizontalRail','MetatileBehavior_IsVerticalRail','MetatileBehavior_IsHorizontalRail'],
        'src/event_object_movement.c':['InitMovementNormal','NpcTakeStep','MovementAction_RideWaterCurrentDown_Step0','Step2','Step3']}
    for path,names in inventory.items():
        functions.extend(function(path,name) for name in names)
    steps=[int(n) for n in re.findall(r'\bStep(\d+)',block(text['src/event_object_movement.c'],'sSpeedFast2StepFuncs'))]
    require(steps==[2,3,3,2,3,3],'Original cycling pixel steps differ')
    require('[MOVE_SPEED_FAST_2] = GetMoveDirectionFastAnimNum' in text[OBJECT_ROOT+'movement_action_func_tables.h'],'Cycling animation speed binding differs')
    require(re.search(r'^#define MB_RUNNING_DISALLOWED 0x0A$',text['include/constants/metatile_behaviors.h'],re.M),'Biking forbidden tile differs')
    for fn in functions:
        if fn['symbol'] in ['MetatileBehavior_IsFortreeBridge','MetatileBehavior_IsIsolatedVerticalRail','MetatileBehavior_IsIsolatedHorizontalRail','MetatileBehavior_IsVerticalRail','MetatileBehavior_IsHorizontalRail']:
            require('{ return FALSE; }' in fn['sourceText'],'FireRed unused terrain predicate differs')
    return dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,runtimeIntegrated=False,
        allowedMapProperty='allow_cycling',forbiddenCurrentPlayerStates=['PLAYER_AVATAR_FLAG_UNDERWATER','PLAYER_AVATAR_FLAG_SURFING'],
        forbiddenCurrentOrDestinationBehavior=dict(symbol='MB_RUNNING_DISALLOWED',value=10,sourceRef=ref('include/constants/metatile_behaviors.h','MB_RUNNING_DISALLOWED')),
        blocksItemUseWhenFlagSet='FLAG_SYS_ON_CYCLING_ROAD',unusedAlwaysFalsePredicates=['MetatileBehavior_IsFortreeBridge','MetatileBehavior_IsIsolatedVerticalRail','MetatileBehavior_IsIsolatedHorizontalRail','MetatileBehavior_IsVerticalRail','MetatileBehavior_IsHorizontalRail'],
        toggle=dict(onFootFlag='PLAYER_AVATAR_FLAG_ON_FOOT',requestedBikeFlags=['PLAYER_AVATAR_FLAG_MACH_BIKE','PLAYER_AVATAR_FLAG_ACRO_BIKE'],item='ITEM_BICYCLE',music='MUS_CYCLING',mountSound='SE_BIKE_BELL',restoreMapMusicOnDismount=True),
        ordinaryMovement=dict(sourceAction='PlayerRideWaterCurrent',sourceSpeed='MOVE_SPEED_FAST_2',pixelsPerSourceFrame=steps,pixelsPerTile=sum(steps),sourceFramesPerTile=len(steps),sourceTimingHz=60,
            animationBinding='GetMoveDirectionFastAnimNum',animationFrameDuration=4,sourceRef=ref('src/event_object_movement.c','sSpeedFast2StepFuncs'),
            facingChangeWhileStanding='Turn in place before moving; BikeInputHandler_Turning returns BIKE_TRANS_TURNING.',
            collision='Use the original field collision check; jumping permitted ledges uses PlayerJumpLedge. No water or blocked-object bypass.'),
        mapTransition=dict(allowCyclingFalse='PLAYER_AVATAR_FLAG_ON_FOOT',allowCyclingTrue='Preserve prior MACH_BIKE or ACRO_BIKE transition flag.',
            sourceRef=ref('src/overworld.c','GetAdjustedInitialTransitionFlags'),
            priorBranches=['Cruise mode on non-indoor map forces ON_FOOT','Underwater map selects UNDERWATER','Seafoam surfable tiles force ON_FOOT','Other surfable tiles select SURFING'],
            boundedScope='No surfing, underwater, sea routes, Cycling Road slope or newly reachable maps are admitted by this content profile.'),
        functions=functions,
        limitations=['Art and source contracts only; no executable field rules, save migration, mounted input, audio or native rendering acceptance.',
            'MACH/ACRO avatar flags describe the one FireRed Bicycle; separate collectible bicycles and Ruby/Sapphire rail/bridge mechanics are not admitted.',
            'Four-source-frame sprite cadence and six-source-frame tile motion are different clocks. Animation alternation/pause needs original field semantics.'])

def fishing_art(sources, ref):
    text={p:b.decode() for p,b in sources.items() if not p.endswith('.png')}
    animations=[];animpath=OBJECT_ROOT+'object_event_anims.h';table=block(text[animpath],'sAnimTable_RedGreenFish')
    for action,prefix,enum,start in [('cast','TakeOutRod','TAKE_OUT_ROD',0),('retract','PutAwayRod','PUT_AWAY_ROD',4),('hook','HookedPokemon','HOOKED_POKEMON',8)]:
        for n,direction in enumerate(['south','north','west','east']):
            symbol='sAnim_'+prefix+direction.title();enumname='ANIM_'+enum+'_'+direction.upper()
            require(re.search(r'\['+enumname+r'\]\s*=\s*'+symbol+r',',table),'Fishing animation table differs')
            require(re.search(r'^#define '+enumname+r'\s+'+str(start+n)+r'$',text['include/constants/event_object_movement.h'],re.M),'Fishing animation ID differs')
            body=block(text[animpath],symbol);commands=[]
            for line in body.splitlines():
                if not line.strip():continue
                frame=re.fullmatch(r'\s*ANIMCMD_FRAME\((\d+), (\d+)(, \.hFlip = TRUE)?\),\s*',line)
                if frame:
                    value,duration=int(frame[1]),int(frame[2]);ox=(-8 if direction=='west' else 8) if value in (1,2,3) else 0;oy=-8 if value==5 else 8 if value in (10,11) else 0
                    commands.append(dict(opcode='frame',value=value,duration=duration,flipH=bool(frame[3]),offsetX=ox,offsetY=oy))
                else:
                    other=re.fullmatch(r'\s*ANIMCMD_(LOOP|JUMP)\((\d+)\),\s*',line)
                    require(other is not None or line.strip()=='ANIMCMD_END,','Unknown fishing animation command')
                    commands.append(dict(opcode=other[1].lower() if other else 'end',value=int(other[2]) if other else 0,duration=0,flipH=False,offsetX=0,offsetY=0))
            animations.append(dict(id=action+'.'+direction,sourceAnimationId=start+n,direction=direction,commands=commands,sourceText=body,sourceRef=ref(animpath,symbol)))
    colors,_=palette(sources[PALETTE_PATH]);assets=[];outputs={}
    for gender,name,gfx_id in [(0,'Red',4),(1,'Green',11)]:
        symbol=name+'Fish';gfx='OBJ_EVENT_GFX_'+name.upper()+'_FISH';image='graphics/object_events/pics/people/'+name.lower()+'_fish.png'
        require(re.search(r'^#define '+gfx+' '+str(gfx_id)+'$',text['include/constants/event_objects.h'],re.M),'Fishing graphics ID differs')
        info=block(text[OBJECT_ROOT+'object_event_graphics_info.h'],'gObjectEventGraphicsInfo_'+symbol)
        fields=dict(re.findall(r'\.(\w+)\s*=\s*([^,\n]+)',info))
        require(all(fields.get(k)==v for k,v in dict(width='32',height='32',anims='sAnimTable_RedGreenFish',images='sPicTable_'+symbol,paletteTag='OBJ_EVENT_PAL_TAG_PLAYER_'+name.upper(),paletteSlot='PALSLOT_PLAYER').items()),'Fishing graphics info differs')
        frames=re.findall(r'overworld_frame\((\w+), (\d+), (\d+), (\d+)\)',block(text[OBJECT_ROOT+'object_event_pic_tables.h'],'sPicTable_'+symbol))
        require(frames==[('gObjectEventPic_'+symbol,'4','4',str(i)) for i in range(12)],'Fishing frame table differs')
        require(re.search(r'\['+gfx+r'\]\s*=\s*&gObjectEventGraphicsInfo_'+symbol+r',',text[OBJECT_ROOT+'object_event_graphics_info_pointers.h']),'Fishing graphics pointer differs')
        require('gObjectEventPic_'+symbol+'[] = INCBIN_U16("'+image.replace('.png','.4bpp')+'")' in text[OBJECT_ROOT+'object_event_graphics.h'],'Fishing image binding differs')
        indices=indexed_png(sources[image],384);rgba=bytes(c for i in indices for c in (*colors[i],0 if i==0 else 255));filename='fish-'+('male' if gender==0 else 'female')+'.png';png=rgba_png(rgba,384);outputs[filename]=png
        assets.append(dict(gender=gender,graphicsId=gfx,sourceGraphicsId=gfx_id,texturePath=filename,sha256=sha(png),rgbaSha256=sha(rgba),width=384,height=32,frameWidth=32,frameHeight=32,frameCount=12,
            drawOffsetFromTileOrigin=dict(x=-8,y=-16),sourceRefs=[ref(image,'gObjectEventPic_'+symbol),ref(PALETTE_PATH,'gObjectEventPal_Player'),ref(OBJECT_ROOT+'object_event_graphics_info.h','gObjectEventGraphicsInfo_'+symbol),ref(OBJECT_ROOT+'object_event_pic_tables.h','sPicTable_'+symbol)]))
    texts={}
    for symbol in ['gText_PokemonOnHook','gText_NotEvenANibble','gText_ItGotAway']:
        matches=re.findall(r'^const u8 '+symbol+r'\[\] = _\("(.*?)"\);$',text['src/strings.c'],re.M);require(len(matches)==1,'Fishing text identity differs');texts[symbol]=matches[0]
    outputs['fishing-art.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,assets=assets,animations=animations,texts=texts,
        sourceTimingHz=60,alignmentSource=ref('src/field_player_avatar.c','AlignFishingAnimationFrames'),
        limitations=['Original cast/retract/hook frame commands and original per-frame offsets only.',
            'ANIMCMD_LOOP and JUMP remain explicit source commands; no animation player or fishing rule implementation is certified.',
            'No surf blob, offshore access, fishing encounter or new map permission is introduced.']))
    return outputs

def import_content(raw, output, repository=None):
    raw, output = safe(raw), safe(output)
    require(not (raw.resolve().is_relative_to(output.resolve()) or output.resolve().is_relative_to(raw.resolve())),
            'Player raw/output paths overlap')
    if repository is not None:
        repo = pathlib.Path(repository).resolve()
        require(not any(p.resolve().is_relative_to(repo) or repo.is_relative_to(p.resolve()) for p in (raw, output)),
                'Player materialization overlaps reference repository')
    for kind in ['raw','generated']:
        for protected in (ROOT/'content'/kind).iterdir():
            if protected.name=='city-cycle-art': continue
            require(not any(p.resolve().is_relative_to(protected.resolve()) or protected.resolve().is_relative_to(p.resolve()) for p in (raw,output)), 'Existing profile overlap')
    lock = load_lock()
    sources = read_sources(lock, raw, repository)
    outputs = convert(lock, sources)
    require(sha(outputs['manifest.json']) == MANIFEST_SHA256, 'Reviewed cycle art outputs differ: '+sha(outputs['manifest.json']))
    material = {(raw, p): b for p, b in sources.items()} if repository is not None else {}
    if repository is not None:
        material[(raw, 'manifest.json')] = raw_manifest(lock)
    material.update({(output, p): b for p, b in outputs.items()})
    targets = [(safe(root, p), safe(root, p + '.tmp'), b) for (root, p), b in material.items()]
    require(not any(p.exists() and not p.is_file() for target, stage, _ in targets for p in (target, stage)),
            'Player art target is not a regular file')
    require(not any(p.exists() and not p.is_dir() for target, _, _ in targets for p in target.parents),
            'Player art ancestor is not a directory')
    require(not any(stage.exists() for _, stage, _ in targets), 'Player art staging path already exists')
    for target, stage, data in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        with stage.open('xb') as stream:
            stream.write(data)
        stage.replace(target)
    return dict(profile=PROFILE, sourceCount=len(sources), outputCount=len(outputs),
                artSha256=sha(outputs['city-cycle-art.json']), manifestSha256=sha(outputs['manifest.json']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path)
    parser.add_argument('--offline', action='store_true', help='Use only the authenticated existing raw cache')
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/city-cycle-art')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/city-cycle-art')
    args = parser.parse_args()
    require(not (args.offline and args.reference_repo), 'Choose either local Git or offline cache')
    repository = args.reference_repo
    if repository is None and not args.offline:
        repository = pathlib.Path((ROOT / '.tools/reference-root').read_text().strip())
    print(json.dumps(import_content(args.raw, args.output, repository), sort_keys=True))


if __name__ == '__main__':
    main()
