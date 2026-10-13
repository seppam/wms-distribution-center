import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fail, ok, renderPage } from '../test/utils';
import Picks from './Picks';

vi.mock('../services', () => ({ picksApi: { getAll: vi.fn(), confirm: vi.fn() } }));
import { picksApi } from '../services';

const task = (over = {}) => ({
  id: 't1', orderNumber: 'ORD-1', productName: 'Rice 5kg', productSku: 'SKU-1', fromLocationCode: 'A-01-01',
  qtyToPick: 20, qtyPicked: 0, status: 'Assigned', pickerName: null, ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  picksApi.getAll.mockImplementation(() => ok([task(), task({ id: 't2', status: 'Picked', qtyPicked: 20, orderNumber: 'ORD-0' })]));
});

describe('Pick tasks page', () => {
  it('shows only open tasks by default and the picked ones on the other tab', async () => {
    renderPage(<Picks />);
    expect(await screen.findByText('ORD-1')).toBeInTheDocument();
    expect(screen.queryByText('ORD-0')).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('tab', { name: 'Picked' }));
    await waitFor(() => expect(picksApi.getAll).toHaveBeenLastCalledWith({ status: 'Picked' }));
  });

  it('confirms the full quantity by default', async () => {
    picksApi.confirm.mockImplementation(() => ok({}));
    renderPage(<Picks />, { role: 'Operator' });
    await userEvent.click(await screen.findByRole('button', { name: /Confirm/ }));
    await waitFor(() => expect(picksApi.confirm).toHaveBeenCalledWith('t1', 20));
    expect(await screen.findByText('Pick confirmed')).toBeInTheDocument();
  });

  it('records a short pick when fewer units are found', async () => {
    picksApi.confirm.mockImplementation(() => ok({}));
    renderPage(<Picks />);
    const input = await screen.findByLabelText(/Quantity picked for SKU-1/);
    await userEvent.clear(input);
    await userEvent.type(input, '15');
    await userEvent.click(screen.getByRole('button', { name: /Confirm/ }));
    await waitFor(() => expect(picksApi.confirm).toHaveBeenCalledWith('t1', 15));
    expect(await screen.findByText(/Short pick recorded/)).toBeInTheDocument();
  });

  it('shows the over-pick rejection', async () => {
    picksApi.confirm.mockImplementation(() => fail(400, 'Over-pick rejected: you can pick at most 20 (requested 25).'));
    renderPage(<Picks />);
    const input = await screen.findByLabelText(/Quantity picked/);
    await userEvent.clear(input);
    await userEvent.type(input, '25');
    await userEvent.click(screen.getByRole('button', { name: /Confirm/ }));
    expect(await screen.findByText(/Over-pick rejected/)).toBeInTheDocument();
  });

  it('is read-only for viewers', async () => {
    renderPage(<Picks />, { role: 'Viewer' });
    await screen.findByText('ORD-1');
    expect(screen.queryByRole('button', { name: /Confirm/ })).not.toBeInTheDocument();
  });

  it('shows an empty state', async () => {
    picksApi.getAll.mockImplementation(() => ok([]));
    renderPage(<Picks />);
    expect(await screen.findByText('No open pick tasks')).toBeInTheDocument();
  });
});
