import html
import json
import sys
from pathlib import Path

from maplib import assign_files, load_map, repo_files, repo_root, source_files
from profiles import PROFILES

KINDS = {"feature", "service", "adapter", "utility", "infra", "model"}
EDGE_TYPES = {"uses", "listens", "implements", "cycle", "wiring", "external"}
NOTE_LEVELS = {"bad", "warn", "ok"}
MODULE_FIELDS = [
    "id", "name", "kind", "project", "ns", "col", "row", "portability", "iface", "impl", "libs", "note", "files"
]
EXTERNAL_FIELDS = ["id", "name", "sub", "kind", "projects", "note"]


def validate_source(source):
    profile = source.get("profile")
    if profile is not None and profile not in PROFILES:
        return [f"source: unknown profile {profile}, expected one of {', '.join(PROFILES)}"]
    if profile is None and "suffixes" not in source:
        return ["source: give suffixes when there is no profile"]
    return []


def validate(data):
    errors = []
    projects = {project["id"] for project in data["projects"]}
    rows = {row["id"] for row in data["rows"]}
    modules = {}
    cells = {}

    for module in data["modules"]:
        missing = [field for field in MODULE_FIELDS if field not in module]
        if missing:
            errors.append(f"module {module.get('id', '?')}: missing {', '.join(missing)}")
            continue
        if module["id"] in modules:
            errors.append(f"module {module['id']}: duplicate id")
        modules[module["id"]] = module
        if module["kind"] not in KINDS:
            errors.append(f"module {module['id']}: unknown kind {module['kind']}")
        if module["project"] not in projects:
            errors.append(f"module {module['id']}: unknown project {module['project']}")
        if module["row"] not in rows:
            errors.append(f"module {module['id']}: unknown row {module['row']}")
        if module["portability"] not in (1, 2, 3):
            errors.append(f"module {module['id']}: portability must be 1, 2 or 3")
        cell = (module["row"], module["col"])
        if cell in cells:
            errors.append(f"modules {cells[cell]} and {module['id']} share row {cell[0]}, col {cell[1]}")
        cells[cell] = module["id"]

    externals = set()
    for external in data["externals"]:
        missing = [field for field in EXTERNAL_FIELDS if field not in external]
        if missing:
            errors.append(f"external {external.get('id', '?')}: missing {', '.join(missing)}")
        if external["id"] in modules or external["id"] in externals:
            errors.append(f"external {external['id']}: duplicate id")
        externals.add(external["id"])

    for edge in data["edges"]:
        source, target = edge[0], edge[1]
        kind = edge[2] if len(edge) > 2 else "uses"
        if source not in modules:
            errors.append(f"edge {edge}: unknown source")
        if target not in modules and target not in externals:
            errors.append(f"edge {edge}: unknown target")
        if kind not in EDGE_TYPES:
            errors.append(f"edge {edge}: unknown type {kind}")
        if (target in externals) != (kind == "external"):
            errors.append(f"edge {edge}: type external is for edges to externals only")

    for note in data["notes"]:
        if note.get("level") not in NOTE_LEVELS:
            errors.append(f"note {note.get('title', '?')}: level must be bad, warn or ok")

    return errors


def main():
    if len(sys.argv) != 3:
        print("usage: python build.py <map.json> <output.html>")
        return 2

    data = load_map(sys.argv[1])
    errors = validate_source(data["source"])
    if errors:
        print("\n".join(errors))
        return 1

    errors = validate(data)
    files = source_files(data["source"], repo_files(repo_root()))
    file_to_module, uncovered, duplicated = assign_files(data, files)
    errors += [f"file {path}: not in any module" for path in uncovered]
    errors += [f"file {path}: in several modules {', '.join(ids)}" for path, ids in duplicated.items()]

    if errors:
        print("\n".join(errors))
        return 1

    template = (Path(__file__).parent / "template.html").read_text(encoding="utf-8")
    payload = json.dumps(data, ensure_ascii=False).replace("</", "<\\/")
    page = template.replace("__MAP_TITLE__", html.escape(data["title"])).replace("__MAP_DATA__", payload)
    Path(sys.argv[2]).write_text(page, encoding="utf-8")
    print(
        f"Built {sys.argv[2]}: {len(data['modules'])} modules, {len(data['externals'])} externals, "
        f"{len(data['edges'])} edges, {len(file_to_module)} source files assigned"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
