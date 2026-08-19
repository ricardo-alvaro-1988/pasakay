# Ya! Pasakay

Motorcycle + tricycle taxi SaaS. Super Admin and Operator share the same portal. Customers book on web. Riders use the Flutter app.

## One-click install

Double-click **`Install.bat`**. It restores the API, installs both web apps, fetches Flutter packages if the SDK is present, then starts everything.

Already installed? Double-click **`Start.bat`**.

One local origin (`http://127.0.0.1:5174`):

- Customer: http://127.0.0.1:5174/
- Operator / Administrator: http://127.0.0.1:5174/ops/
- API (proxied on the same host): http://localhost:5088/health

Chrome or Edge can **Install app** on the customer site (Add to Home Screen).

## Stack

- `backend/` — ASP.NET Core 9 API, EF Core, SQL Server Express
- `web/admin/` — React portal for Super Admin and Operators
- `web/customer/` — customer booking web app
- `mobile/rider/` — Flutter rider app (Android + iOS)

## Database

```
Data Source=.\SQLEXPRESS01;Initial Catalog=YaPasakay;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Application Name=YaPasakay.Api
```

The API creates the `YaPasakay` catalog on first run.

## Sign in

OTP is always `1234` while SMS is not wired.

- Super Admin: `09000000000`
- Operator (Cebu Pasakay / Maria Santos): `09170001111`
- Customer: `09181110001` Rico (also `09181110003` Ben, `09181110004` Lina)
- Rider: `09171110003` Ana (tricycle, free for incoming jobs). Juan/Pedro/Lito already have live seed trips.

## Manual run

```powershell
cd backend
dotnet run --project YaPasakay.Api --launch-profile http
```

```powershell
cd web/admin
npm install
npm run dev
```

```powershell
cd web/customer
npm install
npm run dev
```

Keep both Vite processes running. Open only **http://127.0.0.1:5174/** (customer) and **http://127.0.0.1:5174/ops/** (operator / administrator). The customer app proxies `/ops`, `/api`, `/hubs`, and `/uploads` so the browser uses one origin.

## One-domain production

One hostname. Paths:

- `/` — customer
- `/ops/` — operator + administrator
- `/api/`, `/hubs/`, `/uploads/`, `/health` — API

1. Set `PublicOrigin` to `https://yapasakay.com` in [backend/YaPasakay.Api/appsettings.Production.json](backend/YaPasakay.Api/appsettings.Production.json). Replace the JWT key and SQL connection.
2. Google Cloud: allow that origin for Maps HTTP referrers (`https://yapasakay.com/*`) and OAuth JavaScript origins (`https://yapasakay.com` and `https://www.yapasakay.com`).
3. Build the webs into the API wwwroot:

```powershell
powershell -File deploy/sync-wwwroot.ps1
```

4. Point nginx at Kestrel using [deploy/nginx.conf](deploy/nginx.conf) (replace `YOUR_DOMAIN`).
5. Rider release build:

```powershell
cd mobile/rider
flutter build apk --dart-define=API_BASE=https://yapasakay.com
```

Debug rider builds also use `https://yapasakay.com`.
