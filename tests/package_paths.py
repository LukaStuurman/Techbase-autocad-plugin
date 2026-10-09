from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ET.parse(ROOT / 'PackageContents.xml')
VERSION = MANIFEST.getroot().attrib['AppVersion']
BUNDLE = ROOT / 'dist' / VERSION / 'Techbase.Blocks.bundle'
ASSETS = BUNDLE / 'Contents' / 'Assets'
def dll(series='R25.0'):
    component = next(c for c in MANIFEST.findall('Components') if c.find('RuntimeRequirements').attrib['SeriesMin'] == series)
    return BUNDLE / component.find('ComponentEntry').attrib['ModuleName']
