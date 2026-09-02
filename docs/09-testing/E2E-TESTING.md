# End-to-end testing

Playwright against staging or docker-compose.

Critical paths:

1. Employee creates SR, technician comments, resolve, employee sees public only
2. Attended remote: request, consent, (engine mocked), history
3. Change approve SoD (requester cannot approve)
4. IDOR: employee cannot open another’s ticket by GUID
5. Admin role change appears in security audit

Engine may be mocked at adapter in CI; staging uses MeshCentral lab.
