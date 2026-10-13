import { useMemo, useState } from 'react';
import { Plus, Search, Edit2, Trash2 } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useLoad } from '../../hooks/useLoad';
import { errorMessage } from '../../services/api';
import { Confirm, DataState, ErrorBanner, Field, Modal, PageHeader, Select, useToast } from './index';

/**
 * Generic list + create/edit/delete page for master data.
 * Buttons are hidden for roles the API would reject (write: Admin/Supervisor, delete: Admin by default).
 */
export default function CrudPage({
  title,
  subtitle,
  singular,
  apiService,
  columns,
  formFields,
  initialValues = {},
  writePolicy = 'manage',
  deletePolicy = 'admin',
}) {
  const { can } = useAuth();
  const toast = useToast();
  const { data, loading, error, reload } = useLoad(() => apiService.getAll(), [], { initial: [] });
  const [search, setSearch] = useState('');
  const [editing, setEditing] = useState(null); // null = closed, {} = creating, item = editing
  const [form, setForm] = useState({});
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState('');
  const [toDelete, setToDelete] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const name = singular || title.replace(/s$/, '');
  const isEdit = !!editing?.id;
  const fields = useMemo(
    () => formFields.filter((f) => !(isEdit ? f.createOnly : f.editOnly)),
    [formFields, isEdit]
  );

  const rows = Array.isArray(data) ? data : [];
  const filtered = rows.filter((row) =>
    !search || JSON.stringify(row).toLowerCase().includes(search.toLowerCase())
  );

  function openCreate() {
    setEditing({});
    setForm({ ...initialValues });
    setFormError('');
  }

  function openEdit(item) {
    setEditing(item);
    setForm({ ...item });
    setFormError('');
  }

  function payload() {
    const out = {};
    for (const f of fields) {
      let v = form[f.name];
      if (f.type === 'number') v = v === '' || v == null ? null : Number(v);
      else if (f.type === 'select') v = v === '' ? null : v;
      else if (f.type === 'checkbox') v = !!v;
      else if (v === '') v = null;
      out[f.name] = v;
    }
    return out;
  }

  async function handleSave(e) {
    e.preventDefault();
    setSaving(true);
    setFormError('');
    try {
      if (isEdit) await apiService.update(editing.id, payload());
      else await apiService.create(payload());
      toast.success(`${name} ${isEdit ? 'updated' : 'created'}`);
      setEditing(null);
      reload();
    } catch (err) {
      setFormError(errorMessage(err, `Failed to save ${name.toLowerCase()}`));
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    setDeleting(true);
    try {
      await apiService.delete(toDelete.id);
      toast.success(`${name} deleted`);
      setToDelete(null);
      reload();
    } catch (err) {
      toast.error(errorMessage(err, `Failed to delete ${name.toLowerCase()}`));
      setToDelete(null);
    } finally {
      setDeleting(false);
    }
  }

  const canWrite = can(writePolicy);
  const canDelete = can(deletePolicy);

  return (
    <div>
      <PageHeader title={title} subtitle={subtitle}>
        {canWrite && (
          <button className="btn btn-primary" onClick={openCreate}>
            <Plus size={16} /> Add {name}
          </button>
        )}
      </PageHeader>

      <div className="search-bar" style={{ position: 'relative' }}>
        <Search size={18} style={{ position: 'absolute', left: 12, top: 10, color: '#64748b' }} />
        <input
          aria-label={`Search ${title}`}
          placeholder={`Search ${title.toLowerCase()}...`}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{ paddingLeft: 38 }}
        />
      </div>

      <div className="card">
        <DataState
          loading={loading}
          error={error}
          onRetry={reload}
          empty={filtered.length === 0}
          emptyTitle={search ? 'No matches' : `No ${title.toLowerCase()} yet`}
          emptyText={search ? 'Try a different search term.' : canWrite ? `Use "Add ${name}" to create the first record.` : undefined}
        >
          <table className="data-table">
            <thead>
              <tr>
                {columns.map((col) => <th key={col.key}>{col.label}</th>)}
                {(canWrite || canDelete) && <th>Actions</th>}
              </tr>
            </thead>
            <tbody>
              {filtered.map((item) => (
                <tr key={item.id}>
                  {columns.map((col) => (
                    <td key={col.key}>{col.render ? col.render(item[col.key], item) : (item[col.key] ?? '-')}</td>
                  ))}
                  {(canWrite || canDelete) && (
                    <td>
                      <div className="row-actions">
                        {canWrite && (
                          <button className="btn btn-sm btn-outline" onClick={() => openEdit(item)} aria-label={`Edit ${item.name || item.code || ''}`}>
                            <Edit2 size={13} />
                          </button>
                        )}
                        {canDelete && (
                          <button className="btn btn-sm btn-danger" onClick={() => setToDelete(item)} aria-label={`Delete ${item.name || item.code || ''}`}>
                            <Trash2 size={13} />
                          </button>
                        )}
                      </div>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </DataState>
      </div>

      {editing && (
        <Modal title={isEdit ? `Edit ${name}` : `New ${name}`} onClose={() => setEditing(null)}>
          <form onSubmit={handleSave}>
            <ErrorBanner message={formError} />
            {fields.map((field) => (
              <Field key={field.name} label={field.label}>
                {(id) => field.type === 'select' ? (
                  <Select id={id} value={form[field.name]} required={field.required}
                    options={field.options || []} onChange={(v) => setForm((p) => ({ ...p, [field.name]: v }))} />
                ) : field.type === 'textarea' ? (
                  <textarea id={id} className="form-input" rows={3} value={form[field.name] ?? ''}
                    onChange={(e) => setForm((p) => ({ ...p, [field.name]: e.target.value }))} placeholder={field.placeholder || ''} />
                ) : field.type === 'checkbox' ? (
                  <input id={id} type="checkbox" checked={!!form[field.name]}
                    onChange={(e) => setForm((p) => ({ ...p, [field.name]: e.target.checked }))} />
                ) : (
                  <input id={id} type={field.type || 'text'} className="form-input" value={form[field.name] ?? ''}
                    onChange={(e) => setForm((p) => ({ ...p, [field.name]: e.target.value }))}
                    placeholder={field.placeholder || ''} required={field.required && !(isEdit && field.type === 'password')}
                    min={field.min} step={field.step} minLength={field.minLength} />
                )}
              </Field>
            ))}
            <div className="modal-footer">
              <button type="button" className="btn btn-secondary" onClick={() => setEditing(null)}>Cancel</button>
              <button type="submit" className="btn btn-primary" disabled={saving}>{isEdit ? 'Update' : 'Create'}</button>
            </div>
          </form>
        </Modal>
      )}

      {toDelete && (
        <Confirm
          title={`Delete ${name}`}
          message={`Delete "${toDelete.name || toDelete.code || toDelete.username || toDelete.locationCode}"? This cannot be undone.`}
          confirmLabel="Delete"
          danger
          busy={deleting}
          onConfirm={handleDelete}
          onCancel={() => setToDelete(null)}
        />
      )}
    </div>
  );
}
