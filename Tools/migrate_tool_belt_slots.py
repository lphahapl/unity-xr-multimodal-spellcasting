"""Merge legacy belt helper + SnapZone in a text-serialized Unity scene (dry run by default)."""
import argparse
import re
from pathlib import Path

BASE = "87454a17ac8b80841b5aaa0fddb69c1f"
BELT = "718aecc28f0a4447a04f721baf833e27"


def migrate(text):
    blocks = re.split(r"(?m)(?=^--- !u!)", text)
    pairs = {}
    for index, block in enumerate(blocks):
        component = re.match(r"--- !u!114 &(-?\d+)\r?\nMonoBehaviour:", block)
        script = re.search(r"(?m)^  m_Script: \{fileID: 11500000, guid: (\w+),", block)
        if not component or not script or script[1] not in (BASE, BELT):
            continue
        owner = re.search(r"(?m)^  m_GameObject: \{fileID: (-?\d+)\}", block)[1]
        entries = pairs.setdefault(owner, {})
        if script[1] in entries:
            raise ValueError("Duplicate zone on object " + owner)
        entries[script[1]] = (index, component[1])

    redirects = {}
    for owner, entries in pairs.items():
        if len(entries) != 2:
            continue
        bi, keep = entries[BASE]
        hi, old = entries[BELT]
        base, helper = blocks[bi], blocks[hi]
        if any("m_PrefabInstance: {fileID: 0}" not in x for x in (base, helper)):
            raise ValueError("Migrate the prefab source before editing an instance override")
        if re.search(r"(?m)^  m_Enabled: 0", base + helper):
            raise ValueError("Review disabled legacy components before migration")
        field_start = helper.index("  opening:")
        if "  _snapMode:" in helper:
            raise ValueError("Found mixed legacy and derived layouts on object " + owner)
        derived_identity = re.search(r"(?m)^  m_EditorClassIdentifier:[^\r\n]*", helper)[0]
        blocks[bi] = re.sub(r"(?m)^  m_EditorClassIdentifier:[^\r\n]*", derived_identity, base.replace(BASE, BELT)) + helper[field_start:]
        blocks[hi] = ""
        redirects[old] = keep
    merged = "".join(blocks)
    for old, keep in redirects.items():
        merged, n = re.subn(r"(?m)^  - component: \{fileID: "+old+r"\}\r?\n", "", merged)
        if n != 1:
            raise ValueError("Expected one component list entry for " + old)
        merged = merged.replace("{fileID: "+old+"}", "{fileID: "+keep+"}")
    return merged, len(redirects)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scene", type=Path)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    if args.scene.suffix != ".unity":
        parser.error("This entry migrates scene-local components; prefab overrides need separate reference review.")
    original = args.scene.read_bytes()
    if not original.startswith(b"%YAML"):
        parser.error("Expected a text-serialized Unity scene")
    updated, count = migrate(original.decode("utf-8"))
    if count and args.write:
        if args.scene.read_bytes() != original:
            raise RuntimeError("Scene changed since it was read")
        args.scene.write_bytes(updated.encode("utf-8"))
    print(f"{args.scene}: {count} pairs ({'write' if args.write else 'dry run'})")
