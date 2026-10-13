import { useState } from 'react';
import { DataState, PageHeader, useToast } from '../components/ui';
import { useAuth } from '../contexts/AuthContext';
import { useLoad } from '../hooks/useLoad';
import { shipmentsApi } from '../services';
import { errorMessage } from '../services/api';
import { badgeClass, fmtDateTime } from '../utils/format';

export default function Shipments() {
  const { can } = useAuth();
  const toast = useToast();
  const { data, loading, error, reload } = useLoad(() => shipmentsApi.getAll(), [], { initial: [] });
  const [busy, setBusy] = useState(null);

  async function deliver(id) {
    setBusy(id);
    try {
      await shipmentsApi.deliver(id);
      toast.success('Shipment marked as delivered');
      reload();
    } catch (err) {
      toast.error(errorMessage(err));
    } finally {
      setBusy(null);
    }
  }

  return (
    <div>
      <PageHeader title="Shipments" subtitle="Dispatched orders and their carriers; ship orders from the Orders page" />
      <div className="card">
        <DataState loading={loading} error={error} onRetry={reload} empty={!data?.length}
          emptyTitle="No shipments yet" emptyText="Shipments are created when a packed order is shipped.">
          <table className="data-table">
            <thead><tr><th>Order</th><th>Carrier</th><th>Tracking</th><th>Vehicle</th><th>Driver</th><th>Status</th><th>Shipped</th><th>Delivered</th>{can('operate') && <th>Actions</th>}</tr></thead>
            <tbody>
              {data.map((s) => (
                <tr key={s.id}>
                  <td><strong>{s.orderNumber}</strong></td>
                  <td>{s.carrierName}</td>
                  <td>{s.trackingNo || '-'}{s.bolNumber ? <div className="muted">BOL {s.bolNumber}</div> : null}</td>
                  <td>{s.vehiclePlate || '-'}</td>
                  <td>{s.driverName || '-'}</td>
                  <td><span className={badgeClass(s.status)}>{s.status}</span></td>
                  <td>{fmtDateTime(s.shippedAt)}</td>
                  <td>{fmtDateTime(s.deliveredAt)}</td>
                  {can('operate') && (
                    <td>{s.status !== 'Delivered' && <button className="btn btn-sm btn-success" disabled={busy === s.id} onClick={() => deliver(s.id)}>Mark delivered</button>}</td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </DataState>
      </div>
    </div>
  );
}
