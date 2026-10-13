import CrudPage from '../components/ui/CrudPage';
import { enumOptions, useOptions } from '../hooks/useOptions';
import { warehousesApi, zonesApi } from '../services';

const ZONE_TYPES = ['Receiving', 'Storage', 'Packing', 'Shipping', 'Returns', 'Staging'];

const columns = [
  { key: 'name', label: 'Zone Name' },
  { key: 'zoneType', label: 'Type', render: (v) => <span className="badge badge-info">{v}</span> },
  { key: 'warehouseName', label: 'Warehouse' },
  { key: 'isActive', label: 'Status', render: (v) => (
    <span className={`badge ${v ? 'badge-success' : 'badge-default'}`}>{v ? 'Active' : 'Inactive'}</span>
  )},
];

export default function Zones() {
  const warehouses = useOptions(() => warehousesApi.getAll(), (w) => `${w.code} - ${w.name}`);
  const formFields = [
    { name: 'warehouseId', label: 'Warehouse *', type: 'select', required: true, options: warehouses.options, createOnly: true },
    { name: 'name', label: 'Zone Name *', type: 'text', required: true },
    { name: 'zoneType', label: 'Zone Type *', type: 'select', required: true, options: enumOptions(ZONE_TYPES) },
    { name: 'description', label: 'Description', type: 'textarea' },
    { name: 'isActive', label: 'Is Active', type: 'checkbox', editOnly: true },
  ];
  return (
    <CrudPage title="Zones" subtitle="Warehouse zones: receiving, storage, packing, shipping, returns"
      apiService={zonesApi} columns={columns} formFields={formFields} />
  );
}
