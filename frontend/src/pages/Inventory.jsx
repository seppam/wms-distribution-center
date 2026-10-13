import { useState } from 'react';
import { DataState, PageHeader } from '../components/ui';
import { useLoad } from '../hooks/useLoad';
import { inventoryApi } from '../services';
import { badgeClass, fmtDateTime, fmtNum } from '../utils/format';
import './Inventory.css';

const TX_BADGE = { In: 'success', Out: 'warning', Adjust: 'info' };

function StockTab({ search }) {
  const { data, loading, error, reload } = useLoad(() => inventoryApi.query(), [], { initial: [] });
  const rows = (data || []).filter((i) => {
    const q = search.toLowerCase();
    return !q || [i.productName, i.productSku, i.locationCode].some((v) => v?.toLowerCase().includes(q));
  });
  const available = (i) => (i.qtyOnHand || 0) - (i.qtyReserved || 0);

  return (
    <>
      <div className="inv-stats">
        <div className="inv-stat">
          <span className="inv-stat-num">{fmtNum((data || []).reduce((s, i) => s + (i.qtyOnHand || 0), 0))}</span>
          <span className="inv-stat-label">Total Units</span>
        </div>
        <div className="inv-stat">
          <span className="inv-stat-num">{(data || []).length}</span>
          <span className="inv-stat-label">SKU Locations</span>
        </div>
        <div className="inv-stat">
          <span className="inv-stat-num" style={{ color: '#fbbf24' }}>{fmtNum((data || []).reduce((s, i) => s + (i.qtyReserved || 0), 0))}</span>
          <span className="inv-stat-label">Reserved for orders</span>
        </div>
        <div className="inv-stat">
          <span className="inv-stat-num" style={{ color: '#f87171' }}>{(data || []).filter((i) => i.status === 'Damaged' || i.status === 'Hold').length}</span>
          <span className="inv-stat-label">On hold / damaged</span>
        </div>
      </div>
      <div className="card">
        <DataState loading={loading} error={error} onRetry={reload} empty={rows.length === 0}
          emptyTitle="No stock found" emptyText="Receive goods on the Inbound page to create stock.">
          <table className="data-table">
            <thead>
              <tr><th>Product</th><th>Location</th><th>Zone</th><th>On Hand</th><th>Reserved</th><th>Available</th><th>Lot #</th><th>Expiry</th><th>Status</th></tr>
            </thead>
            <tbody>
              {rows.map((item) => (
                <tr key={item.id}>
                  <td>
                    <div style={{ fontWeight: 600 }}>{item.productName || '-'}</div>
                    <div className="muted">{item.productSku}</div>
                  </td>
                  <td><span className="code-chip">{item.locationCode}</span></td>
                  <td>{item.zoneName || '-'}</td>
                  <td style={{ fontWeight: 600 }}>{fmtNum(item.qtyOnHand)}</td>
                  <td>{fmtNum(item.qtyReserved)}</td>
                  <td style={{ fontWeight: 700, color: '#34d399' }}>{fmtNum(available(item))}</td>
                  <td className="muted">{item.lotNumber || '-'}</td>
                  <td>{item.expiryDate || '-'}</td>
                  <td><span className={badgeClass(item.status)}>{item.status}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </DataState>
      </div>
    </>
  );
}

function LedgerTab({ search }) {
  const { data, loading, error, reload } = useLoad(() => inventoryApi.getTransactions({ take: 200 }), [], { initial: [] });
  const rows = (data || []).filter((t) => {
    const q = search.toLowerCase();
    return !q || [t.productName, t.productSku, t.locationCode, t.referenceType].some((v) => v?.toLowerCase().includes(q));
  });
  return (
    <div className="card">
      <DataState loading={loading} error={error} onRetry={reload} empty={rows.length === 0}
        emptyTitle="No stock movements yet" emptyText="Every receipt, shipment, adjustment, transfer and return is logged here.">
        <table className="data-table">
          <thead>
            <tr><th>When</th><th>Product</th><th>Location</th><th>Type</th><th>Source</th><th>Qty</th><th>Before</th><th>After</th><th>By</th></tr>
          </thead>
          <tbody>
            {rows.map((t) => (
              <tr key={t.id}>
                <td>{fmtDateTime(t.createdAt)}</td>
                <td><div style={{ fontWeight: 600 }}>{t.productName}</div><div className="muted">{t.productSku}</div></td>
                <td><span className="code-chip">{t.locationCode}</span></td>
                <td><span className={`badge badge-${TX_BADGE[t.transactionType] || 'default'}`}>{t.transactionType}</span></td>
                <td>{t.referenceType}</td>
                <td style={{ fontWeight: 600 }}>{fmtNum(t.uomQty)}</td>
                <td>{fmtNum(t.qtyBefore)}</td>
                <td>{fmtNum(t.qtyAfter)}</td>
                <td>{t.userName}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </DataState>
    </div>
  );
}

export default function Inventory() {
  const [tab, setTab] = useState('stock');
  const [search, setSearch] = useState('');

  return (
    <div>
      <PageHeader title="Inventory" subtitle="Stock levels by location and the immutable movement ledger" />
      <div className="tabs" role="tablist">
        <button role="tab" aria-selected={tab === 'stock'} className={`tab ${tab === 'stock' ? 'active' : ''}`} onClick={() => setTab('stock')}>Stock</button>
        <button role="tab" aria-selected={tab === 'ledger'} className={`tab ${tab === 'ledger' ? 'active' : ''}`} onClick={() => setTab('ledger')}>Movement ledger</button>
      </div>
      <div className="search-bar">
        <input aria-label="Search inventory" placeholder="Search by SKU, product name, or location code..." value={search} onChange={(e) => setSearch(e.target.value)} />
      </div>
      {tab === 'stock' ? <StockTab search={search} /> : <LedgerTab search={search} />}
    </div>
  );
}
