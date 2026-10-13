import CrudPage from '../components/ui/CrudPage';
import { useOptions } from '../hooks/useOptions';
import { categoriesApi } from '../services';

const columns = [
  { key: 'name', label: 'Category' },
  { key: 'parentName', label: 'Parent' },
  { key: 'level', label: 'Level' },
  { key: 'description', label: 'Description' },
];

export default function Categories() {
  const parents = useOptions(() => categoriesApi.getAll(), (c) => c.name);
  const formFields = [
    { name: 'name', label: 'Category Name *', type: 'text', required: true },
    { name: 'parentId', label: 'Parent Category', type: 'select', options: parents.options },
    { name: 'description', label: 'Description', type: 'textarea' },
  ];
  return (
    <CrudPage title="Categories" singular="Category" subtitle="Product category hierarchy"
      apiService={categoriesApi} columns={columns} formFields={formFields} />
  );
}
