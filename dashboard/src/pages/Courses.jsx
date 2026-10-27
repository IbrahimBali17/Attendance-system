import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, errorMessage } from '../api.js';

export default function Courses() {
  const [data, setData] = useState(null);
  const [error, setError] = useState('');

  useEffect(() => {
    api.get('/api/professor/courses').then((r) => setData(r.data)).catch((e) => setError(errorMessage(e)));
  }, []);

  if (error) return <div className="alert error">{error}</div>;
  if (!data) return <p className="muted">Loading courses...</p>;

  return (
    <>
      <h1>My Courses</h1>
      {data.courses.length === 0 && <p className="muted">No courses assigned to you yet.</p>}
      <div className="grid">
        {data.courses.map((c) => (
          <div className="card course" key={c.id}>
            <div className="row between">
              <span className="chip">{c.code}</span>
              {c.activeSessionId && <span className="chip live">LIVE</span>}
            </div>
            <h2>{c.name}</h2>
            <p className="muted">{c.semester} - {c.enrolled} students - alert below {c.thresholdPercent}%</p>
            <div className="row gap">
              <Link className="btn primary" to={`/session/${c.id}`}>{c.activeSessionId ? 'Resume live session' : 'Live session'}</Link>
              <Link className="btn" to={`/analytics/${c.id}`}>Analytics</Link>
            </div>
          </div>
        ))}
      </div>
    </>
  );
}
