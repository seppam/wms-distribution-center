import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import AuthContext from '../contexts/AuthContext';
import { ToastProvider } from '../components/ui';
import { roleCan } from '../utils/permissions';

/** Axios-style response */
export const ok = (data) => Promise.resolve({ data });

/** Axios-style failure with an API error body */
export const fail = (status, message) => Promise.reject({ response: { status, data: { message } } });

export function renderPage(ui, { role = 'Admin', route = '/' } = {}) {
  const value = {
    user: { id: 'u1', username: role.toLowerCase(), fullName: `${role} User`, role },
    loading: false,
    role,
    can: (policy) => roleCan(role, policy),
    isAdmin: role === 'Admin',
    login: async () => {},
    logout: () => {},
  };
  return render(
    <MemoryRouter initialEntries={[route]}>
      <AuthContext.Provider value={value}>
        <ToastProvider>{ui}</ToastProvider>
      </AuthContext.Provider>
    </MemoryRouter>
  );
}
