import { useState } from 'react';
import { Plus } from 'lucide-react';
import { DataState, ErrorBanner, Field, Modal, PageHeader, Select, useToast } from '../components/ui';
import { useAuth } from '../contexts/AuthContext';
import { useLoad } from '../hooks/useLoad';
import { useOptions } from '../hooks/useOptions';
import { inventoryApi, locationsApi, transfersApi } from '../services';
import { errorMessage } from '../services/api';
import { badgeClass, fmtDateTime, fmtNum } from '../utils/format';

function NewTransfer({ onClose, onSaved }) {
  const toast = useToast();
  const stock = useLoad(() => inventoryApi.query(), [], { initial: [] });
  const locations = useOptions(() => locationsApi.getAll(), (l) => `${l.locationCode} (${l.zoneName})`);
  const [form, setForm] = useState({ stockId: '', toLocationId: '', qty: '', reason: '' });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const set = (k) => (v) => setForm((f) => ({ ...f, [k]: v }));

  const movable = (stock.data || []).filter((s) => s.qtyOnHand - s.qtyReserved > 0);
  const source = movable.find((s) => s.id === form.stockId);

  async function submit(e) {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      await transfersApi.create({
        productId: source.productId, fromLocationId: source.locationId,
        toLocationId: form.toLocationId, qty: Number(form.qty), reason: form.reason || null,
      });
      toast.success('Transfer created. Complete it to move the stock.');
      onSaved();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal title="New stock transfer" onClose={onClose}>
      <form onSubmit={submit}>
        <ErrorBanner message={error || stock.error || locations.error} />
        <Field label="Stock to move" hint="Only unreserved stock can be moved.">
          {(id) => (
            <Select id={id} value={form.stockId} onChange={set('stockId')} required
              options={movable.map((s) => ({ value: s.id, label: `${s.productSku} - ${s.productName} @ ${s.locationCode} (available ${fmtNum(s.qtyOnHand - s.qtyReserved)})` }))} />
          )}
        </Field>
        <Field label="Destination location">
          {(id) => <Select id={id} value={form.toLocationId} onChange={set('toLocationId')} required options={locations.options.filter((o) => o.value !== source?.locationId)} />}
        </Field>
        <Field label="Quantity">
          {(id) => <input id={id} className="form-input" type="number" min="0.0001" step="any" required value={form.qty}
            max={source ? source.qtyOnHand - source.qtyReserved : undefined} onChange={(e) => set('qty')(e.target.value)} />}
        </Field>
        <Field label="Reason">{(id) => <input id={id} className="form-input" value={form.reason} onChange={(e) => set('reason')(e.target.value)} placeholder="e.g. Consolidate bins" />}</Field>
        <div className="modal-footer">
          <button type="button" className="btn btn-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={saving}>Create transfer</button>
        </div>
      </form>
    </Modal>
  );
}

export default function Transfers() {
  const { can } = useAuth();
  const toast = useToast();
  const { data, loading, error, reload } = useLoad(() => transfersApi.getAll(), [], { initial: [] });
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState(null);

  async function act(id, action) {
    setBusy(id);
    try {
      await transfersApi[action](id);
      toast.success(action === 'complete' ? 'Transfer completed: stock moved' : 'Transfer cancelled');
      reload();
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setBusy(null);
    }
  }

  return (
    <div>
      <PageHeader title="Stock Transfers" subtitle="Move stock between locations; completion posts the ledger entries">
        {can('operate') && <button className="btn btn-primary" onClick={() => setCreating(true)}><Plus size={16} /> New transfer</button>}
      </PageHeader>
      <div className="card">
        <DataState loading={loading} error={error} onRetry={reload} empty={!data?.length}
          emptyTitle="No transfers yet" emptyText="Create a transfer to move stock from one bin to another.">
          <table className="data-table">
            <thead><tr><th>Transfer</th><th>Product</th><th>From</th><th>To</th><th>Qty</th><th>Reason</th><th>Status</th><th>Moved</th>{can('operate') && <th>Actions</th>}</tr></thead>
            <tbody>
              {data.map((t) => (
                <tr key={t.id}>
                  <td><strong>{t.transferNo}</strong><div className="muted">{t.requesterName}</div></td>
                  <td>{t.productName}<div className="muted">{t.productSku}</div></td>
                  <td><span className="code-chip">{t.fromLocationCode}</span></td>
                  <td><span className="code-chip">{t.toLocationCode}</span></td>
                  <td style={{ fontWeight: 600 }}>{fmtNum(t.qty)}</td>
                  <td>{t.reason || '-'}</td>
                  <td><span className={badgeClass(t.status)}>{t.status}</span></td>
                  <td>{fmtDateTime(t.transferredAt)}</td>
                  {can('operate') && (
                    <td>
                      {t.status === 'Draft' && (
                        <div className="row-actions">
                          <button className="btn btn-sm btn-success" disabled={busy === t.id} onClick={() => act(t.id, 'complete')}>Complete</button>
                          <button className="btn btn-sm btn-outline" disabled={busy === t.id} onClick={() => act(t.id, 'cancel')}>Cancel</button>
                        </div>
                      )}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </DataState>
      </div>
      {creating && <NewTransfer onClose={() => setCreating(false)} onSaved={() => { setCreating(false); reload(); }} />}
    </div>
  );
}
