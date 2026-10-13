import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fail, ok, renderPage } from '../test/utils';
import Inbound from './Inbound';

vi.mock('../services', () => ({
  inboundApi: { getAll: vi.fn(), getById: vi.fn(), create: vi.fn(), delete: vi.fn(), receive: vi.fn(), complete: vi.fn() },
  suppliersApi: { getAll: vi.fn() },
  warehousesApi: { getAll: vi.fn() },
  productsApi: { getAll: vi.fn() },
  locationsApi: { getAll: vi.fn() },
}));
import { inboundApi, locationsApi, productsApi, suppliersApi, warehousesApi } from '../services';

const receipt = { id: 'r1', receiptNo: 'RCV-20260101-0001', supplierName: 'Demo Foods Ltd', poNumber: 'PO-77', warehouseName: 'Main DC', status: 'Draft', expectedDate: '2026-02-01' };
const detail = (over = {}) => ({
  ...receipt, status: 'InProgress',
  items: [{ id: 'l1', productName: 'Rice 5kg', productSku: 'SKU-1', expectedQty: 40, receivedQty: 0, damagedQty: 0, status: 'Pending', putawayLocationCode: null }],
  ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  inboundApi.getAll.mockImplementation(() => ok([receipt]));
  suppliersApi.getAll.mockImplementation(() => ok([{ id: 's1', name: 'Demo Foods Ltd' }]));
  warehousesApi.getAll.mockImplementation(() => ok([{ id: 'w1', name: 'Main DC' }]));
  productsApi.getAll.mockImplementation(() => ok([{ id: 'p1', sku: 'SKU-1', name: 'Rice 5kg' }]));
  locationsApi.getAll.mockImplementation(() => ok([
    { id: 'loc1', locationCode: 'A-01-01', zoneName: 'Ambient', warehouseName: 'Main DC', status: 'Empty' },
    { id: 'loc2', locationCode: 'Z-99-99', zoneName: 'Far', warehouseName: 'Other DC', status: 'Empty' },
  ]));
});

describe('Inbound page', () => {
  it('lists receipts', async () => {
    renderPage(<Inbound />);
    expect(await screen.findByText('RCV-20260101-0001')).toBeInTheDocument();
    expect(screen.getByText('PO-77')).toBeInTheDocument();
  });

  it('shows an empty state and an error state', async () => {
    inboundApi.getAll.mockImplementationOnce(() => ok([]));
    const { unmount } = renderPage(<Inbound />);
    expect(await screen.findByText('No receipts yet')).toBeInTheDocument();
    unmount();
    inboundApi.getAll.mockImplementationOnce(() => fail(500, 'API unavailable'));
    renderPage(<Inbound />);
    expect(await screen.findByRole('alert')).toHaveTextContent('API unavailable');
  });

  it('hides planning and delete actions from operators and viewers', async () => {
    renderPage(<Inbound />, { role: 'Operator' });
    await screen.findByText('RCV-20260101-0001');
    expect(screen.queryByRole('button', { name: /New receipt/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Delete RCV/ })).not.toBeInTheDocument();
  });

  it('creates a receipt with its lines in one request', async () => {
    inboundApi.create.mockImplementation(() => ok({}));
    renderPage(<Inbound />);
    await userEvent.click(await screen.findByRole('button', { name: /New receipt/ }));
    const dialog = screen.getByRole('dialog', { name: 'New inbound receipt' });
    await waitFor(() => expect(within(dialog).getByLabelText('Supplier')).toHaveTextContent('Demo Foods Ltd'));
    await userEvent.selectOptions(within(dialog).getByLabelText('Supplier'), 's1');
    await userEvent.selectOptions(within(dialog).getByLabelText('Warehouse'), 'w1');
    await userEvent.type(within(dialog).getByLabelText('PO number'), 'PO-123');
    await userEvent.selectOptions(within(dialog).getAllByRole('combobox')[2], 'p1');
    await userEvent.type(within(dialog).getByLabelText('Expected quantity line 1'), '40');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create receipt' }));

    await waitFor(() => expect(inboundApi.create).toHaveBeenCalledTimes(1));
    expect(inboundApi.create.mock.calls[0][0]).toMatchObject({
      supplierId: 's1', warehouseId: 'w1', poNumber: 'PO-123',
      items: [{ productId: 'p1', expectedQty: 40, unitCost: 0 }],
    });
    expect(await screen.findByText('Receipt created')).toBeInTheDocument();
  });

  it('requires at least one product line', async () => {
    renderPage(<Inbound />);
    await userEvent.click(await screen.findByRole('button', { name: /New receipt/ }));
    const dialog = screen.getByRole('dialog');
    await waitFor(() => expect(within(dialog).getByLabelText('Supplier')).toHaveTextContent('Demo Foods Ltd'));
    await userEvent.selectOptions(within(dialog).getByLabelText('Supplier'), 's1');
    await userEvent.selectOptions(within(dialog).getByLabelText('Warehouse'), 'w1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create receipt' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Add at least one product line');
    expect(inboundApi.create).not.toHaveBeenCalled();
  });

  it('receives a line into a put-away location of the same warehouse', async () => {
    inboundApi.getById.mockImplementation(() => ok(detail()));
    inboundApi.receive.mockImplementation(() => ok({}));
    renderPage(<Inbound />);
    await userEvent.click(await screen.findByRole('button', { name: /Open/ }));
    await userEvent.click(await screen.findByRole('button', { name: 'Receive' }));
    const dialog = await screen.findByRole('dialog', { name: /Receive Rice 5kg/ });
    const select = within(dialog).getByLabelText('Put-away location');
    await waitFor(() => expect(select).toHaveTextContent('A-01-01'));
    expect(select).not.toHaveTextContent('Z-99-99'); // other warehouse is not offered
    await userEvent.selectOptions(select, 'loc1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Receive and put away' }));
    await waitFor(() => expect(inboundApi.receive).toHaveBeenCalledWith('r1', expect.objectContaining({
      receiptItemId: 'l1', receivedQty: 40, damagedQty: 0, putawayLocationId: 'loc1',
    })));
    expect(await screen.findByText('Goods received and put away')).toBeInTheDocument();
  });

  it('shows the API rejection when receiving fails', async () => {
    inboundApi.getById.mockImplementation(() => ok(detail()));
    inboundApi.receive.mockImplementation(() => fail(400, 'Damaged quantity cannot exceed the received quantity.'));
    renderPage(<Inbound />);
    await userEvent.click(await screen.findByRole('button', { name: /Open/ }));
    await userEvent.click(await screen.findByRole('button', { name: 'Receive' }));
    const dialog = await screen.findByRole('dialog', { name: /Receive Rice 5kg/ });
    await waitFor(() => expect(within(dialog).getByLabelText('Put-away location')).toHaveTextContent('A-01-01'));
    await userEvent.selectOptions(within(dialog).getByLabelText('Put-away location'), 'loc1');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Receive and put away' }));
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Damaged quantity cannot exceed');
  });

  it('completes an in-progress receipt', async () => {
    inboundApi.getById.mockImplementation(() => ok(detail()));
    inboundApi.complete.mockImplementation(() => ok({ status: 'Received' }));
    renderPage(<Inbound />);
    await userEvent.click(await screen.findByRole('button', { name: /Open/ }));
    await userEvent.click(await screen.findByRole('button', { name: 'Complete receipt' }));
    await waitFor(() => expect(inboundApi.complete).toHaveBeenCalledWith('r1'));
    expect(await screen.findByText('Receipt completed as Received')).toBeInTheDocument();
  });

  it('offers no receiving actions on a completed receipt', async () => {
    inboundApi.getById.mockImplementation(() => ok(detail({ status: 'Received' })));
    renderPage(<Inbound />);
    await userEvent.click(await screen.findByRole('button', { name: /Open/ }));
    await screen.findByRole('dialog', { name: /Receipt RCV/ });
    expect(screen.queryByRole('button', { name: 'Receive' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Complete receipt' })).not.toBeInTheDocument();
  });
});
