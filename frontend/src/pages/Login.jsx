import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Warehouse, Eye, EyeOff } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { errorMessage } from '../services/api';
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
      setError(err.response?.status === 401 ? 'Invalid username or password' : errorMessage(err, 'Sign in failed'));
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
          {error && <div className="alert-error" role="alert">{error}</div>}

          <div className="form-group">
            <label className="form-label" htmlFor="username">Username</label>
            <input
              id="username"
              type="text"
              autoComplete="username"
              className="form-input"
              placeholder="Enter your username"
              value={form.username}
              onChange={(e) => setForm({ ...form, username: e.target.value })}
              required
              autoFocus
            />
          </div>

          <div className="form-group">
            <label className="form-label" htmlFor="password">Password</label>
            <div className="pwd-wrapper">
              <input
                id="password"
                type={showPwd ? 'text' : 'password'}
                autoComplete="current-password"
                className="form-input"
                placeholder="Enter your password"
                value={form.password}
                onChange={(e) => setForm({ ...form, password: e.target.value })}
                required
              />
              <button type="button" className="pwd-toggle" aria-label="Toggle visibility" aria-pressed={showPwd} onClick={() => setShowPwd(!showPwd)}>
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
