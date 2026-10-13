import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fail, ok, renderPage } from '../test/utils';
import Inventory from './Inventory';
import Shipments from './Shipments';
import Uoms from './Uoms';

vi.mock('../services', () => ({
  inventoryApi: { query: vi.fn(), getTransactions: vi.fn() },
  shipmentsApi: { getAll: vi.fn(), deliver: vi.fn() },
  uomsApi: { getAll: vi.fn(), create: vi.fn(), update: vi.fn(), delete: vi.fn() },
}));
import { inventoryApi, shipmentsApi, uomsApi } from '../services';

beforeEach(() => { vi.clearAllMocks(); });

describe('Shipments page', () => {
  const shipment = (over = {}) => ({ id: 's1', orderNumber: 'ORD-1', carrierName: 'Demo Logistics', trackingNo: 'TRK-1', status: 'Dispatched', ...over });

  it('marks a dispatched shipment as delivered', async () => {
    shipmentsApi.getAll.mockImplementation(() => ok([shipment()]));
    shipmentsApi.deliver.mockImplementation(() => ok({}));
    renderPage(<Shipments />, { role: 'Operator' });
    await userEvent.click(await screen.findByRole('button', { name: 'Mark delivered' }));
    await waitFor(() => expect(shipmentsApi.deliver).toHaveBeenCalledWith('s1'));
    expect(await screen.findByText('Shipment marked as delivered')).toBeInTheDocument();
  });

  it('has no action for delivered shipments or viewers', async () => {
    shipmentsApi.getAll.mockImplementation(() => ok([shipment({ status: 'Delivered' })]));
    renderPage(<Shipments />);
    await screen.findByText('ORD-1');
    expect(screen.queryByRole('button', { name: 'Mark delivered' })).not.toBeInTheDocument();
  });

  it('shows empty and error states', async () => {
    shipmentsApi.getAll.mockImplementationOnce(() => ok([]));
    const { unmount } = renderPage(<Shipments />);
    expect(await screen.findByText('No shipments yet')).toBeInTheDocument();
    unmount();
    shipmentsApi.getAll.mockImplementationOnce(() => fail(500, 'Nope'));
    renderPage(<Shipments />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Nope');
  });
});

describe('Inventory page', () => {
  it('shows stock with available quantity and the movement ledger tab', async () => {
    inventoryApi.query.mockImplementation(() => ok([
      { id: 'i1', productName: 'Rice', productSku: 'SKU-1', locationCode: 'A-01', zoneName: 'Ambient', qtyOnHand: 100, qtyReserved: 30, status: 'Available' },
    ]));
    inventoryApi.getTransactions.mockImplementation(() => ok([
      { id: 1, createdAt: '2026-01-01T10:00:00Z', productName: 'Rice', productSku: 'SKU-1', locationCode: 'A-01', transactionType: 'In', referenceType: 'Inbound', uomQty: 100, qtyBefore: 0, qtyAfter: 100, userName: 'Op' },
    ]));
    renderPage(<Inventory />);
    expect(await screen.findByText('70')).toBeInTheDocument(); // available = on hand - reserved
    await userEvent.click(screen.getByRole('tab', { name: 'Movement ledger' }));
    expect(await screen.findByText('Inbound')).toBeInTheDocument();
  });

  it('shows an empty state instead of mock data', async () => {
    inventoryApi.query.mockImplementation(() => ok([]));
    renderPage(<Inventory />);
    expect(await screen.findByText('No stock found')).toBeInTheDocument();
  });

  it('shows a load error', async () => {
    inventoryApi.query.mockImplementation(() => fail(500, 'Stock service down'));
    renderPage(<Inventory />);
    expect(await screen.findByRole('alert')).toHaveTextContent('Stock service down');
  });
});

describe('Units of measure page', () => {
  it('lists units and is read-only for operators', async () => {
    uomsApi.getAll.mockImplementation(() => ok([{ id: 'u1', code: 'PCS', name: 'Piece', conversionFactor: 1, isBase: true }]));
    renderPage(<Uoms />, { role: 'Operator' });
    expect(await screen.findByText('PCS')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Add Unit/ })).not.toBeInTheDocument();
  });
});
