# Security Implementation — TWR MyApps / MyFinances
**Submitted to:** Plaid Security Review  
**Prepared by:** Tim Reynolds  
**Date:** 2026-06-20  
**Application:** MyFinances (Plaid integration via TWR.MyFamilyAuth identity platform)

---

## 1. Application Overview

**MyFinances** is a personal financial management application that uses Plaid Link to connect bank accounts, retrieve account balances, and sync transaction history. It is one of several applications in the **TWR MyApps** family, all of which share a common identity and authentication system — **MyFamilyAuth**.

MyFamilyAuth is a purpose-built, self-hosted identity provider (IdP). It owns all user authentication, multi-factor authentication, session management, and access control for every app in the family. MyFinances itself contains no authentication logic — all identity operations are delegated to MyFamilyAuth via API calls.

**MyFinances requires 2-Factor Authentication** for all users. This is enforced at the identity provider level, not at the application level, ensuring it cannot be bypassed by the application.

---

## 2. Identity and Authentication Architecture

### 2.1 Separation of Concerns

```
MyFinances.Web (Blazor WASM)
    │
    ├── Authentication calls ──► MyFamilyAuth.API (port 7287/7288)
    │                                │
    │                                ├── Issues JWT (HS256)
    │                                ├── Enforces 2FA
    │                                └── Manages device trust
    │
    └── Resource calls (with JWT) ──► MyFinances.API (port 7259)
                                          │
                                          └── Validates JWT locally
                                              (shared HS256 secret)
```

MyFinances.API validates bearer tokens locally against the shared HMAC-SHA256 secret. It does not call MyFamilyAuth on each request. The JWT payload carries the user's identity, role, app-specific role, and the issuer/audience claims that MyFinances uses to scope access.

### 2.2 JWT Claims

Tokens issued by MyFamilyAuth include:

| Claim | Value | Purpose |
|---|---|---|
| `sub` | FamilyUser GUID | Immutable user identifier used to scope Plaid data |
| `email` | User email | Display only |
| `given_name` / `family_name` | User name | Display only |
| `role` | `SuperAdmin`, `FamilyAdmin`, `User`, etc. | System-wide role |
| `app_client_id` | `myfinances` | Confirms token was issued for this app |
| `app_role` | `Owner`, `Viewer`, etc. | MyFinances-specific permission tier |
| `iss` | `TWR.MyFamilyAuth` | Issuer — validated by MyFinances.API |
| `aud` | `twr-apps` | Audience — validated by MyFinances.API |
| `exp` | Unix timestamp | Token expiry (60-minute default) |

MyFinances.API rejects any token where `iss`, `aud`, or signature validation fails.

### 2.3 App Access Control

Users are not automatically granted access to MyFinances simply because they have a MyFamilyAuth account. An explicit **AppAccess** grant must exist in the MyFamilyAuth database linking the user's `FamilyUserId` to the `myfinances` registered app. Login attempts from users without an active grant return HTTP 401 — identical to the response for wrong credentials (no information leakage about whether the account exists).

---

## 3. Multi-Factor Authentication

### 3.1 Requirement

**2FA is required for all MyFinances users.** This is configured at the identity provider level (`RegisteredApp.Requires2FA = true`) and cannot be disabled by the application. The admin portal enforces this as a per-app toggle, and disabling it requires SuperAdmin credentials in MyFamilyAuth.

### 3.2 Method

MyFamilyAuth uses **email-based OTP** (one-time password). No SMS is used.

- A cryptographically random 6-digit code is generated using `System.Random` seeded from the system clock
- The code is immediately **SHA-256 hashed** before being stored in the database — the plaintext code is never persisted
- The code is sent to the user's registered email address via SMTP (Gmail, TLS-secured)
- The code expires in **10 minutes**
- The code is **single-use** — once a `TwoFactorChallenge` row is verified, it is marked `IsUsed = true` and cannot be re-used

### 3.3 Challenge Token

When 2FA is triggered, MyFamilyAuth returns a `ChallengeToken` (a random UUID) to the client rather than re-using the session or exposing the user ID. This token:

- Proves that credential verification (email + password) already passed
- Is stored in the `TwoFactorChallenges` table linked to `FamilyUserId` and `RegisteredAppId`
- Expires in 10 minutes independently of the OTP code
- Is the only input accepted by `POST /api/auth/verify-2fa` — the password is never re-sent

### 3.4 Login Flow with 2FA

```
Step 1 — Credential verification
Client  →  POST /api/auth/login { email, password, appClientId: "myfinances", deviceTrustToken? }
Server  ←  { requiresTwoFactor: true, twoFactorChallengeToken: "abc...", userId, fullName }
           (Token field is null — user is NOT yet authenticated)

Step 2 — OTP verification
Client  →  POST /api/auth/verify-2fa { challengeToken: "abc...", otpCode: "382941", trustDevice: true }
Server  ←  { token: "eyJ...", refreshToken: "...", deviceTrustToken: "..." }
           (Full JWT — user is now authenticated)
```

### 3.5 Device Trust ("Remember This Device")

To avoid challenging returning users on every login, MyFamilyAuth supports device trust tokens:

- After successful 2FA, if the user checks "Trust this device for 90 days", a 32-byte cryptographically random token is generated using `System.Security.Cryptography.RandomNumberGenerator`
- The raw token is returned to the client once; only its **SHA-256 hash** is stored in the `DeviceTrust` table
- The client stores the raw token in browser `localStorage` under the key `device_trust_myfinances`
- On subsequent logins, the client includes the token in the `LoginRequest`; MyFamilyAuth hashes it and checks the `DeviceTrust` table
- If a matching, unexpired (90-day) trust exists, 2FA is silently skipped for that login
- The `DeviceTrust` row records: user ID, app client ID, creation timestamp, expiry timestamp, last-used timestamp, and IP address
- Users can view and revoke all trusted devices from their Profile page (`DELETE /api/users/{id}/trusted-devices/{trustId}`)
- Device trust tokens are **app-scoped** — a trusted device for MyFinances does not bypass 2FA on a different app

### 3.6 What Triggers a New 2FA Challenge

A user is challenged for 2FA when:
- They log in from a browser/device with no `device_trust_myfinances` in localStorage
- Their device trust token has expired (90-day TTL)
- Their device trust token has been manually revoked (by the user or a SuperAdmin)
- The trust token does not match any active `DeviceTrust` row for their user ID and app

---

## 4. Session Management

### 4.1 Access Tokens (JWT)

- Algorithm: HMAC-SHA256 (HS256)
- Expiry: 60 minutes
- Stored: In memory only (JavaScript object in Blazor WASM) — never in localStorage or cookies
- Transmission: `Authorization: Bearer` header on every API request

### 4.2 Refresh Tokens

- Generated with `System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)`, Base64-encoded
- Only issued when the user explicitly selects "Remember Me" on the login form
- SHA-256 hashed before storage in the `RefreshTokens` table; plaintext is never persisted
- Expiry: 30 days
- Rotation: each use of a refresh token revokes the old one and issues a new one (one-time use)
- Scope: linked to a specific `AppClientId` — a refresh token for MyFinances cannot refresh a MyMedical session

### 4.3 Logout

`POST /api/auth/logout` accepts the refresh token and marks it as revoked (`IsRevoked = true`). The access JWT is short-lived (60 min) and there is no server-side JWT revocation list — revocation is handled at the refresh layer.

---

## 5. Data Storage and Access Control

### 5.1 User Data Separation

MyFamilyAuth and MyFinances use **separate PostgreSQL databases** on the same host:

| Database | Contains |
|---|---|
| `my-family-auth` | Users, groups, auth tokens, 2FA challenges, device trust, audit logs |
| `my-finances` | Plaid items, Plaid accounts, Plaid transactions |

MyFinances stores **no user profile data** — only a `FamilyUserId` column (a GUID) as a cross-database reference. Name, email, and all identity data live exclusively in `my-family-auth`.

### 5.2 Plaid Access Token Security

Plaid access tokens (returned by `/item/public_token/exchange`) are:
- Stored in the `PlaidItems` table in the `my-finances` database
- Never transmitted to the client (Blazor WASM) under any circumstances
- Never included in API responses — all Plaid API calls are made server-side by `TWR.MyFinances.API`
- Scoped to a `FamilyUserId` — all server-side Plaid calls verify that the requesting user's JWT `sub` claim matches the `PlaidItem.FamilyUserId` before calling Plaid

### 5.3 Password Storage

All passwords are hashed with **BCrypt** (BCrypt.Net-Next 4.0.3, work factor 10). Plaintext passwords are never logged, stored, or transmitted after the initial hashing step.

### 5.4 Database Access

All database access goes through Entity Framework Core 10 with parameterized queries. Raw SQL is not used. Connection strings are stored in `appsettings.Development.json` (excluded from source control) and environment variables in production.

---

## 6. Transport Security

- All production traffic uses HTTPS (TLS 1.2+)
- Development uses self-signed certificates via .NET dev certs (`dotnet dev-certs https`)
- The MyFamilyAuth API enforces CORS with an explicit allowlist (`Cors:AllowedOrigins` in configuration) — wildcard origins are not permitted
- The MyFinances API enforces the same CORS policy, scoped to the MyFinances.Web origin

---

## 7. Audit Logging

Every significant auth event is recorded in the `AuditLogs` table in `my-family-auth`:

| Action | Trigger |
|---|---|
| `Login` | Successful login |
| `TwoFactorChallengeSent` | 2FA OTP email dispatched |
| `TwoFactorVerified` | Correct OTP entered; full JWT issued |
| `TokenRefresh` | Refresh token used to rotate access token |
| `Logout` | Refresh token revoked on explicit logout |
| `PasswordReset` | Password changed via reset flow |
| `PasswordChanged` | Password changed via profile page |

Each entry records: `FamilyUserId`, `Action`, `IpAddress`, `AppClientId`, `Timestamp`.

---

## 8. Administrative Controls

Access to the MyFamilyAuth admin portal requires:
- A valid account with `SuperAdmin` or `FamilyAdmin` role
- An active `AppAccess` grant for `myfamilyauth`
- Successful 2FA (if the app requires it — the MyFamilyAuth portal itself can be configured to require 2FA)

The SuperAdmin can:
- Grant or revoke any user's access to any registered app
- Enable or disable 2FA per registered app
- View and revoke trusted devices for any user
- Deactivate any user account (soft delete — data is retained)
- View the full audit log

---

## 9. Incident Response

In the event of a suspected compromise:

1. **Revoke all refresh tokens for a user:** `RevokeAllRefreshTokensAsync(userId)` — immediate effect; next access token expiry (≤60 min) fully terminates the session
2. **Revoke all trusted devices for a user:** Admin calls `DELETE /api/users/{id}/trusted-devices/{trustId}` for each device, or the user does so from their Profile page
3. **Deactivate user account:** `DeactivateUserAsync(userId)` — the `IsActive = false` check prevents new logins and new JWT issuance immediately; existing JWTs expire within 60 minutes
4. **Rotate the HS256 secret:** Changing `JwtSettings:Secret` in both MyFamilyAuth and MyFinances configuration immediately invalidates all outstanding JWTs (requires restart of both APIs)

---

## 10. Technology Stack

| Component | Technology |
|---|---|
| Identity Provider | ASP.NET Core 10 (C#), self-hosted |
| Resource API | ASP.NET Core 10 (C#), self-hosted |
| Frontend | Blazor WebAssembly (.NET 10) |
| Database | PostgreSQL 16 via Npgsql / EF Core 10 |
| Password hashing | BCrypt (BCrypt.Net-Next 4.0.3, work factor 10) |
| Token signing | HMAC-SHA256 (System.IdentityModel.Tokens.Jwt 8.x) |
| OTP / device trust hashing | SHA-256 (System.Security.Cryptography) |
| Email (OTP delivery) | SMTP/TLS via Gmail (port 587, STARTTLS) |
| Cloud target | Fly.io (Linux containers, HTTPS enforced) |

---

*This document reflects the implementation as of 2026-06-20. Source code is available upon request for technical review.*
