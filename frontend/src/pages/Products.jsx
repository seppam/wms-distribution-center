import CrudPage from '../components/ui/CrudPage';
import { useOptions } from '../hooks/useOptions';
import { categoriesApi, productsApi, uomsApi } from '../services';

const columns = [
  { key: 'sku', label: 'SKU' },
  { key: 'name', label: 'Product Name' },
  { key: 'categoryName', label: 'Category' },
  { key: 'uomName', label: 'Unit' },
  { key: 'barcode', label: 'Barcode' },
  { key: 'minStock', label: 'Min Stock' },
  { key: 'isActive', label: 'Status', render: (v) => (
    <span className={`badge ${v ? 'badge-success' : 'badge-default'}`}>{v ? 'Active' : 'Inactive'}</span>
  )},
];

export default function Products() {
  const categories = useOptions(() => categoriesApi.getAll(), (c) => c.name);
  const uoms = useOptions(() => uomsApi.getAll(), (u) => `${u.code} - ${u.name}`);

  const formFields = [
    { name: 'sku', label: 'SKU *', type: 'text', required: true, placeholder: 'e.g. SKU-3001', createOnly: true },
    { name: 'name', label: 'Product Name *', type: 'text', required: true },
    { name: 'categoryId', label: 'Category', type: 'select', options: categories.options },
    { name: 'uomId', label: 'Unit of measure *', type: 'select', required: true, options: uoms.options },
    { name: 'barcode', label: 'Barcode', type: 'text' },
    { name: 'weightKg', label: 'Weight (kg)', type: 'number', step: '0.001', min: 0 },
    { name: 'volumeM3', label: 'Volume (m3)', type: 'number', step: '0.0001', min: 0 },
    { name: 'minStock', label: 'Min Stock Level', type: 'number', min: 0 },
    { name: 'maxStock', label: 'Max Stock Level', type: 'number', min: 0 },
    { name: 'isActive', label: 'Is Active', type: 'checkbox', editOnly: true },
  ];

  return (
    <CrudPage
      title="Products"
      subtitle="Manage product master data: SKUs, barcodes, dimensions"
      apiService={productsApi}
      columns={columns}
      formFields={formFields}
      initialValues={{ minStock: 0, weightKg: 0, volumeM3: 0 }}
    />
  );
}
