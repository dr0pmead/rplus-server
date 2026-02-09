# RPlus.SDK.Auth

Represents the canonical contract for the Authentication surface inside the RPlus ecosystem.

## 1. Principles
- **Domain first**: Every intent/response is an immutable record describing the Auth service boundary.
- **Lean & explicit**: No runtime logic, only DTOs that describe commands, queries, and statuses.
- **Version safe**: Changes are additive so integrators can align with the publish pipeline.

## 2. Structure
- **Commands** – request/response records for interactive flows (login, OTP, token refresh, etc.).
- **Queries** – read-only actions that answer identity questions.
- **Enums** – shared status codes.

## 3. Usage
1. Reference `RPlus.SDK.Auth` to send strongly typed commands through the Auth service mediator or gRPC layer.
2. Read the records to understand payload shapes, error codes, and required metadata.
3. Keep your client and SDK versions in sync using `scripts/publish-sdk.ps1` (see `ProjectPath`/`Type`).

## 4. Versioning
- **2.0.04** – initial release of the Auth SDK records.
- Follow the existing `publish-sdk.ps1` helper to increment versions.
