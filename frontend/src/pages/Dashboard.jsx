import { Link } from 'react-router-dom';
import { DataState, ErrorBanner, PageHeader } from '../components/ui';
import { useAuth } from '../contexts/AuthContext';
import { useLoad } from '../hooks/useLoad';
import { inboundApi, outboundApi, reportsApi } from '../services';
import { badgeClass, fmtDate, fmtNum } from '../utils/format';

export default function Dashboard() {
  const { user } = useAuth();
  const stats = useLoad(() => reportsApi.getDashboard());
  const receipts = useLoad(() => inboundApi.getAll(), [], { initial: [] });
  const orders = useLoad(() => outboundApi.getAll(), [], { initial: [] });
  const s = stats.data;

  const cards = [
    { icon: '📦', cls: 'blue', value: s?.totalProducts, label: 'Total SKUs' },
    { icon: '📍', cls: 'green', value: s?.totalLocations, label: 'Storage Locations' },
    { icon: '📥', cls: 'amber', value: s?.totalInboundToday, label: "Today's Receipts" },
    { icon: '📤', cls: 'red', value: s?.totalOutboundToday, label: "Today's Orders" },
    { icon: '⚠️', cls: 'red', value: s?.lowStockCount, label: 'Low Stock Alerts' },
    { icon: '⏳', cls: 'purple', value: s?.expiringSoonCount, label: 'Expiring Soon' },
    { icon: '💰', cls: 'green', value: s?.totalInventoryValue, label: 'Inventory Value' },
  ];

  return (
    <div className="dashboard">
      <PageHeader title="Dashboard" subtitle={`Welcome back, ${user?.fullName || user?.username}: distribution center overview`} />
      <ErrorBanner message={stats.error} onRetry={stats.reload} />

      <div className="stats-grid">
        {cards.map((c) => (
          <div className="stat-card" key={c.label}>
            <div className={`stat-icon ${c.cls}`}>{c.icon}</div>
            <div className="stat-info">
              <h3>{c.value == null ? '-' : fmtNum(c.value)}</h3>
              <p>{c.label}</p>
            </div>
          </div>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(360px, 1fr))', gap: 16 }}>
        <div className="card">
          <div className="card-header">
            <span className="card-title">📥 Recent Inbound Receipts</span>
            <Link to="/inbound" style={{ fontSize: 12 }}>View all</Link>
          </div>
          <DataState loading={receipts.loading} error={receipts.error} onRetry={receipts.reload} empty={!receipts.data?.length} emptyTitle="No receipts yet">
            <table className="data-table">
              <thead><tr><th>Receipt #</th><th>Supplier</th><th>Status</th><th>Created</th></tr></thead>
              <tbody>
                {receipts.data.slice(0, 5).map((r) => (
                  <tr key={r.id}><td>{r.receiptNo}</td><td>{r.supplierName}</td><td><span className={badgeClass(r.status)}>{r.status}</span></td><td>{fmtDate(r.createdAt)}</td></tr>
                ))}
              </tbody>
            </table>
          </DataState>
        </div>

        <div className="card">
          <div className="card-header">
            <span className="card-title">📤 Recent Outbound Orders</span>
            <Link to="/outbound" style={{ fontSize: 12 }}>View all</Link>
          </div>
          <DataState loading={orders.loading} error={orders.error} onRetry={orders.reload} empty={!orders.data?.length} emptyTitle="No orders yet">
            <table className="data-table">
              <thead><tr><th>Order #</th><th>Destination</th><th>Priority</th><th>Status</th></tr></thead>
              <tbody>
                {orders.data.slice(0, 5).map((o) => (
                  <tr key={o.id}><td>{o.orderNumber}</td><td>{o.destination || '-'}</td><td><span className={badgeClass(o.priority)}>{o.priority}</span></td><td><span className={badgeClass(o.status)}>{o.status}</span></td></tr>
                ))}
              </tbody>
            </table>
          </DataState>
        </div>
      </div>
    </div>
  );
}
