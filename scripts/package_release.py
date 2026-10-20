"""Build GitHub assets without databases or user files. Python standard library only."""
from pathlib import Path
import hashlib
import json
import re
import sys
import zipfile

root, out = map(Path, sys.argv[1:3])
repository, version = sys.argv[3:5]
assert re.fullmatch(r'[A-Za-z0-9-]+/[A-Za-z0-9_.-]+', repository)
assert re.fullmatch(r'\d+\.\d+\.\d+', version)
out.mkdir(parents=True, exist_ok=True)
groups = {'application': [], 'database-engine': [], 'runtime': []}
for path in root.rglob('*'):
    if not path.is_file():
        continue
    relative = path.relative_to(root).as_posix()
    parts = relative.split('/')
    managed = len(parts) == 1 and relative.endswith(('.dll', '.exe', '.deps.json', '.runtimeconfig.json'))
    resource = len(parts) == 2 and re.fullmatch(r'[a-z]{2,3}(-[A-Za-z]{2,8})?', parts[0]) and relative.endswith('.resources.dll')
    if not (managed or resource):
        continue
    group = 'application' if relative.startswith('Tickets') else 'database-engine' if relative.startswith(('Microsoft.Data.Sqlite', 'SQLitePCLRaw', 'e_sqlite3')) else 'runtime'
    groups[group].append((path, relative))

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

manifest = {'Format': 1, 'Repository': repository, 'Version': version, 'Packages': []}
for group, files in groups.items():
    if not files:
        continue
    name = f'Tickets-{group}.zip'
    target = out/name
    entries = []
    with zipfile.ZipFile(target, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path, relative in sorted(files):
            archive.write(path, relative)
            entries.append({'Path': relative, 'Sha256': digest(path)})
    with zipfile.ZipFile(target) as archive:
        assert archive.testzip() is None
        assert not any(n.lower().startswith(('data/', 'backups/', 'source/')) for n in archive.namelist())
    manifest['Packages'].append({'Name': group, 'Asset': name, 'Sha256': digest(target), 'Files': entries})
    print(name, target.stat().st_size, 'bytes', len(entries), 'files')
(out/'update-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print('No user data included. Publish all assets to release tag: v'+version)
