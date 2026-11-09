import fnmatch
import json
import subprocess
from pathlib import Path

from profiles import PROFILES


def repo_root():
    output = subprocess.run(["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True, check=True)
    return Path(output.stdout.strip())


def repo_files(root):
    output = subprocess.run(["git", "ls-files", "-z"], cwd=root, capture_output=True, text=True, check=True)
    return [path for path in output.stdout.split("\0") if path]


def source_prefix(source):
    folder = source.get("root", ".").strip("/")
    return "" if folder in ("", ".") else folder + "/"


def source_suffixes(source):
    if "suffixes" in source:
        return tuple(source["suffixes"])
    return PROFILES[source["profile"]].suffixes


def source_files(source, files):
    prefix = source_prefix(source)
    suffixes = source_suffixes(source)
    excluded = source.get("exclude", [])
    paths = [path[len(prefix):] for path in files if path.startswith(prefix) and path.endswith(suffixes)]
    return sorted(path for path in paths if not any(fnmatch.fnmatch(path, pattern) for pattern in excluded))


def load_map(path):
    with open(path, encoding="utf-8") as file:
        return json.load(file)


def assign_files(data, files):
    owners = {}
    for module in data["modules"]:
        for pattern in module.get("files", []):
            for path in files:
                if fnmatch.fnmatch(path, pattern):
                    owners.setdefault(path, set()).add(module["id"])
    shared = set(data.get("shared", []))
    uncovered = [path for path in files if path not in owners and path not in shared]
    duplicated = {path: sorted(ids) for path, ids in owners.items() if len(ids) > 1}
    file_to_module = {path: next(iter(ids)) for path, ids in owners.items() if len(ids) == 1}
    return file_to_module, uncovered, duplicated
