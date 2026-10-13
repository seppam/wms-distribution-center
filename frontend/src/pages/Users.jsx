import CrudPage from '../components/ui/CrudPage';
import { useOptions } from '../hooks/useOptions';
import { rolesApi, usersApi, warehousesApi } from '../services';
import { useAuth } from '../contexts/AuthContext';

const columns = [
  { key: 'username', label: 'Username' },
  { key: 'fullName', label: 'Full Name' },
  { key: 'email', label: 'Email' },
  { key: 'roleName', label: 'Role', render: (v) => <span className="badge badge-info">{v || '-'}</span> },
  { key: 'isActive', label: 'Status', render: (v) => (
    <span className={`badge ${v ? 'badge-success' : 'badge-default'}`}>{v ? 'Active' : 'Inactive'}</span>
  )},
];

export default function Users() {
  const { can } = useAuth();
  const isAdmin = can('admin');
  // Roles are admin-only data; supervisors get a read-only list
  const roles = useOptions(() => (isAdmin ? rolesApi.getAll() : Promise.resolve({ data: [] })), (r) => r.name);
  const warehouses = useOptions(() => warehousesApi.getAll(), (w) => w.name);

  const formFields = [
    { name: 'username', label: 'Username *', type: 'text', required: true, createOnly: true },
    { name: 'fullName', label: 'Full Name *', type: 'text', required: true },
    { name: 'email', label: 'Email *', type: 'email', required: true },
    { name: 'phone', label: 'Phone', type: 'text' },
    { name: 'password', label: 'Password (min 10 characters)', type: 'password', required: true, minLength: 10 },
    { name: 'roleId', label: 'Role *', type: 'select', required: true, options: roles.options },
    { name: 'warehouseId', label: 'Warehouse', type: 'select', options: warehouses.options },
    { name: 'isActive', label: 'Is Active', type: 'checkbox', editOnly: true },
  ];

  return (
    <CrudPage
      title="Users"
      subtitle={isAdmin ? 'Manage users and their role assignments' : 'Team members (read-only for supervisors)'}
      apiService={usersApi}
      columns={columns}
      formFields={formFields}
      writePolicy="admin"
      deletePolicy="admin"
      initialValues={{ isActive: true }}
    />
  );
}
