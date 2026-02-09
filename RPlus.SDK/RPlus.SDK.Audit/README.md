# RPlus.SDK.Audit

Canonical contract for the kernel audit surface and its Kafka-driven events.

## Principles
- **Domain-first**: every command, query, and event maps directly to the kernel audit application line items.
- **Event-friendly**: the audit service streams `kernel.audit.events`, so the SDK documents both the HTTP/grpc shapes and the Kafka payload.
- **Version-safe**: additive changes only; semver bumps go through `scripts/publish-sdk.ps1`.

## Structure
- **Commands** – requests for creating audit records through the API.
- **Queries** – fetch audit history with filtering by source, time, and severity.
- **Enums** – share the canonical audit types/severity/source values across callers.
- **Events** – describe the Kafka envelope and topic so consumers can handle `AuditEventPayload` messages atomically.

## Usage
1. Reference `RPlus.SDK.Audit` for strongly typed commands/queries and to access the audit enums.
2. When publishing or consuming Kafka events, wrap an `AuditEventPayload` inside the standard `EventEnvelope<T>` and send it to `AuditEventTopics.KernelAuditEvents`.
3. Synchronize SDK versions with `scripts/publish-sdk.ps1` (see `ProjectPath`/`Type`).

## Versioning
- **1.0.00** – initial release of the kernel audit SDK surface.
