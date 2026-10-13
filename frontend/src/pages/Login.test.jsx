import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AuthContext from '../contexts/AuthContext';
import Login from './Login';

const login = vi.fn();
const renderLogin = () => render(
  <MemoryRouter><AuthContext.Provider value={{ login }}><Login /></AuthContext.Provider></MemoryRouter>
);

beforeEach(() => { login.mockReset(); });

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
});
