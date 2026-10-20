from pathlib import Path
import shutil
import sys
import zipfile

root, output = map(Path, sys.argv[1:])
for item in ['Data/tickets.db', 'Data/session.lock']:
    (root/item).unlink(missing_ok=True)
for path in (root/'Data').glob('*.before-v2.db'):
    path.unlink()
for directory in ['Backups', '.updates']:
    if (root/directory).exists():
        shutil.rmtree(root/directory)
shutil.copyfile(Path(__file__).resolve().parents[1]/'README.txt', root/'README.txt')
with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for path in root.rglob('*'):
        if path.is_file() and path.suffix!='.pdb':
            z.write(path, Path('Tickets')/path.relative_to(root))
with zipfile.ZipFile(output) as z:
    assert z.testzip() is None
    assert 'Tickets/Tickets.exe' in z.namelist()
    assert 'Tickets/Data/seed.db' in z.namelist()
    assert 'Tickets/Data/tickets.db' not in z.namelist()
print('Verified portable ZIP:',output)
