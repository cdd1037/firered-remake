"""Source-script dependency closure shared by field-item service importers."""
import re


_IMPLICIT = {
    'giveitem': 'Std_ObtainItem', 'giveitem_msg': 'Std_ReceivedItem',
    'msgreceiveditem': 'Std_ReceivedItem', 'putitemaway': 'Std_PutItemAway',
    'finditem': 'Std_FindItem',
}
_MESSAGES = {
    'MSGBOX_DEFAULT': 'Std_MsgboxDefault', 'MSGBOX_YESNO': 'Std_MsgboxYesNo',
    'MSGBOX_SIGN': 'Std_MsgboxSign', 'MSGBOX_NPC': 'Std_MsgboxNPC',
    'MSGBOX_AUTOCLOSE': 'Std_MsgboxAutoclose',
}


def implicit_script_targets(code):
    """Expand source macros whose shared script targets are absent from operands."""
    code = code.split('@')[0].strip()
    if not code:
        return []
    opcode = code.split()[0]
    if opcode in _IMPLICIT:
        return [_IMPLICIT[opcode]]
    if opcode == 'msgbox':
        return [_MESSAGES[code.split(',')[-1].strip()] if ',' in code else 'Std_MsgboxDefault']
    return []


def service_script_graph(inventory, root):
    """Resolve calls, branches, movement/text labels and standard macro programs.

    The inventory is parsed source, never executable upstream code. A missing
    implicit standard program is an incomplete source inventory and fails closed.
    """
    blocks = {block['label']: block for block in inventory['blocks']}
    aliases = inventory['aliases']
    pending = [root]
    selected = set()
    items = set()
    while pending:
        label = pending.pop()
        label = aliases.get(label, label)
        if label in selected:
            continue
        if label not in blocks:
            raise ValueError('Missing field-item service call target ' + label)
        selected.add(label)
        for row in blocks[label]['lines']:
            code = row['text'].split('@')[0].strip()
            pending.extend(implicit_script_targets(code))
            code = re.sub(r'"[^"\n]*"', '', code)
            tokens = re.findall(r'\b\w+\b', code)
            items.update(token for token in tokens if token.startswith('ITEM_') and token != 'ITEM_NONE')
            pending.extend(aliases.get(token, token) for token in tokens
                           if aliases.get(token, token) in blocks and aliases.get(token, token) not in selected)
    programs = sorted(label for label in selected if '_Movement_' not in label
                      and not any(row['text'].strip().startswith('.string') for row in blocks[label]['lines']))
    return dict(programIds=programs, itemSymbols=sorted(items),
                otherReachableLabels=sorted(selected - set(programs)))
