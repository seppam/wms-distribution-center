import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
  headers: { 'Content-Type': 'application/json' },
});

export const TOKEN_KEY = 'wms_token';

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (res) => res,
  (err) => {
    // An expired/revoked session sends the user to the login page; a failed login attempt must not.
    const isLogin = err.config?.url?.includes('/auth/login');
    if (err.response?.status === 401 && !isLogin) {
      localStorage.removeItem(TOKEN_KEY);
      if (window.location.pathname !== '/login') window.location.assign('/login');
    }
    return Promise.reject(err);
  }
);

/** Human readable message for any failed request. */
export function errorMessage(err, fallback = 'Something went wrong') {
  const data = err?.response?.data;
  if (data?.message) return data.message;
  if (data?.errors && typeof data.errors === 'object') {
    const first = Object.values(data.errors).flat()[0];
    if (first) return String(first);
  }
  if (data?.detail || data?.title) return data.detail || data.title;
  if (err?.response?.status === 403) return 'You do not have permission to do that.';
  if (err?.response?.status === 429) return 'Too many attempts. Try again in a minute.';
  if (err?.code === 'ERR_NETWORK') return 'Cannot reach the server. Check that the API is running.';
  return fallback;
}

export default api;
