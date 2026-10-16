"""Compile Trainer Tower actor and encounter-music selection from native tables."""
import re
from map_runtime import require


def compile_trainer_tower(src, worlds, scripts, constants):
    tower = src.text('src/trainer_tower.c')
    def table(name):
        match = re.search(r'\b' + name + r'\[[^;]*?= \{(.*?)\n\};', tower, re.S)
        require(match, 'Missing tower table: ' + name)
        return match[1]
    singles = [dict(graphicsId=g, facilityClass=c, gender=s) for g,c,s in re.findall(
        r'\{(OBJ_EVENT_GFX_\w+),\s*(FACILITY_CLASS_\w+),\s*(\w+)\}', table('sSingleBattleTrainerInfo'))]
    doubles = [dict(graphicsId1=a,graphicsId2=b,facilityClass=c,gender1=d,gender2=e) for a,b,c,d,e in re.findall(
        r'\{(OBJ_EVENT_GFX_\w+),\s*(OBJ_EVENT_GFX_\w+),\s*(FACILITY_CLASS_\w+),\s*(\w+),\s*(\w+)\}', table('sDoubleBattleTrainerInfo'))]
    values=constants['values']
    lookup=[dict(facilityClass=c,comparisonValue=values[c]['value'],musicId=m,musicIndex=values[m]['value'])
            for c,m in re.findall(r'\{(FACILITY_CLASS_\w+),\s*(TRAINER_ENCOUNTER_MUSIC_\w+)\}',table('sTrainerEncounterMusicLUT'))]
    songs=[dict(musicId=m,musicIndex=values[m]['value'],song=s,sourceSongId=values[s]['value'])
           for m,s in re.findall(r'\[(TRAINER_ENCOUNTER_MUSIC_\w+)\]\s*=\s*(MUS_\w+)',table('sTrainerTowerEncounterMusic'))]
    require(singles and doubles and lookup and songs, 'Empty tower actor/music tables')
    graphics_defaults=set(re.findall(r'trainerGfx[12]\s*=\s*(OBJ_EVENT_GFX_\w+);',tower))
    require(len(graphics_defaults)==1,'Tower actor defaults disagree')
    music_function=re.search(r'static void PlayTrainerTowerEncounterMusic\(void\)\n\{(.*?)\n\}',tower,re.S)
    require(music_function,'Missing encounter music selector')
    default=re.search(r'else\s*\{\s*idx = (\d+);',music_function[1])
    require(default,'Missing encounter music default')
    return dict(schemaVersion=1,dynamicGraphics=dict(single=singles,double=doubles,default=next(iter(graphics_defaults))),
                encounterMusic=dict(lookup=lookup,songs=songs,defaultMusicIndex=int(default[1])))
