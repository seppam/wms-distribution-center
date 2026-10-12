import CrudPage from '../components/ui/CrudPage';
import { productsApi } from '../services';

const columns = [
  { key: 'sku', label: 'SKU' },
  { key: 'name', label: 'Product Name' },
  { key: 'category', label: 'Category', render: (v) => v?.name || '-' },
  { key: 'barcode', label: 'Barcode' },
  { key: 'minStock', label: 'Min Stock' },
  { key: 'isActive', label: 'Status', render: (v) => (
    <span className={`badge ${v ? 'badge-success' : 'badge-default'}`}>{v ? 'Active' : 'Inactive'}</span>
  )},
];

const formFields = [
  { name: 'sku', label: 'SKU *', type: 'text', required: true, placeholder: 'e.g. SI-001234' },
  { name: 'name', label: 'Product Name *', type: 'text', required: true },
  { name: 'barcode', label: 'Barcode', type: 'text' },
  { name: 'weightKg', label: 'Weight (kg)', type: 'number', step: '0.001' },
  { name: 'volumeM3', label: 'Volume (m³)', type: 'number', step: '0.0001' },
  { name: 'minStock', label: 'Min Stock Level', type: 'number' },
  { name: 'maxStock', label: 'Max Stock Level', type: 'number' },
  { name: 'isActive', label: 'Is Active', type: 'checkbox' },
];

export default function Products() {
  return (
    <CrudPage
      title="Products"
      subtitle="Manage product master data — SKUs, barcodes, dimensions"
      apiService={productsApi}
      columns={columns}
      formFields={formFields}
      initialValues={{ isActive: true, minStock: 0 }}
    />
  );
}
