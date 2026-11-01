"""Release must carry a leading launch name while old update clients keep working."""
import json, subprocess, sys, tempfile
from pathlib import Path
with tempfile.TemporaryDirectory() as d:
    root=Path(d)/'app';out=Path(d)/'out';root.mkdir()
    for name in ['Tickets.exe','Tickets.dll','Tickets.Updater.exe','Tickets.Updater.dll']:
        (root/name).write_bytes(('fixture '+name).encode())
    subprocess.run([sys.executable,str(Path(__file__).with_name('package_release.py')),str(root),str(out),'test/repo','0.46.0'],check=True)
    manifest=json.loads((out/'update-manifest.json').read_text())
    app=next(p for p in manifest['Packages'] if p['Name']=='application')
    paths={f['Path']:f['Sha256'] for f in app['Files']}
    assert (root/'! Tickets.exe').exists(),'Leading launcher missing from portable folder'
    assert (root/'! Tickets.exe').read_bytes()==(root/'Tickets.exe').read_bytes(),'Launcher differs'
    assert '! Tickets.exe' not in paths,'Old manifest readers reject spaces and punctuation'
    assert 'Tickets.exe' in paths,'Old client launcher missing'
    assert 'Tickets.Updater.exe' in paths,'Updater missing'
print('PASS leading launcher, compatibility launcher and bundled updater')
