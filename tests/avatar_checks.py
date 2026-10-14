"""Avatar upload/delete, normalization, rendering and access checks on a local DB."""
import io
import json
import os
import secrets
from pathlib import Path
import psycopg
from PIL import Image
from playwright.sync_api import sync_playwright, expect

base = os.environ['BOOK_TEST_URL']
db = psycopg.connect(os.environ['BOOK_TEST_DSN'], autocommit=True)
assert db.info.host in ('localhost', '127.0.0.1', '::1')
out = Path(__file__).resolve().parents[1] / 'artifacts'
username = 'avatar_check_' + secrets.token_hex(5)
password = secrets.token_urlsafe(20)
user_id = None
results = []

def passed(message):
    results.append(message)
    print('PASS:', message, flush=True)

def picture(fmt='PNG', color='#397553', size=(600,350)):
    image = Image.new('RGB', size, color)
    buf = io.BytesIO()
    image.save(buf, format=fmt)
    return buf.getvalue()

try:
    user_id = db.execute('INSERT INTO "Users" ("FullName","UserName","Password","Email","CreatedAt","Role","Status") VALUES (%s,%s,%s,%s,NOW(),1,1) RETURNING "UserId"', ('Avatar test',username,password,username+'@example.invalid')).fetchone()[0]
    other_user = db.execute('SELECT "UserId" FROM "Users" WHERE "UserId"<>%s ORDER BY "UserId" LIMIT 1',(user_id,)).fetchone()[0]
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True, channel=os.environ.get('BOOK_TEST_BROWSER_CHANNEL'))
        context = browser.new_context(viewport={'width':1440,'height':1050})
        page = context.new_page()
        errors = []
        page.on('pageerror', lambda error: errors.append(str(error)))
        page.goto(base+'/Account/Login')
        page.locator('#LoginRequest_Username').fill(username)
        page.locator('#LoginRequest_Password').fill(password)
        page.get_by_role('button',name='Đăng nhập',exact=True).click()
        page.goto(base+'/Account/Profile')
        expect(page.locator('.profile-monogram .avatar-initial')).to_be_visible()
        page.get_by_role('button',name='Đổi ảnh đại diện',exact=True).click()
        expect(page.locator('#avatarModal')).to_be_visible()
        expect(page.locator('#uploadAvatarBtn')).to_be_disabled()
        expect(page.locator('#deleteAvatarBtn')).to_be_disabled()
        page.locator('#avatarFile').set_input_files({'name':'portrait.png','mimeType':'image/png','buffer':picture()})
        expect(page.locator('#avatarPreviewImage')).to_be_visible()
        page.wait_for_function('document.querySelector("#avatarPreviewImage").naturalWidth > 0')
        expect(page.locator('#uploadAvatarBtn')).to_be_enabled()
        page.wait_for_function('getComputedStyle(document.querySelector("#avatarModal")).opacity === "1"')
        page.screenshot(path=str(out/'avatar-preview.png'))
        page.locator('#uploadAvatarBtn').click()
        expect(page.locator('#avatarModal')).to_be_hidden()
        expect(page.locator('.profile-monogram img')).to_be_visible()
        expect(page.locator('.account-avatar img')).to_be_visible()
        first_url = page.locator('.profile-monogram img').get_attribute('src')
        assert first_url == page.locator('.account-avatar img').get_attribute('src')
        stored = db.execute('SELECT "Image","Version" FROM "UserAvatars" WHERE "UserId"=%s',(user_id,)).fetchone()
        assert stored and len(stored[0]) < len(picture())
        normalized = Image.open(io.BytesIO(stored[0]))
        assert normalized.format == 'WEBP' and normalized.size == (256,256)
        image_response = page.request.get(base+first_url)
        assert image_response.status == 200 and image_response.headers['content-type']=='image/webp'
        assert image_response.headers['x-content-type-options']=='nosniff'
        assert page.request.get(base+first_url,headers={'If-None-Match':image_response.headers['etag']}).status==304
        page.reload()
        expect(page.locator('.profile-monogram img')).to_have_attribute('src',first_url)
        passed('PNG preview/upload normalizes to 256px WebP; profile/menu update immediately and persist on reload')

        def token():
            return page.locator('input[name="__RequestVerificationToken"]').first.input_value()
        def upload(name, data, mime='image/png', extra=None, csrf=True):
            return page.request.post(base+'/Account/Profile?handler=UploadAvatar',
                headers={'RequestVerificationToken':token()} if csrf else {},
                multipart={'avatar':{'name':name,'mimeType':mime,'buffer':data},**(extra or {})})
        assert upload('portrait.png',picture(),csrf=False).status == 400
        assert upload('fake.png',b'<svg onload="alert(1)"></svg>').status == 400
        assert upload('fake.jpg',picture('GIF'),'image/jpeg').status == 400
        assert upload('picture.svg',picture()).status == 400
        assert upload('too-big.png',b'X'*(2*1024*1024+1)).status == 400
        assert upload('empty.png',b'').status == 400
        assert upload('wide.png',picture(size=(9000,2))).status == 400
        assert page.request.post(base+'/Account/Profile?handler=UploadAvatar',headers={'RequestVerificationToken':token()},multipart={'other':'value'}).status == 400
        assert db.execute('SELECT "Version" FROM "UserAvatars" WHERE "UserId"=%s',(user_id,)).fetchone()[0] == stored[1]
        passed('Server rejects missing CSRF, fake image bytes, GIF/SVG, empty and oversized uploads without replacing the saved image')

        other_before = db.execute('SELECT "AvatarVersion" FROM "Users" WHERE "UserId"=%s',(other_user,)).fetchone()[0]
        replacement = upload('portrait.jpg',picture('JPEG','#8d6042'),'image/jpeg',{'userId':str(other_user)})
        assert replacement.status == 200, replacement.text()
        second_url = replacement.json()['user']['avatarUrl']
        assert second_url != first_url
        assert db.execute('SELECT "AvatarVersion" FROM "Users" WHERE "UserId"=%s',(other_user,)).fetchone()[0] == other_before
        assert page.request.get(base+first_url).status == 404
        webp_response = upload('portrait.webp',picture('WEBP'),'image/webp')
        assert webp_response.status == 200, (webp_response.status,webp_response.text())
        current = db.execute('SELECT "Version" FROM "UserAvatars" WHERE "UserId"=%s',(user_id,)).fetchone()[0]
        current_url = f'/avatars/{user_id}?v={current}'
        updated_profile = page.request.post(base+'/Account/Profile?handler=Update',headers={'RequestVerificationToken':token()},data={'userId':user_id,'fullName':'Avatar profile updated','email':username+'@example.invalid'})
        assert updated_profile.json()['success'] is True
        assert db.execute('SELECT "AvatarVersion" FROM "Users" WHERE "UserId"=%s',(user_id,)).fetchone()[0] == current
        page.reload()
        expect(page.locator('.account-avatar img')).to_have_attribute('src',current_url)
        passed('JPEG/WebP replacements get new URLs; client-supplied user ID cannot change another account')

        book_id = db.execute('SELECT "BookId" FROM "Books" ORDER BY "BookId" LIMIT 1').fetchone()[0]
        page.goto(base+f'/BookDetail/{book_id}')
        page.locator('#commentText').fill('Bình luận có ảnh đại diện')
        page.locator('#commentForm button[type="submit"]').click()
        expect(page.locator('#commentText')).to_have_value('')
        comment_id = db.execute('SELECT "BookCommentId" FROM "BookComments" WHERE "UserId"=%s ORDER BY "BookCommentId" DESC LIMIT 1',(user_id,)).fetchone()[0]
        expect(page.locator(f'#comment-content-{comment_id} .comment-avatar img')).to_have_attribute('src',current_url)
        result = page.request.post(base+f'/BookDetail/{book_id}?handler=AddReply',headers={'RequestVerificationToken':token()},form={'BookId':book_id,'ParentCommentId':comment_id,'Comment':'Phản hồi có avatar'})
        assert result.status==200 and result.json()['reply']['avatarUrl']==current_url
        reply_id=result.json()['reply']['bookCommentId']
        page.reload()
        expect(page.locator(f'#comment-content-{reply_id} .comment-avatar img')).to_have_attribute('src',current_url)
        page.screenshot(path=str(out/'avatar-comments.png'),full_page=True)
        passed('New and reloaded comments/replies show the same current avatar')

        page.goto(base+'/Account/Profile')
        page.get_by_role('button',name='Đổi ảnh đại diện',exact=True).click()
        page.locator('#avatarFile').set_input_files({'name':'retry.png','mimeType':'image/png','buffer':picture()})
        page.wait_for_function('document.querySelector("#avatarPreviewImage").naturalWidth > 0')
        page.route('**/Account/Profile?handler=UploadAvatar',lambda route:route.fulfill(status=503,json={'success':False,'message':'Không lưu được ảnh. Vui lòng thử lại.'}))
        page.locator('#uploadAvatarBtn').click()
        expect(page.locator('#avatarError')).to_be_visible()
        expect(page.locator('#uploadAvatarBtn')).to_be_enabled()
        assert db.execute('SELECT "Version" FROM "UserAvatars" WHERE "UserId"=%s',(user_id,)).fetchone()[0] == current
        page.unroute('**/Account/Profile?handler=UploadAvatar')
        page.locator('#avatarFile').set_input_files({'name':'huge.png','mimeType':'image/png','buffer':b'X'*(2*1024*1024+1)})
        expect(page.locator('#avatarError')).to_be_visible()
        expect(page.locator('#uploadAvatarBtn')).to_be_disabled()
        expect(page.locator('#avatarFile')).to_have_value('')
        page.locator('#avatarModal button[data-bs-dismiss="modal"]').first.click()
        expect(page.locator('#avatarModal')).to_be_hidden()
        page.set_viewport_size({'width':390,'height':844})
        page.get_by_role('button',name='Đổi ảnh đại diện',exact=True).click()
        expect(page.locator('#avatarModal')).to_be_visible()
        page.wait_for_function('getComputedStyle(document.querySelector("#avatarModal")).opacity === "1"')
        assert not page.evaluate('document.documentElement.scrollWidth > innerWidth')
        page.screenshot(path=str(out/'avatar-mobile.png'))
        page.locator('#deleteAvatarBtn').click()
        expect(page.locator('#confirmDeleteAvatarBtn')).to_be_visible()
        page.locator('.avatar-delete-confirm').get_by_role('button',name='Giữ ảnh',exact=True).click()
        assert db.execute('SELECT 1 FROM "UserAvatars" WHERE "UserId"=%s',(user_id,)).fetchone()
        page.locator('#deleteAvatarBtn').click()
        page.locator('#confirmDeleteAvatarBtn').click()
        expect(page.locator('#avatarModal')).to_be_hidden()
        expect(page.locator('.profile-monogram img')).to_have_count(0)
        expect(page.locator('.account-avatar img')).to_have_count(0)
        assert db.execute('SELECT "AvatarVersion" FROM "Users" WHERE "UserId"=%s',(user_id,)).fetchone()[0] is None
        assert not db.execute('SELECT 1 FROM "UserAvatars" WHERE "UserId"=%s',(user_id,)).fetchone()
        page.goto(base+f'/BookDetail/{book_id}')
        expect(page.locator(f'#comment-content-{comment_id} .comment-avatar .avatar-initial').first).to_be_visible()
        expect(page.locator(f'#comment-content-{comment_id} .comment-avatar img')).to_have_count(0)
        assert page.request.get(base+current_url).status == 404
        passed('Client validates size; mobile preview fits; delete confirms and restores initials in profile/menu/comments')

        anonymous = browser.new_context()
        guest = anonymous.new_page()
        guest.goto(base+'/Account/Login')
        guest_token=guest.locator('input[name="__RequestVerificationToken"]').first.input_value()
        for handler in ('UploadAvatar','DeleteAvatar'):
            response=guest.request.post(base+'/Account/Profile?handler='+handler,headers={'RequestVerificationToken':guest_token},multipart={'avatar':{'name':'portrait.png','mimeType':'image/png','buffer':picture()}})
            assert response.status==401
        anonymous.close()
        page.goto(base+'/Account/Profile')
        db.execute('UPDATE "Users" SET "Status"=0 WHERE "UserId"=%s',(user_id,))
        assert upload('portrait.png',picture()).status==403
        assert not errors,errors
        passed('Anonymous/disabled users cannot upload or delete; no JavaScript errors')
        browser.close()
finally:
    if user_id:
        db.execute('DELETE FROM "ActivityLogs" WHERE "UserName"=%s',(username,))
        db.execute('DELETE FROM "Users" WHERE "UserId"=%s',(user_id,))
    db.close()
(out/'avatar-test-results.json').write_text(json.dumps({'passed':results},ensure_ascii=False,indent=2),encoding='utf-8')
