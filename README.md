# WMS Distribution Center

A full-stack **Warehouse Management System** for a distribution center. It covers the whole physical cycle: receiving and put-away, stock control, order allocation, picking, packing, shipping, adjustments, transfers and returns, with role-based access and a REST API documented in Swagger.

> **Portfolio case study.** A personal project built to practise designing a realistic WMS domain model, API and UI. It is not affiliated with any company and ships with fictional demo data only.

![Dashboard](docs/screenshots/02-dashboard.webp)

## Try it

Everything below runs a **demo**: fictional data, one shared public account per role, and one-click sign-in buttons on the login page. Nothing to sign up for, no secrets to create.

| Option | You need | Command / link | Opens at |
|---|---|---|---|
| **Docker, one command** (recommended) | Docker with Compose v2 | `git clone https://github.com/seppam/wms-distribution-center && cd wms-distribution-center && docker compose -f docker-compose.demo.yml up --build` | <http://localhost:5174> |
| **GitHub Codespaces**, nothing installed | a GitHub account | [![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/seppam/wms-distribution-center) | the forwarded port **5174** opens automatically |
| **Without the full Docker stack** | .NET SDK 9, Node 22 (20.19+), Docker only for the database | `./scripts/demo.sh` | <http://localhost:5173> |
| **Local development** | .NET SDK 9, Node 22, Docker for the database | see [Run it locally](#run-it-locally) | <http://localhost:5173> |

- First start builds three images and seeds the data: allow 2-4 minutes (5-8 in Codespaces; open the *Ports* tab and click port 5174 when it is ready).
- **Demo accounts**: `admin`, `supervisor`, `operator`, `viewer`, all with the public demo password `Demo!Pass123`. The login page shows a **Demo mode** panel with *Sign in as Admin / Supervisor / Operator / Viewer* buttons. The panel and the *Demo mode: fictional data that resets* banner only appear when the API reports demo mode (`GET /api/demo-info`, a boolean plus role names, never a password). The normal `docker-compose.yml` never creates demo accounts.
- **Reset**: Admin, banner button *Reset demo data* (confirmation, rate limited, Admin only, 404 outside demo mode), or from a terminal `./scripts/reset-demo.sh` (same as `docker compose -f docker-compose.demo.yml run --rm api --reset-demo`; with `scripts/demo.sh` running use `./scripts/demo.sh --reset`). `docker compose -f docker-compose.demo.yml down -v` starts completely from scratch.
- The demo dataset: 24 SKUs in 6 categories with 4 units of measure, 2 warehouses (45 bins in zones: receiving, ambient and chilled storage, packing, shipping, returns), 6 suppliers, 4 carriers, 11 receipts (draft, in progress, partial, received), 16 outbound orders in **every** state (draft, allocated/picking, picked, packed, shipped, delivered, cancelled; the order *destination* is the customer), adjustments (pending, approved, rejected), transfers (draft, done, cancelled) and returns (awaiting inspection, restocked, disposed, written off). It is built by driving the real services, so stock, reservations and the movement ledger are consistent.

### Try it in 3 minutes

After `docker compose -f docker-compose.demo.yml up --build`, open <http://localhost:5174> and follow this order (every step is also exercised by `frontend/e2e/demo/demo.spec.js`):

1. **Look around as Supervisor**: click *Sign in as Supervisor*. The dashboard shows 4 low-stock alerts and 1 expiring lot. Open **Inventory** (on-hand vs reserved per bin, tab *Movement ledger*).
2. **Receive stock**: *Inbound > Receipts*, open the **Draft** receipt `PO-2413` (Northfield Dairy), press *Receive* on each line (the put-away bin is pre-filled), then *Complete receipt*. It turns *Received* and the stock appears in the ledger.
3. **Allocate an order**: *Outbound > Orders*, open the **Draft** order *Brightside Kindergarten*, press *Allocate stock and start picking*. Stock is reserved first-expiry-first-out and pick tasks appear.
4. **Pick, pack, ship** as Operator: *Logout*, *Sign in as Operator*, *Outbound > Pick Tasks*, *Confirm* each task of that order. Then *Orders*, open it, *Pack* (carton weight) and *Ship* (carrier, tracking number). Finally *Shipments > Mark delivered*. The other orders already sit in every other state to compare.
5. **Adjust**: *Adjustments > New adjustment* (e.g. decrease 2 of any product, give a reason). An Operator can only propose; sign in as Supervisor and *Approve* (nobody but an Admin can approve their own request): stock changes and the ledger records it.
6. **Transfer**: *Transfers*, press *Complete* on the pending move, or *New transfer* to move stock to another bin. Reserved stock cannot be moved.
7. **Return**: *Returns > Receive return* against a delivered order, then as Supervisor *Process* it (restock, refurbish, dispose or write off). Restocked goods return to the bin.
8. **Break it and reset**: sign in as Admin, change anything you like (even delete master data), then press **Reset demo data** in the yellow banner. Try *Viewer* too: every action button is gone, and the API would answer 403 anyway (the reset button exists for Admin only).

**What each role can do**: see the [permission matrix](#roles-and-permission-matrix) below (enforced by the API for every endpoint, tested by `AuthorizationMatrixTests`). In short: Viewer reads; Operator does floor work (receive, pick, pack, ship, adjust, transfer, returns); Supervisor also plans orders and receipts, approves adjustments and edits master data; Admin also manages users, roles and deletes.

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

All data below is fictional: the demo dataset (see [Try it](#try-it)), captured by `frontend/e2e/demo-screenshots.mjs`. The login screenshot shows the **Demo mode** panel; the dashboard shows the **Demo mode** banner.

| | |
|---|---|
| ![Login](docs/screenshots/01-login.webp) **Login** with the Demo mode panel | ![Inventory](docs/screenshots/03-inventory.webp) **Inventory** with reserved vs available |
| ![Receipt](docs/screenshots/04-receipt-detail.webp) **Receipt**: partial delivery with a shortage and damaged goods | ![Orders](docs/screenshots/05-outbound.webp) **Outbound orders** in every state |
| ![Order](docs/screenshots/06-order-picking.webp) **Order in Picking**: lines, pick tasks, picker assignment | ![Picks](docs/screenshots/07-pick-tasks.webp) **Pick tasks**: confirm or short-pick |
| ![Adjustments](docs/screenshots/08-adjustments.webp) **Adjustments** awaiting approval | ![Transfers](docs/screenshots/09-transfers.webp) **Transfers** |
| ![Returns](docs/screenshots/10-returns.webp) **Returns** | ![Dashboard](docs/screenshots/02-dashboard.webp) **Dashboard** with the demo banner |

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
scripts/    secret-scan.sh, demo.sh (demo without the full Docker stack), reset-demo.sh
.devcontainer/  GitHub Codespaces configuration (starts the demo)
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

In local development, demo accounts exist **only in the Development environment with `Seed__DemoData=true`** (the default in `appsettings.Development.json`), with a small dataset: `admin`, `supervisor`, `operator`, `viewer`, all with the documented throwaway password `Demo!Pass123`. For the rich dataset, one-click login and reset use the demo options in [Try it](#try-it) instead. In Development without `Jwt__Key` the API generates a random signing key on every start (sessions end when it restarts), so no key lives in the repository.

## Run the full stack with Docker

```bash
cp .env.example .env     # then set POSTGRES_PASSWORD, JWT_KEY (openssl rand -base64 48) and ADMIN_PASSWORD
docker compose up --build
```

Web app: <http://localhost:5174>, API health: <http://localhost:5301/healthz>. Sign in as `admin` with the `ADMIN_PASSWORD` from `.env`. The stack runs in **Production mode**: Swagger is off, there are no demo accounts, migrations run on startup (`Database__MigrateOnStartup=true`), the API refuses to start with a placeholder or missing signing key, and nginx serves the SPA and proxies `/api` so no CORS is needed. Set `SEED_DEMO_DATA=true` in `.env` to also load a small fictional warehouse and products (still no demo accounts). This is the **production-style** path; for trying the app use `docker-compose.demo.yml` ([Try it](#try-it)), which is a separate file so the production compose never gets demo mode. TLS is not terminated here; put a reverse proxy in front for anything public. `docker compose down` stops it (add `--volumes` to drop the database).

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
| `Demo__Enabled` | **Public demo mode, default `false`.** Seeds the four public accounts and the rich fictional dataset, exposes `GET /api/demo-info` as enabled and allows `POST /api/demo/reset` (Admin). Needs `Demo__Password` (8+ characters, public by design); without `Jwt__Key` it generates an ephemeral signing key. Set only by `docker-compose.demo.yml`, `scripts/demo.sh` and the devcontainer |
| `Demo__ResetPermitLimit` / `Demo__ResetWindowSeconds` | Reset budget for the whole instance, default 3 per 300 s (429 with `Retry-After` beyond it) |
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
dotnet test WmsDistributionCenter.sln        # backend: 107 xUnit tests
cd frontend
npm run lint && npm test && npm run build    # 102 Vitest + Testing Library tests
npm run test:e2e                             # 2 Playwright tests, needs the API and web app running (see below)
npm run test:e2e:demo                        # 5 Playwright tests against a running demo stack (E2E_BASE_URL, default :5174)
./scripts/secret-scan.sh                     # run before every push
```

- **Backend (107)**: every workflow (receive, allocate, pick, pack, ship, deliver, cancel, adjust, transfer, return), over-pick and over-allocation protection, an optimistic-concurrency test and a 6-way parallel-allocation test, the authorization matrix (every endpoint x every role), JWT validation, seeding rules, CORS, rate limiting, revoked tokens, health endpoints, migration drift, and a full warehouse cycle across roles through HTTP. They use the in-memory EF provider plus the real ASP.NET pipeline.
- **Frontend (102)**: each page with loading, empty, error and permission states and its create/confirm flows, the generic CRUD page, login, sidebar visibility, and the permission helpers.
- **Demo mode (backend 12, frontend 8, e2e 5)**: the demo endpoint is off by default and in Production/Development/Testing profiles, creates no accounts or data when off, reset is 404 when off and 401/403/200 for anonymous/other roles/Admin, rate limited (429 + `Retry-After`), the reset restores the dataset and repairs the accounts while the admin's session stays valid, every workflow state is seeded and the stock bookkeeping is consistent, each role signs in with its own permissions, the API refuses to start in demo mode without a password; the login panel and banner only render in demo mode; Playwright covers the panel, every order state, a viewer, allocate/pick/pack, receive and reset against the real demo stack.
- **End to end (2 + 5 demo)**: one Playwright run through the browser: receive, put away, create order, allocate, pick, pack, ship, deliver, adjust, transfer, return, then check the ledger; plus a viewer who sees no actions. Locally: start the API and `npm run dev`, then `npx playwright install chromium` (or `PW_CHANNEL=chrome` to use an installed Chrome) and `npm run test:e2e`. The same test passes against the Docker stack with `E2E_BASE_URL=http://localhost:5174 E2E_DEMO_PASSWORD=<ADMIN_PASSWORD>`.
- **CI** (`.github/workflows/ci.yml`): backend tests and a vulnerability audit, frontend lint/test/build, the Playwright test against a real PostgreSQL service, the demo Playwright tests against `docker-compose.demo.yml` (the actual one-command path), and the secret scan.

## Security

- Per-endpoint role policies (above), tokens revalidated against the user record, BCrypt hashes, a dummy hash comparison so unknown users cost the same as wrong passwords.
- Configuration validated at startup (JWT key strength, issuer/audience, expiry, CORS origins), fail fast with a clear message.
- Rate limiting on login and registration; security headers (`nosniff`, frame deny, no referrer); request validation with minimum password length 10.
- No secrets in the repository: signing keys come from the environment (or are generated in Development and in demo mode), the production Docker stack requires `.env` secrets. The **public demo password is public by design** and exists only when `Demo__Enabled=true`; the endpoint that announces demo mode returns a flag and role names, never the password, and the web app only knows the password when it is built with `VITE_DEMO_PASSWORD` (demo compose and scripts only). `scripts/secret-scan.sh` runs in CI and should be run before pushing.
- Reporting issues: open a GitHub issue; this is a portfolio project, not a supported product.

## Known limitations

- **The demo is not a hosted service.** There is no public URL: you run it yourself (Docker, Codespaces or `scripts/demo.sh`). Do not expose a demo stack to the internet with real data or as a shared environment: the accounts and password are public and any Admin can reset everything. Reset budget is per instance (3 per 5 minutes by default), and nothing resets automatically; restarting the containers keeps the data (`down -v` wipes it).
- **No Docker-free database.** The app uses PostgreSQL (jsonb, migrations), so `scripts/demo.sh` still runs the database in Docker; there is no embedded database mode. Codespaces needs a GitHub account and its first start takes a few minutes.
- **Demo data is a snapshot in time**: documents are stamped with the moment of seeding (dates and numbers like `ORD-<today>-0001`), there are no customers as a separate entity (the order *destination* plays that role), and only one warehouse has real depth.

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
