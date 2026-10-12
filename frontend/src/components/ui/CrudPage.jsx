import { useState, useEffect } from 'react';
import { Plus, Search, Edit2, Trash2, X } from 'lucide-react';

export default function CrudPage({
  title,
  subtitle,
  apiService,
  columns,
  formFields,
  initialValues = {},
  onBeforeSave,
}) {
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [showForm, setShowForm] = useState(false);
  const [editingItem, setEditingItem] = useState(null);
  const [form, setForm] = useState(initialValues);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    loadData();
  }, []);

  async function loadData() {
    setLoading(true);
    try {
      const res = await apiService.getAll();
      setData(res.data?.data || res.data || []);
    } catch (err) {
      console.error('Failed to load:', err);
    } finally {
      setLoading(false);
    }
  }

  function openCreate() {
    setEditingItem(null);
    setForm({ ...initialValues });
    setShowForm(true);
  }

  function openEdit(item) {
    setEditingItem(item);
    setForm({ ...item });
    setShowForm(true);
  }

  function handleFieldChange(name, value) {
    setForm(prev => ({ ...prev, [name]: value }));
  }

  async function handleSave(e) {
    e.preventDefault();
    setSaving(true);
    try {
      if (onBeforeSave) onBeforeSave(form);
      if (editingItem) {
        await apiService.update(editingItem.id, form);
      } else {
        await apiService.create(form);
      }
      setShowForm(false);
      loadData();
    } catch (err) {
      alert(err.response?.data?.message || 'Failed to save');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(id) {
    if (!confirm('Are you sure you want to delete this item?')) return;
    try {
      await apiService.delete(id);
      loadData();
    } catch {
      alert('Failed to delete');
    }
  }

  const filtered = data.filter(item =>
    search === '' || Object.values(item).some(v =>
      String(v).toLowerCase().includes(search.toLowerCase())
    )
  );

  return (
    <div>
      <div className="page-header">
        <div>
          <h1 className="page-title">{title}</h1>
          {subtitle && <p className="page-subtitle">{subtitle}</p>}
        </div>
        <button className="btn btn-primary" onClick={openCreate}>
          <Plus size={16} /> Add New
        </button>
      </div>

      <div className="search-bar">
        <Search size={18} style={{position:'absolute',left:12,top:10,color:'#64748b'}} />
        <input
          placeholder={`Search ${title.toLowerCase()}...`}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          style={{paddingLeft:38}}
        />
      </div>

      <div className="card">
        {loading ? (
          <div style={{display:'flex',justifyContent:'center',padding:40}}><div className="spinner"></div></div>
        ) : filtered.length === 0 ? (
          <div className="empty-state">
            <h3>No data found</h3>
            <p>Get started by adding your first record</p>
          </div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>{columns.map(col => <th key={col.key}>{col.label}</th>)}<th>Actions</th></tr>
            </thead>
            <tbody>
              {filtered.map(item => (
                <tr key={item.id}>
                  {columns.map(col => (
                    <td key={col.key}>
                      {col.render ? col.render(item[col.key], item) : item[col.key]}
                    </td>
                  ))}
                  <td>
                    <div style={{display:'flex',gap:6}}>
                      <button className="btn btn-sm btn-outline" onClick={() => openEdit(item)}>
                        <Edit2 size={13} />
                      </button>
                      <button className="btn btn-sm btn-danger" onClick={() => handleDelete(item.id)}>
                        <Trash2 size={13} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Modal Form */}
      {showForm && (
        <div className="modal-overlay" onClick={() => setShowForm(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>{editingItem ? `Edit ${title}` : `New ${title}`}</h2>
              <button className="btn btn-sm btn-secondary" onClick={() => setShowForm(false)}>
                <X size={16} />
              </button>
            </div>
            <form onSubmit={handleSave}>
              {formFields.map(field => (
                <div className="form-group" key={field.name}>
                  <label className="form-label">{field.label}</label>
                  {field.type === 'select' ? (
                    <select
                      className="form-select"
                      value={form[field.name] || ''}
                      onChange={(e) => handleFieldChange(field.name, e.target.value)}
                      required={field.required}
                    >
                      <option value="">Select...</option>
                      {(field.options || []).map(opt => (
                        <option key={opt.value} value={opt.value}>{opt.label}</option>
                      ))}
                    </select>
                  ) : field.type === 'textarea' ? (
                    <textarea
                      className="form-input"
                      rows={3}
                      value={form[field.name] || ''}
                      onChange={(e) => handleFieldChange(field.name, e.target.value)}
                      placeholder={field.placeholder || ''}
                    />
                  ) : field.type === 'checkbox' ? (
                    <input
                      type="checkbox"
                      checked={!!form[field.name]}
                      onChange={(e) => handleFieldChange(field.name, e.target.checked)}
                    />
                  ) : (
                    <input
                      type={field.type || 'text'}
                      className="form-input"
                      value={form[field.name] ?? ''}
                      onChange={(e) => handleFieldChange(field.name, e.target.value)}
                      placeholder={field.placeholder || ''}
                      required={field.required}
                      min={field.min}
                      max={field.max}
                      step={field.step}
                    />
                  )}
                </div>
              ))}
              <div style={{display:'flex',gap:10,justifyContent:'flex-end',marginTop:20}}>
                <button type="button" className="btn btn-secondary" onClick={() => setShowForm(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary" disabled={saving}>
                  {saving ? <span className="spinner"></span> : (editingItem ? 'Update' : 'Create')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

// No re-export needed — pages import services directly
