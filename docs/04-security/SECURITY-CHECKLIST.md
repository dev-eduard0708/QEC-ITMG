# Security checklist (implementation)

Use during each phase’s DoD. Not a substitute for [THREAT-MODEL.md](THREAT-MODEL.md).

## Authn/z

- [ ] Endpoint has permission policy
- [ ] Resource-level check with tests (positive and IDOR negative)
- [ ] Security incident isolation
- [ ] Privileged actions MFA

## Data

- [ ] UTC timestamps
- [ ] Classification set
- [ ] No secrets in logs
- [ ] History written in same transaction

## HTTP

- [ ] HTTPS
- [ ] Security headers at proxy
- [ ] Rate limits on login/upload
- [ ] OpenAPI not exposing secrets

## Files

- [ ] Allowlist
- [ ] Scan status enforced
- [ ] Authz on download

## Remote

- [ ] No session without ITMG record
- [ ] Consent or unattended policy
- [ ] Engine service account only

## Supply chain

- [ ] Dependencies reviewed
- [ ] No unexpected new services
