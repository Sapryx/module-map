import posixpath
import sys

from maplib import assign_files, load_map, repo_files, repo_root, source_files, source_prefix
from profiles import PROFILES


def main():
    if len(sys.argv) != 2:
        print("usage: python scan.py <map.json>")
        return 2

    data = load_map(sys.argv[1])
    source = data["source"]
    root = repo_root()
    all_files = repo_files(root)
    files = source_files(source, all_files)
    file_to_module, uncovered, duplicated = assign_files(data, files)

    print("== Coverage")
    for path in uncovered:
        print(f"  NOT IN ANY MODULE  {path}")
    for path, ids in duplicated.items():
        print(f"  IN SEVERAL MODULES {path}: {', '.join(ids)}")
    if not uncovered and not duplicated:
        print("  every source file belongs to exactly one module")

    profile = PROFILES.get(source.get("profile"))
    if profile is None:
        print("\n== No language profile: find module dependencies and external libraries by reading the code")
        return 0

    prefix = source_prefix(source)
    texts = {
        path: profile.strip_comments((root / (prefix + path)).read_text(encoding="utf-8", errors="replace"))
        for path in files
    }
    context = profile.context(texts, all_files, prefix, root)

    type_to_module = {}
    for path, text in texts.items():
        module = file_to_module.get(path)
        if module is None:
            continue
        for name in profile.definitions(text):
            type_to_module.setdefault(name, module)

    composition_roots = {edge[0] for edge in data["edges"] if len(edge) > 2 and edge[2] == "wiring"}
    found = {}
    for path, text in texts.items():
        source_module = file_to_module.get(path)
        if source_module is None or source_module in composition_roots:
            continue
        for name in profile.references(text):
            target = type_to_module.get(name)
            if target is not None and target != source_module:
                found.setdefault((source_module, target), set()).add(name)
        for linked in profile.file_links(path, text, context):
            target = file_to_module.get(linked)
            if target is not None and target != source_module:
                found.setdefault((source_module, target), set()).add(posixpath.basename(linked))

    mapped = {
        (edge[0], edge[1]) for edge in data["edges"]
        if (edge[2] if len(edge) > 2 else "uses") not in ("wiring", "external")
    }

    print("\n== Module dependencies found in code (type names and includes; same-named members are false positives)")
    for pair in sorted(found):
        status = "in map" if pair in mapped or (pair[1], pair[0]) in mapped else "NEW"
        print(f"  [{status}] {pair[0]} -> {pair[1]}: {', '.join(sorted(found[pair]))}")

    print("\n== Map edges with no reference in code (stale, made only through events, markup or reflection, or reversed)")
    missing = [pair for pair in sorted(mapped) if pair not in found]
    for pair in missing:
        print(f"  {pair[0]} -> {pair[1]}")
    if not missing:
        print("  none")

    print("\n== External signals per module (imports from outside the codebase, native libraries)")
    signals = {}
    for path, text in texts.items():
        module = file_to_module.get(path, "?")
        for name in profile.imports(text):
            if profile.is_external(name, context):
                signals.setdefault(module, set()).add(name)
        for name in profile.natives(text):
            signals.setdefault(module, set()).add(f"native {name}")
    for module in sorted(signals):
        print(f"  {module}: {', '.join(sorted(signals[module]))}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
