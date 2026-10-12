import { useState, useEffect } from 'react';
import { inboundApi } from '../services';
import { Eye } from 'lucide-react';

const statusColors = {
  Draft: 'badge-default',
  InProgress: 'badge-warning',
  Received: 'badge-success',
  Partial: 'badge-info',
  Cancelled: 'badge-danger',
};

export default function Inbound() {
  const [receipts, setReceipts] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => { loadData(); }, []);

  async function loadData() {
    try {
      const res = await inboundApi.getAll();
      setReceipts(res.data?.data || res.data || []);
    } catch {
      setReceipts([]);
    } finally { setLoading(false); }
  }

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-title">📥 Inbound / Receiving</h1>
          <p className="page-subtitle">Receive goods from suppliers — PO → Receipt → QC → Put-away</p>
        </div>
      </div>

      <div className="card">
        {loading ? (
          <div style={{display:'flex',justifyContent:'center',padding:40}}><div className="spinner"></div></div>
        ) : (
          <table className="data-table">
            <thead>
              <tr><th>Receipt #</th><th>Supplier</th><th>PO Number</th><th>Status</th><th>Received Date</th><th>Actions</th></tr>
            </thead>
            <tbody>
              {receipts.map(r => (
                <tr key={r.id}>
                  <td><strong>{r.receiptNo}</strong></td>
                  <td>{r.supplierName || '-'}</td>
                  <td>{r.poNumber || '-'}</td>
                  <td><span className={`badge ${statusColors[r.status] || 'badge-default'}`}>{r.status}</span></td>
                  <td>{r.receivedDate ? new Date(r.receivedDate).toLocaleString('id-ID') : '-'}</td>
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
