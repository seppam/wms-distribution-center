import { useState } from 'react';
import { Plus, Check, X } from 'lucide-react';
import { DataState, ErrorBanner, Field, Modal, PageHeader, Select, useToast } from '../components/ui';
import { useAuth } from '../contexts/AuthContext';
import { useLoad } from '../hooks/useLoad';
import { useOptions } from '../hooks/useOptions';
import { inventoryApi, locationsApi, productsApi } from '../services';
import { errorMessage } from '../services/api';
import { badgeClass, fmtDateTime, fmtNum } from '../utils/format';

const TYPES = [
  { value: 'Positive', label: 'Increase (found stock)' },
  { value: 'Negative', label: 'Decrease (loss, damage)' },
  { value: 'Recount', label: 'Recount (set counted quantity)' },
];

function NewAdjustment({ onClose, onSaved }) {
  const toast = useToast();
  const products = useOptions(() => productsApi.getAll(), (p) => `${p.sku} - ${p.name}`);
  const locations = useOptions(() => locationsApi.getAll(), (l) => `${l.locationCode} (${l.zoneName})`);
  const [form, setForm] = useState({ productId: '', locationId: '', adjType: 'Negative', adjQty: '', reason: '' });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const set = (k) => (v) => setForm((f) => ({ ...f, [k]: v }));

  async function submit(e) {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      await inventoryApi.createAdjustment({ ...form, adjQty: Number(form.adjQty) });
      toast.success('Adjustment submitted for approval');
      onSaved();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal title="New stock adjustment" onClose={onClose}>
      <form onSubmit={submit}>
        <ErrorBanner message={error || products.error || locations.error} />
        <Field label="Product">{(id) => <Select id={id} value={form.productId} onChange={set('productId')} options={products.options} required />}</Field>
        <Field label="Location">{(id) => <Select id={id} value={form.locationId} onChange={set('locationId')} options={locations.options} required />}</Field>
        <div className="grid-2">
          <Field label="Adjustment type">{(id) => <Select id={id} value={form.adjType} onChange={set('adjType')} options={TYPES} required />}</Field>
          <Field label={form.adjType === 'Recount' ? 'Counted quantity' : 'Quantity'}>
            {(id) => <input id={id} className="form-input" type="number" min="0" step="any" required value={form.adjQty} onChange={(e) => set('adjQty')(e.target.value)} />}
          </Field>
        </div>
        <Field label="Reason">{(id) => <input id={id} className="form-input" value={form.reason} onChange={(e) => set('reason')(e.target.value)} placeholder="e.g. Cycle count variance" />}</Field>
        <div className="modal-footer">
          <button type="button" className="btn btn-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={saving}>Submit adjustment</button>
        </div>
      </form>
    </Modal>
  );
}

export default function Adjustments() {
  const { can } = useAuth();
  const toast = useToast();
  const { data, loading, error, reload } = useLoad(() => inventoryApi.getAdjustments(), [], { initial: [] });
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState(null);

  async function decide(id, action) {
    setBusy(id);
    try {
      await (action === 'approve' ? inventoryApi.approveAdjustment(id) : inventoryApi.rejectAdjustment(id));
      toast.success(action === 'approve' ? 'Adjustment approved and applied to stock' : 'Adjustment rejected');
      reload();
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setBusy(null);
    }
  }

  return (
    <div>
      <PageHeader title="Stock Adjustments" subtitle="Corrections change stock only after a supervisor approves them">
        {can('operate') && <button className="btn btn-primary" onClick={() => setCreating(true)}><Plus size={16} /> New adjustment</button>}
      </PageHeader>
      <div className="card">
        <DataState loading={loading} error={error} onRetry={reload} empty={!data?.length}
          emptyTitle="No adjustments yet" emptyText="Adjustments record stock corrections such as cycle-count variances.">
          <table className="data-table">
            <thead>
              <tr><th>Adjustment</th><th>Product</th><th>Location</th><th>Type</th><th>Before</th><th>Change</th><th>After</th><th>Reason</th><th>Status</th><th>Created</th>{can('manage') && <th>Actions</th>}</tr>
            </thead>
            <tbody>
              {data.map((a) => (
                <tr key={a.id}>
                  <td><strong>{a.adjNumber}</strong><div className="muted">{a.creatorName}</div></td>
                  <td>{a.productName}</td>
                  <td><span className="code-chip">{a.locationCode}</span></td>
                  <td>{a.adjType}</td>
                  <td>{fmtNum(a.qtyBefore)}</td>
                  <td>{a.adjType === 'Recount' ? `= ${fmtNum(a.adjQty)}` : `${a.adjType === 'Negative' ? '-' : '+'}${fmtNum(a.adjQty)}`}</td>
                  <td style={{ fontWeight: 600 }}>{fmtNum(a.qtyAfter)}</td>
                  <td>{a.reason || '-'}</td>
                  <td><span className={badgeClass(a.status)}>{a.status}</span></td>
                  <td>{fmtDateTime(a.createdAt)}</td>
                  {can('manage') && (
                    <td>
                      {a.status === 'Draft' && (
                        <div className="row-actions">
                          <button className="btn btn-sm btn-success" disabled={busy === a.id} onClick={() => decide(a.id, 'approve')}><Check size={13} /> Approve</button>
                          <button className="btn btn-sm btn-outline" disabled={busy === a.id} onClick={() => decide(a.id, 'reject')}><X size={13} /> Reject</button>
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
      {creating && <NewAdjustment onClose={() => setCreating(false)} onSaved={() => { setCreating(false); reload(); }} />}
    </div>
  );
}
