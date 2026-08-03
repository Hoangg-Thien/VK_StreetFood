<div align="center">

# VK StreetFood

**A location-based, multilingual audio tour guide platform for street-food discovery**

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API%20%2B%20MVC-512BD4?style=flat-square&logo=dotnet)
![MAUI](https://img.shields.io/badge/.NET%20MAUI-Android%20%2F%20-512BD4?style=flat-square&logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Supabase-4169E1?style=flat-square&logo=postgresql&logoColor=white)
![EF Core](https://img.shields.io/badge/EF%20Core-9.0-512BD4?style=flat-square)
![Docker](https://img.shields.io/badge/Docker-Multi--stage-2496ED?style=flat-square&logo=docker&logoColor=white)
![GitHub Actions](https://img.shields.io/badge/CI-GitHub%20Actions-2088FF?style=flat-square&logo=githubactions&logoColor=white)
![License](https://img.shields.io/badge/License-Unspecified-lightgrey?style=flat-square)

[Overview](#1-project-overview) • [Features](#2-features) • [Architecture](#3-architecture) • [Tech Stack](#4-technology-stack) • [Getting Started](#12-getting-started)

</div>

---

## 1. Project Overview

VK StreetFood is a full-stack platform that helps tourists explore the street-food vendors of Phố Vĩnh Khánh (District 4, Ho Chi Minh City) through GPS-triggered, multilingual audio narration.

**The problem it solves:** in a dense street-food area, visitors typically have no easy way to learn what a vendor sells, its history, or why it's worth stopping at — information is scattered, undocumented, or only available in Vietnamese.

**What it does:** the mobile app plots street-food Points of Interest (POIs) on a map, tracks the tourist's GPS position, and automatically plays a short audio narration (Vietnamese, English, or Korean) when the tourist enters a geofence around a POI. Tourists can also browse POIs manually, favorite them, rate them, view vendor details and opening hours, and scan a QR code that opens the mobile app (or, if not installed, a landing page to download it) directly to a given POI or screen. A companion admin/owner web portal lets staff manage POI content, translations, audio, tour groupings, and vendor-owner registration requests.

**Target users:**
- **Tourists** — anonymous, device-registered users of the mobile app.
- **Admins** — staff who manage POI/tour content, review owner registrations, and view analytics through the web portal.
- **Vendor owners** — food-stall owners who can register and submit content-change requests for their own listing through a self-service workflow.

**Why it was built:** to turn an informal, undocumented street-food district into a structured, discoverable, multilingual tourism product without requiring a human tour guide.

---

## 2. Features

> Only features with corresponding implementation in the codebase are listed below.

### Tourist Features
- Device-based tourist registration (`POST /api/Tourist/register`) issuing a JWT bearer token
- GPS location updates with nearby-POI detection
- Visit logging with a 5-minute duplicate-visit window
- Favorites (add / remove / list)
- POI ratings with recent-ratings display
- Per-tourist activity statistics

### POI & Navigation
- POI listing with category filtering and text search (name, description, address)
- Paginated POI listing (`/api/POI/paged`)
- Nearby-POI lookup by GPS coordinates and radius (Haversine distance)
- POI detail view including vendor info, opening hours, audio, tags, and recent ratings
- Category listing
- Tour listing and tour detail with ordered waypoints (`TourPointOfInterest`)

### QR App Launcher (not a real payment gateway)
- Each QR code encodes a landing-page URL (`/open-app?target=...`), not a transaction.
  Scanning it opens a page that either deep-links straight into the installed app
  (`vkstreetfood://{target}`) or falls back to the Android store listing if the
  app isn't installed yet.
- The "target" (e.g. `pay`) only tells the app which screen to land on after opening —
  there is no server-side transaction, amount capture, or payment processing involved.
  `DefaultAmountVnd` and `QrTtlMinutes` exist as configurable metadata for that landing
  screen, not as a real payment amount/expiry enforced by the backend.
- Admin can configure the deep-link host, default display amount, and QR validity
  window (`/Payment` in the web portal), and view a log of `qr_payment` /
  `qr_payment_success` / `qr_payment_failed` **analytics events** — these are
  client-reported UI events for tracking scan-to-open funnel, not verified financial
  transactions.
- **Not implemented:** no payment gateway integration, no transaction ledger, no
  idempotency/webhook handling. This is intentionally out of scope for the current
  version — the QR flow's real job is distributing the mobile app (APK hosted on
  Supabase Storage) to tourists on-site who don't have it installed yet.

### Offline Support
- Route/map package endpoints for offline caching (`/api/Offline/route-package`, `/api/Offline/map-package`)
- Package status endpoints to check availability before download
- Mobile-side SQLite local cache and route-package service (`LocalPOIDatabase`, `RoutePackageService`, `OfflineAudioDownloader` in `VK.Mobile`)

### AI / TTS (Text-to-Speech)
- On-demand audio generation using `edge-tts` (Microsoft Edge's free TTS engine), invoked from .NET via a Python subprocess
- Task de-duplication for concurrent identical TTS requests (`AudioTaskManager`, registered as a singleton)
- Admin-only batch/per-POI audio generation endpoints
- Audio "hotset"/warmup endpoints for pre-generating commonly requested audio
- Per-language audio content lookup with fallback to Vietnamese

### Admin Portal (`VK.Web`)
- Session-authenticated admin and owner areas (separate base controllers enforcing role checks)
- POI, tour, and translation CRUD
- Audio content management
- Owner registration review/approval workflow (`PoiOwnerRegistration`)
- Content-change request workflow for vendor owners (`PoiContentChangeRequest`)
- QR app-launcher configuration management (deep-link host, display amount, QR validity window)
- Usage history view
- POI image upload to Supabase Storage

### Security
- JWT bearer authentication for the REST API (tourists and admins)
- Role-based authorization (`Admin` policy, `[Authorize(Roles = "Admin")]` on sensitive endpoints)
- Explicit ownership verification on tourist-scoped endpoints to prevent one tourist from accessing another's data
- PBKDF2-SHA256 password hashing (100,000 iterations, random salt, timing-safe comparison)
- Global exception-handling middleware
- Data-annotation request validation (`[Required]`, `[Range]`, `[MaxLength]`, `[EmailAddress]`)

### API
- REST API documented via Swagger/OpenAPI (development environment only)
- Consistent HTTP status code usage (`200`, `400`, `401`, `403`, `404`)
- Paginated response model (`PagedResponse<T>`) with `TotalPages`, `HasNext`, `HasPrevious`
- Dedicated health-check endpoint (`/healthz`) for container/platform probes

---

## 3. Architecture

VK StreetFood follows a **layered architecture** with a clear dependency direction: `VK.Core` (innermost, no outward dependencies) → `VK.Infrastructure` → `VK.API` / `VK.Web` (outermost). It borrows Clean Architecture's dependency-inversion principle for the domain layer but is not a strict/pure Clean Architecture implementation, since application services in `VK.API` work directly against `IQueryable<TEntity>` exposed by the repository layer rather than being fully persistence-agnostic.

### Layer Responsibilities

| Layer | Project | Responsibility |
|---|---|---|
| Domain | `VK.Core` | Entities, repository/unit-of-work interfaces, no dependency on EF Core or ASP.NET Core |
| Infrastructure | `VK.Infrastructure` | EF Core `DbContext`, entity configurations, migrations, repository/unit-of-work implementations, database seeding |
| Application/API | `VK.API` | Controllers, application services (business logic orchestration), JWT issuance, TTS generation, middleware |
| Presentation (Web) | `VK.Web` | ASP.NET Core MVC admin/owner portal, session-based auth, Razor views |
| Shared | `VK.Shared`, `VK.Contracts` | Cross-cutting DTOs, constants, password hashing utility, API response envelopes |
| Client | `VK.Mobile` | .NET MAUI app (MVVM, API client services, offline cache) |

### Key Design Decisions

- **Repository Pattern:** a generic `IRepository<TEntity>` (`Query`, `GetByIdAsync`, `AddAsync`, `Update`, `Remove`) is used for standard CRUD, plus two specialized repositories (`IPoiManagementRepository`, `ITourManagementRepository`) for queries that don't fit the generic shape. `Query()` returns `IQueryable<TEntity>`, so application services compose further LINQ filtering/`Include` calls on top of it — this keeps the repository layer thin but means EF Core–specific query composition is visible above the infrastructure layer.
- **Unit of Work:** `IUnitOfWork.SaveChangesAsync()` centralizes commit calls across the application services that use different repositories against the same `DbContext` instance.
- **DTO Mapping:** manual, explicit object-initializer mapping (no AutoMapper/Mapster) from entities to DTOs inside application services — favors readability and debuggability over automated mapping.
- **Dependency Injection:** all services, repositories, and the `DbContext` are registered in `Program.cs` via the built-in ASP.NET Core DI container, using constructor injection throughout. `AudioTaskManager` is deliberately registered as a **singleton** (rather than scoped) so that concurrent identical TTS requests across different HTTP requests can be deduplicated.
- **Soft Deletes:** all domain entities inherit a shared `BaseEntity` (`Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `DeletedAt`), and EF Core global query filters (`HasQueryFilter`) automatically exclude soft-deleted rows from all queries.
- **Two authentication models by design:** the REST API (consumed by the mobile app and any external client) uses stateless JWT bearer tokens; the MVC admin/owner portal uses server-side session state, since it is a traditional server-rendered application with its own login flow.
- **Dual-Layer Error Handling Strategy:**
  - **Application / Orchestration Layer:** Employs the **Result Pattern** via `ServiceResult<T>` for anticipated business and validation outcomes (e.g., entity conflicts, invalid credentials, resource not found from search/filters). This avoids the performance overhead of exception stack-trace allocation on normal request paths and provides explicit, predictable flow control in API controllers.
  - **Domain & Infrastructure Invariants / Outer Safety Net:** Uses a strongly typed **Domain Exception Hierarchy** (`DomainException`, `EntityNotFoundException`, `BusinessRuleViolationException`, `ForbiddenOperationException`) to protect strict domain invariants. Any escaping domain exceptions or unexpected runtime failures are intercepted at the HTTP boundary by `GlobalExceptionMiddleware`, which maps them to standard HTTP status codes (`404 NotFound`, `400 BadRequest`, `403 Forbidden`, `500 InternalServerError`) without leaking stack traces or internal implementation details.

---

## 4. Technology Stack

| Category | Technology |
|---|---|
| **Backend** | ASP.NET Core Web API (.NET 10), C# |
| **Frontend (Admin Portal)** | ASP.NET Core MVC, Razor Views, Bootstrap |
| **Mobile** | .NET MAUI (`net10.0-android`), CommunityToolkit.Mvvm |
| **Database** | PostgreSQL (hosted on Supabase) |
| **ORM / Data Access** | Entity Framework Core 9.0, Npgsql provider |
| **Authentication** | JWT Bearer (API) via `Microsoft.AspNetCore.Authentication.JwtBearer`; session-based auth (Web portal) |
| **Password Hashing** | PBKDF2-SHA256 (custom implementation, `System.Security.Cryptography`) |
| **Testing** | xUnit, Moq, EF Core InMemory provider, SQLite (in-memory, for integration tests), `Microsoft.AspNetCore.Mvc.Testing` |
| **CI/CD** | GitHub Actions |
| **Deployment** | Render.com (Docker-based web services) |
| **Maps (Mobile)** | Mapsui + OpenStreetMap |
| **QR Scanning (Mobile)** | ZXing.Net.Maui |
| **Audio Playback (Mobile)** | Plugin.Maui.Audio |
| **Speech (TTS)** | `edge-tts` (Microsoft Edge Read Aloud engine, invoked from .NET as a Python subprocess) |
| **File Storage** | Supabase Storage (POI images) |
| **API Documentation** | Swagger / Swashbuckle.AspNetCore |
| **Containerization** | Docker (multi-stage builds for both API and Web) |

---

## 5. Project Structure

```
VK_StreetFood/
├── src/
│   ├── Client/
│   │   └── VK.Mobile/              # .NET MAUI app (Android)
│   │       ├── Views/               # XAML pages
│   │       ├── ViewModels/          # MVVM view models
│   │       ├── Services/            # API clients, location, audio, offline cache
│   │       └── Platforms/           # Android-specific code
│   │
│   └── Server/
│       ├── VK.API/                  # REST API — controllers, app services, auth, TTS
│       │   ├── Controllers/
│       │   ├── Services/AppServices/
│       │   ├── Auth/                # JwtTokenService
│       │   └── Middlewares/         # GlobalExceptionMiddleware
│       │
│       ├── VK.Web/                  # ASP.NET Core MVC admin/owner portal
│       │   ├── Controllers/
│       │   ├── Views/
│       │   └── Services/            # Supabase storage, translation service
│       │
│       ├── VK.Core/                 # Domain layer — entities & interfaces, no external deps
│       │   ├── Entities/
│       │   └── Interfaces/
│       │
│       └── VK.Infrastructure/       # Data access layer
│           ├── Data/                # VKStreetFoodDbContext
│           ├── Configurations/      # EF Core fluent configurations
│           ├── Migrations/          # EF Core Code-First migrations
│           ├── Repositories/        # Repository + Unit of Work implementations
│           └── Seeds/               # DatabaseSeeder
│
├── src/Shared/
│   ├── VK.Contracts/                # API response envelopes (ApiResponse<T>, PagedResponse<T>)
│   └── VK.Shared/                   # Shared DTOs, constants, PasswordHasher
│
├── supabase/                        # SQL schema, RLS policies, seed data (Supabase-managed tables)
├── tests/
│   ├── VK.API.Tests/                # Unit + integration tests for the API
│   ├── VK.Core.Tests/                # Domain-layer tests
│   └── VK.Web.Tests/                 # MVC controller tests
│
├── Dockerfile.api / Dockerfile.web   # Multi-stage Docker builds
├── render.yaml                       # Render.com deployment configuration
└── .github/workflows/dotnet.yml      # CI pipeline
```

---

## 6. Authentication & Security

- **JWT Authentication:** the API issues JWT bearer tokens via `IJwtTokenService`. Tourists receive a token on registration (`POST /api/Tourist/register`); admins receive one on login (`POST /api/Auth/login`). Tokens are signed with HMAC-SHA256 and validated for issuer, audience, lifetime, and signing key on every request.
- **Role-Based Authorization:** an `AdminOnly` policy and `[Authorize(Roles = "Admin")]` attributes gate admin-only endpoints (audio generation, analytics, offline package management, admin diagnostics).
- **IDOR Protection:** every tourist-scoped endpoint (`/api/Tourist/{touristId}/...`) verifies that the JWT subject claim matches the requested `touristId` before proceeding, returning `403 Forbidden` on mismatch (admins are exempted and may access any tourist's data).
- **Password Storage:** admin/owner passwords are hashed with PBKDF2-SHA256, a 128-bit random salt, 100,000 iterations, and a 256-bit derived key. Verification uses `CryptographicOperations.FixedTimeEquals` to avoid timing attacks, and a dummy hash comparison is performed even when the account doesn't exist, to avoid leaking account existence through response timing.
- **Validation:** request DTOs use `System.ComponentModel.DataAnnotations` (`[Required]`, `[Range]`, `[MaxLength]`, `[EmailAddress]`); `[ApiController]`'s automatic model validation returns `400 Bad Request` for invalid payloads without additional boilerplate in each action.
- **Global Exception Handling:** `GlobalExceptionMiddleware` catches unhandled exceptions across the request pipeline, logs them, and returns a structured JSON `500` response.
- **Secret Management:** connection strings, JWT signing keys, and third-party API credentials are left blank in `appsettings.json` and supplied via environment variables at runtime (see `render.yaml`); the API throws on startup outside the `Testing` environment if `Jwt:Key` is not configured.

---

## 7. Testing

The solution includes three test projects, executed with **xUnit**:

- **`VK.Core.Tests`** — domain-layer unit tests.
- **`VK.API.Tests`**
  - *Unit tests* (`Unit/`) — application-service logic tested against the EF Core InMemory provider, covering POI listing/pagination/nearby search, tour retrieval, tourist operations, analytics, and audio controller behavior.
  - *Integration tests* (`Integration/`) — full HTTP-pipeline tests using `CustomWebApplicationFactory` (built on `Microsoft.AspNetCore.Mvc.Testing`) against a real SQLite-backed application instance, covering tourist registration/JWT issuance and analytics endpoints end to end.
- **`VK.Web.Tests`** — MVC controller tests (`HomeControllerTests`, `OwnerControllerTests`) for the admin/owner portal.

GitHub Actions automatically runs `VK.Core.Tests`, `VK.API.Tests` and `VK.Web.Tests` on every push and pull request to `main` (see [CI/CD](#8-cicd)).

```bash
# Run all tests locally
dotnet test

# Run a specific project
dotnet test tests/VK.API.Tests/VK.API.Tests.csproj
```

---

## 8. CI/CD

A GitHub Actions workflow (`.github/workflows/dotnet.yml`) runs on every push and pull request to `main`:

1. Checks out the repository
2. Sets up the .NET 10 SDK
3. Runs `dotnet test` for `VK.Core.Tests`
4. Runs `dotnet test` for `VK.API.Tests`
5. Runs `dotnet test` for `VK.Web.Tests`

Deployment is handled separately via **Render.com**, configured through `render.yaml`, which defines two Docker-based web services (`vk-api` and `vk-web`) built from `Dockerfile.api` and `Dockerfile.web` respectively, each with a health-check path and environment-variable-driven secrets. Deployment to Render triggers on push (`autoDeploy: true`) but is not currently wired as an explicit step inside the GitHub Actions workflow itself.

---

## 9. Database

- **Engine:** PostgreSQL, hosted on Supabase.
- **ORM:** Entity Framework Core, Code-First, via the `VKStreetFoodDbContext` in `VK.Infrastructure`.
- **Migrations:** six incremental EF Core migrations are checked into `VK.Infrastructure/Migrations`, evolving the schema from the initial POI/tourist/audio model through tour support, audio metadata, and cleanup passes.
- **Entity Configuration:** each entity has an explicit `IEntityTypeConfiguration<T>` class defining constraints, precision, and relationships (e.g., `PointOfInterest`, `AudioContent`, `Tourist`, `Tour`, `VisitLog`).
- **Soft Deletes:** all entities share a `BaseEntity` with `IsDeleted`/`DeletedAt`, enforced globally through EF Core query filters.
- **Seeder:** `DatabaseSeeder` (in `VK.Infrastructure/Seeds`) seeds baseline POIs, translations, tours, tour translations, vendors, owner users, and the default admin account, and runs idempotently on startup in a background task so it doesn't block application boot.
- **Supabase-side SQL:** the `supabase/` folder additionally contains raw SQL assets (`schema.sql`, `rls.sql`, `seed_pois.sql`, delta migration scripts) used for Supabase-specific concerns such as Row Level Security policies, which sit outside the EF Core migration pipeline.

---

## 10. API

The REST API follows conventional resource-oriented routing (`/api/{Controller}/...`) with consistent use of HTTP verbs and status codes.

- **Pagination:** list endpoints that support it (e.g., `GET /api/POI/paged`) return a `PagedResponse<T>` envelope containing `Items`, `TotalCount`, `PageNumber`, `PageSize`, and computed `TotalPages`, `HasNext`, `HasPrevious`.
- **Standard response models:** shared DTOs (`VK.Shared.DTOs`) and response envelopes (`VK.Contracts.Responses`) are used across controllers for consistent shapes.
- **Validation:** enforced via data annotations on request models, with automatic `400 Bad Request` responses from `[ApiController]` model binding.
- **Documentation:** Swagger UI is available at `/swagger` when running in the Development environment, describing available endpoints and the two authentication flows (tourist registration, admin login).
- **Health check:** `GET /healthz` returns a simple status payload for container/platform liveness probes.

---

## 11. Screenshots

### Mobile

*(Add screenshots)*

### Admin Portal

*(Add screenshots)*

---

## 12. Getting Started

### Prerequisites

- .NET 10.0 SDK
- PostgreSQL database (a Supabase project is recommended, matching the current deployment setup)
- Python 3.x with `edge-tts` installed (required for TTS generation)
- For mobile development: the `maui` .NET workload, plus Android SDK (API 21+)

### Clone

```bash
git clone https://github.com/<your-username>/VK_StreetFood.git
cd VK_StreetFood
```

### Chạy nhanh bằng Docker Compose (khuyến khích cho local dev)

```bash
cp .env.example .env
docker compose up --build
```

- API: `http://localhost:5001/swagger`
- Web Admin: `http://localhost:5002`
- Database sẽ được seed tự động khi API khởi động (xem `DatabaseSeeder.cs`)

### Restore (Chạy thủ công không dùng Docker)

```bash
dotnet restore VKStreetFood.slnx
```

### Database Migration

```bash
cd src/Server/VK.API
dotnet ef database update --project ../VK.Infrastructure --startup-project .
```

> Set `ConnectionStrings__DefaultConnection` (via environment variable or `appsettings.Development.json`) before running migrations.

### Run the API

```bash
cd src/Server/VK.API
dotnet run
```

Swagger UI: `http://localhost:5089/swagger`

### Run the Web Portal

```bash
cd src/Server/VK.Web
dotnet run
```

### Run the Mobile App

```bash
cd src/Client/VK.Mobile

# Android
dotnet build -t:Run -f net10.0-android

```

Update the API base URL in the mobile app's service configuration to point at your running API instance (e.g., `10.0.2.2` for the Android emulator, or your machine's LAN IP for a physical device).

---

## 13. Implementation Highlights

My contribution to this project centered on the API's authentication, authorization, and security infrastructure, along with its automated testing and CI setup:

- Implemented **JWT bearer authentication** for the REST API, including token generation for both tourist and admin flows (`JwtTokenService`) and full `TokenValidationParameters` configuration (issuer, audience, lifetime, signing key validation).
- Implemented **role-based authorization**, including the `AdminOnly` policy and `[Authorize(Roles = "Admin")]` protection on sensitive endpoints (audio generation, analytics, offline package management).
- Designed and implemented **IDOR protection** on all tourist-scoped endpoints (`TouristController.VerifyOwnership`), ensuring a tourist's JWT subject claim must match the requested resource owner, with an explicit admin bypass.
- Implemented **PBKDF2-SHA256 password hashing** (`PasswordHasher`) with per-password salting, 100,000 iterations, and constant-time hash comparison to mitigate timing attacks, including a dummy-hash comparison path for non-existent accounts to prevent user enumeration.
- Added **request validation** using data annotations across request DTOs, relying on `[ApiController]`'s automatic model-state validation for consistent `400` responses.
- Implemented **pagination** for POI listing (`PagedResponse<T>` and `GET /api/POI/paged`), including total-page and has-next/has-previous computation.
- Extracted **shared security and utility infrastructure** into reusable helpers, including `LocalizationHelper` (language-code normalization and translation fallback) and `GeoHelper` (Haversine distance calculation), reducing duplication across application services.
- Set up the **GitHub Actions CI pipeline** (`.github/workflows/dotnet.yml`) to automatically run the `VK.Core.Tests`, `VK.API.Tests` and `VK.Web.Tests` suites on every push and pull request to `main`.
- Wrote **unit tests** for application services (`POIAppServiceTests`, `TourAppServiceTests`, `TouristAppServiceTests`, `AnalyticsAppServiceTests`) covering pagination, localization fallback, and distance-based filtering.
- Architected a **dual-layer error handling strategy**: standardized expected business outcomes with `ServiceResult<T>` across application services while protecting domain invariants with a custom `DomainException` hierarchy and `GlobalExceptionMiddleware` (covered by dedicated unit tests).
- Wrote **integration tests** using a custom `WebApplicationFactory` (`CustomWebApplicationFactory`) against a real HTTP pipeline and SQLite-backed database, verifying JWT issuance and end-to-end tourist/analytics endpoint behavior.
- Wrote **controller tests** for the admin/owner MVC portal (`HomeControllerTests`, `OwnerControllerTests`).
- **Refactored duplicated helper logic** — e.g., replacing ad-hoc language-code parsing and manual distance calculations scattered across services with the shared `LocalizationHelper` and `GeoHelper` utilities.

---

## 14. Future Improvements

Realistic, scoped improvements — not yet implemented:

- **Refresh tokens (Admin only)** — Admin JWTs currently use a long, fixed expiry (365 days) with no revocation mechanism; a refresh-token flow would allow shorter-lived access tokens for the credential-based Admin login. Tourist JWTs are intentionally excluded — they're per-device tokens issued via anonymous registration (no login step to avoid repeating), so a short-lived-access + refresh pattern doesn't fit; a simple revocation flag on the Tourist record would be a better fit there if device-level blocking is ever needed.
- **Rate limiting** — no rate limiting currently exists on authentication or registration endpoints.
- **Redis cache** — POI/category listings are re-queried from PostgreSQL on every request; a cache layer would reduce database load for largely static data.
- **Structured logging** — current logging uses `ILogger` with structured message templates, but there is no centralized log aggregation or distributed tracing (e.g., OpenTelemetry).
- **API versioning** — the API currently has no version segment (e.g., `/api/v1/`), which would ease future breaking changes for the mobile client.
- **Unified EF Core migration strategy** — some owner-registration schema changes are applied via a startup-time raw-SQL bootstrapper rather than EF Core migrations; consolidating onto a single migration path would reduce schema-drift risk.

---

## 15. License

This repository does not currently include a `LICENSE` file. If you intend to open-source this project, add one (MIT is a common choice for portfolio/personal projects) before relying on any license claim.

---

<div align="center">

[⬆ Back to top](#vk-streetfood)

</div>
