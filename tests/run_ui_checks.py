"""Run browser regression checks with an isolated PostgreSQL cluster and app process."""
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tempfile
import time
import urllib.request

ROOT=Path(__file__).resolve().parents[1]
PG=Path(os.environ.get('BOOK_TEST_PG_BIN','C:/Program Files/PostgreSQL/17/bin'))
ARTIFACTS=ROOT/'artifacts'; ARTIFACTS.mkdir(exist_ok=True)
temp_root=Path(tempfile.mkdtemp(prefix='bookinfo-ui-')).resolve()
assert temp_root.is_relative_to(Path(tempfile.gettempdir()).resolve())
pgdata=temp_root/'pgdata'
app=None
started=False
logfile=None

def free_port():
    with socket.socket() as s:
        s.bind(('127.0.0.1',0))
        return s.getsockname()[1]

try:
    dbport=free_port(); appport=free_port()
    subprocess.run([str(PG/'initdb.exe'),'-D',str(pgdata),'-U','ui_test','-A','trust','--encoding=UTF8','--locale=C'],check=True,stdout=subprocess.DEVNULL)
    subprocess.run([str(PG/'pg_ctl.exe'),'-D',str(pgdata),'-l',str(temp_root/'postgres.log'),'-o',f'-h 127.0.0.1 -p {dbport}','-w','start'],check=True,stdout=subprocess.DEVNULL)
    started=True
    env=os.environ.copy()
    for key in list(env):
        if key.startswith('DB_'): del env[key]
    env['ASPNETCORE_ENVIRONMENT']='Development'
    env['Logging__LogLevel__Default']='Warning'
    env['Logging__LogLevel__Microsoft.EntityFrameworkCore']='Warning'
    url=f'http://localhost:{appport}'
    connection=f'Host=127.0.0.1;Port={dbport};Database=postgres;Username=ui_test'
    logfile=(ARTIFACTS/'regression-server.log').open('w',encoding='utf-8')
    app=subprocess.Popen(['dotnet',str(ROOT/'bin/Debug/net9.0/BookInfoFinder.dll'),'--contentRoot',str(ROOT),'--environment','Development','--urls',url,'--ConnectionStrings:DefaultConnection',connection],cwd=temp_root,env=env,stdout=logfile,stderr=logfile)
    for attempt in range(90):
        if app.poll() is not None: raise RuntimeError('Test app stopped; see artifacts/regression-server.log')
        try:
            urllib.request.urlopen(url,timeout=2).close()
            break
        except Exception: time.sleep(.5)
    else: raise RuntimeError('Test app did not start')
    env['BOOK_TEST_URL']=url
    env['BOOK_TEST_DSN']=f'host=127.0.0.1 port={dbport} dbname=postgres user=ui_test'
    script = (ROOT / 'tests' / (sys.argv[1] if len(sys.argv) > 1 else 'ui_regression.py')).resolve()
    if not script.is_relative_to(ROOT / 'tests') or not script.is_file():
        raise RuntimeError('Choose a regression script under tests/.')
    completed=subprocess.run([sys.executable,str(script)],cwd=ROOT,env=env)
    if completed.returncode: raise RuntimeError('UI regression checks failed')
finally:
    if app:
        app.terminate()
        try: app.wait(timeout=10)
        except subprocess.TimeoutExpired: app.kill(); app.wait()
    if logfile: logfile.close()
    if started: subprocess.run([str(PG/'pg_ctl.exe'),'-D',str(pgdata),'-m','fast','-w','stop'],stdout=subprocess.DEVNULL,check=True)
    assert temp_root.is_relative_to(Path(tempfile.gettempdir()).resolve()) and temp_root.name.startswith('bookinfo-ui-')
    shutil.rmtree(temp_root)
