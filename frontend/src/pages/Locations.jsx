import CrudPage from '../components/ui/CrudPage';
import { locationsApi, zonesApi } from '../services';
import { useState, useEffect } from 'react';

const columns = [
  { key: 'locationCode', label: 'Location Code' },
  { key: 'zone', label: 'Zone', render: (v) => v?.name || '-' },
  { key: 'aisle', label: 'Aisle' },
  { key: 'rack', label: 'Rack' },
  { key: 'level', label: 'Level' },
  { key: 'bin', label: 'Bin' },
  { key: 'locationType', label: 'Type', render: (v) => <span className="badge badge-default">{v}</span> },
  { key: 'status', label: 'Status', render: (v) => {
    const colors = { Empty: 'badge-success', Occupied: 'badge-info', Partial: 'badge-warning', Locked: 'badge-danger' };
    return <span className={`badge ${colors[v] || 'badge-default'}`}>{v}</span>;
  }},
];

export default function Locations() {
  const [, setZones] = useState([]);

  useEffect(() => {
    zonesApi.getAll().then(res => setZones(res.data || [])).catch(() => {});
  }, []);

  const formFields = [
    { name: 'locationCode', label: 'Location Code *', type: 'text', required: true, placeholder: 'e.g. A-01-03-02' },
    { name: 'aisle', label: 'Aisle', type: 'text', placeholder: 'e.g. A' },
    { name: 'rack', label: 'Rack', type: 'text', placeholder: 'e.g. 01' },
    { name: 'level', label: 'Level', type: 'text', placeholder: 'e.g. 03' },
    { name: 'bin', label: 'Bin', type: 'text', placeholder: 'e.g. 02' },
    {
      name: 'locationType', label: 'Location Type *', type: 'select', required: true,
      options: [
        { value: 'Pick', label: '📦 Pick Face' },
        { value: 'Floor', label: '🏗️ Floor' },
        { value: 'Bulk', label: '📦 Bulk Storage' },
        { value: 'Reserved', label: '🔒 Reserved' },
        { value: 'Damage', label: '⚠️ Damage Area' },
      ],
    },
    { name: 'maxCapacity', label: 'Max Capacity', type: 'number', step: '0.001' },
    {
      name: 'status', label: 'Status', type: 'select',
      options: [
        { value: 'Empty', label: 'Empty' },
        { value: 'Occupied', label: 'Occupied' },
        { value: 'Partial', label: 'Partial' },
        { value: 'Locked', label: 'Locked' },
      ],
    },
  ];

  return (
    <CrudPage
      title="Locations (Bins)"
      subtitle="Individual storage positions — Zone → Aisle → Rack → Level → Bin"
      apiService={locationsApi}
      columns={columns}
      formFields={formFields}
      initialValues={{ status: 'Empty', maxCapacity: 0 }}
    />
  );
}
