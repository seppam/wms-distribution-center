import CrudPage from '../components/ui/CrudPage';
import { categoriesApi } from '../services';

const columns = [
  { key: 'name', label: 'Category Name' },
  { key: 'level', label: 'Level', render: (v) => `L${v}` },
  { key: 'parent', label: 'Parent', render: (v) => v?.name || '-' },
  { key: 'description', label: 'Description' },
];

const formFields = [
  { name: 'name', label: 'Category Name *', type: 'text', required: true },
  { name: 'description', label: 'Description', type: 'textarea' },
];

export default function Categories() {
  return (
    <CrudPage
      title="Categories"
      subtitle="Product category hierarchy (tree structure)"
      apiService={categoriesApi}
      columns={columns}
      formFields={formFields}
    />
  );
}
