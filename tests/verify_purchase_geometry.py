"""Verify AANKOOP geometry matches the original Stationslocaties symbol.

This is a source/geometry contract test; opening generated DWG blocks in AutoCAD
is still a separate acceptance test.
"""
from __future__ import annotations

import gzip
import json
import math
import pathlib
import re
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]
geometry = json.loads(
    gzip.decompress((ROOT / "src/Stationslocaties/Resources/StationGeometry.json.gz").read_bytes())
)
symbol = geometry["symbol"]
hatches = [s for s in symbol if s.get("k") == "HATCH"]
native = next(s for s in hatches if s.get("s") == "ANSI31" and s.get("c") == 102)
assert math.isclose(native["w"], 0.1, abs_tol=0.00001)
assert math.isclose(native["r"], -math.pi / 2, abs_tol=0.001)

# Original station: outline around the transformer (1.76 x 3.55 m),
# original green purchase area: four-sided hatch perimeter (4.30 x 5.86 m).
outline = next(
    s for s in symbol
    if s.get("k") == "LWPOLYLINE"
    and s.get("c") == 102
    and len(s.get("v", [])) == 5
    and 1.70 < max(p[0] for p in s["v"]) - min(p[0] for p in s["v"]) < 1.81
)
inner = outline["v"]
outer = native["loops"][0]
minimum = lambda v, axis: min(p[axis] for p in v)
maximum = lambda v, axis: max(p[axis] for p in v)
margins = {
    "Left": minimum(inner, 0) - minimum(outer, 0),
    "Right": maximum(outer, 0) - maximum(inner, 0),
    "Top": maximum(outer, 1) - maximum(inner, 1),
    "Bottom": minimum(inner, 1) - minimum(outer, 1),
}
source = (ROOT / "src/StationPurchaseVariants.cs").read_text(encoding="utf-8")
for key, measured in margins.items():
    match = re.search(r"\b" + key + r"\s*=\s*([0-9]+\.[0-9]+)", source)
    assert match, f"Missing source margin constant {key}"
    actual = float(match.group(1))
    assert abs(actual - measured) < 0.02, (key, actual, measured)
assert '"ANSI31"' in source and 'Green = 102' in source
assert "Suffix = \" AANKOOP\"" in source

# The actual shipped block palette includes PACTO 25 on the station layer.
palette = ET.parse(ROOT / "Assets/Palette/Techbase toolpallete.xtp")
tools = []
for tool in palette.findall(".//Palette/Tools/Tool"):
    block = tool.find("./Data/Block")
    if block is None:
        continue
    name = (block.findtext("BlockName") or "").strip()
    layer = tool.find("./Data/GeneralProperties/Layer/BasicPropInfo/PropValue")
    if layer is not None and layer.get("value") == "01 - Station":
        tools.append(name)
assert any("PACTO 25" in t.upper() for t in tools), tools
assert len(tools) >= 3, tools

print("AANKOOP source geometry matches the Stationslocaties green ANSI31 hatch and margins.")
print("Original station block examples:", ", ".join(tools))
print("Margins (m):", {name: round(value, 3) for name, value in margins.items()})
