# MyFamilyAuth — Architecture & Implementation Plan

**Date:** 2026-06-20  
**Status:** Planning  
**Author:** Claude (based on full survey of MyMedical, MyMessages, MyFinances, TheFamilyInfo)

---

## 1. Purpose

MyFamilyAuth is a standalone web application and identity provider for the entire TWR MyApps family. It owns:

- All human identity (users, profiles, avatars)
- All family/group relationships and hierarchy
- All authentication (login, tokens, password management)
- All authorization metadata (roles, membership, permissions)
- The Guardian/Ward and Buddy/Caregiver relationship models
- Invitation and onboarding flows

**Every other app becomes an OAuth/JWT consumer.** They stop maintaining their own `AuthUser` tables and instead call MyFamilyAuth to validate tokens and resolve user identity.

MyFinances will be the **first app migrated** to use MyFamilyAuth. The other apps are not changed yet.

---

## 2. What We Learned From the Survey

### 2.1 User Model — Consolidated Fields

| Field | MyMedical | TheFamilyInfo | MyMessages | MyFinances |
|---|---|---|---|---|
| Id (Guid) | `AuthUserId` | `UserId` | `Id` | `Id` |
| FirstName / LastName | ✓ | ✓ | ✓ | ✓ |
| Email | ✓ | ✓ | ✓ | ✓ |
| PasswordHash (BCrypt) | ✓ | ✓ | ✓ | ✓ |
| Role (string) | User/Admin/SysAdmin | User/GroupAdmin/FamilyAdmin/SuperAdmin | — | User |
| IsActive (soft delete) | ✓ | ✓ | — | — |
| MustChangePassword | ✓ | ✓ | — | — |
| PasswordChangeLocked | — | ✓ | — | — |
| AvatarBase64 / AvatarUrl | Base64 | URL | Base64 | — |
| IsWard | ✓ | — | — | — |
| GuardianId | ✓ | — | — | — |
| LastAccessed | ✓ | ✓ | — | — |
| TimeZoneId | ✓ (JWT claim) | — | — | — |
| PushToken / Platform | ✓ | — | — | — |

### 2.2 Group/Family Model

| Concept | MyMedical | TheFamilyInfo |
|---|---|---|
| Entity | `RegisteredGroup` | `FamilyGroup` |
| Hierarchy | Flat | Nested (`SuperGroupId`) |
| Encryption key per group | — | ✓ (root groups only) |
| Membership junction | FK on AuthUser | `UserToGroup` (with `IsLimitedMember`) |
| Admin junction | `RegisteredGroupAdmin` | Role on `AuthUser` |
| Invitations | ✓ (token, expiry, 14 days) | — |

### 2.3 Relationships Between People

| Pattern | Source | Notes |
|---|---|---|
| Guardian → Ward | MyMedical | Ward cannot log in; Guardian manages their data |
| Buddy → Grantor | MyMedical | Caregiver granted view access; revocable |
| Limited Member | TheFamilyInfo | `UserToGroup.IsLimitedMember` restricts child members |
| Cross-app link | MyMessages | `UserMapping` ties external user ID to messaging user |

### 2.4 JWT Claims Used Across Apps

```
sub             = User ID (Guid)
email           = User email
given_name      = First name
family_name     = Last name
role            = Role string
family_group_id = Primary group (new unified name)
tz              = IANA timezone
iat / exp       = Standard claims
```

---

## 3. MyFamilyAuth Data Model

### 3.1 Core Entities

#### `FamilyUser` — The unified human record

```csharp
public class FamilyUser
{
    public Guid     Id                    { get; set; }
    public string   FirstName             { get; set; }
    public string   LastName              { get; set; }
    public string   Email                 { get; set; }   // unique, login key
    public string   PasswordHash          { get; set; }   // BCrypt
    public string   Role                  { get; set; }   // see §3.5
    public bool     IsActive              { get; set; }   // soft delete
    public bool     IsWard                { get; set; }   // cannot log in; managed by Guardian
    public Guid?    GuardianId            { get; set; }   // FK → FamilyUser
    public bool     MustChangePassword    { get; set; }
    public bool     PasswordChangeLocked  { get; set; }   // admin lock
    public string?  AvatarBase64          { get; set; }
    public string?  TimeZoneId            { get; set; }   // IANA timezone
    public DateTime CreatedAt             { get; set; }
    public DateTime? UpdatedAt            { get; set; }
    public DateTime? LastAccessedAt       { get; set; }

    // Navigation
    public FamilyGroup?       PrimaryGroup  { get; set; }
    public Guid?              PrimaryGroupId { get; set; }
    public List<GroupMember>  GroupMemberships { get; set; }
    public List<BuddyGrant>   BuddyGrantsGiven    { get; set; } // as Grantor
    public List<BuddyGrant>   BuddyGrantsReceived { get; set; } // as Grantee
}
```

#### `FamilyGroup` — Family/group with optional hierarchy

```csharp
public class FamilyGroup
{
    public Guid     Id              { get; set; }
    public string   Name            { get; set; }
    public Guid?    ParentGroupId   { get; set; }   // nested groups
    public bool     IsActive        { get; set; }
    public DateTime CreatedAt       { get; set; }
    public string?  EncryptionKeyId { get; set; }   // reference to key store (not raw key)

    // Navigation
    public FamilyGroup?       ParentGroup { get; set; }
    public List<FamilyGroup>  SubGroups   { get; set; }
    public List<GroupMember>  Members     { get; set; }
}
```

#### `GroupMember` — Junction: FamilyUser → FamilyGroup

```csharp
public class GroupMember
{
    public Guid     Id              { get; set; }
    public Guid     FamilyUserId    { get; set; }
    public Guid     FamilyGroupId   { get; set; }
    public string   GroupRole       { get; set; }  // "Member", "Admin", "Owner"
    public bool     IsLimitedMember { get; set; }  // child/ward restrictions
    public DateTime JoinedAt        { get; set; }

    public FamilyUser  User  { get; set; }
    public FamilyGroup Group { get; set; }
}
```

#### `BuddyGrant` — Caregiver/buddy access relationship

```csharp
public class BuddyGrant
{
    public Guid     Id          { get; set; }
    public Guid     GrantorId   { get; set; }  // user granting access to their data
    public Guid     GranteeId   { get; set; }  // caregiver receiving access
    public bool     IsActive    { get; set; }
    public DateTime GrantedAt   { get; set; }
    public DateTime? RevokedAt  { get; set; }

    public FamilyUser Grantor { get; set; }
    public FamilyUser Grantee { get; set; }
}
```

#### `Invitation` — Group invitation with token

```csharp
public class Invitation
{
    public Guid     Id               { get; set; }
    public Guid     FamilyGroupId    { get; set; }
    public string   InviteeEmail     { get; set; }
    public string   Token            { get; set; }  // unique, URL-safe
    public Guid     InvitedByUserId  { get; set; }
    public DateTime CreatedAt        { get; set; }
    public DateTime ExpiresAt        { get; set; }  // default 14 days
    public bool     IsAccepted       { get; set; }
    public DateTime? AcceptedAt      { get; set; }

    public FamilyGroup Group       { get; set; }
    public FamilyUser  InvitedBy   { get; set; }
}
```

#### `RegisteredApp` — Applications that trust MyFamilyAuth

```csharp
public class RegisteredApp
{
    public Guid     Id              { get; set; }
    public string   Name            { get; set; }  // "MyFinances", "MyMedical", etc.
    public string   ClientId        { get; set; }  // app's public identifier
    public string   ClientSecretHash { get; set; } // SHA-256 of shared secret
    public string   AllowedOrigins  { get; set; }  // JSON array of CORS origins
    public bool     IsActive        { get; set; }
    public DateTime RegisteredAt    { get; set; }
}
```

#### `RefreshToken` — Long-lived token for Remember Me

```csharp
public class RefreshToken
{
    public Guid     Id           { get; set; }
    public Guid     FamilyUserId { get; set; }
    public string   Token        { get; set; }  // opaque, SHA-256 hashed
    public string?  AppClientId  { get; set; }  // which app issued it
    public DateTime CreatedAt    { get; set; }
    public DateTime ExpiresAt    { get; set; }
    public bool     IsRevoked    { get; set; }
    public string?  ReplacedBy   { get; set; }  // rotation chain

    public FamilyUser User { get; set; }
}
```

#### `PasswordResetToken`

```csharp
public class PasswordResetToken
{
    public Guid     Id           { get; set; }
    public Guid     FamilyUserId { get; set; }
    public string   Token        { get; set; }  // 6-char numeric or 32-char URL-safe
    public DateTime CreatedAt    { get; set; }
    public DateTime ExpiresAt    { get; set; }  // 15 minutes
    public bool     IsUsed       { get; set; }

    public FamilyUser User { get; set; }
}
```

#### `AuditLog` — Immutable auth event history

```csharp
public class AuditLog
{
    public Guid     Id          { get; set; }
    public DateTime Timestamp   { get; set; }
    public Guid?    FamilyUserId { get; set; }
    public string   Action      { get; set; }  // "Login", "PasswordChanged", "BuddyGranted", etc.
    public string?  IpAddress   { get; set; }
    public string?  AppClientId { get; set; }
    public string?  Notes       { get; set; }
}
```

### 3.2 Roles

| Role String | Description |
|---|---|
| `SuperAdmin` | System-wide admin (you); manages all groups and apps |
| `FamilyAdmin` | Owns a top-level group; can create sub-groups and invite members |
| `GroupAdmin` | Admin of a specific sub-group |
| `User` | Standard member |
| `Ward` | Cannot log in; managed through `FamilyUser.IsWard = true` |

### 3.3 Database Tables

```
FamilyUsers
FamilyGroups
GroupMembers
BuddyGrants
Invitations
RegisteredApps
RefreshTokens
PasswordResetTokens
AuditLogs
```

---

## 4. API Endpoints

### 4.1 Auth (public)

| Method | Route | Description |
|---|---|---|
| POST | `/api/auth/login` | Email + password → JWT + refresh token |
| POST | `/api/auth/refresh` | Rotate refresh token → new JWT |
| POST | `/api/auth/logout` | Revoke refresh token |
| POST | `/api/auth/forgot-password` | Send reset email |
| POST | `/api/auth/reset-password` | Consume reset token + new password |
| POST | `/api/auth/validate` | App-to-app: validate a JWT, return claims |

### 4.2 Users (authenticated)

| Method | Route | Description |
|---|---|---|
| GET | `/api/users/me` | Current user profile |
| PUT | `/api/users/me` | Update profile (name, avatar, timezone) |
| POST | `/api/users/me/change-password` | Self-service password change |

### 4.3 Groups (Family Admin+)

| Method | Route | Description |
|---|---|---|
| GET | `/api/groups` | List groups current user belongs to |
| POST | `/api/groups` | Create a new top-level group |
| GET | `/api/groups/{id}` | Group detail + members |
| PUT | `/api/groups/{id}` | Update group name |
| POST | `/api/groups/{id}/invite` | Send invitation |
| DELETE | `/api/groups/{id}/members/{userId}` | Remove member |

### 4.4 Buddy Grants (authenticated)

| Method | Route | Description |
|---|---|---|
| GET | `/api/buddy-grants` | Grants I've given and received |
| POST | `/api/buddy-grants` | Grant a buddy access to my account |
| DELETE | `/api/buddy-grants/{id}` | Revoke a buddy grant |

### 4.5 Admin (SuperAdmin only)

| Method | Route | Description |
|---|---|---|
| GET | `/api/admin/users` | All users, paginated |
| PUT | `/api/admin/users/{id}` | Edit any user (role, lock, deactivate) |
| POST | `/api/admin/users/{id}/reset-password` | Force password reset |
| GET | `/api/admin/apps` | List registered apps |
| POST | `/api/admin/apps` | Register a new app |
| DELETE | `/api/admin/apps/{id}` | Deactivate an app |
| GET | `/api/admin/audit-log` | Paginated audit log |

### 4.6 App-to-App Token Validation

Other apps (MyFinances, MyMedical, etc.) call this endpoint to validate a user's JWT without needing to share the signing secret:

```
POST /api/auth/validate
Headers: X-App-Client-Id, X-App-Client-Secret
Body: { "token": "eyJ..." }
Response: { "valid": true, "userId": "...", "email": "...", "role": "...", "groupId": "..." }
```

---

## 5. JWT Design

```json
{
  "sub":             "guid-of-family-user",
  "email":           "user@example.com",
  "given_name":      "Tim",
  "family_name":     "Reynolds",
  "role":            "FamilyAdmin",
  "family_group_id": "guid-of-primary-group",
  "tz":              "America/Denver",
  "iss":             "https://auth.twr-apps.com",
  "aud":             "twr-apps",
  "iat":             1234567890,
  "exp":             1234571490
}
```

All apps validate this JWT using the shared public key (RS256 recommended) or the shared symmetric secret (HS256, simpler for now). The `sub` claim becomes the foreign key in every consuming app.

---

## 6. Web UI

MyFamilyAuth is a Blazor WASM app with the same structure as the other MyApps.

### Pages

| Page | Route | Access |
|---|---|---|
| Login | `/login` | Anonymous |
| Forgot Password | `/forgot-password` | Anonymous |
| Reset Password | `/reset-password?token=...` | Anonymous |
| My Profile | `/profile` | Authenticated |
| Change Password | `/profile/change-password` | Authenticated |
| My Groups | `/groups` | Authenticated |
| Group Detail | `/groups/{id}` | Group member |
| Buddy Grants | `/buddy-grants` | Authenticated |
| **Admin: Users** | `/admin/users` | SuperAdmin |
| **Admin: Groups** | `/admin/groups` | SuperAdmin |
| **Admin: Apps** | `/admin/apps` | SuperAdmin |
| **Admin: Audit Log** | `/admin/audit-log` | SuperAdmin |

---

## 7. How Consuming Apps Will Use MyFamilyAuth

### Short-term (Phase 1 — MyFinances)

MyFinances validates tokens **locally** using the shared JWT secret:

1. MyFinances login page POSTs credentials to **MyFamilyAuth API** (`/api/auth/login`)
2. MyFamilyAuth returns a JWT signed with its secret
3. MyFinances stores the JWT in memory (same as today)
4. MyFinances API validates the JWT locally using the shared secret
5. `sub` claim = `FamilyUserId` = the foreign key replacing `AppUser.Id`

MyFinances **keeps its own `AppUser` table** but adds `FamilyUserId` (Guid, nullable) as a link column. During migration, existing users are matched by email.

### Medium-term (Phase 2 — Other Apps)

Same pattern for MyMedical and TheFamilyInfo. Each app:
1. Removes its own login UI
2. Redirects to MyFamilyAuth for login
3. Validates JWTs locally
4. Keeps its own domain data but uses `FamilyUserId` as the FK

### Long-term (Phase 3 — True SSO)

Replace shared-secret validation with RS256 (asymmetric):
- MyFamilyAuth signs JWTs with its **private key**
- All apps validate with MyFamilyAuth's **public key** (available at `/api/auth/.well-known/jwks.json`)
- No shared secret needed

---

## 8. MyFinances Migration Plan

### Step 1 — Add FamilyUserId to AppUser

```sql
ALTER TABLE "AppUsers" ADD COLUMN "FamilyUserId" UUID NULL;
CREATE UNIQUE INDEX ON "AppUsers" ("FamilyUserId") WHERE "FamilyUserId" IS NOT NULL;
```

### Step 2 — Update MyFinances login flow

- Remove MyFinances `AuthController` (or deprecate it)
- MyFinances Web posts login to MyFamilyAuth API
- JWT returned by MyFamilyAuth is used for all subsequent API calls
- MyFinances API validates the JWT using MyFamilyAuth's shared secret

### Step 3 — Migrate existing users

For each `AppUser` in MyFinances, find or create the matching `FamilyUser` in MyFamilyAuth by email. Set `AppUser.FamilyUserId` to the matched ID.

### Step 4 — Switch FK references

Change `PlaidItem.AppUserId` → `PlaidItem.FamilyUserId` (or add FamilyUserId alongside AppUserId during transition).

---

## 9. Project Structure

Following the TWR MyApps SolutionStructure standard (Web only — no Mobile):

```
TWR.MyFamilyAuth/
├── TWR.MyFamilyAuth.sln
├── TWR.MyFamilyAuth.Contracts/     # DTOs, ApiRoutes
├── TWR.MyFamilyAuth.DAL/           # EF Core, PostgreSQL
│   ├── Entities/                   # FamilyUser, FamilyGroup, etc.
│   ├── Interfaces/                 # partial IDataAccess
│   ├── Migrations/
│   ├── MyFamilyAuthDbContext.cs
│   └── DataAccess.*.cs
├── TWR.MyFamilyAuth.API/           # ASP.NET Core 10, pure REST
│   ├── Controllers/                # Auth, Users, Groups, BuddyGrants, Admin
│   ├── AppServices/
│   ├── Services/                   # JwtService, EmailService
│   ├── Models/                     # JwtSettings, AppSettings
│   └── Middleware/
├── TWR.MyFamilyAuth.Web/           # Blazor WASM
│   ├── Pages/                      # Login, Profile, Groups, Admin/*
│   ├── Layout/                     # Dark navbar, AdminLayout
│   ├── Services/                   # AuthService, GroupService, AdminService
│   └── wwwroot/
└── TWR.MyFamilyAuth.Tests/         # xUnit
```

---

## 10. Build Order

| Phase | Work | Complexity |
|---|---|---|
| **Phase 0** | Scaffold solution (same pattern as MyFinances) | Low |
| **Phase 1** | Core auth — FamilyUser CRUD, login, JWT, password reset | Medium |
| **Phase 2** | Groups — FamilyGroup, GroupMember, Invitations | Medium |
| **Phase 3** | Relationships — BuddyGrant, Ward/Guardian | Low |
| **Phase 4** | Admin panel — Users, Groups, Apps, Audit Log | Medium |
| **Phase 5** | RegisteredApp + app-to-app token validation endpoint | Medium |
| **Phase 6** | MyFinances migration — replace AppUser auth with MyFamilyAuth | Medium |

---

## 11. Key Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Token format | HS256 JWT initially, RS256 later | Simple to start; RS256 enables true SSO without shared secrets |
| Session storage | In-memory in Blazor (same as other apps) | Consistent with family |
| Password reset | 6-digit code via email, 15-min expiry | Simpler than URL tokens for mobile compat |
| Avatar storage | Base64 in DB | Consistent with MyMedical/MyMessages; revisit if size becomes an issue |
| Encryption keys | Reference only (not stored in DB) | TheFamilyInfo pattern; actual keys go in a secret store |
| Group hierarchy | Single-level nesting supported | Covers real family needs without unlimited recursion complexity |
| Impersonation | Buddy grant = impersonation; `X-Impersonated-User-Id` header pattern | Reuse MyMedical's proven approach |

---

## 12. Open Questions (Decide Before Building)

1. **Email service** — Which SMTP provider for invitations and password resets? (MyMedical uses Gmail app password; same?)
2. **Port assignment** — What port should MyFamilyAuth API run on locally?
3. **Domain** — Will this ever have a public domain (`auth.twr-apps.com`)?
4. **Existing user migration** — When MyMedical migrates, do users need to re-set their passwords or can we migrate hashes?
5. **Admin seeding** — Should your email (`twreynol@hotmail.com`) be the initial SuperAdmin?

---

## 13. Confirmed Decisions (2026-06-20)

| Question | Decision |
|---|---|
| Email service | Gmail app password — same account as MyMedical (`mymedical32@gmail.com`) |
| API port (HTTPS/HTTP) | 7287 / 5287 |
| Web port (HTTPS/HTTP) | 7288 / 5288 |
| Initial SuperAdmin | `twreynol@hotmail.com` seeded automatically |
| Password migration | Migrate existing BCrypt hashes directly — no re-hashing needed. On email clash across apps, MyMedical wins (it is the only app in BETA) |
| Cloud hosting | Fly.io, same as other apps — no prod config needed yet |
| User management scope | Phase 1: SuperAdmin manages all users. Phase 2: GroupAdmins manage their own group's members. Invitation flow deferred to Phase 2. |
| Mobile | MyFamilyAuth has no Mobile app. Mobile clients authenticate through their associated API (e.g., MyFinances.API calls MyFamilyAuth.API to validate credentials and returns its own app-scoped JWT to the mobile client). |

## 14. AppAccess — Per-App User Authorization (Added 2026-06-20)

### Problem
A universal `FamilyUser` should not automatically have access to every app. `MyFinances` may only be authorized for 2 users; `MyMessages` for the whole extended family.

### Solution: `AppAccess` junction table

`FamilyUser` ↔ `RegisteredApp` with:
- `AppRole` — optional app-specific role (Owner, Viewer, etc.)
- `IsActive` / `RevokedAt` — soft revoke
- `GrantedByUserId` — audit trail of who granted it

### Login flow with AppAccess

1. Client sends `LoginRequest` with `AppClientId` (e.g. `"myfinances"`)
2. MyFamilyAuth looks up `RegisteredApp` by `ClientId`
3. Verifies user credentials (email + BCrypt password)
4. Checks `AppAccess` table for an active row — if missing, returns 401 (same as wrong password — no information leakage)
5. Issues JWT with claims: `sub`, `email`, `role`, `app_client_id`, `app_role`, `family_group_id`, `tz`

### Seeding behavior

- When MyFamilyAuth first starts, it registers itself as a `RegisteredApp` with `ClientId = "myfamilyauth"`
- The SuperAdmin is automatically granted access to it
- When a new app is registered via `POST /api/admin/apps`, all SuperAdmins automatically get access

### Source of truth rule

**MyFamilyAuth owns all user identity.** Sibling apps:
- Keep only a `FamilyUserId` foreign key (no name, email, role, avatar)
- Call `POST /api/auth/validate` with the bearer token to get identity on each request
- Never maintain their own user tables for human users (service accounts are different)

This eliminates the dual-source-of-truth problem entirely.
