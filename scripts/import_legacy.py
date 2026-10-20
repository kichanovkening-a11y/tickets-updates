"""One-time MDB -> SQLite seed; deliberately does not read old ticket records."""
import csv
import io
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import sys

source, target, mdb_bin, mdb_lib = sys.argv[1:]
env = dict(os.environ, LD_LIBRARY_PATH=mdb_lib, MDB_JET3_CHARSET="CP1251")

def read(name):
    data = subprocess.check_output([mdb_bin, source, name], env=env).decode("utf-8")
    return list(csv.DictReader(io.StringIO(data)))

tables = {name: read(name) for name in (
    "theatre", "zone", "zone_dict", "place", "perfomance", "event", "price_place", "price_dict"
)}
Path(target).parent.mkdir(parents=True, exist_ok=True)
if Path(target).exists():
    raise SystemExit("Target exists; refusing to overwrite")
db = sqlite3.connect(target)
db.executescript('''
PRAGMA foreign_keys=ON;
CREATE TABLE venue(id INTEGER PRIMARY KEY, name TEXT NOT NULL, address TEXT NOT NULL);
CREATE TABLE sector(venue_id INTEGER NOT NULL REFERENCES venue(id), id INTEGER NOT NULL,
 name TEXT NOT NULL, x REAL NOT NULL, y REAL NOT NULL, PRIMARY KEY(venue_id,id));
CREATE TABLE seat(id INTEGER PRIMARY KEY, venue_id INTEGER NOT NULL, sector_id INTEGER NOT NULL,
 row_label TEXT NOT NULL, number TEXT NOT NULL, x REAL NOT NULL, y REAL NOT NULL,
 FOREIGN KEY(venue_id,sector_id) REFERENCES sector(venue_id,id));
CREATE INDEX seat_venue ON seat(venue_id);
CREATE TABLE event(id INTEGER PRIMARY KEY, venue_id INTEGER NOT NULL REFERENCES venue(id),
 title TEXT NOT NULL, starts_at TEXT NOT NULL, legacy_date TEXT NOT NULL);
CREATE TABLE event_price(event_id INTEGER NOT NULL REFERENCES event(id),
 seat_id INTEGER NOT NULL REFERENCES seat(id), amount INTEGER NOT NULL CHECK(amount>=0),
 color TEXT NOT NULL, PRIMARY KEY(event_id,seat_id));
CREATE TABLE ticket(id INTEGER PRIMARY KEY AUTOINCREMENT, event_id INTEGER NOT NULL REFERENCES event(id),
 seat_id INTEGER NOT NULL REFERENCES seat(id), recipient TEXT NOT NULL, series TEXT NOT NULL,
 number INTEGER NOT NULL CHECK(number>0), amount INTEGER NOT NULL CHECK(amount>=0),
 issued_at TEXT NOT NULL, returned_at TEXT, UNIQUE(series,number));
CREATE UNIQUE INDEX active_seat ON ticket(event_id,seat_id) WHERE returned_at IS NULL;
CREATE INDEX ticket_event ON ticket(event_id);
CREATE TABLE metadata(key TEXT PRIMARY KEY,value TEXT NOT NULL);
PRAGMA user_version=1;
''')
venues = {int(r['id_theatre']) for r in tables['theatre']}
db.executemany("INSERT INTO venue VALUES(?,?,?)", [
    (int(r['id_theatre']), r['theatre_name'].strip(), r['theatre_adress'].strip()) for r in tables['theatre']])
names = {int(r['id_zone_dict']): r['zone_name'].strip() for r in tables['zone_dict']}
sectors = {}
for r in tables['zone']:
    key = int(r['id_theatre']), int(r['id_zone_dict'])
    if key in sectors:
        raise ValueError(f"Duplicate sector {key}")
    sectors[key] = (float(r['left_zone'] or 0), float(r['top_zone'] or 0))
    db.execute("INSERT INTO sector VALUES(?,?,?,?,?)", (*key, names.get(key[1], str(key[1])), *sectors[key]))
seats = {}
skipped = []
for r in tables['place']:
    if int(r['place_type'] or -1) != 0:
        continue
    key = int(r['id_theatre']), int(r['id_zone'])
    if key not in sectors:
        skipped.append({'seat': r['id_place'], 'reason': 'no sector'})
        continue
    sid = int(r['id_place'])
    sx, sy = sectors[key]
    seats[sid] = key[0]
    db.execute("INSERT INTO seat VALUES(?,?,?,?,?,?,?)", (
        sid, *key, r['place_row'].strip(), r['place_num'].strip(),
        sx + float(r['col'] or 0) * 18, sy + 26 + float(r['row'] or 0) * 18))
performances = {int(r['id_perfomance']): r['perfomance_name'].strip() for r in tables['perfomance']}
colors = {}
for r in tables['price_dict']:
    colors[int(r['price'])] = '#%06X' % (int(r['color']) & 0xFFFFFF)
prices = {}
for r in tables['price_place']:
    prices.setdefault(int(r['id_price']), {})[int(r['id_place'])] = int(r['price'] or 0)
from datetime import datetime
for r in tables['event']:
    eid, vid, perf = int(r['id_event']), int(r['id_theatre']), int(r['id_perfomance'])
    if vid not in venues or perf not in performances:
        skipped.append({'event': eid, 'reason': 'missing venue/performance'})
        continue
    date = r['event_datetime'].strip()
    try:
        iso = datetime.strptime(date, '%d.%m.%Y %H:%M:%S').isoformat()
    except ValueError:
        try:
            iso = datetime.strptime(date, '%d.%m.%Y %H:%M').isoformat()
        except ValueError:
            iso = date
    db.execute("INSERT INTO event VALUES(?,?,?,?,?)", (eid, vid, performances[perf], iso, date))
    for sid, amount in prices.get(int(r['id_price'] or 0), {}).items():
        if seats.get(sid) == vid and amount >= 0:
            db.execute("INSERT INTO event_price VALUES(?,?,?,?)",
                       (eid, sid, amount, colors.get(amount, '#7AA8D9')))
db.execute("INSERT INTO metadata VALUES('source',?)", (Path(source).name,))
db.execute("INSERT INTO metadata VALUES('old_tickets','not imported by user request')")
db.commit()
assert not list(db.execute('PRAGMA foreign_key_check'))
assert db.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
stats = {t: db.execute(f'SELECT count(*) FROM {t}').fetchone()[0] for t in (
    'venue','sector','seat','event','event_price','ticket')}
stats['skipped'] = skipped
Path(target).with_suffix('.import.json').write_text(json.dumps(stats,ensure_ascii=False,indent=2), encoding='utf-8')
print(json.dumps(stats, ensure_ascii=False))
