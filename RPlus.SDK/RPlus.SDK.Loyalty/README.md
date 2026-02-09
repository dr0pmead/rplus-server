# RPlus.SDK.Loyalty

The definitive domain boundary for the Loyalty service in the RPlus ecosystem.

## 1. Principles
- **Domain First**: Defines domain language only.
- **Strict Immutability**: All contracts are immutable records.
- **Service Agnostic**: Contains no business logic, persistence, or infrastructure.
- **Single Source of Truth**: The Loyalty service implements ONLY what is defined here.

## 2. Structure
- **Commands**: Intent (e.g., `AccruePointsCommand`).
- **Events**: Facts (e.g., `LoyaltyPointsAccrued`).
- **State**: Current snapshot (e.g., `LoyaltyProfile`, `LoyaltyBalance`).
- **Rules**: Declarative configuration (`LoyaltyProgram`, `LoyaltyRule`).
- **Abstractions**: Service obligations (`ILoyaltyRuleEvaluator`, `ILoyaltyStateStore`).
- **Results**: Operation outcomes (`AccruePointsResult`).
- **Errors**: Domain error codes (`LoyaltyErrorCode`).

## 3. Usage
Integrators reference this SDK to:
1.  Understand the Loyalty domain model.
2.  Send Commands to the Loyalty Service.
3.  Subscribe to Loyalty Events.

## 4. Versioning
- **1.1.0**: Initial Production Spec implementation.
- **Compatibility**: Forward compatible; additive changes only for State/Events.
