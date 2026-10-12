# User Manual
## WMS Distribution Center — Warehouse Management System
**Portfolio case study | Distribution Center**

---

## Table of Contents
1. [Getting Started](#1-getting-started)
2. [Dashboard Overview](#2-dashboard-overview)
3. [Master Data Management](#3-master-data-management)
4. [Inbound / Receiving](#4-inbound--receiving)
5. [Inventory Management](#5-inventory-management)
6. [Outbound / Orders](#6-outbound--orders)
7. [User & Role Administration](#7-user--role-administration)
8. [Reports & KPIs](#8-reports--kpis)
9. [Troubleshooting](#9-troubleshooting)

---

## 1. Getting Started

### 1.1 Logging In

1. Open your browser and navigate to: `http://localhost:3000`
2. Enter your **Username** and **Password**
3. Click **Sign In**
4. Upon successful login, you will be redirected to the Dashboard

**Default Credentials:**

| Role | Username | Password (demo seed only) |
|------|----------|----------|
| Admin | `admin` | `admin123` |

> ⚠️ **Security Notice:** Change default passwords immediately after first login.

### 1.2 Understanding the Interface

```
┌─────────────────────────────────────────────────────────────────┐
│  [Logo] WMS Distribution Center              [Admin ▼]  [Logout]        │
├─────────────────────────────────────────────────────────────────┤
│ ┌──────────┐ ┌──────────────────────────────────────────────┐  │
│ │          │ │                                              │  │
│ │ SIDEBAR  │ │          MAIN CONTENT AREA                   │  │
│ │          │ │                                              │  │
│ │ Dashboard│ │  Page title + subtitle                        │  │
│ │ ─────────│ │  ────────────────────────────────────────     │  │
│ │ Master   │ │                                              │  │
│ │  Products│ │  [Content: Tables, Forms, Charts]            │  │
│ │  Categories│                                              │  │
│ │  ...     │ │                                              │  │
│ │ ─────────│ │                                              │  │
│ │ Warehouse│ │                                              │  │
│ │ ─────────│ │                                              │  │
│ │ Inventory│ │                                              │  │
│ │ ─────────│ │                                              │  │
│ │ Inbound  │ │                                              │  │
│ │ Outbound │ │                                              │  │
│ │ ─────────│ │                                              │  │
│ │ Admin ▼  │ │                                              │  │
│ │  Users   │ │                                              │  │
│ │  Roles   │ │                                              │  │
│ └──────────┘ └──────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### 1.3 Navigation Menu

The sidebar shows only the modules your role has access to:
- **Dashboard** — Visible to all roles
- **Master Data** — Products, Categories, Suppliers, Carriers, UOMs
- **Warehouse** — Warehouses, Zones, Locations (Bins)
- **Inventory** — Stock levels and transactions
- **Inbound** — Receiving receipts
- **Outbound** — Orders, Pick Tasks, Shipments
- **Admin** — Users, Roles (Admin only)

---

## 2. Dashboard Overview

The Dashboard provides a real-time overview of DC operations.

### 2.1 KPI Cards

| Card | Description | Refresh |
|------|-------------|---------|
| **Total SKUs** | Number of active products in the system | Real-time |
| **Storage Locations** | Number of active bin locations | Real-time |
| **Today's Receipts** | Number of inbound receipts received today | Real-time |
| **Today's Shipments** | Number of outbound shipments dispatched today | Real-time |
| **Pending Picks** | Number of pick tasks awaiting completion | Real-time |
| **Low Stock Alerts** | Products below minimum stock threshold | Real-time |
| **Inventory Accuracy** | % of inventory counts matching system records | Daily |
| **Receiving Accuracy** | % of receipts with no discrepancies | Daily |

### 2.2 Recent Activity Tables

- **Recent Inbound Receipts** — Last 5 receipts with status
- **Pending Outbound Orders** — Orders awaiting fulfillment

---

## 3. Master Data Management

### 3.1 Products

Products are the core SKU master data. Every product has a unique SKU code.

**To add a new product:**
1. Navigate to **Master Data → Products**
2. Click **+ Add New**
3. Fill in the required fields:
   - **SKU** — Unique product code (e.g., `SI-000001`)
   - **Name** — Product display name
   - **Category** — Select from category tree
   - **UOM** — Unit of Measure (PCS, CTN, PLT)
   - **Weight (kg)** — Unit weight
   - **Volume (m³)** — Unit volume
   - **Barcode** — Product barcode (EAN-13, etc.)
   - **Min Stock** — Reorder threshold
   - **Max Stock** — Maximum stock level
4. Click **Create**

**To edit a product:** Click the **Edit** (pencil) icon on any product row.

### 3.2 Categories

Categories support a **hierarchical tree structure** (e.g., Food → Snacks → Chips).

**To add a category:**
1. Navigate to **Master Data → Categories**
2. Click **+ Add New**
3. Enter category name
4. Optionally select a **Parent Category** to create a subcategory
5. Click **Create**

### 3.3 Warehouses

Define your DC facilities.

**Warehouse Code Examples:**
- `DC-MAIN` — Main Distribution Center
- `DC-TNG` — DC Tangerang
- `DC-SEC` — Secondary DC

### 3.4 Zones

Zones divide a warehouse into functional areas.

| Zone Type | Description |
|-----------|-------------|
| **Receiving** | Inbound dock area |
| **Storage** | Main racking/storage area |
| **Packing** | Packing station area |
| **Shipping** | Outbound dock area |
| **Returns** | Return processing area |
| **Staging** | Temporary holding area |

**Location Code Structure:** `Zone-Aisle-Rack-Level-Bin`
Example: `A-01-03-02` = Zone A, Aisle 01, Rack 03, Level 02

### 3.5 Locations (Bins)

Each storage position in the warehouse is a **Location**.

**Location Types:**
| Type | Description |
|------|-------------|
| **Pick** | Primary picking face — high-velocity items |
| **Floor** | Floor-level storage |
| **Bulk** | Reserve/bulk storage |
| **Reserved** | Quarantine, inspection hold |
| **Damage** | Damaged goods area |

---

## 4. Inbound / Receiving

The inbound process handles goods arriving from suppliers.

### 4.1 Process Flow

```
Supplier Shipment → PO/ASN Match → Receiving → QC Check → Put-away → Inventory Update
```

### 4.2 Creating a Receipt

1. Navigate to **Inbound → Receipts**
2. Click **+ New Receipt**
3. Fill in:
   - **Supplier** — Select from supplier list
   - **PO Number** — Purchase Order reference
   - **ASN Number** — Advance Shipping Notice (optional)
   - **Expected Date** — Expected arrival date
   - **Dock Door** — Assigned receiving dock
4. Click **Create**

### 4.3 Receiving Items

Once the truck arrives and goods are physically received:

1. Open the receipt record
2. Click **Receive Items**
3. For each item:
   - Enter **Received Quantity** (expected vs. actual)
   - Enter **Damaged Quantity** (if any)
   - Enter **Lot Number** (from supplier label)
   - Enter **Expiry Date** (if applicable)
   - Select **Put-away Location** (where the goods will be stored)
4. Click **Confirm Receipt**

### 4.4 Status Definitions

| Status | Meaning |
|--------|---------|
| **Draft** | Receipt created but receiving not started |
| **In Progress** | Receiving underway |
| **Received** | All items fully received |
| **Partial** | Some items partially received |
| **Cancelled** | Receipt cancelled |

---

## 5. Inventory Management

### 5.1 Viewing Stock Levels

Navigate to **Inventory** to see all stock levels.

**Filters:**
- By Product (search by SKU or name)
- By Location (search by location code)
- By Status (Available / Reserved / Hold / Damaged)
- Low Stock Only

### 5.2 Stock Adjustment

When physical count differs from system record:

1. Navigate to **Inventory**
2. Click **Adjust** on the relevant row
3. Enter:
   - **Adjustment Type** — Positive (+) or Negative (-)
   - **Adjustment Quantity** — How many units to adjust
   - **Reason** — e.g., "Cycle count", "Damage found", "Theft"
   - **Notes** — Additional details
4. Click **Submit**

> ⚠️ All adjustments are logged in the audit trail with the user's name and timestamp.

### 5.3 Internal Transfer

Moving stock from one location to another:

1. Navigate to **Transfers** (coming in Phase 2)
2. Select product, source location, and destination location
3. Enter quantity to transfer
4. Submit transfer

---

## 6. Outbound / Orders

### 6.1 Process Flow

```
Sales Order Created → Wave Planning → Pick Task Assignment → Picking → Packing → Shipping → Invoice
```

### 6.2 Creating an Order

1. Navigate to **Outbound → Orders**
2. Click **+ New Order**
3. Fill in:
   - **Order Type** — Sales / Transfer / Internal / Sample
   - **Destination** — Store name and address
   - **Priority** — Normal / Urgent / Rush
   - **Due Date** — Required delivery date
4. Add line items (select product, enter quantity)
5. Click **Create**

### 6.3 Picking Process

1. **Supervisor** generates pick tasks (Start Picking)
2. **Picker** is assigned to pick tasks
3. **Picker** goes to each location, scans barcode, confirms quantity
4. System marks pick task as **Picked**

### 6.4 Packing & Shipping

1. **Packer** packs items into cartons
2. **Carton weight** is recorded
3. **Carrier** is assigned
4. **BOL (Bill of Lading)** is generated
5. **Ship** confirms dispatch and deducts inventory

### 6.5 Order Status Definitions

| Status | Meaning |
|--------|---------|
| **Draft** | Order created, not yet released |
| **Picked** | All items picked from locations |
| **Packed** | All items packed into cartons |
| **Shipped** | Dispatched to carrier |
| **Completed** | Delivery confirmed |
| **Cancelled** | Order cancelled |

---

## 7. User & Role Administration

### 7.1 User Management (Admin Only)

**To create a new user:**
1. Navigate to **Admin → Users**
2. Click **+ Add New**
3. Fill in:
   - **Username** — Login name
   - **Full Name** — Display name
   - **Email** — Work email
   - **Phone** — Contact number
   - **Password** — Initial password
   - **Role** — Select role (Admin/Supervisor/Operator/Viewer)
4. Click **Create**

### 7.2 Role Management (Admin Only)

Roles define what users can see and do.

| Role | Can Do |
|------|--------|
| **Admin** | Full access — system config, users, all operations |
| **Supervisor** | Manage operations in assigned zones, approve adjustments |
| **Operator** | Execute assigned tasks (receive, pick, pack) |
| **Viewer** | Read-only access to all modules |

### 7.3 Password Policy

- Minimum 8 characters
- Recommend: mix of letters, numbers, and symbols
- Change passwords every 90 days

---

## 8. Reports & KPIs

### 8.1 Dashboard KPIs

| KPI | Formula | Target |
|-----|---------|--------|
| **Inventory Accuracy** | (System qty = Physical qty) / Total locations × 100% | ≥ 99% |
| **Receiving Accuracy** | Receipts with no discrepancies / Total receipts × 100% | ≥ 98% |
| **On-Time Shipment** | Orders shipped on or before due date / Total orders × 100% | ≥ 95% |
| **Pick Productivity** | Lines picked per hour per picker | ≥ 100 lines/hr |
| **Dock-to-Stock Time** | Time from receipt to items available for picking | ≤ 4 hours |

### 8.2 Generating Reports

Reports are accessed via the **Reports** module (Admin/Supervisor only). Available reports include:
- Inbound summary by date range
- Outbound summary by date range
- Inventory valuation report
- Pick task productivity report
- Stock movement history

---

## 9. Troubleshooting

### 9.1 Login Issues

| Problem | Solution |
|---------|----------|
| "Invalid credentials" | Check username/password. Passwords are case-sensitive |
| "Session expired" | Reload the page and log in again |
| Can't log in | Contact Admin to reset password |

### 9.2 API Errors

| Error | Meaning | Solution |
|-------|---------|----------|
| `401 Unauthorized` | JWT token expired | Log out and log back in |
| `403 Forbidden` | No permission for this action | Contact Admin to update your role |
| `404 Not Found` | Record doesn't exist | Check if the item was deleted |
| `409 Conflict` | Duplicate record | SKU/Code already exists — use a different value |

### 9.3 System Errors

| Problem | Solution |
|---------|----------|
| Page won't load | Clear browser cache, reload page |
| Data not saving | Check your internet connection, try again |
| Dashboard shows no data | Backend may not be running — restart with `dotnet run` |

### 9.4 Contact

| Issue | Contact |
|-------|---------|
| Login / Access | IT Admin |
| Operational questions | DC Supervisor |
| System bugs / suggestions | WMS Specialist |

---

## Appendix: Keyboard Shortcuts

| Action | Shortcut |
|--------|----------|
| Logout | Click user dropdown → Logout |
| Search | Click the search bar or press `/` |
| Go to Dashboard | Press `G` then `D` |
| New Record | Click `+ Add New` button |

---

*Document Version: 1.0 — June 2026*
