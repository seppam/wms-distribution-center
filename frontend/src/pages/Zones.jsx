import CrudPage from '../components/ui/CrudPage';
import { zonesApi, warehousesApi } from '../services';
import { useState, useEffect } from 'react';

const columns = [
  { key: 'name', label: 'Zone Name' },
  { key: 'zoneType', label: 'Type', render: (v) => (
    <span className="badge badge-info">{v}</span>
  )},
  { key: 'warehouse', label: 'Warehouse', render: (v) => v?.name || '-' },
  { key: 'isActive', label: 'Active', render: (v) => v ? '✅' : '❌' },
];

export default function Zones() {
  const [warehouses, setWarehouses] = useState([]);

  useEffect(() => {
    warehousesApi.getAll().then(res => setWarehouses(res.data || [])).catch(() => {});
  }, []);

  const formFields = [
    { name: 'name', label: 'Zone Name *', type: 'text', required: true },
    {
      name: 'zoneType', label: 'Zone Type *', type: 'select', required: true,
      options: [
        { value: 'Receiving', label: '📥 Receiving' },
        { value: 'Storage', label: '📦 Storage' },
        { value: 'Packing', label: '📦 Packing' },
        { value: 'Shipping', label: '🚚 Shipping' },
        { value: 'Returns', label: '↩️ Returns' },
        { value: 'Staging', label: '📋 Staging' },
      ],
    },
    {
      name: 'warehouseId', label: 'Warehouse *', type: 'select', required: true,
      options: [], // populated dynamically
    },
    { name: 'description', label: 'Description', type: 'textarea' },
  ];

  return (
    <CrudPage
      title="Zones"
      subtitle="Warehouse zones — Receiving, Storage, Packing, Shipping, Returns"
      apiService={zonesApi}
      columns={columns}
      formFields={formFields.map(f =>
        f.name === 'warehouseId'
          ? { ...f, options: warehouses.map(w => ({ value: w.id, label: `${w.code} - ${w.name}` })) }
          : f
      )}
      initialValues={{ isActive: true }}
    />
  );
}
