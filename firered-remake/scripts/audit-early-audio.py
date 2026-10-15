#!/usr/bin/env python3
"""Read-only audit of pinned FireRed opening/first-badge audio inputs.

No network, ROM, upstream executable, audio device or product mutation. The
normal command re-derives and validates pinned Git inputs; --report optionally
writes the resulting inventory. No committed duplicate snapshot is required.
This is a source inventory, not an m4a renderer or listening acceptance test.
"""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import struct
import subprocess

ROOT = Path(__file__).resolve().parents[1]
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
PROFILE = "firered-early-audio-source-v1"
SPECIES = "BULBASAUR IVYSAUR VENUSAUR CHARMANDER CHARMELEON CHARIZARD SQUIRTLE WARTORTLE BLASTOISE CATERPIE METAPOD BUTTERFREE WEEDLE KAKUNA BEEDRILL PIDGEY PIDGEOTTO PIDGEOT RATTATA RATICATE PIKACHU RAICHU SANDSHREW SANDSLASH GEODUDE GRAVELER GOLEM ONIX STEELIX".split()
MAPS = "PalletTown PalletTown_PlayersHouse_1F PalletTown_PlayersHouse_2F PalletTown_RivalsHouse PalletTown_ProfessorOaksLab Route1 ViridianCity ViridianCity_Mart ViridianCity_PokemonCenter_1F Route2 Route2_ViridianForest_SouthEntrance ViridianForest Route2_ViridianForest_NorthEntrance PewterCity PewterCity_Gym PewterCity_Mart PewterCity_PokemonCenter_1F PewterCity_House1 PewterCity_House2 PewterCity_Museum_1F PewterCity_Museum_2F".split()
EVENT_MUSIC = "OAK ENCOUNTER_RIVAL RIVAL_EXIT ENCOUNTER_BOY ENCOUNTER_GIRL VS_TRAINER VS_WILD VS_GYM_LEADER VICTORY_TRAINER VICTORY_WILD VICTORY_GYM_LEADER CAUGHT CAUGHT_INTRO LEVEL_UP OBTAIN_ITEM OBTAIN_KEY_ITEM HEAL OBTAIN_BADGE OBTAIN_TMHM EVOLVED EVOLUTION_INTRO EVOLUTION".split()
COMMON_SE = "USE_ITEM PC_LOGIN PC_OFF PC_ON SELECT WIN_OPEN WALL_HIT EXIT LEDGE NOT_EFFECTIVE EFFECTIVE SUPER_EFFECTIVE BALL_OPEN FAINT FLEE BALL EXP EXP_MAX SWITCH CLICK SAVE BALL_BOUNCE_1 BALL_BOUNCE_2 BALL_BOUNCE_3 BALL_BOUNCE_4 BALL_THROW BALL_CLICK FIELD_POISON LOW_HEALTH DEX_SCROLL DEX_PAGE DEX_SEARCH BALL_TRAY_ENTER BALL_TRAY_BALL BALL_TRAY_EXIT DOOR BAG_CURSOR BAG_POCKET SHOP HELP_OPEN HELP_CLOSE HELP_ERROR M_STAT_INCREASE M_STAT_DECREASE".split()
MOVES = "TACKLE SCRATCH GROWL TAIL_WHIP VINE_WHIP EMBER BUBBLE WATER_GUN POISON_STING STRING_SHOT HARDEN GUST SAND_ATTACK QUICK_ATTACK ROCK_TOMB BIND ROCK_THROW LEECH_SEED CONFUSION WING_ATTACK LEER BIDE POISON_POWDER STUN_SPORE SLEEP_POWDER".split()
RULE_PATHS = [
    "include/constants/songs.h", "include/constants/sound.h", "include/constants/species.h",
    "include/gba/m4a_internal.h", "sound/song_table.inc", "sound/music_player_table.inc",
    "sound/voice_groups.inc", "sound/keysplit_tables.inc", "sound/direct_sound_data.inc",
    "sound/programmable_wave_data.inc", "sound/cry_tables.inc", "sound/MPlayDef.s",
    "asm/macros/music_voice.inc", "src/sound.c", "src/m4a.c", "src/m4a_1.s",
    "src/overworld.c", "src/battle_setup.c", "src/battle_main.c", "src/battle_anim_sound_tasks.c",
    "src/field_effect.c", "src/scrcmd.c", "src/pokemon.c", "data/battle_anim_scripts.s",
    "sound/songs/midi/midi.cfg", "tools/mid2agb/midi.cpp", "tools/mid2agb/agb.cpp",
    "tools/mid2agb/main.cpp", "tools/wav2agb/converter.cpp", "tools/wav2agb/wav_file.cpp",
    "Makefile", "data/sound_data.s", "src/m4a_tables.c", "audio_rules.mk",
    "src/main.c", "include/main.h", "src/battle_script_commands.c", "src/pokeball.c", "src/evolution_scene.c",
    "src/battle_message.c", "src/text.c",
]


def require(ok, message):
    if not ok:
        raise ValueError(message)


def canonical(value):
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def vlq(data, pos):
    value = 0
    for _ in range(4):
        require(pos < len(data), "Truncated MIDI VLQ")
        b = data[pos]
        pos += 1
        value = (value << 7) | (b & 127)
        if b < 128:
            return value, pos
    raise ValueError("Invalid MIDI VLQ")


def midi(data):
    require(data[:4] == b"MThd" and len(data) >= 14, "Invalid MIDI header")
    size = struct.unpack_from(">I", data, 4)[0]
    fmt, count, division = struct.unpack_from(">HHH", data, 8)
    require(fmt in (0, 1) and count > 0 and 0 < division < 32768, "Unsupported MIDI timing/format")
    pos, tracks, tempos, markers = 8 + size, [], [], []
    for number in range(count):
        require(data[pos:pos + 4] == b"MTrk", "Missing MIDI track")
        length = struct.unpack_from(">I", data, pos + 4)[0]
        body = data[pos + 8:pos + 8 + length]
        require(len(body) == length, "Truncated MIDI track")
        pos += 8 + length
        cursor, tick, running, notes = 0, 0, None, []
        events, programs, controllers, ended = [], [0] * 16, set(), False
        while cursor < len(body):
            delta, cursor = vlq(body, cursor)
            tick += delta
            require(cursor < len(body), "Truncated MIDI status")
            status = body[cursor]
            if status >= 128:
                cursor += 1
                if status < 240:
                    running = status
            else:
                require(running is not None, "MIDI running status without status")
                status = running
            if status == 255:
                kind = body[cursor]
                cursor += 1
                n, cursor = vlq(body, cursor)
                payload = body[cursor:cursor + n]
                require(len(payload) == n, "Truncated MIDI meta event")
                cursor += n
                if kind == 81:
                    require(n == 3, "Invalid MIDI tempo")
                    tempos.append({"tick": tick, "microsecondsPerQuarter": int.from_bytes(payload, "big")})
                if 1 <= kind <= 7:
                    text = payload.decode("latin1")
                    if text in ("[", "]", "][", ":"):
                        markers.append({"track": number, "tick": tick, "text": text})
                if kind == 47:
                    ended = True
                    # Several pinned RS-derived tracks repeat a zero-delta EOT.
                    tail = body[cursor:]
                    require(tail in (b"", b"\x00\xff\x2f\x00"), "Data after MIDI end-of-track")
                    cursor = len(body)
            elif status in (240, 247):
                n, cursor = vlq(body, cursor)
                cursor += n
                require(cursor <= len(body), "Truncated SysEx")
            else:
                require(128 <= status < 240, "Unsupported MIDI status")
                kind, channel = status >> 4, status & 15
                n = 1 if kind in (12, 13) else 2
                payload = list(body[cursor:cursor + n])
                require(len(payload) == n and all(v < 128 for v in payload), "Invalid MIDI event")
                cursor += n
                events.append((tick, kind, channel, payload))
                if kind == 12:
                    programs[channel] = payload[0]
                elif kind == 11:
                    controllers.add(payload[0])
                elif kind == 9 and payload[1] > 0:
                    notes.append({"tick": tick, "channel": channel, "key": payload[0], "velocity": payload[1], "program": programs[channel]})
        require(ended, "Missing MIDI end-of-track")
        tracks.append({"track": number, "endTick": tick, "notes": notes, "controllers": sorted(controllers), "events": events})
    require(pos == len(data), "Trailing MIDI bytes")
    return {"format": fmt, "ticksPerQuarter": division, "tracks": tracks, "tempos": sorted(tempos, key=lambda e: e["tick"]), "markers": markers}


def midi_seconds(parsed, tick):
    last, tempo, seconds = 0, 500000, 0.0
    for event in parsed["tempos"]:
        if event["tick"] > tick:
            break
        seconds += (event["tick"] - last) * tempo / parsed["ticksPerQuarter"] / 1000000
        last, tempo = event["tick"], event["microsecondsPerQuarter"]
    return seconds + (tick - last) * tempo / parsed["ticksPerQuarter"] / 1000000


def wav(data):
    require(data[:4] == b"RIFF" and data[8:12] == b"WAVE", "Invalid RIFF/WAVE")
    require(struct.unpack_from("<I", data, 4)[0] + 8 == len(data), "RIFF size differs")
    pos, chunks = 12, {}
    while pos + 8 <= len(data):
        key, size = data[pos:pos + 4], struct.unpack_from("<I", data, pos + 4)[0]
        require(key not in chunks, "Duplicate WAV chunk")
        chunks[key] = data[pos + 8:pos + 8 + size]
        require(len(chunks[key]) == size, "Truncated WAV chunk")
        pos += 8 + size + size % 2
    require(pos == len(data), "Trailing WAV bytes")
    require(b"fmt " in chunks and b"data" in chunks, "Missing WAV format/data")
    fmt, channels, rate, _, align, bits = struct.unpack_from("<HHIIHH", chunks[b"fmt "])
    require((fmt, channels, align, bits) == (1, 1, 1, 8), "Expected pinned unsigned 8-bit mono PCM")
    pcm = chunks[b"data"]
    require(len(pcm) > 0, "Empty WAV sample")
    info = {"format": "pcm-u8-mono", "sampleRate": rate, "sampleCount": len(pcm), "pcmSha256": digest(pcm), "loopEnabled": False}
    if b"smpl" in chunks:
        s = chunks[b"smpl"]
        require(len(s) >= 36, "Truncated smpl")
        info["unityNote"], info["pitchFraction"] = struct.unpack_from("<II", s, 12)
        loops = struct.unpack_from("<I", s, 28)[0]
        require(loops in (0, 1), "Unsupported sample loops")
        if loops:
            require(len(s) >= 60, "Truncated sample loop")
            _, kind, start, end, _, _ = struct.unpack_from("<IIIIII", s, 36)
            require(kind == 0 and start <= end < len(pcm), "Invalid sample loop")
            info.update(loopEnabled=True, smplLoopBegin=start, smplLoopEndExclusive=end + 1)
    for key, name in [(b"agbp", "agbPitch"), (b"agbl", "agbLoopEnd")]:
        if key in chunks:
            require(len(chunks[key]) == 4, "Invalid AGB metadata")
            info[name] = struct.unpack("<I", chunks[key])[0]
    info["effectiveEndExclusive"] = info.get("agbLoopEnd") or info.get("smplLoopEndExclusive", len(pcm))
    info["effectivePitchHz"] = info.get("agbPitch", rate * 1024) / 1024
    samples = [x - 128 for x in pcm]
    info["peakPcm8"] = max(abs(x) for x in samples)
    info["rmsPcm8"] = round(math.sqrt(sum(x*x for x in samples)/len(samples)), 6)
    require(info["peakPcm8"] > 0 and info["rmsPcm8"] > 0, "Silent source WAV")
    return info, pcm


class Source:
    def __init__(self, root):
        self.root, self.files = root.resolve(), {}
        self.tree = subprocess.check_output(["git", "-C", str(root), "rev-parse", COMMIT + "^{tree}"], text=True).strip()
        require(self.tree == TREE, "Pinned reference tree differs")
        rows = subprocess.check_output(["git", "-C", str(root), "ls-tree", "-r", COMMIT], text=True).splitlines()
        self.blobs = {row.split("\t", 1)[1]: row.split("\t", 1)[0].split() for row in rows}

    def read(self, path):
        target = self.root / path
        require(target.resolve().is_relative_to(self.root) and not target.is_symlink(), "Unsafe source path")
        data = target.read_bytes()
        blob = hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()
        require(self.blobs.get(path) == ["100644", "blob", blob], "Source differs from fixed Git blob: " + path)
        self.files[path] = {"path": path, "byteLength": len(data), "gitBlobSha1": blob, "sha256": digest(data)}
        return data

    def text(self, path):
        return self.read(path).decode()


def build(root):
    source = Source(root)
    texts = {p: source.text(p) for p in RULE_PATHS}
    constants = {s: int(v) for s, v in re.findall(r"^#define\s+((?:MUS|SE)_\w+)\s+(\d+)", texts["include/constants/songs.h"], re.M)}
    table = re.findall(r"^\s*song\s+(\w+),\s*(\d+),\s*(\d+)", texts["sound/song_table.inc"], re.M)
    configs = dict(re.findall(r"^(\w+)\.mid:\s*(.*)$", texts["sound/songs/midi/midi.cfg"], re.M))
    groups, current, voice_rows, group_starts = {}, None, [], {}
    for line in texts["sound/voice_groups.inc"].splitlines():
        match = re.match(r"(voicegroup\d+)::", line)
        if match:
            current = match[1]
            groups[current] = []
            group_starts[current] = len(voice_rows)
        elif line.strip().startswith("voice_"):
            macro, args = line.strip().split(None, 1)
            row = {"macro": macro, "args": [a.strip() for a in args.split(",")]}
            groups[current].append(row)
            voice_rows.append(row)
    split_tables, split_starts, split_bytes, current = {}, {}, [], None
    for line in texts["sound/keysplit_tables.inc"].splitlines():
        m = re.match(r"\.set (KeySplitTable\d+), \. - (\d+)", line)
        if m:
            current, start = m[1], int(m[2])
            split_tables[current] = {}
            split_starts[current] = len(split_bytes) - start
        m = re.match(r"\s*\.byte (\d+)\s*@\s*(\d+)", line)
        if m:
            split_tables[current][int(m[2])] = int(m[1])
            split_bytes.append(int(m[1]))
    wave_paths = {}
    for path in ("sound/direct_sound_data.inc", "sound/programmable_wave_data.inc"):
        for label, asset in re.findall(r'(\w+)::\s*\n\s*\.incbin "([^"]+)"', texts[path]):
            wave_paths[label] = asset[:-4] + ".wav" if asset.endswith(".bin") else asset

    def resolve(group, program, key, depth=0):
        # Labels may split a physical bank: e.g. drum keys in group001 continue
        # into group002. The source stores contiguous 12-byte ToneData entries.
        index = group_starts[group] + program
        require(depth < 5 and program >= 0 and index < len(voice_rows), "Invalid/cyclic voice reference")
        voice = voice_rows[index]
        macro, args = voice["macro"], voice["args"]
        if macro == "voice_keysplit_all":
            return resolve(args[0], key, key, depth + 1)
        if macro == "voice_keysplit":
            split_index = split_starts[args[1]] + key
            require(0 <= split_index < len(split_bytes), "Key outside physical split data")
            return resolve(args[0], split_bytes[split_index], key, depth + 1)
        result = {"voicegroup": group, "program": program, **voice}
        for arg in args:
            if arg in wave_paths:
                result["samplePath"] = wave_paths[arg]
        return result

    maps, music, environment_cries = [], set("MUS_" + s for s in EVENT_MUSIC), []
    for name in MAPS:
        data = json.loads(source.text("data/maps/" + name + "/map.json"))
        maps.append({"map": name, "music": data["music"]})
        music.add(data["music"])
        script_path = "data/maps/" + name + "/scripts.inc"
        script = source.text(script_path)
        for line_number, line in enumerate(script.splitlines(), 1):
            m = re.match(r"\s*(?:playbgm|playfanfare)\s+(MUS_\w+)", line)
            if m and m[1] != "MUS_DUMMY":
                music.add(m[1])
            m = re.match(r"\s*playmoncry\s+SPECIES_(\w+),\s*(\w+)", line)
            if m:
                environment_cries.append({"species": "SPECIES_" + m[1], "mode": m[2], "path": script_path, "line": line_number})
    fanfares = {s: int(n) for s, n in re.findall(r"\{\s*(MUS_\w+),\s*(\d+)\s*\}", texts["src/sound.c"])}
    blocks = dict(re.findall(r"^(\w+):\s*\n(.*?)(?=^\w+:|\Z)", texts["data/battle_anim_scripts.s"], re.M | re.S))
    effects = set("SE_" + s for s in COMMON_SE)
    move_effects = []
    for move in MOVES:
        queue, visited, move_sounds = ["Move_" + move], set(), set()
        while queue:
            label = queue.pop()
            if label in visited:
                continue
            require(label in blocks, "Unknown animation label " + label)
            visited.add(label)
            body = blocks[label]
            move_sounds.update(re.findall(r"\bSE_\w+", body))
            for line in body.splitlines():
                if re.match(r"\s*(call|goto|jump\w*)\s", line):
                    queue.extend(token for token in re.findall(r"\b\w+\b", line) if token in blocks)
        effects.update(move_sounds)
        move_effects.append({"move": move, "animationBlocks": sorted(visited), "se": sorted(move_sounds), "cryTask": move == "GROWL"})
    songs, needed_samples = [], set()
    for symbol in sorted(music | effects):
        sid = constants[symbol]
        name, player, ms = table[sid]
        require(name == symbol.lower(), "Song table/constant mismatch " + symbol)
        cfg = configs[name]
        values = dict(re.findall(r"-([GRVP])([0-9]+)", cfg))
        group = "voicegroup" + values.get("G", "0").zfill(3)
        path = "sound/songs/midi/" + name + ".mid"
        parsed = midi(source.read(path))
        voices = {}
        for track in parsed["tracks"]:
            for note in track["notes"]:
                voice = resolve(group, note["program"], note["key"])
                voices[canonical(voice)] = voice
                if "samplePath" in voice:
                    needed_samples.add(voice["samplePath"])
        end = max(t["endTick"] for t in parsed["tracks"])
        songs.append({"symbol": symbol, "songId": sid, "player": int(player), "musicStatusPlayer": int(ms), "path": path,
                      "mid2agbOptions": cfg, "voicegroup": group, "fanfareCounterFrames": fanfares.get(symbol),
                      "midi": {"format": parsed["format"], "ticksPerQuarter": parsed["ticksPerQuarter"], "endTick": end,
                               "nominalMidiSeconds": round(midi_seconds(parsed, end), 9), "tempoEvents": parsed["tempos"],
                               "loopMarkers": parsed["markers"], "tracks": [{"track": t["track"], "endTick": t["endTick"], "noteCount": len(t["notes"]), "controllers": t["controllers"]} for t in parsed["tracks"]]},
                      "voices": sorted(voices.values(), key=lambda v: (v["voicegroup"], v["program"]))})
    species_ids = {s: int(v) for s, v in re.findall(r"^#define\s+SPECIES_(\w+)\s+(\d+)", texts["include/constants/species.h"], re.M)}
    cries = []
    cry_table = re.findall(r"^\s*cry (Cry_\w+)", texts["sound/cry_tables.inc"], re.M)
    cry_species = sorted(set(SPECIES) | {e["species"].removeprefix("SPECIES_") for e in environment_cries}, key=lambda s: species_ids[s])
    for species in cry_species:
        sid = species_ids[species]
        require(sid <= 251, "Cry mapping requires explicit Hoenn mapping")
        label = cry_table[sid - 1]
        require(label.lower().replace("_", "") == ("cry_" + species.lower()).replace("_", ""), "Cry identity mismatch")
        path = wave_paths[label]
        info, _ = wav(source.read(path))
        cries.append({"species": "SPECIES_" + species, "speciesId": sid, "cryTableIndex": sid - 1, "path": path, **info})
    samples = []
    for path in sorted(needed_samples):
        data = source.read(path)
        if path.endswith(".wav"):
            info, _ = wav(data)
        else:
            require(path.endswith(".pcm") and len(data) == 16, "Unexpected programmable wave")
            info = {"format": "psg-wave-32-unsigned-nibbles", "sampleCount": 32}
        samples.append({"path": path, **info})
    files = sorted(source.files.values(), key=lambda f: f["path"])
    return {"schemaVersion": 1, "profile": PROFILE, "source": {"repository": "pret/pokefirered", "commit": COMMIT, "tree": TREE},
            "scope": "Current 21 early maps including explicit script music/cry operands; event/battle/evolution music; 25 selected early move animations; 29 currently catalogued species plus connected environmental cry references. Catalogued trade/item evolutions are inventory, not reachability claims. Intro/title/Teachy TV and later areas are outside this batch.",
            "status": "source-audited-not-rendered", "maps": maps, "songs": songs, "moveEffects": move_effects,
            "cries": cries, "environmentCryUses": environment_cries, "samples": samples, "files": files, "fullSourceHash": digest(canonical(files)),
            "limitations": ["MIDI seconds are nominal SMF timing, not m4a frame timing or PCM end/loop positions.", "PCM source measurement does not establish rendered m4a output, audible playback or listening acceptance.", "Runtime cry modes, reverse/chorus/release and original compressed decode require rendering, not raw-WAV passthrough.", "Move animation SE closure includes named branch/call blocks; sounds emitted inside C tasks need separate runtime tracing."]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference-root", type=Path)
    parser.add_argument("--report", type=Path, help="Optional audit report outside runtime content.")
    args = parser.parse_args()
    root = args.reference_root or Path(os.environ.get("FIRERED_ORIGINAL_REFERENCE") or (ROOT / ".tools/reference-root").read_text(encoding="utf-8").strip())
    result = build(root)
    if args.report:
        args.report.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"status": "source-audit-passed", "maps": len(result["maps"]), "songs": len(result["songs"]), "cries": len(result["cries"]), "instrumentSamples": len(result["samples"]), "sourceFiles": len(result["files"]), "fullSourceHash": result["fullSourceHash"], "renderedAudio": False, "audibleOutputVerified": False}))


if __name__ == "__main__":
    main()
