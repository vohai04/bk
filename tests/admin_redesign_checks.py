"""Actual admin CRUD and audit checks against the isolated harness database."""
import secrets
from datetime import datetime, timezone
from pathlib import Path
from playwright.sync_api import expect

def run_checks(admin, conn, base, out, actor_name, actor_id, passed):
    def ready():
        admin.wait_for_load_state('networkidle')
    def goto(path):
        admin.goto(base+path); ready()
    def post_response(handler, action):
        with admin.expect_response(lambda response: ('handler='+handler) in response.url and response.request.method == 'POST') as response:
            action()
        result=response.value.json()
        assert result.get('success'), result
        ready()
        return result
    def catalog_save(handler):
        post_response(handler, lambda:admin.locator('#catalogSave').click())
        expect(admin.locator('#catalogEditor')).to_be_hidden()
    def catalog_delete(row):
        row.get_by_role('button',name='Xóa',exact=False).click()
        expect(admin.locator('#confirmModal')).to_be_visible()
        post_response('AjaxDelete',lambda:admin.locator('#confirmBtn').click())
        expect(admin.locator('#confirmModal')).to_have_count(0)
    def api_post(path,data):
        token=admin.locator('input[name="__RequestVerificationToken"]').first.input_value()
        return admin.request.post(base+path,form=data,headers={'RequestVerificationToken':token})

    goto('/Admin/Dashboard')
    expect(admin.locator('.admin-flash.error')).to_have_count(0)
    assert admin.locator('.category-distribution .distribution-track').count()>0
    text=admin.locator('#mainContent').inner_text()
    assert 'Hoạt động gần đây' not in text and 'Thông báo & Log' not in text and 'Log hoạt động' not in text
    assert admin.locator('.overview-book-row').count()>0
    admin.screenshot(path=str(out/'admin-redesign-dashboard.png'),full_page=True)
    passed('Dashboard contains library overview only; audit log and notifications have separate navigation')

    definitions=[
        ('/Admin/Categories','Thêm thể loại','Category',{'name':'UI category ','description':'A new category'}),
        ('/Admin/Tags','Thêm thẻ sách','Tag',{'name':'UI tag ','description':'<img src=x onerror=window.injected=true>'}),
        ('/Admin/ManageAuthor','Thêm tác giả','Author',{'name':'UI author ','nationality':'Vietnam','dateOfBirth':'1990-01-02','biography':'A short biography'}),
        ('/Admin/ManageNXB','Thêm nhà xuất bản','Publisher',{'name':'UI publisher ','address':'Hanoi','contactInfo':'contact@example.com'})
    ]
    for path,add_label,entity,values in definitions:
        goto(path)
        name=values['name']+secrets.token_hex(3)
        admin.get_by_role('button',name=add_label,exact=True).click()
        for key,value in values.items(): admin.locator('#catalog_'+key).fill(name if key=='name' else value)
        catalog_save('AjaxAdd')
        row=admin.locator('.catalog-table tbody tr').filter(has_text=name)
        expect(row).to_have_count(1)
        record_id=int(row.get_attribute('data-record-id'))
        assert admin.evaluate('window.injected === undefined')
        if entity=='Tag': assert row.locator('img').count()==0
        row.get_by_role('button',name='Sửa '+name,exact=True).click()
        updated=name+' v2'
        admin.locator('#catalog_name').fill(updated)
        catalog_save('AjaxEdit')
        row=admin.locator('.catalog-table tbody tr').filter(has_text=updated)
        expect(row).to_have_count(1)
        admin.locator('#catalogSearch').fill(updated)
        admin.locator('#catalogSearchForm').get_by_role('button',name='Tìm kiếm').click(); ready()
        expect(admin.locator('.catalog-table tbody tr')).to_have_count(1)
        assert admin.locator('#catalogCount').inner_text().startswith('1 ')
        catalog_delete(admin.locator('.catalog-table tbody tr').first)
        expect(admin.locator('.catalog-table tbody tr')).to_have_count(0)
        entries=conn.execute('SELECT "Action","UserName","EntityId","Description" FROM "ActivityLogs" WHERE "EntityType"=%s AND "EntityId"=%s ORDER BY "ActivityId"',(entity,record_id)).fetchall()
        assert [r[0] for r in entries]==['Created','Updated','Deleted'], entries
        assert all(r[1]==actor_name and r[2]==record_id for r in entries)
        assert entries[-1][3]==updated
    passed('Category, tag, author and publisher add/edit/delete/search work; each operation records its actor and object')

    goto('/Admin/AddBook')
    title='UI book '+secrets.token_hex(4)
    isbn='978'+''.join(str(secrets.randbelow(10)) for _ in range(10))
    for field,value in {'Book_Title':title,'Book_ISBN':isbn,'Book_PublicationDate':'2025-01-03','Book_Description':'A short description','Book_Abstract':'A test book summary.'}.items(): admin.locator('#'+field).fill(value)
    for field in ['Book_AuthorId','Book_CategoryId','Book_PublisherId']:
        value=admin.locator('#'+field+' option').nth(1).get_attribute('value'); admin.locator('#'+field).select_option(value)
    admin.locator('input[name="SelectedTagIds"]').first.check()
    admin.screenshot(path=str(out/'admin-redesign-editor.png'),full_page=True)
    admin.locator('#bookEditorSave').click(); ready()
    assert '/Admin/ManageBook' in admin.url, admin.locator('#mainContent').inner_text()
    book_row=conn.execute('SELECT "BookId","UserId" FROM "Books" WHERE "Title"=%s',(title,)).fetchone()
    assert book_row, 'Created book missing'
    book_id, owner_id=book_row
    assert owner_id==actor_id
    assert conn.execute('SELECT count(*) FROM "BookTags" WHERE "BookId"=%s',(book_id,)).fetchone()[0]==1
    goto('/Admin/UpdateBook/'+str(book_id))
    updated_title=title+' updated'
    admin.locator('#Book_Title').fill(updated_title)
    admin.locator('#Book_Abstract').fill('The summary was updated.')
    admin.locator('#bookEditorSave').click(); ready()
    assert '/Admin/ManageBook' in admin.url, admin.locator('#mainContent').inner_text()
    assert conn.execute('SELECT "Title" FROM "Books" WHERE "BookId"=%s',(book_id,)).fetchone()[0]==updated_title
    admin.locator('#bookSearch').fill(updated_title)
    admin.locator('#bookSearchForm').get_by_role('button',name='Tìm kiếm').click(); ready()
    row=admin.locator('.book-table tbody tr').filter(has_text=updated_title)
    expect(row).to_have_count(1)
    row.get_by_role('button',name='Xóa sách '+updated_title,exact=True).click()
    assert conn.execute('SELECT count(*) FROM "Books" WHERE "BookId"=%s',(book_id,)).fetchone()[0]==1
    post_response('Delete',lambda:admin.locator('#confirmBtn').click())
    expect(admin.locator('#confirmModal')).to_have_count(0)
    assert conn.execute('SELECT count(*) FROM "Books" WHERE "BookId"=%s',(book_id,)).fetchone()[0]==0
    assert conn.execute('SELECT count(*) FROM "BookTags" WHERE "BookId"=%s',(book_id,)).fetchone()[0]==0
    entries=conn.execute('SELECT "Action","UserName" FROM "ActivityLogs" WHERE "EntityType"=\'Book\' AND "EntityId"=%s ORDER BY "ActivityId"',(book_id,)).fetchall()
    assert [r[0] for r in entries]==['Book Created','Book Updated','Book Deleted'],entries
    assert all(r[1]==actor_name for r in entries)
    passed('Actual book add/edit/delete works, tags are not duplicated, deletion confirms and audit identifies the actual administrator')

    goto('/Admin/Users')
    username='ui_person_'+secrets.token_hex(4)
    admin.locator('#userAdd').click()
    for field,value in {'editUserName':username,'editFullName':'A test reader','editEmail':username+'@example.invalid','editPassword':'temporary-874421'}.items(): admin.locator('#'+field).fill(value)
    post_response('AjaxAdd',lambda:admin.locator('#userSave').click())
    expect(admin.locator('#userEditor')).to_be_hidden()
    user_id=conn.execute('SELECT "UserId" FROM "Users" WHERE "UserName"=%s',(username,)).fetchone()[0]
    row=admin.locator(f'tr[data-user-id="{user_id}"]')
    row.get_by_role('button',name='Sửa người dùng '+username,exact=True).click()
    admin.locator('#editFullName').fill('Updated test reader')
    post_response('AjaxEdit',lambda:admin.locator('#userSave').click())
    expect(admin.locator('#userEditor')).to_be_hidden()
    row=admin.locator(f'tr[data-user-id="{user_id}"]')
    row.get_by_role('button',name='Khóa tài khoản '+username,exact=True).click()
    post_response('AjaxSetUserStatus',lambda:admin.locator('#confirmBtn').click())
    expect(admin.locator('#confirmModal')).to_have_count(0)
    assert conn.execute('SELECT "Status" FROM "Users" WHERE "UserId"=%s',(user_id,)).fetchone()[0]==0
    row=admin.locator(f'tr[data-user-id="{user_id}"]')
    row.get_by_role('button',name='Kích hoạt tài khoản '+username,exact=True).click()
    post_response('AjaxSetUserStatus',lambda:admin.locator('#confirmBtn').click())
    expect(admin.locator('#confirmModal')).to_have_count(0)
    row=admin.locator(f'tr[data-user-id="{user_id}"]')
    row.get_by_role('button',name='Xóa người dùng '+username,exact=True).click()
    post_response('AjaxDelete',lambda:admin.locator('#confirmBtn').click())
    expect(admin.locator('#confirmModal')).to_have_count(0)
    assert conn.execute('SELECT count(*) FROM "Users" WHERE "UserId"=%s',(user_id,)).fetchone()[0]==0
    own_lock=api_post('/Admin/Users?handler=AjaxSetUserStatus',{'userId':actor_id,'status':0}).json()
    assert not own_lock['success']
    own_delete=api_post('/Admin/Users?handler=AjaxDelete',{'userId':actor_id}).json()
    assert not own_delete['success']
    assert conn.execute('SELECT "Status" FROM "Users" WHERE "UserId"=%s',(actor_id,)).fetchone()[0]==1
    passed('User add/edit/lock/reactivate/delete works and the current administrator cannot lock or delete itself')

    goto('/Admin/AuditLog')
    assert admin.locator('.audit-table tbody tr').count()>0
    admin.locator('#EntityType').select_option('Tag')
    admin.locator('#ActionType').select_option('Deleted')
    admin.get_by_role('button',name='Lọc nhật ký',exact=True).click(); ready()
    expect(admin.locator('.audit-table tbody tr')).to_have_count(1)
    assert 'Xóa' in admin.locator('.audit-table tbody tr').first.inner_text()
    assert actor_name in admin.locator('.audit-table tbody tr').first.inner_text()
    admin.reload(); ready()
    assert admin.locator('#EntityType').input_value()=='Tag'
    assert admin.locator('#ActionType').input_value()=='Deleted'
    goto('/Admin/AuditLog?Search='+actor_name)
    admin.screenshot(path=str(out/'admin-redesign-audit.png'),full_page=True)
    entries=conn.execute('SELECT count(*) FROM "ActivityLogs" WHERE "UserName"=%s',(actor_name,)).fetchone()[0]
    assert entries>=20
    # More than one page after additional harmless fixture events.
    for index in range(25):
        conn.execute('INSERT INTO "ActivityLogs" ("UserName","Action","Description","EntityType","CreatedAt","IpAddress") VALUES (%s,%s,%s,%s,NOW(),%s)',(actor_name,'Updated','UI pagination '+str(index),'Tag','127.0.0.1'))
    goto('/Admin/AuditLog?Search='+actor_name)
    expect(admin.locator('.audit-table tbody tr')).to_have_count(20)
    admin.get_by_role('navigation',name='Trang nhật ký').get_by_role('link',name='Sau',exact=True).click(); ready()
    assert 'p=2' in admin.url
    assert admin.locator('#Search').input_value()==actor_name
    details=admin.locator('.audit-details').first
    details.locator('summary').click()
    assert details.get_attribute('open') is not None
    goto('/Admin/AuditLog?From=2026-10-15&To=2026-10-14')
    assert 'Ngày bắt đầu' in admin.locator('.admin-form-error').inner_text()
    # Boundary event at 00:30 Vietnam time belongs to the following local day.
    boundary_name='boundary-'+secrets.token_hex(4)
    conn.execute('INSERT INTO "ActivityLogs" ("UserName","Action","Description","EntityType","CreatedAt","IpAddress") VALUES (%s,%s,%s,%s,%s,%s)',(actor_name,'Updated',boundary_name,'Tag',datetime(2026,10,13,17,30,tzinfo=timezone.utc),'127.0.0.1'))
    goto('/Admin/AuditLog?Search='+boundary_name+'&From=2026-10-14&To=2026-10-14')
    expect(admin.locator('.audit-table tbody tr')).to_have_count(1)
    assert '14/10/2026' in admin.locator('.audit-table tbody tr').inner_text()
    passed('Audit filters, paging, details, reload, invalid ranges and Vietnam day boundaries work')

    conn.execute('INSERT INTO "Notifications" ("UserId","Title","Message","Type","IsRead","CreatedAt") VALUES (%s,%s,%s,%s,false,NOW())',(actor_id,'UI test notice','Test notification','comment_reply'))
    goto('/Admin/Notifications?UnreadOnly=true')
    expect(admin.locator('.admin-notification')).to_have_count(1)
    admin.locator('.admin-notification').get_by_role('button',name='Đánh dấu đã đọc:',exact=False).click(); ready()
    expect(admin.locator('.admin-notification')).to_have_count(0)
    passed('Notifications are separate and marking a reply notification read updates the unread list')

    goto('/Admin/Report')
    expect(admin.locator('[data-stat="totalBooks"]')).not_to_have_text('—')
    assert admin.locator('.report-bar-row').count()>0
    stats=admin.request.get(base+'/Admin/Report?handler=GetStatistics').json()
    expected=[r[0] for r in conn.execute('SELECT count(b."BookId") FROM "Publishers" p LEFT JOIN "Books" b ON b."PublisherId"=p."PublisherId" GROUP BY p."PublisherId",p."Name" ORDER BY count(b."BookId") DESC,p."Name" LIMIT 10').fetchall()]
    assert stats['chartData']['publisherData']['data']==expected
    pdf=api_post('/Admin/Report?handler=ExportTodayPdf',{})
    assert pdf.status==200 and pdf.body().startswith(b'%PDF'),pdf.status
    admin.screenshot(path=str(out/'admin-redesign-report.png'),full_page=True)
    passed('Reports show actual publisher book counts and export a valid PDF')

    for path,filename in [('/Admin/Dashboard','admin-redesign-dashboard-mobile.png'),('/Admin/AuditLog','admin-redesign-audit-mobile.png'),('/Admin/ManageBook','admin-redesign-books-mobile.png')]:
        goto(path)
        admin.set_viewport_size({'width':390,'height':844})
        admin.wait_for_function('document.querySelector(".management-sidebar").getBoundingClientRect().right <= 1')
        assert not admin.evaluate('document.documentElement.scrollWidth>innerWidth'),path
        admin.screenshot(path=str(out/filename),full_page=False)
        admin.screenshot(path=str(out/filename.replace('.png','-full.png')),full_page=True)
        admin.set_viewport_size({'width':1440,'height':1050})
    goto('/Admin/ManageBook')
    admin.screenshot(path=str(out/'admin-redesign-books.png'),full_page=True)
    passed('New dashboard, audit and book management layouts fit 390px screens')
