# Software Requirements Specification (SRS)
## WMS Distribution Center — Warehouse Management System
**Portfolio case study | Distribution Center**

| Field | Value |
|-------|-------|
| Document ID | SRS-WMS-Distribution Center-001 |
| Version | 1.0 |
| Status | Draft |
| Author | DC System Specialist |
| Date | June 2026 |
| Classification | Internal Use |

---

## 1. Introduction

### 1.1 Purpose
This document defines the functional and non-functional requirements for the Warehouse Management System (WMS) to be deployed at a Distribution Center in Indonesia.

The WMS shall serve as the central technology platform managing all inbound logistics, storage, inventory, outbound fulfillment, and warehouse operations for the DC.

### 1.2 Scope
The system covers the following operational domains:
- **Inbound Operations**: Supplier delivery receipt, quality control, put-away
- **Storage Management**: Location management, bin management, zone configuration
- **Inventory Control**: Real-time stock tracking, lot/batch management, expiry tracking
- **Outbound Operations**: Order fulfillment, picking, packing, shipping
- **Returns Processing**: Customer/store returns, inspection, disposition
- **Administration**: User management, role-based access control, audit logging

### 1.3 Definitions and Abbreviations

| Term | Definition |
|------|-----------|
| WMS | Warehouse Management System |
| DC | Distribution Center |
| SKU | Stock Keeping Unit |
| ASN | Advance Shipping Notice |
| PO | Purchase Order |
| BOL | Bill of Lading |
| RBAC | Role-Based Access Control |
| JWT | JSON Web Token |
| FEFO | First-Expired-First-Out |
| FIFO | First-In-First-Out |

---

## 2. User Characteristics

### 2.1 User Roles

| Role | Description | Count (Est.) |
|------|-------------|-------------|
| **Admin** | IT/DevOps — system configuration, user management | 2–3 |
| **Supervisor** | DC Team Lead — operational oversight, approvals | 4–6 |
| **Operator** | Warehouse Staff — receiving, picking, packing | 30–50 |
| **Viewer** | Store staff, external auditors | 10–20 |

### 2.2 User Skill Levels
- All users are familiar with warehouse operations processes
- Operators are comfortable using handheld scanners and mobile devices
- Administrators have basic IT literacy and system navigation skills

---

## 3. Functional Requirements

### 3.1 Authentication & Authorization

| ID | Requirement | Priority |
|----|-----------|----------|
| AUTH-001 | System shall authenticate users via username and password | **MUST** |
| AUTH-002 | System shall issue JWT tokens upon successful login | **MUST** |
| AUTH-003 | System shall support session expiry with configurable timeout | **MUST** |
| AUTH-004 | System shall enforce RBAC — each user has exactly one role | **MUST** |
| AUTH-005 | Unauthorized access attempts shall be logged in the audit trail | **MUST** |
| AUTH-006 | System shall support role-based navigation (menus hidden by permission) | **SHOULD** |

### 3.2 Master Data Management

| ID | Requirement | Priority |
|----|-----------|----------|
| MASTER-001 | System shall manage Product/SKU master data with unique SKU codes | **MUST** |
| MASTER-002 | System shall support multi-level product category hierarchy (parent-child) | **MUST** |
| MASTER-003 | System shall manage Warehouse master (multiple DC support) | **MUST** |
| MASTER-004 | System shall support Zone configuration per warehouse | **MUST** |
| MASTER-005 | System shall support Location/Bin management with full address structure (Zone→Aisle→Rack→Level→Bin) | **MUST** |
| MASTER-006 | System shall manage UOM (Unit of Measure) with conversion factors | **MUST** |
| MASTER-007 | System shall manage Supplier master data | **MUST** |
| MASTER-008 | System shall manage Carrier master data | **MUST** |

### 3.3 Inbound / Receiving

| ID | Requirement | Priority |
|----|-----------|----------|
| INBOUND-001 | System shall create inbound receipts linked to PO/ASN | **MUST** |
| INBOUND-002 | System shall allow receiving items with expected vs. received quantity comparison | **MUST** |
| INBOUND-003 | System shall capture lot number and expiry date during receiving | **MUST** |
| INBOUND-004 | System shall record damaged/shortage quantities separately | **MUST** |
| INBOUND-005 | System shall generate receipt numbers automatically (RCV-YYYYMMDD-NNN) | **MUST** |
| INBOUND-006 | System shall support dock door assignment | **SHOULD** |
| INBOUND-007 | System shall update inventory immediately upon receiving confirmation | **MUST** |

### 3.4 Inventory Management

| ID | Requirement | Priority |
|----|-----------|----------|
| INV-001 | System shall maintain real-time inventory by product + location | **MUST** |
| INV-002 | System shall support lot/batch number tracking | **MUST** |
| INV-003 | System shall support expiry date tracking per inventory lot | **MUST** |
| INV-004 | System shall support FEFO and FIFO picking strategies | **MUST** |
| INV-005 | System shall maintain complete stock movement audit trail | **MUST** |
| INV-006 | System shall support stock adjustments (positive/negative) with reason code | **MUST** |
| INV-007 | System shall support internal location transfers | **MUST** |
| INV-008 | System shall support reserved stock (allocated to outbound orders) | **MUST** |
| INV-009 | System shall trigger low-stock alerts when qty ≤ min_stock threshold | **SHOULD** |

### 3.5 Outbound / Fulfillment

| ID | Requirement | Priority |
|----|-----------|----------|
| OUT-001 | System shall create outbound orders (sales/transfer) | **MUST** |
| OUT-002 | System shall generate pick tasks automatically from order items | **MUST** |
| OUT-003 | System shall support batch/wave picking (group multiple orders) | **SHOULD** |
| OUT-004 | System shall assign pick tasks to operators | **MUST** |
| OUT-005 | System shall track picking status per task (assigned → in_progress → picked) | **MUST** |
| OUT-006 | System shall support cartonization / packing station workflow | **MUST** |
| OUT-007 | System shall generate BOL and shipment records | **MUST** |
| OUT-008 | System shall deduct inventory upon shipment confirmation | **MUST** |
| OUT-009 | System shall support priority levels (Normal / Urgent / Rush) | **MUST** |

### 3.6 Returns Management

| ID | Requirement | Priority |
|----|-----------|----------|
| RETURN-001 | System shall create return orders linked to original outbound order | **MUST** |
| RETURN-002 | System shall capture return reason and item condition | **MUST** |
| RETURN-003 | System shall support disposition options: Restock / Refurbish / Dispose / Write-off | **MUST** |
| RETURN-004 | System shall update inventory based on disposition result | **MUST** |

### 3.7 Reporting & Dashboard

| ID | Requirement | Priority |
|----|-----------|----------|
| REPORT-001 | Dashboard shall display real-time KPI summary (total SKUs, locations, inbound/outbound counts) | **MUST** |
| REPORT-002 | System shall calculate inventory accuracy % | **SHOULD** |
| REPORT-003 | System shall calculate receiving accuracy % | **SHOULD** |
| REPORT-004 | All reports shall be accessible by role (Admin/Supervisor only) | **MUST** |

### 3.8 Audit & Compliance

| ID | Requirement | Priority |
|----|-----------|----------|
| AUDIT-001 | System shall log all create/update/delete operations with user, timestamp, and old/new values | **MUST** |
| AUDIT-002 | Audit log entries shall be immutable (append-only) | **MUST** |
| AUDIT-003 | System shall support audit log search and filter | **SHOULD** |

---

## 4. Non-Functional Requirements

### 4.1 Performance
- API response time ≤ 500ms for standard CRUD operations
- Dashboard load time ≤ 3 seconds
- System shall support ≥ 50 concurrent users during peak hours
- Database shall handle ≥ 100,000 inventory transactions per day

### 4.2 Security
- All API endpoints shall require authentication (except login)
- JWT tokens shall expire within 8 hours
- Passwords shall be hashed using BCrypt (cost factor ≥ 10)
- All sensitive data in transit shall use HTTPS (TLS 1.2+)
- RBAC shall be enforced at both API and UI levels

### 4.3 Reliability
- System uptime target: 99.5% during business hours (08:00–22:00)
- Database shall be backed up daily
- Application shall handle connection failures gracefully with retry logic

### 4.4 Usability
- Web-based UI accessible via Chrome/Safari/Firefox (latest versions)
- UI optimized for 1920×1080 desktop resolution
- Mobile-responsive layout for tablet use in warehouse floor

### 4.5 Compatibility
- Backend: ASP.NET Core 9.0 / .NET 9
- Database: PostgreSQL 16
- Frontend: React 18 + Vite
- Authentication: JWT Bearer tokens

---

## 5. Data Handling

### 5.1 Data Ownership
- The operating company retains all data ownership
- Data is stored within Indonesia (on-premise or local cloud)

### 5.2 Data Retention
- Inventory transaction history: minimum 7 years
- Audit logs: minimum 7 years
- User session logs: minimum 2 years

### 5.3 Data Backup
- Daily automated full database backup
- Backup retention: 30 days
- Recovery Point Objective (RPO): ≤ 24 hours
- Recovery Time Objective (RTO): ≤ 4 hours

---

## 6. Constraints

- System must be compatible with Indonesian retail/DC operational workflows
- Integration with ERP (SAP) is planned for Phase 2 — not in scope for v1.0
- WMS runs as a web application — no mobile native app in v1.0
- Barcode scanning integration deferred to Phase 2

---

## 7. Acceptance Criteria

| ID | Criterion | Verification Method |
|----|-----------|-------------------|
| AC-001 | User can login with valid credentials and receive JWT | Login test |
| AC-002 | Admin can create/read/update/delete products | CRUD test |
| AC-003 | Inbound receipt can be created and inventory updated | End-to-end test |
| AC-004 | Outbound order generates pick tasks automatically | Integration test |
| AC-005 | All stock movements are recorded in inventory_transactions | Audit query |
| AC-006 | Unauthorized users cannot access protected endpoints | Security test |
| AC-007 | Dashboard displays KPI summary data | UI verification |
| AC-008 | All API endpoints documented in Swagger UI | Documentation review |

---

*Document approved by: _________________ Date: _____________*
