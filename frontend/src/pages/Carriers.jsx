import CrudPage from '../components/ui/CrudPage';
import { carriersApi } from '../services';

const columns = [
  { key: 'name', label: 'Carrier Name' },
  { key: 'contactPerson', label: 'Contact' },
  { key: 'phone', label: 'Phone' },
  { key: 'vehicleType', label: 'Vehicle Type' },
  { key: 'isActive', label: 'Status', render: (v) => (
    <span className={`badge ${v ? 'badge-success' : 'badge-default'}`}>{v ? 'Active' : 'Inactive'}</span>
  )},
];

const formFields = [
  { name: 'name', label: 'Carrier Name *', type: 'text', required: true },
  { name: 'contactPerson', label: 'Contact Person', type: 'text' },
  { name: 'phone', label: 'Phone', type: 'text' },
  { name: 'email', label: 'Email', type: 'email' },
  { name: 'vehicleType', label: 'Vehicle Type', type: 'text', placeholder: 'e.g. Box truck' },
  { name: 'isActive', label: 'Is Active', type: 'checkbox', editOnly: true },
];

export default function Carriers() {
  return (
    <CrudPage title="Carriers" subtitle="Shipping and transportation carriers"
      apiService={carriersApi} columns={columns} formFields={formFields} />
  );
}
