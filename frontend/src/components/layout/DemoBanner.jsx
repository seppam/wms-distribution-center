import { useState } from 'react';
import { FlaskConical } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import useDemoInfo from '../../hooks/useDemoInfo';
import { errorMessage } from '../../services/api';
import { reloadPage, resetDemoData } from '../../services/demo';
import { Confirm, useToast } from '../ui';

/** Shown only when the API reports demo mode. Admins can restore the seeded dataset. */
export default function DemoBanner() {
  const { enabled } = useDemoInfo();
  const { isAdmin } = useAuth();
  const toast = useToast();
  const [asking, setAsking] = useState(false);
  const [busy, setBusy] = useState(false);

  if (!enabled) return null;

  async function reset() {
    setBusy(true);
    try {
      await resetDemoData();
      toast.success('Demo data restored');
      setAsking(false);
      setTimeout(reloadPage, 700);
    } catch (err) {
      toast.error(errorMessage(err, 'Could not reset the demo data'));
      setAsking(false);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="demo-banner" role="region" aria-label="Demo mode">
      <FlaskConical size={16} aria-hidden="true" />
      <span><strong>Demo mode:</strong> fictional data that resets. Click around freely.</span>
      {isAdmin && (
        <button type="button" className="btn btn-sm btn-secondary" onClick={() => setAsking(true)}>Reset demo data</button>
      )}
      {asking && (
        <Confirm
          title="Reset demo data?"
          message="This deletes everything you changed and restores the original fictional dataset and demo accounts."
          confirmLabel="Reset demo data"
          danger
          busy={busy}
          onConfirm={reset}
          onCancel={() => setAsking(false)}
        />
      )}
    </div>
  );
}
