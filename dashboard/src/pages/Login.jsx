import { useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth.jsx';
import { errorMessage } from '../api.js';

export default function Login() {
  const { user, login } = useAuth();
  const navigate = useNavigate();
  const [id, setId] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  if (user) return <Navigate to="/" replace />;

  const submit = async (e) => {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      await login(id, password);
      navigate('/');
    } catch (err) {
      setError(err.response?.status === 401 ? 'Invalid credentials.' : errorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="login-wrap">
      <form className="card login" onSubmit={submit}>
        <div className="logo big">HU</div>
        <h1>Professor Dashboard</h1>
        <p className="muted">The Hashemite University - Student Attendance via QR Code</p>
        <label>
          Email or university ID
          <input value={id} onChange={(e) => setId(e.target.value)} autoComplete="username" required />
        </label>
        <label>
          Password
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
        </label>
        {error && <div className="alert error" role="alert">{error}</div>}
        <button className="btn primary" disabled={busy}>{busy ? 'Signing in...' : 'Sign in'}</button>
      </form>
    </div>
  );
}
