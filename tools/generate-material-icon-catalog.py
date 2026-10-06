#!/usr/bin/env python3
"""Generate DecorIconCatalog from official Google Material Symbols Outlined SVGs."""

from __future__ import annotations

import re
import sys
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SVG_ROOT = ROOT / "tools" / "material-symbols"
OUTPUT = ROOT / "src" / "Decor.AvaloniaUI" / "Icons" / "DecorIconCatalog.cs"
ICON_IDS = ROOT / "src" / "Decor.AvaloniaUI" / "Icons" / "DecorIconId.cs"

# Material Symbols are in symbols/web, not in the old icon-category folders.
# This is the official Google-hosted SVG endpoint used by the upstream
# material-design-icons updater. Weight 400 uses its canonical "default"
# directory; the other static axis variants use e.g. "wght300".
BASE_URL = "https://fonts.gstatic.com/s/i/short-term/release/materialsymbolsoutlined/{icon}/{style}/24px.svg"
SUPPORTED_WEIGHTS = (100, 200, 300, 400, 500, 600, 700)

# (Decor family, Decor member, official Material Symbol name)
ICONS = [
    ("Application", "Home", "home"), ("Application", "Settings", "settings"), ("Application", "Help", "help"),
    ("Modules", "Cadastros", "dataset"), ("Modules", "Compras", "shopping_cart"), ("Modules", "Estoque", "inventory_2"),
    ("Modules", "Comercial", "storefront"), ("Modules", "Servicos", "handyman"), ("Modules", "Financeiro", "payments"), ("Modules", "Configuracoes", "settings"),
    ("Forms", "Products", "inventory_2"), ("Forms", "Brands", "sell"), ("Forms", "Classifications", "category"),
    ("Forms", "TermDelivery", "local_shipping"), ("Forms", "Users", "manage_accounts"), ("Forms", "PermissionGroups", "groups"), ("Forms", "DatabaseMaintenance", "database"),
    ("Actions", "Search", "search"), ("Actions", "View", "visibility"), ("Actions", "Create", "add"), ("Actions", "Edit", "edit"),
    ("Actions", "Delete", "delete"), ("Actions", "Save", "save"), ("Actions", "Cancel", "cancel"), ("Actions", "Add", "add"),
    ("Actions", "Remove", "remove"), ("Actions", "Report", "description"), ("Actions", "Close", "close"), ("Actions", "Clear", "cleaning_services"), ("Actions", "Copy", "content_copy"),
    ("User", "Profile", "person"), ("User", "Preferences", "tune"), ("User", "ChangePassword", "key"), ("User", "Notifications", "notifications"), ("User", "SignOut", "logout"),
    ("Common", "Calendar", "calendar_month"), ("Common", "Clock", "schedule"), ("Common", "Folder", "folder"), ("Common", "Database", "database"), ("Common", "Backup", "backup"),
    ("Navigation", "FirstPage", "first_page"), ("Navigation", "PreviousPage", "chevron_left"), ("Navigation", "NextPage", "chevron_right"), ("Navigation", "LastPage", "last_page"), ("Navigation", "Dropdown", "arrow_drop_down"),
]


@dataclass(frozen=True)
class SvgResult:
    icon: str
    weight: int
    url: str
    http_status: str
    byte_count: int
    path_data: str
    downloaded: bool


def material_url(icon: str, weight: int) -> str:
    style = "default" if weight == 400 else f"wght{weight}"
    return BASE_URL.format(icon=icon, style=style)


def cache_path(icon: str, weight: int) -> Path:
    return SVG_ROOT / icon / f"weight{weight}.svg"


def validate_svg(svg: bytes, icon: str) -> str:
    """Validate that this is a 24px SVG and safely extract all its paths."""
    try:
        root = ET.fromstring(svg)
    except ET.ParseError as exc:
        raise RuntimeError(f"{icon}: response is not valid XML/SVG: {exc}") from exc
    if root.tag.rsplit("}", 1)[-1] != "svg":
        raise RuntimeError(f"{icon}: root element is not <svg>.")
    view_box = root.attrib.get("viewBox")
    if not view_box:
        raise RuntimeError(f"{icon}: SVG has no viewBox.")
    try:
        values = [float(value) for value in re.split(r"[ ,]+", view_box.strip())]
    except ValueError as exc:
        raise RuntimeError(f"{icon}: invalid viewBox {view_box!r}.") from exc
    # The official static 24px files currently use a 960-unit design grid
    # (for example, "0 -960 960 960").  The 24px suffix denotes the target
    # icon size, not necessarily coordinate units.  Preserve that geometry:
    # Avalonia's Stretch scales a Geometry from its bounds at render time.
    if len(values) != 4 or values[2] <= 0 or values[2] != values[3]:
        raise RuntimeError(f"{icon}: expected a square positive viewBox, got {view_box!r}.")

    paths: list[str] = []
    drawing_elements = {"circle", "ellipse", "line", "polygon", "polyline", "rect", "use"}
    for element in root.iter():
        tag = element.tag.rsplit("}", 1)[-1] if isinstance(element.tag, str) else ""
        if tag == "path":
            data = element.attrib.get("d", "").strip()
            if not data:
                raise RuntimeError(f"{icon}: a <path> has an empty d attribute.")
            paths.append(data)
        elif tag in drawing_elements:
            raise RuntimeError(f"{icon}: SVG uses <{tag}>; refusing to discard non-path geometry.")
    if not paths:
        raise RuntimeError(f"{icon}: SVG contains no <path d=...> geometry.")
    return " ".join(paths)


def fetch_svg(icon: str, weight: int) -> SvgResult:
    """Fetch (or reuse) and validate one official SVG."""
    url = material_url(icon, weight)
    target = cache_path(icon, weight)
    if target.exists():
        data = target.read_bytes()
        return SvgResult(icon, weight, url, "cache (previously HTTP 200)", len(data), validate_svg(data, icon), False)
    try:
        request = urllib.request.Request(url, headers={"User-Agent": "Decor-MaterialIconCatalogGenerator/1.0"})
        with urllib.request.urlopen(request, timeout=30) as response:
            status_code = response.status
            content_type = response.headers.get_content_type()
            data = response.read()
    except urllib.error.HTTPError as exc:
        raise RuntimeError(f"{icon} weight {weight}: HTTP {exc.code}; URL: {url}") from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"{icon} weight {weight}: network error ({exc.reason}); URL: {url}") from exc
    if not 200 <= status_code < 300:
        raise RuntimeError(f"{icon} weight {weight}: HTTP {status_code}; URL: {url}")
    if content_type not in {"image/svg+xml", "application/octet-stream"}:
        raise RuntimeError(f"{icon} weight {weight}: unexpected Content-Type {content_type!r}; URL: {url}")
    geometry = validate_svg(data, icon)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)
    return SvgResult(icon, weight, url, f"HTTP {status_code}", len(data), geometry, True)


def declared_icon_ids() -> set[str]:
    return set(re.findall(r'new\("([A-Za-z]+\.[A-Za-z]+)"\)', ICON_IDS.read_text(encoding="utf-8")))


def validate_semantic_ids() -> None:
    expected = {f"{family}.{name}" for family, name, _ in ICONS}
    actual = declared_icon_ids()
    missing, unexpected = sorted(expected - actual), sorted(actual - expected)
    if missing or unexpected:
        details = ["DecorIconId.cs and the generator semantic map disagree; catalog was not written.", f"Expected: {len(expected)} IDs; declared: {len(actual)} IDs."]
        if missing:
            details.append("Missing declarations: " + ", ".join(missing))
        if unexpected:
            details.append("Unexpected declarations: " + ", ".join(unexpected))
        raise RuntimeError("\n".join(details))


def csharp_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"') + '"'


def generate_catalog(geometries: dict[int, dict[str, str]]) -> str:
    families = list(dict.fromkeys(family for family, _, _ in ICONS))
    symbols = list(dict.fromkeys(symbol for _, _, symbol in ICONS))
    lines = [
        "using System.Collections.ObjectModel;", "using Avalonia.Media;", "", "namespace Decor.AvaloniaUI.Icons;", "", "public static class DecorIconCatalog", "{",
        "    private static readonly IReadOnlyList<string> FamilyOrder = [" + ", ".join(csharp_string(family) for family in families) + "];", "",
        "    private static readonly IReadOnlyList<int> AvailableWeights = [" + ", ".join(map(str, SUPPORTED_WEIGHTS)) + "];", "",
        "    private static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<string, Lazy<Geometry>>> SymbolGeometriesByWeight =",
        "        new ReadOnlyDictionary<int, IReadOnlyDictionary<string, Lazy<Geometry>>>(new Dictionary<int, IReadOnlyDictionary<string, Lazy<Geometry>>>", "        {",
    ]
    for weight in SUPPORTED_WEIGHTS:
        lines.extend([f"            [{weight}] = new ReadOnlyDictionary<string, Lazy<Geometry>>(new Dictionary<string, Lazy<Geometry>>", "            {"])
        for symbol in symbols:
            lines.append(f"                [{csharp_string(symbol)}] = CreateGeometry({csharp_string(geometries[weight][symbol])}),")
        lines.extend(["            }),"])
    lines += [
        "        });", "",
        "    private static readonly IReadOnlyDictionary<DecorIconId, string> SymbolsById =",
        "        new ReadOnlyDictionary<DecorIconId, string>(new Dictionary<DecorIconId, string>", "        {",
    ]
    for family, name, symbol in ICONS:
        lines.append(f"            [DecorIconId.{family}.{name}] = {csharp_string(symbol)},")
    lines += [
        "        });", "", "    private static readonly IReadOnlyList<DecorIconId> RegisteredIds = Array.AsReadOnly(SymbolsById.Keys.ToArray());", "",
        "    public static IReadOnlyList<DecorIconId> Ids => RegisteredIds;", "    public static IReadOnlyList<int> SupportedWeights => AvailableWeights;", "",
        "    public static IReadOnlyList<DecorIconCatalogFamily> GetFamilyGroups() => FamilyOrder",
        "        .Select(name => new DecorIconCatalogFamily(name, RegisteredIds", "            .Where(id => id.Value.StartsWith(name + \".\", StringComparison.OrdinalIgnoreCase))",
        "            .OrderBy(id => id.Value, StringComparer.OrdinalIgnoreCase).ToArray()))", "        .Where(group => group.Ids.Count > 0).ToArray();", "",
        "    public static Geometry Get(DecorIconId id) => Get(id, 400);", "",
        "    public static Geometry Get(DecorIconId id, int weight)", "    {",
        "        if (!SymbolsById.TryGetValue(id, out var symbol))", "            throw new KeyNotFoundException($\"No vector geometry is registered for icon '{id.Value}'.\");",
        "        if (!SymbolGeometriesByWeight.TryGetValue(weight, out var geometries))", "            throw new ArgumentOutOfRangeException(nameof(weight), weight, \"The Material Symbol weight is not supported.\");",
        "        return geometries[symbol].Value;", "    }", "",
        "    private static Lazy<Geometry> CreateGeometry(string path) => new(() => Geometry.Parse(path), LazyThreadSafetyMode.ExecutionAndPublication);", "}", "",
        "public sealed record DecorIconCatalogFamily(string Name, IReadOnlyList<DecorIconId> Ids);", "",
    ]
    return "\n".join(lines)


def main() -> int:
    semantic_ids = {f"{family}.{name}" for family, name, _ in ICONS}
    symbols = list(dict.fromkeys(symbol for _, _, symbol in ICONS))
    if len(semantic_ids) != 44 or len(symbols) != 40:
        raise RuntimeError("The fixed semantic map must contain 44 IDs and 40 unique symbols.")
    print(f"Decor semantic IDs: {len(semantic_ids)}")
    print(f"Unique Material Symbols: {len(symbols)}")
    results: dict[int, dict[str, SvgResult]] = {}
    failures: list[str] = []
    for weight in SUPPORTED_WEIGHTS:
        results[weight] = {}
        for symbol in symbols:
            try:
                result = fetch_svg(symbol, weight)
                results[weight][symbol] = result
                print(f"OK   {symbol} weight {weight}: {result.http_status}; {result.byte_count} bytes; validated; {result.url}")
            except RuntimeError as exc:
                failures.append(str(exc))
                print(f"FAIL {symbol}: {exc}", file=sys.stderr)
    downloaded = sum(result.downloaded for by_symbol in results.values() for result in by_symbol.values())
    print(f"Successfully validated: {len(results)}")
    print(f"Failed: {len(failures)}")
    print(f"Downloaded: {downloaded}")
    print(f"Weight variants: {len(SUPPORTED_WEIGHTS) * len(symbols)}")
    if failures:
        raise RuntimeError("No catalog was generated because one or more Material Symbols failed validation.")
    validate_semantic_ids()  # Must pass before OUTPUT is ever touched.
    OUTPUT.write_text(generate_catalog({weight: {name: result.path_data for name, result in by_symbol.items()} for weight, by_symbol in results.items()}), encoding="utf-8", newline="\n")
    print(f"Generated: {OUTPUT}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        raise SystemExit(130)
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
