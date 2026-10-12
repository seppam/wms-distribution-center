import { createContext, useContext, useState, useEffect } from 'react';
import api from '../services/api';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(localStorage.getItem('wms_token'));
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (token) {
      fetchUser();
    } else {
      setLoading(false);
    }
  }, [token]);

  async function fetchUser() {
    try {
      const res = await api.get('/auth/me');
      setUser(res.data);
    } catch {
      logout();
    } finally {
      setLoading(false);
    }
  }

  async function login(username, password) {
    const res = await api.post('/auth/login', { username, password });
    const { token: newToken, ...userData } = res.data;
    localStorage.setItem('wms_token', newToken);
    setToken(newToken);
    setUser(userData);
    return userData;
  }

  function logout() {
    localStorage.removeItem('wms_token');
    setToken(null);
    setUser(null);
  }

  function hasPermission(module, action) {
    if (!user?.role?.permissions) return false;
    const perms = user.role.permissions;
    if (perms === '*') return true;
    return perms[module]?.includes(action) || perms[module] === '*';
  }

  function hasAnyRole(...roles) {
    return roles.includes(user?.role?.name);
  }

  const value = {
    user,
    token,
    loading,
    login,
    logout,
    hasPermission,
    hasAnyRole,
    isAdmin: user?.role?.name === 'Admin',
    isSupervisor: user?.role?.name === 'Supervisor',
    isOperator: user?.role?.name === 'Operator',
    isViewer: user?.role?.name === 'Viewer',
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}

export default AuthContext;
