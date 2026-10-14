"""Keep a local demo running with persistent, isolated data and test accounts."""
import json
import hashlib
import base64
import struct
import os
from pathlib import Path
import socket
import subprocess
import time
import urllib.request

import psycopg

ROOT = Path(__file__).resolve().parents[1]
PG = Path(os.environ.get('BOOK_TEST_PG_BIN', 'C:/Program Files/PostgreSQL/17/bin'))
DEMO = (ROOT / 'artifacts' / 'manual-demo').resolve()
assert DEMO.is_relative_to(ROOT.resolve())
DEMO.mkdir(parents=True, exist_ok=True)
DATA = DEMO / 'pgdata'
STATE = DEMO / 'server.json'
ACCOUNTS = [
    ('admin_test', 'AdminTest@2026', 'Quản trị viên dùng thử', 0),
    ('user_test', 'UserTest@2026', 'Người đọc dùng thử', 1),
]


def free_port():
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 0))
        return probe.getsockname()[1]


def pg(*args):
    subprocess.run([str(PG / 'pg_ctl.exe'), '-D', str(DATA), *args],
                   check=True, stdout=subprocess.DEVNULL)


if not DATA.exists():
    subprocess.run([str(PG / 'initdb.exe'), '-D', str(DATA), '-U', 'demo_owner',
                    '-A', 'trust', '--encoding=UTF8', '--locale=C'],
                   check=True, stdout=subprocess.DEVNULL)

with socket.socket() as probe:
    try:
        probe.bind(('127.0.0.1', 5180))
    except OSError as error:
        raise RuntimeError('Port 5180 is in use. Stop the previous preview before starting the demo.') from error

db_port = free_port()
url = 'http://localhost:5180'
app = None
log = None
started = False
try:
    pg('-l', str(DEMO / 'postgres.log'), '-o', f'-h 127.0.0.1 -p {db_port}', '-w', 'start')
    started = True
    env = {key: value for key, value in os.environ.items() if not key.startswith('DB_')}
    env['ASPNETCORE_ENVIRONMENT'] = 'Development'
    env['Logging__LogLevel__Default'] = 'Warning'
    env['Logging__LogLevel__Microsoft.EntityFrameworkCore'] = 'Warning'
    connection = f'Host=127.0.0.1;Port={db_port};Database=postgres;Username=demo_owner'
    log = (DEMO / 'app.log').open('w', encoding='utf-8')
    app = subprocess.Popen([
        'dotnet', str(ROOT / 'bin/Debug/net9.0/BookInfoFinder.dll'),
        '--contentRoot', str(ROOT), '--environment', 'Development',
        '--urls', url, '--ConnectionStrings:DefaultConnection', connection,
    ], cwd=DEMO, env=env, stdout=log, stderr=log,
        creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
    for _ in range(90):
        if app.poll() is not None:
            raise RuntimeError('Demo app stopped. See artifacts/manual-demo/app.log.')
        try:
            urllib.request.urlopen(url, timeout=2).close()
            break
        except Exception:
            time.sleep(.5)
    else:
        raise RuntimeError('Demo app did not start.')

    with psycopg.connect(host='127.0.0.1', port=db_port, dbname='postgres', user='demo_owner') as db:
        for username, password, full_name, role in ACCOUNTS:
            salt = os.urandom(16)
            key = hashlib.pbkdf2_hmac("sha512", password.encode(), salt, 100000, 32)
            password = base64.b64encode(b"\x01" + struct.pack(">III", 2, 100000, 16) + salt + key).decode()
            existing = db.execute('SELECT "UserId" FROM "Users" WHERE "UserName"=%s', (username,)).fetchone()
            if existing:
                db.execute('UPDATE "Users" SET "Password"=%s,"Role"=%s,"Status"=1 WHERE "UserId"=%s',
                           (password, role, existing[0]))
            else:
                db.execute('INSERT INTO "Users" ("FullName","UserName","Password","Email","CreatedAt","Role","Status") VALUES (%s,%s,%s,%s,NOW(),%s,1)',
                           (full_name, username, password, username + '@example.invalid', role))

    STATE.write_text(json.dumps({'url': url, 'appPid': app.pid, 'databasePort': db_port,
                                 'status': 'running'}, indent=2), encoding='utf-8')
    print(f'Demo ready: {url}', flush=True)
    print('Accounts ready: admin_test (Admin), user_test (User)', flush=True)
    print('Local demo data is preserved under artifacts/manual-demo.', flush=True)
    app.wait()
finally:
    if app is not None and app.poll() is None:
        app.terminate()
        try:
            app.wait(timeout=10)
        except subprocess.TimeoutExpired:
            app.kill()
            app.wait()
    if log:
        log.close()
    if started:
        pg('-m', 'fast', '-w', 'stop')
    if STATE.exists():
        state = json.loads(STATE.read_text(encoding='utf-8'))
        state['status'] = 'stopped'
        STATE.write_text(json.dumps(state, indent=2), encoding='utf-8')
