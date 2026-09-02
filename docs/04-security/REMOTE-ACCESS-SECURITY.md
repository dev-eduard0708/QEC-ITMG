# Remote access security

Related: [../03-modules/REMOTE-SUPPORT.md](../03-modules/REMOTE-SUPPORT.md) · [THREAT-MODEL.md](THREAT-MODEL.md)

## Threats

- Technician connects without ticket/reason
- User social-engineered; no durable consent
- MeshCentral admin equals god mode
- Unattended to a CEO laptop
- Session left open
- File transfer exfiltration
- Engine webhook forger starts sessions

## Controls

1. ITMG authorization before adapter `StartSession`
2. Short-lived engine token / one-time session id
3. Attended: explicit consent artifact
4. Unattended: CI flag + permission + MFA + reason + ticket/change
5. Auto-disconnect idle
6. Record start/end even if engine crashes (ITMG timeout job marks unknown)
7. Webhooks HMAC + allowlist IPs
8. Engine admin UI: jump host / VPN / named admins only
9. File transfer: if engine API reports it, copy to security audit; consider disable inbound file to endpoints unless ticket allows
10. No shared “remote” AD account; technician is identified as ITMG user

## Monitoring

Alert: unattended after hours, failed consents burst, engine admin login, session without ITMG id (should be impossible if engine locked down).
