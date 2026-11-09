---
name: module-map
description: Build or rebuild a repository's module map (modules, dependencies, project boundaries, external libraries) from the current code and publish it as an artifact.
disable-model-invocation: true
---

# Module map

The map is an interactive HTML page that shows every behavioral **module** of a codebase inside its project, with wires for dependencies. All content lives in the repository's `docs/module-map.json`; the scripts and `template.html` next to this file render and check it for any repository. The map file is the single source of truth and records the commit it describes (`commit`, `date`), so each run is a diff against the previous map.

Run the scripts from the repository root; `<skill>` below is this file's directory (`$HOME/.claude/skills/module-map`):

- `python <skill>/scan.py docs/module-map.json` prints coverage (source files in no module or in several), module dependencies found in code marked `[in map]` or `[NEW]`, map edges with no reference in code, and external imports and native libraries per module.
- `python <skill>/build.py docs/module-map.json <scratchpad>/<repo>-module-map.html` validates the map and writes the page. It fails on unknown ids, edge types, profiles, overlapping grid cells and files outside or in several modules, and prints `Built ...` on success.

## Steps

1. **Baseline.** With no `docs/module-map.json`, this is a first map: read the build files (solution, `Cargo.toml`, `pom.xml` / `settings.gradle`, `CMakeLists.txt`) and the source tree, write the `source` block, `projects` and a first cut of `modules` with `files`, and continue at step 2. Otherwise read the map, run `git log --oneline <commit>..HEAD` and `git status --short` to see what changed since it was made. A dirty working tree means the code is mid-change: tell the user and ask whether to map the working tree or wait. Done when you can name every commit and uncommitted area that may affect modules, or, for a first map, every source file has a module.

2. **Scan** and read the files behind every `[NEW]`, every unreferenced edge and every uncovered file. Matching by names and includes has false positives (a member named like a type) and misses relations made only through events, markup, macros or reflection, so each line is a lead to verify in the code, not a verdict. Without a profile the scan reports coverage only: find dependencies and external libraries by reading imports and calls. Done when every line of the scan output is either confirmed and planned as a change to the map, or dismissed with a reason.

3. **Update the map** following the reference below: modules, files, edges, externals, notes, `commit` (short HEAD hash, plus `+dirty` when mapping a dirty tree) and `date`. Rewrite each `note` and `iface` the changed code touches, and rewrite the coupling `notes` list from what the map now shows: drop hotspots that are gone, add new cycles and new boundary crossings.

4. **Build.** Done when it prints `Built ...`. Then scan again: done when coverage is clean and every remaining `[NEW]` or unreferenced edge is one you dismissed in step 2.

5. **Publish** the built file with the Artifact tool to the map's `artifact` URL (read the artifact first when this conversation has not published it). With no `artifact`, publish a new one and write its URL into the map. Tell the user what changed on the map: modules added, removed or reshaped, new or removed cycles and boundary crossings, portability changes.

## Reference

### What a module is

A piece of behavior with a public interface and a hidden implementation: a feature (UI or command plus its views), a service, an adapter over a library or the OS, a utility, app infrastructure, or the shared model. Group by behavior, not by namespace or folder: one module may span namespaces, and one namespace may hold several modules. Every source file belongs to exactly one module through `files`; a file shared by many modules for structural reasons only (a base class) goes into the top-level `shared` list. A **project** is a build unit: a .NET project, a Cargo crate, a Maven or Gradle module, a CMake target.

### Language profiles

`source.profile` picks how the scan reads the code:

| Profile | Default suffixes | Dependencies through | External signals |
|---|---|---|---|
| `csharp` | `.cs` | type names | `using`s outside the solution's namespaces and `System`, quoted `.dll`/`.so`/`.dylib` |
| `java` | `.java` | type names | `import`s outside the declared packages and `java.`/`javax.`/`jdk.`, `System.loadLibrary`, JNA `Native.load` |
| `rust` | `.rs` | type names | paths through crates from the `Cargo.toml` dependency tables other than the workspace's own, `#[link(name)]` |
| `cpp` | C and C++ sources and headers | type names (any case) and `#include`s that resolve to repository files | includes that resolve to no repository file and are not standard headers, `#pragma comment(lib)`, quoted `.dll`/`.so` |

Any other language goes without a profile: give `suffixes`, and the scan checks coverage only.

### Map fields

- `source`: `root` (the folder `files` globs are relative to; `.` for the repository root), `profile`, `suffixes` (overrides the profile's, required without one; add markup files the modules own, such as `.axaml`), `exclude` (globs relative to `root`, for tests, generated code and vendored libraries).
- `artifact`: the published page's URL. `packageKind`: what an external package is called in this ecosystem (`NuGet package`, `crate`, `Maven artifact`, `library`); `package` by default.
- `projects[]`: `id` (the module `project` value), `title`, `sub` (`path · summary`; narrow projects show only the summary). Frames get colors in list order.
- `rows[]`: grid rows top to bottom; `lane` labels the row, `gapBefore` adds space (use it between projects stacked vertically).
- `modules[]`: `id`, `name`, `kind` (`feature`, `service`, `adapter`, `utility`, `infra`, `model`), `project`, `ns` (namespace, package or module path), `col`, `row`, `portability`, `iface` (public interface; the part before the first comma shows on the box), `impl` (hidden implementation), `libs`, `note` (one or two sentences on what it does), `files` (glob patterns relative to `source.root`; `*` crosses folders).
- `externals[]`: `id` (prefix `x-`), `name`, `sub`, `kind` (`package` or `native`), `projects`, `note`. Leave the standard library and anything nearly every module uses (logging) off the map, and say so in `notDrawn`.
- `edges[]`: `[from, to, type, via]`, where `type` defaults to `uses` and `via` names the types or members the dependency goes through:
  - `uses`: calls or holds the target;
  - `listens`: subscribes to the target's events or callbacks;
  - `implements`: implements an interface or trait the target owns (adapter to port);
  - `cycle`: one direction of a mutual dependency; give both directions this type;
  - `wiring`: composition root or `main` wiring, hidden behind a toggle; the scan ignores other references from a module that has wiring edges;
  - `external`: a module using an external library.
- `notes[]`: coupling hotspots, `level` `bad` (cycles), `warn` (couplings worth fixing) or `ok` (intentional crossings and good seams).

### Layout

The page computes positions: `col` × 205 px and the row order give each box, and project frames wrap their modules. Keep dependents above their dependencies: entry points and features at the top, then infrastructure, then domain services, then I/O and storage adapters, model and utilities at the bottom. Place an adapter next to the module whose interface it implements, and a module next to the modules it talks to most, so wires stay short. A fractional `col` (for example `2.5`) centers a box between columns; a project placed beside another one starts at a fractional column that leaves its frame clear of the neighbour's.

### Portability

`3` (high): no dependency on other modules beyond the model, or only on a library it wraps. `2` (medium): depends on a few interfaces or one concrete module. `1` (low): app-specific wiring, or depends on several concrete modules.
