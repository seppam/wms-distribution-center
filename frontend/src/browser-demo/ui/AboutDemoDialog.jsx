import { ExternalLink } from 'lucide-react';
import { Modal } from '../../components/ui';

export const REPO_URL = 'https://github.com/seppam/wms-distribution-center';
export const HOSTING_URL = `${REPO_URL}#hosting-the-real-thing`;

/** Explains what this build simulates and how it differs from the full stack. Opens from the banner. */
export default function AboutDemoDialog({ status, onClose }) {
  const kb = Math.max(1, Math.round((status?.bytes ?? 0) / 1024));
  return (
    <Modal title="About this demo" onClose={onClose} wide footer={<button className="btn btn-primary" onClick={onClose}>Got it</button>}>
      <div className="about-demo">
        <section>
          <h3>What you are looking at</h3>
          <p>
            A complete warehouse system (receiving and put-away, allocation, picking, packing, shipping, adjustments, transfers, returns)
            running entirely in your browser, with fictional data. There is no server and no database: nothing you do is sent anywhere.
          </p>
        </section>
        <section>
          <h3>What is simulated in the browser</h3>
          <ul>
            <li>The business rules of the real backend are reimplemented in TypeScript: first-expiry-first-out allocation that is all-or-nothing, no over-allocation, over-pick rejection and short picks, approvals for adjustments, lot and reservation protection.</li>
            <li>The four roles use the same permission matrix as the real API (it lives in one shared contract file that both implementations are tested against).</li>
            <li>It is a single-user simulation. Your data lives in this browser only, per browser and per device (currently about {kb} KB in {status?.kind === 'memory' ? 'memory' : 'localStorage'}). Clearing the site data wipes it, and a private window starts fresh every time.</li>
            <li>Use <strong>Reset demo data</strong> in the banner to restore the original dataset at any time.</li>
          </ul>
        </section>
        <section>
          <h3>How it differs from the full stack</h3>
          <table>
            <thead><tr><th></th><th>Browser demo</th><th>Full stack</th></tr></thead>
            <tbody>
              <tr><th>API</th><td>TypeScript engine in the page</td><td>ASP.NET Core 9 Web API</td></tr>
              <tr><th>Data</th><td>localStorage, this browser only</td><td>PostgreSQL 16 with EF Core migrations</td></tr>
              <tr><th>Sign-in</th><td>Pick a role, no real authentication</td><td>BCrypt passwords, JWT, rate limiting</td></tr>
              <tr><th>Users</th><td>Just you</td><td>Many users at once, optimistic concurrency</td></tr>
              <tr><th>Persistence</th><td>Lost with site data</td><td>Durable, backed up like any database</td></tr>
            </tbody>
          </table>
        </section>
        <section>
          <h3>Want a real instance?</h3>
          <p>
            The repository explains how to host the real thing (API, database, environment variables, costs and trade-offs):{' '}
            <a href={HOSTING_URL} target="_blank" rel="noreferrer noopener">Hosting the real thing <ExternalLink size={12} aria-hidden="true" /></a>.
          </p>
        </section>
      </div>
    </Modal>
  );
}
