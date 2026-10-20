"""Restore the deduplicated public test database. Never overwrites a database."""
import gzip
import json
from pathlib import Path
import sqlite3
import sys

root = Path(__file__).resolve().parents[1]
source = root/'data/seed.snapshot.json.gz'
target = Path(sys.argv[1]) if len(sys.argv)>1 else root/'data/seed.db'
if target.exists():
    raise SystemExit('Refusing to overwrite '+str(target))
doc = json.loads(gzip.decompress(source.read_bytes()))
target.parent.mkdir(parents=True, exist_ok=True)
c = sqlite3.connect(target)
c.execute('PRAGMA foreign_keys=ON')
for sql in doc['schema']:
    c.execute(sql)
for table, data in doc['tables'].items():
    c.executemany('INSERT INTO "'+table+'" VALUES('+','.join('?' for _ in data['columns'])+')', data['rows'])
for event, index in doc['events_to_prices']:
    c.executemany('INSERT INTO event_price VALUES(?,?,?,?)', [(event,*row) for row in doc['price_sets'][index]])
for sql in doc['indexes']:
    c.execute(sql)
c.execute('PRAGMA user_version='+str(int(doc['user_version'])))
c.commit()
assert c.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
assert not list(c.execute('PRAGMA foreign_key_check'))
assert c.execute('SELECT count(*) FROM ticket').fetchone()[0]==0
print('Restored',target,'events:',c.execute('SELECT count(*) FROM event').fetchone()[0])
c.close()
