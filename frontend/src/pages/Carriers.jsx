import CrudPage from '../components/ui/CrudPage';
import { carriersApi } from '../services';

const columns = [
  { key: 'name', label: 'Carrier Name' },
  { key: 'contactPerson', label: 'Contact' },
  { key: 'phone', label: 'Phone' },
  { key: 'vehicleType', label: 'Vehicle Type' },
  { key: 'isActive', label: 'Active', render: (v) => v ? '✅' : '❌' },
];

export default function Carriers() {
  return (
    <CrudPage
      title="Carriers"
      subtitle="Shipping & transportation carriers"
      apiService={carriersApi}
      columns={columns}
      formFields={[
        { name: 'name', label: 'Carrier Name *', type: 'text', required: true },
        { name: 'contactPerson', label: 'Contact Person', type: 'text' },
        { name: 'phone', label: 'Phone', type: 'text' },
        { name: 'email', label: 'Email', type: 'email' },
        { name: 'vehicleType', label: 'Vehicle Type', type: 'text', placeholder: 'e.g. CDE, Fuso, Box Truck' },
        { name: 'isActive', label: 'Is Active', type: 'checkbox' },
      ]}
      initialValues={{ isActive: true }}
    />
  );
}
