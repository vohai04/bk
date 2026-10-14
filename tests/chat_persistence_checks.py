"""Cover durability, grounded recommendations and private chat history on an isolated DB."""
import io
import os
import secrets
import socket
import subprocess
import tempfile
import time
import urllib.request
from pathlib import Path
import psycopg
from PIL import Image
from playwright.sync_api import sync_playwright, expect

ROOT = Path(__file__).resolve().parents[1]
BASE = os.environ['BOOK_TEST_URL']
db = psycopg.connect(os.environ['BOOK_TEST_DSN'], autocommit=True)
assert db.info.host in ('localhost', '127.0.0.1', '::1')
suffix = secrets.token_hex(5)
title = 'Book beyond first ten ' + suffix
child = None
legacy_file = ROOT / 'wwwroot/images' / ('persistence_check_' + suffix + '.png')

def passed(label): print('PASS:', label, flush=True)
def token(page): return page.locator('input[name="__RequestVerificationToken"]').first.input_value()
def chat(page, question):
    response = page.request.post(BASE + '/Index?handler=Chat', form={'message':question}, headers={'RequestVerificationToken':token(page)})
    assert response.status == 200, response.text()
    return response.json()['reply']
def login(page, name, password):
    page.goto(BASE + '/Account/Login')
    page.locator('input[name="LoginRequest.Username"]').fill(name)
    page.locator('input[name="LoginRequest.Password"]').fill(password)
    page.locator('button[type="submit"]').click()
    page.wait_for_load_state('networkidle')

try:
    third = db.execute('INSERT INTO "Users" ("UserName","FullName","Password","Email","Role","Status","CreatedAt") VALUES (%s,%s,%s,%s,1,1,now()) RETURNING "UserId"',
        ('persist_'+suffix,'Private fixture','temporary','persist_'+suffix+'@example.invalid')).fetchone()[0]
    for index in range(12):
        db.execute('INSERT INTO "Books" ("Title","ISBN","AuthorId","CategoryId","PublisherId","UserId","PublicationDate","Description","Abstract") VALUES (%s,%s,1,2,1,1,\'2025-01-01\',\'Fixture\',%s)',
            ('Fixture '+suffix+' '+str(index),suffix+str(index),'A public book description'))
    target = db.execute('INSERT INTO "Books" ("Title","ISBN","AuthorId","CategoryId","PublisherId","UserId","PublicationDate","Description","Abstract") VALUES (%s,%s,1,2,1,1,\'2026-01-01\',\'Fixture\',%s) RETURNING "BookId"',
        (title,suffix+'T','Distinct public content '+suffix)).fetchone()[0]
    for user in (1,2): db.execute('INSERT INTO "Ratings" ("BookId","UserId","Star","Review","CreatedAt") VALUES (%s,%s,5,\'Good fixture review\',now())',(target,user))
    for user in (1,2,third): db.execute('INSERT INTO "Favorites" ("BookId","UserId","CreatedAt") VALUES (%s,%s,now())',(target,user))
    png = io.BytesIO(); Image.new('RGB',(480,720),'#2d6b53').save(png,format='PNG')

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, channel=os.environ.get('BOOK_TEST_BROWSER_CHANNEL','msedge'))
        guest_context = browser.new_context(viewport={'width':1100,'height':780})
        guest = guest_context.new_page(); guest.goto(BASE)
        best = chat(guest,'tìm quyển sách hay nhát')
        assert title in best and 'đánh giá' in best, best
        hot = chat(guest,'tìm sách hot, được yêu thích')
        assert title in hot and '3 lượt yêu thích' in hot, hot
        assert title in chat(guest,'sách hay nhất thể loại Science Fiction')
        assert '0/5' not in best, best
        passed('Recommendations use ratings/favorites, tolerate accents and include books beyond the first ten')
        assert title in chat(guest,'Tìm sách '+title)
        passed('Public catalog search fallback works without a Gemini key')

        history = guest.request.get(BASE+'/Index?handler=ChatHistory').json()['messages']
        assert len(history)==8
        assert all('sessionId' not in message for message in history)
        assert history[0]['createdAt'].endswith('Z')
        assert sum(message['text']=='tìm quyển sách hay nhát' for message in history)==1
        cookie = next(cookie for cookie in guest_context.cookies() if cookie['name']=='BookInfoFinder.Chat')
        assert cookie['httpOnly'] and len(cookie['value'])==64
        guest.get_by_role('button',name='Mở trợ lý tìm sách').click()
        expect(guest.locator('#chatMessages .chat-message--user')).to_have_count(4)
        before_times = guest.locator('#chatMessages time').evaluate_all('(els)=>els.map(el=>el.dateTime)')
        guest.goto(BASE+'/Privacy')
        expect(guest.locator('#chatbotWindow')).to_be_visible()
        expect(guest.locator('#chatMessages .chat-message--user')).to_have_count(4)
        assert guest.locator('#chatMessages time').evaluate_all('(els)=>els.map(el=>el.dateTime)')==before_times
        guest.reload(); expect(guest.locator('#chatMessages .chat-message--user')).to_have_count(4)
        outsider = browser.new_context().new_page(); outsider.goto(BASE)
        assert outsider.request.get(BASE+'/Index?handler=ChatHistory&sessionId=user:2').json()['messages']==[]
        passed('History/open state survives navigation and reload, preserves timestamps and is isolated from another browser')

        conversation = 'guest:'+cookie['value']
        db.execute('INSERT INTO "Chatbots" ("SessionId","Role","Message","CreatedAt") VALUES (%s,\'user\',\'Pending fixture\',now())',(conversation,))
        guest.goto(BASE+'/Privacy')
        expect(guest.locator('.chat-message--pending')).to_be_visible()
        db.execute('INSERT INTO "Chatbots" ("SessionId","Role","Message","CreatedAt") VALUES (%s,\'assistant\',\'Completed after navigation\',now())',(conversation,))
        expect(guest.locator('#chatMessages')).to_contain_text('Completed after navigation',timeout=10000)
        expect(guest.locator('.chat-message--pending')).to_have_count(0)
        passed('An in-flight answer can be recovered after navigating away')

        reader = browser.new_context().new_page(); login(reader,'johndoe','user123'); reader.goto(BASE)
        assert reader.request.get(BASE+'/Index?handler=ChatHistory').json()['messages']==[]
        assert chat(reader,'Có bao nhiêu sách?')
        admin = browser.new_context().new_page(); login(admin,'admin','admin123')
        assert admin.request.get(BASE+'/Index?handler=ChatHistory').json()['messages']==[]
        passed('Account transcripts do not leak between logged-in users or anonymous browsers')

        admin.goto(BASE+f'/Admin/UpdateBook/{target}')
        fields={'Book.BookId':str(target),'Book.Title':title,'Book.ISBN':'9781234567890','Book.AuthorId':'1','Book.CategoryId':'2','Book.PublisherId':'1','Book.UserId':'1','Book.PublicationDate':'2026-01-01','Book.Description':'Fixture','Book.Abstract':'Public description'}
        headers={'RequestVerificationToken':token(admin),'X-Requested-With':'XMLHttpRequest'}
        response=admin.request.post(BASE+f'/Admin/UpdateBook/{target}',headers=headers,multipart={**fields,'ImageFile':{'name':'cover.png','mimeType':'image/png','buffer':png.getvalue()}})
        assert response.status==200 and '/Admin/ManageBook' in response.url, response.text()[:700]
        cover=db.execute('SELECT "ImageBase64" FROM "Books" WHERE "BookId"=%s',(target,)).fetchone()[0]
        assert cover.startswith('data:image/webp;base64,')
        response=admin.request.post(BASE+f'/Admin/UpdateBook/{target}',headers=headers,multipart={**fields,'ImageFile':{'name':'fake.png','mimeType':'image/png','buffer':b'not an image'}})
        assert '/Admin/UpdateBook/' in response.url
        assert db.execute('SELECT "ImageBase64" FROM "Books" WHERE "BookId"=%s',(target,)).fetchone()[0]==cover
        response=admin.request.post(BASE+f'/Admin/UpdateBook/{target}',headers=headers,form=fields)
        assert '/Admin/ManageBook' in response.url
        assert db.execute('SELECT "ImageBase64" FROM "Books" WHERE "BookId"=%s',(target,)).fetchone()[0]==cover
        passed('Uploaded cover is stored as normalized DB bytes; invalid uploads and edits without a file preserve the existing cover')

        admin.goto(BASE+'/Admin/AddBook')
        create_fields={**fields,'Book.Title':'Created cover '+suffix,'Book.ISBN':'9781234567891'}
        create_fields.pop('Book.BookId')
        response=admin.request.post(BASE+'/Admin/AddBook',headers={'RequestVerificationToken':token(admin),'X-Requested-With':'XMLHttpRequest'},multipart={**create_fields,'ImageFile':{'name':'new.jpg','mimeType':'image/jpeg','buffer':png.getvalue()}})
        assert response.json()['success'], response.text()
        new_id=response.json()['bookId']
        created_cover=db.execute('SELECT "ImageBase64" FROM "Books" WHERE "BookId"=%s',(new_id,)).fetchone()[0]
        assert created_cover.startswith('data:image/webp;base64,')
        passed('New-book uploads also store decoded image bytes rather than local file paths or a claimed MIME type')

        legacy_file.write_bytes(png.getvalue())
        db.execute('UPDATE "Books" SET "ImageBase64"=%s WHERE "BookId"=1',('/images/'+legacy_file.name,))
        before_count=db.execute('SELECT count(*) FROM "Books"').fetchone()[0]
        with socket.socket() as probe: probe.bind(('127.0.0.1',0)); port=probe.getsockname()[1]
        second=f'http://localhost:{port}'
        env=os.environ.copy()
        for key in list(env):
            if key.startswith('DB_') or key.startswith('GEMINI'): del env[key]
        connection=f'Host=127.0.0.1;Port={db.info.port};Database=postgres;Username=ui_test'
        with tempfile.TemporaryDirectory(prefix='bookinfo-restart-') as cwd:
            with (ROOT/'artifacts/persistence-restart.log').open('w',encoding='utf-8') as log:
                child=subprocess.Popen(['dotnet',str(ROOT/'bin/Debug/net9.0/BookInfoFinder.dll'),'--contentRoot',str(ROOT),'--environment','Development','--urls',second,'--ConnectionStrings:DefaultConnection',connection],cwd=cwd,env=env,stdout=log,stderr=log)
                for attempt in range(60):
                    try: urllib.request.urlopen(second,timeout=2).close(); break
                    except Exception: time.sleep(.3)
                else: raise RuntimeError('Second app did not start')
                assert db.execute('SELECT count(*) FROM "Books"').fetchone()[0]==before_count
                assert db.execute('SELECT "ImageBase64" FROM "Books" WHERE "BookId"=%s',(target,)).fetchone()[0]==cover
                assert db.execute('SELECT "ImageBase64" FROM "Books" WHERE "BookId"=%s',(new_id,)).fetchone()[0]==created_cover
                assert db.execute('SELECT "ImageBase64" FROM "Books" WHERE "BookId"=1').fetchone()[0].startswith('data:image/webp;base64,')
                guest.goto(second+'/Privacy')
                guest.get_by_role('button',name='Mở trợ lý tìm sách').click()
                expect(guest.locator('#chatMessages')).to_contain_text('Completed after navigation')
                guest.screenshot(path=str(ROOT/'artifacts/chat-persistence.png'))
                child.terminate(); child.wait(timeout=10); child=None
        passed('A new app process preserves DB records, covers and guest chat; available legacy cover files are imported')
        browser.close()
finally:
    if child: child.terminate(); child.wait(timeout=10)
    if legacy_file.exists(): legacy_file.unlink()
    db.close()
