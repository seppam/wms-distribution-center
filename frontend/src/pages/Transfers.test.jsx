import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fail, ok, renderPage } from '../test/utils';
import Transfers from './Transfers';

vi.mock('../services', () => ({
  transfersApi: { getAll: vi.fn(), create: vi.fn(), complete: vi.fn(), cancel: vi.fn() },
  inventoryApi: { query: vi.fn() },
  locationsApi: { getAll: vi.fn() },
}));
import { inventoryApi, locationsApi, transfersApi } from '../services';

const transfer = (over = {}) => ({ id: 'tr1', transferNo: 'TRF-1', productName: 'Rice', productSku: 'SKU-1', fromLocationCode: 'A-01', toLocationCode: 'B-01', qty: 5, status: 'Draft', requesterName: 'Op', ...over });

beforeEach(() => {
  vi.clearAllMocks();
  transfersApi.getAll.mockImplementation(() => ok([transfer()]));
  inventoryApi.query.mockImplementation(() => ok([
    { id: 's1', productId: 'p1', productSku: 'SKU-1', productName: 'Rice', locationId: 'l1', locationCode: 'A-01', qtyOnHand: 50, qtyReserved: 45 },
    { id: 's2', productId: 'p2', productSku: 'SKU-2', productName: 'Oil', locationId: 'l2', locationCode: 'A-02', qtyOnHand: 10, qtyReserved: 10 },
  ]));
  locationsApi.getAll.mockImplementation(() => ok([
    { id: 'l1', locationCode: 'A-01', zoneName: 'Ambient' }, { id: 'l3', locationCode: 'B-01', zoneName: 'Ambient' },
  ]));
});

describe('Transfers page', () => {
  it('lists transfers and completes a draft', async () => {
    transfersApi.complete.mockImplementation(() => ok({}));
    renderPage(<Transfers />, { role: 'Operator' });
    expect(await screen.findByText('TRF-1')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Complete' }));
    await waitFor(() => expect(transfersApi.complete).toHaveBeenCalledWith('tr1'));
    expect(await screen.findByText('Transfer completed: stock moved')).toBeInTheDocument();
  });

  it('cancels a draft', async () => {
    transfersApi.cancel.mockImplementation(() => ok({}));
    renderPage(<Transfers />);
    await userEvent.click(await screen.findByRole('button', { name: 'Cancel' }));
    await waitFor(() => expect(transfersApi.cancel).toHaveBeenCalledWith('tr1'));
  });

  it('offers only unreserved stock and creates the transfer from the chosen stock row', async () => {
    transfersApi.create.mockImplementation(() => ok({}));
    renderPage(<Transfers />);
    await userEvent.click(await screen.findByRole('button', { name: /New transfer/ }));
    const dialog = screen.getByRole('dialog', { name: 'New stock transfer' });
    const stock = within(dialog).getByLabelText('Stock to move');
    await waitFor(() => expect(stock).toHaveTextContent('SKU-1'));
    expect(stock).not.toHaveTextContent('SKU-2'); // fully reserved
    expect(stock).toHaveTextContent('available 5');
    await userEvent.selectOptions(stock, 's1');
    await waitFor(() => expect(within(dialog).getByLabelText('Destination location')).toHaveTextContent('B-01'));
    expect(within(dialog).getByLabelText('Destination location')).not.toHaveTextContent('A-01'); // not the source
    await userEvent.selectOptions(within(dialog).getByLabelText('Destination location'), 'l3');
    await userEvent.type(within(dialog).getByLabelText('Quantity'), '4');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create transfer' }));
    await waitFor(() => expect(transfersApi.create).toHaveBeenCalledWith({
      productId: 'p1', fromLocationId: 'l1', toLocationId: 'l3', qty: 4, reason: null,
    }));
  });

  it('shows API validation errors in the dialog', async () => {
    transfersApi.create.mockImplementation(() => fail(400, 'Only 5 available at the source location (reserved stock cannot be moved).'));
    renderPage(<Transfers />);
    await userEvent.click(await screen.findByRole('button', { name: /New transfer/ }));
    const dialog = screen.getByRole('dialog');
    await waitFor(() => expect(within(dialog).getByLabelText('Stock to move')).toHaveTextContent('SKU-1'));
    await userEvent.selectOptions(within(dialog).getByLabelText('Stock to move'), 's1');
    await userEvent.selectOptions(within(dialog).getByLabelText('Destination location'), 'l3');
    await userEvent.type(within(dialog).getByLabelText('Quantity'), '4');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create transfer' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('reserved stock cannot be moved');
  });

  it('has no actions for viewers and a friendly empty state', async () => {
    transfersApi.getAll.mockImplementation(() => ok([]));
    renderPage(<Transfers />, { role: 'Viewer' });
    expect(await screen.findByText('No transfers yet')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /New transfer/ })).not.toBeInTheDocument();
  });
});
