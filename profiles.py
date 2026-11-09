import posixpath
import re
import tomllib

C_COMMENTS = re.compile(r"//[^\n]*|/\*.*?\*/", re.DOTALL)
QUOTED_NATIVE = re.compile(r"\"([\w.\-]+\.(?:dll|so(?:\.\d+)*|dylib))\"")
UPPER_IDENTIFIER = re.compile(r"\b[A-Z]\w*\b")
C_HEADERS = {
    "assert.h", "complex.h", "ctype.h", "errno.h", "fenv.h", "float.h", "inttypes.h", "iso646.h", "limits.h",
    "locale.h", "math.h", "setjmp.h", "signal.h", "stdalign.h", "stdarg.h", "stdatomic.h", "stdbool.h", "stddef.h",
    "stdint.h", "stdio.h", "stdlib.h", "stdnoreturn.h", "string.h", "tgmath.h", "threads.h", "time.h", "uchar.h",
    "wchar.h", "wctype.h",
}


class Profile:
    suffixes = ()
    comments = C_COMMENTS
    definition_patterns = []
    identifier = UPPER_IDENTIFIER
    native_patterns = [QUOTED_NATIVE]

    def strip_comments(self, text):
        return self.comments.sub("", text)

    def definitions(self, text):
        return [name for pattern in self.definition_patterns for name in pattern.findall(text)]

    def references(self, text):
        return set(self.identifier.findall(text))

    def natives(self, text):
        return [name for pattern in self.native_patterns for name in pattern.findall(text)]

    def file_links(self, path, text, context):
        return []


class CSharp(Profile):
    suffixes = (".cs",)
    comments = re.compile(r"//[^\n]*|/\*.*?\*/|<!--.*?-->", re.DOTALL)
    definition_patterns = [re.compile(r"\b(?:class|interface|record|enum|struct)\s+([A-Z]\w*)")]
    using = re.compile(r"^using\s+(?:static\s+)?([\w.]+)\s*;", re.MULTILINE)
    namespace = re.compile(r"^namespace\s+([\w.]+)", re.MULTILINE)

    def context(self, texts, repo_files, prefix, root):
        return {name.split(".")[0] for text in texts.values() for name in self.namespace.findall(text)}

    def imports(self, text):
        return self.using.findall(text)

    def is_external(self, name, roots):
        return name.split(".")[0] not in roots and not name.startswith("System")


class Java(Profile):
    suffixes = (".java",)
    definition_patterns = [re.compile(r"\b(?:class|interface|enum|record)\s+([A-Z]\w*)")]
    native_patterns = [
        QUOTED_NATIVE,
        re.compile(r"\b(?:System\.loadLibrary|System\.load|Native\.load(?:Library)?)\(\s*\"([^\"]+)\""),
    ]
    import_pattern = re.compile(r"^\s*import\s+(?:static\s+)?([\w.]+?)(?:\.\*)?\s*;", re.MULTILINE)
    package = re.compile(r"^\s*package\s+([\w.]+)\s*;", re.MULTILINE)
    standard = ("java.", "javax.", "jdk.")

    def context(self, texts, repo_files, prefix, root):
        return {name for text in texts.values() for name in self.package.findall(text)}

    def imports(self, text):
        return self.import_pattern.findall(text)

    def is_external(self, name, packages):
        if name.startswith(self.standard):
            return False
        return not any(
            name == package or name.startswith(package + ".") or package.startswith(name + ".")
            for package in packages
        )


class Rust(Profile):
    suffixes = (".rs",)
    definition_patterns = [
        re.compile(r"\b(?:struct|enum|trait|union)\s+([A-Z]\w*)"),
        re.compile(r"^(?:pub(?:\([^)]*\))?\s+)?type\s+([A-Z]\w*)", re.MULTILINE),
    ]
    native_patterns = [QUOTED_NATIVE, re.compile(r"#\[link\(\s*name\s*=\s*\"([^\"]+)\"")]
    path_root = re.compile(r"(?<![\w:])([a-z_][a-z0-9_]*)::")
    extern_crate = re.compile(r"\bextern\s+crate\s+(\w+)")
    dependency_tables = ("dependencies", "dev-dependencies", "build-dependencies")

    def context(self, texts, repo_files, prefix, root):
        own = set()
        dependencies = set()
        for path in repo_files:
            if posixpath.basename(path) != "Cargo.toml":
                continue
            manifest = tomllib.loads((root / path).read_text(encoding="utf-8"))
            if "package" in manifest:
                own.add(manifest["package"]["name"])
            tables = [manifest] + list(manifest.get("target", {}).values()) + [manifest.get("workspace", {})]
            for table in tables:
                for key in self.dependency_tables:
                    dependencies.update(table.get(key, {}))
        return {name.replace("-", "_") for name in dependencies - own}

    def imports(self, text):
        return set(self.path_root.findall(text)) | set(self.extern_crate.findall(text))

    def is_external(self, name, dependencies):
        return name in dependencies


class IncludeResolver:
    def __init__(self, repo_files, prefix):
        self.files = set(repo_files)
        self.prefix = prefix
        self.by_suffix = {}
        for path in repo_files:
            parts = path.split("/")
            for index in range(len(parts)):
                self.by_suffix.setdefault("/".join(parts[index:]), []).append(path)

    def key(self, include):
        return "/".join(part for part in posixpath.normpath(include).split("/") if part not in ("..", "."))

    def exists(self, include):
        return self.key(include) in self.by_suffix

    def resolve(self, source, include):
        relative = posixpath.normpath(posixpath.join(posixpath.dirname(self.prefix + source), include))
        if relative in self.files:
            return relative
        matches = self.by_suffix.get(self.key(include), [])
        return matches[0] if len(matches) == 1 else None


class Cpp(Profile):
    suffixes = (".h", ".hh", ".hpp", ".hxx", ".inl", ".ixx", ".c", ".cc", ".cpp", ".cxx")
    definition_patterns = [
        re.compile(
            r"\b(?:class|struct|union|enum(?:\s+(?:class|struct))?)\s+(?:\[\[[^\]]*\]\]\s*)?"
            r"(?:[A-Z][A-Z0-9_]*\s+)?([A-Za-z_]\w*)\s*(?:final\s*)?[:{]"
        )
    ]
    identifier = re.compile(r"\b[A-Za-z_]\w*\b")
    native_patterns = [QUOTED_NATIVE, re.compile(r"#\s*pragma\s+comment\s*\(\s*lib\s*,\s*\"([^\"]+)\"")]
    include = re.compile(r"^\s*#\s*include\s*[<\"]([^>\"]+)[>\"]", re.MULTILINE)

    def context(self, texts, repo_files, prefix, root):
        return IncludeResolver(repo_files, prefix)

    def imports(self, text):
        return self.include.findall(text)

    def is_external(self, name, resolver):
        if name in C_HEADERS or ("/" not in name and "." not in name and name.islower()):
            return False
        return not resolver.exists(name)

    def file_links(self, path, text, resolver):
        targets = []
        for name in self.imports(text):
            target = resolver.resolve(path, name)
            if target is not None and target.startswith(resolver.prefix):
                targets.append(target[len(resolver.prefix):])
        return targets


PROFILES = {"csharp": CSharp(), "java": Java(), "rust": Rust(), "cpp": Cpp()}
