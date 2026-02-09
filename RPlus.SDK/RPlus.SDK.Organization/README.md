# RPlus.SDK.Organization

Canonical contract for the Organization service and its Kafka-aware events.

## Principles
- **Domain-first**: every command, query, and event mirrors the Organization API recorded operations.
- **Event-driven**: that team builds on Kafka envelopes; we provide the payload definition and topic names so integrators can consume or react to `kernel.organization.*` events.
- **Version-safe**: `scripts/publish-sdk.ps1` is the only way to bump the SDK version.

## Structure
- **Commands** – create/update/delete/batch operations.
- **Queries** – grab organization details, tree summaries, or user profiles.
- **DTOs** – typed responses plus leader/member profiles, metadata payloads, and helper types.
- **Events** – encode the Kafka-friendly payload plus topic constants that the kernel service publishes after every mutation.

## Usage
1. Reference `RPlus.SDK.Organization` and send the commands/queries through your mediator or gRPC layer to stay in sync with what the Organization API exposes.
2. When reacting to lifecycle changes, consume the JSON payloads described in `Events/OrganizationEventPayload.cs` from the `OrganizationEventTopics` strings.
3. Keep the package and runtime aligned by running `scripts/publish-sdk.ps1 -ProjectPath RPlus.SDK/RPlus.SDK.Organization/RPlus.SDK.Organization.csproj -Type <minor|medium|major>`.

## Versioning
- **1.0.00** – initial release for the organization surface plus event metadata.
