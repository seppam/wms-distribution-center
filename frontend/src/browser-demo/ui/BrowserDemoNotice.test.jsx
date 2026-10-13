import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage } from '../../test/utils';
import { getEngine, resetEngineForTests } from '../axiosAdapter';
import BrowserDemoNotice from './BrowserDemoNotice';

beforeEach(() => { localStorage.clear(); resetEngineForTests(); });

describe('Browser demo banner', () => {
  it('states that data stays in the browser and offers About and Reset to every role', async () => {
    renderPage(<BrowserDemoNotice />, { role: 'Viewer' });
    const banner = screen.getByRole('region', { name: 'Browser demo' });
    expect(banner).toHaveTextContent('Browser demo: data is stored only in this browser (localStorage); nothing is sent to a server');
    expect(screen.getByRole('button', { name: 'Reset demo data' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'About this demo' }));
    const dialog = screen.getByRole('dialog', { name: 'About this demo' });
    expect(dialog).toHaveTextContent('reimplemented in TypeScript');
    expect(dialog).toHaveTextContent('PostgreSQL');
    expect(dialog.querySelector('a').getAttribute('href')).toMatch(/#hosting-the-real-thing$/);
  });

  it('confirms before resetting and restores the dataset', async () => {
    const engine = getEngine();
    const spy = vi.spyOn(engine, 'resetDataset');
    const reload = vi.fn();
    vi.stubGlobal('location', { ...window.location, reload });
    renderPage(<BrowserDemoNotice />, { role: 'Operator' });
    await userEvent.click(screen.getByRole('button', { name: 'Reset demo data' }));
    expect(spy).not.toHaveBeenCalled();
    await userEvent.click(screen.getAllByRole('button', { name: 'Reset demo data' }).at(-1));
    expect(spy).toHaveBeenCalledTimes(1);
    vi.unstubAllGlobals();
  });

  it('warns when the browser storage is full', async () => {
    const engine = getEngine();
    renderPage(<BrowserDemoNotice />, { role: 'Admin' });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new DOMException('full', 'QuotaExceededError'); });
    engine.resetDataset();
    expect(await screen.findByRole('alert')).toHaveTextContent(/storage is full/);
    vi.restoreAllMocks();
  });
});
