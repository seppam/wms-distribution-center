import CrudPage from '../components/ui/CrudPage';
import { rolesApi } from '../services';

const columns = [
  { key: 'name', label: 'Role Name' },
  { key: 'description', label: 'Description' },
  { key: 'isSystem', label: 'System Role', render: (v) => v ? '✅ Yes' : '❌ No' },
];

const formFields = [
  { name: 'name', label: 'Role Name *', type: 'text', required: true, placeholder: 'e.g. Supervisor' },
  { name: 'description', label: 'Description', type: 'textarea', placeholder: 'Describe the role permissions...' },
];

export default function Roles() {
  return (
    <CrudPage
      title="Roles"
      subtitle="RBAC — Define roles and their permission sets"
      apiService={rolesApi}
      columns={columns}
      formFields={formFields}
    />
  );
}
