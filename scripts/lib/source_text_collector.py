"""Read-only, source-labelled text extraction and encoded control comparison.

No upstream code, build tools, preprocessors, or importers are executed.
The default branch selection is FireRed revision 0, BUGFIX off, XZONN_HACK off.
"""
from __future__ import annotations

import argparse
import ast
from collections import Counter, defaultdict
from dataclasses import asdict, dataclass, field
import hashlib
import json
from pathlib import Path
import re
from typing import Iterable, Mapping

KNOWN_EXCLUSIONS = {
    "PalletTown_Text_CanStoreItemsAndMonsInPC":
        "Xzonn translator/port commentary replaces the original NPC meaning; needs explicit editorial correction."
}
DEFAULT_DEFINES = {"FIRERED": 1, "LEAFGREEN": 0, "REVISION": 0,
                   "BUGFIX": 0, "UBFIX": 0, "XZONN_HACK": 0}
STRING = r'"((?:[^"\\]|\\.)*)"'
TOKEN = re.compile(r"\{[^{}]*\}")
LABEL = re.compile(r"^\s*([A-Za-z_]\w*):{1,2}\s*(?:@.*)?$")
DIRECTIVE = re.compile(r"^\s*\.(string|braille)\s+" + STRING + r"\s*(?:@.*)?$")
C_DECL = re.compile(
    r"\b(?:static\s+)?const\s+u8\s+([A-Za-z_]\w*)\s*"
    r"\[\s*(?:\d+)?\s*\]\s*=\s*"
    r"(?:(?:_|N_)\s*\(\s*((?:\"(?:[^\"\\]|\\.)*\"\s*)+)\)|"
    r"((?:\"(?:[^\"\\]|\\.)*\"\s*)+))\s*;", re.S)


@dataclass
class Declaration:
    symbol: str
    text: str
    path: str
    line: int
    directive: str
    conditional_unknown: list[str] = field(default_factory=list)


@dataclass
class Collection:
    texts: dict[str, str]
    records: dict[str, list[Declaration]]
    diagnostics: dict
    excluded: dict[str, str]

    def to_dict(self):
        return {"texts": self.texts,
                "records": {k: [asdict(v) for v in vs] for k, vs in self.records.items()},
                "diagnostics": self.diagnostics, "excluded": self.excluded}


def _condition(expression: str, defines: Mapping[str, int]) -> bool | None:
    """Interpret only a small declarative integer/boolean expression grammar."""
    expression = expression.split("@", 1)[0].strip()
    expression = re.sub(r"defined\s*\(\s*(\w+)\s*\)|defined\s+(\w+)",
                        lambda m: str(int(bool(defines.get(m[1] or m[2], 0)))), expression)
    expression = expression.replace("&&", " and ").replace("||", " or ")
    expression = re.sub(r"!(?!=)", " not ", expression).strip()

    def visit(n):
        if isinstance(n, ast.Constant) and isinstance(n.value, (int, bool)):
            return int(n.value)
        if isinstance(n, ast.Name):
            if n.id not in defines:
                raise ValueError(n.id)
            return defines[n.id]
        if isinstance(n, ast.UnaryOp):
            v = visit(n.operand)
            if isinstance(n.op, ast.Not): return not v
            if isinstance(n.op, ast.USub): return -v
            if isinstance(n.op, ast.UAdd): return +v
        if isinstance(n, ast.BoolOp):
            vs = [bool(visit(v)) for v in n.values]
            return all(vs) if isinstance(n.op, ast.And) else any(vs)
        if isinstance(n, ast.Compare) and len(n.ops) == 1:
            a, b, op = visit(n.left), visit(n.comparators[0]), n.ops[0]
            for kind, value in ((ast.Eq, a == b), (ast.NotEq, a != b),
                                (ast.Lt, a < b), (ast.LtE, a <= b),
                                (ast.Gt, a > b), (ast.GtE, a >= b)):
                if isinstance(op, kind): return value
        raise ValueError(ast.dump(n))
    try:
        return bool(visit(ast.parse(expression, mode="eval").body))
    except (ValueError, SyntaxError):
        return None


def _strip_c_comments(text: str) -> str:
    """Mask comments, preserving strings, byte offsets and line numbers."""
    rx = re.compile(r'"(?:[^"\\]|\\.)*"|\'(?:[^\'\\]|\\.)*\'|//[^\n]*|/\*.*?\*/', re.S)
    return rx.sub(lambda m: re.sub(r"[^\n]", " ", m[0]) if m[0].startswith(("//", "/*")) else m[0], text)


def _select_lines(text: str, defines: Mapping[str, int]):
    lines, unknown_by_line, stack, issues = [], {}, [], []
    rx = re.compile(r"^\s*(?:#\s*|\.)"
                    r"(ifdef|ifndef|if|elif|elseif|else|endif)\b(.*)$")
    for no, line in enumerate(text.splitlines(keepends=True), 1):
        m = rx.match(line)
        if m:
            cmd, arg = m[1], m[2].strip().split("@", 1)[0].strip()
            if cmd in ("if", "ifdef", "ifndef"):
                val = (_condition(arg, defines) if cmd == "if" else
                       bool(defines.get(arg, 0)) if cmd == "ifdef" else
                       not bool(defines.get(arg, 0)))
                stack.append({"active": val, "taken": val, "expression": line.strip()})
            elif cmd in ("elif", "elseif"):
                if not stack:
                    issues.append({"line": no, "issue": "orphan conditional"})
                else:
                    current = stack[-1]
                    value = _condition(arg, defines)
                    current["active"] = (False if current["taken"] is True else
                                         None if current["taken"] is None else value)
                    if current["active"] is True: current["taken"] = True
                    elif current["active"] is None: current["taken"] = None
                    current["expression"] = line.strip()
            elif cmd == "else":
                if stack:
                    current = stack[-1]
                    current["active"] = None if current["taken"] is None else not current["taken"]
                else:
                    issues.append({"line": no, "issue": "orphan else"})
            elif cmd == "endif":
                if stack: stack.pop()
                else: issues.append({"line": no, "issue": "orphan endif"})
            lines.append("\n" if line.endswith("\n") else "")
            continue
        active = not any(s["active"] is False for s in stack)
        unknown = [s["expression"] for s in stack if s["active"] is None]
        lines.append(line if active else "\n" if line.endswith("\n") else "")
        if active and unknown:
            unknown_by_line[no] = unknown
    if stack: issues.append({"issue": "unclosed conditional", "stack": stack})
    return "".join(lines), unknown_by_line, issues


def collect_declarations(source_root: str | Path, *,
                         exclude_symbols: Mapping[str, str] | None = None,
                         defines: Mapping[str, int] | None = None,
                         paths: Iterable[str] | None = None) -> Collection:
    """Collect ASM/C literal declarations; conflicting static symbols are withheld.

    .texts is the safe unambiguous map. .records retains all parsed declarations,
    including explicitly excluded commentary. Control strings remain source-escaped.
    Multidimensional name arrays and JSON catalogs are intentionally handled elsewhere.
    """
    root = Path(source_root)
    defines = dict(DEFAULT_DEFINES if defines is None else defines)
    exclusions = dict(KNOWN_EXCLUSIONS if exclude_symbols is None else exclude_symbols)
    records, issues = defaultdict(list), []
    selected = ([root / p for p in paths] if paths is not None else
                sorted(p for p in root.rglob("*") if p.is_file() and
                       p.suffix in (".inc", ".s", ".c", ".h")))
    for p in selected:
        relative = p.relative_to(root).as_posix()
        original = p.read_text(encoding="utf-8")
        clean = _strip_c_comments(original) if p.suffix in (".c", ".h") else original
        text, unknown, condition_issues = _select_lines(clean, defines)
        issues.extend({"path": relative, **i} for i in condition_issues)
        if p.suffix in (".inc", ".s"):
            symbol, start, parts, directives, conditionals = None, None, [], [], []

            def flush():
                if symbol and parts:
                    records[symbol].append(Declaration(symbol, "".join(parts), relative,
                                                       start, "+".join(sorted(set(directives))),
                                                       sorted(set(conditionals))))
            for no, line in enumerate(text.splitlines(), 1):
                m = LABEL.match(line)
                if m:
                    flush()
                    symbol, start, parts, directives, conditionals = m[1], no, [], [], []
                    continue
                m = DIRECTIVE.match(line)
                if m and symbol:
                    directives.append(m[1]); parts.append(m[2])
                    conditionals.extend(unknown.get(no, []))
                elif symbol and re.match(r"^\s*\.(string|braille)\b", line):
                    issues.append({"path": relative, "line": no,
                                   "symbol": symbol, "issue": "unsupported text directive",
                                   "source": line.strip()})
            flush()
        else:
            for m in C_DECL.finditer(text):
                no = text.count("\n", 0, m.start()) + 1
                end = text.count("\n", 0, m.end()) + 1
                contexts = sorted({c for n in range(no, end + 1) for c in unknown.get(n, [])})
                body = m[2] or m[3]
                value = "".join(re.findall(STRING, body))
                records[m[1]].append(Declaration(m[1], value, relative, no, "c-literal", contexts))
    texts, conflicts, uncertain = {}, {}, {}
    for symbol, entries in records.items():
        variants = sorted(set(e.text for e in entries))
        if any(e.conditional_unknown for e in entries):
            uncertain[symbol] = [asdict(e) for e in entries]
        elif len(variants) != 1:
            conflicts[symbol] = [asdict(e) for e in entries]
        elif symbol not in exclusions:
            texts[symbol] = variants[0]
    excluded = {s: reason for s, reason in exclusions.items() if s in records}
    return Collection(dict(sorted(texts.items())), dict(sorted(records.items())),
                      {"sourceFiles": len(selected), "declarations": sum(map(len, records.values())),
                       "symbolCount": len(records), "conflicts": conflicts,
                       "unknownConditions": uncertain, "issues": issues,
                       "defines": defines}, excluded)


class Charmap(dict):
    """Named encodings with exact quoted-glyph operands available separately."""
    def __init__(self):
        super().__init__()
        self.glyphs = {}


def parse_charmap(path: str | Path) -> Charmap:
    """Read named encodings and glyph bytes needed by legacy operand spelling."""
    result = Charmap()
    for no, line in enumerate(Path(path).read_text(encoding="utf-8").splitlines(), 1):
        glyph = re.match(r"^'((?:[^'\\]|\\.)*)'\s*=\s*([0-9A-Fa-f ]+)", line)
        if glyph:
            encoded = re.sub(r"\s", "", glyph[2])
            if len(encoded) % 2 == 0:
                result.glyphs[glyph[1]] = tuple(bytes.fromhex(encoded))
        m = re.match(r"^([A-Za-z_]\w*)\s*=\s*((?:[0-9A-Fa-f]{2}(?:\s|$))+)", line)
        if m:
            value = tuple(int(x, 16) for x in m[2].split())
            if m[1] in result and result[m[1]] != value:
                raise ValueError(f"Conflicting charmap symbol {m[1]} at line {no}")
            result[m[1]] = value
    return result


def encode_control(token: str, charmap: Mapping[str, tuple[int, ...]]) -> tuple[int, ...] | None:
    """Encode a brace control plus operands exactly as declared in charmap."""
    words = token[1:-1].split() if token.startswith("{") and token.endswith("}") else []
    if not words:
        return None
    encoded = []
    for word in words:
        if word in charmap:
            encoded.extend(charmap[word])
        elif re.fullmatch(r"0x[0-9A-Fa-f]{1,2}|\d{1,3}", word):
            number = int(word, 16) if word.startswith("0x") else int(word, 10)
            if not 0 <= number <= 255: return None
            encoded.append(number)
        else:
            return None
    return tuple(encoded)


# Exact length table from baseline src/string_util.c:GetExtCtrlCodeLength.
# Each value counts the opcode byte after FC and its operands.
EXT_CONTROL_LENGTHS = (1, 2, 2, 2, 4, 2, 2, 1, 2, 1, 1, 3, 2,
                       2, 2, 1, 3, 2, 2, 2, 2, 1, 1, 1, 1)


def control_units(text: str, charmap: Mapping) -> list[dict]:
    """Join adjacent command/operand spellings by verified binary arity.

    For example {PLAY_BGM}{MUS_LEVEL_UP} is one unit. The glyph À after
    {HIGHLIGHT} is byte 01, consumed as its operand, not displayed text.
    """
    units, cursor = [], 0
    glyphs = getattr(charmap, "glyphs", {})
    for match in TOKEN.finditer(text):
        if match.start() < cursor:
            continue
        encoded = encode_control(match[0], charmap)
        end, issue = match.end(), None
        if encoded and len(encoded) >= 2 and encoded[0] == 0xFC:
            if encoded[1] < len(EXT_CONTROL_LENGTHS):
                length = 1 + EXT_CONTROL_LENGTHS[encoded[1]]
                while len(encoded) < length and end < len(text):
                    operand = TOKEN.match(text, end)
                    if operand:
                        values = encode_control(operand[0], charmap)
                        if values is None or len(encoded) + len(values) > length:
                            break
                        encoded += values; end = operand.end()
                    elif text[end] in glyphs:
                        values = glyphs[text[end]]
                        if len(encoded) + len(values) > length:
                            break
                        encoded += values; end += 1
                    else:
                        break
                if len(encoded) != length:
                    issue = {"kind": "control-operand-length",
                             "expectedBytes": length, "actualBytes": len(encoded)}
        units.append({"start": match.start(), "end": end, "token": text[match.start():end],
                      "encoding": encoded, "issue": issue})
        cursor = end
    return units


def _encoded_counter(tokens, charmap):
    counter = Counter()
    for token in tokens:
        encoded = encode_control(token, charmap)
        counter[encoded if encoded is not None else ("unknown", token)] += 1
    return counter


def _counter_rows(counter):
    return [{"encoding": " ".join(f"{b:02X}" for b in key)
             if key and isinstance(key[0], int) else list(key), "count": value}
            for key, value in sorted(counter.items(), key=lambda kv: str(kv[0]))]


def canonicalize_controls(text: str, baseline_text: str,
                          source_charmap: Mapping[str, tuple[int, ...]],
                          baseline_charmap: Mapping[str, tuple[int, ...]]) -> dict:
    """Use only baseline spellings with identical encoded meaning.

    Placeholder order and visual \\n reflow are allowed. Page/scroll, timing, and
    terminator differences are diagnostics. No missing command is inserted or dropped.
    """
    baseline_units = control_units(baseline_text, baseline_charmap)
    source_units = control_units(text, source_charmap)
    baseline_tokens = [u["token"] for u in baseline_units]
    source_tokens = [u["token"] for u in source_units]
    by_encoding = defaultdict(set)
    for unit in baseline_units:
        key = unit["encoding"]
        if key is not None: by_encoding[key].add(unit["token"])
    changes, unresolved, unknown = [], [], []

    def substitute(unit, index):
        token, key = unit["token"], unit["encoding"]
        if key is None:
            unknown.append({"side": "source", "token": token, "tokenIndex": index})
            return token
        options = by_encoding.get(key, set())
        if token in options:
            return token
        if len(options) == 1:
            replacement = next(iter(options))
            changes.append({"from": token, "to": replacement, "tokenIndex": index,
                            "bytes": " ".join(f"{b:02X}" for b in key)})
            return replacement
        if len(options) > 1:
            unresolved.append({"kind": "ambiguous-baseline-alias", "token": token,
                               "candidates": sorted(options),
                               "bytes": " ".join(f"{b:02X}" for b in key)})
        return token

    pieces, cursor = [], 0
    for index, unit in enumerate(source_units):
        pieces.extend([text[cursor:unit["start"]], substitute(unit, index)])
        cursor = unit["end"]
    pieces.append(text[cursor:])
    normalized = "".join(pieces)
    for unit in baseline_units:
        if unit["encoding"] is None:
            unknown.append({"side": "baseline", "token": unit["token"]})
    for side, units in (("source", source_units), ("baseline", baseline_units)):
        for unit in units:
            if unit["issue"]:
                unresolved.append({"side": side, "token": unit["token"], **unit["issue"]})
    def count(units):
        return Counter(u["encoding"] if u["encoding"] is not None else ("unknown", u["token"])
                       for u in units)
    a, b = count(source_units), count(baseline_units)
    if a != b:
        unresolved.append({"kind": "control-encoding-multiset-difference",
                           "sourceOnly": _counter_rows(a - b),
                           "baselineOnly": _counter_rows(b - a)})
    if unknown:
        unresolved.append({"kind": "unknown-control-encoding", "tokens": unknown})
    source_pages = re.findall(r"\\[pl]", text)
    baseline_pages = re.findall(r"\\[pl]", baseline_text)
    if source_pages != baseline_pages:
        unresolved.append({"kind": "page-scroll-sequence-difference",
                           "source": source_pages, "baseline": baseline_pages,
                           "sourceCounts": dict(Counter(source_pages)),
                           "baselineCounts": dict(Counter(baseline_pages))})
    def command_events(value, units):
        events = [(u["start"], " ".join(f"{b:02X}" for b in u["encoding"]))
                  for u in units if u["encoding"] and u["encoding"][0] == 0xFC]
        events += [(m.start(), m[0]) for m in re.finditer(r"\\[pl]|\$", value)]
        return [event for _, event in sorted(events)]
    command_source = command_events(text, source_units)
    command_baseline = command_events(baseline_text, baseline_units)
    if command_source != command_baseline:
        unresolved.append({"kind": "command-page-order-difference",
                           "source": command_source, "baseline": command_baseline})
    source_terms = {"count": text.count("$"), "atEnd": text.endswith("$")}
    baseline_terms = {"count": baseline_text.count("$"), "atEnd": baseline_text.endswith("$")}
    if source_terms != baseline_terms:
        unresolved.append({"kind": "terminator-anchor-difference",
                           "source": source_terms, "baseline": baseline_terms})
    return {"text": normalized, "changes": changes, "unresolved": unresolved,
            "visualNewlines": {"source": text.count(r"\n"), "baseline": baseline_text.count(r"\n")},
            "placeholderSequence": {
                "source": [u["token"] for u in source_units
                           if u["encoding"] and u["encoding"][0] == 0xFD],
                "baseline": [u["token"] for u in baseline_units
                             if u["encoding"] and u["encoding"][0] == 0xFD]},
            "encodedControlsMatch": a == b, "safeWithoutEditorialCorrection": not unresolved}


def apply_declared_transform(text: str, transform: Mapping) -> str:
    """Reapply only an explicitly supplied existing producer transformation."""
    if transform.get("removePause"):
        text = text.replace("{PAUSE_UNTIL_PRESS}", "")
    if transform.get("decodeNewlines"):
        text = text.replace(r"\n", "\n")
    if transform.get("escapeNewlines"):
        text = text.replace("\n", r"\n")
    if transform.get("terminate"):
        text = text.rstrip("$") + "$"
    return text


def candidate_maps(collection: Collection, baseline: Mapping[str, str],
                   source_charmap, baseline_charmap, *,
                   source_transforms: Mapping[str, Mapping] | None = None) -> dict:
    candidates, safe, missing, excluded, diagnostics, unchanged = {}, {}, [], {}, {}, []
    for symbol, baseline_text in sorted(baseline.items()):
        if symbol in collection.excluded:
            excluded[symbol] = collection.excluded[symbol]
            continue
        if symbol not in collection.texts:
            missing.append(symbol)
            continue
        source_text = collection.texts[symbol]
        if source_transforms and symbol in source_transforms:
            source_text = apply_declared_transform(source_text, source_transforms[symbol])
        result = canonicalize_controls(source_text, baseline_text,
                                       source_charmap, baseline_charmap)
        candidates[symbol] = result["text"]
        if result["safeWithoutEditorialCorrection"]:
            safe[symbol] = result["text"]
        if result["changes"] or result["unresolved"]:
            diagnostics[symbol] = {k: v for k, v in result.items() if k != "text"}
        if result["text"] == baseline_text:
            unchanged.append(symbol)
    return {"candidates": candidates, "safeCandidates": safe, "missing": missing,
            "excluded": excluded, "diagnostics": diagnostics, "unchanged": unchanged,
            "summary": {"baseline": len(baseline), "candidateCount": len(candidates),
                        "safeCount": len(safe), "missing": len(missing),
                        "excluded": len(excluded), "unchanged": len(unchanged),
                        "requiresReview": len(candidates) - len(safe)}}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--repo", type=Path, required=True)
    p.add_argument("--output-dir", type=Path, required=True)
    args = p.parse_args()
    repo, out = args.repo.resolve(), args.output_dir.resolve()
    if out == repo or repo in out.parents:
        raise SystemExit("Staging output must be outside the canonical repository.")
    out.mkdir(parents=True, exist_ok=True)
    source = repo / "content/raw/PretFRLG/a04f6317"
    baseline = repo / "content/raw/pokefirered/037335f4"
    collection = collect_declarations(source)
    zh_map, en_map = parse_charmap(source / "charmap.txt"), parse_charmap(baseline / "charmap.txt")

    def write(name, value):
        (out / name).write_text(json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
                                encoding="utf-8")
    write("collected-declarations.json", collection.to_dict())
    groups = {
        "story": json.loads((repo / "content/generated/data/story.json").read_text())["TextBank"],
        "battle": json.loads((repo / "content/generated/battle/text.json").read_text())}
    summary, editorial = {}, {}
    story_recipe = json.loads((repo / "tools/import-story/recipe.json").read_text())
    story_transforms = {entry["symbol"]: entry for entry in story_recipe["supplementalTexts"]}
    for group, bank in groups.items():
        result = candidate_maps(collection, bank, zh_map, en_map,
                                source_transforms=story_transforms if group == "story" else None)
        write(group + "-candidates.json", result)
        summary[group] = result["summary"]
        editorial[group] = {
            symbol: {"baseline": bank[symbol], "candidate": result["candidates"][symbol],
                     "source": [asdict(d) for d in collection.records[symbol]],
                     "unresolved": d["unresolved"]}
            for symbol, d in result["diagnostics"].items() if d["unresolved"]}
    write("editorial-corrections.json", editorial)
    fd_map = {
        name: {"encoding": " ".join(f"{b:02X}" for b in encoded),
               "kind": "variable-placeholder", "contextDependent": True}
        for name, encoded in en_map.items()
        if len(encoded) == 2 and encoded[0] == 0xFD and not name.startswith(("SE_", "MUS_"))
        and zh_map.get(name) == encoded}
    write("verified-variable-placeholders.json", fd_map)
    write("collection-summary.json", {
        **summary, "collectorDiagnostics": collection.diagnostics,
        "sourceCommit": "a04f63173ca5fcf9c2db18ee223cf057c7f232c0",
        "baselineCommit": "037335f4c725d7c9aecdac87066f2002b4bd7e14",
        "variablePlaceholderCount": len(fd_map)})
    print(json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    main()
