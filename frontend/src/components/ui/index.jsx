import { createContext, useCallback, useContext, useEffect, useId, useMemo, useRef, useState } from 'react';
import { X } from 'lucide-react';
import './ui.css';

export function Spinner() {
  return <div className="spinner" role="progressbar" aria-label="Loading" />;
}

export function ErrorBanner({ message, onRetry }) {
  if (!message) return null;
  return (
    <div className="alert-error" role="alert">
      {message}
      {onRetry && <button className="btn btn-sm btn-outline" onClick={onRetry}>Retry</button>}
    </div>
  );
}

export function EmptyState({ title = 'Nothing here yet', children }) {
  return (
    <div className="empty-state">
      <h3>{title}</h3>
      {children && <p>{children}</p>}
    </div>
  );
}

/** Shows a spinner, the error (with retry) or an empty state, otherwise its children. */
export function DataState({ loading, error, empty, emptyTitle, emptyText, onRetry, children }) {
  if (loading) return <div className="center-box"><Spinner /></div>;
  if (error) return <ErrorBanner message={error} onRetry={onRetry} />;
  if (empty) return <EmptyState title={emptyTitle}>{emptyText}</EmptyState>;
  return children;
}

export function PageHeader({ title, subtitle, children }) {
  return (
    <div className="page-header">
      <div>
        <h1 className="page-title">{title}</h1>
        {subtitle && <p className="page-subtitle">{subtitle}</p>}
      </div>
      {children && <div className="row-actions">{children}</div>}
    </div>
  );
}

export function Modal({ title, onClose, children, footer, wide = false }) {
  const titleId = useId();
  const ref = useRef(null);
  const closeRef = useRef(onClose);
  useEffect(() => { closeRef.current = onClose; });
  // Focus the dialog once when it opens (not on every re-render, which would steal focus from inputs)
  useEffect(() => {
    const onKey = (e) => { if (e.key === 'Escape') closeRef.current(); };
    document.addEventListener('keydown', onKey);
    ref.current?.focus();
    return () => document.removeEventListener('keydown', onKey);
  }, []);
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className={`modal ${wide ? 'wide' : ''}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        ref={ref}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="modal-header">
          <h2 id={titleId}>{title}</h2>
          <button className="btn btn-sm btn-secondary" onClick={onClose} aria-label="Close"><X size={16} /></button>
        </div>
        {children}
        {footer && <div className="modal-footer">{footer}</div>}
      </div>
    </div>
  );
}

export function Field({ label, children, hint }) {
  const id = useId();
  return (
    <div className="form-group">
      <label className="form-label" htmlFor={id}>{label}</label>
      {typeof children === 'function' ? children(id) : children}
      {hint && <div className="muted" style={{ marginTop: 4 }}>{hint}</div>}
    </div>
  );
}

export function Select({ id, value, onChange, options, placeholder = 'Select...', required, disabled }) {
  return (
    <select id={id} className="form-select" value={value ?? ''} onChange={(e) => onChange(e.target.value)} required={required} disabled={disabled}>
      <option value="">{placeholder}</option>
      {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
    </select>
  );
}

export function Confirm({ title, message, confirmLabel = 'Confirm', danger, onConfirm, onCancel, busy }) {
  return (
    <Modal
      title={title}
      onClose={onCancel}
      footer={(
        <>
          <button className="btn btn-secondary" onClick={onCancel}>Cancel</button>
          <button className={`btn ${danger ? 'btn-danger' : 'btn-primary'}`} onClick={onConfirm} disabled={busy}>{confirmLabel}</button>
        </>
      )}
    >
      <p style={{ color: '#cbd5e1', fontSize: 14 }}>{message}</p>
    </Modal>
  );
}

// ---- toasts
const ToastContext = createContext(null);

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);
  const push = useCallback((kind, message) => {
    const id = Math.random().toString(36).slice(2);
    setToasts((t) => [...t, { id, kind, message }]);
    setTimeout(() => setToasts((t) => t.filter((x) => x.id !== id)), kind === 'error' ? 7000 : 3500);
  }, []);
  const api = useMemo(() => ({ success: (m) => push('success', m), error: (m) => push('error', m) }), [push]);
  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="toast-stack" role="status" aria-live="polite">
        {toasts.map((t) => <div key={t.id} className={`toast ${t.kind}`}>{t.message}</div>)}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used within ToastProvider');
  return ctx;
}
