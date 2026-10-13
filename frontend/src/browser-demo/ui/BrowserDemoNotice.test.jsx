import { screen, within, render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderPage } from '../../test/utils';
import { getEngine, resetEngineForTests } from '../axiosAdapter';
import BrowserDemoNotice from './BrowserDemoNotice';
import { BrowserDemoExplainer, REPO_URL } from './AboutDemoDialog';

beforeEach(() => { localStorage.clear(); resetEngineForTests(); });

describe('Browser demo banner', () => {
  it('states that data stays in the browser and offers About and Reset to every role', async () => {
    renderPage(<BrowserDemoNotice />, { role: 'Viewer' });
    const banner = screen.getByRole('region', { name: 'Browser demo' });
    expect(banner).toHaveTextContent('Demo version: runs entirely in your browser. No server and no database; data is saved in your browser (localStorage).');
    const link = within(banner).getByRole('link', { name: /Run the full project on your own computer/ });
    expect(link).toHaveAttribute('href', REPO_URL);
    expect(link).toHaveAttribute('target', '_blank');
    expect(link.getAttribute('rel')).toMatch(/noopener/);
    expect(screen.getByRole('button', { name: 'Reset demo data' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'About this demo' }));
    const dialog = screen.getByRole('dialog', { name: 'About this demo' });
    expect(dialog).toHaveTextContent('reimplemented in TypeScript');
    expect(dialog).toHaveTextContent('PostgreSQL');
    expect(dialog).toHaveTextContent('ASP.NET Core 9 API + PostgreSQL');
    expect(dialog).toHaveTextContent('docker compose -f docker-compose.demo.yml up --build');
    const readme = within(dialog).getByRole('link', { name: /repository README/ });
    expect(readme).toHaveAttribute('href', `${REPO_URL}#readme`);
    for (const a of dialog.querySelectorAll('a')) {
      expect(a).toHaveAttribute('target', '_blank');
      expect(a.getAttribute('rel')).toMatch(/noopener/);
    }
  });

  it('the login explainer repeats the sentence and links to the repo in a new tab', () => {
    render(<BrowserDemoExplainer />);
    expect(screen.getByText(/Demo version: runs entirely in your browser/)).toBeInTheDocument();
    const link = screen.getByRole('link', { name: /Run the full project on your own computer/ });
    expect(link).toHaveAttribute('href', REPO_URL);
    expect(link).toHaveAttribute('target', '_blank');
    expect(link.getAttribute('rel')).toMatch(/noopener/);
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
