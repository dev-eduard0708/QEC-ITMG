# ADR-0004: Modular monolith instead of microservices

Date: 2026-09-02
Status: Accepted

## Context

The product spans many domains. Microservices are often proposed for “enterprise” scope.

## Decision

Build a **modular monolith**: module assemblies, schema isolation, architecture tests. One deployable, one database.

## Rationale

- Internal user count does not justify distributed transactions
- Cross-module workflows (ticket → session → change → evidence) are the product
- On-prem operations complexity must stay low
- Modules can be extracted later if a true scale or team boundary appears

## Consequences

- Discipline required: no “big ball of mud”
- Architecture tests and code review enforce boundaries

## Alternatives considered

- Microservices + Kafka: rejected as premature
- Distributed monolith (many services, one DB): worst of both worlds
