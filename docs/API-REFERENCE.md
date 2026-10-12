# API Reference
## WMS Distribution Center — Warehouse Management System
**Base URL:** `http://localhost:5243/api`

---

## Authentication

All endpoints except `/auth/login` and `/auth/register` require authentication.
Add the JWT token to every request header:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Endpoints Summary

### Authentication
| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| POST | `/auth/login` | User login | ❌ |
| POST | `/auth/register` | Register new user | ❌ |
| GET | `/auth/me` | Get current user profile | ✅ |

### Users
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/users` | List all users |
| GET | `/users/{id:guid}` | Get user by ID |
| POST | `/users` | Create new user |
| PUT | `/users/{id:guid}` | Update user |
| DELETE | `/users/{id:guid}` | Delete user |

### Roles
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/roles` | List all roles |
| GET | `/roles/{id:guid}` | Get role by ID |
| POST | `/roles` | Create new role |
| PUT | `/roles/{id:guid}` | Update role |
| DELETE | `/roles/{id:guid}` | Delete role |

### Products
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/products` | List products (with search, pagination) |
| GET | `/products/{id:guid}` | Get product by ID |
| GET | `/products/search?q=` | Search products |
| POST | `/products` | Create product |
| PUT | `/products/{id:guid}` | Update product |
| DELETE | `/products/{id:guid}` | Delete product |

### Categories
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/categories` | List all categories |
| GET | `/categories/tree` | Get category tree structure |
| GET | `/categories/{id:guid}` | Get category by ID |
| POST | `/categories` | Create category |
| PUT | `/categories/{id:guid}` | Update category |
| DELETE | `/categories/{id:guid}` | Delete category |

### Warehouses
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/warehouses` | List all warehouses |
| GET | `/warehouses/{id:guid}` | Get warehouse by ID |
| POST | `/warehouses` | Create warehouse |
| PUT | `/warehouses/{id:guid}` | Update warehouse |
| DELETE | `/warehouses/{id:guid}` | Delete warehouse |

### Zones
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/zones` | List zones |
| GET | `/zones/warehouse/{warehouseId:guid}` | Zones by warehouse |
| GET | `/zones/{id:guid}` | Get zone by ID |
| POST | `/zones` | Create zone |
| PUT | `/zones/{id:guid}` | Update zone |
| DELETE | `/zones/{id:guid}` | Delete zone |

### Locations
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/locations` | List locations |
| GET | `/locations/search/{code}` | Search by location code |
| GET | `/locations/{id:guid}` | Get location by ID |
| POST | `/locations` | Create location |
| PUT | `/locations/{id:guid}` | Update location |
| DELETE | `/locations/{id:guid}` | Delete location |

### Inventory
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/inventory/summary` | Get all inventory with filters |
| GET | `/inventory/product/{productId:guid}` | Inventory by product |
| GET | `/inventory/location/{locationId:guid}` | Inventory by location |
| POST | `/inventory/adjustments` | Create stock adjustment |
| GET | `/inventory/transactions` | Transaction history |

### Inbound
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/inbound` | List inbound receipts |
| GET | `/inbound/{id:guid}` | Get receipt by ID |
| GET | `/inbound/{id:guid}/items` | Get receipt line items |
| POST | `/inbound` | Create new receipt |
| POST | `/inbound/{id:guid}/receive` | Receive items into receipt |

### Outbound
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/outbound` | List outbound orders |
| GET | `/outbound/{id:guid}` | Get order by ID |
| GET | `/outbound/{id:guid}/items` | Get order line items |
| POST | `/outbound` | Create new order |
| POST | `/outbound/{id:guid}/start-picking` | Generate pick tasks |
| POST | `/outbound/{id:guid}/assign-picker` | Assign picker to order |
| POST | `/outbound/{id:guid}/pack` | Record packing |
| POST | `/outbound/{id:guid}/ship` | Confirm shipment |

### Reports
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/reports/dashboard` | Dashboard KPIs |
| GET | `/reports/inbound-summary` | Inbound summary stats |
| GET | `/reports/outbound-summary` | Outbound summary stats |
| GET | `/reports/kpis` | KPI metrics |

### Suppliers
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/suppliers` | List suppliers |
| GET | `/suppliers/{id:guid}` | Get supplier by ID |
| POST | `/suppliers` | Create supplier |
| PUT | `/suppliers/{id:guid}` | Update supplier |
| DELETE | `/suppliers/{id:guid}` | Delete supplier |

### Carriers
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/carriers` | List carriers |
| GET | `/carriers/{id:guid}` | Get carrier by ID |
| POST | `/carriers` | Create carrier |
| PUT | `/carriers/{id:guid}` | Update carrier |
| DELETE | `/carriers/{id:guid}` | Delete carrier |

---

## Common Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `page` | int | Page number (default: 1) |
| `pageSize` | int | Items per page (default: 20, max: 100) |
| `search` | string | Text search across name/code fields |
| `sortBy` | string | Field name to sort by |
| `sortDir` | string | `asc` or `desc` |

---

## HTTP Status Codes

| Code | Meaning |
|------|--------|
| `200 OK` | Request succeeded |
| `201 Created` | Resource created successfully |
| `204 No Content` | Resource deleted successfully |
| `400 Bad Request` | Invalid request body or parameters |
| `401 Unauthorized` | Missing or invalid JWT token |
| `403 Forbidden` | Valid token but insufficient permissions |
| `404 Not Found` | Resource not found |
| `409 Conflict` | Duplicate resource (e.g., duplicate SKU) |
| `500 Internal Server Error` | Server error |

---

## Error Response Format

All errors follow this format:

```json
{
  "message": "Human-readable error message",
  "errors": {
    "fieldName": ["Validation error message"]
  }
}
```

---

## Pagination Response Format

All list endpoints return paginated results:

```json
{
  "data": [...],
  "totalCount": 1247,
  "page": 1,
  "pageSize": 20,
  "totalPages": 63
}
```

---

## Postman Collection

A Postman collection with all endpoints is available at:
`docs/wms-distribution-center.postman_collection.json`

### Postman Setup

1. Create a new **Environment** in Postman:
   - Variable: `baseUrl` = `http://localhost:5243/api`
   - Variable: `token` (leave empty)

2. **Login** request → save token to environment variable:
   - In Tests tab:
   ```javascript
   var jsonData = pm.response.json();
   pm.collectionVariables.set("token", jsonData.token);
   ```

3. Add **Authorization** header to the collection:
   - Type: Bearer Token
   - Token: `{{token}}`

4. All subsequent requests will automatically include the JWT token.
