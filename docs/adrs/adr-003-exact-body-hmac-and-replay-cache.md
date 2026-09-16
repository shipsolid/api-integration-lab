---
adr: 003
title: "Authenticate webhooks with exact-body HMAC and replay detection"
status: accepted
date: 2026-09-17
deciders: Amit Singh
scope: lab
domain: security
---

# ADR 003: Authenticate Webhooks with Exact-body HMAC and Replay Detection

## Context

An inbound webhook cannot rely on the API's outbound provider credentials. It must prove that the
sender knew a shared secret, that the message bytes were not changed, and that a captured valid
request cannot be accepted repeatedly.

## Decision

Require these headers:

- `X-Webhook-Timestamp`: Unix seconds;
- `X-Webhook-Signature-256`: lowercase `sha256=` followed by 64 hexadecimal characters.

Calculate HMAC-SHA256 over the UTF-8 bytes of `<unix-seconds>.<exact-body-bytes>`. Reject bodies over
65,536 bytes, timestamps outside five minutes, malformed signatures, fixed-time comparison
failures, and any accepted MAC already present in the expiring in-memory replay cache. Verify before
JSON parsing and never log headers or body.

## Alternatives Considered

### Sign parsed and re-serialized JSON

Rejected because whitespace, escaping, encoding, and property order can change during
serialization; authentication must bind the bytes that actually crossed the wire.

### Use timestamp freshness without a replay cache

Rejected because a captured valid request can be replayed repeatedly during the freshness window.

### Use asymmetric request signatures

Rejected for this local shared-secret lab because certificate/key distribution, discovery,
revocation, and rotation would dominate the learning scope. It may be appropriate for a provider
contract that already defines asymmetric signatures.

## Consequences

Positive:

- tampering and duplicate delivery are detected before domain parsing;
- fixed-time comparison reduces timing leakage;
- memory and work are bounded before authentication;
- tests can reproduce every security decision without a live provider.

Negative:

- sender and receiver clocks must remain within five minutes;
- the shared secret must be distributed out of band;
- accepted deliveries are forgotten on restart;
- multiple replicas would need a shared replay store or a signed unique delivery identifier.

## Links

- [Authentication guide](../authentication.md)
- [Security](../../SECURITY.md)
- [Webhook tests](../../tests/ApiIntegrationLab.UnitTests/Authentication/WebhookSignatureVerifierTests.cs)
