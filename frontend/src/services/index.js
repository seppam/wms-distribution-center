import api from './api';

// Auth
export const authApi = {
  login: (data) => api.post('/auth/login', data),
  register: (data) => api.post('/auth/register', data),
  me: () => api.get('/auth/me'),
};

// Users
export const usersApi = {
  getAll: (params) => api.get('/users', { params }),
  getById: (id) => api.get(`/users/${id}`),
  create: (data) => api.post('/users', data),
  update: (id, data) => api.put(`/users/${id}`, data),
  delete: (id) => api.delete(`/users/${id}`),
};

// Roles
export const rolesApi = {
  getAll: () => api.get('/roles'),
  getById: (id) => api.get(`/roles/${id}`),
  create: (data) => api.post('/roles', data),
  update: (id, data) => api.put(`/roles/${id}`, data),
  delete: (id) => api.delete(`/roles/${id}`),
};

// Products
export const productsApi = {
  getAll: (params) => api.get('/products', { params }),
  getById: (id) => api.get(`/products/${id}`),
  create: (data) => api.post('/products', data),
  update: (id, data) => api.put(`/products/${id}`, data),
  delete: (id) => api.delete(`/products/${id}`),
};

// Categories
export const categoriesApi = {
  getAll: () => api.get('/categories'),
  getTree: () => api.get('/categories/tree'),
  create: (data) => api.post('/categories', data),
  update: (id, data) => api.put(`/categories/${id}`, data),
  delete: (id) => api.delete(`/categories/${id}`),
};

// Warehouses
export const warehousesApi = {
  getAll: () => api.get('/warehouses'),
  getById: (id) => api.get(`/warehouses/${id}`),
  create: (data) => api.post('/warehouses', data),
  update: (id, data) => api.put(`/warehouses/${id}`, data),
  delete: (id) => api.delete(`/warehouses/${id}`),
};

// Zones
export const zonesApi = {
  getAll: (params) => api.get('/zones', { params }),
  getByWarehouse: (warehouseId) => api.get(`/zones/warehouse/${warehouseId}`),
  create: (data) => api.post('/zones', data),
  update: (id, data) => api.put(`/zones/${id}`, data),
  delete: (id) => api.delete(`/zones/${id}`),
};

// Locations
export const locationsApi = {
  getAll: (params) => api.get('/locations', { params }),
  getById: (id) => api.get(`/locations/${id}`),
  search: (code) => api.get(`/locations/search/${code}`),
  create: (data) => api.post('/locations', data),
  update: (id, data) => api.put(`/locations/${id}`, data),
  delete: (id) => api.delete(`/locations/${id}`),
};

// Inventory
export const inventoryApi = {
  getSummary: (params) => api.get('/inventory', { params }),
  getByProduct: (productId) => api.get(`/inventory/product/${productId}`),
  getByLocation: (locationId) => api.get(`/inventory/location/${locationId}`),
  adjust: (data) => api.post('/inventory/adjust', data),
  getTransactions: (params) => api.get('/inventory/transactions', { params }),
};

// Inbound
export const inboundApi = {
  getAll: (params) => api.get('/inboundreceipts', { params }),
  getById: (id) => api.get(`/inboundreceipts/${id}`),
  create: (data) => api.post('/inboundreceipts', data),
  receive: (id, data) => api.post(`/inboundreceipts/${id}/receive`, data),
  putaway: (id, data) => api.post(`/inboundreceipts/${id}/putaway`, data),
};

// Outbound
export const outboundApi = {
  getAll: (params) => api.get('/outboundorders', { params }),
  getById: (id) => api.get(`/outboundorders/${id}`),
  create: (data) => api.post('/outboundorders', data),
  pick: (orderId, data) => api.post(`/outboundorders/${orderId}/pick`, data),
  pack: (orderId, data) => api.post(`/outboundorders/${orderId}/pack`, data),
  ship: (orderId, data) => api.post(`/outboundorders/${orderId}/ship`, data),
};

// Suppliers
export const suppliersApi = {
  getAll: () => api.get('/suppliers'),
  create: (data) => api.post('/suppliers', data),
  update: (id, data) => api.put(`/suppliers/${id}`, data),
  delete: (id) => api.delete(`/suppliers/${id}`),
};

// Carriers
export const carriersApi = {
  getAll: () => api.get('/carriers'),
  create: (data) => api.post('/carriers', data),
  update: (id, data) => api.put(`/carriers/${id}`, data),
  delete: (id) => api.delete(`/carriers/${id}`),
};

// Reports / Dashboard
export const reportsApi = {
  getDashboard: () => api.get('/reports/dashboard'),
  getKpis: (params) => api.get('/reports/kpis', { params }),
};
