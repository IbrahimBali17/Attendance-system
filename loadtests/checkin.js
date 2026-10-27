// 50 students log in and check in at the same time.
//   k6 run -e BASE_URL=http://localhost:8080 loadtests/checkin.js
// Needs the seeded demo data (60 students in CIS301, Seed__Enabled=true).
import http from 'k6/http';
import { check, fail } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:8080';
const ROOM = { lat: 32.1030, lon: 36.1820 };   // must match the seeded classroom

export const options = {
  scenarios: {
    class_check_in: { executor: 'per-vu-iterations', vus: 50, iterations: 1, maxDuration: '2m' },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<2000'],
    'http_req_duration{name:check-in}': ['p(95)<2000'],
  },
};

http.setResponseCallback(http.expectedStatuses(200, 201));
const json = { headers: { 'Content-Type': 'application/json' } };

export function setup() {
  const login = http.post(`${BASE}/api/auth/login`, JSON.stringify({ login: 'prof@hu.edu.jo', password: 'Prof@12345' }), json);
  if (login.status !== 200) fail(`professor login failed: ${login.status}`);
  const auth = { headers: { Authorization: `Bearer ${login.json('accessToken')}`, 'Content-Type': 'application/json' } };

  const overview = http.get(`${BASE}/api/professor/courses`, auth).json();
  const course = overview.courses.find((c) => c.code === 'CIS301');
  let sessionId = course.activeSessionId;
  if (!sessionId) {
    const started = http.post(`${BASE}/api/sessions`, JSON.stringify({ courseId: course.id, classroomId: overview.classrooms[0].id }), auth);
    if (started.status !== 201) fail(`could not start session: ${started.status}`);
    sessionId = started.json('id');
  }
  return { auth, sessionId };
}

export default function (data) {
  const n = String(__VU).padStart(3, '0');
  const login = http.post(`${BASE}/api/auth/login`, JSON.stringify({ login: `student${n}@hu.edu.jo`, password: 'Student@123' }), json);
  if (!check(login, { 'student login 200': (r) => r.status === 200 })) return;

  // the QR the professor's screen is showing right now
  const qr = http.get(`${BASE}/api/sessions/${data.sessionId}/qr`, data.auth).json();
  const res = http.post(
    `${BASE}/api/attendance/check-in`,
    JSON.stringify({ sessionId: data.sessionId, token: qr.token, latitude: ROOM.lat + (__VU % 10) * 0.00001, longitude: ROOM.lon }),
    { headers: { Authorization: `Bearer ${login.json('accessToken')}`, 'Content-Type': 'application/json' }, tags: { name: 'check-in' } }
  );
  check(res, { 'check-in 201': (r) => r.status === 201 });
}

export function teardown(data) {
  http.post(`${BASE}/api/sessions/${data.sessionId}/end`, null, data.auth);
}
