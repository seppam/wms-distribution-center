import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fail, ok, renderPage } from '../../test/utils';
import DemoBanner from './DemoBanner';

vi.mock('../../services/demo', () => ({ loadDemoInfo: vi.fn(), resetDemoData: vi.fn(), reloadPage: vi.fn() }));
import { loadDemoInfo, reloadPage, resetDemoData } from '../../services/demo';

beforeEach(() => {
  vi.clearAllMocks();
  loadDemoInfo.mockResolvedValue({ enabled: true, accounts: [] });
});

describe('Demo banner', () => {
  it('is not rendered outside demo mode', async () => {
    loadDemoInfo.mockResolvedValue({ enabled: false });
    renderPage(<DemoBanner />);
    await waitFor(() => expect(loadDemoInfo).toHaveBeenCalled());
    expect(screen.queryByRole('region', { name: 'Demo mode' })).not.toBeInTheDocument();
  });

  it('tells everyone the data is fictional and resets', async () => {
    renderPage(<DemoBanner />, { role: 'Viewer' });
    expect(await screen.findByRole('region', { name: 'Demo mode' })).toHaveTextContent('fictional data that resets');
  });

  it('offers the reset to admins only', async () => {
    const { unmount } = renderPage(<DemoBanner />, { role: 'Operator' });
    await screen.findByRole('region', { name: 'Demo mode' });
    expect(screen.queryByRole('button', { name: 'Reset demo data' })).not.toBeInTheDocument();
    unmount();
    renderPage(<DemoBanner />, { role: 'Admin' });
    expect(await screen.findByRole('button', { name: 'Reset demo data' })).toBeInTheDocument();
  });

  it('asks for confirmation, resets and reloads', async () => {
    resetDemoData.mockImplementation(() => ok({}));
    renderPage(<DemoBanner />, { role: 'Admin' });
    await userEvent.click(await screen.findByRole('button', { name: 'Reset demo data' }));
    expect(resetDemoData).not.toHaveBeenCalled();
    await userEvent.click(screen.getAllByRole('button', { name: 'Reset demo data' }).at(-1));
    await waitFor(() => expect(resetDemoData).toHaveBeenCalledTimes(1));
    expect(await screen.findByText('Demo data restored')).toBeInTheDocument();
    await waitFor(() => expect(reloadPage).toHaveBeenCalled());
  });

  it('shows the rate-limit message from the API', async () => {
    resetDemoData.mockImplementation(() => fail(429, 'The demo was reset too often. Try again in 60 seconds.'));
    renderPage(<DemoBanner />, { role: 'Admin' });
    await userEvent.click(await screen.findByRole('button', { name: 'Reset demo data' }));
    await userEvent.click(screen.getAllByRole('button', { name: 'Reset demo data' }).at(-1));
    expect(await screen.findByText(/reset too often/)).toBeInTheDocument();
    expect(reloadPage).not.toHaveBeenCalled();
  });
});
