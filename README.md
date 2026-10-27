# Student Attendance Recording System via QR Code

The Hashemite University - Faculty of IT, Computer Information Systems.

Professors start a class session and show a **dynamic QR code** (new token every 28 s, 30 s lifetime).
Students scan it with their own phone. A check-in is accepted only if **both** layers pass:
**(1)** the QR token is valid and **(2)** the student's GPS position is **within 30 m** of the classroom.
This blocks proxy attendance without any classroom hardware.

| Part | Technology |
|---|---|
| **Backend** | **ASP.NET Core 8 Web API**, EF Core 8 + PostgreSQL 15, SignalR, JWT, BCrypt, Redis (Upstash), FCM, ClosedXML, OpenXML |
| Student app | React Native (Expo SDK 51), expo-camera, expo-location, expo-notifications |
| Professor dashboard | React 18 + Vite, SignalR client, Recharts, qrcode.react |
| Tests | xUnit + FluentAssertions + WebApplicationFactory, Playwright, k6 |
| CI/CD | GitHub Actions; Render (API), Vercel (dashboard), EAS (APK) |

```
backend/      ASP.NET Core API + xUnit tests          (Dockerfile, .sln)
dashboard/    Professor web dashboard                  (Vercel)
mobile/       Student Expo app                         (EAS -> Android APK)
e2e/          Playwright browser tests
loadtests/    k6 script - 50 concurrent students
docs/         Architecture, ER diagram, API reference
docker-compose.yml   PostgreSQL + Redis + API for local development
render.yaml          Render blueprint for the API
```

## Quick start (local)

```bash
# 1. Backend + PostgreSQL + Redis (demo data is seeded automatically)
docker compose up --build            # API: http://localhost:8080   Swagger: /swagger

# 2. Professor dashboard
cd dashboard && cp .env.example .env && npm install && npm run dev    # http://localhost:5173

# 3. Student app
cd mobile && cp .env.example .env && npm install && npx expo start
#   Android emulator:  EXPO_PUBLIC_API_URL=http://10.0.2.2:8080
#   Real phone (Expo Go): use http://<your-PC-LAN-IP>:8080
```

No Docker? Run the API alone: `cd backend/src/AttendanceSystem.Api && dotnet run` with
`Database__Provider=InMemory` (no PostgreSQL needed; data resets on restart).

### Demo accounts (seeded)
| Role | Login | Password |
|---|---|---|
| Professor | `prof@hu.edu.jo` | `Prof@12345` |
| Student | `student001@hu.edu.jo` ... `student060@hu.edu.jo` (or ID `20260001`...) | `Student@123` |

Seed data: 2 courses (CIS301 with 60 students, CIS320 with 40), 1 classroom, 8 past sessions per course with
realistic attendance (some students deliberately below 75% so analytics and alerts have data).
**Edit the classroom coordinates** in `Data/DbSeeder.cs` (or insert your real classroom row) before real use.

### Try the flow end to end
1. Dashboard -> *CIS301* -> **Start session** -> QR appears with a 28 s countdown.
2. Phone: sign in as a student -> *Check-in* -> scan. GPS must be within 30 m of the classroom coordinates.
   (To test away from the room, temporarily raise `RadiusMeters`, or start the session with the professor's own lat/lon.)
3. The student appears on the dashboard instantly (SignalR). End the session -> open *Analytics* -> export Excel/Word.

## Tests

```bash
cd backend && dotnet test                         # 9 black-box + 7 white-box + engine/QR tests
cd e2e && npm ci && npx playwright install chromium && npx playwright test   # needs stack + dashboard running
k6 run -e BASE_URL=http://localhost:8080 loadtests/checkin.js                # 50 concurrent students, p95 < 2 s
```

| ID | Case | Expected |
|---|---|---|
| BB-01 | valid QR + GPS | 201 Created |
| BB-02 | expired QR (31 s) | 410 Gone |
| BB-03 | GPS 45 m away | 403 Forbidden |
| BB-04 | student not enrolled | 403 Forbidden |
| BB-05 | duplicate check-in | 409 Conflict |
| BB-06 | no auth header | 401 Unauthorized |
| BB-07 | ended session | 409 Conflict |
| BB-08 | check-in after 10 min | 201, status `Late` |
| BB-09 | null GPS | 400 Bad Request |
| WB-01..07 | Haversine: same point = 0 m, known distance, symmetry, 29.9 m accepted / 30.1 m rejected, null/NaN/out-of-range rejected | |

## Deployment

1. **PostgreSQL (Railway):** create a PostgreSQL service, copy `DATABASE_URL`.
2. **Redis (Upstash):** create a database, copy the `rediss://default:<password>@<host>:6379` URL.
3. **API (Render):** New -> *Blueprint* -> select this repo (`render.yaml`). Set `DATABASE_URL`,
   `Redis__ConnectionString`, `AllowedOrigins` (your Vercel URL). `Jwt__Key` is generated for you.
   The schema is created on first start. Check `https://<api>.onrender.com/health`.
4. **Dashboard (Vercel):** import the repo, root directory `dashboard`, env `VITE_API_URL=https://<api>.onrender.com`.
5. **Push notifications (FCM):** create a Firebase project, add an Android app with package
   `jo.edu.hu.attendance`, put `google-services.json` in `mobile/`, base64 the service-account JSON
   (`base64 -w0 service-account.json`) into `Firebase__CredentialsBase64` on Render.
6. **Android APK (EAS):** `cd mobile && npm i -g eas-cli && eas login && eas init` (put the project ID in `app.json`),
   set `EXPO_PUBLIC_API_URL` in `eas.json`, then `eas build -p android --profile preview` -> download the `.apk`.

Render's free tier sleeps after inactivity; the first request after a nap takes ~30 s. Use a paid plan or a keep-alive
ping for live classes. Free Render also runs a single instance, which is what the in-process SignalR hub assumes;
to scale out, add the Redis backplane (`AddStackExchangeRedis`) to SignalR.

## Important notes

* **Database migrations:** the repo ships without generated EF migrations, so the API uses `EnsureCreated` on first
  start. To switch to migrations: `cd backend/src/AttendanceSystem.Api && dotnet ef migrations add Initial`
  (once, on a fresh database). From then on the API applies them automatically (`Database__AutoMigrate=true`).
* **Security:** JWT (60 min) + rotating one-time refresh tokens, BCrypt, rate limits (login 60/min per IP,
  check-in 20/min per user), RFC 7807 errors, security headers, CORS allow-list. Set `Security__HttpsRedirection=true`
  if your host does not already terminate TLS.
* **Honest limits of GPS + QR:** a determined student can still spoof GPS on a rooted phone or relay a QR photo to a
  friend inside the room. The 30 s token + 30 m radius raises the cost a lot; it is not absolute.
* **Not included yet (listed as future work in the project deck):** admin dashboard, e-mail notifications,
  multi-faculty rollout.
