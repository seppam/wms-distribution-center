import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Warehouse, Eye, EyeOff } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import './Login.css';

export default function Login() {
  const [form, setForm] = useState({ username: '', password: '' });
  const [showPwd, setShowPwd] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const { login } = useAuth();
  const navigate = useNavigate();

  async function handleSubmit(e) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await login(form.username, form.password);
      navigate('/');
    } catch (err) {
      setError(err.response?.data?.message || 'Invalid credentials');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-logo">
          <Warehouse size={40} color="#38bdf8" />
          <h1>WMS Distribution Center</h1>
          <p>Warehouse Management System</p>
        </div>

        <form onSubmit={handleSubmit} className="login-form">
          {error && <div className="alert-error">{error}</div>}

          <div className="form-group">
            <label className="form-label">Username</label>
            <input
              type="text"
              className="form-input"
              placeholder="Enter your username"
              value={form.username}
              onChange={(e) => setForm({ ...form, username: e.target.value })}
              required
              autoFocus
            />
          </div>

          <div className="form-group">
            <label className="form-label">Password</label>
            <div className="pwd-wrapper">
              <input
                type={showPwd ? 'text' : 'password'}
                className="form-input"
                placeholder="Enter your password"
                value={form.password}
                onChange={(e) => setForm({ ...form, password: e.target.value })}
                required
              />
              <button type="button" className="pwd-toggle" onClick={() => setShowPwd(!showPwd)}>
                {showPwd ? <EyeOff size={16} /> : <Eye size={16} />}
              </button>
            </div>
          </div>

          <button type="submit" className="btn btn-primary btn-full" disabled={loading}>
            {loading ? <span className="spinner"></span> : 'Sign In'}
          </button>

          {import.meta.env.DEV && (
            <p className="login-hint">Local demo: admin / Demo!Pass123 (Development seed only)</p>
          )}
        </form>
      </div>
    </div>
  );
}
