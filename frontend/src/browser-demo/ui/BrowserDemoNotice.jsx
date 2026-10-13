import { useEffect, useState } from 'react';
import { HardDrive } from 'lucide-react';
import { Confirm, useToast } from '../../components/ui';
import { getEngine } from '../axiosAdapter';
import AboutDemoDialog, { DEMO_SENTENCE, RepoLink } from './AboutDemoDialog';
import './browser-demo.css';

const reload = () => window.location.reload();

/**
 * Banner of the browser-only demo build. Everyone may reset here: the data belongs to this browser alone, so unlike the
 * shared Docker demo there is nobody else to protect (the API route itself stays Admin-only, like in the real stack).
 */
export default function BrowserDemoNotice() {
  const engine = getEngine();
  const toast = useToast();
  const [status, setStatus] = useState(() => engine.getStatus());
  const [about, setAbout] = useState(false);
  const [asking, setAsking] = useState(false);
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => engine.subscribe(() => setStatus(engine.getStatus())), [engine]);

  function reset() {
    engine.resetDataset();
    toast.success('Demo data restored');
    setAsking(false);
    setTimeout(reload, 700);
  }

  return (
    <div className="browser-demo">
      <div className="demo-banner" role="region" aria-label="Browser demo">
        <HardDrive size={16} aria-hidden="true" />
        <span className="banner-text">
          {DEMO_SENTENCE} <RepoLink />
        </span>
        <span className="banner-actions">
          <button type="button" className="btn btn-sm btn-secondary" onClick={() => setAbout(true)}>About this demo</button>
          <button type="button" className="btn btn-sm btn-secondary" onClick={() => setAsking(true)}>Reset demo data</button>
        </span>
      </div>

      {status.problem === 'quota' && (
        <div className="demo-banner warn" role="alert">
          Browser storage is full, so your latest changes are kept in memory only and will be lost when you reload. Use <strong>Reset demo data</strong> to free space.
        </div>
      )}
      {status.problem === 'unavailable' && (
        <div className="demo-banner warn" role="alert">
          This browser does not allow saving data here (private window or blocked site data). The demo works, but everything is lost when you reload.
        </div>
      )}
      {status.notice && !dismissed && (
        <div className="demo-banner note" role="status">
          <span className="banner-text">{status.notice}</span>
          <button type="button" className="btn btn-sm btn-secondary" onClick={() => setDismissed(true)}>Dismiss</button>
        </div>
      )}

      {about && <AboutDemoDialog status={status} onClose={() => setAbout(false)} />}
      {asking && (
        <Confirm
          title="Reset demo data?"
          message="This deletes everything you changed in this browser and restores the original fictional dataset."
          confirmLabel="Reset demo data"
          danger
          onConfirm={reset}
          onCancel={() => setAsking(false)}
        />
      )}
    </div>
  );
}
