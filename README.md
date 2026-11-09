# module-map

A [Claude Code](https://claude.com/claude-code) skill that maps a codebase's behavioral modules, their dependencies, project boundaries and external libraries into one interactive page, and keeps that map in sync with the code.

**[Try the live example](https://sapryx.github.io/module-map/)**: the map of [Nexora](https://github.com/Sapryx/Nexora), a .NET desktop music player. Hover a module to trace its wires, click it to read its interface, implementation and every dependency in and out, and toggle external libraries and composition-root wiring.

## What it does

- Groups source files into modules by behavior (features, services, adapters, utilities, infrastructure, model) inside their projects (.NET projects, Cargo crates, Maven/Gradle modules, CMake targets).
- Draws dependencies as wires and marks the ones that cross a project boundary, form a cycle or implement another module's interface.
- Rates each module's portability and lists coupling hotspots worth fixing.
- Stores the whole map in one JSON file in your repository (`docs/module-map.json`) that records the commit it describes, so each update is a diff against the last map.
- Checks the map against the code: every source file must belong to exactly one module, and a scan reports dependencies, imports and native libraries the map is missing or has stale.

## Supported languages

| Profile | Dependencies found through | External libraries found through |
|---|---|---|
| `csharp` | type names | `using` directives, native library names |
| `java` | type names | `import` statements, `System.loadLibrary`, JNA |
| `rust` | type names | crates from `Cargo.toml`, `#[link]` |
| `cpp` | type names and `#include`s | non-repository includes, `#pragma comment(lib)`, native library names |

Other languages work without a profile: the scan checks coverage, and Claude finds dependencies by reading the code.

## Requirements

- Claude Code (the CLI, desktop app or IDE extension)
- Python 3.11 or later
- Git; the mapped code must be in a Git repository

## Installation

Clone the repository into your personal skills folder.

macOS and Linux:

```bash
git clone https://github.com/Sapryx/module-map.git ~/.claude/skills/module-map
```

Windows (PowerShell):

```powershell
git clone https://github.com/Sapryx/module-map.git "$HOME\.claude\skills\module-map"
```

To share the skill with everyone working on one repository, clone it into that repository's `.claude/skills/module-map` instead.

Restart Claude Code, or start a new session, so it picks up the skill. Update it later with `git pull` in the same folder.

## Usage

In a Claude Code session at the root of your repository, run:

```
/module-map
```

The first run reads the build files and source tree, writes `docs/module-map.json`, builds the page and publishes it as a private artifact. Later runs read the commits since the map's commit, scan the code, update the map and republish to the same link.

The scripts also run on their own from the repository root:

```bash
python ~/.claude/skills/module-map/scan.py docs/module-map.json
python ~/.claude/skills/module-map/build.py docs/module-map.json module-map.html
```

`scan.py` prints coverage, dependencies found in code (`[in map]` or `[NEW]`), map edges with no reference in code, and external libraries per module. `build.py` validates the map and writes a standalone HTML page.

## Map format

The map file holds the source settings (`source.root`, `source.profile`, optional `suffixes` and `exclude`), the projects, grid rows, modules, external libraries, edges and coupling notes. [`SKILL.md`](SKILL.md) describes every field, and [`docs/nexora-module-map.json`](docs/nexora-module-map.json) is a complete example.
