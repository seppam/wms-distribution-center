import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Warehouse, Eye, EyeOff, FlaskConical } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import useDemoInfo from '../hooks/useDemoInfo';
import { errorMessage } from '../services/api';
import { demoPassword } from '../services/demo';
import './Login.css';

export default function Login() {
  const [form, setForm] = useState({ username: '', password: '' });
  const [showPwd, setShowPwd] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const { login } = useAuth();
  const navigate = useNavigate();
  const demo = useDemoInfo();
  const sharedPassword = demoPassword();

  async function handleSubmit(e) {
    e.preventDefault();
    await signIn(form.username, form.password);
  }

  async function signIn(username, password) {
    setError('');
    setLoading(true);
    try {
      await login(username, password);
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

          {import.meta.env.DEV && !demo.enabled && (
            <p className="login-hint">Local demo: admin / Demo!Pass123 (Development seed only)</p>
          )}
        </form>

        {demo.enabled && (
          <section className="demo-panel" aria-label="Demo mode">
            <h2><FlaskConical size={15} aria-hidden="true" /> Demo mode</h2>
            <p>This is a public demo with fictional data and shared accounts. Pick a role to explore, no sign-up needed.</p>
            {sharedPassword ? (
              <div className="demo-buttons">
                {demo.accounts.map((a) => (
                  <button key={a.role} type="button" className="btn btn-secondary btn-sm" disabled={loading}
                    onClick={() => signIn(a.username, sharedPassword)}>
                    Sign in as {a.role}
                  </button>
                ))}
              </div>
            ) : (
              <p className="demo-note">User names: {demo.accounts.map((a) => a.username).join(', ')}. The password is listed in the README.</p>
            )}
          </section>
        )}
      </div>
    </div>
  );
}
