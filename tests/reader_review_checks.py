"""Reader CRUD, UTC+7, authentication and password regression checks."""
from playwright.sync_api import expect


def run_checks(page, conn, base, out, user_id, other_id, password, passed):
    def post(route, data, json=False):
        token = page.locator('input[name="__RequestVerificationToken"]').first.input_value()
        return page.request.post(base + route, headers={'RequestVerificationToken': token}, **({'data': data} if json else {'form': data}))

    # Compare controls on all public/personal screens with the administrator's primary color.
    for route in ('/Account/Profile', '/Favorites', '/SearchHistory', '/Notifications', '/Privacy', '/Account/Register', '/Account/ForgotPassword'):
        response = page.goto(base + route)
        assert response.status == 200, route
        page.wait_for_load_state('networkidle')
        assert page.evaluate('getComputedStyle(document.body).getPropertyValue("--accent").trim()') == '#2d6b53'
        assert not page.evaluate('document.documentElement.scrollWidth > innerWidth'), route
        assert not page.locator('[id]').evaluate_all('(els)=>els.map(e=>e.id).some((id,i,a)=>a.indexOf(id)!==i)'), route
    passed('All reader/account pages use the shared green theme without duplicate IDs or overflow')
    page.goto(base + '/Account/Profile')
    page.get_by_role('button', name='Chỉnh sửa hồ sơ', exact=True).click()
    expect(page.locator('#editProfileModal')).to_be_visible()
    page.wait_for_function('getComputedStyle(document.querySelector("#editProfileModal")).opacity === "1"')
    assert page.locator('#saveProfileBtn').evaluate('el => getComputedStyle(el).backgroundImage') == 'none'
    assert page.locator('#saveProfileBtn').evaluate('el => getComputedStyle(el).backgroundColor') == 'rgb(45, 107, 83)'
    page.screenshot(path=str(out / 'reader-profile-modal.png'))
    page.locator('#profileFullNameInput').fill('Người đọc kiểm thử')
    page.locator('#saveProfileBtn').click()
    expect(page.locator('#editProfileModal')).to_be_hidden()
    expect(page.locator('#profileFullName')).to_have_text('Người đọc kiểm thử')
    assert post('/Account/Profile?handler=Update', {'userId': user_id, 'fullName':'Bad', 'email':'broken'}, True).json()['success'] is False
    assert post('/Account/Profile?handler=Update', {'userId':other_id, 'fullName':'Intruder','email':'safe@example.invalid'},True).json()['success'] is False
    passed('Profile modal saves valid data and rejects invalid email or a different account')

    page.goto(base + '/Account/ForgotPassword')
    email = conn.execute('SELECT "Email" FROM "Users" WHERE "UserId"=%s', (user_id,)).fetchone()[0]
    before = conn.execute('SELECT "Password" FROM "Users" WHERE "UserId"=%s',(user_id,)).fetchone()[0]
    assert before != password and len(before) >= 80
    response = post('/Account/ResetPassword', {'Email':email, 'NewPassword':'Unauthorized@2026','ConfirmPassword':'Unauthorized@2026'})
    assert response.url.rstrip('/').endswith(('/Account/ForgotPassword', '/ForgotPassword')), (response.status, response.url, response.text()[:200])
    assert conn.execute('SELECT "Password" FROM "Users" WHERE "UserId"=%s',(user_id,)).fetchone()[0] == before
    response = page.request.get(base + '/Account/ResetPassword?email=' + email, max_redirects=0)
    assert response.status == 302
    passed('Legacy login upgrades to a password hash; password reset requires a verified OTP session')

    page.goto(base + '/Account/Profile')
    assert post('/Account/Profile?handler=ChangePassword', {'userId':user_id,'currentPassword':'wrong','newPassword':'Changed@2026'},True).json()['success'] is False
    assert post('/Account/Profile?handler=ChangePassword', {'userId':user_id,'currentPassword':password,'newPassword':'x'},True).json()['success'] is False
    assert post('/Account/Profile?handler=ChangePassword', {'userId':user_id,'currentPassword':password,'newPassword':'Changed@2026'},True).json()['success'] is True
    assert conn.execute('SELECT "Password" FROM "Users" WHERE "UserId"=%s',(user_id,)).fetchone()[0] not in (password,'Changed@2026',before)
    assert post('/Account/Profile?handler=ChangePassword', {'userId':user_id,'currentPassword':'Changed@2026','newPassword':password},True).json()['success'] is True
    passed('Password change verifies the current credential, validates length and stores a salted hash')

    history_id = conn.execute('INSERT INTO "SearchHistories" ("UserId","SearchQuery","SearchedAt","ResultCount") VALUES (%s,%s,NOW(),0) RETURNING "SearchHistoryId"',(other_id,'private search')).fetchone()[0]
    assert post('/SearchHistory?handler=AjaxDelete', {'id':history_id}).status == 403
    assert conn.execute('SELECT 1 FROM "SearchHistories" WHERE "SearchHistoryId"=%s',(history_id,)).fetchone()
    conn.execute('DELETE FROM "SearchHistories" WHERE "SearchHistoryId"=%s',(history_id,))
    passed('Search history deletion enforces ownership')

    books = [row[0] for row in conn.execute('SELECT "BookId" FROM "Books" ORDER BY "BookId" LIMIT 2')]
    book = books[0]
    page.goto(base + f'/BookDetail/{book}')
    expect(page.locator('#commentCount')).to_contain_text('bình luận')
    foreign = conn.execute('SELECT "BookCommentId" FROM "BookComments" WHERE "BookId"=%s LIMIT 1',(book,)).fetchone()
    if foreign:
        assert post(f'/BookDetail/{book}?handler=EditComment', {'commentId':foreign[0],'newComment':'Hacked'}).status == 403
        assert post(f'/BookDetail/{book}?handler=DeleteComment', {'commentId':foreign[0]}).status == 403
    other_root = conn.execute('INSERT INTO "BookComments" ("BookId","UserId","Comment","Star","CreatedAt") VALUES (%s,%s,%s,5,NOW()) RETURNING "BookCommentId"',(book,other_id,'Thông báo kiểm thử')).fetchone()[0]
    actual_reply = post(f'/BookDetail/{book}?handler=AddReply', {'BookId':book,'ParentCommentId':other_root,'Comment':'Phản hồi có thông báo'}).json()['reply']['bookCommentId']
    notification = conn.execute('SELECT "RelatedEntityId","RelatedEntityType" FROM "Notifications" WHERE "UserId"=%s AND "Type"=%s ORDER BY "NotificationId" DESC LIMIT 1',(other_id,'comment_reply')).fetchone()
    assert notification == (actual_reply, 'BookComment'), notification
    conn.execute('DELETE FROM "Notifications" WHERE "RelatedEntityId"=%s AND "Type"=%s',(actual_reply,'comment_reply'))
    conn.execute('DELETE FROM "BookComments" WHERE "BookCommentId"=%s',(other_root,))
    passed('Reply notifications reference the actual new reply and recipient')
    assert post(f'/BookDetail/{book}?handler=AddComment', {'BookId':book,'Star':0,'Comment':'invalid'}).status == 400
    assert post(f'/BookDetail/{book}?handler=AddComment', {'BookId':book,'Star':5,'Comment':' '}).status == 400
    page.locator('#commentText').fill('Một cuốn sách đáng đọc. <img src=x onerror=window.injected=true>')
    page.locator('#commentForm').get_by_role('button', name='Gửi bình luận', exact=True).click()
    expect(page.locator('#commentText')).to_have_value('')
    root_id = conn.execute('SELECT "BookCommentId" FROM "BookComments" WHERE "UserId"=%s AND "BookId"=%s ORDER BY "BookCommentId" DESC LIMIT 1',(user_id,book)).fetchone()[0]
    assert conn.execute('SELECT "Star" FROM "Ratings" WHERE "UserId"=%s AND "BookId"=%s',(user_id,book)).fetchone()[0] == 5
    assert post(f'/BookDetail/{book}?handler=AddReply', {'BookId':books[1],'ParentCommentId':root_id,'Comment':'cross book'}).status == 400
    response = post(f'/BookDetail/{book}?handler=AddReply', {'BookId':book,'ParentCommentId':root_id,'Comment':'Phản hồi đầu tiên'})
    assert response.status == 200, response.text()
    reply_id = response.json()['reply']['bookCommentId']
    nested = post(f'/BookDetail/{book}?handler=AddReply', {'BookId':book,'ParentCommentId':reply_id,'Comment':'Phản hồi tiếp theo'}).json()['reply']['bookCommentId']
    conn.execute('UPDATE "BookComments" SET "CreatedAt"=%s WHERE "BookCommentId"=%s',('2026-10-14T04:35:41Z',reply_id))
    # Different owner replies so the notification should identify the actual reply.
    page.reload(); page.wait_for_load_state('networkidle')
    article = page.locator(f'#comment-content-{reply_id}')
    expect(article.locator('time').first).to_have_text('11:35 14/10/2026')
    assert article.locator('time').first.get_attribute('datetime') == '2026-10-14T04:35:41.000Z'
    assert page.locator('#commentsList img, #commentsList script').count() == 0
    assert page.evaluate('window.injected === undefined')
    article.get_by_role('button',name='Sửa',exact=True).click()
    article.locator('textarea').fill('Phản hồi đã sửa')
    article.get_by_role('button',name='Lưu thay đổi',exact=True).click()
    expect(page.locator(f'#comment-content-{reply_id} .comment-text')).to_have_text('Phản hồi đã sửa')
    expect(page.locator(f'#comment-content-{reply_id} .comment-edited')).to_be_visible()
    page.screenshot(path=str(out / 'reader-book-comments.png'),full_page=True)
    passed('Comments/replies CRUD is safe, validates book ownership and displays exact UTC+7 creation/edit times')
    result = page.request.get(base + f'/BookDetail/{book}?handler=GetCommentPage&commentId={nested}').json()
    assert result['rootId'] == root_id
    page.set_viewport_size({'width':390,'height':844})
    for route in (f'/BookDetail/{book}', '/Account/Profile', '/Favorites', '/SearchHistory', '/Notifications', '/Account/Register'):
        page.goto(base + route); page.wait_for_load_state('networkidle')
        assert not page.evaluate('document.documentElement.scrollWidth > innerWidth'), route
    page.goto(base + f'/BookDetail/{book}');page.wait_for_load_state('networkidle')
    page.screenshot(path=str(out / 'reader-book-mobile.png'),full_page=True)
    passed('Nested reply links resolve to the root page; personal/account/book screens fit mobile')
    assert post(f'/BookDetail/{book}?handler=DeleteComment', {'commentId':root_id}).json()['success'] is True
    assert not conn.execute('SELECT 1 FROM "BookComments" WHERE "BookCommentId"=ANY(%s)',([root_id,reply_id,nested],)).fetchone()
    conn.execute('UPDATE "Users" SET "Status"=0 WHERE "UserId"=%s',(user_id,))
    assert page.request.get(base + '/Notifications?handler=UnreadCount').status == 403
    assert post(f'/BookDetail/{book}?handler=AddComment', {'BookId':book,'Star':5,'Comment':'blocked'}).status == 401
    conn.execute('UPDATE "Users" SET "Status"=1 WHERE "UserId"=%s',(user_id,))
    page.set_viewport_size({'width':1440,'height':1050})
    passed('Deleting a comment cascades through nested replies; disabling a reader revokes the session')
