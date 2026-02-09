# RPlus Wallet Service

Purpose
- Maintain encrypted balances for each platform user, expose wallet operations via gRPC, and emit wallet-domain events for downstream systems.

Responsibilities
- Provision wallets automatically when `users.user.created.v1` events arrive.
- Execute reserve/commit/cancel/accrue/reverse flows through MediatR command handlers, persisting transactions with optimistic concurrency.
- Validate inbound requests (HMAC signature, replay cache, timestamp window) and encrypt all monetary/metadata payloads before storage.
- Publish wallet events (`WalletTransactionCreated`, `WalletBalanceChanged`, etc.) through the Kafka publisher so other services stay in sync.

Key components
- `RPlus.Wallet.Api` – hosts gRPC endpoints (see `WalletGrpcService`) and wires Redis, Postgres, Kafka, and MassTransit consumers.
- `RPlus.Wallet.Application` – handlers for SDK commands/queries; relies on `IWalletRepository`, `IEncryptionService`, and `IEventPublisher` abstractions.
- `RPlus.Wallet.Infrastructure` – implementation of repositories, encryption (AES-GCM with rotating key ids), outbox dispatcher, and the `UserCreatedConsumer` that now rethrows errors to allow retries.
- `RPlus.Wallet.Persistence` – EF Core DbContext with wallet, transaction, outbox, and processed message tables; exposes `FOR UPDATE` locking for Postgres.
- `RPlus.SDK.Wallet` – contracts for commands, queries, gRPC DTOs, and result objects used by clients.

Event & command flow
1. **Provisioning**: MassTransit rider listens on `user.identity.v1` and invokes `UserCreatedConsumer`, which ensures one wallet per user and fails fast if persistence rejects the change so Kafka redelivers.
2. **Reserve**: gRPC `ReservePoints` converts the proto request into `ReservePointsCommand`. Handler verifies signature/timestamp/replay cache, locks the wallet row, adjusts reserved balance, writes a `WalletTransaction`, commits, and publishes `WalletTransactionCreated`.
3. **Commit/cancel/reverse**: Similar MediatR handlers operate inside repo transactions to mutate encrypted balances and enqueue events.
4. **Read models**: `GetBalance` decrypts wallet state; `GetHistory` streams decrypted transactions with cursor-based pagination.

External dependencies
- PostgreSQL (`WalletDbContext`) for durable wallet + transaction state.
- Redis (optional) for replay cache (`wallet:replay:{RequestId}`) and distributed MassTransit infrastructure; falls back to in-memory cache if not configured.
- Kafka for wallet events and for consuming `UserCreated` envelopes; `Kafka:BootstrapServers` controls connectivity.
- Configuration keys: `ConnectionStrings:DefaultConnection`, `ConnectionStrings:Redis`, `Wallet:HmacSecret`, `Encryption:CurrentKeyId`, `Encryption:Keys:{KeyId}`.

Operational notes
- Wallet rows keep encrypted balances and a version column; EF throws `ConcurrencyException` if concurrent writers race.
- AES keys are loaded at startup; missing keys default to all-zero bytes, so production environments must provide real hex secrets via configuration or KeyVault.
- HMAC secret defaults to `super-secret-env-key`; rotate this via `Wallet:HmacSecret` and update clients to avoid signature failures.
- The gRPC host automatically applies pending EF migrations on startup; ensure the service account can ALTER tables.
- Outbox dispatcher (`WalletOutboxDispatcher`) runs as a hosted service to drain `OutboxMessages` into Kafka; keep it enabled for exactly-once semantics when future handlers adopt the outbox pattern.

Next improvement ideas
1. Move event publishing from handlers into the SDK outbox so the dispatcher, not the handler, sends Kafka payloads, preventing duplicate publish attempts if SaveChanges fails.
2. Replace the simple Redis replay cache with the existing `ProcessedMessages` table to guarantee dedupe even if Redis evicts keys.
3. Surface health/metrics endpoints (Prometheus, readiness) for wallet-specific monitoring beyond the generic ASP.NET health check.
