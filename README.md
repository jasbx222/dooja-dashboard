# ضوجة · DOOJA — ASP.NET Core 8 MVC + Web API

Arabic RTL administration dashboard and JSON API for [jasbx222/dooja-app](https://github.com/jasbx222/dooja-app). Run these commands from this folder (`dashboard/` inside the app repo, or the root of the dashboard repo).

## Run

Requires .NET SDK 8 (tested with **8.0.427**). The launch profile turns on Development and `Seed__Demo` so local booking samples exist. Production ignores that flag.

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet restore
dotnet build
dotnet run --project src/Dooja.Web --urls http://127.0.0.1:5080
```

Open **http://localhost:5080**. The launch profile selects Development. Without a launch profile:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Dooja.Web \
  --no-launch-profile --urls http://127.0.0.1:5080
```

Development dashboard login:

- Phone: `07700000000`
- Password: `Dooja.Dev!2026`

To use a different initial password, set `Admin__Password` **before the first database creation**. It is stored as an ASP.NET Identity PBKDF2 password hash, never returned by the API. Subsequent startups do not reset existing passwords.

Development app accounts:

- Customer `07700000001` — حسين علي
- Owner `07700000002` — أبو علي
- Request an OTP first, then verify with **1234**. A challenge lasts five minutes, allows five failed attempts and is consumed after successful verification. Auth endpoints are rate-limited.
- New verified numbers are created as Customers; only an authenticated dashboard Admin can assign Owner privileges. Admin phone login through the OTP API is deliberately prohibited.

## Real persistence versus sample records

**Every page, KPI and API reads/writes SQLite through EF Core.** There are no in-memory mock repositories or fabricated dashboard KPI values. Creating/editing a place in MVC immediately changes `/api/places`; app bookings immediately appear in MVC.

Development imports the reference app's **sample records** once: p1–p12, five original bookings, five owner requests, six offers including owner offers, reviews, reels/comments, favorites/follows, owner services, closed slot and notifications. These are **development seed examples, not verified real businesses, customers, payments or business activity**. The seed is isolated in `Data/Seed.cs`, not in views or request handlers. It is atomic and skipped if places already exist. Sample aggregate ratings/follower/engagement values are imported as-is; new operations persist real changes.

- SQLite defaults to `src/Dooja.Web/Dooja.db` when run with the project profile.
- Configure the connection with `ConnectionStrings__Dooja='Data Source=/absolute/path/Dooja.db'`.
- `Seed__Demo=false` disables demonstration imports, including in Development. Production defaults to false.
- A production database must be **new and separate from the development database**. Changing environment does not erase previously seeded data.
- Initial schema uses `EnsureCreatedAsync`, as requested. Back up DB files before changes; introduce EF migrations before evolving a deployed schema. Do not combine EnsureCreated with migrations on the same existing database without a migration plan.

## MVC pages

| URL | Features |
|---|---|
| `/Auth/Login` | Admin cookie login, antiforgery, rate limiting |
| `/` or `/Home` | Today's bookings and earned totals, places/users, pending requests, seven-day DB chart, latest bookings, top-followed places |
| `/Places` | Search, cards, create/edit, owner assignment, pricing/category/status/featured/visibility |
| `/Bookings` | Search and status filters, lifecycle transitions, arrival recording |
| `/Offers` | Create/edit, real counters, expiry date, activate/pause |
| `/Users` | Search, create/edit customers/owners, points, account activation; admin account protected |
| `/Reviews` | Read reviews, write/update replies, notification for identified reviewers |

All dashboard writes require Admin cookie authentication **and** antiforgery. Mutation actions use POST. Places are hidden instead of destructively deleting related bookings. Lists cap bookings/users/reviews at 300 rows; search/filter narrows these. API list limits are documented in controller source (places has `page`/`pageSize`).

KPI days use **Asia/Baghdad** for this Iraqi business. Income is the total of bookings with recorded arrival or completed status, **not a payment-provider settlement report**. UTC is used for machine timestamps (`createdAt`).

## Store

The shop is database-backed. Production does not import demonstration places: set `Seed__Demo=true` only on a disposable Development database. `Seed:Demo` in `appsettings.json` is false, and a non-Development host ignores the flag. No sample products are inserted. Cash on delivery is the only customer payment method. The dashboard refuses to enable online payment because no payment gateway is implemented. Checkout also rejects an online method unless `Payment__OnlineProvider` is set, which is only a server-side lock and does not charge a card.

Customer API (prices and stock are calculated on the server; client totals are ignored):

- `GET /api/shop/settings` `GET /api/shop/categories` `GET /api/shop/products` `GET /api/shop/products/{id}`
- `GET /api/cart` `POST /api/cart/items` `PUT /api/cart/items/{productId}` `DELETE /api/cart/items/{productId}` `POST /api/cart/merge`
- `GET /api/orders` `GET /api/orders/{id}` `POST /api/orders` `POST /api/orders/{id}/cancel`

`POST /api/orders` accepts `idempotencyKey`. Repeating it returns the original order and does not sell the stock twice. Dashboard pages: `/ShopProducts`, `/ShopCategories`, `/ShopOrders`, `/ShopInventory`, `/ShopReports`, `/ShopSettings`. Money is integer Iraqi dinars, the same unit as bookings, so totals are not floating-point.

## API conventions and examples

JSON uses camelCase. Protected routes need `Authorization: Bearer <token>`. Dashboard cookies cannot be used as API bearer credentials. JWTs expire in seven days. In Development, an ephemeral signing key is generated unless `Jwt__Key` is provided, so restarting invalidates old tokens. No refresh-token mechanism is implemented.

Dates: `YYYY-MM-DD`; times: 24-hour `HH:mm`; money: integer Iraqi dinars. All IDs in example records are stable (`p1`, `o1`); newly created place/offer IDs are random strings; booking/review/reel IDs are integers. Use booking `id` for mutations; `code` is the display reference.

```bash
BASE=http://localhost:5080
curl "$BASE/api/places"
curl -G "$BASE/api/places" --data-urlencode 'category=بادل' --data-urlencode 'q=المنصور'
curl -X POST "$BASE/api/auth/request-otp" -H 'Content-Type: application/json' \
  -d '{"phone":"07700000001"}'
curl -X POST "$BASE/api/auth/verify" -H 'Content-Type: application/json' \
  -d '{"phone":"07700000001","code":"1234"}'
# Copy token from { token, role, user } into TOKEN.
TOKEN='replace-with-response-token'
curl "$BASE/api/bookings" -H "Authorization: Bearer $TOKEN"
curl -X POST "$BASE/api/bookings" -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"placeId":"p2","day":"2026-12-21","time":"18:00","hours":2,"people":4,"payMethod":"كاش بالمكان","wait":false}'
curl -X POST "$BASE/api/favorites/p1/toggle" -H "Authorization: Bearer $TOKEN"
```

Booking requests accept an optional `offerId`. The server checks place ownership/availability of that offer, its active flag/expiry, calculates the total and increments its booking counter. Without an offer, `place.price × hours` for a `ساعة` unit, or `place.price × people` for per-person/child/ticket units. **Client-supplied totals, user IDs, statuses and roles are not trusted.** Payment method is descriptive only; no provider is charged. Requests start as `بانتظار القبول`, or `بالانتظار` when `wait=true`. Overlapping reservations and closed slots are checked server-side; current scheduling models each place as one reservable resource. Add capacities/resources for multi-court venues before production use.

### Public routes

- `POST /api/auth/request-otp` — `{ phone }`
- `POST /api/auth/verify` — `{ phone, code }` → `{ token, role, user }`
- `GET /api/places?category=&q=&page=1&pageSize=50`
- `GET /api/places/{id}`
- `GET /api/places/{id}/services`
- `GET /api/places/{id}/availability?day=YYYY-MM-DD` → `{ closed, booked }`, no customer data
- `GET /api/offers?category=&placeId=` — active, unexpired offers for visible places
- `GET /api/offers/{id}` — counts a view
- `GET /api/reviews?placeId=p1`
- `GET /api/reels`
- `GET /api/reels/{id}/comments`

### Authenticated customer/owner routes

- `GET /api/auth/me`; `PUT /api/auth/me` — `{ name, governorate, area, birth }`
- `GET /api/bookings`; `GET /api/bookings/{id}` — only the current user's bookings
- `POST /api/bookings` — `{ placeId, day, time, hours, people, payMethod, wait?, offerId? }`
- `POST /api/bookings/{id}/cancel`
- `POST /api/offers/{id}/click`
- `POST /api/reviews` — `{ placeId, bookingId, stars, text }`; own completed unrated booking required; awards 20 points once
- `POST /api/reviews/{id}/reply` — `{ text }`; place owner or admin only
- `GET /api/favorites`; `POST /api/favorites/{placeId}/toggle`
- `GET /api/places/following`; `POST /api/places/{id}/follow`
- `POST /api/reels/{id}/like`; `POST /api/reels/{id}/comments` — `{ text }`
- `POST /api/reels` — `{ placeId, caption, imageKey, offerText }`; owner/admin only
- `GET /api/notifications`; `POST /api/notifications/{id}/read`; `POST /api/notifications/read-all`

### Owner/admin routes (ownership checked)

- `GET /api/owner/dashboard` → `{ todayBookings, todayIncome, pending, places }`
- `GET, POST /api/owner/places`; `PUT /api/owner/places/{id}`
  - payload: `{ name, type, area, category, price, unit, imageKey, phone, kind }`
- `GET /api/owner/bookings?status=`
- `POST /api/owner/bookings/{id}/status` — `{ status }`
- `POST /api/owner/bookings/{id}/arrive`
- `GET, POST /api/owner/offers`; `PUT /api/owner/offers/{id}`
  - payload: `{ placeId, title, disc, newPrice, oldPrice, expiryText, expiresOn, active }`
- `POST /api/owner/offers/{id}/toggle`
- `GET /api/owner/reviews`
- `GET /api/owner/services/{placeId}`; `POST /api/owner/services`; `PUT, DELETE /api/owner/services/{id}`
  - payload: `{ placeId, name, price, unit }`
- `GET /api/owner/closed-slots/{placeId}`; `POST /api/owner/closed-slots`; `DELETE /api/owner/closed-slots/{id}`
  - payload: `{ placeId, day, time }`

Booking states use the Flutter vocabulary: `بانتظار القبول`, `بالانتظار`, `مؤكد`, `مكتمل`, `ملغي`. Completed/cancelled bookings cannot be reopened; confirmation checks conflicts. Active-user and current-role checks reject disabled accounts and stale-role bearer tokens. API authorization failures return 401/403 rather than a dashboard login redirect.

## Theme and app assets

`wwwroot/css/dooja.css` uses the exact Flutter palette: primary `#7C3AED`, light `#EDE9FE`, background `#F5F3FF`, dark primary `#5B21B6`, line `#ECE9F5`, ink `#111111`, muted `#6B6B7B`, success/warning/error/star tokens. IBM Plex Sans Arabic is loaded from Google Fonts. Clean RTL cards, purple buttons, responsive sidebar, mobile layouts and focus styles replace Bootstrap's blue defaults. No Bootstrap CDN/script is needed.

The original purple/white logo marks and ten bundled images were copied from the user's public Flutter repository into `wwwroot/logo` and `wwwroot/img`; no checkout was created. Their image keys match the app (`padel`, `football`, `billiard`, `gaming`, `pool`, `cafe`, `food`, `friends`, `reel`, `dice`). Preserve original asset rights; these sample photos are not proof of the listed businesses. Internet access is needed for the font CDN; Tahoma is the offline fallback.

## Verification

```bash
dotnet build
# Use a fresh, disposable development DB on a separate port:
ASPNETCORE_ENVIRONMENT=Development \
  ConnectionStrings__Dooja='Data Source=/tmp/dooja-test.db' \
  dotnet run --project src/Dooja.Web --no-launch-profile --urls http://127.0.0.1:5081
# In another terminal:
DOOJA_URL=http://127.0.0.1:5081 python3 scripts/smoke.py
```

The smoke test writes test records, so **never run it against production**. It verifies API auth/OTP, owner/customer isolation, CRUD persistence, price calculation, double-booking and closed-slot rejection, booking lifecycle, one-time reviews, offers, favorites/follows/likes, comments/notifications, cookie login, antiforgery rejection and HTML rendering of every MVC page. `scripts/shop_smoke.py` covers the store: categories, product images, cart merge, concurrent checkout, idempotent replay, cancellation, inventory, and sales totals. Neither script drives a phone.

## Production setup and remaining work

1. Host behind HTTPS and configure `AllowedHosts` for the real domain. Production uses HSTS and HTTPS redirection. Configure forwarded headers safely if deploying behind a trusted reverse proxy.
2. Use a separate empty production DB, `Seed__Demo=false`, a strong `Admin__Password` for its first startup and a secret **at least 32-byte** `Jwt__Key` (environment variables or a secret store, not source control). Back up SQLite and persist the cookie data-protection key directory when using containers. Scale beyond one instance with a shared key store/database and distributed rate limiting.
3. **SMS delivery is not implemented**: production OTP endpoints intentionally return 503 instead of accepting 1234. Integrate a real SMS OTP provider, secure challenge storage, expiry/attempt limits and verification before enabling production mobile login.
4. The Flutter app calls this API when built with `API_BASE_URL` (no `/api` suffix). Use `http://10.0.2.2:5080` on an Android emulator whose host runs the API, and a reachable HTTPS host on a physical phone. A server bound to 127.0.0.1 is not reachable from a phone. Set explicit CORS origins in production (`Cors__Origins__0`); Development allows any origin. Republish the hosted PWA with `./scripts/publish-flutter-web.sh` from `dashboard/`.
5. Card/ZainCash processing, refunds/settlement, real SMS, push delivery (FCM/APNs), reel video uploads, live distance/geolocation, capacity/resource configuration, and profile interests/settings remain integration work. Store product images (JPEG, PNG, WEBP, up to 4 MB) are saved under `wwwroot/uploads`. Notifications currently persist in the database and are retrieved by the app; no push is sent. Reels use the existing image keys and do not contain uploaded video files.

This is a working, persisted dashboard/API and Flutter client. Third-party SMS and payment providers are not configured.
