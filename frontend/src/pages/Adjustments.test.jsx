import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fail, ok, renderPage } from '../test/utils';
import Adjustments from './Adjustments';

vi.mock('../services', () => ({
  inventoryApi: { getAdjustments: vi.fn(), createAdjustment: vi.fn(), approveAdjustment: vi.fn(), rejectAdjustment: vi.fn() },
  productsApi: { getAll: vi.fn() },
  locationsApi: { getAll: vi.fn() },
}));
import { inventoryApi, locationsApi, productsApi } from '../services';

const adj = (over = {}) => ({
  id: 'a1', adjNumber: 'ADJ-1', productName: 'Rice', locationCode: 'A-01', adjType: 'Negative', qtyBefore: 100, adjQty: 5,
  qtyAfter: 95, reason: 'Damaged', status: 'Draft', creatorName: 'Op', createdAt: '2026-01-01T00:00:00Z', ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  inventoryApi.getAdjustments.mockImplementation(() => ok([adj()]));
  productsApi.getAll.mockImplementation(() => ok([{ id: 'p1', sku: 'SKU-1', name: 'Rice' }]));
  locationsApi.getAll.mockImplementation(() => ok([{ id: 'l1', locationCode: 'A-01', zoneName: 'Ambient' }]));
});

describe('Adjustments page', () => {
  it('lists adjustments with the signed change', async () => {
    renderPage(<Adjustments />);
    expect(await screen.findByText('ADJ-1')).toBeInTheDocument();
    expect(screen.getByText('-5')).toBeInTheDocument();
  });

  it('lets a supervisor approve and reject drafts', async () => {
    inventoryApi.approveAdjustment.mockImplementation(() => ok({}));
    inventoryApi.rejectAdjustment.mockImplementation(() => ok({}));
    renderPage(<Adjustments />, { role: 'Supervisor' });
    await userEvent.click(await screen.findByRole('button', { name: /Approve/ }));
    await waitFor(() => expect(inventoryApi.approveAdjustment).toHaveBeenCalledWith('a1'));
    await userEvent.click(await screen.findByRole('button', { name: /Reject/ }));
    await waitFor(() => expect(inventoryApi.rejectAdjustment).toHaveBeenCalledWith('a1'));
  });

  it('does not offer approval to operators, who can still propose', async () => {
    renderPage(<Adjustments />, { role: 'Operator' });
    await screen.findByText('ADJ-1');
    expect(screen.queryByRole('button', { name: /Approve/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /New adjustment/ })).toBeInTheDocument();
  });

  it('only shows approval buttons for drafts', async () => {
    inventoryApi.getAdjustments.mockImplementation(() => ok([adj({ status: 'Approved' })]));
    renderPage(<Adjustments />);
    await screen.findByText('ADJ-1');
    expect(screen.queryByRole('button', { name: /Approve/ })).not.toBeInTheDocument();
  });

  it('submits a new adjustment with a numeric quantity', async () => {
    inventoryApi.createAdjustment.mockImplementation(() => ok({}));
    renderPage(<Adjustments />, { role: 'Operator' });
    await userEvent.click(await screen.findByRole('button', { name: /New adjustment/ }));
    const dialog = screen.getByRole('dialog', { name: 'New stock adjustment' });
    await waitFor(() => expect(within(dialog).getByLabelText('Product')).toHaveTextContent('SKU-1'));
    await userEvent.selectOptions(within(dialog).getByLabelText('Product'), 'p1');
    await userEvent.selectOptions(within(dialog).getByLabelText('Location'), 'l1');
    await userEvent.type(within(dialog).getByLabelText('Quantity'), '3');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Submit adjustment' }));
    await waitFor(() => expect(inventoryApi.createAdjustment).toHaveBeenCalledWith(expect.objectContaining({
      productId: 'p1', locationId: 'l1', adjType: 'Negative', adjQty: 3,
    })));
  });

  it('shows a rejected approval', async () => {
    inventoryApi.approveAdjustment.mockImplementation(() => fail(400, 'Stock cannot drop below the 80 reserved for open orders.'));
    renderPage(<Adjustments />);
    await userEvent.click(await screen.findByRole('button', { name: /Approve/ }));
    expect(await screen.findByText(/reserved for open orders/)).toBeInTheDocument();
  });
});
