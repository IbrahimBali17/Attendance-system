# REST API

Interactive docs: `/swagger` (enabled in Development, or with `Swagger__Enabled=true`). Errors use RFC 7807 `application/problem+json`.

| Method | Path | Role | Notes |
|---|---|---|---|
| POST | `/api/auth/login` | - | `{login, password}` (email or university ID) -> `{accessToken, refreshToken, expiresAt, user}` |
| POST | `/api/auth/refresh` | - | `{refreshToken}`; refresh tokens are single-use |
| POST | `/api/auth/register-device` | any | `{fcmToken}` (FCM or `ExponentPushToken[...]`) |
| GET | `/api/student/courses` | Student | courses + attendance % |
| GET | `/api/student/active-session` | Student | `{sessions: [...]}` (empty list = "No Active Session") |
| POST | `/api/attendance/check-in` | Student | `{sessionId, token, latitude, longitude}` |
| GET | `/api/student/attendance-history?courseId=` | Student | per-course records |
| GET | `/api/student/notifications` | Student | absence alerts |
| GET | `/api/professor/courses` | Professor | courses, enrolled count, active session, classrooms |
| POST | `/api/sessions` | Professor | `{courseId, classroomId, latitude?, longitude?}` -> 201 |
| POST | `/api/sessions/{id}/end` | Professor | idempotent |
| GET | `/api/sessions/{id}/live` | Professor | enrolled / checked-in / entries |
| GET | `/api/sessions/{id}/qr` | Professor | current token + payload |
| GET | `/api/courses/{id}/analytics` | Professor | per-session rate, per-student %, at-risk list, distribution |
| GET | `/api/courses/{id}/report/excel` | Professor | `.xlsx` |
| GET | `/api/courses/{id}/report/word` | Professor | `.docx` |
| GET | `/health` | - | PostgreSQL + Redis check |

## Check-in status codes
`201` ok - `400` missing/invalid GPS - `401` no/invalid JWT - `403` not a student, not enrolled, or too far (`distanceMeters` in body) -
`404` unknown session - `409` session ended or duplicate check-in - `410` QR expired/invalid - `429` rate-limited.

## SignalR: `/hubs/attendance` (Professor, JWT as `?access_token=`)
Invoke `JoinSession(sessionId)` / `LeaveSession(sessionId)`. Server events: `QrRotated`, `AttendanceUpdated`, `SessionEnded`.
