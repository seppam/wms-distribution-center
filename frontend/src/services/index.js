import api from './api';

// Auth
export const authApi = {
  login: (data) => api.post('/auth/login', data),
  me: () => api.get('/auth/me'),
};

const crud = (path) => ({
  getAll: (params) => api.get(path, { params }),
  getById: (id) => api.get(`${path}/${id}`),
  create: (data) => api.post(path, data),
  update: (id, data) => api.put(`${path}/${id}`, data),
  delete: (id) => api.delete(`${path}/${id}`),
});

export const usersApi = crud('/users');
export const rolesApi = crud('/roles');
export const productsApi = crud('/products');
export const categoriesApi = crud('/categories');
export const warehousesApi = crud('/warehouses');
export const zonesApi = crud('/zones');
export const locationsApi = crud('/locations');
export const uomsApi = crud('/uoms');
export const suppliersApi = crud('/suppliers');
export const carriersApi = crud('/carriers');

export const inventoryApi = {
  query: (params) => api.get('/inventory', { params: { pageSize: 500, ...params } }),
  getTransactions: (params) => api.get('/inventory/transactions', { params }),
  getAdjustments: () => api.get('/inventory/adjustments'),
  createAdjustment: (data) => api.post('/inventory/adjustments', data),
  approveAdjustment: (id) => api.post(`/inventory/adjustments/${id}/approve`),
  rejectAdjustment: (id) => api.post(`/inventory/adjustments/${id}/reject`),
};

export const inboundApi = {
  getAll: () => api.get('/inboundreceipts'),
  getById: (id) => api.get(`/inboundreceipts/${id}`),
  create: (data) => api.post('/inboundreceipts', data),
  delete: (id) => api.delete(`/inboundreceipts/${id}`),
  receive: (id, data) => api.post(`/inboundreceipts/${id}/receive`, data),
  complete: (id) => api.post(`/inboundreceipts/${id}/complete`),
};

export const outboundApi = {
  getAll: () => api.get('/outboundorders'),
  getById: (id) => api.get(`/outboundorders/${id}`),
  create: (data) => api.post('/outboundorders', data),
  delete: (id) => api.delete(`/outboundorders/${id}`),
  startPicking: (id) => api.post(`/outboundorders/${id}/start-picking`),
  assignPicker: (id, pickerId) => api.post(`/outboundorders/${id}/assign-picker`, { pickerId }),
  pack: (id, data) => api.post(`/outboundorders/${id}/pack`, data),
  ship: (id, data) => api.post(`/outboundorders/${id}/ship`, data),
  cancel: (id) => api.post(`/outboundorders/${id}/cancel`),
};

export const picksApi = {
  getAll: (params) => api.get('/pick-tasks', { params }),
  confirm: (id, qtyPicked) => api.post(`/pick-tasks/${id}/confirm`, { qtyPicked }),
};

export const shipmentsApi = {
  getAll: () => api.get('/shipments'),
  deliver: (id) => api.post(`/shipments/${id}/deliver`),
};

export const transfersApi = {
  getAll: () => api.get('/transfers'),
  create: (data) => api.post('/transfers', data),
  complete: (id) => api.post(`/transfers/${id}/complete`),
  cancel: (id) => api.post(`/transfers/${id}/cancel`),
};

export const returnsApi = {
  getAll: () => api.get('/returns'),
  create: (data) => api.post('/returns', data),
  process: (id, data) => api.post(`/returns/${id}/process`, data),
};

export const reportsApi = {
  getDashboard: () => api.get('/reports/dashboard'),
  getOutboundSummary: () => api.get('/reports/outbound-summary'),
};
