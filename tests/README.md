# Admin redesign and UI regression checks

Requirements: .NET 9, PostgreSQL 17 binaries, Python with Playwright and psycopg, and Chromium installed for Playwright.

From the repository root:

```powershell
dotnet build --no-restore
python tests/run_ui_checks.py
```

For the book-detail favorite button (save/unsave, reload, repeated clicks, failed requests, retry and shelf updates):

```powershell
python tests/run_ui_checks.py favorite_toggle_checks.py
```

For title/author/category suggestions, mouse and keyboard selection, dismissal and mobile layout:

```powershell
python tests/run_ui_checks.py search_suggestions_checks.py
```

For avatar preview/upload/replacement/deletion, image validation, session ownership, comments and mobile rendering (requires Pillow):

```powershell
python tests/run_ui_checks.py avatar_checks.py
```

The harness initializes its own PostgreSQL cluster in a verified temporary directory, picks free loopback ports, runs the app in Development with an explicit connection string, then stops both processes and removes the cluster. It does not load `.env` into the test server.

`ui_regression.py` covers the reader UI and admin access. `reader_review_checks.py` covers profile/password changes, reset authorization, history ownership, comment/reply CRUD, exact Vietnam timestamps, notification targets, disabled sessions and mobile screens. `admin_redesign_checks.py` runs actual book/catalog/user CRUD, audit filtering and paging, notification read state, PDF export and responsive checks. The small preliminary tag deletion test is mocked; the subsequent catalog CRUD tests write and delete actual records in the isolated database.

Set `BOOK_TEST_PG_BIN` if PostgreSQL is not at `C:/Program Files/PostgreSQL/17/bin`. Screenshots and test results are saved under `artifacts/` and ignored by Git.

For a manual preview with the app's configured database:

```powershell
dotnet run --no-launch-profile -- --environment Development --urls http://localhost:5180
```

Reader: `/User` or `/`. Admin: `/Admin`; requires an active admin account. Audit log: `/Admin/AuditLog`. Notifications: `/Admin/Notifications`. Existing page routes remain available.

## Manual demo with test accounts

```powershell
python tests/run_demo.py
```

Opens `http://localhost:5180` using a separate PostgreSQL cluster under `artifacts/manual-demo/`. The demo stays running until Ctrl+C, and its data is preserved for the next run. Stop any previous preview on port 5180 before starting it.

- Admin: `admin_test` / `AdminTest@2026`
- Reader: `user_test` / `UserTest@2026`

Each start restores these two demo accounts to their intended roles and active status. The configured application database is not used. The automated regression checks still run against their own temporary database.

Passwords are hashed with ASP.NET Core Identity. Legacy plaintext credentials are upgraded on startup and successful login. Demo seeding writes compatible salted hashes; existing demo reading data is preserved. SMTP delivery is not exercised by the automated suite.
