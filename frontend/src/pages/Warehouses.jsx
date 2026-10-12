import CrudPage from '../components/ui/CrudPage';
import { warehousesApi } from '../services';

const columns = [
  { key: 'code', label: 'Code' },
  { key: 'name', label: 'Warehouse Name' },
  { key: 'city', label: 'City' },
  { key: 'capacity', label: 'Capacity', render: (v) => v?.toLocaleString() },
  { key: 'isActive', label: 'Status', render: (v) => (
    <span className={`badge ${v ? 'badge-success' : 'badge-default'}`}>{v ? 'Active' : 'Inactive'}</span>
  )},
];

const formFields = [
  { name: 'code', label: 'Warehouse Code *', type: 'text', required: true, placeholder: 'e.g. DC-MAIN' },
  { name: 'name', label: 'Name *', type: 'text', required: true, placeholder: 'e.g. Main DC' },
  { name: 'address', label: 'Address', type: 'textarea' },
  { name: 'city', label: 'City', type: 'text' },
  { name: 'capacity', label: 'Capacity (units)', type: 'number' },
  { name: 'isActive', label: 'Is Active', type: 'checkbox' },
];

export default function Warehouses() {
  return (
    <CrudPage
      title="Warehouses"
      subtitle="Distribution Center facilities management"
      apiService={warehousesApi}
      columns={columns}
      formFields={formFields}
      initialValues={{ isActive: true }}
    />
  );
}
