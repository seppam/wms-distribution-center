import CrudPage from '../components/ui/CrudPage';
import { rolesApi } from '../services';

const columns = [
  { key: 'name', label: 'Role' },
  { key: 'description', label: 'Description' },
  { key: 'isSystem', label: 'Built-in', render: (v) => (v ? <span className="badge badge-info">Built-in</span> : 'Custom') },
];

const formFields = [
  { name: 'name', label: 'Role Name *', type: 'text', required: true },
  { name: 'description', label: 'Description', type: 'textarea' },
];

export default function Roles() {
  return (
    <CrudPage title="Roles" subtitle="Built-in roles carry the permissions in the permission matrix; custom roles are read-only"
      apiService={rolesApi} columns={columns} formFields={formFields} writePolicy="admin" deletePolicy="admin" />
  );
}
