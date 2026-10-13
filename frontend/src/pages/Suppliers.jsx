import CrudPage from '../components/ui/CrudPage';
import { suppliersApi } from '../services';

const columns = [
  { key: 'code', label: 'Code' },
  { key: 'name', label: 'Supplier Name' },
  { key: 'contactPerson', label: 'Contact' },
  { key: 'email', label: 'Email' },
  { key: 'phone', label: 'Phone' },
  { key: 'isActive', label: 'Status', render: (v) => (
    <span className={`badge ${v ? 'badge-success' : 'badge-default'}`}>{v ? 'Active' : 'Inactive'}</span>
  )},
];

const formFields = [
  { name: 'code', label: 'Supplier Code *', type: 'text', required: true, placeholder: 'e.g. SUP-003', createOnly: true },
  { name: 'name', label: 'Company Name *', type: 'text', required: true },
  { name: 'contactPerson', label: 'Contact Person', type: 'text' },
  { name: 'email', label: 'Email', type: 'email' },
  { name: 'phone', label: 'Phone', type: 'text' },
  { name: 'address', label: 'Address', type: 'textarea' },
  { name: 'isActive', label: 'Is Active', type: 'checkbox', editOnly: true },
];

export default function Suppliers() {
  return (
    <CrudPage title="Suppliers" subtitle="Vendors that deliver goods to the warehouse"
      apiService={suppliersApi} columns={columns} formFields={formFields} />
  );
}
