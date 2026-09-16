---
adr: 002
title: "Implement separate delegated and workload Microsoft OAuth flows"
status: accepted
date: 2026-09-17
deciders: Amit Singh
scope: lab
domain: security
---

# ADR 002: Implement Separate Delegated and Workload Microsoft OAuth Flows

## Context

Microsoft Graph supports calls made for a signed-in person and calls made by an application. These
identities have different consent, permission, token, and authorization semantics. Demonstrating
only one would leave a central integration concept unexplained.

## Decision

Use Microsoft.Identity.Web for both flows:

- Authorization Code signs in a browser user, validates OIDC state/nonce, redeems the one-time code
  server-side, creates an encrypted local cookie, caches tokens in memory, and acquires delegated
  `User.Read` for `/api/microsoft/me`.
- Client Credentials uses tenant ID, client ID, client secret, and Graph `.default` to acquire an
  application token with admin-consented `User.Read.All` for `/api/microsoft/users`.

Register OIDC only when all required Entra settings exist. Keep cookie authentication as the
default API challenge so protected API routes return 401/403 instead of browser redirects. Only the
explicit login route starts OIDC.

## Alternatives Considered

### Use Client Credentials for every Graph call

Rejected because an application token represents the workload and cannot demonstrate the signed-in
user, delegated consent, or `/me` contract.

### Return access tokens to Swagger or the browser

Rejected because it expands token exposure and is unnecessary; the service can call Graph and
return only normalized resource data.

### Hand-write the authorization and token HTTP exchanges

Rejected because state, nonce, correlation, code redemption, cookie integration, caching, and token
refresh are security-sensitive protocol mechanics already handled by the identity library.

## Consequences

Positive:

- identity intent is explicit: person for `/me`, application for `/users`;
- tokens remain server-side;
- permission and consent failures remain distinguishable;
- the code demonstrates real protocol lifecycle rather than a static bearer string.

Negative:

- setup requires an Entra app registration, delegated consent, application permission, and admin
  consent;
- in-memory token caching and local data-protection keys support only a single lab instance;
- browser interaction prevents full credentialed OAuth automation in the checked-in test suite;
- client-secret authentication would need stronger production secret/credential rotation controls.

## Links

- [Authentication guide](../authentication.md)
- [Architecture](../../ARCHITECTURE.md)
- [Manual OAuth checks](../../tests/manual/README.md)
