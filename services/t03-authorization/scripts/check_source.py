"""Mala staticka provera za KT1/KT2, bez dodatnih Python paketa."""
from pathlib import Path
import re
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
errors = []
for file in list(root.glob('src/**/*.cs')) + list(root.glob('tests/**/*.cs')):
    source = file.read_text(encoding='utf-8')
    if re.search(r'(?i)(password|api[_-]?key|access[_-]?token|client[_-]?secret)\s*=\s*"[^"\s]+"', source):
        errors.append(f'{file.relative_to(root)}: moguća hard-coded tajna')
for project in root.glob('**/*.csproj'):
    tree = ET.parse(project)
    for ref in tree.findall('.//ProjectReference'):
        if not (project.parent / ref.attrib['Include']).is_file():
            errors.append(f'{project.name}: nepostojeća ProjectReference')
for layer in ['Domain', 'Application', 'Infrastructure', 'ConsoleUi']:
    if not (root / f'src/Scutum.T03.{layer}/Scutum.T03.{layer}.csproj').is_file():
        errors.append(f'Nedostaje sloj {layer}')
if errors:
    raise SystemExit('\n'.join(errors))
print('PASS: struktura, reference i osnovna provera hard-coded tajni.')
print('Ovo nije zamena za build, NUnit ili potpun secrets scanner.')
