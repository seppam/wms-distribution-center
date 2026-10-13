import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderPage } from '../../test/utils';
import Sidebar from './Sidebar';

describe('Sidebar', () => {
  it('shows the workflow links to everyone', () => {
    renderPage(<Sidebar />, { role: 'Viewer' });
    expect(screen.getByRole('link', { name: 'Orders' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Pick Tasks' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Transfers' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Returns' })).toBeInTheDocument();
  });

  it('hides user and role administration from non-admins', () => {
    renderPage(<Sidebar />, { role: 'Operator' });
    expect(screen.queryByRole('link', { name: 'Users' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Roles' })).not.toBeInTheDocument();
  });

  it('shows users to supervisors and roles to admins only', () => {
    const { unmount } = renderPage(<Sidebar />, { role: 'Supervisor' });
    expect(screen.getByRole('link', { name: 'Users' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Roles' })).not.toBeInTheDocument();
    unmount();
    renderPage(<Sidebar />, { role: 'Admin' });
    expect(screen.getByRole('link', { name: 'Roles' })).toBeInTheDocument();
  });

  it('displays the signed-in role', () => {
    renderPage(<Sidebar />, { role: 'Supervisor' });
    expect(screen.getByText('Supervisor', { selector: '.user-role' })).toBeInTheDocument();
  });
});
