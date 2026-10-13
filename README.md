# WMS Distribution Center

A full-stack **Warehouse Management System** for a distribution center. It covers the whole physical cycle: receiving and put-away, stock control, order allocation, picking, packing, shipping, adjustments, transfers and returns, with role-based access and a REST API documented in Swagger.

> **Portfolio case study.** A personal project built to practise designing a realistic WMS domain model, API and UI. It is not affiliated with any company and ships with fictional demo data only.

![Dashboard](docs/screenshots/02-dashboard.webp)

## Why

Distribution centers live and die by inventory accuracy. This project models the inbound-to-outbound flow end to end so the business rules are explicit and tested: stock only changes through traceable ledger transactions, allocation cannot over-reserve, picking cannot exceed what was requested, shipping requires packing, and corrections need a second person's approval.

## Features

- **Inbound**: planned receipts with lines, receiving with put-away to a location in one step (repeatable, only the delta posts), damaged goods recorded but not stocked, lot-mixing protection, receipt completion as Received or Partial.
- **Outbound**: orders with lines, all-or-nothing stock allocation (first-expiry-first-out, fullest bin first), pick tasks per bin, pick confirmation with over-pick rejection and short picks, packing into cartons, shipping with carrier details, delivery, cancellation that releases reservations.
- **Inventory**: per-bin on-hand/reserved/available, an immutable movement ledger (inbound, outbound, adjustment, transfer, return), adjustments (increase, decrease, recount) that apply only after approval and are recalculated against current stock, transfers of unreserved stock, returns with restock/refurbish/dispose/write-off dispositions.
- **Safety**: optimistic concurrency token on every stock row so two simultaneous allocations cannot both win, and consistent 400/403/404/409 error messages the UI shows verbatim.
- **Master data**: warehouses, zones, locations, products, categories, units of measure, suppliers, carriers.
- **Security**: JWT, BCrypt, four roles with a per-endpoint permission matrix, CORS allow-list, startup validation of the JWT configuration, login rate limiting, no credentials in the repository (see [Security](#security)).
- **Web UI**: every step above is available in the browser with loading, empty and error states and role-aware actions. There are no mock-data fallbacks: if the API is down the page says so.

## Roles and permission matrix

Four built-in roles. The table below is enforced by the API and verified for **every endpoint and every role** by `AuthorizationMatrixTests` (a new endpoint without an entry fails the build). The UI hides actions the server would reject. Per-route detail: [`docs/API-REFERENCE.md`](docs/API-REFERENCE.md).

| Capability | Admin | Supervisor | Operator | Viewer |
|---|:-:|:-:|:-:|:-:|
| View inventory, receipts, orders, pick tasks, shipments, transfers, returns, adjustments, reports, master data | yes | yes | yes | yes |
| Receive and put away goods, complete receipts | yes | yes | yes | |
| Confirm picks, pack, ship, mark delivered | yes | yes | yes | |
| Propose adjustments, create/complete transfers, receive returns | yes | yes | yes | |
| Plan receipts and orders (create, edit, delete drafts), allocate stock, assign pickers, cancel orders | yes | yes | | |
| Approve or reject adjustments, process returns | yes | yes | | |
| Create and edit master data | yes | yes | | |
| List users | yes | yes | | |
| Delete master data, create/edit/delete users, manage roles | yes | | | |

Rules on top of the matrix: a non-admin cannot approve an adjustment they created; an admin cannot delete their own account; the four built-in roles cannot be renamed or deleted and custom roles only get read access; a token is rejected as soon as its user is deactivated or changes role. Anonymous routes: `POST /api/auth/login` and `/register` (rate limited; registration is off unless `Auth__AllowSelfRegistration=true` and always yields a Viewer), `GET /healthz`, `/readyz`.

## Screenshots

All data below is fictional (seeded through the API by `frontend/e2e/screenshots.mjs`).

| | |
|---|---|
| ![Login](docs/screenshots/01-login.webp) **Login** | ![Inventory](docs/screenshots/03-inventory.webp) **Inventory** with reserved vs available |
| ![Receipt](docs/screenshots/04-receipt-detail.webp) **Receipt**: partial delivery with a shortage and damaged goods | ![Orders](docs/screenshots/05-outbound.webp) **Outbound orders** in every state |
| ![Order](docs/screenshots/06-order-picking.webp) **Order in Picking**: lines, pick tasks, picker assignment | ![Picks](docs/screenshots/07-pick-tasks.webp) **Pick tasks**: confirm or short-pick |
| ![Adjustments](docs/screenshots/08-adjustments.webp) **Adjustments** awaiting approval | ![Transfers](docs/screenshots/09-transfers.webp) **Transfers** |
| ![Returns](docs/screenshots/10-returns.webp) **Returns** | ![Dashboard](docs/screenshots/02-dashboard.webp) **Dashboard** |

## Architecture

```
React 19 + Vite  --/api-->  ASP.NET Core 9 Web API  --EF Core 8-->  PostgreSQL 16
 (axios, router)            Controllers -> Services -> DbContext
                            JWT + role policies, rate limiting, Swagger (Development)
```

```
backend/    ASP.NET Core API: Controllers, Services (business rules), Mapping (explicit DTO mappers),
            Security (roles, policies, JWT settings), Data (DbContext, migrations, seeder)
frontend/   React SPA: pages, shared UI (modal, toasts, data states), auth context, API client,
            Vitest tests next to the code, Playwright end-to-end test in e2e/
tests/      xUnit: unit, workflow, concurrency, authorization matrix, security and migration tests
docs/       API reference, user manual, original specification, ERD, screenshots
scripts/    secret-scan.sh
```

Business rules live in the services behind interfaces (`InboundService`, `OutboundService`, `InventoryService`, `TransferService`, `ReturnService`); controllers only translate HTTP and apply the authorization policy.

## Tech stack

ASP.NET Core 9, Entity Framework Core 8 (Npgsql) with migrations, PostgreSQL 16, JWT bearer auth, BCrypt, Swagger/OpenAPI, React 19, Vite, React Router, Axios, Vitest + Testing Library, Playwright, xUnit, Docker, GitHub Actions. AutoMapper was removed in favour of explicit mapping (its 12.x release carries a high-severity advisory); `dotnet list package --vulnerable --include-transitive` is clean and CI fails if that changes.

## Run it locally

Prerequisites: .NET SDK 9, Node.js 22+, Docker (for the database; any PostgreSQL 16 works too).

```bash
# 1. Database: PostgreSQL 16 on localhost:5333 (throwaway local credentials)
docker compose -f docker-compose.dev.yml up -d

# 2. API -> http://localhost:5300  (Swagger UI at /swagger)
#    Development applies the EF Core migrations and seeds a fictional warehouse and the demo accounts
dotnet run --project backend

# 3. Web app -> http://localhost:5173  (proxies /api to the API)
cd frontend
npm ci
npm run dev
```

Demo accounts exist **only in the Development environment with `Seed__DemoData=true`** (the default in `appsettings.Development.json`): `admin`, `supervisor`, `operator`, `viewer`, all with the documented throwaway password `Demo!Pass123`. In Development without `Jwt__Key` the API generates a random signing key on every start (sessions end when it restarts), so no key lives in the repository.

## Run the full stack with Docker

```bash
cp .env.example .env     # then set POSTGRES_PASSWORD, JWT_KEY (openssl rand -base64 48) and ADMIN_PASSWORD
docker compose up --build
```

Web app: <http://localhost:5174>, API health: <http://localhost:5301/healthz>. Sign in as `admin` with the `ADMIN_PASSWORD` from `.env`. The stack runs in **Production mode**: Swagger is off, there are no demo accounts, migrations run on startup (`Database__MigrateOnStartup=true`), the API refuses to start with a placeholder or missing signing key, and nginx serves the SPA and proxies `/api` so no CORS is needed. Set `SEED_DEMO_DATA=true` in `.env` to also load the fictional warehouse and products (still no demo accounts). TLS is not terminated here; put a reverse proxy in front for anything public. `docker compose down` stops it (add `--volumes` to drop the database).

## Configuration

Configuration comes from `appsettings*.json` and environment variables (`__` replaces `:`). Committed files contain no secrets.

| Setting | Notes |
|---|---|
| `ConnectionStrings__DefaultConnection` | Npgsql connection string (required outside Development) |
| `Jwt__Key` | Required outside Development: 32+ characters, rejected when it looks like a placeholder or has too little entropy. `Jwt__Issuer`, `Jwt__Audience`, `Jwt__ExpiryMinutes` (1-1440) are validated at startup |
| `AllowedOrigins__0`, `__1`, ... | Explicit CORS origins. Empty means same-origin only; `*` is rejected at startup |
| `Auth__AllowSelfRegistration` | Default `false` (`true` in Development) |
| `RateLimiting__AuthPermitLimit` / `__AuthWindowSeconds` | Login/registration limit per client address, default 10 per 60 s |
| `ForwardedHeaders__Enabled` | Set behind a reverse proxy so the limiter sees the real client address (only expose the API to the proxy) |
| `Database__MigrateOnStartup` | Apply migrations on start outside Development (Development always does) |
| `Seed__AdminUsername`, `Seed__AdminEmail`, `Seed__AdminPassword` | Creates the first administrator when no user exists (password 12+ characters) |
| `Seed__DemoData` | Fictional warehouse, products and stock. Demo **accounts** are additionally gated on `ASPNETCORE_ENVIRONMENT=Development` |

## Database migrations

The schema is owned by EF Core migrations in `backend/Data/Migrations`. A test fails when the model drifts from the latest migration.

```bash
dotnet tool restore                                              # installs the pinned dotnet-ef
dotnet ef migrations add <Name> --project backend --output-dir Data/Migrations
dotnet ef database update --project backend                      # uses ConnectionStrings__DefaultConnection
dotnet ef migrations script --idempotent --project backend -o schema.sql   # SQL for a DBA
```

Development applies pending migrations on startup. Elsewhere the API fails fast with the command above when the schema is behind, unless `Database__MigrateOnStartup=true`.

## Tests

```bash
dotnet test WmsDistributionCenter.sln        # backend: 95 xUnit tests
cd frontend
npm run lint && npm test && npm run build    # 94 Vitest + Testing Library tests
npm run test:e2e                             # 2 Playwright tests, needs the API and web app running (see below)
./scripts/secret-scan.sh                     # run before every push
```

- **Backend (95)**: every workflow (receive, allocate, pick, pack, ship, deliver, cancel, adjust, transfer, return), over-pick and over-allocation protection, an optimistic-concurrency test and a 6-way parallel-allocation test, the authorization matrix (every endpoint x every role), JWT validation, seeding rules, CORS, rate limiting, revoked tokens, health endpoints, migration drift, and a full warehouse cycle across roles through HTTP. They use the in-memory EF provider plus the real ASP.NET pipeline.
- **Frontend (94)**: each page with loading, empty, error and permission states and its create/confirm flows, the generic CRUD page, login, sidebar visibility, and the permission helpers.
- **End to end (2)**: one Playwright run through the browser: receive, put away, create order, allocate, pick, pack, ship, deliver, adjust, transfer, return, then check the ledger; plus a viewer who sees no actions. Locally: start the API and `npm run dev`, then `npx playwright install chromium` (or `PW_CHANNEL=chrome` to use an installed Chrome) and `npm run test:e2e`. The same test passes against the Docker stack with `E2E_BASE_URL=http://localhost:5174 E2E_DEMO_PASSWORD=<ADMIN_PASSWORD>`.
- **CI** (`.github/workflows/ci.yml`): backend tests and a vulnerability audit, frontend lint/test/build, the Playwright test against a real PostgreSQL service, and the secret scan.

## Security

- Per-endpoint role policies (above), tokens revalidated against the user record, BCrypt hashes, a dummy hash comparison so unknown users cost the same as wrong passwords.
- Configuration validated at startup (JWT key strength, issuer/audience, expiry, CORS origins), fail fast with a clear message.
- Rate limiting on login and registration; security headers (`nosniff`, frame deny, no referrer); request validation with minimum password length 10.
- No secrets in the repository: signing keys come from the environment (or are generated in Development), demo accounts are Development-only, the Docker stack requires `.env` secrets. `scripts/secret-scan.sh` runs in CI and should be run before pushing.
- Reporting issues: open a GitHub issue; this is a portfolio project, not a supported product.

## Known limitations

- **Not production hardened.** No audit trail beyond the stock ledger, no refresh tokens or password reset, the JWT lives in `localStorage` (exposed to XSS), TLS is not terminated by the compose stack, and there is no account lockout beyond the per-address rate limit.
- **Roles are fixed.** The four built-in roles carry the permissions; custom roles can only read. A user's warehouse is stored but does not restrict access. The role `Permissions` JSON column is unused.
- **Simplified warehouse model.** Allocation is all-or-nothing per order (no back-orders or partial shipments), one carton per order, one stock row per product and bin (so one lot per bin), no wave/batch picking, no multi-UOM conversion at transaction time, put-away is chosen by the user and not suggested.
- **Document numbers** (`RCV-`, `ORD-`, ...) are daily counters; two simultaneous creations can collide and one gets a 409 to retry.
- **Concurrency** is verified with the in-memory provider and an interleaved-parallel test; it has not been load-tested against PostgreSQL under real parallel traffic.
- **Receipts** cannot be cancelled through the API (draft receipts can be deleted), and lines can be added through the API but not edited or removed.
- **UI** is desktop-first: below 768 px the sidebar is hidden without a menu button. Lists are not paginated in the UI (inventory loads up to 500 rows).
- **Docs**: `docs/SRS.md` and `docs/TECHNICAL.md` are the original design documents with a status banner; the Postman collection only covers the first endpoints.

## Author

**Muhamad Septian Pamungkas**: [github.com/seppam](https://github.com/seppam)

Released under the MIT License.
