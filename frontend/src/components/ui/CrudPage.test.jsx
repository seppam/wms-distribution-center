import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { fail, ok, renderPage } from '../../test/utils';
import CrudPage from './CrudPage';

const columns = [{ key: 'name', label: 'Name' }, { key: 'code', label: 'Code' }];
const formFields = [
  { name: 'code', label: 'Code *', required: true, createOnly: true },
  { name: 'name', label: 'Name *', required: true },
  { name: 'capacity', label: 'Capacity', type: 'number' },
];

const makeApi = (rows = []) => ({
  getAll: vi.fn(() => ok(rows)),
  create: vi.fn(() => ok({})),
  update: vi.fn(() => ok({})),
  delete: vi.fn(() => ok({})),
});

const page = (api, role) =>
  renderPage(<CrudPage title="Warehouses" apiService={api} columns={columns} formFields={formFields} />, { role });

describe('CrudPage', () => {
  it('shows a loading state, then the rows', async () => {
    page(makeApi([{ id: '1', name: 'Main DC', code: 'DC1' }]));
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
    expect(await screen.findByText('Main DC')).toBeInTheDocument();
  });

  it('shows an empty state with a hint for managers', async () => {
    page(makeApi([]));
    expect(await screen.findByText('No warehouses yet')).toBeInTheDocument();
    expect(screen.getByText(/Use "Add Warehouse"/)).toBeInTheDocument();
  });

  it('shows the error and retries', async () => {
    const api = makeApi();
    api.getAll.mockImplementationOnce(() => fail(500, 'Database is down'));
    page(api);
    expect(await screen.findByRole('alert')).toHaveTextContent('Database is down');
    api.getAll.mockImplementationOnce(() => ok([{ id: '1', name: 'Recovered', code: 'R1' }]));
    await userEvent.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await screen.findByText('Recovered')).toBeInTheDocument();
  });

  it('hides create, edit and delete from viewers', async () => {
    page(makeApi([{ id: '1', name: 'Main DC', code: 'DC1' }]), 'Viewer');
    await screen.findByText('Main DC');
    expect(screen.queryByRole('button', { name: /Add Warehouse/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Edit/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Delete/ })).not.toBeInTheDocument();
  });

  it('lets supervisors edit but not delete', async () => {
    page(makeApi([{ id: '1', name: 'Main DC', code: 'DC1' }]), 'Supervisor');
    await screen.findByText('Main DC');
    expect(screen.getByRole('button', { name: /Edit/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Delete/ })).not.toBeInTheDocument();
  });

  it('creates a record with a typed payload and reloads', async () => {
    const api = makeApi([]);
    page(api);
    await userEvent.click(await screen.findByRole('button', { name: /Add Warehouse/ }));
    const dialog = screen.getByRole('dialog', { name: 'New Warehouse' });
    await userEvent.type(within(dialog).getByLabelText('Code *'), 'DC9');
    await userEvent.type(within(dialog).getByLabelText('Name *'), 'North DC');
    await userEvent.type(within(dialog).getByLabelText('Capacity'), '500');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }));
    await waitFor(() => expect(api.create).toHaveBeenCalledWith({ code: 'DC9', name: 'North DC', capacity: 500 }));
    expect(await screen.findByText('Warehouse created')).toBeInTheDocument();
    expect(api.getAll).toHaveBeenCalledTimes(2);
  });

  it('shows the server error inside the form and keeps it open', async () => {
    const api = makeApi([]);
    api.create.mockImplementationOnce(() => fail(409, 'A warehouse with this code already exists'));
    page(api);
    await userEvent.click(await screen.findByRole('button', { name: /Add Warehouse/ }));
    const dialog = screen.getByRole('dialog');
    await userEvent.type(within(dialog).getByLabelText('Code *'), 'DC1');
    await userEvent.type(within(dialog).getByLabelText('Name *'), 'Dup');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('already exists');
  });

  it('does not send create-only fields when editing', async () => {
    const api = makeApi([{ id: '1', name: 'Main DC', code: 'DC1' }]);
    page(api);
    await userEvent.click(await screen.findByRole('button', { name: /Edit/ }));
    const dialog = screen.getByRole('dialog', { name: 'Edit Warehouse' });
    expect(within(dialog).queryByLabelText('Code *')).not.toBeInTheDocument();
    await userEvent.clear(within(dialog).getByLabelText('Name *'));
    await userEvent.type(within(dialog).getByLabelText('Name *'), 'Renamed');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Update' }));
    await waitFor(() => expect(api.update).toHaveBeenCalledWith('1', { name: 'Renamed', capacity: null }));
  });

  it('asks for confirmation before deleting', async () => {
    const api = makeApi([{ id: '1', name: 'Main DC', code: 'DC1' }]);
    page(api);
    await userEvent.click(await screen.findByRole('button', { name: /Delete Main DC/ }));
    expect(api.delete).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(api.delete).toHaveBeenCalledWith('1'));
  });

  it('reports delete failures with the API message', async () => {
    const api = makeApi([{ id: '1', name: 'Main DC', code: 'DC1' }]);
    api.delete.mockImplementationOnce(() => fail(409, 'The change conflicts with existing data'));
    page(api);
    await userEvent.click(await screen.findByRole('button', { name: /Delete Main DC/ }));
    await userEvent.click(screen.getByRole('button', { name: 'Delete' }));
    expect(await screen.findByText(/conflicts with existing data/)).toBeInTheDocument();
  });

  it('filters rows with the search box', async () => {
    page(makeApi([{ id: '1', name: 'Alpha', code: 'A' }, { id: '2', name: 'Beta', code: 'B' }]));
    await screen.findByText('Alpha');
    await userEvent.type(screen.getByLabelText('Search Warehouses'), 'bet');
    expect(screen.queryByText('Alpha')).not.toBeInTheDocument();
    expect(screen.getByText('Beta')).toBeInTheDocument();
  });
});
