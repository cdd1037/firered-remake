#!/usr/bin/env python3
"""Import the bounded Power Plant catalog from pinned local Git objects; --check is byte exact.
No reference repository code is executed. Includes the Thunder Stone ownership closure.
"""
import argparse, hashlib, json, re, subprocess
from pathlib import Path
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
SPECIES=['SPECIES_ELECTABUZZ','SPECIES_JOLTEON','SPECIES_ZAPDOS']
BASE='content/generated/power-plant-pokemon/'
RAW='content/raw/power-plant-pokemon/'
DEPENDENCIES={
 'content/generated/canonical-pokemon/catalog.json':'c5ef32d4b425c9ce720b0b29305d5855e6c7f5eff8ff05cc36d2cb51584eb9bc',
 'content/generated/five-island-rules/items.json':'c45233322a3b0bd5c2efe55063ec6066907a6e1a24ff0cfe3088a7b33d4d3968',
 'content/generated/five-island-rules/pokemon.json':'9788f567d061f21e66d877718a542a3fb2068629f34b65da9dd604e387a0a909',
 'content/generated/celio-pokemon/pokemon.json':'3f5b6aeeb5454af27c38ff837757fe9f50647a2ed95dc19ea94e83dc05c54e9f',
 'content/generated/seven-island-rules/pokemon.json':'5c8d7774206cb06ce8798abde68b0c36048bebdc214b4e61c80bfdbf45e70d38',
 'content/generated/seven-island-rules/machines.json':'a9bb573db76700ac35b25510b6452ac3acd36993e6b8aecb2cc6b03407659653',
 'content/generated/event-island-species/catalog.json':'ec077629917befe12235c92727590edefe2d752104793e54b923d21f46ddfa2a',
 'content/generated/league-rematch-rules/pokemon.json':'d9e9657f331699b88a9909d1bbb8dce05797ee0714e4c980730884aab4725b39',
 'scripts/five-island-predecessor.json':'e2241d337388ef781209563eed0fa100c5d219841830ff3f400b71500e37278f'}
def sha(b):return hashlib.sha256(b).hexdigest()
def fingerprint(v):return sha(json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode())
def encoded(v):return (json.dumps(v,indent=2,ensure_ascii=False)+'\n').encode()
def ident(s):return dict(value='pret:'+s)
def need(v,m):
 if not v:raise ValueError(m)
def build_power_plant_presentation(read_source_bytes,dependency_root):
 """Source-authored Power Plant presentation builder; never executes reference code.

 build_presentation(read_source_bytes, dependency_root) returns (files, metadata).
 files uses repository-relative paths and includes generated art, raw source cache,
 an authenticated presentation manifest, and its lock. The caller writes files.
 metadata contains the decoded manifest plus lock/report and MtMoon-schema itemIcons.
 The source callback must return bytes from COMMIT; every read is hash authenticated.
 """
 import base64
 import hashlib
 import io
 import json
 import pathlib
 import re
 import struct
 import wave
 import zlib
 from PIL import Image

 COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
 TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
 PROFILE = 'firered-power-plant-presentation-v1'
 BASE = 'content/generated/power-plant-presentation/'
 RAW = 'content/raw/power-plant-presentation/'
 SPECIES = [('Electabuzz', 125), ('Jolteon', 135), ('Zapdos', 145)]
 SOURCE_PINS = {'graphics/items/icon_palettes/electric_tm_hm.pal': {'byteLength': 193,
                                                      'gitBlobSha1': 'f4ce9febe3b0f976bd8f7adb1284b9abebf4e5c9',
                                                      'path': 'graphics/items/icon_palettes/electric_tm_hm.pal',
                                                      'sha256': '1f23275340a37d78164095a72a2e9628467992c27f29256369b7d3b4fc193d04'},
  'graphics/items/icon_palettes/normal_tm_hm.pal': {'byteLength': 198,
                                                    'gitBlobSha1': 'a5bd9da1c79e9489f785b964c962ab50da39f247',
                                                    'path': 'graphics/items/icon_palettes/normal_tm_hm.pal',
                                                    'sha256': '6a18911b91817e9b86068c1e485420dd99751da04603f6241ee783339bed69d4'},
  'graphics/items/icon_palettes/thunder_stone.pal': {'byteLength': 165,
                                                     'gitBlobSha1': 'debec0709771d046f509e382fdf772ea200b8e8b',
                                                     'path': 'graphics/items/icon_palettes/thunder_stone.pal',
                                                     'sha256': 'd713cf4a5c9b870049c342e53e0916a08f6b27dfeea2b7c4cd9ccdd070cb7cdf'},
  'graphics/items/icons/thunder_stone.png': {'byteLength': 305,
                                             'gitBlobSha1': '8955b0224ce8fd14dd37fce936c7d97117dff5c1',
                                             'path': 'graphics/items/icons/thunder_stone.png',
                                             'sha256': '5b6280774db99b6a18b5730b6197318557d157db15f0374fcee35fdfb4e267e9'},
  'graphics/items/icons/tm_hm.png': {'byteLength': 344,
                                     'gitBlobSha1': 'c4e6d1df78a0498443ae3567ec7c9f3839e7713a',
                                     'path': 'graphics/items/icons/tm_hm.png',
                                     'sha256': 'ad43897b481aa49e13dd85f205fe5fe9496212e47266eb3e9359113ad03d77bf'},
  'graphics/pokedex/kanto_dex_bgpals.pal': {'byteLength': 2004,
                                            'gitBlobSha1': 'b719d111f73ad26966817ebb64a72cf0e369a2a5',
                                            'path': 'graphics/pokedex/kanto_dex_bgpals.pal',
                                            'sha256': '6592bc9bf2739b97b6a0f97b5163748811709dfdb0ef4987acdc119b6d8355bf'},
  'graphics/pokemon/electabuzz/back.png': {'byteLength': 611,
                                           'gitBlobSha1': '8d84c2cb63de22a92b67501d76e81cc5b54a68bd',
                                           'path': 'graphics/pokemon/electabuzz/back.png',
                                           'sha256': 'cd330920c554b454bc2f22d5f945165dfb98792fa2ba2c534a4306c376d6dce1'},
  'graphics/pokemon/electabuzz/footprint.png': {'byteLength': 93,
                                                'gitBlobSha1': 'b2b4de625b1516ffa56b7d8babc4aeab17f9c571',
                                                'path': 'graphics/pokemon/electabuzz/footprint.png',
                                                'sha256': '31a44e4d6633c0ac84de897687713ee278e8077e5f8955610e04e10628b4d514'},
  'graphics/pokemon/electabuzz/front.png': {'byteLength': 882,
                                            'gitBlobSha1': '1bed04c89feee52972093e46230dd2c02bbc7ee3',
                                            'path': 'graphics/pokemon/electabuzz/front.png',
                                            'sha256': 'bcb9e4790b4bdd2cfbcae4dfead5ccb44160646c1eac741d6b4c8fdd6c26f241'},
  'graphics/pokemon/electabuzz/icon.png': {'byteLength': 321,
                                           'gitBlobSha1': '4c9babe2a98cf3bb79bd76b979f57245bbe8e9cf',
                                           'path': 'graphics/pokemon/electabuzz/icon.png',
                                           'sha256': 'd4ca6dfc30d137ebd31632d3a14f05bcf2de3119ce453495a6b48fffd8ee2b97'},
  'graphics/pokemon/electabuzz/normal.pal': {'byteLength': 190,
                                             'gitBlobSha1': '75bfcbabe20e4c81da66a88164d70bf8c96b1db3',
                                             'path': 'graphics/pokemon/electabuzz/normal.pal',
                                             'sha256': '0d9252e7350566aed3e3294b34f7e40bb251936ba817236356103f89de5022b3'},
  'graphics/pokemon/electabuzz/shiny.pal': {'byteLength': 186,
                                            'gitBlobSha1': 'df9fa23e84f87d426abb1be59bd3903bdf08b1ef',
                                            'path': 'graphics/pokemon/electabuzz/shiny.pal',
                                            'sha256': '506b5cecd15c7498c3e93dd119c38180247369b904a2624e533dbfbde1d4c828'},
  'graphics/pokemon/icon_palettes/icon_palette_0.pal': {'byteLength': 195,
                                                        'gitBlobSha1': '48a835b0db32aa5a2195f9dccac3c84e8b5bb87d',
                                                        'path': 'graphics/pokemon/icon_palettes/icon_palette_0.pal',
                                                        'sha256': '05e68987f3a786c27943a03005221d25fdf35e32b201b1b212f7fa454b35ec7a'},
  'graphics/pokemon/icon_palettes/icon_palette_1.pal': {'byteLength': 195,
                                                        'gitBlobSha1': '261ea164d9415f0c3f35ff0efe63df968c9d2c93',
                                                        'path': 'graphics/pokemon/icon_palettes/icon_palette_1.pal',
                                                        'sha256': '39bd2dbfe6ddb9d27cc002fbc3764a7b834178a42fde8bf17adef514037112ae'},
  'graphics/pokemon/jolteon/back.png': {'byteLength': 754,
                                        'gitBlobSha1': '0041316004f1a8a6879c11c0100eb2bf70a236c0',
                                        'path': 'graphics/pokemon/jolteon/back.png',
                                        'sha256': '8d100871bb0e76541b2e6b6b49ce4df6842f336f7f102a1d98645bc110e79a4d'},
  'graphics/pokemon/jolteon/footprint.png': {'byteLength': 91,
                                             'gitBlobSha1': 'd1e9477b3d0c9da9d144ac0fe63e0bf72c968881',
                                             'path': 'graphics/pokemon/jolteon/footprint.png',
                                             'sha256': 'e9c7c9399298696f66a4854ff1691f3e1872fc62e5797694b10bae2040bcd882'},
  'graphics/pokemon/jolteon/front.png': {'byteLength': 706,
                                         'gitBlobSha1': 'be21a9d9ebfd505fbf0ef5587b607daab6f44419',
                                         'path': 'graphics/pokemon/jolteon/front.png',
                                         'sha256': '7a795f6e21a720bf8a29a3fca909659708b843c02ea80372a25018a7cf311e2e'},
  'graphics/pokemon/jolteon/icon.png': {'byteLength': 324,
                                        'gitBlobSha1': 'b98fd78b2779310dc11e1d2dbaff73ef7d99204d',
                                        'path': 'graphics/pokemon/jolteon/icon.png',
                                        'sha256': '1f6935354a6a79f106562f50e830ce5e703507742cb803e82d6720c353658888'},
  'graphics/pokemon/jolteon/normal.pal': {'byteLength': 186,
                                          'gitBlobSha1': 'c4f6d9021ee92b6648519474122061db21b9a91f',
                                          'path': 'graphics/pokemon/jolteon/normal.pal',
                                          'sha256': 'bb29aff839e38e8d00a328a3b47957ac000f6bee753bf58e46e72fb8cfb6b25b'},
  'graphics/pokemon/jolteon/shiny.pal': {'byteLength': 183,
                                         'gitBlobSha1': 'ece1425c3c4055ee9d7d99029432e81ade8cb2af',
                                         'path': 'graphics/pokemon/jolteon/shiny.pal',
                                         'sha256': '0055a8095e5beebc67fd93395a09d354ddf78bb72eb65055d345377c0aa20964'},
  'graphics/pokemon/zapdos/back.png': {'byteLength': 652,
                                       'gitBlobSha1': '248366dc5dcf0523ac3a47008c9e59d0d401f105',
                                       'path': 'graphics/pokemon/zapdos/back.png',
                                       'sha256': '0375518a53994ee499ccdd9c51f48cf0ea66140471d408fd35cd2c8d642e9817'},
  'graphics/pokemon/zapdos/footprint.png': {'byteLength': 104,
                                            'gitBlobSha1': '480063a88b754bf8889b0d48e7eb984ee208bd42',
                                            'path': 'graphics/pokemon/zapdos/footprint.png',
                                            'sha256': '42f30152cdf4ee53bd0d397f8244ae8834c993811a60b8dfd4e2f09147fc0824'},
  'graphics/pokemon/zapdos/front.png': {'byteLength': 904,
                                        'gitBlobSha1': '2d92490276a62a955d90d158d64753c3b5570d95',
                                        'path': 'graphics/pokemon/zapdos/front.png',
                                        'sha256': '1eafdea0501cb6cad51f65af39f329f8052c07957f7eb227b48fc60cdd13ba3d'},
  'graphics/pokemon/zapdos/icon.png': {'byteLength': 375,
                                       'gitBlobSha1': '26fc734275a300eebbe442ee99e252ffa479fac7',
                                       'path': 'graphics/pokemon/zapdos/icon.png',
                                       'sha256': '27d6cc54e9e6f1feb5c6a785b7d60f7c6496325b429aa09721be12adab93b2fb'},
  'graphics/pokemon/zapdos/normal.pal': {'byteLength': 189,
                                         'gitBlobSha1': '88e36bd7f63eafee0d988f2561d8d4d19ed3cf42',
                                         'path': 'graphics/pokemon/zapdos/normal.pal',
                                         'sha256': 'e1754f4f060392fd0ac61828e6d101339338934d9b73fc8339626d026bbaede5'},
  'graphics/pokemon/zapdos/shiny.pal': {'byteLength': 183,
                                        'gitBlobSha1': 'fb7840ebca04e6486ab023a6fad2412eeda3987c',
                                        'path': 'graphics/pokemon/zapdos/shiny.pal',
                                        'sha256': '9e98ae65c97a69d293a39e56b865072c921fc21d11c847e93b66c8c7ad4e5f42'},
  'include/constants/items.h': {'byteLength': 12436,
                                'gitBlobSha1': 'cfc42256c33bb6b807630deacadad8071a3eeee3',
                                'path': 'include/constants/items.h',
                                'sha256': 'af17276346eaecc5a10ac60f14b22c33332ec8c6fb0ec82895bdc3649b678138'},
  'include/constants/sound.h': {'byteLength': 2051,
                                'gitBlobSha1': '0890f4290b871688076881f395de05c9cba8d2ee',
                                'path': 'include/constants/sound.h',
                                'sha256': '20db92775546c49cc2ef4fa5d1f9f068c58f469d16017d9166b98e3bd6ad02e2'},
  'include/constants/species.h': {'byteLength': 12982,
                                  'gitBlobSha1': '7446cc1380995ab62deff8359bad813aa2b936e9',
                                  'path': 'include/constants/species.h',
                                  'sha256': 'ec3f9f9c2717dcd453aa7a27506c32e1e6ca067864534dee494695d74344c51a'},
  'sound/cry_tables.inc': {'byteLength': 16533,
                           'gitBlobSha1': '22e7433635a64d65d2fcf4de7f3f407b62b239d9',
                           'path': 'sound/cry_tables.inc',
                           'sha256': '18210b001e842eb70a9f3b92834007f9434e332b7f0df0c32ed0de4305f47e3a'},
  'sound/direct_sound_samples/cries/electabuzz.wav': {'byteLength': 15412,
                                                      'gitBlobSha1': '8443823fb71db6d23b32fc69fad154b9053a46c2',
                                                      'path': 'sound/direct_sound_samples/cries/electabuzz.wav',
                                                      'sha256': 'd9ff2b21fadff93fd6910487f863d8672d7961b46ce585ad2c887b641aa901e6'},
  'sound/direct_sound_samples/cries/jolteon.wav': {'byteLength': 7934,
                                                   'gitBlobSha1': '61b5cb16043fc9de1a8511c0d153db6861af8fe5',
                                                   'path': 'sound/direct_sound_samples/cries/jolteon.wav',
                                                   'sha256': '1a983687bd79a6943a3db345da4ea6f1ef14c5b7fc06b916f3d06338e60fd378'},
  'sound/direct_sound_samples/cries/zapdos.wav': {'byteLength': 7756,
                                                  'gitBlobSha1': 'f8168a603ccdb9d3e873470fbb6d40c90ef605d4',
                                                  'path': 'sound/direct_sound_samples/cries/zapdos.wav',
                                                  'sha256': 'f5bdded584f7cfba05822dd6c40f3c9434a210dba104e31c83296566493ef682'},
  'src/data/graphics/items.h': {'byteLength': 46353,
                                'gitBlobSha1': 'b5a5a6bb78cd9058ec81a23c142178f3c9e140c8',
                                'path': 'src/data/graphics/items.h',
                                'sha256': '2d735d48189bddbd4289d6c3fa0c15b7838794fee4d2fb1a468428455b015202'},
  'src/data/graphics/pokemon.h': {'byteLength': 214609,
                                  'gitBlobSha1': 'e744f80e08f2f849238d58fdcbfd29107bd3decd',
                                  'path': 'src/data/graphics/pokemon.h',
                                  'sha256': '72985c8d3d839543aab842efe4b5fc711662296d2c7d1ed00fca4096181618f5'},
  'src/data/item_icon_table.h': {'byteLength': 30370,
                                 'gitBlobSha1': 'a9dd7f0b2e0c7906de145286ac41b78905ea448b',
                                 'path': 'src/data/item_icon_table.h',
                                 'sha256': '1953ce418ec5f56b84d6137240218b7bd03969603c2c9f9b243a9c992114391c'},
  'src/data/pokemon_graphics/back_pic_coordinates.h': {'byteLength': 44872,
                                                       'gitBlobSha1': '17f818f458434bc6c35cb7f10acd7776a36e9c22',
                                                       'path': 'src/data/pokemon_graphics/back_pic_coordinates.h',
                                                       'sha256': 'a8d7ae72ebf462ca1a5915bc4f9a77d0ee2c13d4e9a4d9f2f66dd947256b9fab'},
  'src/data/pokemon_graphics/enemy_mon_elevation.h': {'byteLength': 1834,
                                                      'gitBlobSha1': 'dccd063dae2a1ab421c9a990c891ac2eabadf820',
                                                      'path': 'src/data/pokemon_graphics/enemy_mon_elevation.h',
                                                      'sha256': 'bab9380267270b2eb30ac50cc643bc0e3fc684fa0195e85be0dc97fc9d28f7e1'},
  'src/data/pokemon_graphics/footprint_table.h': {'byteLength': 19857,
                                                  'gitBlobSha1': '86ca067901f96aee2b1aed81912bac4f7e84c99b',
                                                  'path': 'src/data/pokemon_graphics/footprint_table.h',
                                                  'sha256': '90b1bf05b85a00ae2f16a2beacf3316bcf4d313a1197cfb8368728fa39090b70'},
  'src/data/pokemon_graphics/front_pic_coordinates.h': {'byteLength': 44879,
                                                        'gitBlobSha1': 'd521b615fd22c171c441ca6b9e4a56ff37474036',
                                                        'path': 'src/data/pokemon_graphics/front_pic_coordinates.h',
                                                        'sha256': '81a9024f3aa28ab750e6f817367d525e3a97233c981ec05e23850a971c54989f'},
  'src/data/text/species_names.h': {'byteLength': 15851,
                                    'gitBlobSha1': '25f2c9aa2363202e75cf2080be01465b2d64b5c4',
                                    'path': 'src/data/text/species_names.h',
                                    'sha256': '73e87117c98828423b7fc15565bd2a839a0671665fb7c1c1b26264d7d7a8912f'},
  'src/pokemon_icon.c': {'byteLength': 45145,
                         'gitBlobSha1': '037b1bfa82189906b205fe1a50a9276a4e2041e6',
                         'path': 'src/pokemon_icon.c',
                         'sha256': '2034625b3e4b804fb8052400b98bf1920a9cb435699338f18c34ea32e3e2f967'},
  'src/sound.c': {'byteLength': 15232,
                  'gitBlobSha1': '9060aa513d2946d5a59c34f24658da96e0ab6a8e',
                  'path': 'src/sound.c',
                  'sha256': '851313931ebdbf2f0933b52295707f82e948735813fac84fdba74c0446ada958'}}
 DEPENDENCY_PINS = {'content/generated/campaign-hit-art/manifest.json': {'byteLength': 132335,
                                                       'path': 'content/generated/campaign-hit-art/manifest.json',
                                                       'sha256': 'f65895babddf0327ad6884ba4d86d2c582e780c17bf91064599a657361acc94c'},
  'content/generated/mtmoon-items/item_tm05.png': {'byteLength': 2396,
                                                   'path': 'content/generated/mtmoon-items/item_tm05.png',
                                                   'sha256': 'd4677385055d30aa7e104a585b4d680b0a4dddd7a7720500851e7a5eae925894'},
  'content/generated/synth-audio/firered.pack': {'byteLength': 2565436,
                                                 'path': 'content/generated/synth-audio/firered.pack',
                                                 'sha256': '00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d'},
  'content/generated/synth-audio/manifest.json': {'byteLength': 88374,
                                                  'path': 'content/generated/synth-audio/manifest.json',
                                                  'sha256': '10906e43fa80b13eb5d54177a93003df3fae5292f5da3a22b5805ff3e21220df'},
  'content/generated/synth-audio/normal-cries.bin': {'byteLength': 6188,
                                                     'path': 'content/generated/synth-audio/normal-cries.bin',
                                                     'sha256': '623fe59bb7fc0b8d3907dd67bc677a7985037e0086698636f35db82e113d7f2e'},
  'content/generated/vermilion-gym-assets/item_tm34.png': {'byteLength': 2396,
                                                           'path': 'content/generated/vermilion-gym-assets/item_tm34.png',
                                                           'sha256': 'd2f5caef93896fa926cfc060ab62d39a6936c0921fc31fa588a66539cc40045f'}}


 SOURCE_PINS.update({'data/maps/PowerPlant/map.json': {'byteLength': 5065,
                                    'gitBlobSha1': '38950b3a4980822bd4ea5cb6105e8f7b59d87926',
                                    'path': 'data/maps/PowerPlant/map.json',
                                    'sha256': '2a33bb77406ca5fefc99b9adc48ef640f4143d990e9b02c4ba2e96f17be0a8cb'},
  'data/maps/PowerPlant/scripts.inc': {'byteLength': 3645,
                                       'gitBlobSha1': 'ec48a37782877dbd07507bbe58964643c5444d06',
                                       'path': 'data/maps/PowerPlant/scripts.inc',
                                       'sha256': '7558c5214a97c767aa1840d14fa15521e7f0993657ee19a3bb088b4202f6f192'},
  'include/constants/songs.h': {'byteLength': 17891,
                                'gitBlobSha1': '4f5fc3cc6fda1aee6585a0944378be934fc10ded',
                                'path': 'include/constants/songs.h',
                                'sha256': 'ae4166b40bdde0bc014657636f63b49ebe0d3e00f7fb61531c99bf714eba36b4'},
  'sound/direct_sound_samples/cries/electrode.wav': {'byteLength': 12594,
                                                     'gitBlobSha1': '4ad346759d0c3ca7ce4e979df8bab409420adf96',
                                                     'path': 'sound/direct_sound_samples/cries/electrode.wav',
                                                     'sha256': 'c3fd5834be0997c8fae89fccbd390522fde9418e33139e4b16cb8e003fc1b0bc'},
  'sound/song_table.inc': {'byteLength': 9204,
                           'gitBlobSha1': '000cc41b99f419fa043d3730d37b657ac6c48a57',
                           'path': 'sound/song_table.inc',
                           'sha256': '015887679404738d0bbe02ee20c891d79d28284ef29d4f180cfec46510e8e061'},
  'sound/songs/midi/mus_encounter_gym_leader.mid': {'byteLength': 2717,
                                                    'gitBlobSha1': 'cb5fef4876f07cd3ab83f97ec2ba1cf3091a3eda',
                                                    'path': 'sound/songs/midi/mus_encounter_gym_leader.mid',
                                                    'sha256': '4ab1e4aedd52e5cbac57ac43936afd2709e2d0e1be5771674a79b27c42d8a0fe'},
  'sound/songs/midi/mus_poke_mansion.mid': {'byteLength': 18230,
                                            'gitBlobSha1': 'e5ee78eb00f7ff8a3acef00ea17ac5b9bc1d73f0',
                                            'path': 'sound/songs/midi/mus_poke_mansion.mid',
                                            'sha256': 'f7b6286bba45986a40acfa6385ead26d6af6a6c03bd645299c6b9ef39bdf2420'},
  'sound/songs/midi/mus_vs_legend.mid': {'byteLength': 16522,
                                         'gitBlobSha1': '93d3785be6d68ec636b09a9d3d82f164a79a34af',
                                         'path': 'sound/songs/midi/mus_vs_legend.mid',
                                         'sha256': '0b6a1bb1175419e4ef72a5dfb733457a167e3e0494df2a634d68d3608333019d'},
  'sound/songs/midi/mus_vs_wild.mid': {'byteLength': 14879,
                                       'gitBlobSha1': '835aff19b8ad12ea6f593738af02901e13e85917',
                                       'path': 'sound/songs/midi/mus_vs_wild.mid',
                                       'sha256': 'a1a72f5d1fb313f9b2f03c6edd8cc3a4c79fa7becd75545ee25bc6a9415cd344'},
  'src/battle_setup.c': {'byteLength': 33351,
                         'gitBlobSha1': '6d62084dbb697fc3a42d49a6dc865657339dcea2',
                         'path': 'src/battle_setup.c',
                         'sha256': 'f33e6cdd3ff22ab9fe0f43219dfc795a9abfcc5f01c04e96809b98ddf5b952fd'}})
 DEPENDENCY_PINS.update({'content/generated/cinnabar-synth-audio/manifest.json': {'byteLength': 3542,
                                                           'path': 'content/generated/cinnabar-synth-audio/manifest.json',
                                                           'sha256': 'cb132bd0dd8b34a8b305d09fca84fa0b23c7dca09ef9f97937c20f826e6c994c'},
  'content/generated/event-island-presentation/synth-manifest.json': {'byteLength': 6981,
                                                                      'path': 'content/generated/event-island-presentation/synth-manifest.json',
                                                                      'sha256': 'fdbb69c5d1db92476303fc9fb84c022a3fe45ca0e49c35913fc7e8d92fe77d0e'},
  'content/generated/rocktunnel-synth-audio/manifest.json': {'byteLength': 5587,
                                                             'path': 'content/generated/rocktunnel-synth-audio/manifest.json',
                                                             'sha256': '478237a8683d31438da6a5504272e76a52313d1417133767e08fa83a4cc0a621'}})
 def need(ok, message):
     if not ok:
         raise ValueError(message)


 def sha(data):
     return hashlib.sha256(data).hexdigest()


 def encode(value):
     return (json.dumps(value, sort_keys=True, indent=2, ensure_ascii=False) + '\n').encode()


 def png(width, height, rgba):
     # Identical stored-DEFLATE encoding to import-event-island-presentation.py's helper.
     def chunk(kind, data):
         return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
     scan = b''.join(b'\0' + rgba[y * width * 4:(y + 1) * width * 4] for y in range(height))
     blocks = [scan[i:i + 65535] for i in range(0, len(scan), 65535)]
     compressed = b'\x78\x01' + b''.join(bytes([int(i == len(blocks) - 1)]) +
         struct.pack('<HH', len(block), len(block) ^ 65535) + block for i, block in enumerate(blocks))
     compressed += struct.pack('>I', zlib.adler32(scan) & 0xffffffff)
     return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) + chunk(b'IDAT', compressed) + chunk(b'IEND', b'')


 def build_presentation(read_source_bytes, dependency_root):
     raw, inputs, inherited, out, assets = {}, {}, {}, {}, []
     dependency_root = pathlib.Path(dependency_root).absolute()

     def read(path):
         if path not in raw:
             data = read_source_bytes(path)
             need(isinstance(data, bytes), 'Source callback must return bytes: ' + path)
             row = dict(path=path, byteLength=len(data), sha256=sha(data),
                        gitBlobSha1=hashlib.sha1(f'blob {len(data)}\0'.encode() + data).hexdigest())
             if SOURCE_PINS:
                 need(SOURCE_PINS.get(path) == row, 'Pinned source bytes differ: ' + path)
             raw[path], inputs[path] = data, row
         return raw[path]

     def text(path):
         return read(path).decode()

     def reuse(path):
         need(path and not pathlib.PurePosixPath(path).is_absolute() and '\\' not in path and ':' not in path and
              all(p not in ('', '.', '..') for p in path.split('/')), 'Unsafe inherited path')
         file = dependency_root / path
         need(not any(p.is_symlink() for p in (file, *file.parents)), 'Inherited symlink rejected')
         data = file.read_bytes()
         row = dict(path=path, byteLength=len(data), sha256=sha(data))
         if DEPENDENCY_PINS:
             need(DEPENDENCY_PINS.get(path) == row, 'Pinned inherited bytes differ: ' + path)
         inherited[path] = row
         return data

     def ref(path, symbol, match=None):
         data = read(path)
         content = '' if path.endswith('.png') else data.decode()
         return dict(path=path, symbol=symbol, startLine=1 if match is None else content.count('\n', 0, match.start()) + 1,
                     endLine=max(1, content.count('\n')) if match is None else content.count('\n', 0, match.end()) + 1, sha256=sha(data))

     def emit(identity, width, height, rgba, paths):
         need(len(rgba) == width * height * 4, 'RGBA dimensions differ')
         out[identity + '.rgba'], out[identity + '.png'] = rgba, png(width, height, rgba)
         row = dict(id=identity, width=width, height=height, rgbaPath=BASE + identity + '.rgba',
                    texturePath=BASE + identity + '.png', rgbaSha256=sha(rgba), sha256=sha(out[identity + '.png']), sourcePaths=paths)
         assets.append(row)
         return row

     def palette(path, floor=True):
         lines = text(path).splitlines()
         need(lines[:2] == ['JASC-PAL', '0100'] and len(lines) == int(lines[2]) + 3, 'Palette framing differs: ' + path)
         colors = [tuple(map(int, line.split())) for line in lines[3:]]
         need(all(len(row) == 3 and all(0 <= c <= 255 for c in row) for row in colors), 'Palette channels differ')
         return [tuple((c >> 3) * 255 // 31 if floor else ((c >> 3) << 3) | ((c >> 3) >> 2) for c in row) for row in colors]

     def indexed(path, pal, shape, floor=True, first_frame=False):
         with Image.open(io.BytesIO(read(path))) as im:
             need(im.mode == 'P' and im.size == shape and max(im.tobytes()) < 16, 'Indexed source differs: ' + path)
             indices = im.tobytes()[:1024] if first_frame else im.tobytes()
         colors = palette(pal, floor)
         return indices, bytes(v for index in indices for v in (*colors[index], 255 if index else 0))

     sprites, icons, footprints, names = [], [], [], []
     graphics = text('src/data/graphics/pokemon.h')
     constants = text('include/constants/species.h')
     for name, species in SPECIES:
         stem, symbol = name.lower(), 'SPECIES_' + name.upper()
         need(re.search(r'^#define ' + symbol + r'\s+' + str(species) + r'\s*$', constants, re.M), 'Species ID differs')
         elevation_match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', text('src/data/pokemon_graphics/enemy_mon_elevation.h'))
         elevation = int(elevation_match[1]) if elevation_match else 0
         for side in ('front', 'back'):
             coordinates = 'src/data/pokemon_graphics/' + side + '_pic_coordinates.h'
             match = re.search(r'\[' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', text(coordinates))
             need(match, 'Species coordinates missing')
             width, height, y_offset = map(int, match.groups())
             image = f'graphics/pokemon/{stem}/{side}.png'
             need(f'gMon{side.title()}Pic_{name}[] = INCBIN_U32("{image[:-4]}.4bpp.lz")' in graphics, 'Species image binding differs')
             for variant in ('normal', 'shiny'):
                 pal = f'graphics/pokemon/{stem}/{variant}.pal'
                 palette_symbol = 'gMonPalette_' if variant == 'normal' else 'gMonShinyPalette_'
                 need(f'{palette_symbol}{name}[] = INCBIN_U32("{pal[:-4]}.gbapal.lz")' in graphics, 'Species palette binding differs')
                 indices, rgba = indexed(image, pal, (64, 64))
                 x, y = (144, 8 + y_offset - elevation) if side == 'front' else (40, 48 + y_offset)
                 sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, renderSpeciesId=species, sourceFrameIndex=0, form=None,
                     side=side, paletteVariant=variant, x=x, y=y, sourceWidth=width, sourceHeight=height,
                     sourceYOffset=y_offset, sourceEnemyElevation=elevation, indexSha256=sha(indices), sourceCoordinatePath=coordinates,
                     paletteExpansion='floor(rgb5 * 255 / 31)', transparentIndex=0,
                     **emit(f'{stem}-{side}-{variant}', 64, 64, rgba, [image, pal, coordinates])))
         icon_table = text('src/pokemon_icon.c')
         match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', icon_table)
         need(match and re.search(r'\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', icon_table), 'Species icon binding differs')
         pal_index = int(match[1])
         image, pal = f'graphics/pokemon/{stem}/icon.png', f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
         need(f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")' in graphics, 'Species icon image binding differs')
         indices, rgba = indexed(image, pal, (32, 64), first_frame=True)
         icons.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, paletteIndex=pal_index, sourceFrameIndex=0,
                           sourceFrameCount=2, indexSha256=sha(indices), **emit('icons/' + stem, 32, 32, rgba, [image, pal])))
         image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
         need('[' + symbol + '] = gMonFootprint_' + name + ',' in text('src/data/pokemon_graphics/footprint_table.h'), 'Species footprint binding differs')
         with Image.open(io.BytesIO(read(image))) as im:
             need(im.mode == '1' and im.size == (16, 16), 'Footprint source differs')
             indices = bytes(1 if v == 0 else 0 for v in im.get_flattened_data())
         colors = palette(pal)
         rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
         footprints.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, **emit(stem + '-footprint', 16, 16, rgba, [image, pal])))
         display = re.search(r'\[' + symbol + r'\]\s*=\s*_\("([^"]+)"\)', text('src/data/text/species_names.h'))
         need(display, 'Species display name missing')
         names.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, name=display[1]))

     pack = reuse('content/generated/synth-audio/firered.pack')
     cries = reuse('content/generated/synth-audio/normal-cries.bin')
     need(sha(pack) == '00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d' and len(pack) == 0x086fbea4 - 0x08489968 and
          sha(cries) == '623fe59bb7fc0b8d3907dd67bc677a7985037e0086698636f35db82e113d7f2e' and len(cries) == 6188, 'Original audio pack differs')
     old_cues = json.loads(reuse('content/generated/synth-audio/manifest.json'))['cues']
     for prior in ['rocktunnel-synth-audio/manifest.json', 'cinnabar-synth-audio/manifest.json', 'event-island-presentation/synth-manifest.json']:
         old_cues.extend(json.loads(reuse('content/generated/' + prior))['cues'])
     cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', text('sound/cry_tables.inc').split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
     cry_rows = {struct.unpack_from('<H', cries, offset)[0]: struct.unpack_from('<HHIIB3x', cries, offset) for offset in range(12, len(cries), 16)}
     priority = int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)', text('include/constants/sound.h'), re.M)[1])
     need(priority == 10, 'Normal cry priority differs')
     cues, inherited_cues, cry_evidence = [], [], []
     for name, species in [*SPECIES, ('Electrode', 101)]:
         need(re.search(r'^#define SPECIES_' + name.upper() + r'\s+' + str(species) + r'\s*$', constants, re.M), 'Cry species ID differs')
         row = cry_rows[species]
         need(cry_table[row[1]] == name and row[1] == species - 1, 'Species cry table differs')
         tone = 0x0848c914 + row[1] * 12 - 0x08489968
         wave_offset = struct.unpack_from('<I', pack, tone + 4)[0] - 0x08489968
         need(struct.unpack_from('<HHIII', pack, wave_offset) == (1, 0, 10764288, 0, row[2]), 'Packed cry header differs')
         path = 'sound/direct_sound_samples/cries/' + name.lower() + '.wav'
         with wave.open(io.BytesIO(read(path))) as original:
             need((original.getnchannels(), original.getsampwidth(), original.getframerate(), original.getnframes(), original.getcomptype()) ==
                  (1, 1, 10512, row[2] + 1, 'NONE'), 'Source cry sample/header differs')
             pcm = original.readframes(original.getnframes())
         delta = (0, 1, 4, 9, 16, 25, 36, 49, -64, -49, -36, -25, -16, -9, -4, -1)
         decoded = bytearray()
         for index in range(row[2] + row[4]):
             block, i = divmod(index, 64)
             start = wave_offset + 16 + block * 33
             if i == 0:
                 value = pack[start]
             else:
                 packed = pack[start + (1 if i == 1 else i // 2 + 1)]
                 nibble = packed & 15 if i == 1 or i % 2 else packed >> 4
                 value = (value + delta[nibble]) & 255
             decoded.append((value + 128) & 255)
         need(decoded == pcm[:len(decoded)], 'Cry pack/source PCM differs: ' + name)
         cry_evidence.append(dict(species=species, cryIndex=row[1], sampleCount=row[2], payloadBytes=row[3], guardStored=bool(row[4]),
             decodedBytes=len(decoded), decodedSha256=sha(decoded), sourcePath=path, sourcePlayer=4, priority=priority))
         cue = dict(id='CRY_' + name.upper() + '_NORMAL', sourceCommit=COMMIT, sourceSymbol='SPECIES_' + name.upper(),
             sourcePath=path, sourcePlayer=4, priority=priority, sourceCounter=None, songAddress=0, species=species, looping=False, cryMode=0)
         if species in (101, 135):
             old = next(c for c in old_cues if c['id'] == cue['id'])
             need(dict(old, cryMode=old.get('cryMode', 0)) == cue, 'Inherited cry cue differs')
             inherited_cues.append(cue)
         else:
             need(not any(c['id'] == cue['id'] for c in old_cues), 'New cry already admitted')
             cues.append(cue)
         if species in (101, 145):
             cues.append(dict(cue, id='CRY_' + name.upper() + '_ENCOUNTER', cryMode=2))
     sound = text('src/sound.c')
     need(re.search(r'case CRY_MODE_ENCOUNTER:\s*release = 225;\s*pitch = 15600;\s*chorus = 20;\s*volume = 90;\s*break;', sound), 'Encounter cry parameters differ')


     need(re.search(r'#define CRY_MODE_ENCOUNTER\s+2', text('include/constants/sound.h')) and
          re.search(r'length = 140;\s*reverse = FALSE;\s*release = 0;\s*pitch = 15360;\s*chorus = 0;', sound),
          'Encounter cry default length/mode differs')
     script_path = 'data/maps/PowerPlant/scripts.inc'
     script = text(script_path)
     zapdos = script.split('PowerPlant_EventScript_Zapdos::', 1)[1].split('\nPowerPlant_EventScript_DefeatedZapdos::', 1)[0]
     need('setwildbattle SPECIES_ZAPDOS, 50' in zapdos and 'playmoncry SPECIES_ZAPDOS, CRY_MODE_ENCOUNTER' in zapdos and
          'playbgm MUS_ENCOUNTER_GYM_LEADER, 0' in zapdos and 'special StartLegendaryBattle' in zapdos,
          'Zapdos encounter presentation source differs')
     for number in (1, 2):
         electrode = script.split(f'PowerPlant_EventScript_Electrode{number}::', 1)[1].split(f'\nPowerPlant_EventScript_FoughtElectrode{number}::', 1)[0]
         need('setwildbattle SPECIES_ELECTRODE, 34' in electrode and 'playmoncry SPECIES_ELECTRODE, CRY_MODE_ENCOUNTER' in electrode and
              '\n\tdowildbattle\n' in electrode and 'StartLegendaryBattle' not in electrode, 'Electrode encounter presentation source differs')
     need(json.loads(read('data/maps/PowerPlant/map.json'))['music'] == 'MUS_POKE_MANSION', 'Power Plant map music differs')
     setup_path = 'src/battle_setup.c'
     setup = text(setup_path)
     legendary = setup.split('void StartLegendaryBattle(void)', 1)[1].split('void StartGroudonKyogreBattle(void)', 1)[0]
     need(re.search(r'case SPECIES_ZAPDOS:\s*case SPECIES_HO_OH:\s*case SPECIES_LUGIA:\s*CreateBattleStartTask\(B_TRANSITION_BLUR, MUS_VS_LEGEND\);', legendary),
          'Zapdos source battle music binding differs')
     songs = text('include/constants/songs.h')
     song_table = re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)', text('sound/song_table.inc'), re.M)
     for symbol in ['MUS_POKE_MANSION', 'MUS_ENCOUNTER_GYM_LEADER', 'MUS_VS_WILD', 'MUS_VS_LEGEND']:
         song_id = int(re.search(r'^#define ' + symbol + r'\s+(\d+)', songs, re.M)[1])
         address, player, second = struct.unpack_from('<IHH', pack, 0x084A32CC + song_id * 8 - 0x08489968)
         need(song_table[song_id] == (symbol.lower(), str(player), str(second)) and player == 0, 'Inherited song table differs')
         path = 'sound/songs/midi/' + symbol.lower() + '.mid'
         midi = read(path)
         need(midi[:4] == b'MThd' and b'\xff\x06\x01[' in midi and b'\xff\x06\x01]' in midi, 'Inherited source music loop differs')
         cue = dict(id=symbol, sourceCommit=COMMIT, sourceSymbol=symbol, sourcePath=path, sourcePlayer=player,
                    priority=pack[address - 0x08489968 + 2], sourceCounter=None, songAddress=address, species=0, looping=True, cryMode=0)
         old = next(c for c in old_cues if c['id'] == symbol)
         need(dict(old, cryMode=old.get('cryMode', 0)) == cue, 'Inherited music cue differs')
         inherited_cues.append(cue)
     need(len(cues) == 4 and len(inherited_cues) == 6, 'Power Plant audio cue inventory differs')

     geometry_path = 'content/generated/campaign-hit-art/manifest.json'
     geometry = json.loads(reuse(geometry_path))['geometry']
     geometry_rows = []
     for row in sprites:
         if row['paletteVariant'] != 'normal':
             continue
         expected = dict(species=row['sourceSpeciesId'], side=row['side'], sourceWidth=row['sourceWidth'], sourceHeight=row['sourceHeight'],
                         yOffset=row['sourceYOffset'], elevation=row['sourceEnemyElevation'] if row['side'] == 'front' else 0)
         need(expected in geometry, 'Inherited battle geometry differs')
         geometry_rows.append(expected)

     # Bag icons keep the earlier item importers' replicated-RGB5 palette convention.
     item_icons = []
     item_table, item_graphics = text('src/data/item_icon_table.h'), text('src/data/graphics/items.h')
     for symbol, item_id, inherited_png in [
         ('ITEM_THUNDER_STONE', 96, None),
         ('ITEM_TM17', 305, 'content/generated/mtmoon-items/item_tm05.png'),
         ('ITEM_TM25', 313, 'content/generated/vermilion-gym-assets/item_tm34.png')]:
         need(re.search(r'^#define ' + symbol + r'\s+' + str(item_id) + r'\s*(?://[^\n]*)?$', text('include/constants/items.h'), re.M), 'Item ID differs')
         binding = re.search(r'^    \[' + symbol + r'\][ \t]*= \{(\w+), (\w+)\},', item_table, re.M)
         need(binding, 'Item icon binding missing')
         paths = []
         for sym in binding.groups():
             definition = re.search(r'^const u32 ' + sym + r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);', item_graphics, re.M)
             need(definition, 'Item graphics source missing')
             paths.append(definition[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
         image, pal = paths
         _, rgba = indexed(image, pal, (24, 24), floor=False)
         if inherited_png:
             png_bytes = reuse(inherited_png)
             with Image.open(io.BytesIO(png_bytes)) as im:
                 need(im.size == (24, 24) and im.convert('RGBA').tobytes() == rgba, 'Inherited TM icon differs')
             png_path = inherited_png
         else:
             emitted = emit('items/' + symbol.lower(), 24, 24, rgba, paths)
             png_path = emitted['texturePath']
             png_bytes = out['items/' + symbol.lower() + '.png']
         item_icons.append(dict(item=dict(value='pret:' + symbol), sourceItemId=item_id, width=24, height=24,
             rgbaBase64=base64.b64encode(rgba).decode(), rgbaSha256=sha(rgba), pngPath=png_path, pngSha256=sha(png_bytes),
             source=ref(image, binding[1]), paletteSource=ref(pal, binding[2]), bindingSource=ref('src/data/item_icon_table.h', symbol, binding),
             inherited=bool(inherited_png)))

     if SOURCE_PINS:
         need(set(inputs) == set(SOURCE_PINS), 'Source input inventory differs')
     if DEPENDENCY_PINS:
         need(set(inherited) == set(DEPENDENCY_PINS), 'Inherited input inventory differs')
     out['synth-manifest.json'] = encode(dict(schemaVersion=1, sourceCommit=COMMIT, cues=cues, inheritedCues=inherited_cues))
     lock = dict(schemaVersion=1, profile=PROFILE, repository='pret/pokefirered', commit=COMMIT, tree=TREE,
                 files=[inputs[p] for p in sorted(inputs)], inheritedInputs=[inherited[p] for p in sorted(inherited)])
     lock_bytes = encode(lock)
     manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, sourceTree=TREE, lockSha256=sha(lock_bytes),
         inputs=lock['files'], inheritedInputs=lock['inheritedInputs'], assets=assets, sprites=sprites, pokemonIcons=icons,
         footprints=footprints, speciesNames=names, newOwnedSpecies=[s for _, s in SPECIES], itemIcons=item_icons,
         moveAnimationGeometry=dict(inheritedManifest=geometry_path, rows=geometry_rows),
         audio=dict(additionalCueIds=[c['id'] for c in cues], inheritedCueIds=[c['id'] for c in inherited_cues], cries=cry_evidence,
                    originalPackUnchanged=True, newAudioAssets=0, recordingsWritten=False, physicalAudioVerified=False,
                    encounter=dict(sourcePath=script_path, battleMusicSourcePath=setup_path,
                        cryMode=2, cryParameters=dict(length=140, release=225, pitch=15600, chorus=20, volume=90),
                        zapdos=dict(species=145, level=50, cry='CRY_ZAPDOS_ENCOUNTER', introductionMusic='MUS_ENCOUNTER_GYM_LEADER', battleMusic='MUS_VS_LEGEND'),
                        electrodes=dict(species=101, level=34, count=2, cry='CRY_ELECTRODE_ENCOUNTER', battleMusic='MUS_VS_WILD'),
                        mapMusic='MUS_POKE_MANSION', playbackAuthority='existing committed presentation effects and typed battle context')),
         outputs=[dict(path=p, byteLength=len(b), sha256=sha(b)) for p, b in sorted(out.items())])
     manifest_bytes = encode(manifest)
     out['manifest.json'] = manifest_bytes
     files = {BASE + p: b for p, b in out.items()}
     files.update({RAW + p: b for p, b in raw.items()})
     files['scripts/power-plant-presentation.lock.json'] = lock_bytes
     report = dict(passed=True, sourceFiles=len(raw), inheritedInputs=len(inherited), assets=len(assets), sprites=len(sprites),
         icons=len(icons), footprints=len(footprints), itemIcons=len(item_icons), inheritedItemIcons=2,
         geometry=len(geometry_rows), outputs=len(out) - 1, lockSha256=sha(lock_bytes), lockBytes=len(lock_bytes),
         manifestSha256=sha(manifest_bytes), manifestBytes=len(manifest_bytes), synthSha256=sha(out['synth-manifest.json']),
         synthBytes=len(out['synth-manifest.json']), newCues=len(cues), inheritedCues=len(inherited_cues),
         exactCryPcmCompared=True, nativeWindowVerified=False, physicalAudioVerified=False)
     return files, dict(manifest=manifest, sourceLock=lock, report=report, itemIcons=item_icons)
 return build_presentation(read_source_bytes,dependency_root)

def generate(reference,dependency):
 need(subprocess.check_output(['git','rev-parse',COMMIT+'^{tree}'],cwd=reference).decode().strip()==TREE,'Source tree')
 inputs={};raw={};dependencies=[]
 def readbytes(path):
  if path not in raw:
   b=subprocess.check_output(['git','show',COMMIT+':'+path],cwd=reference);raw[path]=b
   inputs[path]=dict(path=path,byteLength=len(b),gitBlobSha1=hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest(),sha256=sha(b))
  return raw[path]
 def read(path):return readbytes(path).decode()
 def one(path,pattern):
  m=list(re.finditer(pattern,read(path),re.M|re.S));need(len(m)==1,'Unique source: '+pattern);return m[0]
 def ref(path,symbol,m):return dict(path=path,symbol=symbol,startLine=read(path).count('\n',0,m.start())+1,endLine=read(path).count('\n',0,m.end())+1,sha256=inputs[path]['sha256'])
 def dep(path):
  b=(dependency/path).read_bytes();need(sha(b)==DEPENDENCIES[path],'Dependency: '+path)
  dependencies.append(dict(path=path,byteLength=len(b),sha256=sha(b)));return json.loads(b)
 d={p:dep(p) for p in DEPENDENCIES};canonical=d['content/generated/canonical-pokemon/catalog.json']
 species={s['id']['value'][5:]:s for s in canonical['species']};metadata={s['id']['value'][5:]:s for s in canonical['metadata']}
 five=d['content/generated/five-island-rules/items.json'];celio=d['content/generated/celio-pokemon/pokemon.json'];seven=d['content/generated/seven-island-rules/pokemon.json'];events=d['content/generated/event-island-species/catalog.json']
 owned=[r['species']['value'][5:] for r in five['tm36']['compatibility']]+celio['speciesAdded']+['SPECIES_MEWTWO']+seven['ownedSpeciesAdded']+[r['definition']['id']['value'][5:] for r in events['species']]
 opponents={s[5:] for s in d['scripts/five-island-predecessor.json']['opponentSpecies']}|set(owned)|set(d['content/generated/five-island-rules/pokemon.json']['opponentSpeciesAdded'])|{s['id']['value'][5:] for s in d['content/generated/league-rematch-rules/pokemon.json']['opponentSpecies']}|set(seven['trainerOnlySpeciesAdded'])
 need(len(owned)==len(set(owned))==149 and len(opponents)==183,'Exact Event Island predecessor')
 need(not set(SPECIES)&set(owned) and not set(SPECIES)&opponents,'Minimal additions not previously admitted')
 owned+=SPECIES;need(len(owned)==152 and len(opponents|set(SPECIES))==186,'Exact successor counts')
 for s in SPECIES:
  need(not metadata[s]['sourceEvolutions'] and not species[s]['evolutions'],'Terminal added species')
  for r in [species[s]['source'],species[s]['learnsetSource'],metadata[s]['dexSource'],metadata[s]['learnsetPointerSource']]:
   read(r['path']);need(inputs[r['path']]['sha256']==r['sha256'],'Canonical source '+s)
 edges=[]
 for s,t in [('SPECIES_PIKACHU','SPECIES_RAICHU'),('SPECIES_EEVEE','SPECIES_JOLTEON')]:
  e=[e for e in metadata[s]['sourceEvolutions'] if e['method']=='EVO_ITEM' and e['parameter']=='ITEM_THUNDER_STONE']
  need(len(e)==1 and e[0]['parameterValue']==96 and e[0]['target']==ident(t),'Exact Thunder Stone edge');edges.append(dict(species=ident(s),sourceEvolution=e[0]));read(e[0]['source']['path'])
 wildpath='src/data/wild_encounters.json';wild=json.loads(read(wildpath));entries=[e for g in wild['wild_encounter_groups'] for e in g.get('encounters',[]) if e['map']=='MAP_POWER_PLANT' and e['base_label'].endswith('_FireRed')]
 need(len(entries)==1 and set(entries[0])=={'map','base_label','land_mons'},'Only original FireRed land encounters')
 w=entries[0];weights=next(f['encounter_rates'] for g in wild['wild_encounter_groups'] for f in g.get('fields',[]) if f['type']=='land_mons')
 need(weights==[20,20,10,10,10,10,5,5,4,4,1,1] and w['land_mons']['encounter_rate']==7,'Original encounter weights/rate')
 land=dict(map=dict(value='map.power-plant'),sourceCommit=COMMIT,sourcePath=wildpath,sourceSymbol=w['base_label'],sourceSha256=inputs[wildpath]['sha256'],rawEncounterRate=7,slots=[dict(index=i,species=ident(m['species']),minimumLevel=m['min_level'],maximumLevel=m['max_level'],weight=weights[i]) for i,m in enumerate(w['land_mons']['mons'])])
 map_path='data/maps/PowerPlant/map.json';mapping=json.loads(read(map_path));scriptpath='data/maps/PowerPlant/scripts.inc';scripts=read(scriptpath);statics=[]
 for local,obj in enumerate(mapping['object_events'],1):
  block=re.search(r'^'+obj['script']+r'::\n((?:(?!^\w+::).)*)',scripts,re.M|re.S)
  if not block:continue
  m=re.search(r'^\tsetwildbattle (SPECIES_\w+), (\d+)\n',block[1],re.M)
  if m:
   kind=obj['script'].split('_')[-1];statics.append(dict(kind=kind[0].lower()+kind[1:],localId=local,species=ident(m[1]),level=int(m[2]),heldItem='ITEM_NONE',modernFatefulEncounter=False,legendary=kind=='Zapdos',source=ref(scriptpath,obj['script'],block)))
 need([(s['kind'],s['localId'],s['level']) for s in statics]==[('zapdos',6,50),('electrode2',7,34),('electrode1',8,34)],'Exactly three source static objects')
 for p in ['asm/macros/event.inc','src/pokemon.c','src/script_pokemon_util.c','src/scrcmd.c','src/battle_setup.c','src/battle_main.c','include/random.h','include/constants/pokemon.h','include/constants/species.h','include/constants/items.h','src/data/pokemon/tmhm_learnsets.h','src/data/items.json','src/data/pokemon/item_effects.h','include/constants/item_effects.h','src/move_descriptions.c']:read(p)
 need('CreateMon(&gEnemyParty[0], species, level, 32, 0, 0, OT_ID_PLAYER_ID, 0);' in read('src/script_pokemon_util.c'),'Source scripted generation')
 held=[]
 for s in sorted({r['species']['value'][5:] for r in land['slots']}|{'SPECIES_ELECTRODE','SPECIES_ZAPDOS'}):
  fields=metadata[s]['originalFields'];read(metadata[s]['source']['path']) if 'source' in metadata[s] else read(species[s]['source']['path'])
  held.append(dict(species=ident(s),common=fields['itemCommon'],rare=fields['itemRare']))
 need([(r['species']['value'],r['rare']) for r in held if r['rare']!='ITEM_NONE']==[('pret:SPECIES_MAGNETON','ITEM_MAGNET')],'FireRed held-item tables')
 old=d['content/generated/seven-island-rules/machines.json'];machineids=sorted(r['item']['value'][5:] for r in old['additions']);need(len(machineids)==39,'39 inherited machines')
 tm='src/data/pokemon/tmhm_learnsets.h'
 def cell(s,item):
  m=one(tm,r'^    \['+s+r'\]\s*=\s*TMHM_LEARNSET\((.*?)\),');return dict(species=ident(s),sourceSpeciesId=species[s]['sourceSpeciesId'],canLearn=bool(re.search(r'TMHM\('+item[5:]+r'_',m[1])),source=ref(tm,s,m))
 cells=[dict(item=ident(item),compatibility=[cell(s,item) for s in SPECIES]) for item in machineids]
 itemtext=read('src/data/items.json');sourceitems=json.loads(itemtext)['items'];constants={m[1]:int(m[2],0) for m in re.finditer(r'^#define\s+(ITEM_\w+)\s+(0x[0-9A-Fa-f]+|\d+)\b',read('include/constants/items.h'),re.M)}
 def item(symbol):
  i=next(i for i in sourceitems if i['itemId']==symbol);m=one('src/data/items.json',r'"itemId":\s*"'+symbol+r'"');r=ref('src/data/items.json',symbol,m)
  meta=dict(id=ident(symbol),sourceItemSymbol=symbol,sourceItemId=constants[symbol],name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['importance']==0,canGive=i['importance']==0,source=r)
  description=i['description_english'].replace('\\n','\n');descriptionref=r
  if symbol.startswith('ITEM_TM'):
   p='src/move_descriptions.c';m=one(p,r'^const u8 gMoveDescription_'+i['moveId']+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$');description=json.loads(m[1]);descriptionref=ref(p,'gMoveDescription_'+i['moveId'],m)
  return dict(item=meta,description=description,declarationDescription=i['description_english'],descriptionSource=descriptionref)
 items=[]
 for symbol in ['ITEM_THUNDER_STONE','ITEM_TM17','ITEM_TM25']:
  wrapped=item(symbol);meta=wrapped['item'];stone=symbol=='ITEM_THUNDER_STONE';effect=None
  if stone:
   p='src/data/pokemon/item_effects.h';m=one(p,r'^static const u8 sItemEffect_ThunderStone\[6\] = \{\n    \[4\] = ITEM4_EVO_STONE,\n\};');effect=ref(p,'sItemEffect_ThunderStone',m)
   need('#define ITEM4_EVO_STONE         0x80' in read('include/constants/item_effects.h'),'Stone effect byte')
  items.append({k:meta[k] for k in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}|dict(description=wrapped['description'],healHp=0,cureStatus=0,effectBytes=[0,0,0,0,128,0] if stone else [],useSupport=9 if stone else 10,effectSource=effect,sourceMetadata=meta))
 newmachines=[];moveconstants=read('include/constants/moves.h');movedata=read('src/data/battle_moves.h')
 for symbol,move in [('ITEM_TM17','MOVE_PROTECT'),('ITEM_TM25','MOVE_THUNDER')]:
  i=next(i for i in sourceitems if i['itemId']==symbol);need('MOVE_'+i['moveId'].upper()==move,'Source machine move');moveid=int(re.search(r'^#define '+move+r'\s+(\d+)',moveconstants,re.M)[1]);moveblock=re.search(r'\['+move+r'\] =\s*\{(.*?)\}',movedata,re.S)[1];movepp=int(re.search(r'\.pp = (\d+)',moveblock)[1])
  newmachines.append(dict(machine=item(symbol),move=ident(move),sourceMoveId=moveid,movePp=movepp,compatibility=[cell(s,symbol) for s in owned]))
 flags=read('include/constants/flags.h');ballscripts=read('data/scripts/item_ball_scripts.inc');pickups=[]
 def flagvalue(symbol):
  m=re.search(r'^#define\s+'+symbol+r'\s+([^\n]+)',flags,re.M);need(m,'Flag exists');expr=m[1].split('//')[0].strip()
  if re.fullmatch(r'0x[0-9A-Fa-f]+|\d+',expr):return int(expr,0)
  a=re.fullmatch(r'\((FLAG_\w+)\s+\+\s+(\d+)\)',expr);need(a,'Flag expression');return flagvalue(a[1])+int(a[2])
 def pickup(obj,hidden,local,symbol,quantity):
  i=item(symbol)['item'];pickups.append(dict(id='power-plant:'+obj['flag'],mapSymbol='MAP_POWER_PLANT',hidden=hidden,sourceLocalId=local,x=obj['x'],y=obj['y'],elevation=obj['elevation'],script='EventScript_HiddenItemScript' if hidden else obj['script'],itemSymbol=symbol,sourceItemId=i['sourceItemId'],item=i['id'],itemName=i['name'],pocket=i['pocket'],quantity=quantity,flag=obj['flag'],sourceFlagId=flagvalue(obj['flag'])))
 for local,obj in enumerate(mapping['object_events'],1):
  m=re.search(r'^'+obj['script']+r'::\n\tfinditem (ITEM_\w+)(?:, (\d+))?\n',ballscripts,re.M)
  if m:pickup(obj,False,local,m[1],int(m[2] or 1))
 for obj in mapping['bg_events']:
  need(obj['type']=='hidden_item' and not obj['underfoot'],'Only ordinary hidden pickups');pickup(obj,True,None,obj['item'],obj['quantity'])
 need(len(pickups)==7 and sum(p['hidden'] for p in pickups)==2,'Five visible and two hidden pickups')
 vectors=[]
 for kind,s,level,seed in [('zapdos','SPECIES_ZAPDOS',50,42),('electrode1','SPECIES_ELECTRODE',34,0x12345678),('electrode2','SPECIES_ELECTRODE',34,0xDEADBEEF)]:
  initial=seed;draws=[]
  for _ in range(4):seed=(seed*1103515245+24691)&0xffffffff;draws.append(seed>>16)
  pid=draws[0]|draws[1]<<16;ivs=[draws[2]&31,(draws[2]>>5)&31,(draws[2]>>10)&31,draws[3]&31,(draws[3]>>5)&31,(draws[3]>>10)&31]
  vectors.append(dict(kind=kind,species=ident(s),level=level,initialSeed=initial,finalSeed=seed,rawDraws=draws,personality=pid,nature=pid%25,ivs=ivs,originalTrainerId=0x12345678))
 doc=dict(sourceCommit=COMMIT,sourceTree=TREE,profile='firered-power-plant-pokemon-v1',ownedSpeciesCount=152,opponentSpeciesCount=186,itemCount=123,machineCount=41,species=[dict(definition=species[s],metadata=metadata[s]) for s in SPECIES],thunderStoneEvolutions=edges,land=land,heldItems=held,staticEncounters=statics,machines=cells,machinesSemanticSha256=fingerprint(cells),newMachines=newmachines,newMachinesSemanticSha256=fingerprint(newmachines),items=items,itemsSemanticSha256=fingerprint(items),pickups=pickups,pickupsSemanticSha256=fingerprint(pickups),generationVectors=vectors,generationOrder='Static: player OT, random PID then IVs, explicit low16/high16 profile; original target-compiler Random32 operand ordering unverified. Ordinary land uses existing nature rejection generation. Legendary Zapdos skips held draw; ordinary land and Electrode consume one held draw at battle entry. No fateful metadata.',inputs=[inputs[p] for p in sorted(inputs)],dependencyInputs=dependencies)
 slots=',\n            '.join(f'new({r["index"]},new("{r["species"]["value"]}"),{r["minimumLevel"]},{r["maximumLevel"]},{r["weight"]})' for r in land['slots'])
 heldrows=',\n        '.join(f'new(new("{r["species"]["value"]}"),"{r["common"]}","{r["rare"]}")' for r in held)
 code='''using System.Collections.Immutable;\nusing Game.Domain.Ids;\nnamespace Game.Domain.Encounters;\n\npublic sealed record PowerPlantHeldItemSource(SpeciesId Species, string Common, string Rare);\n/// <summary>Exact FireRed Power Plant table, generated only from pinned source.</summary>\npublic static class PowerPlantEncounterTables\n{\n'''+f'''    public const string SourceSha256 = "{land['sourceSha256']}";\n    public static MapId Map {{ get; }} = new("map.power-plant");\n    public static EarlyLandEncounterTable Land {{ get; }} = new(Map, EarlyLandEncounterRules.SourceCommit,\n        EarlyLandEncounterRules.SourcePath, "sPowerPlant_FireRed", SourceSha256, 7,\n        [{slots}]);\n    public static ImmutableArray<PowerPlantHeldItemSource> HeldItems {{ get; }} = [\n        {heldrows}];\n}}\n'''
 return doc,raw,{'src/Game.Domain/Encounters/PowerPlantEncounterTables.cs':code.encode()}
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('reference',type=Path);p.add_argument('output',type=Path);p.add_argument('--dependency-root',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args()
 doc,raw,files=generate(a.reference,a.dependency_root);files[BASE+'catalog.json']=encoded(doc)
 presentation,art=build_power_plant_presentation(lambda path:subprocess.check_output(['git','show',COMMIT+':'+path],cwd=a.reference),a.dependency_root);files.update(presentation)
 for path,b in raw.items():files[RAW+path]=b
 lock=dict(sourceCommit=COMMIT,sourceTree=TREE,profile=doc['profile'],files=[dict(path=p,byteLength=len(b),sha256=sha(b)) for p,b in sorted(files.items())]);files['scripts/power-plant-pokemon.lock.json']=encoded(lock)
 for path,b in files.items():
  dst=a.output/path
  if a.check:need(dst.read_bytes()==b,'Regeneration differs: '+path)
  else:dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes(b)
 print(json.dumps(dict(checked=a.check,catalogSha256=sha(files[BASE+'catalog.json']),lockSha256=sha(files['scripts/power-plant-pokemon.lock.json']),machineFingerprint=doc['machinesSemanticSha256'],newMachineFingerprint=doc['newMachinesSemanticSha256'],itemFingerprint=doc['itemsSemanticSha256'],pickupsFingerprint=doc['pickupsSemanticSha256'],inputs=len(raw),presentation=art['report']),indent=2))
if __name__=='__main__':main()
