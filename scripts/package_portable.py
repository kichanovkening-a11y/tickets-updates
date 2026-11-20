from pathlib import Path
import shutil
import sys
import zipfile

root, output = map(Path, sys.argv[1:3])
classic = sys.argv[3:] == ['--classic']
assert not sys.argv[3:] or classic, 'Unknown packaging option'
folder = 'Tickets-Classic' if classic else 'Tickets'
app = 'Tickets.Classic.exe' if classic else 'Tickets.exe'
for item in ['Data/tickets.db', 'Data/session.lock']:
    (root/item).unlink(missing_ok=True)
for path in (root/'Data').glob('*.before-v*.db'):
    path.unlink()
for directory in ['Backups', '.updates', 'Data/PrintBlanks', 'Data/Documents']:
    if (root/directory).exists():
        shutil.rmtree(root/directory)
if classic:
    (root/'README.txt').write_text('Билеты: классический вид\n\nЗапустите Tickets.Classic.exe.\nОбновление: Настройки → Обновление.\nДанные хранятся в папке Data рядом с программой.\n', encoding='utf-8-sig')
else:
    shutil.copyfile(Path(__file__).resolve().parents[1]/'README.txt', root/'README.txt')
with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for path in root.rglob('*'):
        if path.is_file() and path.suffix!='.pdb':
            z.write(path, Path(folder)/path.relative_to(root))
with zipfile.ZipFile(output) as z:
    assert z.testzip() is None
    assert f'{folder}/{app}' in z.namelist()
    assert f'{folder}/Data/seed.db' in z.namelist()
    assert f'{folder}/Data/tickets.db' not in z.namelist()
print('Verified portable ZIP:',output)
