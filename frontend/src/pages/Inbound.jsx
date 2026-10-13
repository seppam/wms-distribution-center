import { useState } from 'react';
import { Eye, Plus, Trash2 } from 'lucide-react';
import { Confirm, DataState, ErrorBanner, Field, Modal, PageHeader, Select, useToast } from '../components/ui';
import { useAuth } from '../contexts/AuthContext';
import { useLoad } from '../hooks/useLoad';
import { useOptions } from '../hooks/useOptions';
import { inboundApi, locationsApi, productsApi, suppliersApi, warehousesApi } from '../services';
import { errorMessage } from '../services/api';
import { badgeClass, fmtDate, fmtDateTime, fmtNum } from '../utils/format';

const emptyLine = () => ({ key: Math.random(), productId: '', expectedQty: '', unitCost: '', lotNumber: '', expiryDate: '' });

function NewReceipt({ onClose, onSaved }) {
  const toast = useToast();
  const suppliers = useOptions(() => suppliersApi.getAll(), (s) => s.name);
  const warehouses = useOptions(() => warehousesApi.getAll(), (w) => w.name);
  const products = useOptions(() => productsApi.getAll(), (p) => `${p.sku} - ${p.name}`);
  const [form, setForm] = useState({ supplierId: '', warehouseId: '', poNumber: '', expectedDate: '', notes: '' });
  const [lines, setLines] = useState([emptyLine()]);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const setF = (k) => (v) => setForm((f) => ({ ...f, [k]: v }));
  const setLine = (key, k, v) => setLines((ls) => ls.map((l) => (l.key === key ? { ...l, [k]: v } : l)));

  async function submit(e) {
    e.preventDefault();
    setError('');
    const filled = lines.filter((l) => l.productId);
    if (filled.length === 0) return setError('Add at least one product line.');
    setSaving(true);
    try {
      await inboundApi.create({
        supplierId: form.supplierId, warehouseId: form.warehouseId,
        poNumber: form.poNumber || null, expectedDate: form.expectedDate || null, notes: form.notes || null,
        items: filled.map((l) => ({
          productId: l.productId, expectedQty: Number(l.expectedQty), unitCost: Number(l.unitCost || 0),
          lotNumber: l.lotNumber || null, expiryDate: l.expiryDate || null,
        })),
      });
      toast.success('Receipt created');
      onSaved();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal title="New inbound receipt" onClose={onClose} wide>
      <form onSubmit={submit}>
        <ErrorBanner message={error || suppliers.error || warehouses.error || products.error} />
        <div className="grid-2">
          <Field label="Supplier">{(id) => <Select id={id} value={form.supplierId} onChange={setF('supplierId')} options={suppliers.options} required />}</Field>
          <Field label="Warehouse">{(id) => <Select id={id} value={form.warehouseId} onChange={setF('warehouseId')} options={warehouses.options} required />}</Field>
          <Field label="PO number">{(id) => <input id={id} className="form-input" value={form.poNumber} onChange={(e) => setF('poNumber')(e.target.value)} />}</Field>
          <Field label="Expected date">{(id) => <input id={id} type="date" className="form-input" value={form.expectedDate} onChange={(e) => setF('expectedDate')(e.target.value)} />}</Field>
        </div>
        <div className="section-title">Expected lines</div>
        {lines.map((l, i) => (
          <div className="line-row" key={l.key}>
            <Select value={l.productId} onChange={(v) => setLine(l.key, 'productId', v)} options={products.options} placeholder="Product" />
            <input className="form-input" type="number" min="0.0001" step="any" placeholder="Qty" aria-label={`Expected quantity line ${i + 1}`} value={l.expectedQty}
              required={!!l.productId} onChange={(e) => setLine(l.key, 'expectedQty', e.target.value)} />
            <input className="form-input" type="number" min="0" step="any" placeholder="Unit cost" aria-label={`Unit cost line ${i + 1}`} value={l.unitCost} onChange={(e) => setLine(l.key, 'unitCost', e.target.value)} />
            <input className="form-input" placeholder="Lot #" aria-label={`Lot line ${i + 1}`} value={l.lotNumber} onChange={(e) => setLine(l.key, 'lotNumber', e.target.value)} />
            <input className="form-input" type="date" aria-label={`Expiry line ${i + 1}`} value={l.expiryDate} onChange={(e) => setLine(l.key, 'expiryDate', e.target.value)} />
            <button type="button" className="btn btn-sm btn-outline" aria-label={`Remove line ${i + 1}`} disabled={lines.length === 1} onClick={() => setLines((ls) => ls.filter((x) => x.key !== l.key))}><Trash2 size={13} /></button>
          </div>
        ))}
        <button type="button" className="btn btn-sm btn-outline" onClick={() => setLines((ls) => [...ls, emptyLine()])}><Plus size={13} /> Add line</button>
        <div className="modal-footer">
          <button type="button" className="btn btn-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={saving}>Create receipt</button>
        </div>
      </form>
    </Modal>
  );
}

function ReceiveLine({ receipt, line, onClose, onSaved }) {
  const toast = useToast();
  const locations = useLoad(() => locationsApi.getAll(), [], { initial: [] });
  const options = (locations.data || [])
    .filter((l) => l.warehouseName === receipt.warehouseName && l.status !== 'Locked')
    .map((l) => ({ value: l.id, label: `${l.locationCode} (${l.zoneName}, ${l.status})` }));
  const [form, setForm] = useState({
    receivedQty: line.receivedQty || line.expectedQty, damagedQty: line.damagedQty || 0,
    putawayLocationId: line.putawayLocationId || '', lotNumber: line.lotNumber || '', expiryDate: line.expiryDate || '',
  });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const set = (k) => (v) => setForm((f) => ({ ...f, [k]: v }));

  async function submit(e) {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      await inboundApi.receive(receipt.id, {
        receiptItemId: line.id, receivedQty: Number(form.receivedQty), damagedQty: Number(form.damagedQty || 0),
        putawayLocationId: form.putawayLocationId, lotNumber: form.lotNumber || null, expiryDate: form.expiryDate || null,
      });
      toast.success('Goods received and put away');
      onSaved();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal title={`Receive ${line.productName}`} onClose={onClose}>
      <form onSubmit={submit}>
        <ErrorBanner message={error || locations.error} />
        <p className="muted" style={{ marginBottom: 12 }}>Expected {fmtNum(line.expectedQty)}. Damaged goods are recorded but not stocked.</p>
        <div className="grid-2">
          <Field label="Received quantity">{(id) => <input id={id} className="form-input" type="number" min="0" step="any" required value={form.receivedQty} onChange={(e) => set('receivedQty')(e.target.value)} />}</Field>
          <Field label="of which damaged">{(id) => <input id={id} className="form-input" type="number" min="0" step="any" value={form.damagedQty} onChange={(e) => set('damagedQty')(e.target.value)} />}</Field>
        </div>
        <Field label="Put-away location" hint="Good stock is placed here in the same step.">
          {(id) => <Select id={id} value={form.putawayLocationId} onChange={set('putawayLocationId')} options={options} required />}
        </Field>
        <div className="grid-2">
          <Field label="Lot #">{(id) => <input id={id} className="form-input" value={form.lotNumber} onChange={(e) => set('lotNumber')(e.target.value)} />}</Field>
          <Field label="Expiry date">{(id) => <input id={id} type="date" className="form-input" value={form.expiryDate} onChange={(e) => set('expiryDate')(e.target.value)} />}</Field>
        </div>
        <div className="modal-footer">
          <button type="button" className="btn btn-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={saving}>Receive and put away</button>
        </div>
      </form>
    </Modal>
  );
}

function ReceiptDetail({ id, onClose, onChanged }) {
  const { can } = useAuth();
  const toast = useToast();
  const { data: receipt, loading, error, reload } = useLoad(() => inboundApi.getById(id), [id]);
  const [receiving, setReceiving] = useState(null);
  const [busy, setBusy] = useState(false);
  const open = receipt && (receipt.status === 'Draft' || receipt.status === 'InProgress');

  async function complete() {
    setBusy(true);
    try {
      const res = await inboundApi.complete(id);
      toast.success(`Receipt completed as ${res.data.status}`);
      await reload();
      onChanged();
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal title={receipt ? `Receipt ${receipt.receiptNo}` : 'Receipt'} onClose={onClose} wide>
      <DataState loading={loading} error={error} onRetry={reload}>
        {receipt && (
          <>
            <dl className="kv">
              <dt>Status</dt><dd><span className={badgeClass(receipt.status)}>{receipt.status}</span></dd>
              <dt>Supplier</dt><dd>{receipt.supplierName}</dd>
              <dt>Warehouse</dt><dd>{receipt.warehouseName}</dd>
              <dt>PO number</dt><dd>{receipt.poNumber || '-'}</dd>
              <dt>Expected</dt><dd>{fmtDate(receipt.expectedDate)}</dd>
              {receipt.receivedDate && (<><dt>Last received</dt><dd>{fmtDateTime(receipt.receivedDate)}{receipt.receiverName ? ` by ${receipt.receiverName}` : ''}</dd></>)}
            </dl>
            <div className="section-title">Lines</div>
            <table className="data-table">
              <thead><tr><th>Product</th><th>Expected</th><th>Received</th><th>Damaged</th><th>Put-away</th><th>Lot</th><th>Status</th>{can('operate') && <th />}</tr></thead>
              <tbody>
                {receipt.items.map((l) => (
                  <tr key={l.id}>
                    <td>{l.productName}<div className="muted">{l.productSku}</div></td>
                    <td>{fmtNum(l.expectedQty)}</td>
                    <td style={{ fontWeight: 600 }}>{fmtNum(l.receivedQty)}</td>
                    <td>{fmtNum(l.damagedQty)}</td>
                    <td>{l.putawayLocationCode ? <span className="code-chip">{l.putawayLocationCode}</span> : '-'}</td>
                    <td>{l.lotNumber || '-'}</td>
                    <td><span className={badgeClass(l.status)}>{l.status}</span></td>
                    {can('operate') && (
                      <td>{open && <button className="btn btn-sm btn-primary" onClick={() => setReceiving(l)}>{l.status === 'Pending' ? 'Receive' : 'Update'}</button>}</td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={onClose}>Close</button>
              {can('operate') && receipt.status === 'InProgress' && (
                <button className="btn btn-success" disabled={busy} onClick={complete}>Complete receipt</button>
              )}
            </div>
            {receiving && (
              <ReceiveLine receipt={receipt} line={receiving} onClose={() => setReceiving(null)}
                onSaved={() => { setReceiving(null); reload(); onChanged(); }} />
            )}
          </>
        )}
      </DataState>
    </Modal>
  );
}

export default function Inbound() {
  const { can } = useAuth();
  const toast = useToast();
  const { data, loading, error, reload } = useLoad(() => inboundApi.getAll(), [], { initial: [] });
  const [creating, setCreating] = useState(false);
  const [openId, setOpenId] = useState(null);
  const [toDelete, setToDelete] = useState(null);

  async function remove() {
    try {
      await inboundApi.delete(toDelete.id);
      toast.success('Draft receipt deleted');
      reload();
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setToDelete(null);
    }
  }

  return (
    <div>
      <PageHeader title="Inbound / Receiving" subtitle="Receive goods from suppliers: plan, receive, put away, complete">
        {can('manage') && <button className="btn btn-primary" onClick={() => setCreating(true)}><Plus size={16} /> New receipt</button>}
      </PageHeader>
      <div className="card">
        <DataState loading={loading} error={error} onRetry={reload} empty={!data?.length}
          emptyTitle="No receipts yet" emptyText={can('manage') ? 'Create a receipt for an expected supplier delivery.' : 'Receipts appear here once a supervisor plans a delivery.'}>
          <table className="data-table">
            <thead><tr><th>Receipt #</th><th>Supplier</th><th>PO number</th><th>Warehouse</th><th>Status</th><th>Expected</th><th>Actions</th></tr></thead>
            <tbody>
              {data.map((r) => (
                <tr key={r.id}>
                  <td><strong>{r.receiptNo}</strong></td>
                  <td>{r.supplierName}</td>
                  <td>{r.poNumber || '-'}</td>
                  <td>{r.warehouseName}</td>
                  <td><span className={badgeClass(r.status)}>{r.status}</span></td>
                  <td>{fmtDate(r.expectedDate)}</td>
                  <td>
                    <div className="row-actions">
                      <button className="btn btn-sm btn-outline" onClick={() => setOpenId(r.id)}><Eye size={13} /> Open</button>
                      {can('manage') && r.status === 'Draft' && (
                        <button className="btn btn-sm btn-danger" aria-label={`Delete ${r.receiptNo}`} onClick={() => setToDelete(r)}><Trash2 size={13} /></button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </DataState>
      </div>
      {creating && <NewReceipt onClose={() => setCreating(false)} onSaved={() => { setCreating(false); reload(); }} />}
      {openId && <ReceiptDetail id={openId} onClose={() => setOpenId(null)} onChanged={reload} />}
      {toDelete && <Confirm title="Delete receipt" message={`Delete draft receipt ${toDelete.receiptNo}?`} confirmLabel="Delete" danger onConfirm={remove} onCancel={() => setToDelete(null)} />}
    </div>
  );
}
