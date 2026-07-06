# TWR.MyFamilyAuth

**MyFamilyAuth** is the centralized identity and access management hub for the TWR MyApps family of products. It provides a single sign-on service, user and group management, two-factor authentication, and per-app access control for all family applications.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Projects](#projects)
- [Technology Stack](#technology-stack)
- [API Reference](#api-reference)
- [Integrating a New App](#integrating-a-new-app)
- [User Roles](#user-roles)
- [Two-Factor Authentication](#two-factor-authentication)
- [Local Development](#local-development)
- [Deployment](#deployment)
- [Git Workflow](#git-workflow)
- [Currently Integrated Apps](#currently-integrated-apps)

---

## Overview

MyFamilyAuth acts as the **authentication and authorization gateway** for all TWR MyApps. Rather than each app managing its own users and passwords, every app delegates sign-in to MyFamilyAuth. This means:

- One set of credentials works across all family apps
- Access to each app is explicitly granted and can be revoked centrally
- Two-factor authentication is enforced per-app as needed
- User and group management is centralized in one admin interface

**Production URLs**
| Service | URL |
|---|---|
| Auth API | https://myfamilyauth-api.fly.dev |
| Admin Web App | https://myfamilyauth-web.fly.dev |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Client Apps                          │
│   MyFinances · MyMedical · MyMessages · TheFamilyInfo ...   │
└───────────────────────┬─────────────────────────────────────┘
                        │  POST /api/auth/login
                        │  POST /api/auth/refresh
                        │  POST /api/auth/validate
                        ▼
┌─────────────────────────────────────────────────────────────┐
│                  MyFamilyAuth API                           │
│              https://myfamilyauth-api.fly.dev               │
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │ AuthController│  │UsersController│  │AppAccessController│ │
│  └──────────────┘  └──────────────┘  └──────────────────┘  │
│  ┌──────────────┐  ┌──────────────┐                        │
│  │GroupsController  │AdminController│                        │
│  └──────────────┘  └──────────────┘                        │
│                         │                                   │
│              ┌──────────▼──────────┐                        │
│              │    DataAccess (DAL)  │                        │
│              └──────────┬──────────┘                        │
│                         │                                   │
│              ┌──────────▼──────────┐                        │
│              │  PostgreSQL (Fly)    │                        │
│              └─────────────────────┘                        │
└─────────────────────────────────────────────────────────────┘
                        │
                        │  JWT token returned to client
                        │  Client attaches token to all API calls
                        ▼
┌─────────────────────────────────────────────────────────────┐
│                   Client App's Own API                      │
│   Validates JWT using shared Secret + Issuer + Audience     │
│   Reads claims: sub (userId), email, role, appClientId      │
└─────────────────────────────────────────────────────────────┘
```

### Authentication Flow

1. The user enters their email and password in the client app's login screen.
2. The client app sends credentials to `POST /api/auth/login` along with its `AppClientId` and optionally a `DeviceTrustToken`.
3. MyFamilyAuth validates credentials, checks the user has access to that app, and checks whether 2FA is required.
4. If 2FA is required (and device is not trusted), a challenge token is returned. The app prompts the user for their code, then calls `POST /api/auth/verify-2fa`.
5. On success, MyFamilyAuth returns a **JWT access token** and a **refresh token**.
6. The client app stores the tokens and attaches the JWT as a `Bearer` token on all subsequent API calls to its own backend.
7. The client app's API validates the JWT locally (no network call to MyFamilyAuth required).
8. When the JWT nears expiry, the client calls `POST /api/auth/refresh` to get a new token pair.

---

## Projects

| Project | Type | Purpose |
|---|---|---|
| `TWR.MyFamilyAuth.API` | ASP.NET Core 10 | REST API — authentication, users, groups, app access |
| `TWR.MyFamilyAuth.DAL` | Class Library | Entity Framework Core data access and migrations |
| `TWR.MyFamilyAuth.Contracts` | Class Library | Shared DTOs and API route constants |
| `TWR.MyFamilyAuth.Web` | Blazor WebAssembly | Admin web application |
| `TWR.MyFamilyAuth.Tests` | xUnit | Unit and integration tests |

---

## Technology Stack

- **.NET 10** / ASP.NET Core 10
- **Entity Framework Core 10** with **PostgreSQL** (Fly Postgres in production — `myfamilyauth-db`, NOT Neon; verified 2026-07-05)
- **Blazor WebAssembly** (Bootstrap 5, Bootstrap Icons)
- **JWT Bearer** authentication with BCrypt password hashing
- **Serilog** for structured logging
- **Docker** + **Fly.io** for hosting
- **GitHub Actions** for CI/CD

---

## API Reference

All endpoints are under `https://myfamilyauth-api.fly.dev`.

### Auth (`/api/auth`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/login` | Anonymous | Sign in with email, password, and appClientId |
| POST | `/api/auth/refresh` | Anonymous | Exchange a refresh token for a new token pair |
| POST | `/api/auth/logout` | Anonymous | Revoke a refresh token |
| POST | `/api/auth/validate` | Anonymous | Validate a JWT and return its claims |
| POST | `/api/auth/verify-2fa` | Anonymous | Complete a 2FA challenge |
| POST | `/api/auth/forgot-password` | Anonymous | Request a password reset email |
| POST | `/api/auth/reset-password` | Anonymous | Complete a password reset |

### Users (`/api/users`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/users` | Bearer | List users (paged, searchable) |
| POST | `/api/users` | Bearer | Create a new user |
| GET | `/api/users/{id}` | Bearer | Get a user by ID |
| PUT | `/api/users/{id}` | Bearer | Update a user |
| DELETE | `/api/users/{id}` | Bearer | Deactivate a user |
| POST | `/api/users/{id}/change-password` | Bearer | Change a user's password |
| GET | `/api/users/{id}/trusted-devices` | Bearer | List trusted devices |
| DELETE | `/api/users/{id}/trusted-devices/{trustId}` | Bearer | Revoke a trusted device |

### App Access (`/api/app-access`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/app-access/by-app/{appId}` | Bearer | List all users with access to an app |
| GET | `/api/app-access/by-user/{userId}` | Bearer | List all apps a user has access to |
| POST | `/api/app-access` | Bearer | Grant a user access to an app |
| DELETE | `/api/app-access/{id}` | Bearer | Revoke access |

### Groups (`/api/groups`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/groups` | Bearer | List all groups with members |
| POST | `/api/groups` | Bearer | Create a group |
| PUT | `/api/groups/{id}` | Bearer | Update a group |
| POST | `/api/groups/{id}/members` | Bearer | Add a member to a group |
| DELETE | `/api/groups/{id}/members/{userId}` | Bearer | Remove a member |

### Admin (`/api/admin`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/admin/apps` | Bearer (Admin) | List all registered apps |
| POST | `/api/admin/apps` | Bearer (Admin) | Register a new app |
| PUT | `/api/admin/apps/{id}` | Bearer (Admin) | Update an app |
| POST | `/api/admin/apps/{id}/regenerate-secret` | Bearer (Admin) | Issue a new client secret |

---

## Integrating a New App

Follow these steps to wire a new TWR MyApp into MyFamilyAuth.

### Step 1 — Register the App

Log in to the **MyFamilyAuth admin web app** at https://myfamilyauth-web.fly.dev, go to **Admin → Apps**, and click **Register New App**.

- **Name:** Display name (e.g. `MyMedical`)
- **Allowed Origins:** Comma-separated list of your app's URLs (e.g. `https://mymedical.fly.dev,https://localhost:7100`)
- **Supported Roles:** The role names your app uses (e.g. `Owner`, `Viewer`, `Admin`)
- **Requires 2FA:** Check this if the app should always require two-factor authentication

After saving, **copy the Client ID and Client Secret immediately** — the secret is only shown once.

### Step 2 — Store the Credentials

Add the following as secrets in your app's environment (Fly.io secrets, local `.env`, etc.):

```
MyFamilyAuth__BaseUrl=https://myfamilyauth-api.fly.dev
MyFamilyAuth__ClientId=your-app-clientid
MyFamilyAuth__ClientSecret=your-client-secret
JwtSettings__Secret=<same secret used by MyFamilyAuth>
JwtSettings__Issuer=TWR.MyFamilyAuth
JwtSettings__Audience=twr-apps
```

> The `JwtSettings` values must **exactly match** the values configured in MyFamilyAuth so your API can validate tokens locally without a network call.

### Step 3 — Implement Login in Your App

Your login screen sends credentials to MyFamilyAuth on behalf of the user:

```http
POST https://myfamilyauth-api.fly.dev/api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "their-password",
  "appClientId": "your-app-clientid",
  "deviceTrustToken": null,
  "rememberMe": false
}
```

**Success response (no 2FA):**
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "abc123...",
  "expiresAt": "2026-06-22T14:00:00Z"
}
```

**2FA required response:**
```json
{
  "requiresTwoFactor": true,
  "challengeToken": "eyJ..."
}
```

If `requiresTwoFactor` is true, prompt the user for their code, then call:

```http
POST /api/auth/verify-2fa
{
  "challengeToken": "eyJ...",
  "code": "123456",
  "trustDevice": true,
  "appClientId": "your-app-clientid"
}
```

### Step 4 — Validate Tokens in Your API

Your app's backend validates JWTs **locally** — no call back to MyFamilyAuth needed. Configure JWT Bearer authentication using the shared settings:

```csharp
// In Program.cs
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = "TWR.MyFamilyAuth",
        ValidAudience            = "twr-apps",
        IssuerSigningKey         = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["JwtSettings:Secret"]!))
    });
```

### Step 5 — Read JWT Claims

The JWT contains these claims your app can use:

| Claim | Value | Example |
|---|---|---|
| `sub` | User ID (GUID) | `3fa85f64-...` |
| `email` | User's email | `user@example.com` |
| `role` | System role | `SuperAdmin`, `FamilyAdmin`, `User` |
| `appRole` | Role within your app | `Owner`, `Viewer` |
| `appClientId` | Which app issued the token | `myfinances` |

```csharp
var userId      = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
var email       = User.FindFirst(ClaimTypes.Email)?.Value;
var appRole     = User.FindFirst("appRole")?.Value;
```

### Step 6 — Handle Token Refresh

Access tokens expire (default 60 minutes). When your app receives a `401`, call:

```http
POST /api/auth/refresh
{ "refreshToken": "abc123..." }
```

Store the new token pair and retry the original request.

### Step 7 — Grant Users Access

Back in the **MyFamilyAuth admin web app**, go to **Admin → App Access**, select your new app, and grant each user access with the appropriate role. Users without a grant will receive a `401` even with valid credentials.

---

## User Roles

MyFamilyAuth has two tiers of roles: **system roles** (global) and **app roles** (per-app).

### System Roles

| Role | Description |
|---|---|
| `SuperAdmin` | Full access to everything — users, groups, apps, app access |
| `FamilyAdmin` | Manage users, groups, apps, and app access |
| `GroupAdmin` | Manage members within their own group |
| `User` | Standard user — can sign in and manage their own profile |

### App Roles

App roles are defined per registered app and are returned in the `appRole` JWT claim. Examples:

| App | Roles |
|---|---|
| MyFamilyAuth | `SuperAdmin`, `FamilyAdmin`, `User` |
| MyFinances | `Owner`, `Viewer` |
| MyMedical | *(to be defined)* |

---

## Two-Factor Authentication

2FA is enforced at the **app level** — you can require it for sensitive apps (e.g. MyFinances) while keeping it optional for others.

- When a user logs into an app with `Requires2FA = true`, they receive a challenge token instead of an access token.
- A verification code is emailed to the user.
- The user submits the code via `POST /api/auth/verify-2fa`.
- On success, they receive the normal access + refresh token pair.
- If the user chooses **Trust this device**, a `DeviceTrustToken` is stored locally. Subsequent logins from that device skip the 2FA prompt for the duration of the trust period.

---

## Local Development

### Prerequisites

- .NET 10 SDK
- Docker Desktop
- PostgreSQL (via Docker — see below)

### Start local dependencies

```bash
docker-compose up -d
```

This starts a local PostgreSQL instance on port `5432`.

### Run the API

```bash
dotnet run --project TWR.MyFamilyAuth.API
```

The API runs at `https://localhost:7087` / `http://localhost:5087`. Migrations and seed data are applied automatically on startup.

### Run the Web app

```bash
dotnet run --project TWR.MyFamilyAuth.Web
```

The admin web app runs at `https://localhost:7288` / `http://localhost:5288`.

### Default SuperAdmin credentials

Set in `appsettings.Development.json`:
- **Email:** `twreynol@hotmail.com`
- **Password:** see `Seed:SuperAdminPassword` in local config

---

## Deployment

Both the API and Web are containerized and deployed to **Fly.io** via GitHub Actions.

### Infrastructure

| Component | Fly App | Notes |
|---|---|---|
| API | `myfamilyauth-api` | Always-on (1 machine minimum), 512 MB RAM |
| Web | `myfamilyauth-web` | nginx serving Blazor WASM static files, 256 MB RAM |
| Database | Fly Postgres (`myfamilyauth-db`, unmanaged) | Reachable only via internal `myfamilyauth-db.flycast` address. ⚠️ Not Neon — verified 2026-07-05. No confirmed automated backup policy; needs one before Phase 7. |

### Fly.io Secrets (API)

These must be set on the `myfamilyauth-api` Fly app:

| Secret | Description |
|---|---|
| `ConnectionStrings__PostgreSQL` | Full connection string to Fly Postgres `myfamilyauth-db` (internal `.flycast` address — NOT Neon) |
| `JwtSettings__Secret` | Long random string (shared with all client apps) |
| `JwtSettings__Issuer` | `TWR.MyFamilyAuth` |
| `JwtSettings__Audience` | `twr-apps` |
| `JwtSettings__ExpiryMinutes` | `60` |
| `JwtSettings__RefreshTokenExpiryDays` | `30` |
| `Cors__AllowedOrigins__0` | Production URL of Web app |
| `Cors__AllowedOrigins__1` | Production URL of any client app |
| `EmailSettings__SmtpServer` | SMTP host for password reset / 2FA emails |
| `EmailSettings__SmtpUser` | SMTP username |
| `EmailSettings__SmtpPassword` | SMTP password |
| `Seed__SuperAdminEmail` | Initial super admin email |
| `Seed__SuperAdminPassword` | Initial super admin password |

---

## Git Workflow

```
local/dev  ──push──▶  remote/dev  ──PR──▶  remote/master  ──GitHub Action──▶  Fly.io
```

1. All development happens on the local `dev` branch.
2. When a milestone is reached, push `dev` to `origin/dev`.
3. Open a Pull Request from `dev` → `master` on GitHub.
4. GitHub Actions automatically builds and deploys both the API and Web to Fly.io on merge to `master`.
5. Test the production deployment at the Fly.io URLs above.

**Branch protection:** `master` is protected — direct pushes are blocked. All changes must go through a PR.

---

## Currently Integrated Apps

| App | Client ID | 2FA Required | Roles | Status |
|---|---|---|---|---|
| MyFamilyAuth (self) | `myfamilyauth` | No | SuperAdmin, FamilyAdmin, User | ✅ Live |
| MyFinances | `myfinances` | Yes | Owner, Viewer | ✅ Live |
| MyMedical | `mymedical` | TBD | TBD | 🔜 Planned |
| MyMessages | `mymessages` | TBD | TBD | 🔜 Planned |
| TheFamilyInfo | `thefamilyinfo` | TBD | TBD | 🔜 Planned |
