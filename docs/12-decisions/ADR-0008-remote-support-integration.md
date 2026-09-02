# ADR-0008: Remote-support integration instead of a custom protocol

Date: 2026-09-02
Status: Accepted

## Context

Remote support is required. Implementing a proprietary remote desktop protocol would be a security and maintenance failure.

## Decision

**Do not** design a remote desktop protocol. Integrate a proven self-hosted engine. QEC ITMG owns authorization, consent, ticket/CI linkage, and audit. The engine owns screen/input transport.

Default engine recommendation: **MeshCentral** ([ADR-0014](ADR-0014-meshcentral-default-engine.md)). Architecture uses `IRemoteSupportEngine` so RustDesk or Guacamole can be adapted.

## Rationale

- Transport security and codecs are specialized
- Attended consent and unattended policy belong with ITSM/GRC
- Engine admin bypass is a threat to be designed out operationally

## Consequences

- Two systems to patch
- Adapter and webhook/polling complexity
- Network isolation of the engine host

## Alternatives considered

- Custom protocol: rejected
- Cloud-only remote vendors as the core: rejected for on-prem control of session policy (may exist later as additional adapter)
