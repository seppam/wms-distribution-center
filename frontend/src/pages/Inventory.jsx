import { useState, useEffect } from 'react';
import { inventoryApi } from '../services';
import './Inventory.css';

export default function Inventory() {
  const [inventory, setInventory] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');

  useEffect(() => {
    loadInventory();
  }, []);

  async function loadInventory() {
    try {
      const res = await inventoryApi.getSummary({});
      setInventory(res.data?.data || res.data || []);
    } catch {
      setInventory([]);
    } finally {
      setLoading(false);
    }
  }

  const filtered = inventory.filter(item =>
    !search ||
    item.productName?.toLowerCase().includes(search.toLowerCase()) ||
    item.productSku?.toLowerCase().includes(search.toLowerCase()) ||
    item.locationCode?.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-title">Inventory</h1>
          <p className="page-subtitle">Real-time stock levels across all locations</p>
        </div>
      </div>

      <div className="inv-stats">
        <div className="inv-stat">
          <span className="inv-stat-num">{inventory.reduce((s, i) => s + (i.qtyOnHand || 0), 0).toLocaleString()}</span>
          <span className="inv-stat-label">Total Units</span>
        </div>
        <div className="inv-stat">
          <span className="inv-stat-num">{inventory.length}</span>
          <span className="inv-stat-label">SKU Locations</span>
        </div>
        <div className="inv-stat">
          <span className="inv-stat-num" style={{color:'#fbbf24'}}>{inventory.filter(i => i.status === 'Hold').length}</span>
          <span className="inv-stat-label">On Hold</span>
        </div>
        <div className="inv-stat">
          <span className="inv-stat-num" style={{color:'#f87171'}}>{inventory.filter(i => i.status === 'Damaged').length}</span>
          <span className="inv-stat-label">Damaged</span>
        </div>
      </div>

      <div className="search-bar">
        <input placeholder="Search by SKU, product name, or location code..." value={search} onChange={(e) => setSearch(e.target.value)} />
      </div>

      <div className="card">
        {loading ? (
          <div style={{display:'flex',justifyContent:'center',padding:40}}><div className="spinner"></div></div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Product</th>
                <th>Location</th>
                <th>Zone</th>
                <th>On Hand</th>
                <th>Reserved</th>
                <th>Available</th>
                <th>Lot #</th>
                <th>Expiry</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(item => (
                <tr key={item.id}>
                  <td>
                    <div style={{fontWeight:600}}>{item.productName || '-'}</div>
                    <div style={{fontSize:11,color:'#64748b'}}>{item.productSku}</div>
                  </td>
                  <td><code style={{background:'#0f172a',padding:'2px 8px',borderRadius:4}}>{item.locationCode}</code></td>
                  <td>{item.zoneName || '-'}</td>
                  <td style={{fontWeight:600}}>{(item.qtyOnHand || 0).toLocaleString()}</td>
                  <td>{(item.qtyReserved || 0).toLocaleString()}</td>
                  <td style={{fontWeight:700,color:'#34d399'}}>{((item.qtyOnHand || 0) - (item.qtyReserved || 0)).toLocaleString()}</td>
                  <td style={{fontSize:11.5,color:'#94a3b8'}}>{item.lotNumber || '-'}</td>
                  <td>{item.expiryDate || '-'}</td>
                  <td>
                    <span className={`badge badge-${
                      item.status === 'Available' ? 'success' :
                      item.status === 'Hold' ? 'warning' :
                      item.status === 'Damaged' ? 'danger' : 'default'
                    }`}>{item.status}</span>
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
