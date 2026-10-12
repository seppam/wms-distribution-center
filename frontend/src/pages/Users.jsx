import CrudPage from '../components/ui/CrudPage';
import { usersApi, rolesApi } from '../services';
import { useState, useEffect } from 'react';

const columns = [
  { key: 'username', label: 'Username' },
  { key: 'fullName', label: 'Full Name' },
  { key: 'email', label: 'Email' },
  { key: 'role', label: 'Role', render: (v) => (
    <span className="badge badge-info">{v?.name || '-'}</span>
  )},
  { key: 'isActive', label: 'Status', render: (v) => (
    <span className={`badge ${v ? 'badge-success' : 'badge-default'}`}>{v ? 'Active' : 'Inactive'}</span>
  )},
];

export default function Users() {
  const [roles, setRoles] = useState([]);

  useEffect(() => {
    rolesApi.getAll().then(res => setRoles(res.data || [])).catch(() => {});
  }, []);

  const formFields = [
    { name: 'username', label: 'Username *', type: 'text', required: true },
    { name: 'fullName', label: 'Full Name *', type: 'text', required: true },
    { name: 'email', label: 'Email *', type: 'email', required: true },
    { name: 'phone', label: 'Phone', type: 'text' },
    { name: 'password', label: 'Password (for new users)', type: 'password' },
    {
      name: 'roleId', label: 'Role *', type: 'select', required: true,
      options: roles.map(r => ({ value: r.id, label: r.name })),
    },
    { name: 'isActive', label: 'Is Active', type: 'checkbox' },
  ];

  return (
    <CrudPage
      title="Users"
      subtitle="Manage system users and their role assignments"
      apiService={usersApi}
      columns={columns}
      formFields={formFields}
      initialValues={{ isActive: true }}
    />
  );
}
