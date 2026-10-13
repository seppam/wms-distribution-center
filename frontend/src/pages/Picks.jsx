import { useState } from 'react';
import { Check } from 'lucide-react';
import { DataState, PageHeader, useToast } from '../components/ui';
import { useAuth } from '../contexts/AuthContext';
import { useLoad } from '../hooks/useLoad';
import { picksApi } from '../services';
import { errorMessage } from '../services/api';
import { badgeClass, fmtNum } from '../utils/format';

const FILTERS = [
  { key: 'open', label: 'Open', params: {} },
  { key: 'mine', label: 'Assigned to me', params: { mine: true } },
  { key: 'done', label: 'Picked', params: { status: 'Picked' } },
];

export default function Picks() {
  const { can } = useAuth();
  const toast = useToast();
  const [filter, setFilter] = useState('open');
  const params = FILTERS.find((f) => f.key === filter).params;
  const { data, loading, error, reload } = useLoad(() => picksApi.getAll(params), [filter], { initial: [] });
  const [qty, setQty] = useState({});
  const [busy, setBusy] = useState(null);

  const rows = filter === 'open' ? (data || []).filter((t) => t.status === 'Assigned' || t.status === 'InProgress') : (data || []);

  async function confirm(task) {
    const value = qty[task.id] ?? task.qtyToPick;
    setBusy(task.id);
    try {
      await picksApi.confirm(task.id, Number(value));
      toast.success(Number(value) < task.qtyToPick ? 'Short pick recorded; the missing reservation was released' : 'Pick confirmed');
      reload();
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setBusy(null);
    }
  }

  return (
    <div>
      <PageHeader title="Pick Tasks" subtitle="Pick the quantities from the listed bins. Picking less than requested records a short pick." />
      <div className="tabs" role="tablist">
        {FILTERS.map((f) => (
          <button key={f.key} role="tab" aria-selected={filter === f.key} className={`tab ${filter === f.key ? 'active' : ''}`} onClick={() => setFilter(f.key)}>{f.label}</button>
        ))}
      </div>
      <div className="card">
        <DataState loading={loading} error={error} onRetry={reload} empty={rows.length === 0}
          emptyTitle={filter === 'done' ? 'Nothing picked yet' : 'No open pick tasks'} emptyText="Allocate stock on an order to create pick tasks.">
          <table className="data-table">
            <thead><tr><th>Order</th><th>Product</th><th>From bin</th><th>To pick</th><th>Picked</th><th>Picker</th><th>Status</th>{can('operate') && <th>Confirm</th>}</tr></thead>
            <tbody>
              {rows.map((t) => {
                const open = t.status === 'Assigned' || t.status === 'InProgress';
                return (
                  <tr key={t.id}>
                    <td><strong>{t.orderNumber}</strong></td>
                    <td>{t.productName}<div className="muted">{t.productSku}</div></td>
                    <td><span className="code-chip">{t.fromLocationCode}</span></td>
                    <td style={{ fontWeight: 600 }}>{fmtNum(t.qtyToPick)}</td>
                    <td>{fmtNum(t.qtyPicked)}</td>
                    <td>{t.pickerName || '-'}</td>
                    <td><span className={badgeClass(t.status)}>{t.status}</span></td>
                    {can('operate') && (
                      <td>
                        {open && (
                          <div className="row-actions">
                            <input className="qty-input" type="number" min="0" max={t.qtyToPick} step="any" aria-label={`Quantity picked for ${t.productSku} at ${t.fromLocationCode}`}
                              value={qty[t.id] ?? t.qtyToPick} onChange={(e) => setQty((q) => ({ ...q, [t.id]: e.target.value }))} />
                            <button className="btn btn-sm btn-success" disabled={busy === t.id} onClick={() => confirm(t)}><Check size={13} /> Confirm</button>
                          </div>
                        )}
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </DataState>
      </div>
    </div>
  );
}
