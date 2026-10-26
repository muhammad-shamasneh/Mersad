# Mersad V1 — Security Review

## 1. Purpose

This document records the security controls verified in the Mersad V1 backend and identifies gaps that must be addressed before deployment beyond a trusted local development environment.

This is a code-based review of selected configuration and middleware. It is not a complete security audit or penetration test.

## 2. Current Security Controls

### 2.1 CORS

The API registers a CORS policy named `Dashboard`.

The policy permits requests from:

- `http://localhost:5173`

It allows any request header and HTTP method from that origin.

This supports the local React dashboard. The permitted origin must be updated deliberately for other deployment environments.

CORS is a browser-enforced cross-origin policy. It is not authentication, authorization, or protection against direct HTTP clients.

### 2.2 HTTPS Redirection

The application calls `UseHttpsRedirection()`.

This middleware redirects eligible HTTP requests to HTTPS when an HTTPS endpoint is configured and recognized. Its presence alone does not prove that TLS is correctly configured or enforced in every environment.

Deployment configuration must provide valid TLS and an appropriate HTTP-to-HTTPS policy.

### 2.3 Database Connection

The API reads the `MersadDb` connection string from configuration and uses Entity Framework Core with PostgreSQL.

The inspected `appsettings.json` connection string contains a username and password and targets `localhost`.

These settings appear intended for local development. The actual credential value should not be copied into documentation, committed as a reusable secret, or exposed in logs.

For shared or production environments, use an appropriate secret-management mechanism and a dedicated database account with least-privilege permissions.

No explicit SSL mode was found in the inspected connection string. Database transport security must be configured and verified according to the deployment environment.

### 2.4 Development API Documentation

OpenAPI is mapped only when the application environment is Development.

This is an environment-based restriction in the inspected startup code. Deployment settings must correctly identify the environment.

## 3. Security Gaps Identified

The inspected `Program.cs` does not show explicit registration or middleware for authentication or authorization.

The following capabilities were not verified in the inspected code and must not be assumed to exist:

- API authentication
- Endpoint authorization and role-based access control
- Agent identity and agent-to-server authentication
- Rate limiting and abuse protection
- A production secrets-management strategy
- Explicit database TLS configuration
- A complete input-validation and request-size policy
- Security audit logging
- A documented credential rotation process

These findings are limited to the files and patterns inspected. They do not establish that every capability is absent from every part of the repository.

## 4. Risks

Without API authentication and authorization, accessible endpoints may be callable by clients that should not have access.

A restrictive CORS policy does not prevent non-browser clients from calling an API.

Database credentials stored in a configuration file require care to avoid accidental disclosure. Localhost database configuration must not be carried into a production deployment without review.

HTTPS redirection is not a substitute for correctly configuring and validating TLS.

## 5. Recommended Priorities

Before exposing Mersad beyond local development:

1. Define the threat model and intended deployment boundaries.
2. Add and test authentication for API clients.
3. Apply authorization to protected endpoints.
4. Define how agents authenticate and how their identities are managed.
5. Move environment-specific secrets out of committed configuration.
6. Configure and verify HTTPS and database transport security.
7. Add request validation, size limits, and rate limiting where appropriate.
8. Add security-relevant logging without recording credentials or sensitive telemetry unnecessarily.
9. Test denied access, invalid credentials, malformed input, and database failure scenarios.
10. Review the complete application and deployment configuration before production use.

## 6. V1 Security Posture

Mersad V1 has a local dashboard CORS policy, HTTPS redirection middleware, environment-gated OpenAPI mapping, and configuration-based PostgreSQL connectivity.

These are useful foundations, but they do not constitute a complete security boundary.

V1 should be treated as a development-stage MVP. API-key authentication is implemented, but production-grade secret handling, transport security, deployment hardening, and a complete security assessment remain outstanding.

## Implemented API key authentication

The API uses separate configured Bearer API keys for dashboard and agent access. Dashboard policies protect read endpoints, while agent policies protect device registration and telemetry ingestion. Requests without valid credentials are rejected.

Keys are supplied through environment-based configuration and must not be committed to source control. This is an MVP authentication mechanism, not a replacement for production identity management, user accounts, role administration, TLS configuration, or a full security assessment.
