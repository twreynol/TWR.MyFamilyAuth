# Privacy Policy

**Effective Date:** June 21, 2026  
**Last Updated:** June 21, 2026

## 1. Introduction

This Privacy Policy describes how TWR MyFinances ("we," "us," or "our") collects, uses, and protects information about you when you use our personal finance management application and related services (collectively, the "Service"). This Service is a private, family-use application and is not offered to the general public.

## 2. Information We Collect

### Information You Provide
- **Account credentials:** Your name, email address, and password (stored as a one-way cryptographic hash — we never store your plain-text password).
- **Financial preferences:** Account nicknames, transaction notes, custom categories, and manually entered transactions you choose to record.

### Information Collected Automatically via Plaid
When you connect a bank or credit card account, we use Plaid Inc. ("Plaid") to retrieve your financial data on your behalf. Through this connection we may collect:
- Account names, types, and balances
- Transaction history, including merchant names, dates, and amounts

We do not collect, store, or have access to your bank login credentials. Those are entered directly into Plaid's secure interface and are never transmitted to or stored by this application.

For more information about how Plaid handles your data, please review [Plaid's Privacy Policy](https://plaid.com/legal/#privacy-policy).

### Technical Information
- **Authentication tokens:** Short-lived JSON Web Tokens (JWTs) used to maintain your session. These are stored in your browser's memory and are not persisted to disk.
- **Device trust tokens:** If you choose "Trust this device" during two-factor authentication, a token is stored in your browser's local storage to avoid repeated verification prompts on the same device.
- **Log data:** Server logs may capture IP addresses, request timestamps, and error details for debugging and operational purposes. Log files are retained for up to 30 days.

## 3. How We Use Your Information

We use the information we collect solely to:
- Authenticate you and maintain the security of your account
- Display your linked bank accounts and transaction history
- Enable manual transaction entry, transfers, and reconciliation features
- Improve and troubleshoot the Service

We do not sell, rent, or share your personal or financial information with any third party for marketing or commercial purposes.

## 4. Data Storage and Security

- All data is stored in a private, password-protected PostgreSQL database.
- Data at rest is protected by the security controls of the hosting provider (fly.io).
- Data in transit is encrypted using TLS (HTTPS) for all connections between your browser and our servers, and between our servers and Plaid.
- Access to the application requires authentication. Administrative access to the database is restricted to the application owner.
- Two-factor authentication (2FA) via one-time email codes is available and encouraged.

## 5. Third-Party Services

This application integrates with the following third-party service:

| Service | Purpose | Privacy Policy |
|---------|---------|----------------|
| Plaid Inc. | Bank account connectivity and transaction retrieval | [plaid.com/legal](https://plaid.com/legal/#privacy-policy) |
| fly.io | Application and database hosting | [fly.io/legal/privacy-policy](https://fly.io/legal/privacy-policy/) |

## 6. Data Retention

Financial transaction data retrieved from Plaid is retained in the application database for as long as you maintain a connected account. You may disconnect a bank account at any time through the application, which removes the Plaid access token and stops future data retrieval. Historical transaction data already stored will be removed upon your request.

Account data is retained for as long as your user account exists. You may request deletion of your account and all associated data by contacting the application administrator.

## 7. Your Rights

As a user of this private application, you have the right to:
- **Access** the personal and financial data we hold about you
- **Correct** inaccurate data through the profile and transaction editing features
- **Delete** your account and associated data by contacting the administrator
- **Disconnect** any linked bank account at any time through the application

## 8. Children's Privacy

This Service is intended for use by adults managing household finances. We do not knowingly collect personal information from children under the age of 13.

## 9. Changes to This Policy

We may update this Privacy Policy from time to time. When we do, we will update the "Last Updated" date at the top of this document. Continued use of the Service after any changes constitutes your acceptance of the updated policy.

## 10. Contact

This is a private family application. If you have questions or requests regarding your data, please contact the application administrator directly.
