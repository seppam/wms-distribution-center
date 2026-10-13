import CrudPage from '../components/ui/CrudPage';
import { enumOptions, useOptions } from '../hooks/useOptions';
import { locationsApi, zonesApi } from '../services';

const LOCATION_TYPES = ['Pick', 'Floor', 'Bulk', 'Reserved', 'Damage'];
const STATUS_COLORS = { Empty: 'badge-success', Occupied: 'badge-info', Partial: 'badge-warning', Locked: 'badge-danger' };

const columns = [
  { key: 'locationCode', label: 'Location Code', render: (v) => <span className="code-chip">{v}</span> },
  { key: 'zoneName', label: 'Zone' },
  { key: 'warehouseName', label: 'Warehouse' },
  { key: 'locationType', label: 'Type', render: (v) => <span className="badge badge-default">{v}</span> },
  { key: 'maxCapacity', label: 'Max Capacity', render: (v) => (v ?? 0).toLocaleString() },
  { key: 'status', label: 'Status', render: (v) => <span className={`badge ${STATUS_COLORS[v] || 'badge-default'}`}>{v}</span> },
];

export default function Locations() {
  const zones = useOptions(() => zonesApi.getAll(), (z) => `${z.warehouseName} / ${z.name}`);
  const formFields = [
    { name: 'zoneId', label: 'Zone *', type: 'select', required: true, options: zones.options, createOnly: true },
    { name: 'locationCode', label: 'Location Code *', type: 'text', required: true, placeholder: 'e.g. A-04-01' },
    { name: 'aisle', label: 'Aisle', type: 'text' },
    { name: 'rack', label: 'Rack', type: 'text' },
    { name: 'level', label: 'Level', type: 'text' },
    { name: 'bin', label: 'Bin', type: 'text' },
    { name: 'locationType', label: 'Location Type *', type: 'select', required: true, options: enumOptions(LOCATION_TYPES) },
    { name: 'maxCapacity', label: 'Max Capacity', type: 'number', min: 0 },
    { name: 'status', label: 'Status', type: 'select', options: enumOptions(['Empty', 'Occupied', 'Partial', 'Locked']), editOnly: true },
  ];
  return (
    <CrudPage title="Locations (Bins)" singular="Location" subtitle="Storage positions: zone, aisle, rack, level, bin"
      apiService={locationsApi} columns={columns} formFields={formFields}
      initialValues={{ locationType: 'Pick', maxCapacity: 1000 }} />
  );
}
