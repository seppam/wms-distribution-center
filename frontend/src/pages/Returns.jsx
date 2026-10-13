import { useState } from 'react';
import { Plus } from 'lucide-react';
import { DataState, ErrorBanner, Field, Modal, PageHeader, Select, useToast } from '../components/ui';
import { useAuth } from '../contexts/AuthContext';
import { useLoad } from '../hooks/useLoad';
import { enumOptions, useOptions } from '../hooks/useOptions';
import { locationsApi, outboundApi, productsApi, returnsApi, warehousesApi } from '../services';
import { errorMessage } from '../services/api';
import { badgeClass, fmtDateTime, fmtNum } from '../utils/format';

const CONDITIONS = ['Good', 'Damaged', 'Expired', 'WrongItem'];
const DISPOSITIONS = [
  { value: 'Restock', label: 'Restock (back into stock)' },
  { value: 'Refurbish', label: 'Refurbish' },
  { value: 'Dispose', label: 'Dispose' },
  { value: 'WriteOff', label: 'Write off' },
];

function NewReturn({ onClose, onSaved }) {
  const toast = useToast();
  const warehouses = useOptions(() => warehousesApi.getAll(), (w) => w.name);
  const products = useOptions(() => productsApi.getAll(), (p) => `${p.sku} - ${p.name}`);
  const orders = useLoad(() => outboundApi.getAll(), [], { initial: [] });
  const shipped = (orders.data || []).filter((o) => o.status === 'Shipped' || o.status === 'Completed');
  const [form, setForm] = useState({ warehouseId: '', productId: '', qty: '', condition: 'Good', orderId: '', reason: '' });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const set = (k) => (v) => setForm((f) => ({ ...f, [k]: v }));

  async function submit(e) {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      await returnsApi.create({ ...form, qty: Number(form.qty), orderId: form.orderId || null, reason: form.reason || null });
      toast.success('Return received. A supervisor can now decide the disposition.');
      onSaved();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal title="Receive a return" onClose={onClose}>
      <form onSubmit={submit}>
        <ErrorBanner message={error || warehouses.error || products.error || orders.error} />
        <div className="grid-2">
          <Field label="Warehouse">{(id) => <Select id={id} value={form.warehouseId} onChange={set('warehouseId')} options={warehouses.options} required />}</Field>
          <Field label="Original order (optional)">
            {(id) => <Select id={id} value={form.orderId} onChange={set('orderId')} placeholder="No order reference"
              options={shipped.map((o) => ({ value: o.id, label: `${o.orderNumber} - ${o.destination || 'no destination'}` }))} />}
          </Field>
        </div>
        <Field label="Product">{(id) => <Select id={id} value={form.productId} onChange={set('productId')} options={products.options} required />}</Field>
        <div className="grid-2">
          <Field label="Quantity">{(id) => <input id={id} className="form-input" type="number" min="0.0001" step="any" required value={form.qty} onChange={(e) => set('qty')(e.target.value)} />}</Field>
          <Field label="Condition">{(id) => <Select id={id} value={form.condition} onChange={set('condition')} options={enumOptions(CONDITIONS)} required />}</Field>
        </div>
        <Field label="Reason">{(id) => <input id={id} className="form-input" value={form.reason} onChange={(e) => set('reason')(e.target.value)} placeholder="e.g. Wrong size delivered" />}</Field>
        <div className="modal-footer">
          <button type="button" className="btn btn-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={saving}>Receive return</button>
        </div>
      </form>
    </Modal>
  );
}

function ProcessReturn({ ret, onClose, onSaved }) {
  const toast = useToast();
  const locations = useLoad(() => locationsApi.getAll(), [], { initial: [] });
  const [form, setForm] = useState({ disposition: ret.condition === 'Good' ? 'Restock' : 'Dispose', restockLocationId: '', notes: '' });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const set = (k) => (v) => setForm((f) => ({ ...f, [k]: v }));
  const restock = form.disposition === 'Restock';
  const options = (locations.data || [])
    .filter((l) => l.warehouseName === ret.warehouseName)
    .map((l) => ({ value: l.id, label: `${l.locationCode} (${l.zoneName})` }));

  async function submit(e) {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      await returnsApi.process(ret.id, { disposition: form.disposition, restockLocationId: restock ? form.restockLocationId : null, notes: form.notes || null });
      toast.success(restock ? 'Return restocked' : 'Return processed');
      onSaved();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal title={`Process ${ret.returnNo}`} onClose={onClose}>
      <form onSubmit={submit}>
        <ErrorBanner message={error || locations.error} />
        <p className="muted" style={{ marginBottom: 12 }}>{fmtNum(ret.qty)} x {ret.productName}, condition: {ret.condition}</p>
        <Field label="Disposition">{(id) => <Select id={id} value={form.disposition} onChange={set('disposition')} options={DISPOSITIONS} required />}</Field>
        {restock && (
          <Field label="Restock location" hint="Only items returned in good condition can be restocked.">
            {(id) => <Select id={id} value={form.restockLocationId} onChange={set('restockLocationId')} options={options} required />}
          </Field>
        )}
        <Field label="Notes">{(id) => <input id={id} className="form-input" value={form.notes} onChange={(e) => set('notes')(e.target.value)} />}</Field>
        <div className="modal-footer">
          <button type="button" className="btn btn-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={saving}>Process return</button>
        </div>
      </form>
    </Modal>
  );
}

export default function Returns() {
  const { can } = useAuth();
  const { data, loading, error, reload } = useLoad(() => returnsApi.getAll(), [], { initial: [] });
  const [creating, setCreating] = useState(false);
  const [processing, setProcessing] = useState(null);

  return (
    <div>
      <PageHeader title="Returns" subtitle="Receive returned goods, then restock, refurbish or dispose">
        {can('operate') && <button className="btn btn-primary" onClick={() => setCreating(true)}><Plus size={16} /> Receive return</button>}
      </PageHeader>
      <div className="card">
        <DataState loading={loading} error={error} onRetry={reload} empty={!data?.length}
          emptyTitle="No returns yet" emptyText="Returned goods appear here once they are received at the dock.">
          <table className="data-table">
            <thead><tr><th>Return</th><th>Product</th><th>Qty</th><th>Order</th><th>Condition</th><th>Disposition</th><th>Status</th><th>Received</th>{can('manage') && <th>Actions</th>}</tr></thead>
            <tbody>
              {data.map((r) => (
                <tr key={r.id}>
                  <td><strong>{r.returnNo}</strong><div className="muted">{r.receiverName}</div></td>
                  <td>{r.productName}<div className="muted">{r.productSku}</div></td>
                  <td style={{ fontWeight: 600 }}>{fmtNum(r.qty)}</td>
                  <td>{r.orderNumber || '-'}</td>
                  <td>{r.condition}</td>
                  <td>{r.disposition || '-'}{r.restockLocationCode ? ` (${r.restockLocationCode})` : ''}</td>
                  <td><span className={badgeClass(r.status)}>{r.status}</span></td>
                  <td>{fmtDateTime(r.receivedAt)}</td>
                  {can('manage') && (
                    <td>{r.status === 'Received' && <button className="btn btn-sm btn-primary" onClick={() => setProcessing(r)}>Process</button>}</td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </DataState>
      </div>
      {creating && <NewReturn onClose={() => setCreating(false)} onSaved={() => { setCreating(false); reload(); }} />}
      {processing && <ProcessReturn ret={processing} onClose={() => setProcessing(null)} onSaved={() => { setProcessing(null); reload(); }} />}
    </div>
  );
}
