import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fail, ok, renderPage } from '../test/utils';
import Returns from './Returns';

vi.mock('../services', () => ({
  returnsApi: { getAll: vi.fn(), create: vi.fn(), process: vi.fn() },
  warehousesApi: { getAll: vi.fn() },
  productsApi: { getAll: vi.fn() },
  outboundApi: { getAll: vi.fn() },
  locationsApi: { getAll: vi.fn() },
}));
import { locationsApi, outboundApi, productsApi, returnsApi, warehousesApi } from '../services';

const ret = (over = {}) => ({
  id: 'r1', returnNo: 'RET-1', productName: 'Rice', productSku: 'SKU-1', qty: 3, orderNumber: 'ORD-1', condition: 'Good',
  status: 'Received', warehouseName: 'Main DC', receiverName: 'Op', ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  returnsApi.getAll.mockImplementation(() => ok([ret()]));
  warehousesApi.getAll.mockImplementation(() => ok([{ id: 'w1', name: 'Main DC' }]));
  productsApi.getAll.mockImplementation(() => ok([{ id: 'p1', sku: 'SKU-1', name: 'Rice' }]));
  outboundApi.getAll.mockImplementation(() => ok([
    { id: 'o1', orderNumber: 'ORD-1', status: 'Shipped', destination: 'Store' },
    { id: 'o2', orderNumber: 'ORD-2', status: 'Draft', destination: 'Nope' },
  ]));
  locationsApi.getAll.mockImplementation(() => ok([
    { id: 'l1', locationCode: 'A-01', zoneName: 'Ambient', warehouseName: 'Main DC' },
    { id: 'l9', locationCode: 'Z-01', zoneName: 'Far', warehouseName: 'Other DC' },
  ]));
});

describe('Returns page', () => {
  it('lists returns', async () => {
    renderPage(<Returns />);
    expect(await screen.findByText('RET-1')).toBeInTheDocument();
  });

  it('receives a return against a shipped order only', async () => {
    returnsApi.create.mockImplementation(() => ok({}));
    renderPage(<Returns />, { role: 'Operator' });
    await userEvent.click(await screen.findByRole('button', { name: /Receive return/ }));
    const dialog = screen.getByRole('dialog', { name: 'Receive a return' });
    await waitFor(() => expect(within(dialog).getByLabelText('Original order (optional)')).toHaveTextContent('ORD-1'));
    expect(within(dialog).getByLabelText('Original order (optional)')).not.toHaveTextContent('ORD-2');
    await userEvent.selectOptions(within(dialog).getByLabelText('Warehouse'), 'w1');
    await userEvent.selectOptions(within(dialog).getByLabelText('Original order (optional)'), 'o1');
    await userEvent.selectOptions(within(dialog).getByLabelText('Product'), 'p1');
    await userEvent.type(within(dialog).getByLabelText('Quantity'), '2');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Receive return' }));
    await waitFor(() => expect(returnsApi.create).toHaveBeenCalledWith(expect.objectContaining({
      warehouseId: 'w1', productId: 'p1', qty: 2, condition: 'Good', orderId: 'o1',
    })));
  });

  it('lets supervisors restock into a location of the return warehouse only', async () => {
    returnsApi.process.mockImplementation(() => ok({}));
    renderPage(<Returns />);
    await userEvent.click(await screen.findByRole('button', { name: 'Process' }));
    const dialog = screen.getByRole('dialog', { name: 'Process RET-1' });
    const location = within(dialog).getByLabelText('Restock location');
    await waitFor(() => expect(location).toHaveTextContent('A-01'));
    expect(location).not.toHaveTextContent('Z-01');
    await userEvent.selectOptions(location, 'l1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Process return' }));
    await waitFor(() => expect(returnsApi.process).toHaveBeenCalledWith('r1', { disposition: 'Restock', restockLocationId: 'l1', notes: null }));
  });

  it('defaults damaged returns to disposal and needs no location', async () => {
    returnsApi.getAll.mockImplementation(() => ok([ret({ condition: 'Damaged' })]));
    returnsApi.process.mockImplementation(() => ok({}));
    renderPage(<Returns />);
    await userEvent.click(await screen.findByRole('button', { name: 'Process' }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).queryByLabelText('Restock location')).not.toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Process return' }));
    await waitFor(() => expect(returnsApi.process).toHaveBeenCalledWith('r1', { disposition: 'Dispose', restockLocationId: null, notes: null }));
  });

  it('hides processing from operators and reports API errors', async () => {
    renderPage(<Returns />, { role: 'Operator' });
    await screen.findByText('RET-1');
    expect(screen.queryByRole('button', { name: 'Process' })).not.toBeInTheDocument();
  });

  it('shows the load error', async () => {
    returnsApi.getAll.mockImplementationOnce(() => fail(500, 'Cannot load returns'));
    renderPage(<Returns />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Cannot load returns');
  });
});
