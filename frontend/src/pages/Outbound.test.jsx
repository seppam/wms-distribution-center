import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fail, ok, renderPage } from '../test/utils';
import Outbound from './Outbound';

vi.mock('../services', () => ({
  outboundApi: { getAll: vi.fn(), getById: vi.fn(), create: vi.fn(), delete: vi.fn(), startPicking: vi.fn(), assignPicker: vi.fn(), pack: vi.fn(), ship: vi.fn(), cancel: vi.fn() },
  picksApi: { getAll: vi.fn() },
  warehousesApi: { getAll: vi.fn() },
  productsApi: { getAll: vi.fn() },
  inventoryApi: { query: vi.fn() },
  carriersApi: { getAll: vi.fn() },
  usersApi: { getAll: vi.fn() },
}));
import { carriersApi, inventoryApi, outboundApi, picksApi, productsApi, usersApi, warehousesApi } from '../services';

const order = (status) => ({ id: 'o1', orderNumber: 'ORD-20260101-0001', destination: 'Store 12', priority: 'Normal', status, dueDate: null });
const detail = (status, over = {}) => ({
  ...order(status), warehouseName: 'Main DC', creatorName: 'Demo Admin', createdAt: '2026-01-01T10:00:00Z',
  items: [{ id: 'i1', productName: 'Rice 5kg', productSku: 'SKU-1', orderedQty: 30, pickedQty: 0, shippedQty: 0, unitPrice: 4, status: 'Pending' }],
  packLists: [], shipments: [], ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  outboundApi.getAll.mockImplementation(() => ok([order('Draft')]));
  picksApi.getAll.mockImplementation(() => ok([]));
  warehousesApi.getAll.mockImplementation(() => ok([{ id: 'w1', name: 'Main DC' }]));
  productsApi.getAll.mockImplementation(() => ok([{ id: 'p1', sku: 'SKU-1', name: 'Rice 5kg' }]));
  inventoryApi.query.mockImplementation(() => ok([{ productId: 'p1', status: 'Available', qtyOnHand: 100, qtyReserved: 40 }]));
  carriersApi.getAll.mockImplementation(() => ok([{ id: 'c1', name: 'Demo Logistics' }]));
  usersApi.getAll.mockImplementation(() => ok([{ id: 'u9', fullName: 'Olive Operator', roleName: 'Operator', isActive: true }]));
});

const openFirst = async () => userEvent.click(await screen.findByRole('button', { name: /Open/ }));

describe('Outbound page', () => {
  it('lists orders and filters by status', async () => {
    outboundApi.getAll.mockImplementation(() => ok([order('Draft'), { ...order('Shipped'), id: 'o2', orderNumber: 'ORD-20260101-0002' }]));
    renderPage(<Outbound />);
    expect(await screen.findByText('ORD-20260101-0001')).toBeInTheDocument();
    await userEvent.selectOptions(screen.getByRole('combobox'), 'Shipped');
    expect(screen.queryByText('ORD-20260101-0001')).not.toBeInTheDocument();
    expect(screen.getByText('ORD-20260101-0002')).toBeInTheDocument();
  });

  it('shows loading failure with retry', async () => {
    outboundApi.getAll.mockImplementationOnce(() => fail(500, 'Boom'));
    renderPage(<Outbound />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Boom');
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument();
  });

  it('creates an order with lines and shows available stock as a hint', async () => {
    outboundApi.create.mockImplementation(() => ok({}));
    renderPage(<Outbound />);
    await userEvent.click(await screen.findByRole('button', { name: /New order/ }));
    const dialog = screen.getByRole('dialog', { name: 'New outbound order' });
    await waitFor(() => expect(within(dialog).getByLabelText('Warehouse')).toHaveTextContent('Main DC'));
    await userEvent.selectOptions(within(dialog).getByLabelText('Warehouse'), 'w1');
    await userEvent.type(within(dialog).getByLabelText('Destination'), 'Store 12');
    await userEvent.selectOptions(within(dialog).getAllByRole('combobox')[2], 'p1');
    expect(within(dialog).getByText(/Available now: 60/)).toBeInTheDocument();
    await userEvent.type(within(dialog).getByLabelText('Quantity line 1'), '90');
    expect(within(dialog).getByText(/not enough stock/)).toBeInTheDocument();
    await userEvent.clear(within(dialog).getByLabelText('Quantity line 1'));
    await userEvent.type(within(dialog).getByLabelText('Quantity line 1'), '30');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create order' }));
    await waitFor(() => expect(outboundApi.create).toHaveBeenCalledTimes(1));
    expect(outboundApi.create.mock.calls[0][0]).toMatchObject({
      warehouseId: 'w1', orderType: 'Sales', destination: 'Store 12', items: [{ productId: 'p1', orderedQty: 30, unitPrice: 0 }],
    });
  });

  it('allocates stock for a draft order', async () => {
    outboundApi.getById.mockImplementation(() => ok(detail('Draft')));
    outboundApi.startPicking.mockImplementation(() => ok({}));
    renderPage(<Outbound />);
    await openFirst();
    await userEvent.click(await screen.findByRole('button', { name: 'Allocate stock and start picking' }));
    await waitFor(() => expect(outboundApi.startPicking).toHaveBeenCalledWith('o1'));
  });

  it('surfaces an insufficient-stock rejection from the API', async () => {
    outboundApi.getById.mockImplementation(() => ok(detail('Draft')));
    outboundApi.startPicking.mockImplementation(() => fail(400, 'Insufficient available stock for SKU-1: ordered 30, available 10.'));
    renderPage(<Outbound />);
    await openFirst();
    await userEvent.click(await screen.findByRole('button', { name: 'Allocate stock and start picking' }));
    expect(await screen.findByText(/Insufficient available stock/)).toBeInTheDocument();
  });

  it('walks a picked order through pack and ship', async () => {
    outboundApi.getById.mockImplementation(() => ok(detail('Picked')));
    outboundApi.pack.mockImplementation(() => ok({}));
    outboundApi.ship.mockImplementation(() => ok({}));
    renderPage(<Outbound />);
    await openFirst();
    await userEvent.click(await screen.findByRole('button', { name: 'Pack' }));
    const pack = screen.getByRole('dialog', { name: 'Pack order' });
    await userEvent.type(within(pack).getByLabelText('Weight (kg)'), '12.5');
    outboundApi.getById.mockImplementation(() => ok(detail('Packed'))); // the order reloads as Packed after packing
    await userEvent.click(within(pack).getByRole('button', { name: 'Pack' }));
    await waitFor(() => expect(outboundApi.pack).toHaveBeenCalledWith('o1', { cartonNo: expect.stringMatching(/^CTN-/), weightKg: 12.5 }));
    await userEvent.click(await screen.findByRole('button', { name: 'Ship' }));
    const ship = await screen.findByRole('dialog', { name: 'Ship order' });
    await waitFor(() => expect(within(ship).getByLabelText('Carrier')).toHaveTextContent('Demo Logistics'));
    await userEvent.selectOptions(within(ship).getByLabelText('Carrier'), 'c1');
    await userEvent.type(within(ship).getByLabelText('Tracking number'), 'TRK-1');
    await userEvent.click(within(ship).getByRole('button', { name: 'Ship order' }));
    await waitFor(() => expect(outboundApi.ship).toHaveBeenCalledWith('o1', expect.objectContaining({ carrierId: 'c1', trackingNo: 'TRK-1' })));
  });

  it('only offers steps the role may perform', async () => {
    outboundApi.getById.mockImplementation(() => ok(detail('Draft')));
    const { unmount } = renderPage(<Outbound />, { role: 'Operator' });
    await openFirst();
    await screen.findByRole('dialog', { name: /Order ORD/ });
    expect(screen.queryByRole('button', { name: 'Allocate stock and start picking' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancel order' })).not.toBeInTheDocument();
    unmount();

    outboundApi.getById.mockImplementation(() => ok(detail('Packed')));
    renderPage(<Outbound />, { role: 'Viewer' });
    await openFirst();
    await screen.findByRole('dialog', { name: /Order ORD/ });
    expect(screen.queryByRole('button', { name: 'Ship' })).not.toBeInTheDocument();
  });

  it('cancels an order after confirmation', async () => {
    outboundApi.getById.mockImplementation(() => ok(detail('Picking')));
    outboundApi.cancel.mockImplementation(() => ok({}));
    renderPage(<Outbound />);
    await openFirst();
    await userEvent.click(await screen.findByRole('button', { name: 'Cancel order' }));
    const confirm = await screen.findByRole('dialog', { name: 'Cancel order' });
    await userEvent.click(within(confirm).getByRole('button', { name: 'Cancel order' }));
    await waitFor(() => expect(outboundApi.cancel).toHaveBeenCalledWith('o1'));
  });

  it('shows pick tasks and lets a supervisor assign a picker while picking', async () => {
    outboundApi.getById.mockImplementation(() => ok(detail('Picking')));
    outboundApi.assignPicker.mockImplementation(() => ok({}));
    picksApi.getAll.mockImplementation(() => ok([{ id: 't1', productSku: 'SKU-1', fromLocationCode: 'A-01-01', qtyToPick: 30, qtyPicked: 0, status: 'Assigned' }]));
    renderPage(<Outbound />);
    await openFirst();
    expect(await screen.findByText('A-01-01')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText(/Olive Operator/)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getAllByRole('combobox').at(-1), 'u9');
    await userEvent.click(screen.getByRole('button', { name: 'Assign' }));
    await waitFor(() => expect(outboundApi.assignPicker).toHaveBeenCalledWith('o1', 'u9'));
  });
});
