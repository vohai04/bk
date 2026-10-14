"""Real favorite toggling against an isolated local database."""
import os
import secrets
from pathlib import Path
import psycopg
from playwright.sync_api import sync_playwright, expect

base = os.environ['BOOK_TEST_URL']
db = psycopg.connect(os.environ['BOOK_TEST_DSN'], autocommit=True)
assert db.info.host in ('localhost', '127.0.0.1', '::1')
username = 'favorite_toggle_' + secrets.token_hex(5)
password = secrets.token_urlsafe(20)
user_id = None
try:
    user_id = db.execute('INSERT INTO "Users" ("FullName","UserName","Password","Email","CreatedAt","Role","Status") VALUES (%s,%s,%s,%s,NOW(),1,1) RETURNING "UserId"', ('Toggle test', username, password, username+'@example.invalid')).fetchone()[0]
    book_id = db.execute('SELECT "BookId" FROM "Books" ORDER BY "BookId" LIMIT 1').fetchone()[0]
    other_favorites = db.execute('SELECT "UserId","BookId" FROM "Favorites" WHERE "UserId"<>%s ORDER BY "UserId","BookId"', (user_id,)).fetchall()
    def count():
        return db.execute('SELECT count(*) FROM "Favorites" WHERE "UserId"=%s AND "BookId"=%s',(user_id,book_id)).fetchone()[0]
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page()
        errors = []
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.goto(base+'/Account/Login')
        page.locator('#LoginRequest_Username').fill(username)
        page.locator('#LoginRequest_Password').fill(password)
        page.get_by_role('button',name='Đăng nhập',exact=True).click()
        page.goto(base+f'/BookDetail/{book_id}')
        button = page.locator('#saveBookBtn')
        expect(button).to_be_enabled()
        expect(button).to_have_attribute('aria-pressed','false')
        button.click()
        expect(button).to_have_attribute('aria-pressed','true')
        expect(button).to_be_enabled()
        assert count()==1
        page.reload()
        expect(button).to_have_attribute('aria-pressed','true')
        expect(button).to_be_enabled()
        button.click()
        expect(button).to_have_attribute('aria-pressed','false')
        expect(button).to_be_enabled()
        assert count()==0
        page.reload()
        expect(button).to_have_attribute('aria-pressed','false')
        print('PASS: save, unsave and reload reflect the database; saved button remains clickable',flush=True)

        # A burst of clicks must send only one mutation while the request is pending.
        mutations = []
        page.on('request',lambda request: mutations.append(request.url) if request.method=='POST' and 'handler=AddFavorite' in request.url else None)
        expect(button).to_be_enabled()
        button.evaluate('button => { button.click(); button.click(); button.click(); }')
        expect(button).to_have_attribute('aria-pressed','true')
        expect(button).to_be_enabled()
        assert count()==1 and len(mutations)==1
        print('PASS: repeated clicks do not duplicate a favorite',flush=True)

        page.route('**/Favorites?handler=RemoveFavorite',lambda route: route.fulfill(status=503,json={'success':False,'message':'Request failed'}))
        button.click()
        expect(page.locator('#sharedToast')).to_be_visible()
        expect(button).to_be_enabled()
        expect(button).to_have_attribute('aria-pressed','true')
        assert count()==1
        page.unroute('**/Favorites?handler=RemoveFavorite')
        button.click()
        expect(button).to_have_attribute('aria-pressed','false')
        expect(button).to_be_enabled()
        assert count()==0
        print('PASS: failure keeps the saved state; retry removes the favorite',flush=True)

        button.click()
        expect(button).to_have_attribute('aria-pressed','true')
        page.goto(base+'/Favorites')
        expect(page.locator(f'.saved-book a[href="/BookDetail/{book_id}"]').first).to_be_visible()
        page.goto(base+f'/BookDetail/{book_id}')
        expect(button).to_be_enabled()
        button.click()
        expect(button).to_have_attribute('aria-pressed','false')
        page.goto(base+'/Favorites')
        expect(page.locator('.reader-empty')).to_be_visible()
        assert db.execute('SELECT "UserId","BookId" FROM "Favorites" WHERE "UserId"<>%s ORDER BY "UserId","BookId"',(user_id,)).fetchall()==other_favorites
        assert not errors, errors
        print('PASS: shelf updates and other accounts are unaffected; no JavaScript errors',flush=True)
        browser.close()
finally:
    if user_id:
        db.execute('DELETE FROM "Users" WHERE "UserId"=%s',(user_id,))
    db.close()
