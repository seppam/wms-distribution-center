import { useState, useEffect } from 'react';
import { reportsApi, inboundApi, outboundApi } from '../services';
import { useAuth } from '../contexts/AuthContext';

const inboundBadge = { Draft: 'default', InProgress: 'warning', Received: 'success', Partial: 'info', Cancelled: 'danger' };
const outboundBadge = { Draft: 'info', Picked: 'warning', Packed: 'warning', Shipped: 'success', Completed: 'success', Cancelled: 'danger' };
const priorityBadge = { Normal: 'default', Urgent: 'warning', Rush: 'danger' };

const fmtDate = (d) => (d ? new Date(d).toLocaleDateString() : '-');
const list = (res) => res.data?.data || res.data || [];

export default function Dashboard() {
  const [stats, setStats] = useState(null);
  const [receipts, setReceipts] = useState([]);
  const [orders, setOrders] = useState([]);
  const { user } = useAuth();

  useEffect(() => {
    reportsApi.getDashboard().then((r) => setStats(r.data)).catch(() => setStats(null));
    inboundApi.getAll().then((r) => setReceipts(list(r).slice(0, 5))).catch(() => {});
    outboundApi.getAll().then((r) => setOrders(list(r).slice(0, 5))).catch(() => {});
  }, []);

  const cards = [
    { icon: '📦', cls: 'blue', value: stats?.totalProducts, label: 'Total SKUs' },
    { icon: '📍', cls: 'green', value: stats?.totalLocations, label: 'Storage Locations' },
    { icon: '📥', cls: 'amber', value: stats?.totalInboundToday, label: "Today's Receipts" },
    { icon: '📤', cls: 'red', value: stats?.totalOutboundToday, label: "Today's Orders" },
    { icon: '⚠️', cls: 'red', value: stats?.lowStockCount, label: 'Low Stock Alerts' },
    { icon: '⏳', cls: 'purple', value: stats?.expiringSoonCount, label: 'Expiring Soon' },
    { icon: '💰', cls: 'green', value: stats?.totalInventoryValue, label: 'Inventory Value' },
  ];

  return (
    <div className="dashboard">
      <div className="page-header">
        <div>
          <h1 className="page-title">Dashboard</h1>
          <p className="page-subtitle">
            Welcome back, {user?.fullName || user?.username} — Distribution Center overview
          </p>
        </div>
      </div>

      <div className="stats-grid">
        {cards.map((c) => (
          <div className="stat-card" key={c.label}>
            <div className={`stat-icon ${c.cls}`}>{c.icon}</div>
            <div className="stat-info">
              <h3>{c.value == null ? '—' : Number(c.value).toLocaleString()}</h3>
              <p>{c.label}</p>
            </div>
          </div>
        ))}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
        <div className="card">
          <div className="card-header">
            <span className="card-title">📥 Recent Inbound Receipts</span>
            <a href="/inbound" style={{color:'#38bdf8',fontSize:12,textDecoration:'none'}}>View All →</a>
          </div>
          <table className="data-table">
            <thead><tr><th>Receipt #</th><th>Supplier</th><th>Status</th><th>Created</th></tr></thead>
            <tbody>
              {receipts.length === 0 && <tr><td colSpan="4">No receipts yet</td></tr>}
              {receipts.map((r) => (
                <tr key={r.id}><td>{r.receiptNo}</td><td>{r.supplierName}</td><td><span className={`badge badge-${inboundBadge[r.status] || 'default'}`}>{r.status}</span></td><td>{fmtDate(r.createdAt)}</td></tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="card">
          <div className="card-header">
            <span className="card-title">📤 Recent Outbound Orders</span>
            <a href="/outbound" style={{color:'#38bdf8',fontSize:12,textDecoration:'none'}}>View All →</a>
          </div>
          <table className="data-table">
            <thead><tr><th>Order #</th><th>Destination</th><th>Priority</th><th>Status</th></tr></thead>
            <tbody>
              {orders.length === 0 && <tr><td colSpan="4">No orders yet</td></tr>}
              {orders.map((o) => (
                <tr key={o.id}><td>{o.orderNumber}</td><td>{o.destination || '-'}</td><td><span className={`badge badge-${priorityBadge[o.priority] || 'default'}`}>{o.priority}</span></td><td><span className={`badge badge-${outboundBadge[o.status] || 'default'}`}>{o.status}</span></td></tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
