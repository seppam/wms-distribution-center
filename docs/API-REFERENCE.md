# API Reference

REST API of the WMS Distribution Center. In Development the interactive Swagger UI is served at `/swagger`
(`http://localhost:5300/swagger`); it is the source of truth for request and response shapes. This page lists every
route with the role that may call it. The same table is enforced by `AuthorizationMatrixTests`, which fails if a route
is added without being listed or answers differently from this table.

- **Base URL**: `http://localhost:5300/api` (Docker compose: `http://localhost:5301/api`, or `/api` through the web container).
- **Authentication**: `POST /api/auth/login` returns `{ "token", "expiresAt", "user" }`. Send `Authorization: Bearer <token>`.
  A token stops working immediately when the user is deactivated or their role changes.
- **Roles**: Admin, Supervisor, Operator, Viewer. "Any signed-in user" includes Viewer. Calling a route without the
  required role returns `403`; without a token `401`.
- **Enums** are strings (`"Normal"`, `"Picking"`, ...). Dates are ISO 8601 (`DateOnly` fields use `yyyy-MM-dd`).
- **Health**: `GET /healthz` (liveness) and `GET /readyz` (database), anonymous.

## Errors

Failures return a JSON problem body with a human readable `message`:

```json
{ "status": 400, "title": "Over-pick rejected: you can pick at most 20 (requested 25).", "message": "Over-pick rejected: you can pick at most 20 (requested 25)." }
```

| Status | Meaning |
|---|---|
| 400 | Validation failed or a business rule was violated (insufficient stock, wrong state, over-pick, ...) |
| 401 | Missing, invalid or revoked token |
| 403 | Signed in, but the role is not allowed |
| 404 | Record not found (also returned by `/auth/register` when self-registration is disabled) |
| 409 | Conflict: concurrent stock change (retry), duplicate or still referenced record |
| 429 | Too many login or registration attempts from one address (default 10 per minute) |

## Workflow in one example

```bash
TOKEN=$(curl -s localhost:5300/api/auth/login -H 'content-type: application/json' \
  -d '{"username":"supervisor","password":"<demo password>"}' | jq -r .token)

# 1. plan an order with lines, 2. allocate stock (creates pick tasks)
curl -s localhost:5300/api/outboundorders -H "authorization: Bearer $TOKEN" -H 'content-type: application/json' \
  -d '{"warehouseId":"<id>","orderType":"Sales","priority":"Normal","destination":"Store 12","items":[{"productId":"<id>","orderedQty":10,"unitPrice":4.5}]}'
curl -s -X POST localhost:5300/api/outboundorders/<orderId>/start-picking -H "authorization: Bearer $TOKEN"
# 3. an operator confirms each pick task, then packs and ships (see the table below)
```

## Status lifecycles

- **Receipt**: `Draft` -> `InProgress` (first line received) -> `Received` (everything matches) or `Partial`; `Cancelled` exists in the model but no endpoint sets it yet (delete a draft instead).
- **Outbound order**: `Draft` -> `Picking` (stock allocated) -> `Picked` (all tasks confirmed) -> `Packed` -> `Shipped` -> `Completed` (delivered); `Cancelled` from any state before shipping.
- **Pick task**: `Assigned` -> `Picked` (or `Cancelled`).
- **Adjustment**: `Draft` -> `Approved` (applied) or `Cancelled` (rejected). **Transfer**: `Draft` -> `Completed` or `Cancelled`.
- **Return**: `Received` -> `Completed` after a disposition (`Restock`, `Refurbish`, `Dispose`, `WriteOff`).

## Routes

### Auth

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/auth/me` | Any signed-in user | Current user |
| POST | `/api/auth/login` | Anyone (rate limited) | Sign in, returns a JWT and the user |
| POST | `/api/auth/register` | Anyone (rate limited) | Self-registration (Viewer role); returns 404 unless Auth:AllowSelfRegistration is true |

### Demo mode

Inert unless `Demo__Enabled=true` (see the README, "Try it").

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/demo-info` | Anyone | `{ "enabled": false }`, or `{ "enabled": true, "accounts": [{ "role", "username" }] }` in demo mode. Never contains a password |
| POST | `/api/demo/reset` | Admin | Restores the seeded dataset and the four demo accounts. 404 when demo mode is off, 429 with `Retry-After` beyond the reset budget (default 3 per 300 s). The CLI equivalent is `--reset-demo` |

### Users

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/users` | Admin, Supervisor |  |
| GET | `/api/users/{id}` | Admin, Supervisor |  |
| POST | `/api/users` | Admin |  |
| PUT | `/api/users/{id}` | Admin |  |
| DELETE | `/api/users/{id}` | Admin |  |

### Roles

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/roles` | Admin |  |
| GET | `/api/roles/{id}` | Admin |  |
| POST | `/api/roles` | Admin |  |
| PUT | `/api/roles/{id}` | Admin |  |
| DELETE | `/api/roles/{id}` | Admin |  |

### Products

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/products` | Any signed-in user |  |
| GET | `/api/products/search` | Any signed-in user | Search by name, SKU or barcode (`q`) |
| GET | `/api/products/{id}` | Any signed-in user |  |
| POST | `/api/products` | Admin, Supervisor |  |
| PUT | `/api/products/{id}` | Admin, Supervisor |  |
| DELETE | `/api/products/{id}` | Admin |  |

### Categories

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/categories` | Any signed-in user |  |
| GET | `/api/categories/tree` | Any signed-in user | Category hierarchy |
| GET | `/api/categories/{id}` | Any signed-in user |  |
| POST | `/api/categories` | Admin, Supervisor |  |
| PUT | `/api/categories/{id}` | Admin, Supervisor |  |
| DELETE | `/api/categories/{id}` | Admin |  |

### Units of measure

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/uoms` | Any signed-in user |  |
| GET | `/api/uoms/{id}` | Any signed-in user |  |
| POST | `/api/uoms` | Admin, Supervisor |  |
| PUT | `/api/uoms/{id}` | Admin, Supervisor |  |
| DELETE | `/api/uoms/{id}` | Admin |  |

### Suppliers

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/suppliers` | Any signed-in user |  |
| GET | `/api/suppliers/{id}` | Any signed-in user |  |
| POST | `/api/suppliers` | Admin, Supervisor |  |
| PUT | `/api/suppliers/{id}` | Admin, Supervisor |  |
| DELETE | `/api/suppliers/{id}` | Admin |  |

### Carriers

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/carriers` | Any signed-in user |  |
| GET | `/api/carriers/{id}` | Any signed-in user |  |
| POST | `/api/carriers` | Admin, Supervisor |  |
| PUT | `/api/carriers/{id}` | Admin, Supervisor |  |
| DELETE | `/api/carriers/{id}` | Admin |  |

### Warehouses

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/warehouses` | Any signed-in user |  |
| GET | `/api/warehouses/{id}` | Any signed-in user |  |
| POST | `/api/warehouses` | Admin, Supervisor |  |
| PUT | `/api/warehouses/{id}` | Admin, Supervisor |  |
| DELETE | `/api/warehouses/{id}` | Admin |  |

### Zones

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/zones` | Any signed-in user |  |
| GET | `/api/zones/{id}` | Any signed-in user |  |
| POST | `/api/zones` | Admin, Supervisor |  |
| PUT | `/api/zones/{id}` | Admin, Supervisor |  |
| DELETE | `/api/zones/{id}` | Admin |  |

### Locations

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/locations` | Any signed-in user |  |
| GET | `/api/locations/{id}` | Any signed-in user |  |
| POST | `/api/locations` | Admin, Supervisor |  |
| PUT | `/api/locations/{id}` | Admin, Supervisor |  |
| DELETE | `/api/locations/{id}` | Admin |  |

### InboundReceipts

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/inboundreceipts` | Any signed-in user |  |
| GET | `/api/inboundreceipts/{id}` | Any signed-in user | Receipt with its lines |
| POST | `/api/inboundreceipts` | Admin, Supervisor | Create a receipt, optionally with lines (`items`) |
| POST | `/api/inboundreceipts/{id}/complete` | Admin, Supervisor, Operator | Close the receipt: Received, or Partial when anything is short, damaged or untouched |
| POST | `/api/inboundreceipts/{id}/items` | Admin, Supervisor | Add an expected line |
| POST | `/api/inboundreceipts/{id}/receive` | Admin, Supervisor, Operator | Receive a line and put the good quantity away in one step (repeatable; only the delta is posted) |
| PUT | `/api/inboundreceipts/{id}` | Admin, Supervisor | Edit a draft receipt |
| DELETE | `/api/inboundreceipts/{id}` | Admin, Supervisor | Delete a draft receipt |

### OutboundOrders

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/outboundorders` | Any signed-in user |  |
| GET | `/api/outboundorders/{id}` | Any signed-in user | Order with lines, cartons and shipments |
| GET | `/api/outboundorders/{id}/items` | Any signed-in user |  |
| POST | `/api/outboundorders` | Admin, Supervisor | Create a draft order, optionally with lines (`items`) |
| POST | `/api/outboundorders/{id}/assign-picker` | Admin, Supervisor | Assign the open pick tasks to a floor user |
| POST | `/api/outboundorders/{id}/cancel` | Admin, Supervisor | Cancel an order that has not shipped; releases its reservations |
| POST | `/api/outboundorders/{id}/items` | Admin, Supervisor | Add a line to a draft order |
| POST | `/api/outboundorders/{id}/pack` | Admin, Supervisor, Operator | Pack a fully picked order into a carton: Picked to Packed |
| POST | `/api/outboundorders/{id}/ship` | Admin, Supervisor, Operator | Ship a packed order: deducts stock, logs `Out` transactions, creates the shipment |
| POST | `/api/outboundorders/{id}/start-picking` | Admin, Supervisor | Allocate stock (first-expiry-first-out, all-or-nothing) and create pick tasks: Draft to Picking |
| PUT | `/api/outboundorders/{id}` | Admin, Supervisor | Edit a draft order |
| DELETE | `/api/outboundorders/{id}` | Admin, Supervisor | Delete a draft order |

### Pick tasks

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/pick-tasks` | Any signed-in user | List tasks (`status`, `orderId`, `mine`) |
| POST | `/api/pick-tasks/{id}/confirm` | Admin, Supervisor, Operator | Confirm a pick `{ qtyPicked }`; more than requested is rejected, less is a short pick |

### Shipments

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/shipments` | Any signed-in user | List shipments |
| POST | `/api/shipments/{id}/deliver` | Admin, Supervisor, Operator | Mark delivered; the order becomes Completed |

### Inventory

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/inventory` | Any signed-in user | Stock rows (`productId`, `locationId`, `warehouseId`, `status`, `page`, `pageSize` up to 500) |
| GET | `/api/inventory/adjustments` | Any signed-in user | List adjustments |
| GET | `/api/inventory/transactions` | Any signed-in user | Immutable movement ledger (`productId`, `locationId`, `take` up to 500) |
| GET | `/api/inventory/{id}` | Any signed-in user |  |
| POST | `/api/inventory/adjustments` | Admin, Supervisor, Operator | Propose an adjustment (Positive, Negative or Recount); stock is unchanged until approved |
| POST | `/api/inventory/adjustments/{id}/approve` | Admin, Supervisor | Approve and apply (recalculated against current stock; non-admins cannot approve their own) |
| POST | `/api/inventory/adjustments/{id}/reject` | Admin, Supervisor | Reject a draft adjustment |

### Transfers

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/transfers` | Any signed-in user | List transfers |
| GET | `/api/transfers/{id}` | Any signed-in user |  |
| POST | `/api/transfers` | Admin, Supervisor, Operator | Create a draft transfer (only unreserved stock can be moved) |
| POST | `/api/transfers/{id}/cancel` | Admin, Supervisor, Operator | Cancel a draft transfer |
| POST | `/api/transfers/{id}/complete` | Admin, Supervisor, Operator | Move the stock and write the Out and In ledger entries |

### Returns

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/returns` | Any signed-in user | List returns |
| GET | `/api/returns/{id}` | Any signed-in user |  |
| POST | `/api/returns` | Admin, Supervisor, Operator | Receive a return, optionally against a shipped order (quantity checked against what shipped) |
| POST | `/api/returns/{id}/process` | Admin, Supervisor | Decide the disposition; Restock (good condition only) puts the goods back into a location |

### Reports

| Method | Route | Who | Notes |
|---|---|---|---|
| GET | `/api/reports/dashboard` | Any signed-in user | KPIs |
| GET | `/api/reports/inbound-summary` | Any signed-in user |  |
| GET | `/api/reports/inventory-summary` | Any signed-in user |  |
| GET | `/api/reports/outbound-summary` | Any signed-in user |  |

## Postman

`wms-distribution-center.postman_collection.json` contains a starting collection (base URL variable `baseUrl`). It predates
the workflow endpoints above; prefer Swagger for the complete list.
