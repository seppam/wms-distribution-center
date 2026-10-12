import { useState, useEffect } from 'react';
import { outboundApi } from '../services';
import { Eye } from 'lucide-react';

const statusColors = {
  Draft: 'badge-default', Picked: 'badge-info', Packed: 'badge-warning',
  Shipped: 'badge-success', Completed: 'badge-success', Cancelled: 'badge-danger',
};
const priorityColors = { Normal: 'badge-default', Urgent: 'badge-warning', Rush: 'badge-danger' };

export default function Outbound() {
  const [orders, setOrders] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => { loadData(); }, []);

  async function loadData() {
    try {
      const res = await outboundApi.getAll();
      setOrders(res.data?.data || res.data || []);
    } catch {
      setOrders([]);
    } finally { setLoading(false); }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-title">📤 Outbound / Orders</h1>
          <p className="page-subtitle">Sales orders & transfers — Pick → Pack → Ship</p>
        </div>
      </div>

      <div className="card">
        {loading ? (
          <div style={{display:'flex',justifyContent:'center',padding:40}}><div className="spinner"></div></div>
        ) : (
          <table className="data-table">
            <thead>
              <tr><th>Order #</th><th>Destination</th><th>Priority</th><th>Due Date</th><th>Status</th><th>Actions</th></tr>
            </thead>
            <tbody>
              {orders.map(o => (
                <tr key={o.id}>
                  <td><strong>{o.orderNumber}</strong></td>
                  <td>{o.destination}</td>
                  <td><span className={`badge ${priorityColors[o.priority] || 'badge-default'}`}>{o.priority}</span></td>
                  <td>{o.dueDate || '-'}</td>
                  <td><span className={`badge ${statusColors[o.status] || 'badge-default'}`}>{o.status}</span></td>
                  <td>
                    <button className="btn btn-sm btn-outline"><Eye size={13} /> View</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
