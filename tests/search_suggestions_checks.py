"""Search suggestions must be visible, selectable and usable from the keyboard."""
import os
from pathlib import Path
from urllib.parse import urlparse, parse_qs
from playwright.sync_api import sync_playwright, expect

base = os.environ.get('BOOK_TEST_URL', 'http://localhost:5180')
with sync_playwright() as p:
    browser = p.chromium.launch(headless=True)
    page = browser.new_page(viewport={'width':1440, 'height':1050})
    errors = []
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.goto(base)
    page.wait_for_load_state('networkidle')
    field = page.locator('#mainSearchInput')
    popup = page.locator('#searchSuggestions')
    field.fill('Harry')
    expect(popup).to_be_visible()
    expect(popup.locator('.suggestion-item').first).to_be_visible()
    expect(field).to_have_attribute('aria-expanded','true')
    page.screenshot(path=str(Path(__file__).resolve().parents[1] / 'artifacts/search-suggestions.png'))
    title = popup.locator('.suggestion-item span').first.inner_text()
    popup.locator('.suggestion-item').first.click()
    expect(popup).to_be_hidden()
    page.wait_for_load_state('networkidle')
    assert parse_qs(urlparse(page.url).query)['title'][0] == title
    assert page.locator('.book-card').count() > 0
    print('PASS: title suggestions are visible and clicking searches the selected title', flush=True)

    for keyword, kind, param in [('Rowling', 'Tác giả', 'author'), ('Fantasy', 'Thể loại', 'category')]:
        field.fill(keyword)
        expect(popup).to_be_visible()
        option = popup.locator('.suggestion-item').filter(has=page.locator('small',has_text=kind)).first
        expect(option).to_be_visible()
        option.click()
        expect(popup).to_be_hidden()
        page.wait_for_load_state('networkidle')
        assert param in parse_qs(urlparse(page.url).query)
        assert page.locator('.book-card').count() > 0
    print('PASS: author and category suggestions search the correct fields', flush=True)

    field.fill('Harry')
    expect(popup).to_be_visible()
    field.press('ArrowDown')
    expect(popup.locator('button').first).to_be_focused()
    page.keyboard.press('Enter')
    expect(popup).to_be_hidden()
    page.wait_for_load_state('networkidle')
    assert 'title' in parse_qs(urlparse(page.url).query)
    field.fill('Harry')
    expect(popup).to_be_visible()
    field.press('Escape')
    expect(popup).to_be_hidden()
    expect(field).to_be_focused()
    field.fill('Harr')
    expect(popup).to_be_visible()
    field.fill('')
    expect(popup).to_be_hidden()
    expect(field).to_have_attribute('aria-expanded','false')
    print('PASS: ArrowDown/Enter selects; Escape and clearing close the popup', flush=True)

    page.set_viewport_size({'width':390,'height':844})
    field.fill('Harry')
    expect(popup).to_be_visible()
    assert not page.evaluate('document.documentElement.scrollWidth > innerWidth')
    popup.locator('button').first.click()
    expect(popup).to_be_hidden()
    assert not errors, errors
    print('PASS: suggestions work on mobile without overflow or JavaScript errors', flush=True)
    browser.close()
