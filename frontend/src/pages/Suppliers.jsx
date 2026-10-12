import CrudPage from '../components/ui/CrudPage';
import { suppliersApi } from '../services';

const columns = [
  { key: 'code', label: 'Code' },
  { key: 'name', label: 'Supplier Name' },
  { key: 'contactPerson', label: 'Contact' },
  { key: 'email', label: 'Email' },
  { key: 'phone', label: 'Phone' },
  { key: 'isActive', label: 'Active', render: (v) => v ? '✅' : '❌' },
];

export default function Suppliers() {
  return (
    <CrudPage
      title="Suppliers"
      subtitle="Vendor/supplier master data"
      apiService={suppliersApi}
      columns={columns}
      formFields={[
        { name: 'code', label: 'Supplier Code *', type: 'text', required: true, placeholder: 'e.g. SUP-001' },
        { name: 'name', label: 'Company Name *', type: 'text', required: true },
        { name: 'contactPerson', label: 'Contact Person', type: 'text' },
        { name: 'email', label: 'Email', type: 'email' },
        { name: 'phone', label: 'Phone', type: 'text' },
        { name: 'address', label: 'Address', type: 'textarea' },
        { name: 'isActive', label: 'Is Active', type: 'checkbox' },
      ]}
      initialValues={{ isActive: true }}
    />
  );
}
