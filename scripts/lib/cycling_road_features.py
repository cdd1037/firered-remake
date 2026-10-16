"""Initial Cycling Road battles and land encounters compiled only from raw source."""
import re
from trainer_import import selected_trainers

ROUTES = ('Route16', 'Route17', 'Route18')


def require(value, message):
    if not value:
        raise ValueError(message)


def trainer_battles(src):
    """Select initial singles by actual map actors, never rematches or the Route16 pair."""
    path = 'data/scripts/trainers.inc'
    text = src.text(path)
    trainers, battles = [], []
    for route in ROUTES:
        for actor in src.json(f'data/maps/{route}/map.json')['object_events']:
            if actor['trainer_type'] != 'TRAINER_TYPE_NORMAL':
                continue
            label = actor['script']
            match = re.search(r'^' + re.escape(label) + r'::\n(.*?)(?=^\w+::|\Z)', text, re.M | re.S)
            require(match is not None, 'Missing trainer script: ' + label)
            body = match[1]
            if re.search(r'^\s*trainerbattle_double\b', body, re.M):
                continue
            single = re.search(r'^\s*trainerbattle_single (TRAINER_\w+), (\w+), (\w+)\s*$', body, re.M)
            post = re.search(r'^\s*msgbox (\w+), MSGBOX_AUTOCLOSE\s*$', body, re.M)
            require(single is not None and post is not None, 'Unsupported initial trainer script: ' + label)
            symbol, intro, defeat = single.groups()
            identity = 'pret:' + symbol
            require(identity not in trainers, 'Duplicate initial trainer: ' + identity)
            trainers.append(identity)
            # These spans identify executable battle source bindings, not asset proofs.
            source = dict(path=path, symbol=label, startLine=text.count('\n', 0, match.start()) + 1,
                          endLine=text.count('\n', 0, match.start(1) + len(body.rstrip())) + 1)
            battles.append(dict(trainer=dict(value=identity), scriptEntry=label, mode=0,
                                introText=intro, defeatText=defeat, victoryText='', postBattleText=post[1],
                                battleTypeFlags=8, entryScript='EventScript_TryDoNormalTrainerBattle',
                                callback='CB2_EndTrainerBattle', rivalFlags=0, musicOption=None,
                                postBattleScript=None, source=source))
    require(bool(battles), 'No Cycling Road trainer battles found')
    # Validate every selected identity against the source trainer and party declarations.
    selected_trainers(src.text, [identity.removeprefix('pret:') for identity in trainers])
    return dict(trainers=trainers, battles=battles)


def land_encounters(src):
    wild = src.json('src/data/wild_encounters.json')
    tables = []
    for number in (17, 18):
        selected = [(group, row) for group in wild['wild_encounter_groups']
                    if group.get('for_maps') is True
                    for row in group.get('encounters', [])
                    if row.get('map') == f'MAP_ROUTE{number}'
                    and row.get('base_label') == f'sRoute{number}_FireRed']
        require(len(selected) == 1, f'Expected one Route{number} FireRed encounter table')
        group, row = selected[0]
        fields = [field for field in group['fields'] if field['type'] == 'land_mons']
        require(len(fields) == 1, 'Expected one land encounter weight table')
        weights = fields[0]['encounter_rates']
        land = row['land_mons']
        require(len(weights) == 12 and len(land['mons']) == len(weights), 'Invalid land encounter slot count')
        require(all(type(weight) is int and weight > 0 for weight in weights)
                and sum(weights) == 100, 'Invalid land encounter weights')
        rate = land['encounter_rate']
        require(type(rate) is int and 0 < rate <= 255, 'Invalid land encounter rate')
        slots = []
        for index, (mon, weight) in enumerate(zip(land['mons'], weights)):
            symbol, low, high = mon['species'], mon['min_level'], mon['max_level']
            require(re.fullmatch(r'SPECIES_[A-Z0-9_]+', symbol), 'Invalid encounter species: ' + symbol)
            require(type(low) is int and type(high) is int and 1 <= low <= high <= 100,
                    'Invalid encounter level range')
            slots.append(dict(index=index, species=dict(value='pret:' + symbol),
                              minimumLevel=low, maximumLevel=high, weight=weight))
        tables.append(dict(map=dict(value=f'map.route-{number}'), rawEncounterRate=rate, slots=slots))
    return tables


def compile_cycling_road(src):
    return {'trainers.json': trainer_battles(src), 'land-encounters.json': land_encounters(src)}
