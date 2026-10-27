import { Navigate, NavLink, Route, Routes } from 'react-router-dom';
import { useAuth } from './auth.jsx';
import Login from './pages/Login.jsx';
import Courses from './pages/Courses.jsx';
import Session from './pages/Session.jsx';
import Analytics from './pages/Analytics.jsx';

function Protected({ children }) {
  const { user } = useAuth();
  return user ? children : <Navigate to="/login" replace />;
}

function Shell({ children }) {
  const { user, logout } = useAuth();
  return (
    <div className="shell">
      <header className="topbar">
        <div className="brand">
          <span className="logo">HU</span>
          <span>Student Attendance</span>
        </div>
        <nav>
          <NavLink to="/">My Courses</NavLink>
        </nav>
        <div className="user">
          <span>{user?.fullName}</span>
          <button className="btn ghost" onClick={logout}>Sign out</button>
        </div>
      </header>
      <main className="content">{children}</main>
    </div>
  );
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/" element={<Protected><Shell><Courses /></Shell></Protected>} />
      <Route path="/session/:courseId" element={<Protected><Shell><Session /></Shell></Protected>} />
      <Route path="/analytics/:courseId" element={<Protected><Shell><Analytics /></Shell></Protected>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
