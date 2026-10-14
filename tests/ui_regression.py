"""Browser and access regression checks. Run against a Development server on port 5180.
Requires Python Playwright + psycopg. Temporary accounts are removed in finally.
"""
import json
import os
import secrets
from pathlib import Path
from urllib.parse import urlparse, parse_qs
import psycopg
from playwright.sync_api import sync_playwright, expect

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts'
OUT.mkdir(exist_ok=True)
BASE = os.environ.get('BOOK_TEST_URL', 'http://localhost:5180')
dsn = os.environ.get('BOOK_TEST_DSN')
if not dsn:
    raise RuntimeError('Use tests/run_ui_checks.py to create an isolated test database.')
conn = psycopg.connect(dsn, autocommit=True)
if conn.info.host not in ('localhost', '127.0.0.1', '::1'):
    conn.close()
    raise RuntimeError('Regression fixtures require a local database.')
fixture_ids = []
names = []
password = secrets.token_urlsafe(24)
results = []

def passed(name):
    results.append(name)
    print('PASS:', name, flush=True)

def settled(page):
    page.wait_for_load_state('networkidle')
    expect(page.locator('#loadingState')).to_be_hidden()

def login(page, username):
    page.goto(BASE + '/Account/Login')
    page.locator('#LoginRequest_Username').fill(username)
    page.locator('#LoginRequest_Password').fill(password)
    page.get_by_role('button', name='Đăng nhập', exact=True).click()
    page.wait_for_load_state('networkidle')
    assert '/Account/Login' not in page.url

try:
    for role in (0, 1):
        name = 'ui_check_' + secrets.token_hex(6)
        user_id = conn.execute('INSERT INTO "Users" ("FullName","UserName","Password","Email","CreatedAt","Role","Status") VALUES (%s,%s,%s,%s,NOW(),%s,1) RETURNING "UserId"', ('UI test', name, password, name + '@example.invalid', role)).fetchone()[0]
        names.append(name); fixture_ids.append(user_id)
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True, channel=os.environ.get('BOOK_TEST_BROWSER_CHANNEL'))
        context = browser.new_context(viewport={'width':1440,'height':1050})
        page = context.new_page()
        errors = []
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.goto(BASE); settled(page)
        baseline_count = page.locator('.book-card').count()
        assert baseline_count > 0
        assert page.locator('#bookList').evaluate('el => getComputedStyle(el).display') == 'grid'
        assert not page.locator('[id]').evaluate_all('(els)=>els.map(e=>e.id).some((id,i,a)=>a.indexOf(id)!==i)')
        page.screenshot(path=str(OUT / 'after-desktop.png'), full_page=True)
        passed('Reader layout loads actual books without duplicate IDs')
        for route in ('/Admin', '/Dashboard', '/Admin/Dashboard', '/Admin/ManageBook', '/Admin/Users', '/Admin/Report', '/Admin/AuditLog', '/Admin/Notifications', '/Admin/AddBook', '/Admin/UpdateBook/1', '/Admin/Categories', '/Admin/Tags', '/Admin/ManageAuthor', '/Admin/ManageNXB'):
            assert page.request.get(BASE + route, max_redirects=0).status == 302, route
            assert page.request.get(BASE + route + '?handler=GetStats', max_redirects=0).status == 401, route
            assert page.request.post(BASE + route + '?handler=GetStats', max_redirects=0).status == 401, route
        passed('Every admin page, alias and GET/POST handler blocks anonymous visitors')
        page.context.add_cookies([{'name':'Role','value':'Admin','url':BASE},{'name':'UserId','value':str(fixture_ids[0]),'url':BASE}])
        assert page.request.get(BASE + '/Admin/Users?handler=AjaxSearch').status == 401
        assert page.request.get(BASE + '/Notifications?handler=UnreadCount').json()['userId'] is None
        passed('Forged identity cookies cannot grant access')
        page.context.clear_cookies()
        assert page.request.get(BASE+'/images/default-book.svg').status == 200
        sample = conn.execute('SELECT "Title" FROM "Books" ORDER BY "BookId" LIMIT 1').fetchone()[0]
        page.locator('#mainSearchInput').fill(sample.upper())
        page.get_by_role('button', name='Tìm sách', exact=True).click(); settled(page)
        assert page.locator('.book-card').count() > 0
        assert parse_qs(urlparse(page.url).query)['q'][0] == sample.upper()
        page.reload(); settled(page)
        assert page.locator('#mainSearchInput').input_value() == sample.upper()
        page.locator('#mainSearchInput').fill('')
        page.get_by_role('button', name='Tìm sách', exact=True).click(); settled(page)
        assert page.locator('.book-card').count() == baseline_count
        passed('Search matches title case-insensitively, restores on reload and clears correctly')
        for sort in ('rating','favorites','year','title'):
            data=page.request.get(BASE+f'/Index?handler=Search&sort={sort}&pageSize=2').json()
            assert len(data['items']) == 2 and data['total'] >= 2
            if sort == 'rating': assert data['items'][0]['rating'] >= data['items'][1]['rating']
        data=page.request.get(BASE+'/Index?handler=Search&year=99999&pageSize=999999').json()
        assert len(data['items']) <= 48
        passed('Database sorting, paging and bounded input return valid results')
        page.locator('#mainSearchInput').fill('zzzz-no-such-book-874421')
        page.get_by_role('button', name='Tìm sách', exact=True).click(); settled(page)
        expect(page.locator('#emptyState')).to_be_visible()
        page.locator('#clearSearch').click(); settled(page)
        assert page.locator('.book-card').count() == baseline_count
        passed('No-results state offers a working reset')
        payload='<img src=x onerror="window.injected=true">'
        def mock_books(route):
            route.fulfill(json={'items':[{'id':99,'title':payload,'author':payload,'coverUrl':'javascript:alert(1)'}], 'total':18,'totalPages':2})
        page.route('**/Index?handler=Search*',mock_books)
        page.reload(); settled(page)
        assert page.locator('.book-title a').first.inner_text() == payload
        assert page.evaluate('window.injected === undefined')
        page.locator('#paginationButtons [data-page="2"]').first.click(); settled(page)
        assert parse_qs(urlparse(page.url).query)['page'] == ['2']
        page.go_back(); settled(page)
        assert 'page' not in parse_qs(urlparse(page.url).query)
        page.unroute('**/Index?handler=Search*',mock_books)
        passed('Book content renders safely; pagination and browser Back retain URL state')
        page.route('**/Index?handler=Search*', lambda route:route.abort())
        page.reload(); settled(page)
        expect(page.locator('#searchError')).to_be_visible()
        page.unroute('**/Index?handler=Search*')
        page.locator('#retrySearch').click(); settled(page)
        expect(page.locator('#searchError')).to_be_hidden()
        assert page.locator('.book-card').count() > 0
        passed('Network failure has a working retry')
        page.evaluate('document.activeElement.blur()')
        page.set_viewport_size({'width':390,'height':844})
        page.wait_for_function('document.querySelector(".reader-sidebar").getBoundingClientRect().right <= 1')
        assert not page.evaluate('document.documentElement.scrollWidth>innerWidth')
        page.screenshot(path=str(OUT/'after-mobile.png'),full_page=True)
        menu=page.locator('#workspaceMenuToggle')
        menu.click(); expect(menu).to_have_attribute('aria-expanded','true')
        page.keyboard.press('Escape'); expect(menu).to_have_attribute('aria-expanded','false')
        expect(menu).to_be_focused()
        page.get_by_role('button',name='Mở trợ lý tìm sách').click()
        expect(page.locator('#chatbotWindow')).to_be_visible()
        page.get_by_role('button',name='Đóng trợ lý').click()
        passed('390px mobile has no overflow; drawer keyboard and chat controls work')
        page.set_viewport_size({'width':1440,'height':1050})
        login(page,names[1])
        assert page.request.get(BASE+'/Admin/Dashboard?handler=GetStats').status == 403
        assert page.request.post(BASE+'/Admin/ManageBook?handler=Delete',form={'id':0}).status == 403
        passed('Ordinary users cannot read or mutate admin data')
        from reader_review_checks import run_checks as reader_checks
        reader_checks(page, conn, BASE, OUT, fixture_ids[1], fixture_ids[0], password, passed)
        admin=context.new_page()
        admin.on('pageerror', lambda error: errors.append(str(error)))
        # Clear the user session before logging into the second fixture.
        context.clear_cookies(); login(admin,names[0])
        assert urlparse(admin.url).path in ('/Admin', '/Admin/Dashboard', '/Dashboard')
        assert admin.locator('.management-sidebar').count() == 1
        assert admin.locator('.reader-sidebar').count() == 0
        assert not admin.locator('[id]').evaluate_all('(els)=>els.map(e=>e.id).some((id,i,a)=>a.indexOf(id)!==i)')
        admin.screenshot(path=str(OUT/'admin-desktop.png'),full_page=True)
        assert admin.request.get(BASE+'/Admin/Dashboard?handler=GetStats').status == 200
        assert admin.request.post(BASE+'/Admin/Dashboard?handler=GetStats').status == 400
        token=admin.locator('input[name="__RequestVerificationToken"]').first.input_value()
        assert admin.request.post(BASE+'/Admin/Dashboard?handler=GetStats',headers={'RequestVerificationToken':token}).status == 200
        assert admin.evaluate('''async () => await new Promise(resolve => $.post('/Admin/Dashboard?handler=GetStats').done(()=>resolve(true)).fail(()=>resolve(false)))''')
        passed('Admins have a separate layout; CSRF blocks missing tokens and permits jQuery mutations')
        for route in ('ManageBook','Users','Categories','Tags','ManageAuthor','ManageNXB','Report','AddBook'):
            admin.goto(BASE+'/Admin/'+route); admin.wait_for_load_state('networkidle')
            assert admin.locator('.management-sidebar').count() == 1, route
            assert not admin.locator('[id]').evaluate_all('(els)=>els.map(e=>e.id).some((id,i,a)=>a.indexOf(id)!==i)'), route
        admin.goto(BASE+'/Admin/ManageBook'); admin.wait_for_load_state('networkidle')
        assert admin.locator('img').evaluate_all('(imgs)=>imgs.every(i=>i.complete && i.naturalWidth>0)')
        admin.screenshot(path=str(OUT/'admin-books.png'),full_page=True)
        admin.set_viewport_size({'width':390,'height':844})
        admin.wait_for_function('document.querySelector(".management-sidebar").getBoundingClientRect().right <= 1')
        assert not admin.evaluate('document.documentElement.scrollWidth>innerWidth')
        admin.screenshot(path=str(OUT/'admin-mobile.png'),full_page=True)
        passed('All admin sections render; tables stay within the mobile viewport')
        admin.set_viewport_size({'width':1440,'height':1050})
        admin.goto(BASE+'/Admin/Tags'); admin.wait_for_load_state('networkidle')
        admin.route('**/Admin/Tags?handler=AjaxDelete',lambda route:route.fulfill(json={'success':True}))
        admin.locator('.btn-delete').first.click()
        expect(admin.locator('#confirmModal')).to_be_visible()
        with admin.expect_request(lambda request:'handler=AjaxDelete' in request.url) as deletion:
            admin.locator('#confirmBtn').click()
        assert deletion.value.headers.get('requestverificationtoken')
        expect(admin.locator('#confirmModal')).to_have_count(0)
        admin.unroute('**/Admin/Tags?handler=AjaxDelete')
        admin.evaluate("""() => { window.earlyConfirmed=false; showConfirmModal('Test', 'Test', () => { window.earlyConfirmed=true; }); document.querySelector('#confirmBtn').click(); }""")
        assert admin.evaluate('window.earlyConfirmed === false')
        admin.locator('#confirmBtn').click()
        expect(admin.locator('#confirmModal')).to_have_count(0)
        assert admin.evaluate('window.earlyConfirmed === true')
        passed('Confirmation waits for its opening animation and closes reliably after one action')
        admin.evaluate("showConfirmModal('<img src=x onerror=window.injected=true>', '<script>window.injected=true</script>', () => { window.confirmed=true; })")
        expect(admin.locator('#confirmModal')).to_be_visible()
        assert admin.locator('#confirmModal img, #confirmModal script').count() == 0
        admin.locator('#confirmModal').get_by_role('button',name='Hủy',exact=True).click()
        expect(admin.locator('#confirmModal')).to_have_count(0)
        assert admin.evaluate('window.injected === undefined && window.confirmed === undefined')
        passed('Tag deletion confirms before sending a token; confirmation content is safe')
        assert admin.evaluate("""async (otherUserId) => {
            const connection=new signalR.HubConnectionBuilder().withUrl('/notificationHub').build();
            await connection.start();
            try { await connection.invoke('Register',otherUserId); return false; }
            catch (_) { return true; }
            finally { await connection.stop(); }
        }""",fixture_ids[1])
        passed('SignalR prevents joining another account notification group')
        from admin_redesign_checks import run_checks
        run_checks(admin, conn, BASE, OUT, names[0], fixture_ids[0], passed)
        conn.execute('UPDATE "Users" SET "Status"=0 WHERE "UserId"=%s',(fixture_ids[0],))
        assert admin.request.get(BASE+'/Admin/Dashboard?handler=GetStats').status == 403
        passed('Disabling an admin invalidates access immediately')
        assert not errors, errors
        passed('Reader flows produce no JavaScript runtime errors')
        browser.close()
finally:
    if fixture_ids:
        conn.execute('DELETE FROM "ActivityLogs" WHERE "UserName" = ANY(%s)',(names,))
        conn.execute('DELETE FROM "Users" WHERE "UserId" = ANY(%s)',(fixture_ids,))
    conn.close()
(OUT/'test-results.json').write_text(json.dumps({'passed':results},ensure_ascii=False,indent=2),encoding='utf-8')
