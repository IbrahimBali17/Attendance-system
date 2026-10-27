import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Bar, BarChart, CartesianGrid, Cell, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { api, downloadFile, errorMessage } from '../api.js';

const COLORS = ['#0b6b4f', '#4aa37f', '#e0a400', '#c0392b'];

export default function Analytics() {
  const { courseId } = useParams();
  const [a, setA] = useState(null);
  const [error, setError] = useState('');

  useEffect(() => {
    api.get(`/api/courses/${courseId}/analytics`).then((r) => setA(r.data)).catch((e) => setError(errorMessage(e)));
  }, [courseId]);

  const exportFile = (kind, ext) => downloadFile(`/api/courses/${courseId}/report/${kind}`, `${a.code}-attendance.${ext}`).catch((e) => setError(errorMessage(e)));

  if (error) return <div className="alert error">{error}</div>;
  if (!a) return <p className="muted">Loading analytics...</p>;

  const sessions = a.sessions.map((s) => ({
    date: new Date(s.startedAt + 'Z').toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
    Present: s.present, Late: s.late, Absent: s.absent, Rate: s.rate,
  }));

  return (
    <>
      <div className="row between">
        <div>
          <Link to="/" className="muted">&larr; My Courses</Link>
          <h1>{a.code} - {a.courseName}</h1>
        </div>
        <div className="row gap">
          <button className="btn" onClick={() => exportFile('excel', 'xlsx')}>Export Excel</button>
          <button className="btn" onClick={() => exportFile('word', 'docx')}>Export Word</button>
        </div>
      </div>

      <div className="stats">
        <div className="card stat"><span>Enrolled</span><strong>{a.enrolled}</strong></div>
        <div className="card stat"><span>Finished sessions</span><strong>{a.endedSessions}</strong></div>
        <div className="card stat"><span>Average attendance</span><strong>{a.averageRate}%</strong></div>
        <div className="card stat"><span>At risk (&lt; {a.thresholdPercent}%)</span><strong className={a.atRisk.length ? 'danger-text' : ''}>{a.atRisk.length}</strong></div>
      </div>

      <div className="charts">
        <div className="card">
          <h2>Attendance by session</h2>
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={sessions}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" />
              <YAxis allowDecimals={false} />
              <Tooltip />
              <Legend />
              <Bar dataKey="Present" stackId="a" fill="#0b6b4f" />
              <Bar dataKey="Late" stackId="a" fill="#e0a400" />
              <Bar dataKey="Absent" stackId="a" fill="#d9dcd9" />
            </BarChart>
          </ResponsiveContainer>
        </div>
        <div className="card">
          <h2>Attendance trend (%)</h2>
          <ResponsiveContainer width="100%" height={280}>
            <LineChart data={sessions}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" />
              <YAxis domain={[0, 100]} />
              <Tooltip />
              <Line type="monotone" dataKey="Rate" stroke="#0b6b4f" strokeWidth={3} dot />
            </LineChart>
          </ResponsiveContainer>
        </div>
        <div className="card">
          <h2>Students by attendance</h2>
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={a.distribution}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="label" />
              <YAxis allowDecimals={false} />
              <Tooltip />
              <Bar dataKey="count" name="Students">
                {a.distribution.map((_, i) => <Cell key={i} fill={COLORS[i]} />)}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div className="card">
        <h2>At-risk students</h2>
        {a.atRisk.length === 0 ? <p className="muted">No student is below the threshold.</p> : (
          <div className="table-wrap">
            <table>
              <thead><tr><th>ID</th><th>Name</th><th>Attended</th><th>Late</th><th>Attendance</th></tr></thead>
              <tbody>
                {a.atRisk.map((s) => (
                  <tr key={s.studentId}>
                    <td>{s.universityId}</td><td>{s.fullName}</td>
                    <td>{s.attended} / {s.total}</td><td>{s.late}</td>
                    <td><span className="badge absent">{s.percent}%</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </>
  );
}
