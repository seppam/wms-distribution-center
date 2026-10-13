# Technical Specification
## WMS Distribution Center — Warehouse Management System
**Portfolio case study | Distribution Center**

| Field | Value |
|-------|-------|
| Document ID | TECH-WMS-Distribution Center-001 |
| Version | 1.0 |
| Status | Draft |
| Date | June 2026 |

---

## 1. System Architecture Overview

### 1.1 Architecture Pattern
**3-Tier Web Architecture** (Client-Server-Database)

```
┌─────────────────────────────────────────────────────────┐
│                    CLIENT LAYER                          │
│                                                          │
│   Browser (Chrome / Safari / Firefox)                    │
│   ├── React 18 SPA (Vite Build)                        │
│   ├── JWT Auth Token (stored in localStorage)          │
│   └── HTTPS (TLS 1.2+)                                  │
└────────────────────────┬────────────────────────────────┘
                         │ HTTP/REST (JSON)
                         ▼
┌─────────────────────────────────────────────────────────┐
│                   APPLICATION LAYER                      │
│                                                          │
│   ASP.NET Core 9 Web API                                 │
│   ├── Controllers (13 REST endpoints)                  │
│   ├── Services (8 business logic layer)                 │
│   ├── AutoMapper (DTO ↔ Entity mapping)                 │
│   ├── JWT Bearer Authentication                         │
│   └── BCrypt Password Hashing                           │
│                                                          │
│   Runtime: Kestrel Web Server (no IIS/IIS Express)      │
│   Port: 5300 (Development)                               │
└────────────────────────┬────────────────────────────────┘
                         │ Npgsql / ADO.NET
                         ▼
┌─────────────────────────────────────────────────────────┐
│                     DATA LAYER                          │
│                                                          │
│   PostgreSQL 16                                          │
│   ├── Entity Framework Core 8 (ORM)                    │
│   ├── 25 Tables (fully normalized 3NF)                 │
│   ├── GIN/BTREE Indexes                                 │
│   └── JSONB for flexible attributes                     │
└─────────────────────────────────────────────────────────┘
```

### 1.2 Technology Stack Summary

| Layer | Technology | Version | Purpose |
|-------|-----------|---------|---------|
| **Frontend Runtime** | Vite | 8.x | Build tool, dev server, HMR |
| **Frontend Framework** | React | 18.x | Single Page Application |
| **Routing** | React Router DOM | 6.x | Client-side routing |
| **HTTP Client** | Axios | 1.x | API communication |
| **UI Icons** | Lucide React | Latest | Icon library |
| **State Management** | React Context | Built-in | Auth + role state |
| **Backend Runtime** | .NET 9 | 9.0 | ASP.NET Core Web API |
| **ORM** | Entity Framework Core | 8.0 | Database access |
| **Database Driver** | Npgsql | 8.x | PostgreSQL provider |
| **Auth** | JWT Bearer | Built-in | Token-based auth |
| **Password Hashing** | BCrypt.Net-Next | 2.x | Secure password storage |
| **Mapping** | AutoMapper | 12.x | DTO ↔ Entity mapping |
| **API Docs** | Swagger/OpenAPI | 3.0 | Interactive API docs |
| **Database** | PostgreSQL | 16 | Relational database |

---

## 2. Database Design

### 2.1 Database Schema — 25 Tables

#### Core / Authentication Tables
| # | Table | Description | Rows (Est.) |
|---|-------|-------------|-------------|
| 1 | `roles` | RBAC role definitions | 4 |
| 2 | `users` | System user accounts | 50 |

#### Master Data Tables
| # | Table | Description | Rows (Est.) |
|---|-------|-------------|-------------|
| 3 | `warehouses` | DC facility master | 5 |
| 4 | `zones` | Warehouse zone areas | 30 |
| 5 | `locations` | Individual bin positions | 5,000 |
| 6 | `products` | SKU/product master | 5,000 |
| 7 | `categories` | Product category tree | 200 |
| 8 | `uoms` | Unit of measure definitions | 20 |
| 9 | `suppliers` | Vendor/supplier master | 100 |
| 10 | `carriers` | Shipping carrier master | 30 |

#### Transaction Tables
| # | Table | Description | Rows (Est.) |
|---|-------|-------------|-------------|
| 11 | `inventory` | Current stock per product/location | 50,000 |
| 12 | `inventory_transactions` | All stock movement log | 1,000,000/yr |
| 13 | `inbound_receipts` | Inbound receiving header | 5,000/mo |
| 14 | `receipt_items` | Inbound receiving line items | 50,000/mo |
| 15 | `outbound_orders` | Outbound order headers | 10,000/mo |
| 16 | `order_items` | Outbound order line items | 100,000/mo |
| 17 | `pick_tasks` | Individual picking tasks | 500,000/yr |
| 18 | `pack_lists` | Packing records | 200,000/yr |
| 19 | `shipments` | Shipment header records | 10,000/mo |
| 20 | `return_orders` | Return processing records | 1,000/mo |
| 21 | `adjustments` | Inventory adjustment records | 500/mo |
| 22 | `transfers` | Internal location transfers | 2,000/mo |

#### Audit Tables
| # | Table | Description | Rows (Est.) |
|---|-------|-------------|-------------|
| 23 | `audit_logs` | Immutable system audit trail | 5,000,000/yr |

### 2.2 Entity Relationship Diagram
See: `docs/erd-diagram.html` (interactive HTML visualization)

### 2.3 Key Database Indexes

```sql
-- Composite unique index for inventory (prevents duplicate product/location)
CREATE UNIQUE INDEX ix_inventory_product_location
  ON inventory(product_id, location_id);

-- Index for inventory transactions by reference (fast lookups)
CREATE INDEX ix_inventory_transactions_reference
  ON inventory_transactions(reference_type, reference_id);

-- Index for pick tasks by status (operator dashboard)
CREATE INDEX ix_pick_tasks_status
  ON pick_tasks(status) WHERE status IN ('Assigned', 'InProgress');

-- Index for fast product search
CREATE INDEX ix_products_sku_name
  ON products(sku, name);

-- Index for location search by code
CREATE INDEX ix_locations_code
  ON locations(location_code);

-- Index for audit logs by user and time range
CREATE INDEX ix_audit_logs_user_time
  ON audit_logs(user_id, created_at DESC);

-- GIN index for JSONB permissions in roles
CREATE INDEX ix_roles_permissions
  ON roles USING GIN (permissions);
```

### 2.4 Data Types Reference

| PostgreSQL Type | Usage |
|----------------|-------|
| `UUID` | All primary keys (Id columns) |
| `BIGSERIAL` | High-volume audit/transaction IDs |
| `VARCHAR(n)` | String fields with length constraint |
| `TEXT` | Unlimited text (descriptions, notes) |
| `DECIMAL(p,s)` | All monetary and quantity values |
| `BOOLEAN` | Status flags, active/inactive |
| `TIMESTAMP` | All datetime fields (stored as UTC) |
| `DATE` | Expiry dates, scheduled dates |
| `ENUM` | Status fields with fixed values |
| `JSONB` | Flexible permissions, audit diffs |
| `INET` | IP address logging |

---

## 3. API Specification

### 3.1 Authentication

**Base URL:** `http://localhost:5300/api`

#### POST /auth/login
**Description:** Authenticate user and receive JWT token.

**Request:**
```json
{
  "username": "admin",
  "password": "Demo!Pass123"
}
```

**Response (200):**
```json
{
  "token": "<jwt>",
  "expiresAt": "2026-06-15T08:00:00Z",
  "user": {
    "id": "4645b3d3-1da8-4a8f-b70a-0e6280ea8dff",
    "username": "admin",
    "email": "admin@example.com",
    "fullName": "System Administrator",
    "role": "Admin"
  }
}
```

**Error Response (401):**
```json
{
  "message": "Invalid username or password"
}
```

---

#### POST /auth/register
**Description:** Register new user account. **Admin only.**

**Request:**
```json
{
  "username": "newuser",
  "password": "<your-password>",
  "email": "newuser@example.com",
  "fullName": "New User",
  "roleId": "uuid-of-role"
}
```

---

#### GET /auth/me
**Description:** Get current authenticated user profile.

**Headers:** `Authorization: Bearer <token>`

**Response (200):**
```json
{
  "id": "4645b3d3-...",
  "username": "admin",
  "email": "admin@example.com",
  "fullName": "System Administrator",
  "role": "Admin",
  "warehouseId": "uuid"
}
```

---

### 3.2 Products

#### GET /products
**Description:** List all products with pagination.

**Query Parameters:**
| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `page` | int | 1 | Page number |
| `pageSize` | int | 20 | Items per page (max 100) |
| `search` | string | — | Search by SKU or name |
| `categoryId` | UUID | — | Filter by category |

**Response (200):**
```json
{
  "data": [
    {
      "id": "uuid",
      "sku": "SI-000001",
      "name": "Instant Noodles Pack",
      "category": { "id": "uuid", "name": "Instant Noodles" },
      "uom": { "id": "uuid", "code": "CTN" },
      "weightKg": 4.5,
      "volumeM3": 0.02,
      "barcode": "8998888123456",
      "minStock": 100,
      "maxStock": 5000,
      "isActive": true
    }
  ],
  "totalCount": 1247,
  "page": 1,
  "pageSize": 20,
  "totalPages": 63
}
```

#### POST /products
**Description:** Create new product.

**Request:**
```json
{
  "sku": "SI-000002",
  "name": "Bottled Water 600ml",
  "categoryId": "uuid",
  "uomId": "uuid",
  "weightKg": 9.0,
  "volumeM3": 0.018,
  "barcode": "8998888123457",
  "minStock": 500,
  "maxStock": 20000,
  "isActive": true
}
```

#### PUT /products/{id:guid}
**Description:** Update existing product.

#### DELETE /products/{id:guid}
**Description:** Soft-delete product (set isActive=false).

---

### 3.3 Inventory

#### GET /inventory/summary
**Description:** Get all inventory records with product and location details.

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| `productId` | UUID | Filter by product |
| `locationId` | UUID | Filter by location |
| `status` | string | Available/Reserved/Hold/Damaged |
| `lowStock` | bool | Show only low-stock items |

**Response (200):**
```json
{
  "data": [
    {
      "id": "uuid",
      "product": {
        "id": "uuid",
        "sku": "SI-000001",
        "name": "Instant Noodles Pack"
      },
      "location": {
        "id": "uuid",
        "locationCode": "A-01-03-02",
        "zone": { "name": "Storage-A" }
      },
      "qtyOnHand": 5000,
      "qtyReserved": 200,
      "qtyAvailable": 4800,
      "lotNumber": "LOT-20260601",
      "expiryDate": "2027-06-01",
      "unitCost": 45000.00,
      "status": "Available"
    }
  ],
  "totalCount": 3840
}
```

#### POST /inventory/adjustments
**Description:** Create inventory adjustment (positive/negative).

**Request:**
```json
{
  "productId": "uuid",
  "locationId": "uuid",
  "adjType": "Negative",
  "adjQty": 5,
  "reason": "Cycle count difference — shelf A-01-03-02",
  "notes": "Physical count vs system count discrepancy after quarterly audit"
}
```

**Response (201):**
```json
{
  "adjNumber": "ADJ-20260615-001",
  "qtyBefore": 5000,
  "adjQty": -5,
  "qtyAfter": 4995,
  "status": "Approved"
}
```

#### GET /inventory/transactions
**Description:** Get inventory transaction history (audit trail).

**Query Parameters:** `productId`, `locationId`, `referenceType`, `fromDate`, `toDate`, `page`, `pageSize`

---

### 3.4 Inbound / Receiving

#### GET /inbound
**Description:** List all inbound receipts.

**Query Parameters:** `status`, `supplierId`, `fromDate`, `toDate`, `page`, `pageSize`

#### POST /inbound
**Description:** Create new inbound receipt.

**Request:**
```json
{
  "supplierId": "uuid",
  "warehouseId": "uuid",
  "poNumber": "PO-2026-0500",
  "asnNumber": "ASN-2026-0500",
  "expectedDate": "2026-06-16",
  "dockDoor": "D-03",
  "notes": "Urgent delivery — store reorder"
}
```

#### POST /inbound/{id:guid}/receive
**Description:** Record item received quantities (partial or full).

**Request:**
```json
{
  "items": [
    {
      "productId": "uuid",
      "expectedQty": 100,
      "receivedQty": 98,
      "damagedQty": 2,
      "lotNumber": "LOT-20260616",
      "expiryDate": "2027-06-16",
      "unitCost": 45000.00,
      "putawayLocationId": "uuid"
    }
  ]
}
```

---

### 3.5 Outbound / Orders

#### GET /outbound
**Description:** List all outbound orders.

#### POST /outbound
**Description:** Create new outbound order.

**Request:**
```json
{
  "warehouseId": "uuid",
  "orderType": "Sales",
  "priority": "Urgent",
  "destination": "Store #045",
  "dueDate": "2026-06-16",
  "notes": "Priority restocking",
  "items": [
    {
      "productId": "uuid",
      "orderedQty": 50,
      "unitPrice": 50000.00
    }
  ]
}
```

#### POST /outbound/{id:guid}/start-picking
**Description:** Generate pick tasks from order items (wave planning).

#### POST /outbound/{id:guid}/pack
**Description:** Record packing/cartonization.

#### POST /outbound/{id:guid}/ship
**Description:** Confirm shipment and deduct inventory.

---

### 3.6 Reports

#### GET /reports/dashboard
**Description:** Get dashboard KPI summary.

**Response (200):**
```json
{
  "totalProducts": 1247,
  "totalLocations": 3840,
  "todayInbound": 12,
  "todayOutbound": 28,
  "pendingPickTasks": 15,
  "lowStockItems": 8,
  "inventoryAccuracy": 99.2,
  "receivingAccuracy": 98.5,
  "recentInbound": [...],
  "pendingOutbound": [...]
}
```

---

## 4. Security Architecture

### 4.1 Authentication Flow

```
┌──────────┐          ┌──────────────┐          ┌───────────┐
│  Client  │          │   Backend    │          │ Database  │
└────┬─────┘          └──────┬───────┘          └───────────┘
     │                       │
     │  POST /auth/login     │
     │  {username, password} │
     │───────────────────────►│
     │                       │  Validate credentials
     │                       │  Generate JWT token
     │                       │  ┌──────────────┐
     │                       │  │ JWT Payload: │
     │                       │  │ sub: userId  │
     │                       │  │ name: username│
     │                       │  │ role: Admin   │
     │                       │  │ exp: +8hrs   │
     │                       │  └──────────────┘
     │  {token, user, exp}   │◄──────────────────│
     │◄───────────────────────│
     │                       │
     │  GET /api/products    │
     │  Authorization: Bearer │
     │  eyJhbGciOiJIUzI1...  │
     │───────────────────────►│
     │                       │  Validate JWT
     │                       │  Check RBAC permissions
     │  {data: [...]}        │◄──────────────────│
     │◄───────────────────────│
```

### 4.2 JWT Token Structure

**Header:**
```json
{
  "alg": "HS256",
  "typ": "JWT"
}
```

**Payload:**
```json
{
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "4645b3d3-...",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": "admin",
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress": "admin@example.com",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Admin",
  "exp": 1781485336,
  "iss": "WmsDistributionCenter.Api",
  "aud": "WmsDistributionCenter.Client"
}
```

### 4.3 Password Security
- Algorithm: **BCrypt** with cost factor 11
- Hash format: `$2a$11$...` (61 characters)
- Minimum password length: 8 characters
- Stored: BCrypt hash only (plaintext never stored)

---

## 5. Project Structure

### 5.1 Backend (.NET)

```
backend/WmsDistributionCenter.Api/
├── Configuration/
│   └── MappingProfile.cs          # AutoMapper config
├── Controllers/
│   ├── AuthController.cs          # Login, Register, Me
│   ├── UsersController.cs         # User CRUD
│   ├── RolesController.cs         # Role CRUD + tree
│   ├── ProductsController.cs      # Product CRUD
│   ├── CategoriesController.cs    # Category CRUD + tree
│   ├── WarehousesController.cs    # Warehouse CRUD
│   ├── ZonesController.cs         # Zone CRUD
│   ├── LocationsController.cs     # Location CRUD
│   ├── InventoryController.cs     # Stock summary + adjustments
│   ├── InboundReceiptsController.cs # Receiving workflow
│   ├── OutboundOrdersController.cs # Orders + fulfillment
│   ├── ReportsController.cs       # Dashboard KPIs
│   ├── SuppliersController.cs     # Supplier CRUD
│   └── CarriersController.cs      # Carrier CRUD
├── Data/
│   ├── WmsDbContext.cs            # EF Core DbContext
│   └── DbSeeder.cs                # Initial data seeder
├── DTOs/
│   ├── Auth/                       # Login DTOs
│   ├── User/                      # User DTOs
│   ├── Role/                      # Role DTOs
│   ├── Product/                   # Product DTOs
│   ├── Category/                  # Category DTOs
│   ├── Warehouse/                 # Warehouse DTOs
│   ├── Zone/                     # Zone DTOs
│   ├── Location/                  # Location DTOs
│   ├── Inventory/                 # Inventory DTOs
│   ├── InboundReceipt/            # Inbound DTOs
│   ├── OutboundOrder/             # Outbound DTOs
│   ├── Report/                    # Report DTOs
│   ├── Supplier/                 # Supplier DTOs
│   ├── Carrier/                   # Carrier DTOs
│   ├── Adjustment/                # Adjustment DTOs
│   ├── Transfer/                  # Transfer DTOs
│   └── Uom/                       # UOM DTOs
├── Enums/                         # 23 C# enum types
├── Models/
│   ├── User.cs, Role.cs, Warehouse.cs, Zone.cs,
│   ├── Location.cs, Product.cs, Category.cs, Uom.cs,
│   ├── Supplier.cs, Carrier.cs,
│   ├── Inventory.cs, InventoryTransaction.cs,
│   ├── InboundReceipt.cs, ReceiptItem.cs,
│   ├── OutboundOrder.cs, OrderItem.cs, PickTask.cs,
│   ├── PackList.cs, Shipment.cs,
│   ├── ReturnOrder.cs, Adjustment.cs, Transfer.cs, AuditLog.cs
├── Services/
│   ├── IAuthService.cs, AuthService.cs
│   ├── IUserService.cs, UserService.cs
│   ├── IRoleService.cs, RoleService.cs
│   ├── IProductService.cs, ProductService.cs
│   ├── IInventoryService.cs, InventoryService.cs
│   ├── IInboundService.cs, InboundService.cs
│   ├── IOutboundService.cs, OutboundService.cs
│   └── IReportService.cs, ReportService.cs
├── Program.cs                     # App startup + DI configuration
└── appsettings.json              # Configuration
```

### 5.2 Frontend (React)

```
frontend/src/
├── components/
│   ├── layout/
│   │   ├── Layout.jsx           # Main layout wrapper
│   │   ├── Sidebar.jsx         # Navigation sidebar
│   │   ├── Sidebar.css         # Sidebar styles
│   │   └── Layout.css          # Global styles
│   └── ui/
│       └── CrudPage.jsx        # Reusable CRUD page component
├── contexts/
│   └── AuthContext.jsx         # JWT auth + role state
├── pages/
│   ├── Dashboard.jsx            # KPI dashboard
│   ├── Login.jsx              # Login page
│   ├── Products.jsx            # Product CRUD
│   ├── Categories.jsx          # Category CRUD
│   ├── Users.jsx               # User management
│   ├── Roles.jsx               # Role management
│   ├── Warehouses.jsx           # Warehouse CRUD
│   ├── Zones.jsx               # Zone CRUD
│   ├── Locations.jsx            # Location CRUD
│   ├── Inventory.jsx           # Stock overview
│   ├── Inbound.jsx              # Inbound receipts
│   ├── Outbound.jsx             # Outbound orders
│   ├── Suppliers.jsx             # Supplier CRUD
│   ├── Carriers.jsx             # Carrier CRUD
│   ├── Transfers.jsx            # Internal transfers
│   ├── Returns.jsx              # Returns processing
│   ├── Adjustments.jsx          # Stock adjustments
│   ├── Picks.jsx                # Pick task management
│   ├── Shipments.jsx            # Shipment tracking
│   └── Uoms.jsx                 # UOM configuration
├── services/
│   ├── api.js                  # Axios instance + interceptors
│   └── index.js               # Named API service functions
├── App.jsx                     # Router + route definitions
├── App.css                     # Global CSS variables + styles
└── main.jsx                    # React root
```

---

## 6. Configuration

### 6.1 Environment Variables

**Backend (`appsettings.json` / `appsettings.Development.json`):**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=wms_dc;Username=wms_admin;Password=<set via env/user-secrets>"
  },
  "Jwt": {
    "Key": "<set via Jwt__Key env var, min 32 chars>",
    "Issuer": "WmsDistributionCenter.Api",
    "Audience": "WmsDistributionCenter.Client",
    "ExpiryMinutes": 480
  },
  "AllowedOrigins": [
    "http://localhost:5173",
    "http://localhost:5173"
  ]
}
```

**Frontend (`.env`):**

```env
VITE_API_URL=http://localhost:5300/api
```

### 6.2 Secrets Management (Production)
In production, store secrets in environment variables or a secrets manager:
- **Azure:** Azure Key Vault
- **AWS:** AWS Secrets Manager
- **Local Dev:** `.env` files (gitignored)

---

## 7. Deployment Architecture (Future)

### Phase 1 (Current — Development)
- Backend: `dotnet run` on local machine
- Frontend: `npm run dev` on local machine
- Database: PostgreSQL 16 on local machine

### Phase 2 (Staging)
- Backend: **Railway** or **Azure App Service** (containerized)
- Frontend: **Vercel** (static hosting)
- Database: **Supabase** or **Azure Database for PostgreSQL**

### Phase 3 (Production)
- Backend: **Azure App Service** with auto-scale
- Frontend: **Vercel** with CDN
- Database: **Azure Database for PostgreSQL** (HA with read replica)
- CI/CD: **GitHub Actions**
- Monitoring: **Application Insights** or **Datadog**
- Secrets: **Azure Key Vault**

---

## 8. Dependencies & Versions

### Backend NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.EntityFrameworkCore | 8.0.x | ORM framework |
| Microsoft.EntityFrameworkCore.Design | 8.0.x | EF migrations tooling |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.x | PostgreSQL provider |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.x | JWT authentication |
| BCrypt.Net-Next | 2.0.x | Password hashing |
| AutoMapper | 12.0.x | DTO ↔ Entity mapping |
| Swashbuckle.AspNetCore | 6.5.x | Swagger/OpenAPI docs |

### Frontend NPM Packages

| Package | Version | Purpose |
|---------|---------|---------|
| react | 18.x | UI framework |
| react-dom | 18.x | DOM rendering |
| react-router-dom | 6.x | Client-side routing |
| axios | 1.x | HTTP client |
| lucide-react | Latest | Icon library |
| recharts | Latest | Dashboard charts |
| @tanstack/react-table | Latest | Data tables |
| date-fns | Latest | Date formatting |

---

*Document approved by: _________________ Date: _____________*
