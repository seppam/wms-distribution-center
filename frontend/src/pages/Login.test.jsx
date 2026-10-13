import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AuthContext from '../contexts/AuthContext';
import Login from './Login';

vi.mock('../services/demo', () => ({ loadDemoInfo: vi.fn(), demoPassword: vi.fn() }));
import { demoPassword, loadDemoInfo } from '../services/demo';

const login = vi.fn();
const renderLogin = () => render(
  <MemoryRouter><AuthContext.Provider value={{ login }}><Login /></AuthContext.Provider></MemoryRouter>
);

const accounts = ['Admin', 'Supervisor', 'Operator', 'Viewer'].map((role) => ({ role, username: role.toLowerCase() }));
beforeEach(() => {
  login.mockReset();
  loadDemoInfo.mockResolvedValue({ enabled: false });
  demoPassword.mockReturnValue('');
});

describe('Login page', () => {
  it('submits the credentials', async () => {
    login.mockResolvedValue({});
    renderLogin();
    await userEvent.type(screen.getByLabelText('Username'), 'operator');
    await userEvent.type(screen.getByLabelText('Password'), 'secret-value');
    await userEvent.click(screen.getByRole('button', { name: 'Sign In' }));
    await waitFor(() => expect(login).toHaveBeenCalledWith('operator', 'secret-value'));
  });

  it('shows a clear message for wrong credentials', async () => {
    login.mockImplementation(() => Promise.reject({ response: { status: 401, data: { message: 'x' } } }));
    renderLogin();
    await userEvent.type(screen.getByLabelText('Username'), 'operator');
    await userEvent.type(screen.getByLabelText('Password'), 'wrong');
    await userEvent.click(screen.getByRole('button', { name: 'Sign In' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid username or password');
  });

  it('tells the user when sign-in is rate limited', async () => {
    login.mockImplementation(() => Promise.reject({ response: { status: 429, data: {} } }));
    renderLogin();
    await userEvent.type(screen.getByLabelText('Username'), 'a');
    await userEvent.type(screen.getByLabelText('Password'), 'b');
    await userEvent.click(screen.getByRole('button', { name: 'Sign In' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/Too many attempts/);
  });

  describe('demo mode panel', () => {
    it('is hidden when the API is not in demo mode', async () => {
      renderLogin();
      await waitFor(() => expect(loadDemoInfo).toHaveBeenCalled());
      expect(screen.queryByText('Demo mode')).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Sign in as/ })).not.toBeInTheDocument();
    });

    it('offers one-click sign-in per role when the API reports demo mode', async () => {
      loadDemoInfo.mockResolvedValue({ enabled: true, accounts });
      demoPassword.mockReturnValue('shared-demo-pass');
      login.mockResolvedValue({});
      renderLogin();
      expect(await screen.findByRole('region', { name: 'Demo mode' })).toBeInTheDocument();
      for (const role of ['Admin', 'Supervisor', 'Operator', 'Viewer'])
        expect(screen.getByRole('button', { name: `Sign in as ${role}` })).toBeInTheDocument();
      await userEvent.click(screen.getByRole('button', { name: 'Sign in as Operator' }));
      await waitFor(() => expect(login).toHaveBeenCalledWith('operator', 'shared-demo-pass'));
    });

    it('without a configured demo password it lists user names but no buttons', async () => {
      loadDemoInfo.mockResolvedValue({ enabled: true, accounts });
      renderLogin();
      expect(await screen.findByText(/User names: admin, supervisor, operator, viewer/)).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Sign in as/ })).not.toBeInTheDocument();
    });
  });
});
