import { useState } from 'react';
import { Eye, Plus, Trash2 } from 'lucide-react';
import { Confirm, DataState, ErrorBanner, Field, Modal, PageHeader, Select, useToast } from '../components/ui';
import { useAuth } from '../contexts/AuthContext';
import { useLoad } from '../hooks/useLoad';
import { enumOptions, useOptions } from '../hooks/useOptions';
import { carriersApi, inventoryApi, outboundApi, picksApi, productsApi, usersApi, warehousesApi } from '../services';
import { errorMessage } from '../services/api';
import { badgeClass, fmtDate, fmtDateTime, fmtNum } from '../utils/format';

const STEPS = ['Draft', 'Picking', 'Picked', 'Packed', 'Shipped', 'Completed'];
const emptyLine = () => ({ key: Math.random(), productId: '', orderedQty: '', unitPrice: '' });

function NewOrder({ onClose, onSaved }) {
  const toast = useToast();
  const warehouses = useOptions(() => warehousesApi.getAll(), (w) => w.name);
  const products = useOptions(() => productsApi.getAll(), (p) => `${p.sku} - ${p.name}`);
  const stock = useLoad(() => inventoryApi.query(), [], { initial: [] });
  const [form, setForm] = useState({ warehouseId: '', priority: 'Normal', destination: '', dueDate: '', notes: '' });
  const [lines, setLines] = useState([emptyLine()]);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const setF = (k) => (v) => setForm((f) => ({ ...f, [k]: v }));
  const setLine = (key, k, v) => setLines((ls) => ls.map((l) => (l.key === key ? { ...l, [k]: v } : l)));
  const availableFor = (productId) => (stock.data || [])
    .filter((s) => s.productId === productId && s.status === 'Available')
    .reduce((sum, s) => sum + s.qtyOnHand - s.qtyReserved, 0);

  async function submit(e) {
    e.preventDefault();
    setError('');
    const filled = lines.filter((l) => l.productId);
    if (filled.length === 0) return setError('Add at least one product line.');
    setSaving(true);
    try {
      await outboundApi.create({
        warehouseId: form.warehouseId, orderType: 'Sales', priority: form.priority,
        destination: form.destination || null, dueDate: form.dueDate || null, notes: form.notes || null,
        items: filled.map((l) => ({ productId: l.productId, orderedQty: Number(l.orderedQty), unitPrice: Number(l.unitPrice || 0) })),
      });
      toast.success('Order created as a draft');
      onSaved();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal title="New outbound order" onClose={onClose} wide>
      <form onSubmit={submit}>
        <ErrorBanner message={error || warehouses.error || products.error || stock.error} />
        <div className="grid-2">
          <Field label="Warehouse">{(id) => <Select id={id} value={form.warehouseId} onChange={setF('warehouseId')} options={warehouses.options} required />}</Field>
          <Field label="Priority">{(id) => <Select id={id} value={form.priority} onChange={setF('priority')} options={enumOptions(['Normal', 'Urgent', 'Rush'])} required />}</Field>
          <Field label="Destination">{(id) => <input id={id} className="form-input" value={form.destination} onChange={(e) => setF('destination')(e.target.value)} placeholder="e.g. Store 12" />}</Field>
          <Field label="Due date">{(id) => <input id={id} type="date" className="form-input" value={form.dueDate} onChange={(e) => setF('dueDate')(e.target.value)} />}</Field>
        </div>
        <div className="section-title">Order lines</div>
        {lines.map((l, i) => {
          const avail = l.productId ? availableFor(l.productId) : null;
          return (
            <div key={l.key}>
              <div className="line-row simple">
                <Select value={l.productId} onChange={(v) => setLine(l.key, 'productId', v)} options={products.options} placeholder="Product" />
                <input className="form-input" type="number" min="0.0001" step="any" placeholder="Qty" aria-label={`Quantity line ${i + 1}`} value={l.orderedQty}
                  required={!!l.productId} onChange={(e) => setLine(l.key, 'orderedQty', e.target.value)} />
                <input className="form-input" type="number" min="0" step="any" placeholder="Unit price" aria-label={`Unit price line ${i + 1}`} value={l.unitPrice} onChange={(e) => setLine(l.key, 'unitPrice', e.target.value)} />
                <button type="button" className="btn btn-sm btn-outline" aria-label={`Remove line ${i + 1}`} disabled={lines.length === 1} onClick={() => setLines((ls) => ls.filter((x) => x.key !== l.key))}><Trash2 size={13} /></button>
              </div>
              {avail != null && (
                <div className="muted" style={{ margin: '-4px 0 8px' }}>
                  Available now: {fmtNum(avail)}{Number(l.orderedQty) > avail ? ' (not enough stock: allocation will be rejected)' : ''}
                </div>
              )}
            </div>
          );
        })}
        <button type="button" className="btn btn-sm btn-outline" onClick={() => setLines((ls) => [...ls, emptyLine()])}><Plus size={13} /> Add line</button>
        <div className="modal-footer">
          <button type="button" className="btn btn-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={saving}>Create order</button>
        </div>
      </form>
    </Modal>
  );
}

function PackForm({ order, onClose, onSaved }) {
  const toast = useToast();
  const [form, setForm] = useState({ cartonNo: `CTN-${order.orderNumber.slice(-4)}`, weightKg: '' });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  async function submit(e) {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      await outboundApi.pack(order.id, { cartonNo: form.cartonNo, weightKg: Number(form.weightKg || 0) });
      toast.success('Order packed');
      onSaved();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }
  return (
    <Modal title="Pack order" onClose={onClose}>
      <form onSubmit={submit}>
        <ErrorBanner message={error} />
        <Field label="Carton number">{(id) => <input id={id} className="form-input" required value={form.cartonNo} onChange={(e) => setForm({ ...form, cartonNo: e.target.value })} />}</Field>
        <Field label="Weight (kg)">{(id) => <input id={id} className="form-input" type="number" min="0" step="any" required value={form.weightKg} onChange={(e) => setForm({ ...form, weightKg: e.target.value })} />}</Field>
        <div className="modal-footer">
          <button type="button" className="btn btn-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn btn-primary" disabled={saving}>Pack</button>
        </div>
      </form>
    </Modal>
  );
}

function ShipForm({ order, onClose, onSaved }) {
  const toast = useToast();
  const carriers = useOptions(() => carriersApi.getAll(), (c) => c.name);
  const [form, setForm] = useState({ carrierId: '', trackingNo: '', bolNumber: '', vehiclePlate: '', driverName: '' });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const set = (k) => (e) => setForm((f) => ({ ...f, [k]: e.target.value }));
  async function submit(e) {
    e.preventDefault();
    setSaving(true);
    setError('');
    try {
      await outboundApi.ship(order.id, {
        carrierId: form.carrierId, trackingNo: form.trackingNo || null, bolNumber: form.bolNumber || null,
        vehiclePlate: form.vehiclePlate || null, driverName: form.driverName || null,
      });
      toast.success('Order shipped: stock deducted');
      onSaved();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }
  return (
    <Modal title="Ship order" onClose={onClose}>
      <form onSubmit={submit}>
        <ErrorBanner message={error || carriers.error} />
        <Field label="Carrier">{(id) => <Select id={id} value={form.carrierId} onChange={(v) => setForm((f) => ({ ...f, carrierId: v }))} options={carriers.options} required />}</Field>
        <div className="grid-2">
          <Field label="Tracking number">{(id) => <input id={id} className="form-input" value={form.trackingNo} onChange={set('trackingNo')} />}</Field>
          <Field label="Bill of lading">{(id) => <input id={id} className="form-input" value={form.bolNumber} onChange={set('bolNumber')} />}</Field>
          <Field label="Vehicle plate">{(id) => <input id={id} className="form-input" value={form.vehiclePlate} onChange={set('vehiclePlate')} />}</Field>
          <Field label="Driver">{(id) => <input id={id} className="form-input" value={form.driverName} onChange={set('driverName')} />}</Field>
        </div>
        <div className="modal-footer">
          <button type="button" className="btn btn-secondary" onClick={onClose}>Cancel</button>
          <button type="submit" className="btn btn-success" disabled={saving}>Ship order</button>
        </div>
      </form>
    </Modal>
  );
}

function OrderDetail({ id, onClose, onChanged }) {
  const { can } = useAuth();
  const toast = useToast();
  const { data: order, loading, error, reload } = useLoad(() => outboundApi.getById(id), [id]);
  const tasks = useLoad(() => picksApi.getAll({ orderId: id }), [id], { initial: [] });
  const pickers = useLoad(() => (can('manage') ? usersApi.getAll() : Promise.resolve({ data: [] })), [id], { initial: [] });
  const [pickerId, setPickerId] = useState('');
  const [dialog, setDialog] = useState(null); // 'pack' | 'ship' | 'cancel'
  const [busy, setBusy] = useState(false);

  const refresh = async () => { await Promise.all([reload(), tasks.reload()]); onChanged(); };

  async function run(label, fn) {
    setBusy(true);
    try {
      await fn();
      toast.success(label);
      await refresh();
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setBusy(false);
      setDialog(null);
    }
  }

  const status = order?.status;
  const floorUsers = (pickers.data || []).filter((u) => u.isActive && ['Admin', 'Supervisor', 'Operator'].includes(u.roleName));
  const shipment = order?.shipments?.[0];

  return (
    <Modal title={order ? `Order ${order.orderNumber}` : 'Order'} onClose={onClose} wide>
      <DataState loading={loading} error={error} onRetry={reload}>
        {order && (
          <>
            <div className="steps" aria-label="Order progress">
              {status === 'Cancelled'
                ? <span className="step cancelled">Cancelled</span>
                : STEPS.map((s, i) => (
                  <span key={s} className={`step ${s === status ? 'current' : STEPS.indexOf(status) > i ? 'done' : ''}`}>{s}</span>
                ))}
            </div>
            <dl className="kv">
              <dt>Destination</dt><dd>{order.destination || '-'}</dd>
              <dt>Priority</dt><dd><span className={badgeClass(order.priority)}>{order.priority}</span></dd>
              <dt>Warehouse</dt><dd>{order.warehouseName}</dd>
              <dt>Due</dt><dd>{fmtDate(order.dueDate)}</dd>
              <dt>Created by</dt><dd>{order.creatorName} on {fmtDateTime(order.createdAt)}</dd>
            </dl>

            <div className="section-title">Lines</div>
            <table className="data-table">
              <thead><tr><th>Product</th><th>Ordered</th><th>Picked</th><th>Shipped</th><th>Unit price</th><th>Status</th></tr></thead>
              <tbody>
                {order.items.map((l) => (
                  <tr key={l.id}>
                    <td>{l.productName}<div className="muted">{l.productSku}</div></td>
                    <td>{fmtNum(l.orderedQty)}</td><td>{fmtNum(l.pickedQty)}</td><td>{fmtNum(l.shippedQty)}</td>
                    <td>{fmtNum(l.unitPrice)}</td>
                    <td><span className={badgeClass(l.status)}>{l.status}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>

            {(tasks.data || []).length > 0 && (
              <>
                <div className="section-title">Pick tasks</div>
                <table className="data-table">
                  <thead><tr><th>Product</th><th>Bin</th><th>To pick</th><th>Picked</th><th>Picker</th><th>Status</th></tr></thead>
                  <tbody>
                    {tasks.data.map((t) => (
                      <tr key={t.id}>
                        <td>{t.productSku}</td><td><span className="code-chip">{t.fromLocationCode}</span></td>
                        <td>{fmtNum(t.qtyToPick)}</td><td>{fmtNum(t.qtyPicked)}</td><td>{t.pickerName || '-'}</td>
                        <td><span className={badgeClass(t.status)}>{t.status}</span></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </>
            )}

            {order.packLists?.length > 0 && (
              <p className="muted" style={{ marginTop: 12 }}>
                Cartons: {order.packLists.map((p) => `${p.cartonNo} (${fmtNum(p.weightKg)} kg)`).join(', ')}
              </p>
            )}
            {shipment && (
              <p className="muted" style={{ marginTop: 6 }}>
                Shipment via {shipment.carrierName}, status {shipment.status}{shipment.trackingNo ? `, tracking ${shipment.trackingNo}` : ''}
              </p>
            )}

            {status === 'Picking' && can('manage') && floorUsers.length > 0 && (
              <div className="toolbar" style={{ marginTop: 16 }}>
                <div style={{ minWidth: 220 }}>
                  <Select value={pickerId} onChange={setPickerId} placeholder="Assign picker..." options={floorUsers.map((u) => ({ value: u.id, label: `${u.fullName} (${u.roleName})` }))} />
                </div>
                <button className="btn btn-sm btn-outline" disabled={!pickerId || busy}
                  onClick={() => run('Picker assigned', () => outboundApi.assignPicker(id, pickerId))}>Assign</button>
              </div>
            )}
            {status === 'Picking' && (
              <p className="muted" style={{ marginTop: 10 }}>Confirm the picks on the Pick Tasks page; the order moves to Picked when all tasks are done.</p>
            )}

            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={onClose}>Close</button>
              {can('manage') && !['Shipped', 'Completed', 'Cancelled'].includes(status) && (
                <button className="btn btn-outline" disabled={busy} onClick={() => setDialog('cancel')}>Cancel order</button>
              )}
              {can('manage') && status === 'Draft' && (
                <button className="btn btn-primary" disabled={busy}
                  onClick={() => run('Stock allocated: pick tasks created', () => outboundApi.startPicking(id))}>Allocate stock and start picking</button>
              )}
              {can('operate') && status === 'Picked' && <button className="btn btn-primary" onClick={() => setDialog('pack')}>Pack</button>}
              {can('operate') && status === 'Packed' && <button className="btn btn-success" onClick={() => setDialog('ship')}>Ship</button>}
            </div>

            {dialog === 'pack' && <PackForm order={order} onClose={() => setDialog(null)} onSaved={() => { setDialog(null); refresh(); }} />}
            {dialog === 'ship' && <ShipForm order={order} onClose={() => setDialog(null)} onSaved={() => { setDialog(null); refresh(); }} />}
            {dialog === 'cancel' && (
              <Confirm title="Cancel order" message={`Cancel ${order.orderNumber}? Reserved stock is released.`} confirmLabel="Cancel order" danger busy={busy}
                onConfirm={() => run('Order cancelled', () => outboundApi.cancel(id))} onCancel={() => setDialog(null)} />
            )}
          </>
        )}
      </DataState>
    </Modal>
  );
}

export default function Outbound() {
  const { can } = useAuth();
  const toast = useToast();
  const { data, loading, error, reload } = useLoad(() => outboundApi.getAll(), [], { initial: [] });
  const [creating, setCreating] = useState(false);
  const [openId, setOpenId] = useState(null);
  const [toDelete, setToDelete] = useState(null);
  const [status, setStatus] = useState('');

  const rows = (data || []).filter((o) => !status || o.status === status);

  async function remove() {
    try {
      await outboundApi.delete(toDelete.id);
      toast.success('Draft order deleted');
      reload();
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setToDelete(null);
    }
  }

  return (
    <div>
      <PageHeader title="Outbound / Orders" subtitle="Sales orders: allocate, pick, pack, ship">
        {can('manage') && <button className="btn btn-primary" onClick={() => setCreating(true)}><Plus size={16} /> New order</button>}
      </PageHeader>
      <div className="toolbar">
        <div style={{ width: 200 }}>
          <Select value={status} onChange={setStatus} placeholder="All statuses" options={enumOptions(['Draft', 'Picking', 'Picked', 'Packed', 'Shipped', 'Completed', 'Cancelled'])} />
        </div>
      </div>
      <div className="card">
        <DataState loading={loading} error={error} onRetry={reload} empty={rows.length === 0}
          emptyTitle={status ? 'No orders with this status' : 'No orders yet'} emptyText={can('manage') && !status ? 'Create an order to start the pick, pack and ship flow.' : undefined}>
          <table className="data-table">
            <thead><tr><th>Order #</th><th>Destination</th><th>Priority</th><th>Due</th><th>Status</th><th>Actions</th></tr></thead>
            <tbody>
              {rows.map((o) => (
                <tr key={o.id}>
                  <td><strong>{o.orderNumber}</strong></td>
                  <td>{o.destination || '-'}</td>
                  <td><span className={badgeClass(o.priority)}>{o.priority}</span></td>
                  <td>{fmtDate(o.dueDate)}</td>
                  <td><span className={badgeClass(o.status)}>{o.status}</span></td>
                  <td>
                    <div className="row-actions">
                      <button className="btn btn-sm btn-outline" onClick={() => setOpenId(o.id)}><Eye size={13} /> Open</button>
                      {can('manage') && o.status === 'Draft' && (
                        <button className="btn btn-sm btn-danger" aria-label={`Delete ${o.orderNumber}`} onClick={() => setToDelete(o)}><Trash2 size={13} /></button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </DataState>
      </div>
      {creating && <NewOrder onClose={() => setCreating(false)} onSaved={() => { setCreating(false); reload(); }} />}
      {openId && <OrderDetail id={openId} onClose={() => setOpenId(null)} onChanged={reload} />}
      {toDelete && <Confirm title="Delete order" message={`Delete draft order ${toDelete.orderNumber}?`} confirmLabel="Delete" danger onConfirm={remove} onCancel={() => setToDelete(null)} />}
    </div>
  );
}
