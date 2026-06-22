# MyFamilyAuth — Help Guide

MyFamilyAuth is the central identity hub for all TWR MyApps. It handles sign-in, user accounts, family groups, and controls which users have access to which apps.

---

## Table of Contents

- [Signing In](#signing-in)
- [Forgot Password](#forgot-password)
- [Two-Factor Authentication](#two-factor-authentication)
- [Dashboard](#dashboard)
- [My Profile](#my-profile)
  - [Profile Info](#profile-info)
  - [Change Password](#change-password)
  - [App Access](#app-access)
  - [Trusted Devices](#trusted-devices)
- [Admin — Users](#admin--users)
- [Admin — Groups](#admin--groups)
- [Admin — Apps](#admin--apps)
- [Admin — App Access](#admin--app-access)
- [Roles & Permissions](#roles--permissions)

---

## Signing In

**Page:** `/login`

Enter your email address and password, then click **Sign In**.

- If your account has two-factor authentication (2FA) enabled, you will be redirected to enter a verification code after signing in.
- If you have forgotten your password, click **Forgot password?** below the sign-in form.
- Your session stays active until you click **Sign Out** in the top navigation bar.

---

## Forgot Password

**Page:** `/forgot-password`

Enter the email address associated with your account and click **Send Reset Link**. You will receive an email with a link to reset your password. The link expires after a short time for security.

---

## Two-Factor Authentication

**Page:** `/verify-two-factor`

If your account or an app you are logging into requires 2FA, you will be prompted to enter a one-time code after your password is accepted. Check your email or authenticator app for the code. You may choose to trust the current device for a period of time to skip this step on future logins.

---

## Dashboard

**Page:** `/` (Home)

The Dashboard is the landing page after you sign in. It confirms you are logged in and provides a starting point for navigating the app. Future versions will display a summary of your account activity here.

---

## My Profile

**Page:** `/profile`

The Profile page lets you manage your personal account information, security settings, and see what apps you have access to.

### Profile Info

Update your **First Name**, **Last Name**, **Email address**, and **Timezone**. You can also upload an **Avatar** (profile picture). Click **Save Changes** to apply updates.

### Change Password

Enter your **Current Password**, then your **New Password** (minimum 8 characters), and confirm it. Click **Update Password**. You will need to use the new password on your next sign-in.

### App Access

A read-only list of the apps you have been granted access to and your role within each app. Contact an administrator if you need access to an app that is not listed here.

### Trusted Devices

When you complete two-factor authentication and choose to trust your device, it is recorded here. Each entry shows the app, IP address, and expiration date.

- Click **Revoke** next to a device to force 2FA on that device the next time you sign in.
- Click **Revoke All** to remove all trusted devices at once.

---

## Admin — Users

**Page:** `/admin/users`  
**Requires:** SuperAdmin or FamilyAdmin role

Manage all user accounts in the system.

- **Search** users by name or email using the search box.
- Click **New User** to create a new account. You must supply a name, email, role, and initial password.
- Click **Edit** on any row to update a user's name, email, role, group assignment, or password options.
- Click **Deactivate** to disable a user's account. Deactivated users cannot sign in. SuperAdmin accounts cannot be deactivated.
- Use the **Previous / Next** buttons to page through large user lists.

**User Roles:**

| Role | Badge Color | Description |
|---|---|---|
| SuperAdmin | Red | Full system access |
| FamilyAdmin | Yellow | Manage users, groups, apps |
| GroupAdmin | Blue | Manage members within their group |
| User | Gray | Standard user access |

---

## Admin — Groups

**Page:** `/admin/groups`  
**Requires:** SuperAdmin or FamilyAdmin role

Groups organize users into families or teams and can be nested (a group can have a parent group).

- Click **New Group** to create a group and optionally assign it a parent group.
- Click **Edit** to rename a group or change its parent.
- Click the **chevron arrow** (›) on any group row to expand it and see its current members.
- Click **Add Member** to add an existing user to a group. Assign them a role (Member, Admin, or Owner) and optionally mark them as a **Limited Member**.
- Click **Remove** next to any member to remove them from the group.

---

## Admin — Apps

**Page:** `/admin/apps`  
**Requires:** SuperAdmin or FamilyAdmin role

Registered Apps are the other TWR MyApps (MyFinances, MyMedical, etc.) that use MyFamilyAuth for sign-in.

- Click **Register New App** to add a new application. You will receive a **Client ID** and **Client Secret** — save the secret immediately, it is only shown once.
- Click **Edit** on any app to update its name, display settings, or supported roles.
- Click **Regenerate Secret** to issue a new client secret (e.g. if the old one is compromised). The old secret immediately stops working.
- The **Client ID** is a stable identifier used in configuration. The **Client Secret** is like a password for the app — keep it secure.

---

## Admin — App Access

**Page:** `/admin/app-access`  
**Requires:** SuperAdmin or FamilyAdmin role

App Access controls which users can sign into which apps, and what role they have within each app.

1. Select an **App** from the dropdown to load its current access list.
2. The table shows each user who has been granted access, along with their assigned role for that app.
3. Click **Grant Access** to add a user to the app. Select the user and their role, then save.
4. Click **Remove** to revoke a user's access to the app.

Changes take effect immediately — the user will not be able to sign into that app on their next login attempt if access is removed.

---

## Roles & Permissions

| Feature | User | GroupAdmin | FamilyAdmin | SuperAdmin |
|---|:---:|:---:|:---:|:---:|
| Sign in & use apps | ✓ | ✓ | ✓ | ✓ |
| Edit own profile | ✓ | ✓ | ✓ | ✓ |
| Manage trusted devices | ✓ | ✓ | ✓ | ✓ |
| Manage group members | | ✓ | ✓ | ✓ |
| Manage all users | | | ✓ | ✓ |
| Manage all groups | | | ✓ | ✓ |
| Register apps | | | ✓ | ✓ |
| Control app access | | | ✓ | ✓ |
| Full system access | | | | ✓ |
