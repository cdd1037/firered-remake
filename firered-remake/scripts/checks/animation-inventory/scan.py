#!/usr/bin/env python3
"""Read pinned source and current runtime; emit a bounded lexical dependency inventory.

This is not a C compiler, animation executor, asset importer, or visual acceptance test.
Only this author-written scanner and the project's author-written script parser execute.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import collections
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

sys.dont_write_bytecode = True
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
KEYWORDS = {"if", "for", "while", "switch", "sizeof", "return"}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def mask(text):
    """Replace comments/strings with spaces while retaining exact offsets/newlines."""
    return re.sub(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'',
                  lambda m: re.sub(r"[^\n]", " ", m[0]), text, flags=re.S)


def close_brace(text, start):
    depth = 1
    for i in range(start + 1, len(text)):
        depth += (text[i] == "{") - (text[i] == "}")
        if depth == 0:
            return i
    raise ValueError("Unbalanced source braces")


def run(repo, reference):
    parser_path = repo / "scripts/imports/battle/import-battle-animation-catalog.py"
    spec = importlib.util.spec_from_file_location("local_catalog", parser_path)
    catalog_module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(catalog_module)
    catalog = catalog_module.inventory(reference)
    assert catalog["sourceCommit"] == COMMIT and len(catalog["moves"]) == 354

    def git(*args):
        return subprocess.check_output(["git", "-C", str(reference), *args])

    # Read immutable Git objects, not the working tree, so generated build files cannot
    # silently broaden this inventory. No upstream executable or script is run.
    paths = git("ls-tree", "-r", "--name-only", COMMIT, "src").decode().splitlines()
    paths = [p for p in paths if re.fullmatch(r"src/battle_anim[^/]*\.c", p)]
    paths += ["src/sprite.c", "src/task.c"]
    raw = {p: git("show", COMMIT + ":" + p) for p in sorted(paths)}
    texts = {p: b.decode() for p, b in raw.items()}
    masks = {p: mask(t) for p, t in texts.items()}
    inputs = [{"path": p, "bytes": len(b), "sha256": sha(b),
               "gitBlobSha1": hashlib.sha1(f"blob {len(b)}\0".encode() + b).hexdigest()}
              for p, b in raw.items()]

    functions, templates = {}, {}
    # Pinned decompilation definitions use ordinary, non-macro function signatures.
    signature = re.compile(r"^[ \t]*(?P<signature>(?:(?:static|inline|const)\s+)*"
                           r"(?:void|bool8|bool16|bool32|s8|s16|s32|u8|u16|u32|int|unsigned|struct\s+\w+)"
                           r"[ \t*]+(?P<name>\w+)\s*\([^;{}]*?\))\s*\{", re.M)
    for path, source in masks.items():
        for match in signature.finditer(source):
            end = close_brace(source, match.end() - 1)
            name = match["name"]
            key = path + "::" + name
            if key in functions:
                raise ValueError("Duplicate function definition " + key)
            functions[key] = {"name": name, "path": path,
                "line": source.count("\n", 0, match.start()) + 1,
                "static": bool(re.match(r"static\b", match["signature"])),
                "body": source[match.end():end], "bodyOffset": match.end(),
                "parameters": match["signature"].split("(", 1)[1].rsplit(")", 1)[0],
                "sha256": sha(texts[path][match.start():end + 1].encode())}
        for match in re.finditer(r"(?:static\s+)?const\s+struct\s+SpriteTemplate\s+(\w+)\s*=\s*\{", source):
            end = close_brace(source, match.end() - 1)
            body = source[match.end():end]
            name = match[1]
            if name in templates:
                raise ValueError("Duplicate template name " + name)
            fields = dict(re.findall(r"\.(\w+)\s*=\s*([^,]+)", body))
            templates[name] = {"name": name, "path": path,
                "line": source.count("\n", 0, match.start()) + 1,
                "fields": {k: v.strip() for k, v in fields.items()}}

    by_name = collections.defaultdict(list)
    for key, fn in functions.items():
        by_name[fn["name"]].append(key)

    def resolve(name, path):
        local = path + "::" + name
        if local in functions:
            return local
        choices = [k for k in by_name[name] if not functions[k]["static"]]
        if len(choices) > 1:
            raise ValueError("Ambiguous external function " + name)
        return choices[0] if choices else None

    for name, template in templates.items():
        callback = template["fields"].get("callback", "")
        template["callbackFunction"] = resolve(callback, template["path"])

    macros = set()
    for source in masks.values():
        macros.update(re.findall(r"^\s*#define\s+(\w+)", source, re.M))
    for key, fn in functions.items():
        body, path = fn["body"], fn["path"]
        tokens = set(re.findall(r"\b\w+\b", body))
        # A member named callback/func is an indirect call, never resolve by a
        # coincidentally named global function. Named pointer values are separate.
        call_tokens = set(re.findall(r"(?<![.>\w])\b([A-Za-z_]\w*)\s*\(", body)) - KEYWORDS
        direct = {resolve(n, path) for n in call_tokens} - {None}
        refs = {resolve(n, path) for n in tokens & set(by_name)} - {None}
        fn["directCalls"] = sorted(direct)
        fn["namedFunctionReferences"] = sorted(refs - direct)
        fn["templateReferences"] = sorted(tokens & set(templates))
        fn["graphicTagSymbols"] = sorted(t for t in tokens if t.startswith("ANIM_TAG_"))
        fn["soundSymbols"] = sorted(t for t in tokens if t.startswith("SE_"))
        fn["outsideScopeCalls"] = sorted(n for n in call_tokens if not resolve(n, path))
        fn["knownLocalMacroCalls"] = sorted(call_tokens & macros)
        fn["conditionalTokens"] = len(re.findall(r"\b(?:if|switch)\s*\(|\?", body))
        fn["preprocessorDirectives"] = [x.strip() for x in re.findall(r"^\s*#(?:if|elif|else|endif)[^\n]*", body, re.M)]
        sites = []
        for m in re.finditer(r"(?:->|\.)\s*(?:callback|func)\s*\(|\b\w+\s*\[[^;\n]*?\]\s*\(|\(\s*\*\s*\w+\s*\)\s*\(", body):
            sites.append({"line": masks[path].count("\n", 0, fn["bodyOffset"] + m.start()) + 1,
                          "expression": m[0], "kind": "indirect-call"})
        for m in re.finditer(r"(?:->|\.)\s*(?:callback|func)\s*=\s*([^;]+);", body):
            value = m[1].strip()
            if not re.fullmatch(r"\w+", value) or not resolve(value, path):
                sites.append({"line": masks[path].count("\n", 0, fn["bodyOffset"] + m.start()) + 1,
                              "expression": value, "kind": "nonliteral-callback-assignment"})
        # Stored callbacks are a dynamic invocation even when the caller's named
        # argument is captured in namedFunctionReferences. Do not invent points-to.
        if "LoadSpriteCallbackFromData6" in call_tokens:
            sites.append({"line": fn["line"], "expression": "LoadSpriteCallbackFromData6",
                          "kind": "stored-callback-recovery"})
        fn["dynamicSites"] = sites

    def closure(roots, direct_only=False):
        seen, pending = set(), list(roots)
        while pending:
            key = pending.pop()
            if key in seen:
                continue
            seen.add(key)
            fn = functions[key]
            pending += fn["directCalls"]
            if not direct_only:
                pending += fn["namedFunctionReferences"]
                pending += [templates[t]["callbackFunction"] for t in fn["templateReferences"]
                            if templates[t]["callbackFunction"]]
        return sorted(seen)

    runtime_path = "src/Game.Godot/SourceBattleAnimation.cs"
    runtime_manifest_path = "content/generated/battle/animations/hit/manifest.json"
    runtime_inputs = {p: (repo / p).read_bytes() for p in [runtime_path, runtime_manifest_path,
        "src/Game.Application/CampaignMoveAnimationDispatch.cs"]}
    runtime_text = runtime_inputs[runtime_path].decode()
    runtime_manifest = json.loads(runtime_inputs[runtime_manifest_path])
    supported_match = re.search(r"SupportedCommands=.*?new\[\]\{(.*?)\}\)", runtime_text)
    supported_ops = sorted(re.findall(r'"(\w+)"', supported_match[1]))
    registered_ids = sorted(x["moveId"] for x in runtime_manifest["scripts"])
    dispatch_text = runtime_inputs["src/Game.Application/CampaignMoveAnimationDispatch.cs"].decode()
    dispatch_ids = sorted({int(n) for n in re.findall(r'"pret:MOVE_\w+"\s*=>\s*(\d+)', dispatch_text)})
    admitted_templates = {x["template"] for x in runtime_manifest["spriteRecipes"]}
    admitted_templates |= {"gBasicHitSplatSpriteTemplate", "gHorizontalLungeSpriteTemplate"}
    admitted_tasks = {"AnimTask_ShakeMon", "AnimTask_ShakeMon2"}
    admitted_root_functions = {templates[t]["callbackFunction"] for t in admitted_templates}
    admitted_root_functions |= {resolve(t, "") for t in admitted_tasks}
    assert registered_ids == dispatch_ids == [1, 10, 15, 33, 163, 206]

    evidence_root = "evidence/recovery-20261011/basic-slice-animations/"
    prior_result = json.loads((repo / evidence_root / "RESULT.json").read_text())
    source_witness = json.loads((repo / evidence_root / "product-source-hashes.json").read_text())
    witness_status = {p: (repo / p).exists() and sha((repo / p).read_bytes()) == h for p, h in source_witness.items()}
    oracle = json.loads((repo / evidence_root / "oracle/provenance.json").read_text())
    oracle_fn_keys = {x["path"] + "::" + x["name"] for x in oracle["functions"] if x["kind"] == "function"}
    missing_oracle = sorted(oracle_fn_keys - set(functions))
    assert not missing_oracle, missing_oracle
    fn_indices = {k: i for i, k in enumerate(sorted(functions))}
    template_use, task_use, callback_use, command_use = (collections.defaultdict(list) for _ in range(4))
    entries, unresolved_roots = [], []
    script_lines = git("show", COMMIT + ":data/battle_anim_scripts.s").decode().splitlines()
    for move in catalog["moves"]:
        mid = move["sourceMoveId"]
        roots, template_callbacks = set(), {}
        for t in move["spriteTemplates"]:
            template_use[t].append(mid)
            if t not in templates or not templates[t]["callbackFunction"]:
                unresolved_roots.append({"moveId": mid, "kind": "template", "name": t})
                continue
            key = templates[t]["callbackFunction"]
            roots.add(key)
            template_callbacks[t] = key
        for name in move["visualTasks"] + move["soundTasks"]:
            task_use[name].append(mid)
            key = resolve(name, "")
            if not key:
                unresolved_roots.append({"moveId": mid, "kind": "task", "name": name})
            else:
                roots.add(key)
        for root in roots:
            callback_use[root].append(mid)
        for op in move["commands"]:
            command_use[op].append(mid)
        known_closure = closure(roots)
        referenced_templates = sorted(set(move["spriteTemplates"]) |
            {t for k in known_closure for t in functions[k]["templateReferences"]})
        branch_lines = [line for line in move["reachableInstructionLines"]
                        if re.match(r"\s*(?:jump\w*|choosetwoturnanim)\b", script_lines[line - 1])]
        dynamic = [fn_indices[k] for k in known_closure if functions[k]["dynamicSites"]]
        entries.append({"id": mid, "move": move["move"], "script": move["script"],
            "line": move["entryLine"], "reachableScriptInstructions": len(move["reachableInstructionLines"]),
            "commands": move["commands"], "templates": move["spriteTemplates"],
            "visualTasks": move["visualTasks"], "soundTasks": move["soundTasks"],
            "graphicTags": move["scriptGraphicTags"], "directSounds": move["scriptDirectSounds"],
            "conditionalScriptLines": branch_lines,
            "callbackRoots": sorted(fn_indices[k] for k in roots),
            "directStaticCallClosure": sorted(fn_indices[k] for k in closure(roots, direct_only=True)),
            "knownStaticNamedClosure": sorted(fn_indices[k] for k in known_closure),
            "cReferencedSpriteTemplates": referenced_templates,
            "cReferencedGraphicTags": sorted({s for k in known_closure for s in functions[k]["graphicTagSymbols"]} |
                {v for t in referenced_templates for key, v in templates[t]["fields"].items()
                 if key in {"tileTag", "paletteTag"} and v.startswith("ANIM_TAG_")}),
            "cReferencedSoundSymbols": sorted({s for k in known_closure for s in functions[k]["soundSymbols"]}),
            "dynamicSiteFunctions": sorted(dynamic),
            "outsideScopeCalls": sorted({c for k in known_closure for c in functions[k]["outsideScopeCalls"]}),
            "missingRuntimeCommands": sorted(set(move["commands"]) - set(supported_ops)),
            "missingRuntimeTemplates": sorted(set(move["spriteTemplates"]) - admitted_templates),
            "missingRuntimeTasks": sorted(set(move["visualTasks"] + move["soundTasks"]) - admitted_tasks),
            "newRootCallbacks": sorted(fn_indices[k] for k in roots - admitted_root_functions),
            "registeredRecipe": mid in registered_ids,
            "historicalNativeFixtureEvidence": "six-move-checkpoint" if mid in registered_ids else None,
            "originalRuntimePixelParityVerified": False,
            "completeExecutionClosureProven": False})
    assert not unresolved_roots, unresolved_roots

    def ranked(uses, known):
        return [{"name": name, "moves": ids, "moveCount": len(ids), "runtimeRootAdmitted": name in known}
                for name, ids in sorted(uses.items(), key=lambda item: (-len(item[1]), item[0]))]

    nodes = []
    for key in sorted(functions):
        fn = functions[key]
        node = {k: v for k, v in fn.items() if k not in {"body", "bodyOffset", "parameters"}}
        node["id"] = fn_indices[key]
        for field in ["directCalls", "namedFunctionReferences"]:
            node[field] = [fn_indices[k] for k in node[field]]
        node["inExistingCOracle"] = key in oracle_fn_keys
        nodes.append(node)
    for t in templates.values():
        if t["callbackFunction"]:
            t["callbackFunction"] = fn_indices[t["callbackFunction"]]
        t["moveCount"] = len(template_use[t["name"]])
        t["runtimeRootAdmitted"] = t["name"] in admitted_templates

    macro_text = git("show", COMMIT + ":asm/macros/battle_anim_script.inc").decode()
    macro_bodies = {m[1]: m[2] for m in re.finditer(r"^\s*\.macro\s+(\w+)(.*?)^\s*\.endm", macro_text, re.M | re.S)}
    def command_handlers(op, seen=()):
        assert op not in seen, (op, seen)
        handler = resolve("Cmd_" + op, "src/battle_anim.c")
        if handler:
            return [handler]
        expanded = [name for name in macro_bodies
                    if re.search(r"^\s*" + re.escape(name) + r"\b", macro_bodies[op], re.M)]
        assert expanded, op
        return sorted({h for name in expanded for h in command_handlers(name, (*seen, op))})
    command_rows = []
    for op in catalog["usedCommands"]:
        handler = resolve("Cmd_" + op, "src/battle_anim.c")
        expanded_ops = []
        if not handler:
            expanded_ops = [name for name in macro_bodies
                            if re.search(r"^\s*" + re.escape(name) + r"\b", macro_bodies[op], re.M)]
        handlers = command_handlers(op)
        assert handlers and all(handlers), (op, expanded_ops)
        command_rows.append({"command": op, "moves": command_use[op],
            "runtimeCommandNameAdmitted": op in supported_ops,
            "expandsToCommands": sorted(expanded_ops),
            "sourceHandlers": [fn_indices[k] for k in handlers],
            "handlerStaticNamedClosure": [fn_indices[k] for k in closure(handlers)]})

    result = {"schemaVersion": 1, "kind": "bounded-static-animation-dependency-inventory",
        "sourceCommit": COMMIT, "gameRevision": 0,
        "scope": "All 354 move script roots; all src/battle_anim*.c, src/sprite.c and src/task.c. C preprocessor branches are lexical supersets; external engine/library/macro calls are boundaries.",
        "limitations": [
            "Known static named closure follows lexical direct calls, named callback references and referenced SpriteTemplates, including all conditional paths. It is not a context-sensitive points-to analysis.",
            "Indirect function calls, nonliteral callback assignments, stored callback recovery and external engine/macro calls remain explicit boundaries. Dynamic target absence is not proof of a complete execution graph.",
            "No preprocessing/compilation, source execution, new renderer acceptance, GUI, audio listening or original-ROM pixel comparison was performed.",
            "Function declarations, global callback tables and callbacks hidden by macro expansion are not expanded. Root resolution and all 68 existing C-oracle function definitions are checked.",
            "Supported command names do not imply all operands/modes are supported. Registered templates do not admit every callback parameter combination or move.",
            "Historical native fixture reports prove only their documented six-move singles checkpoint; they do not accept later production changes or the in-progress doubles port."],
        "provenance": {"scannerSha256": sha(Path(__file__).read_bytes()),
            "catalogParserSha256": sha(parser_path.read_bytes()), "scriptInputs": catalog["inputs"],
            "cInputs": inputs, "runtimeSourceSha256": sha(runtime_inputs[runtime_path]),
            "runtimeManifestSha256": sha(runtime_inputs[runtime_manifest_path]),
            "runtimeInputs": [{"path": p, "sha256": sha(b)} for p, b in runtime_inputs.items()]},
        "summary": {**catalog["summary"], "allScriptInstructions": catalog["instructionCount"],
            "allScriptLabels": catalog["labelCount"], "allScriptCommands": len(catalog["usedCommands"]),
            "cFiles": len(raw), "parsedFunctionDefinitions": len(functions),
            "parsedSpriteTemplates": len(templates), "directRootCallbacks": len(callback_use),
            "commandsUsedByMoves": sum(bool(v) for v in command_use.values()),
            "scriptConditionalMoves": sum(bool(m["conditionalScriptLines"]) for m in entries),
            "registeredRecipes": len(registered_ids), "runtimeCommandNames": len(supported_ops),
            "runtimeSpriteTemplateRoots": len(admitted_templates), "runtimeVisualTaskRoots": len(admitted_tasks)},
        "runtime": {"moveIds": registered_ids, "commandNames": supported_ops,
            "spriteTemplates": sorted(admitted_templates), "visualTasks": sorted(admitted_tasks),
            "callbackRootFunctions": sorted(fn_indices[k] for k in admitted_root_functions),
            "conditionalOperandRestrictions": "SourceBattleAnimation.Validate and committed invocation dispatch remain authoritative.",
            "existingOracleFunctionCount": len(oracle_fn_keys),
            "nativeEvidence": {"id": "six-move-checkpoint", "resultPath": evidence_root + "RESULT.json",
                "resultSha256": sha((repo / evidence_root / "RESULT.json").read_bytes()),
                "newMoveIds": prior_result["newMoveIds"], "sourceFrames": prior_result["sourceFrames"],
                "nativeChecks": prior_result["nativeChecks"], "nativeAnimations": prior_result["nativeAnimations"],
                "nativeDraws": prior_result["nativeDraws"], "sourceWitnessMatchesCurrent": witness_status,
                "knownLimitations": prior_result["knownLimitations"], "freshVerificationPerformed": False}},
        "rankings": {"templates": ranked(template_use, admitted_templates),
            "tasks": ranked(task_use, admitted_tasks), "commands": ranked(command_use, set(supported_ops)),
            "callbackRoots": [{"function": fn_indices[k], "name": functions[k]["name"], "moveCount": len(v), "moves": v}
                for k, v in sorted(callback_use.items(), key=lambda x: (-len(x[1]), x[0]))]},
        "scriptCommands": command_rows,
        "moves": entries, "templates": [templates[k] for k in sorted(templates)], "functions": nodes}
    assert all((repo / p).read_bytes() == b for p, b in runtime_inputs.items()), "Runtime changed while scanning; retry for a consistent snapshot"
    return result


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--repo", type=Path, required=True)
    p.add_argument("--reference", type=Path, required=True)
    p.add_argument("--output", type=Path, required=True)
    a = p.parse_args()
    if a.output.exists():
        p.error("Refuse to overwrite inventory")
    result = run(a.repo.resolve(), a.reference.resolve())
    a.output.parent.mkdir(parents=True, exist_ok=True)
    data = (json.dumps(result, ensure_ascii=False, separators=(",", ":")) + "\n").encode()
    a.output.write_bytes(gzip.compress(data, mtime=0) if a.output.suffix == ".gz" else data)
    print(json.dumps(result["summary"], indent=2))


if __name__ == "__main__":
    main()
