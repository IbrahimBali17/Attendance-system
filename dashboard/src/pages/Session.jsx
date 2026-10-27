import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { QRCodeSVG } from 'qrcode.react';
import { API_URL, api, errorMessage, storage } from '../api.js';

const ROTATE_SECONDS = 28;
const RING = 2 * Math.PI * 54;

function CountdownRing({ rotatedAt }) {
  const [left, setLeft] = useState(ROTATE_SECONDS);
  useEffect(() => {
    const id = setInterval(() => setLeft(Math.max(0, ROTATE_SECONDS - (Date.now() - rotatedAt) / 1000)), 250);
    return () => clearInterval(id);
  }, [rotatedAt]);
  return (
    <svg className="ring" viewBox="0 0 120 120" aria-label={`QR refreshes in ${Math.ceil(left)} seconds`}>
      <circle cx="60" cy="60" r="54" className="ring-bg" />
      <circle cx="60" cy="60" r="54" className="ring-fg" strokeDasharray={RING} strokeDashoffset={RING * (1 - left / ROTATE_SECONDS)} />
      <text x="60" y="68" textAnchor="middle" className="ring-text">{Math.ceil(left)}s</text>
    </svg>
  );
}

export default function Session() {
  const { courseId } = useParams();
  const [overview, setOverview] = useState(null);
  const [roomId, setRoomId] = useState('');
  const [session, setSession] = useState(null);
  const [live, setLive] = useState({ enrolled: 0, checkedIn: 0, entries: [] });
  const [qr, setQr] = useState(null);
  const [rotatedAt, setRotatedAt] = useState(Date.now());
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [connected, setConnected] = useState(false);
  const connRef = useRef(null);

  const course = overview?.courses.find((c) => c.id === courseId);
  const active = session && session.status === 'Active';

  const loadLive = useCallback(async (id) => {
    const { data } = await api.get(`/api/sessions/${id}/live`);
    setSession(data.session);
    setLive({ enrolled: data.enrolled, checkedIn: data.checkedIn, entries: data.entries });
  }, []);

  const loadQr = useCallback(async (id) => {
    const { data } = await api.get(`/api/sessions/${id}/qr`);
    setQr(data);
    setRotatedAt(Date.now());
  }, []);

  // initial load: course info + resume an active session if there is one
  useEffect(() => {
    (async () => {
      try {
        const { data } = await api.get('/api/professor/courses');
        setOverview(data);
        setRoomId(data.classrooms[0]?.id ?? '');
        const c = data.courses.find((x) => x.id === courseId);
        if (c?.activeSessionId) {
          await loadLive(c.activeSessionId);
          await loadQr(c.activeSessionId);
        }
      } catch (e) {
        setError(errorMessage(e));
      }
    })();
  }, [courseId, loadLive, loadQr]);

  // real-time channel while a session is active
  useEffect(() => {
    if (!active) return undefined;
    const sessionId = session.id;
    const conn = new HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/attendance`, { accessTokenFactory: () => storage.read()?.accessToken ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connRef.current = conn;

    conn.on('QrRotated', (dto) => { setQr(dto); setRotatedAt(Date.now()); });
    conn.on('AttendanceUpdated', ({ entry, checkedIn, enrolled }) =>
      setLive((prev) => ({
        enrolled,
        checkedIn,
        entries: [...prev.entries.filter((e) => e.studentId !== entry.studentId), entry].sort((a, b) => a.checkedInAt.localeCompare(b.checkedInAt)),
      })));
    conn.on('SessionEnded', () => { setSession((s) => (s ? { ...s, status: 'Ended' } : s)); setQr(null); });
    conn.onreconnected(async () => {
      setConnected(true);
      await conn.invoke('JoinSession', sessionId);
      loadLive(sessionId).catch(() => {});
      loadQr(sessionId).catch(() => {});
    });
    conn.onclose(() => setConnected(false));

    conn.start()
      .then(() => conn.invoke('JoinSession', sessionId))
      .then(() => setConnected(true))
      .catch((e) => setError(`Live connection failed: ${e.message}`));

    return () => {
      conn.stop();
      setConnected(false);
    };
  }, [active, session?.id, loadLive, loadQr]);

  const start = async () => {
    setBusy(true);
    setError('');
    try {
      const { data } = await api.post('/api/sessions', { courseId, classroomId: roomId });
      setSession(data);
      setLive({ enrolled: course?.enrolled ?? 0, checkedIn: 0, entries: [] });
      await loadQr(data.id);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  };

  const end = async () => {
    if (!window.confirm('End this session? Students will no longer be able to check in.')) return;
    setBusy(true);
    try {
      const { data } = await api.post(`/api/sessions/${session.id}/end`);
      setSession(data);
      setQr(null);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  };

  if (!overview && !error) return <p className="muted">Loading...</p>;

  return (
    <>
      <div className="row between">
        <div>
          <Link to="/" className="muted">&larr; My Courses</Link>
          <h1>{course ? `${course.code} - ${course.name}` : 'Live session'}</h1>
        </div>
        {session && <span className={`chip ${active ? 'live' : ''}`}>{active ? 'LIVE' : 'ENDED'}</span>}
      </div>
      {error && <div className="alert error" role="alert">{error}</div>}

      {!session && overview && (
        <div className="card start">
          <h2>Start a session</h2>
          <p className="muted">A new QR code appears every 28 seconds. Students must also be within the classroom radius.</p>
          <label>
            Classroom
            <select value={roomId} onChange={(e) => setRoomId(e.target.value)}>
              {overview.classrooms.map((r) => <option key={r.id} value={r.id}>{r.name} ({r.radiusMeters} m)</option>)}
            </select>
          </label>
          <button className="btn primary" onClick={start} disabled={busy || !roomId}>Start session</button>
        </div>
      )}

      {session && (
        <div className="session-grid">
          <div className="card qr-card">
            {active && qr ? (
              <>
                <div className="qr-box" data-testid="qr-code">
                  <QRCodeSVG value={qr.payload} size={300} level="M" marginSize={2} />
                  <CountdownRing rotatedAt={rotatedAt} />
                </div>
                <p className="muted">Students scan this code with the attendance app.</p>
                <p className={`conn ${connected ? 'ok' : 'down'}`}>{connected ? 'Live feed connected' : 'Reconnecting...'}</p>
                <button className="btn danger" onClick={end} disabled={busy}>End session</button>
              </>
            ) : (
              <>
                <h2>Session ended</h2>
                <p className="muted">{live.checkedIn} of {live.enrolled} students checked in.</p>
                <Link className="btn primary" to={`/analytics/${courseId}`}>View analytics</Link>
              </>
            )}
          </div>

          <div className="card">
            <div className="row between">
              <h2>Checked in</h2>
              <div className="counter" data-testid="counter"><strong>{live.checkedIn}</strong> / {live.enrolled}</div>
            </div>
            <div className="bar"><div style={{ width: `${live.enrolled ? (live.checkedIn / live.enrolled) * 100 : 0}%` }} /></div>
            <div className="table-wrap">
              <table>
                <thead><tr><th>Time</th><th>Student</th><th>ID</th><th>Status</th></tr></thead>
                <tbody>
                  {[...live.entries].reverse().map((e) => (
                    <tr key={e.studentId}>
                      <td>{new Date(e.checkedInAt + (e.checkedInAt.endsWith('Z') ? '' : 'Z')).toLocaleTimeString()}</td>
                      <td>{e.fullName}</td>
                      <td>{e.universityId}</td>
                      <td><span className={`badge ${e.status === 'Late' ? 'late' : 'present'}`}>{e.status}</span></td>
                    </tr>
                  ))}
                  {live.entries.length === 0 && <tr><td colSpan="4" className="muted">Waiting for students...</td></tr>}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
