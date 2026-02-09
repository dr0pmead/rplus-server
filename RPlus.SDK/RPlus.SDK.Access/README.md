# RPlus.SDK.Access

Canonical definition of the Access surface and its Kafka-ready events. It mirrors the Auth surface (checks, registrations, grants) while exposing the topics that keep the kernel event mesh aware of every decision.

## Principles
- **Domain first** – every query/command/envelope reflects the real Access gRPC contract so integrators share the same DTOs.
- **Event-driven** – we declare the topic names and payloads for `kernel.access.*` so consumers know what payloads to deserialize.
- **Version safe** – bump the `Version` in `RPlus.SDK.Access.csproj` and rerun `scripts/publish-sdk.ps1` when adding fields.

## Structure
- **Commands** – mutate the permission registry (register, activate, grant, revoke) and return `Result`/status objects.
- **Queries** – ask the kernel for effective rights, permission lists, integration permissions, and API key validation outcomes.
- **DTOs** – share the shapes for permissions and policy decisions to keep clients in sync with the API.
- **Events** – describe every Kafka topic that Access emits so the audit/event mesh can react.

## Usage
1. Reference `RPlus.SDK.Access` to strongly type Access calls inside your mediator/handler.
2. Serialize the event records from `Events` and publish them on the `AccessEventTopics` constants so the kernel event bus stays accurate.
3. Increment the version with `scripts/publish-sdk.ps1` before shipping (choose `minor`, `medium`, or `major`).

## Versioning
- **1.0.00** – initial event-driven Access SDK release.
