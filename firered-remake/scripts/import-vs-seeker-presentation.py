#!/usr/bin/env python3
"""Bounded local source extraction. No upstream execution, ROM, network or PCM files."""
from item_catalog import item_definition as shared_item, inventory_item, item_json
import argparse,  io, json, os, re, struct, subprocess
from pathlib import Path
from PIL import Image
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE='content/generated/vs-seeker-presentation/'
PROFILE='firered-vs-seeker-presentation-v1'
def need(ok,message):
    if not ok: raise ValueError(message)
def encoded(value): return (json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=False, default=item_json)+'\n').encode()
def generate(reference,dependency):
    env={k:v for k,v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_NO_REPLACE_OBJECTS='1',GIT_OPTIONAL_LOCKS='0',GIT_CONFIG_NOSYSTEM='1',GIT_CONFIG_GLOBAL=os.devnull)
    def git(*args): return subprocess.check_output(['git','--no-replace-objects','--no-optional-locks','-C',str(reference),*args],env=env)
    inputs={}; cache={}; reused={}; files={}; assets=[]
    def source(path):
        if path not in cache:
            entry=git('ls-tree','-z',COMMIT,'--',path).decode();m=re.fullmatch(r'100644 blob ([0-9a-f]{40})\t'+re.escape(path)+'\0',entry)
            need(m is not None,'Regular source Git blob: '+path);blob=m[1];data=git('cat-file','blob',blob)
            inputs[path]=dict(path=path);cache[path]=data
        return cache[path]
    def text(path): return source(path).decode()
    def block(path,pattern):
        values=list(re.finditer(pattern,text(path),re.M|re.S));need(len(values)==1,'Unique source block '+pattern);return values[0]
    def ref(path,symbol,m): return dict(path=path,symbol=symbol,startLine=text(path).count('\n',0,m.start())+1,endLine=text(path).count('\n',0,m.end())+1)
    def reuse(path):
        data=(dependency/path).read_bytes();reused[path]=dict(path=path);return data
    palpath='graphics/object_events/palettes/player.pal'
    def palette(path):
        lines=text(path).splitlines();need(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Source palette shape')
        return [tuple(((int(c)>>3)<<3)|((int(c)>>3)>>2) for c in row.split()) for row in lines[3:]]
    def image(path):
        im=Image.open(io.BytesIO(source(path)));need(im.mode=='P' and max(im.tobytes())<16,'Original indexed source '+path);return im
    def save(id,im,pal,sourcepath,sourceframes,framewidth):
        rgba=bytes(v for index in im.tobytes() for v in (*pal[index],255 if index else 0));output=Image.frombytes('RGBA',im.size,rgba)
        stream=io.BytesIO();output.save(stream,format='PNG',compress_level=9);data=stream.getvalue();path=BASE+id+'.png';files[path]=data
        assets.append(dict(id=id,path=path,width=im.width,height=im.height,frameWidth=framewidth,frameHeight=im.height,frameCount=im.width//framewidth,sourcePath=sourcepath,sourceFrames=sourceframes,reused=False))
        return rgba
    def inherited(id,path,width,height,framewidth,rgba):
        data=reuse(path);im=Image.open(io.BytesIO(data));need(im.size==(width,height) and im.convert('RGBA').tobytes()==rgba,'Reused pixels equal source '+id)
        assets.append(dict(id=id,path=path,width=width,height=height,frameWidth=framewidth,frameHeight=height,frameCount=width//framewidth,sourcePath='graphics/object_events/pics/people/'+id.split('-')[0]+'_item.png',sourceFrames=list(range(5)),reused=True))
    animpath='src/data/object_events/object_event_anims.h';anim=text(animpath)
    normal=block(animpath,r'static const union AnimCmd sAnim_VSSeeker\[\] = \{(.*?)\};')[1]
    bike=block(animpath,r'static const union AnimCmd sAnim_VSSeekerBike\[\] = \{(.*?)\};')[1]
    need(re.findall(r'ANIMCMD_FRAME\((\d+), (\d+)\)',normal)==[('0','4'),('1','4'),('5','4'),('6','4'),('7','4'),('8','4'),('6','4'),('1','4'),('0','4')],'Normal scanner sequence')
    need(re.findall('ANIMCMD_FRAME\\((\\d+), (\\d+)\\)', bike) == [(str(x), '4') for x in [0, 1, 2, 3, 4, 5, 3, 2, 1, 0]], 'Bike scanner sequence')
    need(re.findall(r'ANIMCMD_LOOP\((\d+)\)',normal)==['0','6'] and re.findall(r'ANIMCMD_LOOP\((\d+)\)',bike)==['0','6'],'Seven iterations of source scanner loop')
    info=text('src/data/object_events/object_event_graphics_info.h');pics=text('src/data/object_events/object_event_pic_tables.h');graphics=text('src/data/object_events/object_event_graphics.h')
    colors=palette(palpath)
    for gender in ['red','green']:
        cap=gender.title();path='graphics/object_events/pics/people/'+gender+'_item.png';im=image(path);need(im.size==(144,32),'Nine original player item frames')
        expected='sPicTable_'+cap+'Item'
        for fragment in ['.images = '+expected, '.anims = sAnimTable_RedGreenVSSeeker']:
            need(fragment in block('src/data/object_events/object_event_graphics_info.h',r'const struct ObjectEventGraphicsInfo gObjectEventGraphicsInfo_'+cap+r'VSSeeker = \{(.*?)\};')[1],'Scanner original graphics binding')
        prefix=im.crop((0,0,80,32));rgba=bytes(v for index in prefix.tobytes() for v in (*colors[index],255 if index else 0))
        inherited(gender+'-field-move','content/generated/vermilion-gym-field-move-art/'+gender+'-field-move.png',80,32,16,rgba)
        save(gender+'-scanner-extra',im.crop((80,0,144,32)),colors,path,[5,6,7,8],16)
        path='graphics/object_events/pics/people/'+gender+'_vs_seeker_bike.png';im=image(path);need(im.size==(192,32),'Six original bicycle scanner frames')
        need('sPicTable_'+cap+'VSSeekerBike' in pics and 'gObjectEventPic_'+cap+'VSSeekerBike' in graphics,'Bicycle original graphics binding')
        save(gender+'-scanner-bike',im,colors,path,list(range(6)),32)
    emotepath='graphics/misc/emoticons.png';emote=image(emotepath);need(emote.size==(48,80),'Fifteen original emote frames')
    need('graphics/misc/emoticons.4bpp: %.4bpp: %.png\n\t$(GFX) $< $@ -mwidth 2 -mheight 2' in text('graphics_file_rules.mk'),'Emote source 16x16 metatile ordering')
    trainer=text('src/trainer_see.c');need('sprite->sData3 = -5;' in trainer and '.paletteNum = 0,' in trainer,'Original emote offset and player-palette slot')
    emote_strip=Image.new('P',(144,16))
    for n in range(9):emote_strip.paste(emote.crop((n%3*16,n//3*16,n%3*16+16,n//3*16+16)),(n*16,0))
    save('reaction-icons',emote_strip,colors,emotepath,list(range(9)),16)
    itempath='src/data/items.json';item=next(x for x in json.loads(text(itempath))['items'] if x['itemId']=='ITEM_VS_SEEKER')
    itemblock=block(itempath,r'^    \{\n      "english": "VS SEEKER",.*?^    \}');src=ref(itempath,'ITEM_VS_SEEKER',itemblock)
    itemid=int(block('include/constants/items.h',r'^#define ITEM_VS_SEEKER\s+(\d+)\s*$')[1]);need(itemid==362 and item['importance']==item['registrability']==1 and item['pocket']=='POCKET_KEY_ITEMS','Original key item identity')
    metadata=shared_item('ITEM_VS_SEEKER')
    definition=dict(item=metadata)
    definition.update(description=item['description_english'].replace('\\n','\n'), healHp=0, cureStatus=0, effectBytes=[], useSupport=10)
    icons=text('src/data/item_icon_table.h');itemgraphics=text('src/data/graphics/items.h')
    need('[ITEM_VS_SEEKER]      = {gItemIcon_VSSeeker, gItemIconPalette_VSSeeker}' in icons,'Item source icon binding')
    for fragment in ['gItemIcon_VSSeeker[] = INCBIN_U32("graphics/items/icons/vs_seeker.4bpp.lz")','gItemIconPalette_VSSeeker[] = INCBIN_U32("graphics/items/icon_palettes/vs_seeker.gbapal.lz")']: need(fragment in itemgraphics,'Item source pixels binding')
    iconpath='graphics/items/icons/vs_seeker.png';im=image(iconpath);need(im.size==(24,24),'Source item icon dimensions')
    iconrgba=save('item-vs-seeker',im,palette('graphics/items/icon_palettes/vs_seeker.pal'),iconpath,[0],24)
    texts=[]
    selections={'data/text/trainers.inc':['VSSeeker_Text_BatteryNotChargedNeedXSteps','VSSeeker_Text_NoTrainersWithinRange','VSSeeker_Text_TrainersNotReady'],'data/maps/VermilionCity_PokemonCenter_1F/text.inc':['VermilionCity_PokemonCenter_1F_Text_UrgeToBattleSomeoneAgain','VermilionCity_PokemonCenter_1F_Text_UseDeviceForRematches','VermilionCity_PokemonCenter_1F_Text_ExplainVSSeeker']}
    for path,names in selections.items():
        for name in names:
            m=block(path,r'^'+name+r'::\n((?:[ \t]+\.string [^\n]*\n)+)');parts=re.findall(r'\.string "(.*)"',m[1])
            encodedtext=''.join(parts);need(encodedtext.endswith('$'),'Original terminated text')
            texts.append(dict(symbol=name,encodedText=encodedtext,source=ref(path,name,m)))
    vs=text('src/vs_seeker.c');movement=text('src/event_object_movement.c');effect=text('src/field_effect.c');avatar=text('src/field_player_avatar.c')
    for fragment in ['gTasks[taskId].data[0] = 15;','gTasks[taskId].data[1] = 16;','data[1] = 11;','data[2] != 2','sMovementScript_Wait48','PlaySE(SE_CONTEST_MONS_TURN);','PlaySE(SE_PIN);']:need(fragment in vs,'Vs seeker source contract '+fragment)
    for path in ['src/sprite.c','src/task.c','src/script_movement.c','src/overworld.c','src/item_use.c','data/event_scripts.s','src/data/object_events/movement_action_func_tables.h','src/data/object_events/movement_type_func_tables.h']:source(path)
    sine=[int(float(x)*256) for x in re.findall(r'Q_8_8\((-?[0-9.]+)\)',block('src/trig.c',r'const s16 gSineTable\[\] =\s*\{(.*?)\};')[1])]
    need(len(sine)==320 and sine[64]==256 and sine[192]==-256,'Exact original Q8.8 sine table')
    jump=[-((3*sine[i])>>7) for i in range(10,128,10)]+[0];swim=[sine[i]>>7 for i in range(0,256,4)]
    pack=reuse('content/generated/synth-audio/firered.pack');need(len(pack) == 141541028 - 138975592,'Unchanged authenticated original synth pack')
    songs=text('include/constants/songs.h');table=re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)',text('sound/song_table.inc'),re.M);cfg=text('sound/songs/midi/midi.cfg');cues=[]
    baseline=json.loads(reuse('content/generated/synth-audio/manifest.json'))['cues']
    for symbol in ['SE_CONTEST_MONS_TURN','SE_PIN']:
        sid=int(re.search(r'^#define '+symbol+r'\s+(\d+)',songs,re.M)[1]);address,player,player2=struct.unpack_from('<IHH',pack,0x084A32CC+sid*8-0x08489968)
        need(table[sid]==(symbol.lower(),str(player),str(player2)),'Source sound table binding')
        midipath='sound/songs/midi/'+symbol.lower()+'.mid';midi=source(midipath);need(midi[:4]==b'MThd' and b'\xff\x06\x01[' not in midi,'Finite original source MIDI')
        options=re.search(r'^'+symbol.lower()+r'\.mid: (.+)',cfg,re.M)[1];priority=int(re.search(r'-P(\d+)',options)[1]);need(priority==pack[address-0x08489968+2],'Source sound priority')
        cue=dict(id=symbol,sourceCommit=COMMIT,sourceSymbol=symbol,sourcePath=midipath,sourcePlayer=player,priority=priority,sourceCounter=None,songAddress=address,species=0,looping=False,cryMode=0)
        old=next((c for c in baseline if c['id']==symbol),None)
        if old:need({**old,'cryMode':old.get('cryMode',0)}==cue,'Inherited source cue')
        cues.append(cue)
    need(cues[0]['id'] not in [c['id'] for c in baseline] and cues[1]['id'] in [c['id'] for c in baseline],'Exactly one new cue')
    schedule = dict(normalFrames=[0, 1, 5, 6] + [7, 8] * 7 + [6, 1, 0], bikeFrames=[0, 1, 2, 3] + [4, 5] * 7 + [3, 2, 1, 0], frameTicks=4, signalParentTaskTicks=[31, 42], responseWaitTicks=48, acceptedStepTicks=4, iconFrameTicks=[4, 4, 52], iconSourceFrames=dict(unfought=[0, 1, 2], accepted=[3, 4, 5], declined=[6, 7, 8]), jumpYOffsets=jump, swimXOffsets=swim, readyFrame=9, jumpMiddleWaitTicks=17, jumpRestTicks=81, readyJumpPeriodTicks=126, readySwimPeriodTicks=66, sourceQuestLogTransitionTicks=89)
    lock=dict(schemaVersion=1,profile=PROFILE,repository='pret/pokefirered',sourceCommit=COMMIT,sourceFiles=[inputs[p] for p in sorted(inputs)],reusedInputs=[reused[p] for p in sorted(reused)])
    lockbytes=encoded(lock)
    manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,assets=assets,itemDefinition=definition,texts=texts,audioCues=cues,timing=schedule,outputs=[dict(path=p) for p,b in sorted(files.items()) if p.startswith(BASE)],limitations=['Resource and pure presentation contract only; Application owns charge, RNG, replies and durable rematches.','Source-local animation ticks are separate from field-effect task/held-movement completion.','Icons outlive the 48-tick response wait. No ring or scanner beam exists in the original effect.','No native rendering, physical listening or bit-perfect certification.'])
    files[BASE+'manifest.json']=encoded(manifest)
    return files,manifest
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ['reference','dependency','output']:p.add_argument(name,type=Path)
    p.add_argument('--check',action='store_true');a=p.parse_args();files,manifest=generate(a.reference,a.dependency)
    for path,data in files.items():
        target=a.output/path
        if a.check:need(target.read_bytes()==data,'Exact regeneration '+path)
        else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(json.dumps(dict(passed=True,checked=a.check,files=len(files),assets=len(manifest['assets']),newTextures=6,sourceFiles=len(lock['sourceFiles']),manifestBytes=len(files[BASE+'manifest.json']),cues=manifest['audioCues'],jumpYOffsets=manifest['timing']['jumpYOffsets'],swimXOffsets=manifest['timing']['swimXOffsets']), default=item_json))
